using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using TMPro;

// Ellipse→Circle depth-scale control condition.
//
// A flat ellipse (aspect ratio a) is spun about the line of sight so it reads as a
// circle of diameter D tilted in depth. The participant reaches in real space to
// the perceived nearest and farthest points of the spinning disk; the hand's depth
// excursion (max Z − min Z over a timed reach window) is the PERCEIVED depth. Each
// trial presets the aspect ratio (→ implied slant σ = acos(a)); offline we compare
// perceived depth to the isotropic prediction D·√(1−a²) to recover the depth scale k.
//
// Structure mirrors RotatingTraceExperimentManager (experimenter-driven coroutine,
// OnGUI panel, refresh-rate query, CSV output) with the calibration blocks removed.
public class EllipseCircleExperimentManager : MonoBehaviour
{
    // ─── Inspector ──────────────────────────────────────────────────────────

    [Header("Stimulus")]
    public RotatingEllipseDisk disk;

    [Header("Hand Tracking")]
    [Tooltip("VIVE Tracker pose source mounted on the participant's hand/finger.")]
    public TrackerPoseProvider trackerProvider;
    public FingerCursorVisualizer fingerCursor;
    [Tooltip("Min distance (m) between recorded reach samples.")]
    public float minSampleDistance = 0.001f;

    [Header("Disk Defaults")]
    [Tooltip("Major-axis diameter D in LOCAL units (world = diameter * disk lossyScale).")]
    public float diameter = 1.0f;
    public float rotationSpeed = 90f;

    [Header("Trials")]
    [Tooltip("Aspect ratios a = minor/major to sweep. Avoid a=1 (predicts zero depth).")]
    public float[] aspectRatios = { 0.3f, 0.5f, 0.7f, 0.9f };
    [Tooltip("Spin directions to cross with aspect ratio (1=CCW, -1=CW).")]
    public int[]   directions = { 1, -1 };
    public int   repetitions = 3;
    [Tooltip("Practice trials before the main session. Practice data is NOT saved.")]
    public int   practiceTrials = 1;
    [Tooltip("Length of each reach window in seconds (recording auto-stops after this).")]
    public float reachWindowSeconds = 15f;
    [Tooltip("Insert a break after this many completed main trials. 0 disables.")]
    public int   autoBreakInterval = 6;

    [Header("UI")]
    public TextMeshProUGUI instructionText;
    public TextMeshProUGUI explainText;

    [Header("Trail")]
    [Tooltip("Number of most-recent reach samples shown as guide dots (right-eye only).")]
    public int   trailPointCount = 15;
    public float trailDotDiameter = 0.006f;
    public Color trailColor = new Color(1f, 0.5f, 0f, 0.35f);  // amber, semi-transparent


    // ─── Runtime state ─────────────────────────────────────────────────────

    private List<TrialRecord> records      = new List<TrialRecord>();
    private List<Vector3>     currentTraj   = new List<Vector3>();
    private List<float>       currentTrajT  = new List<float>();
    private Vector3 lastSampledPos;

    private List<GameObject> trailDots = new List<GameObject>();

    private bool signalStart  = false;
    private bool requestBreak = false;
    private bool dataSaved    = false;

    private bool reachingPhase = false;
    private bool isRecording   = false;

    private int   globalTrialIndex = 0;
    private string statusLine = "Idle";

    private float displayRefreshRate = 0f;
    private int   framesInReach = 0;


    void Start()
    {
        QueryDisplayRefreshRate();
        BuildTrailDots();

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

        Debug.Log($"[EllipseCircle] Display refresh rate: {displayRefreshRate:F1} Hz");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) signalStart = true;
        if (Input.GetKeyDown(KeyCode.B))     requestBreak = true;

