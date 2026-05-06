using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using TMPro;


public class RotatingTraceExperimentManager : MonoBehaviour
{
    // ─── Inspector ──────────────────────────────────────────────────────────

    [Header("Stimulus")]
    public TrefoilGenerator rotatingTrefoil;   // the single rotating 2D trefoil

    [Header("Marker")]
    public GameObject markerPrefab;            // amber sphere prefab (can reuse StrategicPointFlasher)
    [Tooltip("phi value (0-2pi) on the trefoil curve where the marker sits")]
    public float markerPhi = 0f;
    [Tooltip("How many full orbits before auto-advance (0 = manual only)")]
    public int orbitsToAutoAdvance = 1;

    [Header("Hand Tracking / Tracing")]
    public FingerCursorVisualizer fingerCursor; // live finger dot (reuse existing)
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
    public float rotationSpeed = 30f;   // slower than before — participant needs to keep up
    public int   rotationDirection = 1;

    [Header("Trials")]
    public int trialsPerBlock = 6;
    public int totalBlocks    = 4;

    
    private GameObject markerObject;
    private MeshRenderer markerRenderer;

   
    private List<TraceRecord> records = new List<TraceRecord>();
    private List<Vector3>  currentTrace    = new List<Vector3>();
    private List<float>    currentTracePhi = new List<float>();  // phi at time of each point
    private List<float>    currentTraceT   = new List<float>();  // time
    private Vector3 lastTracedPoint;
    private bool tracingEnabled = false;
    private bool isRecording    = false;

   
    private XRHandSubsystem handSubsystem;
    private bool lastPinch = false;

   
    private bool signalStart = false;
    private bool signalDone  = false;
    private bool signalNext  = false;

    // Trial bookkeeping
    private int currentBlock = 0;
    private int currentTrialInBlock = 0;
    private int globalTrialIndex = 0;
    private float orbitsAccumulated = 0f;
    private float lastTrefoilAngle  = 0f;

  

    void Start()
    {
        InitHandTracking();
        BuildMarker();
        StartCoroutine(Main());
    }

    void Update()
    {
        // Experimenter keyboard shortcuts
        if (Input.GetKeyDown(KeyCode.Space)) { signalStart = true; signalDone = true; }
        if (Input.GetKeyDown(KeyCode.N))     signalNext  = true;
        if (Input.GetKeyDown(KeyCode.D))     signalDone  = true;

        if (tracingEnabled)
        {
            UpdateMarkerPosition();
            HandlePinchTracing();
            TrackOrbits();
        }
    }



    public void OnStartButton() => signalStart = true;
    public void OnDoneButton()  => signalDone  = true;
    public void OnNextButton()  => signalNext  = true;


    void InitHandTracking()
    {
        var list = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(list);
        if (list.Count > 0) handSubsystem = list[0];
        else Debug.LogWarning("[RotatingTrace] No XRHandSubsystem found — hand tracing will not work.");
    }

    void BuildMarker()
    {
        if (markerPrefab != null)
        {
            markerObject = Instantiate(markerPrefab);
        }
        else
        {
            // Fallback: create a small amber sphere
            markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(markerObject.GetComponent<Collider>());
            markerObject.transform.localScale = Vector3.one * 0.06f;
            var mat = new Material(Shader.Find("Custom/BinocularUnlit") ?? Shader.Find("Standard"));
            mat.color = new Color(1f, 0.75f, 0f); // amber
            markerObject.GetComponent<MeshRenderer>().material = mat;
        }
        markerObject.SetActive(false);
    }

