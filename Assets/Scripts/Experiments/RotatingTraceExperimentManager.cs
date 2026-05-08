using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using TMPro;


public class RotatingTraceExperimentManager : MonoBehaviour
{
    // ─── Inspector ──────────────────────────────────────────────────────────

    [Header("Stimulus")]
    public TrefoilGenerator rotatingTrefoil;   // the single rotating 2D trefoil

    [Header("Marker")]
    public GameObject markerPrefab;            // amber sphere prefab
    [Tooltip("Starting phi (radians) on the trefoil curve.")]
    public float markerStartPhi = 0f;
    [Tooltip("Marker travel speed along phi, in radians/second. The marker traces the full curve every (2 pi / markerSpeed) seconds.")]
    public float markerSpeed = 0.6f;

    [Header("Hand Tracking / Tracing")]
    [Tooltip("VIVE Tracker pose source. Mounted on the participant's hand/finger.")]
    public TrackerPoseProvider trackerProvider;
    public FingerCursorVisualizer fingerCursor; // live cursor (must reference same trackerProvider)
    [Tooltip("Min distance (m) between recorded trace points")]
    public float minTraceDistance = 0.005f;

    [Header("Calibration (reuse existing prefabs)")]
    public TrefoilGenerator calibTrefoil;
    public FourierTrefoil3D  calibModel;

    [Header("UI")]
    public TextMeshProUGUI instructionText;
    public TextMeshProUGUI explainText;

    [Header("Trefoil Parameters")]
    public float R1 = 1.0f;
    public float R2 = 1.5f;
    public float rotationSpeed = 30f;
    public int   rotationDirection = 1;

    [Header("Trials")]
    public int trialsPerBlock = 6;
    public int totalBlocks    = 4;


    // ─── Runtime state ─────────────────────────────────────────────────────

    private GameObject markerObject;

    private List<TraceRecord> records = new List<TraceRecord>();
    private List<Vector3>  currentTrace      = new List<Vector3>();
    private List<float>    currentTracePhi   = new List<float>();   // trefoil orientation when point recorded
    private List<float>    currentTraceMphi  = new List<float>();   // marker phi when point recorded
    private List<float>    currentTraceT     = new List<float>();
    private Vector3 lastTracedPoint;

    // Experimenter-driven flags
    private bool signalStart = false;   // "advance to next phase / start trial"
    private bool signalDone  = false;   // "this trial is finished"
    private bool isRecording = false;   // capture tracker poses into currentTrace

    // Trial-time state
    private float currentMarkerPhi = 0f;
    private bool tracingPhase = false;     // marker is traveling, recording can happen

    // Trial bookkeeping
    private int currentBlock = 0;
    private int currentTrialInBlock = 0;
    private int globalTrialIndex = 0;

    // Status string for the GUI
    private string statusLine = "Idle";


    void Start()
    {
        BuildMarker();
        StartCoroutine(Main());
    }

    void Update()
    {
        // Keyboard shortcuts (mirrors GUI buttons; convenient for the experimenter)
        if (Input.GetKeyDown(KeyCode.Space)) signalStart = true;
        if (Input.GetKeyDown(KeyCode.D))     signalDone  = true;
        if (Input.GetKeyDown(KeyCode.R))     isRecording = !isRecording;

        if (tracingPhase)
        {
            AdvanceMarker();
            UpdateMarkerPosition();
            if (isRecording) SampleTracker();
        }
    }


    // ─── GUI Control Panel ─────────────────────────────────────────────────

    void OnGUI()
    {
        const int W = 280;
        const int H = 240;
        GUILayout.BeginArea(new Rect(10, 10, W, H), GUI.skin.box);

        GUILayout.Label("<b>Experimenter Panel</b>", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Label(statusLine);
        GUILayout.Space(4);

        if (GUILayout.Button("Start / Advance  (Space)"))
            signalStart = true;

        GUI.enabled = tracingPhase;
        string recLabel = isRecording ? "Stop Recording  (R)" : "Begin Recording  (R)";
        if (GUILayout.Button(recLabel))
            isRecording = !isRecording;
        GUI.enabled = true;

        if (GUILayout.Button("Mark Trial Done  (D)"))
            signalDone = true;

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


    // ─── Setup helpers ─────────────────────────────────────────────────────

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
            markerObject.transform.localScale = Vector3.one * 0.06f;
            var mat = new Material(Shader.Find("Custom/BinocularUnlit") ?? Shader.Find("Standard"));
            mat.color = new Color(1f, 0.75f, 0f); // amber
            markerObject.GetComponent<MeshRenderer>().material = mat;
        }
        markerObject.SetActive(false);
    }


