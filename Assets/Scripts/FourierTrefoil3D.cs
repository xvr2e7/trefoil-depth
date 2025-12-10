using UnityEngine;
using UnityEngine.XR;
using System.IO;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FourierTrefoil3D : MonoBehaviour
{
    [Header("Display")]
    public float pointSize = 0.08f;

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
    private Vector3[] baseCoordinates;  // (x, y, z_base) from CSV
    private MeshRenderer meshRenderer;
    private float currentRotation = 0f;
    private InputDevice rightHandDevice;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = Color.black;
        meshRenderer.material = mat;

        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        InitializeInputDevice();
        LoadCoordinatesFromCSV();

        adjustmentEnabled = false;
        meshRenderer.enabled = false;
        GeneratePointMesh();
    }

    void LoadCoordinatesFromCSV()
    {
        TextAsset csvFile = Resources.Load<TextAsset>("coords_final");
        string[] lines = csvFile.text.Split('\n');

        int count = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i])) count++;
        }

        baseCoordinates = new Vector3[count];
        int idx = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] values = line.Split(',');
            float x = float.Parse(values[1]);
            float y = float.Parse(values[2]);
            float z = float.Parse(values[3]);

            baseCoordinates[idx++] = new Vector3(x, y, z);
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

                if (Mathf.Abs(amplitude - oldAmplitude) > 0.001f)
                {
                    GeneratePointMesh();
                }
            }
        }
    }

    void GeneratePointMesh()
    {
        int segments = baseCoordinates.Length;
        int vertsPerPoint = 6;
        int trisPerPoint = 24;

        Vector3[] vertices = new Vector3[segments * vertsPerPoint];
        int[] triangles = new int[segments * trisPerPoint];

        for (int i = 0; i < segments; i++)
        {
            Vector3 basePos = baseCoordinates[i];
            Vector3 center = new Vector3(basePos.x, basePos.y, basePos.z * amplitude);
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

    public void ResetParameters(float r1, float r2, float phase)
    {
        amplitude = 0f;
        currentRotation = 0f;
        transform.localRotation = Quaternion.identity;
        GeneratePointMesh();
        adjustmentEnabled = true;
    }

    public void SetRotationMode(bool enable)
    {
        rotationMode = enable;
        adjustmentEnabled = enable;
        if (enable)
        {
            amplitude = 1f;
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

    public void SetVisibility(bool visible)
    {
        meshRenderer.enabled = visible;
    }
}