        IEnumerator Main()
    {
        yield return null; // one frame

        HideAll();

        Say("Welcome!\n\nExperimenter: press Space or click Start when ready.");
        yield return WaitSignalStart();

        yield return RunCalibration();

        for (currentBlock = 0; currentBlock < totalBlocks; currentBlock++)
        {
            if (currentBlock > 0)
            {
                Say($"Block {currentBlock}/{totalBlocks} complete.\nTake a short break.\n\nExperimenter: press Space when ready.");
                yield return WaitSignalStart();
            }

            for (currentTrialInBlock = 0; currentTrialInBlock < trialsPerBlock; currentTrialInBlock++, globalTrialIndex++)
            {
                yield return RunTrial();
            }
        }

        SaveData();
        Say("All done!\n\nThank you for your participation.");
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
        Explain("CALIBRATION\n\nStare at the rotating curve.\nExperimenter: press Space when participant perceives 3D shape.");
        yield return WaitSignalStart();
        if (calibTrefoil != null) calibTrefoil.SetVisibility(false);

        // Part 2 — explore 3D model
        Explain("This is one possible 3D interpretation.\nUse the joystick to rotate it.\n\nExperimenter: press Space when ready to continue.");
        if (calibModel != null)
        {
            calibModel.ResetParameters(R1, R2, 0f, Random.Range(0.5f, 1.0f));
            calibModel.SetManualRotationMode(true);
            calibModel.SetVisibility(true);
        }
        yield return WaitSignalStart();
        if (calibModel != null) { calibModel.SetVisibility(false); calibModel.SetManualRotationMode(false); }

        // Part 3 — rotating curve again
        if (calibTrefoil != null) calibTrefoil.SetVisibility(true);
        Explain("Look at the curve again.\nCan you perceive the 3D shape?\n\nExperimenter: press Space when ready.");
        yield return WaitSignalStart();
        if (calibTrefoil != null) calibTrefoil.SetVisibility(false);
        Explain("");
    }

   

    IEnumerator RunTrial()
    {
        // --- Setup ---
        currentTrace.Clear();
        currentTracePhi.Clear();
        currentTraceT.Clear();
        orbitsAccumulated = 0f;
        signalDone = false;

        
        if (rotatingTrefoil != null)
        {
            rotatingTrefoil.SetParameters(R1, R2, rotationSpeed, rotationDirection);
            rotatingTrefoil.ResumeRotation();
            rotatingTrefoil.SetVisibility(true);
        }

        
        markerObject.SetActive(true);
        UpdateMarkerPosition();

        lastTrefoilAngle = rotatingTrefoil != null ? rotatingTrefoil.GetCurrentAngle() : 0f;

        
        Explain($"Trial {globalTrialIndex + 1}\n\nWatch the amber marker.\nPinch and trace the depth you perceive as it rotates.\n\nExperimenter: press Space to begin.");
        Say("");

        yield return WaitSignalStart();

        
        Explain("Trace the depth with your dominant hand.\nPinch = record. Release = pause.\n\nExperimenter: press D when trial is complete.");

        if (fingerCursor != null) { fingerCursor.ResetCursor(); fingerCursor.gameObject.SetActive(true); }

        tracingEnabled = true;
        float trialStartTime = Time.time;

       
        while (!signalDone)
        {
            if (orbitsToAutoAdvance > 0 && orbitsAccumulated >= orbitsToAutoAdvance)
            {
                signalDone = true;
            }
            yield return null;
        }

       
        tracingEnabled = false;
        isRecording    = false;

        if (fingerCursor != null) fingerCursor.gameObject.SetActive(false);
        if (rotatingTrefoil != null) { rotatingTrefoil.PauseRotation(); rotatingTrefoil.SetVisibility(false); }
        markerObject.SetActive(false);

        
        float duration = Time.time - trialStartTime;
        records.Add(new TraceRecord(globalTrialIndex, currentBlock, currentTrialInBlock,
                                    R1, R2, rotationSpeed, rotationDirection,
                                    new List<Vector3>(currentTrace),
                                    new List<float>(currentTracePhi),
                                    new List<float>(currentTraceT),
                                    duration));

        Explain($"Trial complete. {currentTrace.Count} points recorded.\n\nExperimenter: press Space for next trial.");
        yield return new WaitForSeconds(0.5f);
        yield return WaitSignalStart();
        Explain("");
    }

   
    void UpdateMarkerPosition()
    {
        if (rotatingTrefoil == null || markerObject == null) return;

        // Get the 2D point at markerPhi in local space, then transform to world
        Vector3 localPoint = rotatingTrefoil.GetPointAt(markerPhi);
        // The trefoil's rotation is applied to the transform, so TransformPoint handles it
        Vector3 worldPoint = rotatingTrefoil.transform.TransformPoint(localPoint);
        markerObject.transform.position = worldPoint;
    }

