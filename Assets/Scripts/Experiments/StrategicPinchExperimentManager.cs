using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using TMPro;

/// <summary>
/// Strategic Pinch Experiment — clean flow.
///
/// PROCEDURE:
///   1. Trefoil rotates. Participant presses A when they perceive the 3D shape.
///   2. Trefoil stops IMMEDIATELY at the current angle (= stopping angle 0).
///   3. For each of the 6 strategic points at this angle (interleaved order):
///        a. Flash marker 4 times.
///        b. Light-green cursor follows index-finger tip live.
///        c. Finger held still ≥ 3 s → cursor turns dark green, freezes (confirmed).
///           Press A at any time as manual override.
///        d. Dark-green cursor stays 2 s, then disappears.
///        e. If CrossSection: "A = Near   B = Far"
///        f. "Same orientation?  A = Same   B = Flipped"
///   4. Resume rotation, turn exactly 20° → stop. Repeat step 3.
///   5. Continue for all stopping-angle offsets [0°, 20°, 40°, 60°, 80°, 100°].
///   6. Save data.
/// </summary>
public class StrategicPinchExperimentManager : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Stimulus")]
    public TrefoilGenerator trefoil;
    public StrategicPointFlasher flasher;

    [Header("Calibration")]
    public TrefoilGenerator calibTrefoil;   // 2D rotating curve shown during calibration
    public FourierTrefoil3D  calibModel;    // 3D model participant explores with joystick

    [Header("Finger cursor")]
    public FingerCursorVisualizer fingerCursor;

    [Header("UI")]
    public TextMeshProUGUI instructionText;   // large text, pre-task screens
    public TextMeshProUGUI explainText;       // small upper text, during trial

    [Header("Trefoil parameters")]
    public float R1 = 1.0f;
    public float R2 = 1.5f;
    public float rotationSpeed = 90f;
    public int   rotationDirection = 1;   // 1 = CCW, -1 = CW

    [Header("Stopping angle offsets (degrees from initial stop)")]
    public float[] stoppingAngles = { 0f, 20f, 40f, 60f, 80f, 100f };

    [Header("Dwell (step 3c)")]
    public float dwellTimeSec       = 3f;
    public float dwellMoveThreshold = 0.02f;   // metres; resets dwell timer

    [Header("Flash timing (step 3a)")]
    public float flashOnDuration  = 0.3f;
    public float flashOffDuration = 0.2f;
    public int   flashCount       = 4;

    [Header("Confirmation display (step 3d)")]
    public float confirmDuration = 2.0f;   // seconds cursor stays dark green

    [Header("Blocks")]
    public int blocksMain = 4;

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private InputDevice rightController;
    private InputDevice leftController;
    private bool lastA = false;
    private bool lastB = false;

    private StrategicPoint[] strategicPoints;
    private List<StrategicPinchRecord> records = new List<StrategicPinchRecord>();
    private int recordIdx = 0;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    void Start()
    {
        StartCoroutine(Main());
    }

    void Update()
    {
        if (!rightController.isValid || !leftController.isValid)
            FindControllers();
    }

    // -----------------------------------------------------------------------
    // Top-level coroutine
    // -----------------------------------------------------------------------

    IEnumerator Main()
    {
        yield return null;   // one frame for subsystems to initialise

        FindControllers();

        strategicPoints = StrategicPinchTrialGenerator.ComputeStrategicPoints(R1, R2);
        LogPoints();

        SafeHide(trefoil);
        SafeHide(calibTrefoil);
        if (calibModel   != null) calibModel.SetVisibility(false);
        if (flasher      != null) flasher.Deactivate();
        if (fingerCursor != null) fingerCursor.gameObject.SetActive(false);

        // --- Welcome ---
        Say("Welcome!\n\nPress A to begin.");
        yield return WaitA();

        // --- Handedness ---
        Say("Which is your dominant hand?\n\nA = Right     B = Left");
        yield return new WaitForSeconds(0.5f);
        bool handDone = false;
        while (!handDone)
        {
            if (GetADown()) { HandednessManager.Instance.SetDominantHand(HandednessManager.Handedness.RightHanded); handDone = true; }
            if (GetBDown()) { HandednessManager.Instance.SetDominantHand(HandednessManager.Handedness.LeftHanded);  handDone = true; }
            yield return null;
        }
        Say("");
        yield return new WaitForSeconds(0.3f);

        yield return Calibration();

        // --- Main blocks ---
        for (int b = 0; b < blocksMain; b++)
        {
            if (b > 0)
            {
                Say($"Block {b}/{blocksMain} complete.\nTake a short break.\n\nPress A when ready.");
                yield return new WaitForSeconds(1f);
                yield return WaitA();
                Say("");
            }

            yield return RunBlock(b);
        }

        // --- End ---
        SaveData();
        Say("All done!\n\nThank you for participating.");
        yield return new WaitForSeconds(3f);
    }

    // -----------------------------------------------------------------------
    // Calibration: 2D curve → explore 3D model → 2D curve again
    // -----------------------------------------------------------------------

    IEnumerator Calibration()
    {
        // Part 1 — watch the 2D rotating trefoil until 3D percept
        if (calibTrefoil != null)
        {
            calibTrefoil.SetParameters(R1, R2, 60f, 1);
            calibTrefoil.SetShaderType(TrefoilGenerator.ShaderType.RightEyeOnly);
            calibTrefoil.ResumeRotation();
            calibTrefoil.SetVisibility(true);
        }
        Explain("CALIBRATION\n\nStare at the rotating curve.\nPress A when you perceive a 3D shape.");
        yield return new WaitForSeconds(0.5f);
        yield return WaitA();
        SafeHide(calibTrefoil);

        // Part 2 — explore the 3D model with the joystick
        Explain("This is one possible 3D interpretation.\nUse the joystick to rotate it.\n\nPress A when ready.");
        if (calibModel != null)
        {
            calibModel.ResetParameters(R1, R2, 0f, Random.Range(0.5f, 1.0f));
            calibModel.SetManualRotationMode(true);
            calibModel.SetVisibility(true);
        }
        yield return new WaitForSeconds(0.5f);
        yield return WaitA();
        if (calibModel != null)
        {
            calibModel.SetVisibility(false);
            calibModel.SetManualRotationMode(false);
        }

        // Part 3 — look at the 2D curve again
        if (calibTrefoil != null) calibTrefoil.SetVisibility(true);
        Explain("Look at the curve again.\nCan you perceive the 3D shape?\n\nPress A when ready.");
        yield return new WaitForSeconds(0.5f);
        yield return WaitA();
        SafeHide(calibTrefoil);
        Explain("");
    }

    void SafeHide(TrefoilGenerator tg)
    {
        if (tg != null) tg.SetVisibility(false);
    }

    // -----------------------------------------------------------------------
    // One block: rotate → A pressed → stop → probe 6 stops → done
    // -----------------------------------------------------------------------

    IEnumerator RunBlock(int blockIdx)
    {
        // Start trefoil rotating
        if (trefoil != null)
        {
            trefoil.SetParameters(R1, R2, rotationSpeed, rotationDirection);
            trefoil.SetVisibility(true);
            trefoil.ResumeRotation();
        }

        Explain("Watch the curve.\n\nPress A when you perceive the 3D shape.");
        yield return new WaitForSeconds(0.5f);
        yield return WaitA();

        // Stop immediately — this IS the first stopping angle
        if (trefoil != null) trefoil.PauseRotation();
        Explain("");
        yield return new WaitForSeconds(0.3f);

        int[] order = StrategicPinchTrialGenerator.PresentationOrder();

        for (int sIdx = 0; sIdx < stoppingAngles.Length; sIdx++)
        {
            float actualAngle = (trefoil != null)
                ? Mathf.Repeat(trefoil.GetCurrentAngle(), 360f)
                : stoppingAngles[sIdx];

            Debug.Log($"[Block {blockIdx}] Stop {sIdx} | offset {stoppingAngles[sIdx]}° | actual {actualAngle:F1}°");

            // Probe all 6 strategic points while trefoil is frozen
            foreach (int pIdx in order)
                yield return ProbePoint(blockIdx, sIdx, stoppingAngles[sIdx], actualAngle, pIdx);

            // Rotate to next stopping angle
            if (sIdx < stoppingAngles.Length - 1)
            {
                float rotateBy = stoppingAngles[sIdx + 1] - stoppingAngles[sIdx];
                Explain("Rotating to next position…");
                if (trefoil != null) trefoil.ResumeRotation();
                yield return RotateByDegrees(Mathf.Abs(rotateBy));
                if (trefoil != null) trefoil.PauseRotation();
                yield return new WaitForSeconds(0.3f);
                Explain("");
            }
        }

        if (trefoil != null) trefoil.SetVisibility(false);
        Explain("");
    }

    // -----------------------------------------------------------------------
    // Rotate the trefoil by exactly [degrees] degrees, then return.
    // -----------------------------------------------------------------------

    IEnumerator RotateByDegrees(float degrees)
    {
        if (trefoil == null) yield break;

        float turned = 0f;
        float prev   = Mathf.Repeat(trefoil.GetCurrentAngle(), 360f);

        while (turned < degrees)
        {
            yield return null;
            if (trefoil == null) yield break;

            float curr  = Mathf.Repeat(trefoil.GetCurrentAngle(), 360f);
            float delta = Mathf.Abs(Mathf.DeltaAngle(prev, curr));
            turned += delta;
            prev    = curr;
        }
    }

    // -----------------------------------------------------------------------
    // Probe one strategic point
    // -----------------------------------------------------------------------

    IEnumerator ProbePoint(int blockIdx, int stopIdx, float offsetAngle, float actualAngle, int pIdx)
    {
        StrategicPoint sp = strategicPoints[pIdx];

        // 2D position of point on frozen trefoil (XY only; Z = trefoil plane)
        Vector3 worldPos = (trefoil != null)
            ? trefoil.transform.TransformPoint(trefoil.GetPointAt(sp.phi))
            : Vector3.zero;

        // Push flash marker slightly toward viewer so it is never occluded by
        // the ribbon mesh (critical at cross-sections where two ribbon segments
        // overlap).  Use Camera.main if available; fall back to trefoil's -forward.
        Vector3 toViewer = (Camera.main != null)
            ? (Camera.main.transform.position - worldPos).normalized
            : (trefoil != null ? -trefoil.transform.forward : Vector3.back);
        Vector3 flashPos = worldPos + toViewer * 0.01f;

        // ------------------------------------------------------------------
        // Step 3a — Flash marker 4 times, then leave it OFF
        // ------------------------------------------------------------------
        Explain(sp.type == StrategicPointType.CrossSection
            ? "Watch the marker…  (crossing point)"
            : "Watch the marker…");

        for (int i = 0; i < flashCount; i++)
        {
            if (flasher != null) flasher.Show(flashPos);
            yield return new WaitForSeconds(flashOnDuration);
            if (flasher != null) flasher.Deactivate();
            yield return new WaitForSeconds(flashOffDuration);
        }
        // Marker stays OFF during the pointing phase

        // ------------------------------------------------------------------
        // Step 3b — Light-green cursor follows finger
        // ------------------------------------------------------------------
        if (fingerCursor != null) fingerCursor.gameObject.SetActive(true);

        Explain("Point to where you perceive this marker in depth.\n" +
            "Hold still for 3 seconds.  (Press A to confirm manually.)");

        // Dwell: timer resets if finger moves more than dwellMoveThreshold
        Vector3 anchor      = Vector3.zero;
        bool    anchorReady = false;
        float   dwellStart  = -1f;
        Vector3 recorded    = worldPos;   // fallback: marker's 2D position
        bool    confirmed   = false;

        while (!confirmed)
        {
            // Manual override: press A
            if (GetADown())
            {
                recorded  = anchorReady ? anchor : worldPos;
                confirmed = true;
                break;
            }

            if (fingerCursor != null && fingerCursor.TryGetIndexTipPosition(out Vector3 tip))
            {
                if (!anchorReady)
                {
                    anchor      = tip;
                    anchorReady = true;
                    dwellStart  = Time.time;
                }
                else if (Vector3.Distance(tip, anchor) > dwellMoveThreshold)
                {
                    anchor     = tip;
                    dwellStart = Time.time;
                    // Cursor remains light green while still moving
                }
                else if (Time.time - dwellStart >= dwellTimeSec)
                {
                    recorded  = tip;
                    confirmed = true;
                }
            }

            yield return null;
        }

        // ------------------------------------------------------------------
        // Step 3c/d — Cursor turns dark green, stays 2 s, then disappears
        // ------------------------------------------------------------------
        if (fingerCursor != null)
            fingerCursor.SetConfirmed(recorded);   // dark green, frozen at recorded pos

        Explain("");
        yield return new WaitForSeconds(confirmDuration);

        if (fingerCursor != null)
        {
            fingerCursor.ResetCursor();
            fingerCursor.gameObject.SetActive(false);
        }

        Debug.Log($"[ProbePoint] Block={blockIdx} Stop={stopIdx} Point={pIdx} Pos={recorded}");

        // ------------------------------------------------------------------
        // Step 3e — Near / Far (cross-sections only)
        // ------------------------------------------------------------------
        string nearFar = "NA";
        if (sp.type == StrategicPointType.CrossSection)
        {
            Explain("Which did you point to?\n\nA = Near     B = Far");
            yield return new WaitForSeconds(0.3f);
            bool nfDone = false;
            while (!nfDone)
            {
                if (GetADown()) { nearFar = "Near"; nfDone = true; }
                if (GetBDown()) { nearFar = "Far";  nfDone = true; }
                yield return null;
            }
            Explain("");
            yield return new WaitForSeconds(0.2f);
        }

        // ------------------------------------------------------------------
        // Step 3f — Flip query
        // ------------------------------------------------------------------
        bool flipped = false;
        Explain("Is the 3D shape in the same orientation?\n\nA = Same     B = Flipped");
        yield return new WaitForSeconds(0.3f);
        bool flipDone = false;
        while (!flipDone)
        {
            if (GetADown()) { flipped = false; flipDone = true; }
            if (GetBDown()) { flipped = true;  flipDone = true; }
            yield return null;
        }
        Explain("");
        yield return new WaitForSeconds(0.2f);

        // ------------------------------------------------------------------
        // Record
        // ------------------------------------------------------------------
        string hand = HandednessManager.Instance.IsRightHanded() ? "RH" : "LH";
        records.Add(new StrategicPinchRecord(
            recordIdx++, blockIdx, stopIdx,
            offsetAngle, actualAngle,
            R1, R2, rotationDirection,
            sp, flipped, recorded, nearFar, dwellTimeSec, hand));

        yield return new WaitForSeconds(0.1f);
    }

    // -----------------------------------------------------------------------
    // Button helpers
    // -----------------------------------------------------------------------

    void FindControllers()
    {
        var list = new List<InputDevice>();

        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, list);
        if (list.Count > 0) rightController = list[0];

        list.Clear();
        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, list);
        if (list.Count > 0) leftController = list[0];
    }

    bool GetADown()
    {
        if (!rightController.isValid) return false;
        if (rightController.TryGetFeatureValue(CommonUsages.primaryButton, out bool cur))
        {
            bool pressed = cur && !lastA;
            lastA = cur;
            return pressed;
        }
        return false;
    }

    bool GetBDown()
    {
        if (!rightController.isValid) return false;
        if (rightController.TryGetFeatureValue(CommonUsages.secondaryButton, out bool cur))
        {
            bool pressed = cur && !lastB;
            lastB = cur;
            return pressed;
        }
        return false;
    }

    IEnumerator WaitA()
    {
        while (!GetADown()) yield return null;
    }

    // -----------------------------------------------------------------------
    // UI
    // -----------------------------------------------------------------------

    void Say(string text)
    {
        if (instructionText != null) instructionText.text = text;
    }

    void Explain(string text)
    {
        if (explainText != null) explainText.text = text;
    }

    // -----------------------------------------------------------------------
    // Debug gizmos
    // -----------------------------------------------------------------------

    void OnDrawGizmos()
    {
        if (strategicPoints == null || trefoil == null) return;
        foreach (var sp in strategicPoints)
        {
            var world = trefoil.transform.TransformPoint(trefoil.GetPointAt(sp.phi));
            Gizmos.color = sp.type == StrategicPointType.LobeTip ? Color.yellow : Color.cyan;
            Gizmos.DrawWireSphere(world, 0.04f);
        }
    }

    // -----------------------------------------------------------------------
    // Data saving
    // -----------------------------------------------------------------------

    void SaveData()
    {
        string ts   = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string hand = HandednessManager.Instance.IsRightHanded() ? "RH" : "LH";
        string path = Path.Combine(Application.persistentDataPath,
                                   $"Trefoil_StrategicPinch_{hand}_{ts}.csv");

        var csv = new StringBuilder();
        csv.AppendLine("RecordIndex,BlockNumber,StopNumber," +
                       "AngleOffset,ActualAngle,R1,R2,RotDir," +
                       "PointIndex,PointType,Phi,PhiB," +
                       "Flipped,PointedX,PointedY,PointedZ," +
                       "NearFar,DwellSec,Timestamp,Handedness");

        foreach (var r in records)
        {
            string phiB = float.IsNaN(r.pointPhiB) ? "NaN" : r.pointPhiB.ToString("F6");
            csv.AppendLine(
                $"{r.recordIndex},{r.blockNumber},{r.stopNumber}," +
                $"{r.targetStopAngle:F2},{r.actualStopAngle:F2}," +
                $"{r.R1:F4},{r.R2:F4},{r.rotationDirection}," +
                $"{r.pointIndex},{r.pointType},{r.pointPhi:F6},{phiB}," +
                $"{r.depthFlipped},{r.pointedX:F6},{r.pointedY:F6},{r.pointedZ:F6}," +
                $"{r.nearFar},{r.dwellTimeSec:F2},{r.timestamp},{r.handedness}");
        }

        File.WriteAllText(path, csv.ToString());
        Debug.Log($"[SaveData] Saved {records.Count} records → {path}");
    }

    void LogPoints()
    {
        foreach (var sp in strategicPoints)
        {
            var p = StrategicPinchTrialGenerator.EvalCurve(R1, R2, sp.phi);
            Debug.Log($"[Strategic] [{sp.index}] {sp.type} φ={sp.phi:F3} xy=({p.x:F2},{p.y:F2})" +
                      (float.IsNaN(sp.phiB) ? "" : $" φB={sp.phiB:F3}"));
        }
    }
}

