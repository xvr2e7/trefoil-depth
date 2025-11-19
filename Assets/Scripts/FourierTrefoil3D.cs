using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FourierTrefoil3D : MonoBehaviour
{
    [Header("Trefoil Parameters")]
    public float R1 = 1.0f;
    public float R2 = 1.5f;
    public int segments = 1000;
    public float tubeRadius = 0.05f;

    [Header("Fourier Coefficients")]
    public float a1 = 0f;
    public float b1 = 0f;
    public float a2 = 0f;
    public float b2 = 0f;
    public float a3 = 0f;
    public float b3 = 0f;

    [Header("Control Settings")]
    public float coefficientSpeed = 1f;
    public float minCoefficient = -3f;
    public float maxCoefficient = 3f;
    public float rotationSpeed = 30f;

    private Mesh mesh;
    private Vector3[] coordinates;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private bool rotationMode = false;
    private float currentRotation = 0f;

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        mesh = new Mesh();
        meshFilter.mesh = mesh;

        Material mat = new Material(Shader.Find("Custom/LeftEyeOnly"));
        if (mat.shader == null)
            mat = new Material(Shader.Find("Standard"));
        mat.color = Color.white;
        meshRenderer.material = mat;

        GenerateCoordinates();
        GenerateTubeMesh();
    }

    void Update()
    {
        InputDevice rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHandDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 joystick))
        {
            if (rotationMode)
            {
                currentRotation += joystick.x * rotationSpeed * Time.deltaTime;
                transform.localRotation = Quaternion.Euler(0, currentRotation, 0);
            }
            else
            {
                a1 += joystick.y * coefficientSpeed * Time.deltaTime;
                a1 = Mathf.Clamp(a1, minCoefficient, maxCoefficient);

                b1 += joystick.x * coefficientSpeed * Time.deltaTime;
                b1 = Mathf.Clamp(b1, minCoefficient, maxCoefficient);

                GenerateCoordinates();
                GenerateTubeMesh();
            }
        }
    }

    void GenerateCoordinates()
    {
        coordinates = new Vector3[segments];

        for (int i = 0; i < segments; i++)
        {
            float phi = i * 2 * Mathf.PI / segments;

            float x = R1 * Mathf.Cos(phi) + R2 * Mathf.Cos(2 * phi);
            float y = R1 * Mathf.Sin(phi) - R2 * Mathf.Sin(2 * phi);

            float z = a1 * Mathf.Sin(phi) + b1 * Mathf.Cos(phi) +
                     a2 * Mathf.Sin(2 * phi) + b2 * Mathf.Cos(2 * phi) +
                     a3 * Mathf.Sin(3 * phi) + b3 * Mathf.Cos(3 * phi);

            coordinates[i] = new Vector3(x, y, z);
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
            Vector3 point = coordinates[i];
            Vector3 nextPoint = coordinates[(i + 1) % segments];
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
        a1 = b1 = a2 = b2 = a3 = b3 = 0f;
        currentRotation = 0f;
        transform.localRotation = Quaternion.identity;
        GenerateCoordinates();
        GenerateTubeMesh();
    }

    public void SetRotationMode(bool enable)
    {
        rotationMode = enable;
        if (enable)
        {
            a1 = 2f;
            b1 = 0f;
            a2 = b2 = a3 = b3 = 0f;
            GenerateCoordinates();
            GenerateTubeMesh();
        }
    }

    public float GetAdjustmentValue()
    {
        return a1;
    }

    public float[] GetCoefficients()
    {
        return new float[] { a1, b1, a2, b2, a3, b3 };
    }

    public void SetVisibility(bool visible)
    {
        meshRenderer.enabled = visible;
    }
}