    void TrackOrbits()
    {
        if (rotatingTrefoil == null) return;

        float currentAngle = rotatingTrefoil.GetCurrentAngle();
        float delta = Mathf.DeltaAngle(lastTrefoilAngle, currentAngle);
        orbitsAccumulated += Mathf.Abs(delta) / 360f;
        lastTrefoilAngle = currentAngle;
    }

    void HandlePinchTracing()
    {
        if (handSubsystem == null) return;

        bool isPinching = CheckPinch();

        if (isPinching && !lastPinch)
        {
            // Pinch started — begin recording
            isRecording = true;
            if (fingerCursor != null) fingerCursor.ResetCursor();

            if (fingerCursor != null && fingerCursor.TryGetIndexTipPosition(out Vector3 startPos))
            {
                lastTracedPoint = startPos;
                RecordPoint(startPos);
            }
        }
        else if (!isPinching && lastPinch)
        {
            
            isRecording = false;
        }
        else if (isPinching && isRecording)
        {
            if (fingerCursor != null && fingerCursor.TryGetIndexTipPosition(out Vector3 pos))
            {
                float dist = Vector3.Distance(pos, lastTracedPoint);
                if (dist > minTraceDistance)
                {
                    RecordPoint(pos);
                    lastTracedPoint = pos;
                }
            }
        }

        lastPinch = isPinching;
    }

    void RecordPoint(Vector3 worldPos)
    {
        currentTrace.Add(worldPos);
        
        float currentAngle = rotatingTrefoil != null ? rotatingTrefoil.GetCurrentAngle() : 0f;
        currentTracePhi.Add(currentAngle);
        currentTraceT.Add(Time.time);
    }

    bool CheckPinch()
    {
        if (handSubsystem == null) return false;

        XRHand hand = (HandednessManager.Instance != null && HandednessManager.Instance.IsRightHanded())
            ? handSubsystem.rightHand
            : handSubsystem.leftHand;

        if (!hand.isTracked) return false;

        XRHandJoint thumbTip = hand.GetJoint(XRHandJointID.ThumbTip);
        XRHandJoint indexTip = hand.GetJoint(XRHandJointID.IndexTip);

        if (thumbTip.TryGetPose(out Pose tp) && indexTip.TryGetPose(out Pose ip))
            return Vector3.Distance(tp.position, ip.position) < 0.03f;

        return false;
    }

   

    IEnumerator WaitSignalStart()
    {
        signalStart = false;
        yield return new WaitForSeconds(0.3f);
        while (!signalStart) yield return null;
        signalStart = false;
        yield return new WaitForSeconds(0.2f);
    }

    

    void Say(string text)    { if (instructionText != null) instructionText.text = text; }
    void Explain(string text){ if (explainText     != null) explainText.text     = text; }

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
                       "PointIndex,WorldX,WorldY,WorldZ,TrefoilAngleDeg,TimeStamp,TrialDuration");

        foreach (var rec in records)
        {
            for (int i = 0; i < rec.tracePoints.Count; i++)
            {
                Vector3 p = rec.tracePoints[i];
                float   a = i < rec.traceAngles.Count ? rec.traceAngles[i] : 0f;
                float   t = i < rec.traceTimes.Count  ? rec.traceTimes[i]  : 0f;
                csv.AppendLine($"{rec.trialIndex},{rec.block},{rec.trialInBlock}," +
                               $"{rec.R1},{rec.R2},{rec.rotationSpeed},{rec.rotationDirection}," +
                               $"{i},{p.x:F4},{p.y:F4},{p.z:F4},{a:F2},{t:F3},{rec.duration:F2}");
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
        public List<float>   traceTimes;

        public TraceRecord(int idx, int blk, int tib, float r1, float r2, float spd, int dir,
                           List<Vector3> pts, List<float> angles, List<float> times, float dur)
        {
            trialIndex = idx; block = blk; trialInBlock = tib;
            R1 = r1; R2 = r2; rotationSpeed = spd; rotationDirection = dir;
            tracePoints = pts; traceAngles = angles; traceTimes = times; duration = dur;
        }
    }
}