// -----------------------------------------------------------------------
// Data record
// -----------------------------------------------------------------------

[System.Serializable]
public class StrategicPinchRecord
{
    public int    recordIndex;
    public int    blockNumber;
    public int    stopNumber;
    public float  targetStopAngle;
    public float  actualStopAngle;
    public float  R1, R2;
    public int    rotationDirection;
    public int    pointIndex;
    public string pointType;
    public float  pointPhi;
    public float  pointPhiB;
    public bool   depthFlipped;
    public float  pointedX, pointedY, pointedZ;
    public string nearFar;
    public float  dwellTimeSec;
    public string timestamp;
    public string handedness;

    public StrategicPinchRecord(
        int ri, int bn, int sn,
        float ta, float aa, float r1, float r2, int rd,
        StrategicPoint sp,
        bool flipped, Vector3 pointed, string nf, float dwell, string hand)
    {
        recordIndex       = ri;
        blockNumber       = bn;
        stopNumber        = sn;
        targetStopAngle   = ta;
        actualStopAngle   = aa;
        R1                = r1;
        R2                = r2;
        rotationDirection = rd;
        pointIndex        = sp.index;
        pointType         = sp.type.ToString();
        pointPhi          = sp.phi;
        pointPhiB         = sp.phiB;
        depthFlipped      = flipped;
        pointedX          = pointed.x;
        pointedY          = pointed.y;
        pointedZ          = pointed.z;
        nearFar           = nf;
        dwellTimeSec      = dwell;
        timestamp         = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        handedness        = hand;
    }
}
