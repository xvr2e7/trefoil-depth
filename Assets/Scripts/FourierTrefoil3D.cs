using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FourierTrefoil3D : MonoBehaviour
{
    [Header("Trefoil Parameters")]
    public float R1 = 1.0f;
    public float R2 = 1.5f;
    public int segments = 300;
    public float pointSize = 0.08f;

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

    [Header("Calibration")]
    public bool rotationMode = false;
    public float rotationSpeed = 30f;
    public bool adjustmentEnabled = true;

    private Mesh mesh;
    private Vector3[] baseCoordinates;
    private MeshRenderer meshRenderer;
    private float currentRotation = 0f;
    private InputDevice rightHandDevice;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Custom/LeftEyeOnly"));
        mat.color = Color.white;
        meshRenderer.material = mat;

        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        InitializeInputDevice();

        adjustmentEnabled = false;
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
            if (rotationMode)
            {
                currentRotation += joystick.x * rotationSpeed * Time.deltaTime;
                transform.localRotation = Quaternion.Euler(0, currentRotation, 0);
            }
            else
            {
                float oldAmplitude = amplitude;
                amplitude += joystick.y * amplitudeSpeed * Time.deltaTime;
                amplitude = Mathf.Clamp(amplitude, minAmplitude, maxAmplitude);

                // Only regenerate mesh if amplitude actually changed
                if (Mathf.Abs(amplitude - oldAmplitude) > 0.001f)
                {
                    GeneratePointMesh();
                }
            }
        }
    }

    void GeneratePointMesh()
    {
        baseCoordinates = new Vector3[segments];

        // Compute 3D coordinates: (x,y) from trefoil, z from Fourier series
        for (int i = 0; i < segments; i++)
        {
            float phi = i * 2 * Mathf.PI / segments;

            // Two-harmonic Fourier base for planar trefoil
            float x = R1 * Mathf.Cos(phi) + R2 * Mathf.Cos(2 * phi);
            float y = R1 * Mathf.Sin(phi) - R2 * Mathf.Sin(2 * phi);

            // Z-depth from Fourier series with global amplitude scaling
            float z_base = a1 * Mathf.Sin(phi) + b1 * Mathf.Cos(phi) +
                          a2 * Mathf.Sin(2 * phi) + b2 * Mathf.Cos(2 * phi) +
                          a3 * Mathf.Sin(3 * phi) + b3 * Mathf.Cos(3 * phi);

            float z = amplitude * z_base;

            baseCoordinates[i] = new Vector3(x, y, z);
        }

        // Create simple octahedron for each point (8 triangular faces)
        int vertsPerPoint = 6;
        int trisPerPoint = 24;

        Vector3[] vertices = new Vector3[segments * vertsPerPoint];
        int[] triangles = new int[segments * trisPerPoint];

        for (int i = 0; i < segments; i++)
        {
            Vector3 center = baseCoordinates[i];
            float r = pointSize * 0.5f;

            // Octahedron vertices
            int vBase = i * vertsPerPoint;
            vertices[vBase + 0] = center + new Vector3(r, 0, 0);   // +X
            vertices[vBase + 1] = center + new Vector3(-r, 0, 0);  // -X
            vertices[vBase + 2] = center + new Vector3(0, r, 0);   // +Y
            vertices[vBase + 3] = center + new Vector3(0, -r, 0);  // -Y
            vertices[vBase + 4] = center + new Vector3(0, 0, r);   // +Z
            vertices[vBase + 5] = center + new Vector3(0, 0, -r);  // -Z

            // 8 triangular faces of octahedron
            int tBase = i * trisPerPoint;

            // Top pyramid (+Y apex)
            triangles[tBase + 0] = vBase + 2; triangles[tBase + 1] = vBase + 0; triangles[tBase + 2] = vBase + 4;
            triangles[tBase + 3] = vBase + 2; triangles[tBase + 4] = vBase + 4; triangles[tBase + 5] = vBase + 1;
            triangles[tBase + 6] = vBase + 2; triangles[tBase + 7] = vBase + 1; triangles[tBase + 8] = vBase + 5;
            triangles[tBase + 9] = vBase + 2; triangles[tBase + 10] = vBase + 5; triangles[tBase + 11] = vBase + 0;

            // Bottom pyramid (-Y apex)
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

    public void ResetParameters(float r1, float r2, float phase)
    {
        R1 = r1;
        R2 = r2;
        amplitude = 0f;
        currentRotation = 0f;
        transform.localRotation = Quaternion.identity;
        GeneratePointMesh();
        adjustmentEnabled = true;
    }

    public void SetRotationMode(bool enable)
    {
        rotationMode = enable;
        adjustmentEnabled = enable; // Enable adjustment in rotation mode
        if (enable)
        {
            amplitude = 2f;
            GeneratePointMesh();
        }
    }

    public void SetAdjustmentEnabled(bool enabled)
    {
        adjustmentEnabled = enabled;
    }

    public float GetAdjustmentValue()
    {
        return amplitude;
    }

    public float[] GetCoefficients()
    {
        return new float[] { a1, b1, a2, b2, a3, b3, amplitude };
    }

    public void SetVisibility(bool visible)
    {
        meshRenderer.enabled = visible;
    }
}