    // ─── Main flow ─────────────────────────────────────────────────────────

    IEnumerator Main()
    {
        yield return null;

        HideAll();
        SetStatus("Welcome");

        Say("Welcome.\n\nThe experimenter will guide you through each step.\nPlease let them know when you're ready.");
        yield return WaitSignalStart();

        yield return RunCalibration();

        for (currentBlock = 0; currentBlock < totalBlocks; currentBlock++)
        {
            if (currentBlock > 0)
            {
                Say($"Block {currentBlock} of {totalBlocks} complete.\nTake a short break.\nLet the experimenter know when you're ready to continue.");
                SetStatus($"Break after block {currentBlock}");
                yield return WaitSignalStart();
            }

            for (currentTrialInBlock = 0; currentTrialInBlock < trialsPerBlock; currentTrialInBlock++, globalTrialIndex++)
            {
                yield return RunTrial();
            }
        }

        SaveData();
        Say("All trials complete.\n\nThank you for your participation!");
        SetStatus("Done");
        yield return new WaitForSeconds(3f);
    }


    IEnumerator RunCalibration()
    {
        // Part 1 — watch the rotating 2D trefoil
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

        // Part 2 — explore an example 3D model (auto-rotating)
        Explain("CALIBRATION (2 / 3)\n\nThis is one possible 3D interpretation, shown rotating in space.\nWatch it for a moment.");
        SetStatus("Calibration 2 — 3D model preview");
        if (calibModel != null)
        {
            calibModel.ResetParameters(R1, R2, 0f, Random.Range(0.5f, 1.0f));
            // Auto Z-axis rotation only; do NOT enter manual joystick mode.
            calibModel.SetManualRotationMode(false);
            calibModel.SetVisibility(true);
        }
        yield return WaitSignalStart();
        if (calibModel != null) calibModel.SetVisibility(false);

        // Part 3 — rotating curve again
        if (calibTrefoil != null) calibTrefoil.SetVisibility(true);
        Explain("CALIBRATION (3 / 3)\n\nLook at the curve again.\nWith the 3D shape in mind, can you still perceive it?");
        SetStatus("Calibration 3 — perception confirm");
        yield return WaitSignalStart();
        if (calibTrefoil != null) calibTrefoil.SetVisibility(false);
        Explain("");
    }


    IEnumerator RunTrial()
    {
        // --- Setup ---
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

        // --- Pre-trial pause for instructions ---
        Explain($"Trial {globalTrialIndex + 1}\n\nWatch the amber marker traveling along the curve.\nWhen you're ready, the experimenter will begin recording.\nFollow the marker with your finger to trace the depth you perceive.");
        Say("");
        SetStatus($"Trial {globalTrialIndex + 1}/{totalBlocks * trialsPerBlock} — waiting to begin");

        yield return WaitSignalStart();

        // --- Tracing phase ---
        tracingPhase = true;
        Explain("Trace the marker with your finger.\nSay 'done' when you've finished.");
        if (fingerCursor != null) { fingerCursor.ResetCursor(); fingerCursor.gameObject.SetActive(true); }

        float trialStartTime = Time.time;

        while (!signalDone)
        {
            SetStatus($"Trial {globalTrialIndex + 1} — recording: {(isRecording ? "ON" : "off")} | pts: {currentTrace.Count} | markerPhi: {currentMarkerPhi:F2}");
            yield return null;
        }

        // --- Wrap up ---
        tracingPhase = false;
        isRecording  = false;
        if (fingerCursor != null) fingerCursor.gameObject.SetActive(false);
        if (rotatingTrefoil != null) { rotatingTrefoil.PauseRotation(); rotatingTrefoil.SetVisibility(false); }
        markerObject.SetActive(false);

        float duration = Time.time - trialStartTime;
        records.Add(new TraceRecord(globalTrialIndex, currentBlock, currentTrialInBlock,
                                    R1, R2, rotationSpeed, rotationDirection,
                                    new List<Vector3>(currentTrace),
                                    new List<float>(currentTracePhi),
                                    new List<float>(currentTraceMphi),
                                    new List<float>(currentTraceT),
                                    duration));

        Explain($"Trial complete.\n{currentTrace.Count} points recorded.\nThe experimenter will start the next trial when you're ready.");
        SetStatus($"Trial {globalTrialIndex + 1} done — {currentTrace.Count} pts");
        yield return new WaitForSeconds(0.5f);
        yield return WaitSignalStart();
        Explain("");
    }