        if (reachingPhase && isRecording) { SampleTracker(); UpdateTrailDots(currentTraj); }
        else                               HideTrailDots();
    }


    // ─── GUI Control Panel ─────────────────────────────────────────────────

    void OnGUI()
    {
        const int W = 340;
        const int H = 320;
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

        // ── Welcome ──
        Say("Welcome!\n\nYou should see a small blue dot that tracks your hand.\n" +
            "Look for it now and confirm to the experimenter that it moves with your hand.");
        Explain("");
        yield return WaitSignalStart();
        Say("");

        // ── Task instructions ──
        SetStatus("Instructions");
        Explain("DEPTH TASK\n\n" +
                "A flat oval will spin on screen. It is really a CIRCLE tilted in depth,\n" +
                "turning toward and away from you.\n\n" +
                "On each trial:\n" +
                "  • Watch until you see it as a tilted, spinning circle.\n" +
                "  • Then reach out and run your hand through its full depth —\n" +
                "    touch the part that looks NEAREST to you, and the part that\n" +
                "    looks FARTHEST away.\n" +
                "  • Keep exploring its nearest and farthest points until the timer ends.\n\n" +
                "Tell the experimenter when you are ready to begin each trial.");
        yield return WaitSignalStart();
        Explain("");

        // ── Practice ──
        if (practiceTrials > 0)
        {
            Explain("PRACTICE\n\nA few unrecorded practice trials first.\n" +
                    "The experimenter will begin when you're ready.");
            SetStatus("Practice — waiting");
            yield return WaitSignalStart();

            var practice = EllipseCircleTrialGenerator.GeneratePracticeTrials(diameter, rotationSpeed);
            for (int p = 0; p < practiceTrials; p++)
            {
                var t = practice[p % practice.Count];
                yield return RunTrial(t, -1, isPractice: true);
            }
        }

        // ── Main trials ──
        var trials = EllipseCircleTrialGenerator.GenerateMainTrials(
            aspectRatios, directions, repetitions, diameter, rotationSpeed);

        Explain("MAIN EXPERIMENT\n\nThe main session is about to begin.\n" +
                "The experimenter will start when you're ready.");
        SetStatus($"Ready for {trials.Count} main trials");
        yield return WaitSignalStart();

        for (globalTrialIndex = 0; globalTrialIndex < trials.Count; globalTrialIndex++)
        {
            yield return RunTrial(trials[globalTrialIndex], globalTrialIndex, isPractice: false);

            int  completed = globalTrialIndex + 1;
            bool isLast    = completed == trials.Count;
            if (isLast) continue;

            bool autoBreak   = autoBreakInterval > 0 && completed % autoBreakInterval == 0;
            bool manualBreak = requestBreak;

            if (autoBreak || manualBreak)
            {
                requestBreak = false;
                Explain("Take a short break.\nLet the experimenter know when you're ready to continue.");
                SetStatus(manualBreak && !autoBreak
                    ? $"Manual break (after trial {completed}/{trials.Count})"
                    : $"Auto break (after trial {completed}/{trials.Count})");
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


    // ─── Single trial ──────────────────────────────────────────────────────

    IEnumerator RunTrial(EllipseCircleTrial trial, int trialIndex, bool isPractice)
    {
        currentTraj.Clear();
        currentTrajT.Clear();
        isRecording   = false;
        reachingPhase = false;

        if (disk != null)
        {
            disk.SetParameters(diameter, trial.aspectRatio, rotationSpeed, trial.direction);
            disk.ResumeRotation();
            disk.SetVisibility(true);
        }

        string statusPrefix   = isPractice ? "Practice" : $"Trial {trialIndex + 1}";
        string practicePrefix  = isPractice ? "PRACTICE\n\n" : "";

        Explain(practicePrefix +
                "Watch the spinning shape until it looks like a tilted circle.\n" +
                "When ready, tell the experimenter and begin reaching.");
        Say("");
        SetStatus($"{statusPrefix} — waiting (a={trial.aspectRatio:F2})");
        yield return WaitSignalStart();

        reachingPhase = true;
        isRecording   = true;
        framesInReach = 0;
        Explain(practicePrefix +
                "Reach through the disk's depth — touch its NEAREST and FARTHEST points,\n" +
                $"exploring back and forth until the timer ends ({reachWindowSeconds:F0}s).");

        float startTime = Time.time;
        while (Time.time - startTime < reachWindowSeconds)
        {
            framesInReach++;
            float remaining = reachWindowSeconds - (Time.time - startTime);
            SetStatus($"{statusPrefix} — {remaining:F0}s | samples: {currentTraj.Count}");
            yield return null;
        }

        isRecording   = false;
        reachingPhase = false;
        if (disk != null) { disk.PauseRotation(); disk.SetVisibility(false); }

        float duration     = Time.time - startTime;
        float measuredRate = duration > 0f ? framesInReach / duration : 0f;

        if (!isPractice)
        {
            // Front = nearest in depth (min world Z), Back = farthest (max world Z).
            // NOTE: assumes the scene camera faces +Z. If it looks down −Z, swap.
            Vector3 front = Vector3.zero, back = Vector3.zero;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in currentTraj)
            {
                if (p.z < minZ) { minZ = p.z; front = p; }
                if (p.z > maxZ) { maxZ = p.z; back  = p; }
            }
            float perceivedDepth = (currentTraj.Count > 0) ? (maxZ - minZ) : 0f;

            float worldDiameter = disk != null ? disk.GetWorldDiameter()      : diameter;
            float predicted     = disk != null ? disk.GetImpliedDepthExtent() : 0f;
            float slantDeg      = disk != null ? disk.GetImpliedSlantDeg()
                                               : Mathf.Acos(Mathf.Clamp01(trial.aspectRatio)) * Mathf.Rad2Deg;

            records.Add(new TrialRecord(
                trialIndex, trial.configurationId, trial.repetitionNumber,
                trial.aspectRatio, slantDeg, worldDiameter, predicted,
                trial.rotationSpeed, trial.direction, duration,
                front, back, perceivedDepth,
                displayRefreshRate, measuredRate,
                new List<Vector3>(currentTraj), new List<float>(currentTrajT)));
        }

        Explain((isPractice ? "Practice complete (not saved).\n" : "Trial complete.\n") +
                "The experimenter will continue when you're ready.");
        SetStatus($"{statusPrefix} done — {currentTraj.Count} samples");
        yield return new WaitForSeconds(0.5f);
        yield return WaitSignalStart();
        Explain("");
    }


    // ─── Per-frame sampling ────────────────────────────────────────────────

    void SampleTracker()
    {
        if (trackerProvider == null) return;
        if (!trackerProvider.TryGetPosition(out Vector3 pos)) return;

        if (currentTraj.Count == 0)
        {
            lastSampledPos = pos;
            RecordSample(pos);
            return;
        }

        if (Vector3.Distance(pos, lastSampledPos) > minSampleDistance)
        {
            RecordSample(pos);
            lastSampledPos = pos;
        }
    }

    void RecordSample(Vector3 worldPos)
    {
        currentTraj.Add(worldPos);
        currentTrajT.Add(Time.time);
    }


    // ─── Helpers ───────────────────────────────────────────────────────────

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
        if (disk != null) disk.SetVisibility(false);
        HideTrailDots();
        // fingerCursor stays active for the whole session.
    }


    // ─── CSV output ────────────────────────────────────────────────────────

    void SaveData()
    {
        if (dataSaved) return;
        dataSaved = true;
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        SaveSummary(timestamp);
        SaveTrajectory(timestamp);
    }

    void SaveSummary(string timestamp)
    {
        string path = Path.Combine(Application.persistentDataPath, $"EllipseCircle_{timestamp}.csv");
        var csv = new StringBuilder();
        csv.AppendLine("TrialIndex,ConfigId,RepetitionNumber,AspectRatio,ImpliedSlantDeg," +
                       "WorldDiameter,PredictedDepth,RotationSpeed,Direction,ReachDuration," +
                       "FrontX,FrontY,FrontZ,BackX,BackY,BackZ,PerceivedDepthZ," +
                       "DisplayRefreshRateHz,MeasuredFrameRateHz,Timestamp");

        foreach (var r in records)
        {
            csv.AppendLine(
                $"{r.trialIndex},{r.configId},{r.repetitionNumber},{r.aspectRatio:F4},{r.slantDeg:F2}," +
                $"{r.worldDiameter:F4},{r.predictedDepth:F4},{r.rotationSpeed:F1},{r.direction},{r.duration:F2}," +
                $"{r.front.x:F4},{r.front.y:F4},{r.front.z:F4}," +
                $"{r.back.x:F4},{r.back.y:F4},{r.back.z:F4},{r.perceivedDepth:F4}," +
                $"{r.displayRefreshRate:F2},{r.measuredFrameRate:F2},{timestamp}");
        }

        File.WriteAllText(path, csv.ToString());
        Debug.Log($"[EllipseCircle] Saved {records.Count} trials → {path}");
    }

    void SaveTrajectory(string timestamp)
    {
        string path = Path.Combine(Application.persistentDataPath, $"EllipseCircle_Traj_{timestamp}.csv");
        var csv = new StringBuilder();
        csv.AppendLine("TrialIndex,PointIndex,WorldX,WorldY,WorldZ,TimeStamp");

        foreach (var r in records)
        {
            for (int i = 0; i < r.traj.Count; i++)
            {
                Vector3 p = r.traj[i];
                float   t = i < r.trajT.Count ? r.trajT[i] : 0f;
                csv.AppendLine($"{r.trialIndex},{i},{p.x:F4},{p.y:F4},{p.z:F4},{t:F3}");
            }
        }

        File.WriteAllText(path, csv.ToString());
        Debug.Log($"[EllipseCircle] Saved trajectory for {records.Count} trials → {path}");
    }


    // ─── Data structure ────────────────────────────────────────────────────

    private class TrialRecord
    {
        public int    trialIndex, configId, repetitionNumber, direction;
        public float  aspectRatio, slantDeg, worldDiameter, predictedDepth, rotationSpeed, duration;
        public Vector3 front, back;
        public float  perceivedDepth, displayRefreshRate, measuredFrameRate;
        public List<Vector3> traj;
        public List<float>   trajT;

        public TrialRecord(int idx, int cfg, int rep, float a, float slant, float worldD, float pred,
                           float spd, int dir, float dur, Vector3 f, Vector3 b, float perc,
                           float refreshHz, float measuredHz, List<Vector3> trajectory, List<float> trajTimes)
        {
            trialIndex = idx; configId = cfg; repetitionNumber = rep;
            aspectRatio = a; slantDeg = slant; worldDiameter = worldD; predictedDepth = pred;
            rotationSpeed = spd; direction = dir; duration = dur;
            front = f; back = b; perceivedDepth = perc;
            displayRefreshRate = refreshHz; measuredFrameRate = measuredHz;
            traj = trajectory; trajT = trajTimes;
        }
    }
}
