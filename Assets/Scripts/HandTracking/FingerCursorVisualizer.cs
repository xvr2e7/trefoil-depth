using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FingerCursorVisualizer : MonoBehaviour
{
    [Header("Pose Source")]
    [Tooltip("VIVE Tracker pose source. Mounted on the participant's hand/finger.")]
    public TrackerPoseProvider trackerProvider;

    [Header("Appearance")]
    public float cursorScale = 0.015f;
    public Color lightGreen = new Color(0.5f, 1f, 0.35f);

    private static readonly Color DarkGreen = new Color(0.05f, 0.55f, 0.1f);

    private MeshRenderer meshRenderer;
    private bool frozen = false;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        Material mat = new Material(Shader.Find("Custom/RightEyeOnly"));
        mat.color = lightGreen;
        meshRenderer.material = mat;

        transform.localScale = Vector3.one * cursorScale;
        gameObject.SetActive(false);
    }

    void OnEnable()
    {
        frozen = false;
        if (meshRenderer != null)
            meshRenderer.material.color = lightGreen;
    }

    void Update()
    {
        if (frozen) return;
        if (trackerProvider == null) return;

        if (trackerProvider.TryGetPosition(out Vector3 pos))
            transform.position = pos;
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
        meshRenderer.material.color = lightGreen;
    }

    public bool TryGetIndexTipPosition(out Vector3 pos)
    {
        pos = Vector3.zero;
        if (trackerProvider == null) return false;
        return trackerProvider.TryGetPosition(out pos);
    }
}
