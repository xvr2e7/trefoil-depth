using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CurvatureSegment : MonoBehaviour
{
    [Header("Trefoil Parameters")]
    public float R1 = 1.0f;
    public float R2 = 2.0f;
    public int segments = 50;
    public float pointSize = 0.08f;

    [Header("Segment Range")]
    public float startPhi = -Mathf.PI / 6f;
    public float endPhi = Mathf.PI / 6f;

    [Header("Fourier Coefficients")]
    public float a1 = 1.0f;
    public float b1 = 0.0f;
    public float a2 = 0.0f;
    public float b2 = 0.0f;
    public float a3 = 0.0f;
    public float b3 = 0.0f;

    [Header("Depth Control")]
    public float amplitude = 0f;
    public float amplitudeSpeed = 1f;
    public float minAmplitude = -5f;
    public float maxAmplitude = 5f;

    [Header("Control")]
    public bool adjustmentEnabled = false;

    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private InputDevice rightHandDevice;
    private Vector3 positionOffset = Vector3.zero;
    private float rotationAngle = 0f;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = Color.white;
        meshRenderer.material = mat;

        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        InitializeInputDevice();
        meshRenderer.enabled = false;
        GeneratePointMesh();
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
                GeneratePointMesh();
            }
        }
    }

    void GeneratePointMesh()
    {
        // Calculate the center point (at phi=0) to use as reference
        float centerX = R1 * Mathf.Cos(0f) + R2 * Mathf.Cos(0f);
        float centerY = R1 * Mathf.Sin(0f) - R2 * Mathf.Sin(0f);
        float centerZ_base = a1 * Mathf.Sin(0f) + b1 * Mathf.Cos(0f) +
                             a2 * Mathf.Sin(0f) + b2 * Mathf.Cos(0f) +
                             a3 * Mathf.Sin(0f) + b3 * Mathf.Cos(0f);
        Vector3 segmentCenter = new Vector3(centerX, centerY, amplitude * centerZ_base);

        Vector3[] baseCoordinates = new Vector3[segments];
        float phiRange = endPhi - startPhi;

        // Convert rotation angle to radians
        float angleRad = rotationAngle * Mathf.Deg2Rad;
        float cosAngle = Mathf.Cos(angleRad);
        float sinAngle = Mathf.Sin(angleRad);

        for (int i = 0; i < segments; i++)
        {
            float phi = startPhi + i * phiRange / (segments - 1);

            float x = R1 * Mathf.Cos(phi) + R2 * Mathf.Cos(2 * phi);
            float y = R1 * Mathf.Sin(phi) - R2 * Mathf.Sin(2 * phi);

            float z_base = a1 * Mathf.Sin(phi) + b1 * Mathf.Cos(phi) +
                          a2 * Mathf.Sin(2 * phi) + b2 * Mathf.Cos(2 * phi) +
                          a3 * Mathf.Sin(3 * phi) + b3 * Mathf.Cos(3 * phi);

            float z = amplitude * z_base;

            // Apply rotation around z-axis to match captured angle
            float xRotated = x * cosAngle - y * sinAngle;
            float yRotated = x * sinAngle + y * cosAngle;

            // Translate so segment center aligns with positionOffset
            baseCoordinates[i] = new Vector3(xRotated, yRotated, z) - segmentCenter + positionOffset;
        }

        int vertsPerPoint = 6;
        int trisPerPoint = 24;

        Vector3[] vertices = new Vector3[segments * vertsPerPoint];
        int[] triangles = new int[segments * trisPerPoint];

        for (int i = 0; i < segments; i++)
        {
            Vector3 center = baseCoordinates[i];
            float r = pointSize * 0.5f;

            int vBase = i * vertsPerPoint;
            vertices[vBase + 0] = center + new Vector3(r, 0, 0);
            vertices[vBase + 1] = center + new Vector3(-r, 0, 0);
            vertices[vBase + 2] = center + new Vector3(0, r, 0);
            vertices[vBase + 3] = center + new Vector3(0, -r, 0);
            vertices[vBase + 4] = center + new Vector3(0, 0, r);
            vertices[vBase + 5] = center + new Vector3(0, 0, -r);

            int tBase = i * trisPerPoint;

            triangles[tBase + 0] = vBase + 2; triangles[tBase + 1] = vBase + 0; triangles[tBase + 2] = vBase + 4;
            triangles[tBase + 3] = vBase + 2; triangles[tBase + 4] = vBase + 4; triangles[tBase + 5] = vBase + 1;
            triangles[tBase + 6] = vBase + 2; triangles[tBase + 7] = vBase + 1; triangles[tBase + 8] = vBase + 5;
            triangles[tBase + 9] = vBase + 2; triangles[tBase + 10] = vBase + 5; triangles[tBase + 11] = vBase + 0;

            triangles[tBase + 12] = vBase + 3; triangles[tBase + 13] = vBase + 4; triangles[tBase + 14] = vBase + 0;
            triangles[tBase + 15] = vBase + 3; triangles[tBase + 16] = vBase + 1; triangles[tBase + 17] = vBase + 4;
            triangles[tBase + 18] = vBase + 3; triangles[tBase + 19] = vBase + 5; triangles[tBase + 20] = vBase + 1;
            triangles[tBase + 21] = vBase + 3; triangles[tBase + 22] = vBase + 0; triangles[tBase + 23] = vBase + 5;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    public void SetPositionOffset(Vector3 offset)
    {
        positionOffset = offset;
        GeneratePointMesh();
    }

    public void SetParameters(float r1, float r2)
    {
        R1 = r1;
        R2 = r2;
        GeneratePointMesh();
    }

    public void ResetAmplitude(float startingAmplitude = 0f)
    {
        amplitude = startingAmplitude;
        GeneratePointMesh();
    }

    public void SetAdjustmentEnabled(bool enabled)
    {
        adjustmentEnabled = enabled;
    }

    public float GetAmplitude()
    {
        return amplitude;
    }

    public void SetVisibility(bool visible)
    {
        meshRenderer.enabled = visible;
    }

    public void SetColor(Color color)
    {
        meshRenderer.material.color = color;
    }

    public void SetRotationAngle(float angle)
    {
        rotationAngle = angle;
        GeneratePointMesh();
    }
}