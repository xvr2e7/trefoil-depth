using UnityEngine;

/// <summary>
/// Amber sphere cue placed at a strategic point on the static trefoil.
///
/// Flash sequence (called by StrategicPinchExperimentManager):
///   - Flash(worldPos) shows the sphere 4 times then leaves it on.
///   - Deactivate() hides it.
///
/// Show / Hide are public so the caller can drive the blink timing
/// using its own coroutine (yield return WaitForSeconds).
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class StrategicPointFlasher : MonoBehaviour
{
    [Header("Appearance")]
    public float markerScale = 0.07f;   // diameter in metres (bigger than before)

    private MeshRenderer meshRenderer;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        Material mat = new Material(Shader.Find("Custom/BinocularUnlit"));
        mat.color = new Color(1f, 0.75f, 0f);   // amber, both eyes
        meshRenderer.material = mat;

        gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Show at a world position
    // ------------------------------------------------------------------
    public void Show(Vector3 worldPos)
    {
        transform.position = worldPos;
        transform.localScale = Vector3.one * markerScale;
        gameObject.SetActive(true);
    }

    // ------------------------------------------------------------------
    // Hide
    // ------------------------------------------------------------------
    public void Deactivate()
    {
        gameObject.SetActive(false);
    }
}
