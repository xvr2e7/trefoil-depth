using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using TMPro;


public class RotatingTraceExperimentManager : MonoBehaviour
{
    // ─── Inspector ──────────────────────────────────────────────────────────

    [Header("Stimulus")]
    public TrefoilGenerator rotatingTrefoil;   // 2D trefoil used for steps 6 (paused) and 9 (rotating)

    [Header("Trace Trail")]
    [Tooltip("Number of most-recent recorded trace points shown as guide dots (right-eye only). " +
             "These are the same points saved to the CSV.")]
    public int   trailPointCount = 15;
    [Tooltip("Diameter of each trail dot in world metres. Independent of the finger-cursor dot size.")]
    public float trailDotDiameter = 0.006f;
    [Tooltip("Colour and opacity of trail dots. Alpha < 1 renders semi-transparently via the shader's Blend setting.")]
    public Color trailColor = new Color(1f, 0.5f, 0f, 0.35f);  // amber, semi-transparent

    [Header("Hand Tracking / Tracing")]
    [Tooltip("VIVE Tracker pose source. Mounted on the participant's hand/finger.")]
    public TrackerPoseProvider trackerProvider;
    public FingerCursorVisualizer fingerCursor;
    [Tooltip("Min distance (m) between recorded trace points.")]
    public float minTraceDistance = 0.001f;

    [Header("Calibration")]
    public FourierTrefoil3D calibModel;
    [Tooltip("Amplitude for the calibration 3D model (positive = 2-front/1-back configuration).")]
    public float calibAmplitude = 1.0f;

    [Header("Cube Calibration")]
    public CubeCalibrator calibrationCube;
    [Tooltip("Rotation speed (deg/sec) for the rotating cube calibration (Z-axis).")]
    public float cubeRotationSpeed = 30f;

    [Header("UI")]
    public TextMeshProUGUI instructionText;
    public TextMeshProUGUI explainText;

    [Header("Trefoil Parameters")]
    public float R1 = 1.0f;
    public float R2 = 1.5f;
    public float rotationSpeed = 30f;
    public int   rotationDirection = 1;

    [Header("Trials")]
    [Tooltip("Number of trials per calibration step (steps 3, 4, 6, 7, 8).")]
    public int calibTrialCount = 3;
    [Tooltip("Number of practice trials before the main session. Practice data is NOT saved.")]
    public int practiceTrials = 1;
    [Tooltip("Number of main trials.")]
    public int totalTrials = 10;
    [Tooltip("Duration of every recorded trial in seconds (calibration and main). Auto-stops after this time.")]
    public float trialDuration = 60f;
    [Tooltip("Automatically insert a break after this many completed main trials. Set to 0 to disable.")]
    public int autoBreakInterval = 5;


    // ─── Runtime state ─────────────────────────────────────────────────────

    // Trail: pool of small sphere objects (RightEyeOnly), positioned at recent recorded points
    private List<GameObject> trailDots = new List<GameObject>();

    // Main trial trace data
    private List<TraceRecord> records         = new List<TraceRecord>();
    private List<Vector3>     currentTrace    = new List<Vector3>();
    private List<float>       currentTracePhi = new List<float>();
    private List<float>       currentTraceT   = new List<float>();
    private Vector3 lastTracedPoint;

    // Calibration trial trace data (steps 3, 4, 6, 7, 8)
    private List<CalibRecord> calibRecords       = new List<CalibRecord>();
    private List<Vector3>     currentCalibPos     = new List<Vector3>();
    private List<Vector3>     currentCalibNearest = new List<Vector3>(); // 3D trials only
    private List<float>       currentCalibPhi     = new List<float>();   // 3D trials only
    private List<float>       currentCalibRotY    = new List<float>();   // rotating 3D only
    private List<float>       currentCalibT       = new List<float>();
    private List<float>       currentCalibDist    = new List<float>();   // distance-to-target at each calib point
    private Vector3 lastCalibPos;

    // Experimenter-driven flags
    private bool signalStart = false;
    private bool isRecording = false;
    private bool requestBreak = false;
    private bool dataSaved = false;

