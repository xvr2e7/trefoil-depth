using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FingerCursorVisualizer : MonoBehaviour
{
    [Header("Pose Source")]
    public TrackerPoseProvider trackerProvider;

    [Header("Appearance")]
    public float cursorScale = 0.015f;
    public Color neutralColor     = new Color(0.1f, 0.45f, 1.0f);    // blue  — idle / no feedback
    public Color onCurveColor    = new Color(0.15f, 0.85f, 0.2f);   // green — on-curve (within threshold)
    public Color underreachColor = new Color(1f, 0.92f, 0.016f);    // yellow — not deep enough
    public Color overreachColor  = new Color(1f, 0.15f, 0.1f);      // red   — too deep
    private static readonly Color ConfirmedColor = new Color(0.05f, 0.55f, 0.1f); // dark green — frozen confirm

    [Header("Proximity Feedback")]
    [Tooltip("Distance threshold in meters. Within this = on-curve (green), outside = depth-directional (yellow/red).")]
    public float proximityThreshold = 0.02f;

    private MeshRenderer meshRenderer;
    private bool frozen = false;
    private bool proximityFeedbackEnabled = false;
    private TrefoilGenerator proximityTrefoil2D = null;
    private FourierTrefoil3D proximityTrefoil3D = null;
    private CubeCalibrator proximityCube = null;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Custom/RightEyeOnly"));
        mat.color = neutralColor;
        meshRenderer.material = mat;
        transform.localScale = Vector3.one * cursorScale;
        gameObject.SetActive(false);
    }

    void OnEnable()
    {
        frozen = false;
        if (meshRenderer != null)
            meshRenderer.material.color = neutralColor;
    }

    void Update()
    {
        if (frozen) return;
        if (trackerProvider == null) return;
        if (!trackerProvider.TryGetPosition(out Vector3 pos)) return;

        transform.position = pos;

        if (proximityFeedbackEnabled)
        {
            GetNearestCurvePoint(pos, out Vector3 nearest, out float dist);
            Color c;
            if (dist <= proximityThreshold)
                c = onCurveColor;
            else if (proximityCube != null)
                c = neutralColor;           // cube edges run in all directions — z-comparison meaningless
            else if (pos.z < nearest.z)
                c = underreachColor;        // yellow: not deep enough (trefoil only)
            else
                c = overreachColor;         // red: too deep (trefoil only)
            meshRenderer.material.color = c;
        }
    }

    // Returns distance to the nearest point on the active curve/cube.
    public float GetDistanceToCurve(Vector3 pos)
    {
        GetNearestCurvePoint(pos, out _, out float dist);
        return dist;
    }

    private void GetNearestCurvePoint(Vector3 pos, out Vector3 nearest, out float distance)
    {
        nearest = pos;
        float minDist = float.MaxValue;
        Vector3 minPt = pos;

        if (proximityTrefoil2D != null)
        {
            int samples = 100;
            for (int i = 0; i < samples; i++)
            {
                float phi = (i / (float)samples) * Mathf.PI * 2f;
                Vector3 worldPt = proximityTrefoil2D.transform.TransformPoint(
                    proximityTrefoil2D.GetPointAt(phi));
                float d = Vector3.Distance(pos, worldPt);
                if (d < minDist) { minDist = d; minPt = worldPt; }
            }
        }

        if (proximityTrefoil3D != null)
        {
            Vector3 np = proximityTrefoil3D.GetNearestCurveWorldPoint(pos, out _);
            float d = Vector3.Distance(pos, np);
            if (d < minDist) { minDist = d; minPt = np; }
        }

        if (proximityCube != null)
        {
            Vector3 np = proximityCube.GetNearestCurveWorldPoint(pos);
            float d = Vector3.Distance(pos, np);
            if (d < minDist) { minDist = d; minPt = np; }
        }

        nearest = minPt;
        distance = minDist;
    }

    public void EnableProximityFeedback2D(TrefoilGenerator trefoil)
    {
        proximityTrefoil2D = trefoil;
        proximityTrefoil3D = null;
        proximityCube = null;
        proximityFeedbackEnabled = true;
    }

    public void EnableProximityFeedback3D(FourierTrefoil3D trefoil)
    {
        proximityTrefoil3D = trefoil;
        proximityTrefoil2D = null;
        proximityCube = null;
        proximityFeedbackEnabled = true;
    }

    public void EnableProximityFeedbackCube(CubeCalibrator cube)
    {
        proximityCube = cube;
        proximityTrefoil2D = null;
        proximityTrefoil3D = null;
        proximityFeedbackEnabled = true;
    }

    public void DisableProximityFeedback()
    {
        proximityFeedbackEnabled = false;
        proximityTrefoil2D = null;
        proximityTrefoil3D = null;
        proximityCube = null;
        if (meshRenderer != null)
            meshRenderer.material.color = neutralColor;
    }

    public void SetConfirmed(Vector3 pos)
    {
        frozen = true;
        transform.position = pos;
        meshRenderer.material.color = ConfirmedColor;
    }

    public void ResetCursor()
    {
        frozen = false;
        if (meshRenderer != null)
            meshRenderer.material.color = neutralColor;
    }

    public bool TryGetIndexTipPosition(out Vector3 pos)
    {
        pos = Vector3.zero;
        if (trackerProvider == null) return false;
        return trackerProvider.TryGetPosition(out pos);
    }
}
