using UnityEngine;
using UnityEngine.XR;
using System.IO;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FourierTrefoil3D : MonoBehaviour
{
    [Header("Display")]
    public float tubeRadius = 0.05f;
    public int radialSegments = 8;

    [Header("Trefoil Parameters")]
    public float R1 = 2.0f;
    public float R2 = 0.5f;

    [Header("Depth Control")]
    public float amplitude = 0f;
    public float amplitudeSpeed = 1f;
    public float minAmplitude = -5f;
    public float maxAmplitude = 5f;

    [Header("Calibration")]
    public bool rotationMode = false;
    public float rotationSpeed = 30f;
    public int rotationDirection = 1;  // 1=CCW, -1=CW
    public bool adjustmentEnabled = true;
    public bool autoRotate = false;  // For automatic rotation during confirmation (Z-axis)
    public bool manualRotationMode = false;  // For manual exploration rotation (Y-axis)

    private Mesh mesh;
    private float[] phiValues;  // φ values from CSV
    private float[] zBaseValues;  // z_base values from CSV (Fourier-optimized)
    private MeshRenderer meshRenderer;
    private float currentRotationY = 0f;  // For Y-axis rotation (manual exploration)
    private float currentRotationZ = 0f;  // For Z-axis rotation (automatic confirmation)
    private InputDevice rightHandDevice;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = Color.white;
        meshRenderer.material = mat;

        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        InitializeInputDevice();
        // LoadCoordinatesFromCSV will be called when ResetParameters is called

        adjustmentEnabled = false;
        meshRenderer.enabled = false;
    }

    void LoadCoordinatesFromCSV(float r2)
    {
        // Determine which CSV file to load based on R2 value
        string csvFileName;
        if (Mathf.Abs(r2 - 1.5f) < 0.01f)
        {
            csvFileName = "coords_R2_1.5";
        }
        else if (Mathf.Abs(r2 - 2.0f) < 0.01f)
        {
            csvFileName = "coords_R2_2.0";
        }
        else
        {
            Debug.LogWarning($"No optimal profile found for R2={r2}, using R2=1.5 as default");
            csvFileName = "coords_R2_1.5";
        }

        TextAsset csvFile = Resources.Load<TextAsset>(csvFileName);
        if (csvFile == null)
        {
            Debug.LogError($"Could not load CSV file: {csvFileName}");
            return;
        }

        string[] lines = csvFile.text.Split('\n');

        int count = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i])) count++;
        }

        phiValues = new float[count];
        zBaseValues = new float[count];
        int idx = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] values = line.Split(',');
            float phi = float.Parse(values[0]);
            float z = float.Parse(values[3]);

            phiValues[idx] = phi;
            zBaseValues[idx] = z;
            idx++;
        }

        Debug.Log($"Loaded {idx} coordinates from {csvFileName} for R2={r2}");
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

        // Handle automatic rotation around Y-axis (for confirmation/preview stage)
        if (autoRotate)
        {
            currentRotationZ += rotationSpeed * Time.deltaTime * rotationDirection;
            transform.localRotation = Quaternion.Euler(0, currentRotationZ, 0);
            return;
        }

        // Handle manual rotation around Y-axis (for exploration in calibration phase)
        if (manualRotationMode && rightHandDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 joystick))
        {
            currentRotationY += joystick.x * rotationSpeed * Time.deltaTime;
            transform.localRotation = Quaternion.Euler(0, currentRotationY, 0);
            return;
        }

        if (!adjustmentEnabled)
            return;

        if (rightHandDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 joystickInput))
        {
            float oldAmplitude = amplitude;
            amplitude += joystickInput.y * amplitudeSpeed * Time.deltaTime;
            amplitude = Mathf.Clamp(amplitude, minAmplitude, maxAmplitude);

            if (Mathf.Abs(amplitude - oldAmplitude) > 0.001f)
            {
                GenerateTubeMesh();
            }
        }
    }

    void GenerateTubeMesh()
    {
        int segments = phiValues.Length;

        // Create path points: calculate XY from R1/R2, use CSV's z values
        Vector3[] pathPoints = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float phi = phiValues[i];

            // Calculate XY coordinates based on R1/R2 parameters
            float x = R1 * Mathf.Cos(phi) + R2 * Mathf.Cos(2 * phi);
            float y = R1 * Mathf.Sin(phi) - R2 * Mathf.Sin(2 * phi);

            // Use Fourier-optimized z from CSV, scaled by amplitude
            float z = zBaseValues[i] * amplitude;

            pathPoints[i] = new Vector3(x, y, z);
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

    public void ResetParameters(float r1, float r2, float phase, float startingAmplitude = 0f)
    {
        R1 = r1;
        R2 = r2;
        amplitude = startingAmplitude;
        currentRotationY = 0f;
        currentRotationZ = 0f;
        transform.localRotation = Quaternion.identity;

        // Load the optimal profile for this R2 value
        LoadCoordinatesFromCSV(r2);

        GenerateTubeMesh();
        adjustmentEnabled = true;
    }

    public void SetRotationMode(bool enable, float speed = 60f, int direction = 1)
    {
        if (enable)
        {
            // Enable automatic rotation mode around Z-axis (matches 2D stimulus)
            autoRotate = true;
            manualRotationMode = false;
            adjustmentEnabled = false;
            rotationSpeed = speed;  // Set rotation speed to match stimulus
            rotationDirection = direction;  // Set rotation direction to match stimulus
            // Don't reset currentRotationZ - start from current orientation
            // Don't change amplitude - keep the adjusted value
            GenerateTubeMesh();
        }
        else
        {
            // Disable rotation mode
            autoRotate = false;
            adjustmentEnabled = true;
        }
    }

    public void SetManualRotationMode(bool enable)
    {
        if (enable)
        {
            // Enable manual rotation mode around Y-axis (for exploration)
            manualRotationMode = true;
            autoRotate = false;
            adjustmentEnabled = false;
            currentRotationY = 0f;  // Reset Y rotation
        }
        else
        {
            // Disable manual rotation mode
            manualRotationMode = false;
            adjustmentEnabled = true;
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

    public void SetVisibility(bool visible)
    {
        meshRenderer.enabled = visible;
    }

    // Returns the current Y-axis rotation angle (degrees) accumulated by autoRotate.
    // Note: the field is named currentRotationZ in legacy code but drives Euler Y.
    public float GetCurrentRotationY() => currentRotationZ;

    // Returns the nearest point on the curve centerline in world space and its phi value.
    // Requires ResetParameters to have been called (loads CSV data).
    public Vector3 GetNearestCurveWorldPoint(Vector3 queryWorldPos, out float nearestPhi)
    {
        nearestPhi = 0f;
        if (phiValues == null || phiValues.Length == 0)
            return queryWorldPos;

        Vector3 localQuery = transform.InverseTransformPoint(queryWorldPos);
        float minSqDist = float.MaxValue;
        int bestIdx = 0;

        for (int i = 0; i < phiValues.Length; i++)
        {
            float phi = phiValues[i];
            float x = R1 * Mathf.Cos(phi) + R2 * Mathf.Cos(2 * phi);
            float y = R1 * Mathf.Sin(phi) - R2 * Mathf.Sin(2 * phi);
            float z = zBaseValues[i] * amplitude;
            float sqd = (localQuery - new Vector3(x, y, z)).sqrMagnitude;
            if (sqd < minSqDist) { minSqDist = sqd; bestIdx = i; }
        }

        nearestPhi = phiValues[bestIdx];
        float bx = R1 * Mathf.Cos(nearestPhi) + R2 * Mathf.Cos(2 * nearestPhi);
        float by = R1 * Mathf.Sin(nearestPhi) - R2 * Mathf.Sin(2 * nearestPhi);
        float bz = zBaseValues[bestIdx] * amplitude;
        return transform.TransformPoint(new Vector3(bx, by, bz));
    }
}