    // Active phase flags
    private bool tracingPhase     = false;  // main / practice trial (step 9)
    private bool calibTracingPhase = false; // any calibration tracing (steps 3, 4, 6, 7, 8)
    private bool isCalib3D        = false;  // calibModel is the active stimulus → sample nearest pt

    // Trial bookkeeping (main trials only)
    private int globalTrialIndex = 0;

    // Status string for the GUI panel
    private string statusLine = "Idle";

    // Display refresh rate; saved per row
    private float displayRefreshRate = 0f;

    // Per-trial frame counter for measured rate
    private int framesInTracing = 0;


    void Start()
    {
        QueryDisplayRefreshRate();
        BuildTrailDots();

        // Finger cursor stays active for the entire session
        if (fingerCursor != null)
        {
            fingerCursor.ResetCursor();
            fingerCursor.gameObject.SetActive(true);
        }

        StartCoroutine(Main());
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
        // Keyboard shortcuts
        if (Input.GetKeyDown(KeyCode.Space)) signalStart = true;
        if (Input.GetKeyDown(KeyCode.B))     requestBreak = true;

        if (calibTracingPhase)
        {
            if (isRecording) { SampleTrackerCalib(); UpdateTrailDots(currentCalibPos); }
            else             HideTrailDots();
        }
        else if (tracingPhase)
        {
            if (isRecording) { SampleTracker(); UpdateTrailDots(currentTrace); }
            else             HideTrailDots();
        }
        else
        {
            HideTrailDots();
        }
    }


    // ─── GUI Control Panel ─────────────────────────────────────────────────

