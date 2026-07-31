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
// circle of diameter D tilted in depth. The participant TRACES the whole rim of that
// perceived circle in real space, following it as it turns — so the recorded hand path
// is the perceived 3D shape, not just its depth extremes.
//
// Each sample stores the disk's spin angle, so offline (and in the trial summary here)
// the trace is de-rotated into the disk's own frame:
//     p_local = R_z(−DiskAngleDeg) · Inverse(Q0) · (p_world − diskPos)
// where Q0 is the disk's un-spun world rotation. In that frame the perceived circle is
// stationary: it lies in a plane through the major (X) axis with z = tan(σ)·y, so the
// trace yields both a depth extent (Z range) and a fitted slant σ̂ = atan|m|. Each trial
// presets the aspect ratio (→ implied slant σ = acos(a)); offline we compare the traced
// depth to the isotropic prediction D·√(1−a²) to recover the depth scale k.
//
// OLD MEASURE (superseded, code left commented below): the participant reached only to
// the perceived nearest and farthest points and the depth was the hand's world max Z −
// min Z over the reach window.
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
    [Tooltip("Min distance (m) between recorded trace samples.")]
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
    [Tooltip("Length of each tracing window in seconds (recording auto-stops after this). " +
             "Long enough for several full laps of the rim — cf. 30 s in RotatingTrace.")]
    public float reachWindowSeconds = 15f;
    [Tooltip("Insert a break after this many completed main trials. 0 disables.")]
    public int   autoBreakInterval = 6;

    [Header("UI")]
    public TextMeshProUGUI instructionText;
    public TextMeshProUGUI explainText;

    [Header("Trail")]
    [Tooltip("Number of most-recent trace samples shown as guide dots (right-eye only).")]
    public int   trailPointCount = 15;
    public float trailDotDiameter = 0.006f;
    public Color trailColor = new Color(1f, 0.5f, 0f, 0.35f);  // amber, semi-transparent


    // ─── Runtime state ─────────────────────────────────────────────────────

    private List<TrialRecord> records           = new List<TrialRecord>();
    private List<Vector3>     currentTraj       = new List<Vector3>();
    private List<float>       currentTrajT      = new List<float>();
    private List<float>       currentTrajAngle  = new List<float>();   // disk spin angle at each sample
    private Vector3 lastSampledPos;

    // Disk pose held fixed for the trial, used to de-rotate the trace (see ToDiskFrame).
    private Vector3    trialDiskPos     = Vector3.zero;
    private Quaternion trialDiskBaseRot = Quaternion.identity;

    private List<GameObject> trailDots = new List<GameObject>();

    private bool signalStart  = false;
    private bool requestBreak = false;
    private bool dataSaved    = false;

    private bool tracingPhase = false;
    private bool isRecording  = false;

    private int   globalTrialIndex = 0;
    private string statusLine = "Idle";

    private float displayRefreshRate = 0f;
    private int   framesInTrace = 0;


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

        if (tracingPhase && isRecording) { SampleTracker(); UpdateTrailDots(currentTraj); }
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
        Explain("SHAPE TRACING TASK\n\n" +
                "A spinning oval will appear in front of you. Watch it for a moment —\n" +
                "it will start to look like a solid CIRCLE tilted in depth:\n" +
                "part of it closer to you, part of it farther away.\n\n" +
                "Your job is to TRACE that tilted circle in the air with your fingertip:\n" +
                "  1. Put your fingertip on the rim of the circle as you see it in space.\n" +
                "  2. Follow the rim all the way around, keeping up with it as it turns.\n" +
                "  3. Keep going round and round until the timer ends.\n\n" +
                "Remember:\n" +
                "  • Move your hand in all three directions — reach toward yourself on the\n" +
                "    near part of the rim and away from yourself on the far part.\n" +
                "  • Trace the shape you SEE, not a flat oval on a screen.\n" +
                "  • If the near and far sides seem to trade places while you watch,\n" +
                "    that's normal. Keep tracing the circle as you see it right now.\n\n" +
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
        currentTrajAngle.Clear();
        isRecording  = false;
        tracingPhase = false;

        if (disk != null)
        {
            disk.SetParameters(diameter, trial.aspectRatio, rotationSpeed, trial.direction);
            disk.ResumeRotation();
            disk.SetVisibility(true);

            // The disk spins about its local Z; everything else about its pose is fixed
            // for the trial, so capture the un-spun basis once and reuse it per sample.
            trialDiskPos     = disk.transform.position;
            trialDiskBaseRot = disk.transform.rotation * Quaternion.Euler(0f, 0f, -disk.GetCurrentAngle());
        }

        string statusPrefix   = isPractice ? "Practice" : $"Trial {trialIndex + 1}";
        string practicePrefix  = isPractice ? "PRACTICE\n\n" : "";

        Explain(practicePrefix +
                "Watch the spinning shape until it looks like a tilted circle.\n" +
                "When ready, tell the experimenter and begin tracing.");
        Say("");
        SetStatus($"{statusPrefix} — waiting (a={trial.aspectRatio:F2})");
        yield return WaitSignalStart();

        tracingPhase = true;
        isRecording  = true;
        framesInTrace = 0;
        Explain(practicePrefix +
                "Trace the rim of the tilted circle with your fingertip — follow it\n" +
                $"around as it turns, lap after lap, until the timer ends ({reachWindowSeconds:F0}s).");

        float startTime = Time.time;
        while (Time.time - startTime < reachWindowSeconds)
        {
            framesInTrace++;
            float remaining = reachWindowSeconds - (Time.time - startTime);
            SetStatus($"{statusPrefix} — {remaining:F0}s | samples: {currentTraj.Count}");
            yield return null;
        }

        isRecording  = false;
        tracingPhase = false;
        if (disk != null) { disk.PauseRotation(); disk.SetVisibility(false); }

        float duration     = Time.time - startTime;
        float measuredRate = duration > 0f ? framesInTrace / duration : 0f;

        if (!isPractice)
        {
            // ── OLD two-point reach measure — superseded by the full-rim trace below ──
            // Front = nearest in depth (min world Z), Back = farthest (max world Z).
            // NOTE: assumes the scene camera faces +Z. If it looks down −Z, swap.
            // Vector3 front = Vector3.zero, back = Vector3.zero;
            // float minZ = float.MaxValue, maxZ = float.MinValue;
            // foreach (var p in currentTraj)
            // {
            //     if (p.z < minZ) { minZ = p.z; front = p; }
            //     if (p.z > maxZ) { maxZ = p.z; back  = p; }
            // }
            // float perceivedDepth = (currentTraj.Count > 0) ? (maxZ - minZ) : 0f;

            // ── Whole traced rim, de-rotated into the disk's own (stationary) frame ──
            // In that frame the perceived circle sits in a plane through the major (X)
            // axis: z = tan(σ̂)·y. So the trace gives a depth extent AND a fitted slant,
            // both independent of which way the scene camera happens to face.
            List<Vector3> local = ToDiskFrame(currentTraj, currentTrajAngle);
            Vector3 centroid    = Centroid(local);
            Vector3 extent      = Extent(local);
            float   fitSlantDeg = FitSlantDeg(local, centroid);

            float worldDiameter = disk != null ? disk.GetWorldDiameter()      : diameter;
            float predicted     = disk != null ? disk.GetImpliedDepthExtent() : 0f;
            float slantDeg      = disk != null ? disk.GetImpliedSlantDeg()
                                               : Mathf.Acos(Mathf.Clamp01(trial.aspectRatio)) * Mathf.Rad2Deg;

            records.Add(new TrialRecord(
                trialIndex, trial.configurationId, trial.repetitionNumber,
                trial.aspectRatio, slantDeg, worldDiameter, predicted,
                trial.rotationSpeed, trial.direction, duration,
                centroid, extent, fitSlantDeg,
                displayRefreshRate, measuredRate,
                new List<Vector3>(currentTraj), new List<float>(currentTrajT),
                new List<float>(currentTrajAngle), local));

            Debug.Log($"[EllipseCircle] a={trial.aspectRatio:F2} " +
                      $"traced depth={extent.z:F3} m (predicted {predicted:F3} m), " +
                      $"slant {fitSlantDeg:F1}° vs implied {slantDeg:F1}°, {local.Count} pts");
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
        currentTrajAngle.Add(disk != null ? disk.GetCurrentAngle() : 0f);
    }


    // ─── Trace geometry ────────────────────────────────────────────────────

    // World → disk frame, undoing the spin each sample was taken under:
    //     p_local = R_z(−angle) · Inverse(Q0) · (p_world − diskPos)
    // Metres are preserved (no divide by lossyScale) so extents stay comparable to
    // WorldDiameter and PredictedDepth.
    List<Vector3> ToDiskFrame(IList<Vector3> world, IList<float> angles)
    {
        var local = new List<Vector3>(world.Count);
        Quaternion invBase = Quaternion.Inverse(trialDiskBaseRot);

        for (int i = 0; i < world.Count; i++)
        {
            float angle = i < angles.Count ? angles[i] : 0f;
            Vector3 v   = invBase * (world[i] - trialDiskPos);
            local.Add(Quaternion.Euler(0f, 0f, -angle) * v);
        }

        return local;
    }

    static Vector3 Centroid(IList<Vector3> pts)
    {
        if (pts.Count == 0) return Vector3.zero;
        Vector3 sum = Vector3.zero;
        foreach (var p in pts) sum += p;
        return sum / pts.Count;
    }

    // Axis-aligned span of the trace in the disk frame: X ≈ major axis, Y ≈ minor axis
    // as drawn, Z = depth.
    static Vector3 Extent(IList<Vector3> pts)
    {
        if (pts.Count == 0) return Vector3.zero;

        Vector3 min = pts[0], max = pts[0];
        foreach (var p in pts)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return max - min;
    }

    // Least-squares slope of z on y about the centroid; the perceived circle satisfies
    // z = tan(σ)·y, so σ̂ = atan|m|. Returns 0 if the trace has no vertical spread.
    static float FitSlantDeg(IList<Vector3> pts, Vector3 centroid)
    {
        float syy = 0f, syz = 0f;
        foreach (var p in pts)
        {
            float dy = p.y - centroid.y;
            float dz = p.z - centroid.z;
            syy += dy * dy;
            syz += dy * dz;
        }

        if (syy < 1e-9f) return 0f;
        return Mathf.Atan(Mathf.Abs(syz / syy)) * Mathf.Rad2Deg;
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

        // OLD header (two-point reach): kept for reference alongside the old measure.
        // csv.AppendLine("TrialIndex,ConfigId,RepetitionNumber,AspectRatio,ImpliedSlantDeg," +
        //                "WorldDiameter,PredictedDepth,RotationSpeed,Direction,ReachDuration," +
        //                "FrontX,FrontY,FrontZ,BackX,BackY,BackZ,PerceivedDepthZ," +
        //                "DisplayRefreshRateHz,MeasuredFrameRateHz,Timestamp");

        // Centroid/Extent/FitSlant are all in the de-rotated disk frame, world metres.
        csv.AppendLine("TrialIndex,ConfigId,RepetitionNumber,AspectRatio,ImpliedSlantDeg," +
                       "WorldDiameter,PredictedDepth,RotationSpeed,Direction,TraceDuration,NumTracePoints," +
                       "CentroidX,CentroidY,CentroidZ,ExtentX,ExtentY,ExtentZ," +
                       "TracedDepthZ,FitSlantDeg," +
                       "DisplayRefreshRateHz,MeasuredFrameRateHz,Timestamp");

        foreach (var r in records)
        {
            csv.AppendLine(
                $"{r.trialIndex},{r.configId},{r.repetitionNumber},{r.aspectRatio:F4},{r.slantDeg:F2}," +
                $"{r.worldDiameter:F4},{r.predictedDepth:F4},{r.rotationSpeed:F1},{r.direction}," +
                $"{r.duration:F2},{r.traj.Count}," +
                $"{r.centroid.x:F4},{r.centroid.y:F4},{r.centroid.z:F4}," +
                $"{r.extent.x:F4},{r.extent.y:F4},{r.extent.z:F4}," +
                $"{r.extent.z:F4},{r.fitSlantDeg:F2}," +
                $"{r.displayRefreshRate:F2},{r.measuredFrameRate:F2},{timestamp}");
        }

        File.WriteAllText(path, csv.ToString());
        Debug.Log($"[EllipseCircle] Saved {records.Count} trials → {path}");
    }

    void SaveTrajectory(string timestamp)
    {
        string path = Path.Combine(Application.persistentDataPath, $"EllipseCircle_Traj_{timestamp}.csv");
        var csv = new StringBuilder();

        // OLD header (world samples only — no way to de-rotate the trace offline):
        // csv.AppendLine("TrialIndex,PointIndex,WorldX,WorldY,WorldZ,TimeStamp");

        // DiskAngleDeg is the spin angle at the moment of the sample; Local* is that
        // sample already de-rotated into the disk frame (see ToDiskFrame).
        csv.AppendLine("TrialIndex,PointIndex,WorldX,WorldY,WorldZ,DiskAngleDeg,TimeStamp," +
                       "LocalX,LocalY,LocalZ");

        foreach (var r in records)
        {
            for (int i = 0; i < r.traj.Count; i++)
            {
                Vector3 p = r.traj[i];
                float   a = i < r.trajAngle.Count ? r.trajAngle[i] : 0f;
                float   t = i < r.trajT.Count     ? r.trajT[i]     : 0f;
                Vector3 l = i < r.trajLocal.Count ? r.trajLocal[i] : Vector3.zero;
                csv.AppendLine($"{r.trialIndex},{i},{p.x:F4},{p.y:F4},{p.z:F4},{a:F2},{t:F3}," +
                               $"{l.x:F4},{l.y:F4},{l.z:F4}");
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
        // public Vector3 front, back;          // OLD two-point reach measure
        // public float   perceivedDepth;       // OLD: world max Z − min Z
        public Vector3 centroid, extent;        // de-rotated disk frame, world metres
        public float  fitSlantDeg;
        public float  displayRefreshRate, measuredFrameRate;
        public List<Vector3> traj;              // world
        public List<float>   trajT;
        public List<float>   trajAngle;         // disk spin angle per sample
        public List<Vector3> trajLocal;         // de-rotated disk frame

        public TrialRecord(int idx, int cfg, int rep, float a, float slant, float worldD, float pred,
                           float spd, int dir, float dur, Vector3 cen, Vector3 ext, float fitSlant,
                           float refreshHz, float measuredHz, List<Vector3> trajectory, List<float> trajTimes,
                           List<float> trajAngles, List<Vector3> trajLocalPoints)
        {
            trialIndex = idx; configId = cfg; repetitionNumber = rep;
            aspectRatio = a; slantDeg = slant; worldDiameter = worldD; predictedDepth = pred;
            rotationSpeed = spd; direction = dir; duration = dur;
            centroid = cen; extent = ext; fitSlantDeg = fitSlant;
            displayRefreshRate = refreshHz; measuredFrameRate = measuredHz;
            traj = trajectory; trajT = trajTimes;
            trajAngle = trajAngles; trajLocal = trajLocalPoints;
        }
    }
}