    // ─── Per-frame trial logic ─────────────────────────────────────────────

    void AdvanceMarker()
    {
        currentMarkerPhi += markerSpeed * Time.deltaTime;
        if (currentMarkerPhi > Mathf.PI * 2f) currentMarkerPhi -= Mathf.PI * 2f;
    }

    void UpdateMarkerPosition()
    {
        if (rotatingTrefoil == null || markerObject == null) return;

        Vector3 localPoint = rotatingTrefoil.GetPointAt(currentMarkerPhi);
        Vector3 worldPoint = rotatingTrefoil.transform.TransformPoint(localPoint);
        markerObject.transform.position = worldPoint;
    }

    void SampleTracker()
    {
        if (trackerProvider == null) return;
        if (!trackerProvider.TryGetPosition(out Vector3 pos)) return;

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
        currentTracePhi.Add(rotatingTrefoil != null ? rotatingTrefoil.GetCurrentAngle() : 0f);
        currentTraceMphi.Add(currentMarkerPhi);
        currentTraceT.Add(Time.time);
    }


    // ─── Misc helpers ──────────────────────────────────────────────────────

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


    // ─── CSV output ────────────────────────────────────────────────────────

    void SaveData()
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename  = $"RotatingTrace_{timestamp}.csv";
        string path      = Path.Combine(Application.persistentDataPath, filename);

        var csv = new StringBuilder();
        csv.AppendLine("TrialIndex,Block,TrialInBlock,R1,R2,RotationSpeed,RotationDirection," +
                       "PointIndex,WorldX,WorldY,WorldZ,TrefoilAngleDeg,MarkerPhi,TimeStamp,TrialDuration");

        foreach (var rec in records)
        {
            for (int i = 0; i < rec.tracePoints.Count; i++)
            {
                Vector3 p = rec.tracePoints[i];
                float   a = i < rec.traceAngles.Count    ? rec.traceAngles[i]    : 0f;
                float   m = i < rec.traceMarkerPhi.Count ? rec.traceMarkerPhi[i] : 0f;
                float   t = i < rec.traceTimes.Count     ? rec.traceTimes[i]     : 0f;
                csv.AppendLine($"{rec.trialIndex},{rec.block},{rec.trialInBlock}," +
                               $"{rec.R1},{rec.R2},{rec.rotationSpeed},{rec.rotationDirection}," +
                               $"{i},{p.x:F4},{p.y:F4},{p.z:F4},{a:F2},{m:F4},{t:F3},{rec.duration:F2}");
            }
        }

        File.WriteAllText(path, csv.ToString());
        Debug.Log($"[RotatingTrace] Saved {records.Count} trials to {path}");
    }


    private class TraceRecord
    {
        public int    trialIndex, block, trialInBlock;
        public float  R1, R2, rotationSpeed, duration;
        public int    rotationDirection;
        public List<Vector3> tracePoints;
        public List<float>   traceAngles;
        public List<float>   traceMarkerPhi;
        public List<float>   traceTimes;

        public TraceRecord(int idx, int blk, int tib, float r1, float r2, float spd, int dir,
                           List<Vector3> pts, List<float> angles, List<float> mphi, List<float> times, float dur)
        {
            trialIndex = idx; block = blk; trialInBlock = tib;
            R1 = r1; R2 = r2; rotationSpeed = spd; rotationDirection = dir;
            tracePoints = pts; traceAngles = angles; traceMarkerPhi = mphi; traceTimes = times; duration = dur;
        }
    }
}
