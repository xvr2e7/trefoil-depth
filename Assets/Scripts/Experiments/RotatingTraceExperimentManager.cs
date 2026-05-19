using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using TMPro;

public class RotatingTraceExperimentManager : MonoBehaviour
{
    [Header("Stimulus")]
    public TrefoilGenerator rotatingTrefoil;

    [Header("Marker")]
    public GameObject markerPrefab;
    [Tooltip("Starting phi (radians) on the trefoil curve.")]
    public float markerStartPhi = 0f;
    [Tooltip("Marker travel speed along phi, in radians/second.")]
    public float markerSpeed = 0.6f;
    [Tooltip("Marker sphere diameter in world meters.")]
    public float markerScale = 0.025f;
    [Tooltip("Lateral offset from the curve along the curve normal.")]
    public float markerOffset = 0.2f;

    [Header("Hand Tracking / Tracing")]
    public FingerCursorVisualizer fingerCursor;
    [Tooltip("Min distance (m) between recorded trace points.")]
    public float minTraceDistance = 0.001f;

    [Header("Calibration")]
    public TrefoilGenerator calibTrefoil;
    public FourierTrefoil3D calibModel;
    [Tooltip("Slow Y-axis rotation speed (deg/sec) for the 3D calibration model preview.")]
    public float calibModelRotationSpeed = 20f;

    [Header("UI")]
    public TextMeshProUGUI instructionText;
    public TextMeshProUGUI explainText;

    [Header("Trefoil Parameters")]
    public float R1 = 1.0f;
    public float R2 = 1.5f;
    public float rotationSpeed = 30f;
    public int rotationDirection = 1;

    [Header("Trials")]
    [Tooltip("Number of practice trials before the main session. Practice data is NOT saved.")]
    public int practiceTrials = 1;
    [Tooltip("Number of main trials.")]
    public int totalTrials = 24;
    [Tooltip("Automatically insert a break after this many completed main trials. Set to 0 to disable.")]
    public int autoBreakInterval = 12;

    private GameObject markerObject;
    private List<TraceRecord> records = new List<TraceRecord>();
    private List<Vector3> currentTrace = new List<Vector3>();
    private List<float> currentTracePhi = new List<float>();
    private List<float> currentTraceMphi = new List<float>();
    private List<float> currentTraceT = new List<float>();
    private Vector3 lastTracedPoint;

    private bool signalStart = false;
    private bool signalDone = false;
    private bool isRecording = false;
    private bool requestBreak = false;

    private float currentMarkerPhi = 0f;
    private bool tracingPhase = false;
    private int globalTrialIndex = 0;
    private string statusLine = "Idle";
    private float displayRefreshRate = 0f;
    private int framesInTracing = 0;

    private InputDevice rightHandDevice;
    private bool primaryWasPressed = false;
    private bool secondaryWasPressed = false;

    void Start()
    {
        QueryDisplayRefreshRate();
        InitController();
        BuildMarker();
        StartCoroutine(Main());
    }

