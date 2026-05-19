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
    public TrefoilGenerator rotatingTrefoil;   // single rotating 2D trefoil for main trials

    [Header("Trace Trail")]
    [Tooltip("Number of most-recent recorded trace points shown as guide dots (right-eye only). " +
             "These are the same points saved to the CSV.")]
    public int   trailPointCount = 15;
    [Tooltip("Diameter of each trail dot in world metres.")]
    public float trailDotDiameter = 0.010f;
    public Color trailColor = new Color(1f, 0.5f, 0f);  // amber

    [Header("Hand Tracking / Tracing")]
    [Tooltip("VIVE Tracker pose source. Mounted on the participant's hand/finger.")]
    public TrackerPoseProvider trackerProvider;
    public FingerCursorVisualizer fingerCursor;
    [Tooltip("Min distance (m) between recorded trace points.")]
    public float minTraceDistance = 0.001f;

    [Header("Calibration")]
    public TrefoilGenerator calibTrefoil;
    public FourierTrefoil3D  calibModel;
    [Tooltip("Slow Y-axis rotation speed (deg/sec) for the 3D calibration model.")]
    public float calibModelRotationSpeed = 20f;
    [Tooltip("Amplitude for the calibration 3D model. Must be positive to show the perceptually-correct " +
             "2-front/1-back cross-junction configuration that matches the SFM percept.")]
    public float calibAmplitude = 1.0f;

    [Header("3D Trace Calibration")]
    [Tooltip("Number of recorded 3D trace trials (plus one unrecorded practice trial that always runs first).")]
    public int calib3DTraceTrials = 3;

    [Header("UI")]
    public TextMeshProUGUI instructionText;
    public TextMeshProUGUI explainText;

    [Header("Trefoil Parameters")]
    public float R1 = 1.0f;
    public float R2 = 1.5f;
    public float rotationSpeed = 30f;
    public int   rotationDirection = 1;

    [Header("Trials")]
    [Tooltip("Number of practice trials before the main session. Practice data is NOT saved.")]
    public int practiceTrials = 1;
    [Tooltip("Number of main trials. Each trial auto-stops after trialDuration seconds.")]
    public int totalTrials = 3;
    [Tooltip("Each main trial runs for exactly this many seconds once the experimenter presses Start.")]
    public float trialDuration = 60f;
    [Tooltip("Automatically insert a break after this many completed main trials. Set to 0 to disable.")]
    public int autoBreakInterval = 0;


    // ─── Runtime state ─────────────────────────────────────────────────────

    // Trail: pool of small sphere objects (RightEyeOnly), positioned at recent recorded points
    private List<GameObject> trailDots = new List<GameObject>();

    // Main trial trace data
    private List<TraceRecord> records         = new List<TraceRecord>();
    private List<Vector3>     currentTrace    = new List<Vector3>();
    private List<float>       currentTracePhi = new List<float>();
    private List<float>       currentTraceT   = new List<float>();
    private Vector3 lastTracedPoint;

    // 3D calibration trace data
    private List<CalibTraceRecord> calib3DTraceRecords     = new List<CalibTraceRecord>();
    private List<Vector3>          currentCalibTrace        = new List<Vector3>();
    private List<Vector3>          currentCalibGroundTruth  = new List<Vector3>();
    private List<float>            currentCalibPhi          = new List<float>();
    private List<float>            currentCalibRotY         = new List<float>();
    private List<float>            currentCalibT            = new List<float>();
    private Vector3 lastCalibTracedPoint;

    // Experimenter-driven flags
    private bool signalStart  = false;
    private bool signalDone   = false;
    private bool isRecording  = false;
    private bool requestBreak = false;

    // Active phase flags
    private bool tracingPhase = false;   // 2D main / practice trial
    private bool calib3DPhase = false;   // 3D calibration trace

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
        // Keyboard shortcuts (mirror GUI buttons for the experimenter)
        if (Input.GetKeyDown(KeyCode.Space)) signalStart = true;
        if (Input.GetKeyDown(KeyCode.D))     signalDone  = true;
        if (Input.GetKeyDown(KeyCode.R))     isRecording = !isRecording;
        if (Input.GetKeyDown(KeyCode.B))     requestBreak = true;

        if (tracingPhase)
        {
            if (isRecording) SampleTracker();
            UpdateTrailDots(currentTrace);
        }

        if (calib3DPhase)
        {
            if (isRecording) SampleTracker3D();
            UpdateTrailDots(currentCalibTrace);
        }

        if (!tracingPhase && !calib3DPhase)
            HideTrailDots();
    }


    // ─── GUI Control Panel ─────────────────────────────────────────────────

    void OnGUI()
    {
        const int W = 340;
        const int H = 420;
        GUILayout.BeginArea(new Rect(10, 10, W, H), GUI.skin.box);

        GUILayout.Label("<b>Experimenter Panel</b>", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Label(statusLine);
        GUILayout.Space(4);

        if (GUILayout.Button("Start / Advance  (Space)"))
            signalStart = true;

        GUI.enabled = tracingPhase || calib3DPhase;
        string recLabel = isRecording ? "Stop Recording  (R)" : "Begin Recording  (R)";
        if (GUILayout.Button(recLabel))
            isRecording = !isRecording;
        GUI.enabled = true;

        if (GUILayout.Button("Mark Trial Done  (D)"))
            signalDone = true;

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

        Say("Welcome.\n\nThe experimenter will guide you through each step.\nPlease let them know when you're ready.");
        yield return WaitSignalStart();

        Say("");

        yield return RunCalibration();
        yield return RunCalibration3DTrace();

        // ── Practice ──
        if (practiceTrials > 0)
        {
            Explain("PRACTICE\n\nWe'll start with a quick practice trial.\nThis run is not recorded.\nThe experimenter will begin when you're ready.");
            SetStatus("Practice — waiting to begin");
            yield return WaitSignalStart();

            for (int p = 0; p < practiceTrials; p++)
                yield return RunTrialInternal(isPractice: true);
        }

        // ── Main trials ──
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

        // Part 2 — slow Y-axis rotation of the 3D model.
        // calibAmplitude > 0 encodes the correct perceptual interpretation:
        // 2 front cross-junctions and 1 back cross-junction (matching the SFM percept).
        Explain("CALIBRATION (2 / 3)\n\nThis is one possible 3D interpretation of the curve, slowly rotating so you can see its shape.\n(This is not how it moves during the main trials.)");
        SetStatus("Calibration 2 — 3D model preview");
        if (calibModel != null)
        {
            calibModel.ResetParameters(R1, R2, 0f, calibAmplitude);
            calibModel.SetRotationMode(true, calibModelRotationSpeed, 1);
            calibModel.SetVisibility(true);
        }
        yield return WaitSignalStart();
        if (calibModel != null)
        {
            calibModel.SetRotationMode(false);
            calibModel.SetVisibility(false);
        }

        // Part 3 — rotating curve again
        if (calibTrefoil != null) calibTrefoil.SetVisibility(true);
        Explain("CALIBRATION (3 / 3)\n\nLook at the curve again.\nWith the 3D shape in mind, can you still perceive it?");
        SetStatus("Calibration 3 — perception confirm");
        yield return WaitSignalStart();
        if (calibTrefoil != null) calibTrefoil.SetVisibility(false);
        Explain("");
    }


    IEnumerator RunCalibration3DTrace()
    {
        Explain("3D TRACE CALIBRATION\n\nYou will now trace along the rotating 3D shape.\nWe'll start with one practice run, then three recorded trials.\nThe experimenter will begin each trial when you're ready.");
        SetStatus("3D trace calibration — intro");
        yield return WaitSignalStart();

        // One unrecorded practice trial
        yield return RunCalib3DTrialInternal(trialIdx: 0, isPractice: true);

        // Three recorded trials
        for (int i = 0; i < calib3DTraceTrials; i++)
            yield return RunCalib3DTrialInternal(trialIdx: i, isPractice: false);

        Explain("");
    }

    IEnumerator RunCalib3DTrialInternal(int trialIdx, bool isPractice)
    {
        currentCalibTrace.Clear();
        currentCalibGroundTruth.Clear();
        currentCalibPhi.Clear();
        currentCalibRotY.Clear();
        currentCalibT.Clear();
        signalDone   = false;
        isRecording  = false;
        calib3DPhase = false;

        if (calibModel != null)
        {
            calibModel.SetRotationMode(true, calibModelRotationSpeed, 1);
            calibModel.SetVisibility(true);
        }

        string prefix = isPractice ? "PRACTICE 3D TRACE" : $"3D TRACE (Trial {trialIdx + 1} / {calib3DTraceTrials})";
        Explain($"{prefix}\n\nWatch the shape rotate, then trace your finger along the curve.\nFollow the curve for several full rotations.\nSay 'done' when finished.");
        SetStatus(isPractice
            ? "Calib3D practice — waiting to begin"
            : $"Calib3D {trialIdx + 1}/{calib3DTraceTrials} — waiting to begin");
        yield return WaitSignalStart();

        calib3DPhase = true;
        if (fingerCursor != null) { fingerCursor.ResetCursor(); fingerCursor.gameObject.SetActive(true); }

        float startTime = Time.time;

        Explain($"{prefix}\n\nTrace along the curve.\nSay 'done' when you've completed several full rotations.");

        while (!signalDone)
        {
            SetStatus(isPractice
                ? $"Calib3D practice — rec: {(isRecording ? "ON" : "off")} | pts: {currentCalibTrace.Count}"
                : $"Calib3D {trialIdx + 1} — rec: {(isRecording ? "ON" : "off")} | pts: {currentCalibTrace.Count}");
            yield return null;
        }

        calib3DPhase = false;
        isRecording  = false;
        if (fingerCursor != null) fingerCursor.gameObject.SetActive(false);
        if (calibModel   != null) calibModel.SetVisibility(false);

        if (!isPractice)
        {
            float duration = Time.time - startTime;
            calib3DTraceRecords.Add(new CalibTraceRecord(
                trialIdx,
                new List<Vector3>(currentCalibTrace),
                new List<Vector3>(currentCalibGroundTruth),
                new List<float>(currentCalibPhi),
                new List<float>(currentCalibRotY),
                new List<float>(currentCalibT),
                duration));
        }

        string doneLabel = isPractice ? "Practice complete.\n" : $"3D trace trial {trialIdx + 1} complete.\n";
        Explain(doneLabel + "The experimenter will continue when you're ready.");
        SetStatus(isPractice
            ? $"Calib3D practice done — {currentCalibTrace.Count} pts (not saved)"
            : $"Calib3D {trialIdx + 1} done — {currentCalibTrace.Count} pts");
        yield return new WaitForSeconds(0.5f);
        yield return WaitSignalStart();
        Explain("");
    }


    IEnumerator RunTrialInternal(bool isPractice)
    {
        currentTrace.Clear();
        currentTracePhi.Clear();
        currentTraceT.Clear();
        signalDone   = false;
        isRecording  = false;
        tracingPhase = false;

        if (rotatingTrefoil != null)
        {
            rotatingTrefoil.SetParameters(R1, R2, rotationSpeed, rotationDirection);
            rotatingTrefoil.ResumeRotation();
            rotatingTrefoil.SetVisibility(true);
        }

        string statusPrefix = isPractice ? "Practice" : $"Trial {globalTrialIndex + 1}/{totalTrials}";

        Explain((isPractice ? "PRACTICE\n\n" : "")
            + "Observe the rotating curve.\nWhen you're ready, the experimenter will start the trial.\nYou'll have one minute to trace the curve continuously.");
        Say("");
        SetStatus($"{statusPrefix} — waiting to begin");

        yield return WaitSignalStart();

        // Recording starts automatically when the trial begins.
        tracingPhase = true;
        isRecording  = true;
        Explain((isPractice ? "PRACTICE\n\n" : "")
            + "Trace the curve continuously with your finger.\nThe trial will end automatically after one minute.");
        if (fingerCursor != null) { fingerCursor.ResetCursor(); fingerCursor.gameObject.SetActive(true); }

        float trialStartTime = Time.time;
        framesInTracing = 0;

        while (Time.time - trialStartTime < trialDuration && !signalDone)
        {
            framesInTracing++;
            float remaining = trialDuration - (Time.time - trialStartTime);
            SetStatus($"{statusPrefix} — {remaining:F0}s | pts: {currentTrace.Count}");
            yield return null;
        }

        tracingPhase = false;
        isRecording  = false;
        if (fingerCursor    != null) fingerCursor.gameObject.SetActive(false);
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

        Explain((isPractice ? "Practice complete (not saved).\n" : "Trial complete.\n")
            + "The experimenter will continue when you're ready.");
        SetStatus($"{statusPrefix} done — {currentTrace.Count} pts");
        yield return new WaitForSeconds(0.5f);
        yield return WaitSignalStart();
        Explain("");
    }


    // ─── Per-frame sampling ────────────────────────────────────────────────

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

    void SampleTracker3D()
    {
        if (trackerProvider == null || calibModel == null) return;
        if (!trackerProvider.TryGetPosition(out Vector3 pos)) return;

        if (currentCalibTrace.Count == 0)
        {
            lastCalibTracedPoint = pos;
            RecordCalibPoint(pos);
            return;
        }

        if (Vector3.Distance(pos, lastCalibTracedPoint) > minTraceDistance)
        {
            RecordCalibPoint(pos);
            lastCalibTracedPoint = pos;
        }
    }

    void RecordCalibPoint(Vector3 worldPos)
    {
        currentCalibTrace.Add(worldPos);

        Vector3 nearest = calibModel.GetNearestCurveWorldPoint(worldPos, out float phi);
        currentCalibGroundTruth.Add(nearest);
        currentCalibPhi.Add(phi);
        currentCalibRotY.Add(calibModel.GetCurrentRotationY());
        currentCalibT.Add(Time.time);
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
        if (fingerCursor    != null) fingerCursor.gameObject.SetActive(false);
        HideTrailDots();
    }


    // ─── CSV output ────────────────────────────────────────────────────────

    void SaveData()
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        SaveMainData(timestamp);
        SaveCalib3DData(timestamp);
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

    void SaveCalib3DData(string timestamp)
    {
        if (calib3DTraceRecords.Count == 0) return;

        string path = Path.Combine(Application.persistentDataPath, $"RotatingTrace_Calib3D_{timestamp}.csv");
        var csv = new StringBuilder();
        csv.AppendLine("CalibTrialIndex,PointIndex," +
                       "TrackerWorldX,TrackerWorldY,TrackerWorldZ," +
                       "NearestCurveX,NearestCurveY,NearestCurveZ," +
                       "NearestPhi,ModelRotationYDeg,TimeStamp,TrialDuration");

        foreach (var rec in calib3DTraceRecords)
        {
            for (int i = 0; i < rec.trackerPositions.Count; i++)
            {
                Vector3 tp = rec.trackerPositions[i];
                Vector3 gp = i < rec.groundTruthPositions.Count ? rec.groundTruthPositions[i] : Vector3.zero;
                float phi  = i < rec.nearestPhi.Count ? rec.nearestPhi[i] : 0f;
                float ry   = i < rec.modelRotY.Count  ? rec.modelRotY[i]  : 0f;
                float t    = i < rec.times.Count      ? rec.times[i]      : 0f;
                csv.AppendLine($"{rec.trialIndex},{i}," +
                               $"{tp.x:F4},{tp.y:F4},{tp.z:F4}," +
                               $"{gp.x:F4},{gp.y:F4},{gp.z:F4}," +
                               $"{phi:F4},{ry:F2},{t:F3},{rec.duration:F2}");
            }
        }

        File.WriteAllText(path, csv.ToString());
        Debug.Log($"[RotatingTrace] Saved {calib3DTraceRecords.Count} calib-3D trials → {path}");
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

    private class CalibTraceRecord
    {
        public int           trialIndex;
        public List<Vector3> trackerPositions;
        public List<Vector3> groundTruthPositions;
        public List<float>   nearestPhi;
        public List<float>   modelRotY;
        public List<float>   times;
        public float         duration;

        public CalibTraceRecord(int idx, List<Vector3> tracker, List<Vector3> groundTruth,
                                List<float> phi, List<float> rotY, List<float> ts, float dur)
        {
            trialIndex           = idx;
            trackerPositions     = tracker;
            groundTruthPositions = groundTruth;
            nearestPhi           = phi;
            modelRotY            = rotY;
            times                = ts;
            duration             = dur;
        }
    }
}
