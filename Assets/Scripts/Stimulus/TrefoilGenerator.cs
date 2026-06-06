using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TrefoilGenerator : MonoBehaviour
{
    [Header("Trefoil Parameters")]
    public float R1 = 1.0f;
    public float R2 = 1.5f;
    public int segments = 1000;
    public float width = 0.1f;

    [Header("Rotation")]
    public float rotationSpeed = 90f;

    // Rotation direction: 1=CCW, -1=CW
    public int direction = 1;

    public enum ShaderType { Binocular, RightEyeOnly }

    [Header("Rendering")]
    public ShaderType shaderType = ShaderType.RightEyeOnly;

    [Header("Occlusion Shading")]
    [Tooltip("Load the Fourier CSV to apply per-vertex z-offsets (depth-buffer occlusion at crossings) " +
             "and grey shading (lighter = front, darker = back). Encodes the 2-front/1-back configuration.")]
    public bool  useOcclusionDepth = true;
    [Tooltip("Scale applied to raw CSV z-values for the vertex z-offset. Keeps the curve visually flat " +
             "while still resolving crossing order in the depth buffer.")]
    public float occlusionDepthScale = 0.005f;
    [Tooltip("Vertex color for the curve portions furthest from the viewer (back cross-junction).")]
    public Color curveBackColor  = new Color(0.25f, 0.25f, 0.25f);
    [Tooltip("Vertex color for the curve portions closest to the viewer (front cross-junctions).")]
    public Color curveFrontColor = new Color(0.75f, 0.75f, 0.75f);

    private Mesh mesh;
    private Vector3[] pathPoints;
    private MeshRenderer meshRenderer;
    private float currentAngle = 0f;
    private bool isRotating = true;

    // CSV-sourced z-profile for occlusion shading (loaded by LoadOcclusionProfile)
    private float[] _occPhi;
    private float[] _occZ;
    private float   _occZMin;
    private float   _occZMax;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        SetupMaterial();

        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        LoadOcclusionProfile();
        GeneratePath();
        GenerateRibbonMesh();
    }

    void SetupMaterial()
    {
        Material mat;
        if (shaderType == ShaderType.RightEyeOnly)
            mat = new Material(Shader.Find("Custom/RightEyeOnly"));
        else
            mat = new Material(Shader.Find("Custom/BinocularUnlit"));

        // White base: vertex colors carry all grey/shading information.
        // Brightness = 1.0 so vertex color values (0–1) map directly to screen grey.
        mat.color = Color.white;
        mat.SetFloat("_Brightness", 1.0f);
        meshRenderer.material = mat;
    }

    void Update()
    {
        if (isRotating)
        {
            currentAngle += rotationSpeed * Time.deltaTime * direction;
            transform.localRotation = Quaternion.Euler(0, 0, currentAngle);
        }
    }

    void GeneratePath()
    {
        pathPoints = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float phi = i * 2 * Mathf.PI / segments;
            float x = R1 * Mathf.Cos(phi) + R2 * Mathf.Cos(2 * phi);
            float y = R1 * Mathf.Sin(phi) - R2 * Mathf.Sin(2 * phi);
            float z = (useOcclusionDepth && _occZ != null) ? GetZAtPhi(phi) * occlusionDepthScale : 0f;
            pathPoints[i] = new Vector3(x, y, z);
        }
    }

    void GenerateRibbonMesh()
    {
        Vector3[] vertices  = new Vector3[segments * 2];
        int[]     triangles = new int[segments * 6];
        Color[]   colors    = new Color[segments * 2];

        for (int i = 0; i < segments; i++)
        {
            Vector3 point     = pathPoints[i];
            Vector3 nextPoint = pathPoints[(i + 1) % segments];
            Vector3 tangent   = (nextPoint - point).normalized;

            Vector3 perpendicular = new Vector3(-tangent.y, tangent.x, 0) * width * 0.5f;

            vertices[i * 2]     = point + perpendicular;
            vertices[i * 2 + 1] = point - perpendicular;

            Color c = GetColorAtPhi(i * 2 * Mathf.PI / segments);
            colors[i * 2]     = c;
            colors[i * 2 + 1] = c;
        }

        int triIndex = 0;
        for (int i = 0; i < segments; i++)
        {
            int nextI = (i + 1) % segments;

            triangles[triIndex++] = i * 2;
            triangles[triIndex++] = nextI * 2;
            triangles[triIndex++] = i * 2 + 1;

            triangles[triIndex++] = i * 2 + 1;
            triangles[triIndex++] = nextI * 2;
            triangles[triIndex++] = nextI * 2 + 1;
        }

        mesh.Clear();
        mesh.vertices  = vertices;
        mesh.triangles = triangles;
        mesh.colors    = colors;
        mesh.RecalculateNormals();
    }

    public void ResetRotation()
    {
        currentAngle = 0f;
        transform.localRotation = Quaternion.identity;
    }

    public void SetStartingAngle(float angle)
    {
        currentAngle = angle;
        transform.localRotation = Quaternion.Euler(0, 0, currentAngle);
    }

    public void SetParameters(float r1, float r2, float speed, int dir)
    {
        R1 = r1;
        R2 = r2;
        rotationSpeed = speed;
        direction = dir;
        LoadOcclusionProfile();
        GeneratePath();
        GenerateRibbonMesh();
        ResetRotation();
    }

    // ── Occlusion shading helpers ──────────────────────────────────────────

    void LoadOcclusionProfile()
    {
        if (!useOcclusionDepth) { _occPhi = null; _occZ = null; return; }

        string csvName = Mathf.Abs(R2 - 2.0f) < 0.01f ? "coords_R2_2.0" : "coords_R2_1.5";
        TextAsset csvFile = Resources.Load<TextAsset>(csvName);
        if (csvFile == null) { _occPhi = null; _occZ = null; return; }

        var lines = csvFile.text.Split('\n');
        var phis = new System.Collections.Generic.List<float>();
        var zs   = new System.Collections.Generic.List<float>();
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] parts = line.Split(',');
            phis.Add(float.Parse(parts[0]));
            zs.Add(float.Parse(parts[3]));
        }
        _occPhi = phis.ToArray();
        _occZ   = zs.ToArray();

        _occZMin = _occZ[0]; _occZMax = _occZ[0];
        foreach (float z in _occZ)
        {
            if (z < _occZMin) _occZMin = z;
            if (z > _occZMax) _occZMax = z;
        }
    }

    float GetZAtPhi(float phi)
    {
        if (_occPhi == null || _occPhi.Length < 2) return 0f;
        while (phi < 0f)              phi += Mathf.PI * 2f;
        while (phi >= Mathf.PI * 2f)  phi -= Mathf.PI * 2f;

        float step = _occPhi[1] - _occPhi[0];
        int   idx  = Mathf.Clamp(Mathf.FloorToInt(phi / step), 0, _occPhi.Length - 2);
        float t    = (phi - _occPhi[idx]) / step;
        return Mathf.Lerp(_occZ[idx], _occZ[(idx + 1) % _occZ.Length], t);
    }

    Color GetColorAtPhi(float phi)
    {
        if (!useOcclusionDepth || _occZ == null || _occZMax <= _occZMin)
            return new Color(0.5f, 0.5f, 0.5f);
        float t = (GetZAtPhi(phi) - _occZMin) / (_occZMax - _occZMin);
        return Color.Lerp(curveBackColor, curveFrontColor, t);
    }

    public void SetVisibility(bool visible)
    {
        meshRenderer.enabled = visible;
    }

    public void PauseRotation()
    {
        isRotating = false;
    }

    public void ResumeRotation()
    {
        isRotating = true;
    }

    public Vector3 GetPointAt(float phi)
    {
        float x = R1 * Mathf.Cos(phi) + R2 * Mathf.Cos(2 * phi);
        float y = R1 * Mathf.Sin(phi) - R2 * Mathf.Sin(2 * phi);
        return new Vector3(x, y, 0);
    }

    public Vector3 GetNormalAt(float phi)
    {
        float dx = -R1 * Mathf.Sin(phi) - 2 * R2 * Mathf.Sin(2 * phi);
        float dy = R1 * Mathf.Cos(phi) - 2 * R2 * Mathf.Cos(2 * phi);

        Vector3 tangent = new Vector3(dx, dy, 0).normalized;
        Vector3 normal = new Vector3(-tangent.y, tangent.x, 0);
        return normal;
    }

    public float GetCurrentAngle()
    {
        return currentAngle;
    }

    public void SetColor(Color color)
    {
        meshRenderer.material.color = color;
    }

    public void SetShaderType(ShaderType type)
    {
        shaderType = type;
        SetupMaterial();
    }
}