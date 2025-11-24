using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CurvatureMarker : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    [SerializeField] private float markerSize = 0.08f;

    public void Initialize(Transform trefoilParent)
    {
        transform.SetParent(trefoilParent, false);

        meshRenderer = GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Custom/RightEyeOnly"));
        mat.color = Color.red;
        meshRenderer.material = mat;

        CreateSphereMesh();
        meshRenderer.enabled = false;
    }

    void CreateSphereMesh()
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh sphereMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
        GetComponent<MeshFilter>().mesh = sphereMesh;
        Destroy(sphere);

        // Since marker is child of trefoil (scaled 0.25), compensate for parent scale
        float parentScale = transform.parent != null ? transform.parent.localScale.x : 1f;
        transform.localScale = Vector3.one * (markerSize / parentScale);
    }

    public void SetPosition(Vector3 position)
    {
        transform.localPosition = position;
    }

    public void SetVisibility(bool visible)
    {
        meshRenderer.enabled = visible;
    }
}