    void OnGUI()
    {
        const int W = 340;
        const int H = 390;
        GUILayout.BeginArea(new Rect(10, 10, W, H), GUI.skin.box);

        GUILayout.Label("<b>Experimenter Panel</b>", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Label(statusLine);
        GUILayout.Space(4);

        if (GUILayout.Button("Start / Advance  (Space)"))
            signalStart = true;

        string breakLabel = requestBreak ? "Break queued ✓" : "Take Break After Current Trial  (B)";
        if (GUILayout.Button(breakLabel))
            requestBreak = true;

        GUILayout.Space(6);
        GUILayout.Label("<b>VIVE Tracker</b>", new GUIStyle(GUI.skin.label) { richText = true });
        DrawTrackerStatus();

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

    void DrawTrackerStatus()
    {
        if (trackerProvider == null)
        {
            GUILayout.Label("(no provider assigned in inspector)");
            return;
        }

        if (trackerProvider.IsTracked)
        {
            GUILayout.Label($"Bound: {trackerProvider.BoundDeviceName}");
            if (trackerProvider.TryGetPosition(out Vector3 p))
                GUILayout.Label($"Pos: ({p.x:F2}, {p.y:F2}, {p.z:F2})");
            else
                GUILayout.Label("Position read FAILED");
        }
        else
        {
            GUILayout.Label("NOT BOUND");
            GUILayout.Label(trackerProvider.LastScanReport);
        }
    }


    // ─── Trail dots ────────────────────────────────────────────────────────

    void BuildTrailDots()
    {
        var mat = new Material(Shader.Find("Custom/RightEyeOnly"));
        mat.color = trailColor;

        for (int i = 0; i < trailPointCount; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"TrailDot_{i}";
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().material = mat;
            go.transform.localScale = Vector3.one * trailDotDiameter;
            go.SetActive(false);
            trailDots.Add(go);
        }
    }

    void UpdateTrailDots(IList<Vector3> points)
    {
        int n     = Mathf.Min(points.Count, trailPointCount);
        int start = points.Count - n;

        for (int i = 0; i < trailPointCount; i++)
        {
            if (i < n)
            {
                trailDots[i].transform.position = points[start + i];
                trailDots[i].SetActive(true);
            }
            else
            {
                trailDots[i].SetActive(false);
            }
        }
    }

    void HideTrailDots()
    {
        foreach (var dot in trailDots) dot.SetActive(false);
    }


    // ─── Main flow ─────────────────────────────────────────────────────────

    IEnumerator Main()
    {
        yield return null;

        HideAll();
        SetStatus("Welcome");

        // ── Step 1: Welcome ──
        Say("Welcome to the experiment!\n\nYou should see a small green dot that tracks your hand position.\n" +
            "Look for it now, and confirm to the experimenter that you can see it moving with your hand.");
        Explain("");
        yield return WaitSignalStart();
        Say("");

        // ── Step 2: Cube Calibration Tutorial ──
        SetStatus("Cube calibration — tutorial");
        Explain("CUBE CALIBRATION TASK\n\n" +
                "You will trace the visible edges of a wireframe cube with your hand.\n\n" +
                "The cube has two square faces connected by four vertical edges.\n" +
                "Three of those connecting edges are dimmed — you only need to\n" +
                "trace both faces and the one clearly visible connecting edge.\n\n" +
                "At the start of each trial:\n" +
                "  • Position the green marker on any visible vertex or edge\n" +
                "  • Let the experimenter know you're ready\n\n" +
                $"You will then trace the visible edges continuously for {trialDuration:F0} seconds.\n\n" +
                "The marker turns YELLOW if you stray too far from an edge.\n" +
                "Try to keep it GREEN throughout the trial.\n\n" +
                "The experimenter will advance when you're ready to begin.");
        yield return WaitSignalStart();
        Explain("");

        // ── Step 3: Static cube (calibTrialCount trials) ──
        for (int i = 0; i < calibTrialCount; i++)
            yield return RunCubeTracingTrial(i, rotating: false);

        // ── Step 4: Rotating cube (calibTrialCount trials) ──
        for (int i = 0; i < calibTrialCount; i++)
            yield return RunCubeTracingTrial(i, rotating: true);

        // ── Break between steps 4 and 5 ──
        Explain("Take a short break.\nLet the experimenter know when you're ready to continue.");
        SetStatus("Break (after rotating cube)");
        yield return WaitSignalStart();
        Explain("");

        // ── Step 5: Trefoil Calibration Tutorial ──
        SetStatus("Trefoil calibration — tutorial");
        Explain("TREFOIL CALIBRATION TASK\n\n" +
                "You will now trace the curve of a trefoil (a shape with three lobes).\n\n" +
                "At the start of each trial:\n" +
                "  • Place the green marker on any point on the curve\n" +
                "  • Let the experimenter know you're ready\n\n" +
                $"You will then trace the curve continuously for {trialDuration:F0} seconds,\n" +
                "completing as many full cycles as you can.\n\n" +
                "The marker turns YELLOW if you stray too far from the curve.\n" +
                "Try to keep it GREEN throughout the trial.\n\n" +
                "The experimenter will advance when you're ready to begin.");
        yield return WaitSignalStart();
        Explain("");

        // ── Step 6: Static 2D trefoil (calibTrialCount trials) ──
        for (int i = 0; i < calibTrialCount; i++)
            yield return RunTrefoilCalibTrial(i, "trefoil2d_static");

        // ── Step 7: Static 3D trefoil (calibTrialCount trials) ──
        for (int i = 0; i < calibTrialCount; i++)
            yield return RunTrefoilCalibTrial(i, "trefoil3d_static");

        // ── Step 8: Rotating 3D trefoil (calibTrialCount trials) ──
        for (int i = 0; i < calibTrialCount; i++)
            yield return RunTrefoilCalibTrial(i, "trefoil3d_rotating");

        // ── Break between steps 8 and 9 ──
        Explain("Take a short break.\nLet the experimenter know when you're ready to continue.");
        SetStatus("Break (after rotating 3D trefoil)");
        yield return WaitSignalStart();
        Explain("");

        // ── Step 9a: Practice ──
        if (practiceTrials > 0)
        {
            Explain("PRACTICE\n\nWe'll do a quick practice trial with the rotating curve.\n" +
                    "This run is not recorded.\n" +
                    "The experimenter will begin when you're ready.");
            SetStatus("Practice — waiting");
            yield return WaitSignalStart();

            for (int p = 0; p < practiceTrials; p++)
                yield return RunTrialInternal(isPractice: true);
        }

        // ── Step 9b: Main trials ──
        Explain("MAIN EXPERIMENT\n\nThe main experiment is about to begin.\n" +
                "The experimenter will start when you're ready.");
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


    // ─── Cube calibration trial ────────────────────────────────────────────

    IEnumerator RunCubeTracingTrial(int trialIdx, bool rotating)
    {
        ClearCalibLists();
        isCalib3D = false;

        if (calibrationCube == null)
        {
            Debug.LogWarning("[RotatingTrace] No CubeCalibrator assigned, skipping cube trial.");
            yield break;
        }

        calibrationCube.SetVisibility(true);
        if (rotating) calibrationCube.StartRotating(cubeRotationSpeed);

        string label    = rotating ? "ROTATING CUBE" : "STATIC CUBE";
        string trialType = rotating ? "cube_rotating" : "cube_static";
        int    total    = calibTrialCount;

        Explain("Position the green marker on any visible vertex or edge of the cube.\n" +
                "Let the experimenter know when you're ready.");
        SetStatus($"{label} trial {trialIdx + 1}/{total} — waiting");
        yield return WaitSignalStart();
        if (fingerCursor != null)
            fingerCursor.EnableProximityFeedbackCube(calibrationCube);
        calibTracingPhase = true;
        isRecording = true;
        Explain("Trace all visible edges continuously — both square faces and the connecting edge.\n" +
                "Keep the marker GREEN — stay as close to the edges as possible.\n" +
                $"Recording stops automatically after {trialDuration:F0} seconds.");

        float startTime = Time.time;
        while (Time.time - startTime < trialDuration)
        {
            float remaining = trialDuration - (Time.time - startTime);
            SetStatus($"{label} {trialIdx + 1}/{total} — {remaining:F0}s | pts: {currentCalibPos.Count}");
            yield return null;
        }

        if (fingerCursor != null) fingerCursor.DisableProximityFeedback();
        isRecording = false;
        calibTracingPhase = false;
        if (rotating) calibrationCube.StopRotating();
        calibrationCube.SetVisibility(false);

        float duration = Time.time - startTime;
        calibRecords.Add(new CalibRecord(trialType, trialIdx,
            new List<Vector3>(currentCalibPos),
            new List<Vector3>(), new List<float>(), new List<float>(),
            new List<float>(currentCalibT), new List<float>(currentCalibDist), duration));

        Explain("Trial complete.\nThe experimenter will continue when you're ready.");
        SetStatus($"{label} {trialIdx + 1} done — {currentCalibPos.Count} pts");
        yield return new WaitForSeconds(0.5f);
        yield return WaitSignalStart();
        Explain("");
    }


    // ─── Trefoil calibration trial ─────────────────────────────────────────

    IEnumerator RunTrefoilCalibTrial(int trialIdx, string trialType)
    {
        ClearCalibLists();
        int total = calibTrialCount;

        string label;
        switch (trialType)
        {
            case "trefoil2d_static":
                isCalib3D = false;
                label = "STATIC 2D TREFOIL";
                if (rotatingTrefoil != null)
                {
                    rotatingTrefoil.SetParameters(R1, R2, rotationSpeed, rotationDirection);
                    rotatingTrefoil.PauseRotation();
                    rotatingTrefoil.SetVisibility(true);
                }
                break;

            case "trefoil3d_static":
                isCalib3D = true;
                label = "STATIC 3D TREFOIL";
                if (calibModel != null)
                {
                    calibModel.ResetParameters(R1, R2, 0f, calibAmplitude);
                    calibModel.SetAdjustmentEnabled(false);
                    calibModel.SetVisibility(true);
                }
                break;

            case "trefoil3d_rotating":
                isCalib3D = true;
                label = "ROTATING 3D TREFOIL";
                if (calibModel != null)
                {
                    calibModel.ResetParameters(R1, R2, 0f, calibAmplitude);
                    calibModel.SetRotationMode(true, rotationSpeed, rotationDirection, zAxis: true);
                    calibModel.SetVisibility(true);
                }
                break;

            default:
                Debug.LogWarning($"[RotatingTrace] Unknown trefoil calib type: {trialType}");
                yield break;
        }

        Explain("Position the green marker on any point on the curve.\n" +
                "Let the experimenter know when you're ready.");
        SetStatus($"{label} trial {trialIdx + 1}/{total} — waiting");
        yield return WaitSignalStart();

            if (fingerCursor != null)
    {
            if (isCalib3D)
                fingerCursor.EnableProximityFeedback3D(calibModel);
            else
                fingerCursor.EnableProximityFeedback2D(rotatingTrefoil);
    }
        calibTracingPhase = true;
        isRecording = true;
        Explain("Trace the curve continuously, completing as many full cycles as you can.\n" +
        "Keep the marker GREEN — stay as close to the curve as possible.\n" +
        $"Recording stops automatically after {trialDuration:F0} seconds.");

        float startTime = Time.time;
        while (Time.time - startTime < trialDuration)
        {
            float remaining = trialDuration - (Time.time - startTime);
            SetStatus($"{label} {trialIdx + 1}/{total} — {remaining:F0}s | pts: {currentCalibPos.Count}");
            yield return null;
        }

        if (fingerCursor != null) fingerCursor.DisableProximityFeedback();
        isRecording = false;
        calibTracingPhase = false;

        if (trialType == "trefoil2d_static")
        {
            if (rotatingTrefoil != null) rotatingTrefoil.SetVisibility(false);
        }
        else
        {
            if (calibModel != null) { calibModel.SetRotationMode(false); calibModel.SetVisibility(false); }
        }

        float duration = Time.time - startTime;
        calibRecords.Add(new CalibRecord(trialType, trialIdx,
            new List<Vector3>(currentCalibPos),
            isCalib3D ? new List<Vector3>(currentCalibNearest) : new List<Vector3>(),
            isCalib3D ? new List<float>(currentCalibPhi)      : new List<float>(),
            isCalib3D ? new List<float>(currentCalibRotY)     : new List<float>(),
            new List<float>(currentCalibT), new List<float>(currentCalibDist), duration));

        Explain("Trial complete.\nThe experimenter will continue when you're ready.");
        SetStatus($"{label} {trialIdx + 1} done — {currentCalibPos.Count} pts");
        yield return new WaitForSeconds(0.5f);
        yield return WaitSignalStart();
        Explain("");
    }


    // ─── Main / practice trial ─────────────────────────────────────────────

    IEnumerator RunTrialInternal(bool isPractice)
    {
        currentTrace.Clear();
        currentTracePhi.Clear();
        currentTraceT.Clear();
        isRecording  = false;
        tracingPhase = false;

        if (rotatingTrefoil != null)
        {
            rotatingTrefoil.SetParameters(R1, R2, rotationSpeed, rotationDirection);
            rotatingTrefoil.ResumeRotation();
            rotatingTrefoil.SetVisibility(true);
        }

        string statusPrefix   = isPractice ? "Practice" : $"Trial {globalTrialIndex + 1}/{totalTrials}";
        string practicePrefix = isPractice ? "PRACTICE\n\n" : "";

        Explain(practicePrefix +
                "Position the green marker on any point on the curve.\n" +
                "Let the experimenter know when you're ready.");
        Say("");
        SetStatus($"{statusPrefix} — waiting");
        yield return WaitSignalStart();

        tracingPhase = true;
        isRecording  = true;
        Explain(practicePrefix +
                "Trace the curve continuously, completing as many full cycles as you can.\n" +
                $"Recording stops automatically after {trialDuration:F0} seconds.");

        float trialStartTime = Time.time;
        framesInTracing = 0;

        while (Time.time - trialStartTime < trialDuration)
        {
            framesInTracing++;
            float remaining = trialDuration - (Time.time - trialStartTime);
            SetStatus($"{statusPrefix} — {remaining:F0}s | pts: {currentTrace.Count}");
            yield return null;
        }

        isRecording  = false;
        tracingPhase = false;
        if (rotatingTrefoil != null) { rotatingTrefoil.PauseRotation(); rotatingTrefoil.SetVisibility(false); }

        float duration     = Time.time - trialStartTime;
        float measuredRate = duration > 0f ? framesInTracing / duration : 0f;

        if (!isPractice)
        {
            int block        = autoBreakInterval > 0 ? globalTrialIndex / autoBreakInterval : 0;
            int trialInBlock = autoBreakInterval > 0 ? globalTrialIndex % autoBreakInterval : globalTrialIndex;
            records.Add(new TraceRecord(globalTrialIndex, block, trialInBlock,
                                        R1, R2, rotationSpeed, rotationDirection,
                                        new List<Vector3>(currentTrace),
                                        new List<float>(currentTracePhi),
                                        new List<float>(currentTraceT),
                                        duration, displayRefreshRate, measuredRate));
        }

        Explain((isPractice ? "Practice complete (not saved).\n" : "Trial complete.\n") +
                "The experimenter will continue when you're ready.");
        SetStatus($"{statusPrefix} done — {currentTrace.Count} pts");
        yield return new WaitForSeconds(0.5f);
        yield return WaitSignalStart();
        Explain("");
    }


    // ─── Per-frame sampling ────────────────────────────────────────────────

    void SampleTrackerCalib()
    {
        if (trackerProvider == null) return;
        if (!trackerProvider.TryGetPosition(out Vector3 pos)) return;

        if (currentCalibPos.Count == 0)
        {
            lastCalibPos = pos;
            RecordCalibPoint(pos);
            return;
        }

        if (Vector3.Distance(pos, lastCalibPos) > minTraceDistance)
        {
            RecordCalibPoint(pos);
            lastCalibPos = pos;
        }
    }

    void RecordCalibPoint(Vector3 worldPos)
    {
        currentCalibPos.Add(worldPos);
        currentCalibT.Add(Time.time);

        float dist = fingerCursor != null ? fingerCursor.GetDistanceToCurve(worldPos) : float.MaxValue;
        currentCalibDist.Add(dist);

        if (isCalib3D && calibModel != null)
        {
            Vector3 nearest = calibModel.GetNearestCurveWorldPoint(worldPos, out float phi);
            currentCalibNearest.Add(nearest);
            currentCalibPhi.Add(phi);
            currentCalibRotY.Add(calibModel.GetCurrentRotationY());
        }
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
        currentTraceT.Add(Time.time);
    }


    // ─── Misc helpers ──────────────────────────────────────────────────────

    void ClearCalibLists()
    {
        currentCalibPos.Clear();
        currentCalibNearest.Clear();
        currentCalibPhi.Clear();
        currentCalibRotY.Clear();
        currentCalibT.Clear();
        currentCalibDist.Clear();
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
        if (calibModel      != null) calibModel.SetVisibility(false);
        if (calibrationCube != null) calibrationCube.SetVisibility(false);
        HideTrailDots();
        // fingerCursor is always active; do not deactivate it here
    }


    // ─── CSV output ────────────────────────────────────────────────────────

    void SaveData()
    {
        if (dataSaved) return;
        dataSaved = true;
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        SaveMainData(timestamp);
        SaveCalibData(timestamp);
    }

    void SaveMainData(string timestamp)
    {
        string path = Path.Combine(Application.persistentDataPath, $"RotatingTrace_{timestamp}.csv");
        var csv = new StringBuilder();
        csv.AppendLine("TrialIndex,Block,TrialInBlock,R1,R2,RotationSpeed,RotationDirection," +
                       "PointIndex,WorldX,WorldY,WorldZ,TrefoilAngleDeg,TimeStamp," +
                       "TrialDuration,DisplayRefreshRateHz,MeasuredFrameRateHz");

        foreach (var rec in records)
        {
            for (int i = 0; i < rec.tracePoints.Count; i++)
            {
                Vector3 p = rec.tracePoints[i];
                float   a = i < rec.traceAngles.Count ? rec.traceAngles[i] : 0f;
                float   t = i < rec.traceTimes.Count  ? rec.traceTimes[i]  : 0f;
                csv.AppendLine($"{rec.trialIndex},{rec.block},{rec.trialInBlock}," +
                               $"{rec.R1},{rec.R2},{rec.rotationSpeed},{rec.rotationDirection}," +
                               $"{i},{p.x:F4},{p.y:F4},{p.z:F4},{a:F2},{t:F3}," +
                               $"{rec.duration:F2},{rec.displayRefreshRate:F2},{rec.measuredFrameRate:F2}");
            }
        }

        File.WriteAllText(path, csv.ToString());
        Debug.Log($"[RotatingTrace] Saved {records.Count} main trials → {path}");
    }

    void SaveCalibData(string timestamp)
    {
        if (calibRecords.Count == 0) return;

        string path = Path.Combine(Application.persistentDataPath, $"RotatingTrace_Calib_{timestamp}.csv");
        var csv = new StringBuilder();
        float threshold = fingerCursor != null ? fingerCursor.proximityThreshold : 0.02f;
        csv.AppendLine("TrialType,TrialIndex,PointIndex," +
                       "WorldX,WorldY,WorldZ," +
                       "NearestCurveX,NearestCurveY,NearestCurveZ," +
                       "NearestPhi,ModelRotYDeg," +
                       "DistanceToCurve,IsOnCurve,TimeStamp,TrialDuration");

        foreach (var rec in calibRecords)
        {
            bool has3D = rec.nearestPositions.Count > 0;
            for (int i = 0; i < rec.trackerPositions.Count; i++)
            {
                Vector3 p    = rec.trackerPositions[i];
                Vector3 np   = has3D && i < rec.nearestPositions.Count ? rec.nearestPositions[i] : Vector3.zero;
                float   ph   = has3D && i < rec.phi.Count              ? rec.phi[i]              : 0f;
                float   ry   = has3D && i < rec.modelRotY.Count        ? rec.modelRotY[i]        : 0f;
                float   dist = i < rec.distanceToCurve.Count           ? rec.distanceToCurve[i]  : float.MaxValue;
                int     onC  = dist <= threshold ? 1 : 0;
                string  distStr = dist == float.MaxValue ? "" : dist.ToString("F4");
                float   t    = i < rec.times.Count ? rec.times[i] : 0f;
                csv.AppendLine($"{rec.trialType},{rec.trialIndex},{i}," +
                               $"{p.x:F4},{p.y:F4},{p.z:F4}," +
                               $"{np.x:F4},{np.y:F4},{np.z:F4}," +
                               $"{ph:F4},{ry:F2}," +
                               $"{distStr},{onC},{t:F3},{rec.duration:F2}");
            }
        }

        File.WriteAllText(path, csv.ToString());
        Debug.Log($"[RotatingTrace] Saved {calibRecords.Count} calib trials → {path}");
    }


    // ─── Data structures ───────────────────────────────────────────────────

    private class TraceRecord
    {
        public int    trialIndex, block, trialInBlock;
        public float  R1, R2, rotationSpeed, duration;
        public int    rotationDirection;
        public List<Vector3> tracePoints;
        public List<float>   traceAngles;
        public List<float>   traceTimes;
        public float  displayRefreshRate;
        public float  measuredFrameRate;

        public TraceRecord(int idx, int blk, int tib, float r1, float r2, float spd, int dir,
                           List<Vector3> pts, List<float> angles, List<float> times,
                           float dur, float refreshHz, float measuredHz)
        {
            trialIndex = idx; block = blk; trialInBlock = tib;
            R1 = r1; R2 = r2; rotationSpeed = spd; rotationDirection = dir;
            tracePoints = pts; traceAngles = angles; traceTimes = times; duration = dur;
            displayRefreshRate = refreshHz; measuredFrameRate = measuredHz;
        }
    }

    private class CalibRecord
    {
        public string        trialType;
        public int           trialIndex;
        public List<Vector3> trackerPositions;
        public List<Vector3> nearestPositions; // non-empty for 3D trials only
        public List<float>   phi;              // non-empty for 3D trials only
        public List<float>   modelRotY;        // non-empty for rotating 3D only
        public List<float>   times;
        public List<float>   distanceToCurve;  // per-point distance to nearest visible edge/curve
        public float         duration;

        public CalibRecord(string type, int idx,
                           List<Vector3> tracker, List<Vector3> nearest,
                           List<float> phis, List<float> rotY,
                           List<float> ts, List<float> dists, float dur)
        {
            trialType        = type;
            trialIndex       = idx;
            trackerPositions = tracker;
            nearestPositions = nearest;
            phi              = phis;
            modelRotY        = rotY;
            times            = ts;
            distanceToCurve  = dists;
            duration         = dur;
        }
    }
}
