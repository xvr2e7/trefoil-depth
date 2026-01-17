using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CurvatureAdjustableTrefoil : MonoBehaviour
{
    [Header("Display")]
    public float tubeRadius = 0.05f;
    public int radialSegments = 8;
    public int pathSegments = 1000;

    [Header("Trefoil Parameters")]
    public float R1 = 1.0f;
    public float R2 = 2.0f;

    [Header("Depth Control")]
    public float amplitude = 0f;
    public float amplitudeSpeed = 1f;
    public float minAmplitude = -5f;
    public float maxAmplitude = 5f;

    [Header("Segment Highlighting")]
    public float highlightStartPhi = -Mathf.PI / 12f;
    public float highlightEndPhi = Mathf.PI / 12f;
    public Color baseColor = Color.white;
    public Color highlightColor = Color.red;

    [Header("Control")]
    public bool adjustmentEnabled = false;

    private Mesh mesh;
    private Vector3[] baseCoordinates;
    private MeshRenderer meshRenderer;
    private Material segmentMaterial;
    private InputDevice rightHandDevice;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        // Create material with segment highlighting shader
        segmentMaterial = new Material(Shader.Find("Custom/SegmentHighlight"));
        segmentMaterial.SetColor("_BaseColor", baseColor);
        segmentMaterial.SetColor("_HighlightColor", highlightColor);
        segmentMaterial.SetFloat("_HighlightStart", highlightStartPhi);
        segmentMaterial.SetFloat("_HighlightEnd", highlightEndPhi);
        segmentMaterial.SetFloat("_R1", R1);
        segmentMaterial.SetFloat("_R2", R2);
        meshRenderer.material = segmentMaterial;

        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        InitializeInputDevice();
        GenerateBaseCoordinates();

        adjustmentEnabled = false;
        meshRenderer.enabled = false;
        GenerateTubeMesh();
    }

    void GenerateBaseCoordinates()
    {
        // Generate trefoil coordinates with R1=1.0, R2=2.0 to match curvature task
        baseCoordinates = new Vector3[pathSegments];

        for (int i = 0; i < pathSegments; i++)
        {
            float phi = i * 2 * Mathf.PI / pathSegments;

            float x = R1 * Mathf.Cos(phi) + R2 * Mathf.Cos(2 * phi);
            float y = R1 * Mathf.Sin(phi) - R2 * Mathf.Sin(2 * phi);

            // For z_base, we'll use a simple Fourier approximation
            // This will be scaled by amplitude during mesh generation
            float z_base = Mathf.Sin(phi) + 0.5f * Mathf.Sin(2 * phi);

            baseCoordinates[i] = new Vector3(x, y, z_base);
        }
    }

    void InitializeInputDevice()
    {
        var devices = new System.Collections.Generic.List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
        if (devices.Count > 0)
        {
            rightHandDevice = devices[0];
        }
    }

    void Update()
    {
        if (!rightHandDevice.isValid)
        {
            InitializeInputDevice();
        }

        if (!adjustmentEnabled)
            return;

        if (rightHandDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 joystick))
        {
            float oldAmplitude = amplitude;
            amplitude += joystick.y * amplitudeSpeed * Time.deltaTime;
            amplitude = Mathf.Clamp(amplitude, minAmplitude, maxAmplitude);

            if (Mathf.Abs(amplitude - oldAmplitude) > 0.001f)
            {
                GenerateTubeMesh();
            }
        }
    }

    void GenerateTubeMesh()
    {
        int segments = baseCoordinates.Length;

        // Create path points with adjusted z-coordinates
        Vector3[] pathPoints = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            Vector3 basePos = baseCoordinates[i];
            pathPoints[i] = new Vector3(basePos.x, basePos.y, basePos.z * amplitude);
        }

        // Generate tube mesh
        int totalVertices = segments * radialSegments;
        Vector3[] vertices = new Vector3[totalVertices];
        int[] triangles = new int[segments * radialSegments * 6];

        for (int i = 0; i < segments; i++)
        {
            Vector3 point = pathPoints[i];
            Vector3 nextPoint = pathPoints[(i + 1) % segments];
            Vector3 forward = (nextPoint - point).normalized;

            // Create perpendicular vectors for tube cross-section
            Vector3 right = Vector3.Cross(forward, Vector3.up);
            if (right.magnitude < 0.01f)
                right = Vector3.Cross(forward, Vector3.right);
            right.Normalize();

            Vector3 up = Vector3.Cross(right, forward).normalized;

            // Create circular cross-section
            for (int j = 0; j < radialSegments; j++)
            {
                float angle = j * 2 * Mathf.PI / radialSegments;
                Vector3 offset = (Mathf.Cos(angle) * right + Mathf.Sin(angle) * up) * tubeRadius;
                vertices[i * radialSegments + j] = point + offset;
            }
        }

        // Create triangles connecting the segments
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
        mesh.RecalculateBounds();
    }

    public void SetRotationAngle(float angle)
    {
        // Set the rotation of this GameObject to match the captured angle
        transform.localRotation = Quaternion.Euler(0, 0, angle);
    }

    public void ResetParameters(float startingAmplitude = 0f)
    {
        amplitude = startingAmplitude;
        GenerateTubeMesh();
    }

    public void SetAdjustmentEnabled(bool enabled)
    {
        adjustmentEnabled = enabled;
    }

    public float GetAdjustmentValue()
    {
        return amplitude;
    }

    public void SetVisibility(bool visible)
    {
        meshRenderer.enabled = visible;
    }

    public void SetHighlightRange(float startPhi, float endPhi)
    {
        highlightStartPhi = startPhi;
        highlightEndPhi = endPhi;

        if (segmentMaterial != null)
        {
            segmentMaterial.SetFloat("_HighlightStart", startPhi);
            segmentMaterial.SetFloat("_HighlightEnd", endPhi);
        }
    }

    public void SetColors(Color baseCol, Color highlightCol)
    {
        baseColor = baseCol;
        highlightColor = highlightCol;

        if (segmentMaterial != null)
        {
            segmentMaterial.SetColor("_BaseColor", baseCol);
            segmentMaterial.SetColor("_HighlightColor", highlightCol);
        }
    }
}
