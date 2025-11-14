using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AdjustableTrefoil3D : MonoBehaviour
{
    [Header("Trefoil Parameters")]
    public float R1 = 1.0f;
    public float R2 = 1.5f;
    public int segments = 1000;
    public float tubeRadius = 0.05f;

    [Header("Depth Parameters")]
    public float amplitude = 0f;
    public float phaseOffset = 0f;

    [Header("Control Settings")]
    public float amplitudeSpeed = 2f;
    public float minAmplitude = -2f;
    public float maxAmplitude = 2f;

    [Header("Confidence")]
    public float confidence = 0f;
    public float confidenceSpeed = 1f;

    private Mesh mesh;
    private Vector3[] pathPoints;
    private MeshRenderer meshRenderer;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Standard"));
        meshRenderer.material.color = Color.white;

        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        GeneratePath();
        GenerateTubeMesh();
    }

    void Update()
    {
        InputDevice rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHandDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 joystick))
        {
            amplitude += joystick.y * amplitudeSpeed * Time.deltaTime;
            amplitude = Mathf.Clamp(amplitude, minAmplitude, maxAmplitude);

            confidence += joystick.x * confidenceSpeed * Time.deltaTime;
            confidence = Mathf.Clamp01(confidence);
        }

        GeneratePath();
        GenerateTubeMesh();
    }

    void GeneratePath()
    {
        pathPoints = new Vector3[segments];

        for (int i = 0; i < segments; i++)
        {
            float phi = i * 2 * Mathf.PI / segments;
            float x = R1 * Mathf.Cos(phi) + R2 * Mathf.Cos(2 * phi);
            float y = R1 * Mathf.Sin(phi) - R2 * Mathf.Sin(2 * phi);
            float z = amplitude * Mathf.Sin(phi + phaseOffset);

            pathPoints[i] = new Vector3(x, y, z);
        }
    }

    void GenerateTubeMesh()
    {
        int radialSegments = 8;
        int totalVertices = segments * radialSegments;
        Vector3[] vertices = new Vector3[totalVertices];
        int[] triangles = new int[segments * radialSegments * 6];

        for (int i = 0; i < segments; i++)
        {
            Vector3 point = pathPoints[i];
            Vector3 nextPoint = pathPoints[(i + 1) % segments];
            Vector3 forward = (nextPoint - point).normalized;

            Vector3 right = Vector3.Cross(forward, Vector3.up);
            if (right.magnitude < 0.01f)
                right = Vector3.Cross(forward, Vector3.right);
            right.Normalize();

            Vector3 up = Vector3.Cross(right, forward).normalized;

            for (int j = 0; j < radialSegments; j++)
            {
                float angle = j * 2 * Mathf.PI / radialSegments;
                Vector3 offset = (Mathf.Cos(angle) * right + Mathf.Sin(angle) * up) * tubeRadius;
                vertices[i * radialSegments + j] = point + offset;
            }
        }

        int triIndex = 0;
        for (int i = 0; i < segments; i++)
        {
            int nextI = (i + 1) % segments;
            for (int j = 0; j < radialSegments; j++)
            {
                int nextJ = (j + 1) % radialSegments;

                int v0 = i * radialSegments + j;
                int v1 = nextI * radialSegments + j;
                int v2 = nextI * radialSegments + nextJ;
                int v3 = i * radialSegments + nextJ;

                triangles[triIndex++] = v0;
                triangles[triIndex++] = v1;
                triangles[triIndex++] = v2;

                triangles[triIndex++] = v0;
                triangles[triIndex++] = v2;
                triangles[triIndex++] = v3;
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    public void ResetParameters(float r1, float r2, float phase)
    {
        R1 = r1;
        R2 = r2;
        phaseOffset = phase;
        amplitude = 0f;
        confidence = 0f;
        GeneratePath();
        GenerateTubeMesh();
    }

    public (float, float) GetAdjustmentValues()
    {
        return (amplitude, confidence);
    }

    public void SetVisibility(bool visible)
    {
        meshRenderer.enabled = visible;
    }
}