    void InitController()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
        if (devices.Count > 0)
            rightHandDevice = devices[0];
    }

    void QueryDisplayRefreshRate()
    {
        var subs = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(subs);
        foreach (var s in subs)
        {
            if (s.TryGetDisplayRefreshRate(out float hz) && hz > 0f)
            {
                displayRefreshRate = hz;
                break;
            }
        }
        if (displayRefreshRate <= 0f)
        {
#pragma warning disable CS0618
            displayRefreshRate = XRDevice.refreshRate;
#pragma warning restore CS0618
        }
        Debug.Log($"[RotatingTrace] Display refresh rate: {displayRefreshRate:F1} Hz");
    }

    void Update()
    {
        
        if (!rightHandDevice.isValid) InitController();

        
        if (Input.GetKeyDown(KeyCode.Space)) signalStart = true;
        if (Input.GetKeyDown(KeyCode.D))     signalDone  = true;
        if (Input.GetKeyDown(KeyCode.R))     isRecording = !isRecording;
        if (Input.GetKeyDown(KeyCode.B))     requestBreak = true;

        
    
        
        if (rightHandDevice.isValid)
        {
            rightHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryBtn);
            rightHandDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondaryBtn);

            if (primaryBtn && !primaryWasPressed)   signalStart = true;
            if (secondaryBtn && !secondaryWasPressed) signalDone = true;

            primaryWasPressed   = primaryBtn;
            secondaryWasPressed = secondaryBtn;
        }

        if (tracingPhase)
        {
            AdvanceMarker();
            UpdateMarkerPosition();
            if (isRecording) SampleFinger();
        }
    }

    void OnGUI()
    {
        const int W = 340;
        const int H = 380;
        GUILayout.BeginArea(new Rect(10, 10, W, H), GUI.skin.box);

        GUILayout.Label("<b>Experimenter Panel</b>",
            new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Label(statusLine);
        GUILayout.Space(4);

        if (GUILayout.Button("Start / Advance  (Space / A button)"))
            signalStart = true;

        GUI.enabled = tracingPhase;
        string recLabel = isRecording
            ? "Stop Recording  (R)"
            : "Begin Recording  (R)";
        if (GUILayout.Button(recLabel))
            isRecording = !isRecording;
        GUI.enabled = true;

        if (GUILayout.Button("Mark Trial Done  (D / B button)"))
            signalDone = true;

        string breakLabel = requestBreak
            ? "Break queued ✓"
            : "Take Break After Current Trial  (B)";
        if (GUILayout.Button(breakLabel))
            requestBreak = true;

        GUILayout.Space(4);
        GUILayout.Label($"Display refresh: {(displayRefreshRate > 0 ? displayRefreshRate.ToString("F1") + " Hz" : "unknown")}");

        GUILayout.Space(6);
        if (GUILayout.Button("Save & Quit"))
        {
            SaveData();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        GUILayout.EndArea();
    }

    void BuildMarker()
    {
        if (markerPrefab != null)
        {
            markerObject = Instantiate(markerPrefab);
        }
        else
        {
            markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(markerObject.GetComponent<Collider>());
            var mat = new Material(
                Shader.Find("Custom/BinocularUnlit") ?? Shader.Find("Standard"));
            mat.color = new Color(1f, 0.75f, 0f);
            markerObject.GetComponent<MeshRenderer>().material = mat;
        }
        markerObject.transform.localScale = Vector3.one * markerScale;
        markerObject.SetActive(false);
    }

    IEnumerator Main()
    {
        yield return null;
        HideAll();
        SetStatus("Welcome");

        Say("Welcome.\n\nThe experimenter will guide you through each step.\nPlease let them know when you're ready.");
        yield return WaitSignalStart();
        Say("");

        yield return RunCalibration();

        if (practiceTrials > 0)
        {
            Explain("PRACTICE\n\nWe'll start with a quick practice trial.\nThis run is not recorded.\nThe experimenter will begin when you're ready.");
            SetStatus("Practice — waiting to begin");
            yield return WaitSignalStart();
            for (int p = 0; p < practiceTrials; p++)
                yield return RunTrialInternal(isPractice: true);
        }

        Explain("That's it for practice.\n\nThe main experiment is about to begin.\nThe experimenter will start when you're ready.");
        SetStatus("Ready for main trials");
        yield return WaitSignalStart();

        for (globalTrialIndex = 0; globalTrialIndex < totalTrials; globalTrialIndex++)
        {
            yield return RunTrialInternal(isPractice: false);

            int completed = globalTrialIndex + 1;
            bool isLast = completed == totalTrials;
            if (isLast) continue;

            bool autoBreak   = autoBreakInterval > 0 && completed % autoBreakInterval == 0;
            bool manualBreak = requestBreak;

            if (autoBreak || manualBreak)
            {
                requestBreak = false;
                Explain("Take a short break.\nLet the experimenter know when you're ready to continue.");
                SetStatus(manualBreak && !autoBreak
                    ? $"Manual break (after trial {completed}/{totalTrials})"
                    : $"Auto break (after trial {completed}/{totalTrials})");
                yield return WaitSignalStart();
                Explain("");
            }
        }

        SaveData();
        Say("All trials complete.\n\nThank you for your participation!");
        Explain("");
        SetStatus("Done");
        yield return new WaitForSeconds(3f);
    }

    IEnumerator RunCalibration()
    {
        if (calibTrefoil != null)
        {
            calibTrefoil.SetParameters(R1, R2, 60f, 1);
            calibTrefoil.SetShaderType(TrefoilGenerator.ShaderType.RightEyeOnly);
            calibTrefoil.ResumeRotation();
            calibTrefoil.SetVisibility(true);
        }
        Explain("CALIBRATION (1 / 3)\n\nObserve the rotating curve.\nLet the experimenter know once you perceive a 3D shape.");
        SetStatus("Calibration 1 — perception check");
        yield return WaitSignalStart();
        if (calibTrefoil != null) calibTrefoil.SetVisibility(false);

        Explain("CALIBRATION (2 / 3)\n\nThis is one possible 3D interpretation of the curve, slowly rotating so you can see its shape.\n(This is not how it actually moves during the trials.)");
        SetStatus("Calibration 2 — 3D model preview");
        if (calibModel != null)
        {
            calibModel.ResetParameters(R1, R2, 0f, Random.Range(0.5f, 1.0f));
            calibModel.SetRotationMode(true, calibModelRotationSpeed, 1);
            calibModel.SetVisibility(true);
        }
        yield return WaitSignalStart();
        if (calibModel != null)
        {
            calibModel.SetRotationMode(false);
            calibModel.SetVisibility(false);
        }

        if (calibTrefoil != null) calibTrefoil.SetVisibility(true);
        Explain("CALIBRATION (3 / 3)\n\nLook at the curve again.\nWith the 3D shape in mind, can you still perceive it?");
        SetStatus("Calibration 3 — perception confirm");
        yield return WaitSignalStart();
        if (calibTrefoil != null) calibTrefoil.SetVisibility(false);
        Explain("");
    }

    IEnumerator RunTrialInternal(bool isPractice)
    {
        currentTrace.Clear();
        currentTracePhi.Clear();
        currentTraceMphi.Clear();
        currentTraceT.Clear();
        currentMarkerPhi = markerStartPhi;
        signalDone   = false;
        isRecording  = false;
        tracingPhase = false;

        if (rotatingTrefoil != null)
        {
            rotatingTrefoil.SetParameters(R1, R2, rotationSpeed, rotationDirection);
            rotatingTrefoil.ResumeRotation();
            rotatingTrefoil.SetVisibility(true);
        }

        markerObject.SetActive(true);
        UpdateMarkerPosition();

        string statusPrefix = isPractice
            ? "Practice"
            : $"Trial {globalTrialIndex + 1}/{totalTrials}";

        string preText = (isPractice ? "PRACTICE\n\n" : "")
            + "Watch the amber marker as it travels along the curve.\n"
            + "When you're ready, the experimenter will begin recording.\n"
            + "Follow the marker with your finger to trace the depth you perceive.";
        Explain(preText);
        Say("");
        SetStatus($"{statusPrefix} — waiting to begin");
        yield return WaitSignalStart();

        tracingPhase = true;
        Explain((isPractice ? "PRACTICE\n\n" : "")
            + "Trace the marker with your finger.\n"
            + "When you are done, notify the experimenter.");
        if (fingerCursor != null)
        {
            fingerCursor.ResetCursor();
            fingerCursor.gameObject.SetActive(true);
        }

        float trialStartTime = Time.time;
        framesInTracing = 0;

        while (!signalDone)
        {
            framesInTracing++;
            SetStatus($"{statusPrefix} — rec: {(isRecording ? "ON" : "off")} | pts: {currentTrace.Count} | markerPhi: {currentMarkerPhi:F2}");
            yield return null;
        }

        tracingPhase = false;
        isRecording  = false;
        if (fingerCursor != null) fingerCursor.gameObject.SetActive(false);
        if (rotatingTrefoil != null)
        {
            rotatingTrefoil.PauseRotation();
            rotatingTrefoil.SetVisibility(false);
        }
        markerObject.SetActive(false);

        float duration = Time.time - trialStartTime;
        float measuredRate = duration > 0f ? framesInTracing / duration : 0f;

        if (!isPractice)
        {
            int block        = autoBreakInterval > 0 ? globalTrialIndex / autoBreakInterval : 0;
            int trialInBlock = autoBreakInterval > 0 ? globalTrialIndex % autoBreakInterval : globalTrialIndex;
            records.Add(new TraceRecord(
                globalTrialIndex, block, trialInBlock,
                R1, R2, rotationSpeed, rotationDirection,
                new List<Vector3>(currentTrace),
                new List<float>(currentTracePhi),
                new List<float>(currentTraceMphi),
                new List<float>(currentTraceT),
                duration, displayRefreshRate, measuredRate));
        }

        Explain((isPractice ? "Practice complete (not saved).\n" : "Trial complete.\n")
            + "Please wait for the experimenter to continue.");
        SetStatus($"{statusPrefix} done — {currentTrace.Count} pts");
        yield return new WaitForSeconds(0.5f);
        yield return WaitSignalStart();
        Explain("");
    }

    void AdvanceMarker()
    {
        currentMarkerPhi += markerSpeed * Time.deltaTime;
        if (currentMarkerPhi > Mathf.PI * 2f)
            currentMarkerPhi -= Mathf.PI * 2f;
    }

    void UpdateMarkerPosition()
    {
        if (rotatingTrefoil == null || markerObject == null) return;
        Vector3 localPoint  = rotatingTrefoil.GetPointAt(currentMarkerPhi);
        Vector3 localNormal = rotatingTrefoil.GetNormalAt(currentMarkerPhi);
        localPoint += localNormal * markerOffset;
        Vector3 worldPoint = rotatingTrefoil.transform.TransformPoint(localPoint);
        markerObject.transform.position = worldPoint;
    }

    void SampleFinger()
    {
        if (fingerCursor == null) return;
        if (!fingerCursor.TryGetIndexTipPosition(out Vector3 pos)) return;

        if (currentTrace.Count == 0)
        {
            lastTracedPoint = pos;
            RecordPoint(pos);
            return;
        }

        if (Vector3.Distance(pos, lastTracedPoint) > minTraceDistance)
        {
            RecordPoint(pos);
            lastTracedPoint = pos;
        }
    }

    void RecordPoint(Vector3 worldPos)
    {
        currentTrace.Add(worldPos);
        currentTracePhi.Add(rotatingTrefoil != null
            ? rotatingTrefoil.GetCurrentAngle() : 0f);
        currentTraceMphi.Add(currentMarkerPhi);
        currentTraceT.Add(Time.time);
    }

    IEnumerator WaitSignalStart()
    {
        signalStart = false;
        yield return new WaitForSeconds(0.3f);
        while (!signalStart) yield return null;
        signalStart = false;
        yield return new WaitForSeconds(0.2f);
    }

    void Say(string text)     { if (instructionText != null) instructionText.text = text; }
    void Explain(string text) { if (explainText     != null) explainText.text     = text; }
    void SetStatus(string s)  { statusLine = s; }

    void HideAll()
    {
        if (rotatingTrefoil != null) rotatingTrefoil.SetVisibility(false);
        if (calibTrefoil    != null) calibTrefoil.SetVisibility(false);
        if (calibModel      != null) calibModel.SetVisibility(false);
        if (markerObject    != null) markerObject.SetActive(false);
        if (fingerCursor    != null) fingerCursor.gameObject.SetActive(false);
    }

    void SaveData()
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename  = $"RotatingTrace_{timestamp}.csv";
        string path      = Path.Combine(Application.persistentDataPath, filename);

        var csv = new StringBuilder();
        csv.AppendLine("TrialIndex,Block,TrialInBlock,R1,R2,RotationSpeed,RotationDirection," +
                       "PointIndex,WorldX,WorldY,WorldZ,TrefoilAngleDeg,MarkerPhi,TimeStamp," +
                       "TrialDuration,DisplayRefreshRateHz,MeasuredFrameRateHz");

        foreach (var rec in records)
        {
            for (int i = 0; i < rec.tracePoints.Count; i++)
            {
                Vector3 p = rec.tracePoints[i];
                float a = i < rec.traceAngles.Count    ? rec.traceAngles[i]    : 0f;
                float m = i < rec.traceMarkerPhi.Count ? rec.traceMarkerPhi[i] : 0f;
                float t = i < rec.traceTimes.Count     ? rec.traceTimes[i]     : 0f;
                csv.AppendLine(
                    $"{rec.trialIndex},{rec.block},{rec.trialInBlock}," +
                    $"{rec.R1},{rec.R2},{rec.rotationSpeed},{rec.rotationDirection}," +
                    $"{i},{p.x:F4},{p.y:F4},{p.z:F4},{a:F2},{m:F4},{t:F3}," +
                    $"{rec.duration:F2},{rec.displayRefreshRate:F2},{rec.measuredFrameRate:F2}");
            }
        }

        File.WriteAllText(path, csv.ToString());
        Debug.Log($"[RotatingTrace] Saved {records.Count} trials to {path}");
    }

    private class TraceRecord
    {
        public int trialIndex, block, trialInBlock;
        public float R1, R2, rotationSpeed, duration;
        public int rotationDirection;
        public List<Vector3> tracePoints;
        public List<float> traceAngles;
        public List<float> traceMarkerPhi;
        public List<float> traceTimes;
        public float displayRefreshRate;
        public float measuredFrameRate;

        public TraceRecord(int idx, int blk, int tib,
                           float r1, float r2, float spd, int dir,
                           List<Vector3> pts, List<float> angles,
                           List<float> mphi, List<float> times,
                           float dur, float refreshHz, float measuredHz)
        {
            trialIndex = idx; block = blk; trialInBlock = tib;
            R1 = r1; R2 = r2; rotationSpeed = spd; rotationDirection = dir;
            tracePoints = pts; traceAngles = angles;
            traceMarkerPhi = mphi; traceTimes = times;
            duration = dur;
            displayRefreshRate = refreshHz;
            measuredFrameRate  = measuredHz;
        }
    }
}