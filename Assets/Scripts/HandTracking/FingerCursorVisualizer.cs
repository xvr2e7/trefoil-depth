using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FingerCursorVisualizer : MonoBehaviour
{
    [Header("Pose Source")]
    public TrackerPoseProvider trackerProvider;

    [Header("Appearance")]
    public float cursorScale = 0.015f;
    public Color onCurveColor  = new Color(0.5f, 1f, 0.35f);   
    public Color offCurveColor = new Color(1f, 0.92f, 0.016f); 
    private static readonly Color DarkGreen = new Color(0.05f, 0.55f, 0.1f);

    [Header("Proximity Feedback")]
    [Tooltip("Distance threshold in meters. Within this = green, outside = yellow.")]
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
        mat.color = onCurveColor;
        meshRenderer.material = mat;
        transform.localScale = Vector3.one * cursorScale;
        gameObject.SetActive(false);
    }

    void OnEnable()
    {
        frozen = false;
        if (meshRenderer != null)
            meshRenderer.material.color = onCurveColor;
    }

    void Update()
    {
        if (frozen) return;
        if (trackerProvider == null) return;
        if (!trackerProvider.TryGetPosition(out Vector3 pos)) return;

        transform.position = pos;

        if (proximityFeedbackEnabled)
        {
            float dist = GetDistanceToCurve(pos);
            meshRenderer.material.color = dist <= proximityThreshold
                ? onCurveColor
                : offCurveColor;
        }
    }

        float GetDistanceToCurve(Vector3 pos)
    {
        float minDist = float.MaxValue;

        if (proximityTrefoil2D != null)
        {
            int samples = 100;
            for (int i = 0; i < samples; i++)
            {
                float phi = (i / (float)samples) * Mathf.PI * 2f;
                Vector3 worldPt = proximityTrefoil2D.transform.TransformPoint(
                    proximityTrefoil2D.GetPointAt(phi));
                float d = Vector3.Distance(pos, worldPt);
                if (d < minDist) minDist = d;
            }
        }

        if (proximityTrefoil3D != null)
        {
            Vector3 nearest = proximityTrefoil3D.GetNearestCurveWorldPoint(pos, out _);
            float d = Vector3.Distance(pos, nearest);
            if (d < minDist) minDist = d;
        }
        if (proximityCube != null)
        {
            for (int i = 0; i < proximityCube.GetEdgeCount(); i++)
            {
                Vector3 edgeStart = proximityCube.GetEdgeStart(i);
                Vector3 edgeEnd   = proximityCube.GetEdgeEnd(i);
                Vector3 edgeDir   = (edgeEnd - edgeStart).normalized;
                float   edgeLen   = Vector3.Distance(edgeStart, edgeEnd);
                Vector3 toPos     = pos - edgeStart;
                float   proj      = Mathf.Clamp(Vector3.Dot(toPos, edgeDir), 0f, edgeLen);
                Vector3 closest   = edgeStart + edgeDir * proj;
                float   d         = Vector3.Distance(pos, closest);
                if (d < minDist) minDist = d;
            }
        }
        return minDist;
    }

    public void EnableProximityFeedback2D(TrefoilGenerator trefoil)
    {
        proximityTrefoil2D = trefoil;
        proximityTrefoil3D = null;
        proximityFeedbackEnabled = true;
    }

    public void EnableProximityFeedback3D(FourierTrefoil3D trefoil)
    {
        proximityTrefoil3D = trefoil;
        proximityTrefoil2D = null;
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
            meshRenderer.material.color = onCurveColor;
    }

    public void SetConfirmed(Vector3 pos)
    {
        frozen = true;
        transform.position = pos;
        meshRenderer.material.color = DarkGreen;
    }

    public void ResetCursor()
    {
        frozen = false;
        if (meshRenderer != null)
            meshRenderer.material.color = onCurveColor;
    }

    public bool TryGetIndexTipPosition(out Vector3 pos)
    {
        pos = Vector3.zero;
        if (trackerProvider == null) return false;
        return trackerProvider.TryGetPosition(out pos);
    }
}