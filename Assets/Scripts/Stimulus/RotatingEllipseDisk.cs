using UnityEngine;

// Flat ellipse-outline ribbon that spins about the line of sight (local Z).
//
// Control stimulus for the Ellipse→Circle depth-scale condition: a 2D ellipse of
// aspect ratio `a = minor/major` is the monocular projection of a circle slanted
// by σ = acos(a). Spun about the view axis, the visual system reifies a rigid
// circle tilted in depth, with a CONSTANT implied depth (constant aspect → constant
// implied slant). No real Z geometry and no shading — the only depth cue is the
// rotation. See RotatingTraceExperimentManager / TrefoilGenerator for the patterns
// this mirrors.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RotatingEllipseDisk : MonoBehaviour
{
    [Header("Ellipse Parameters")]
    [Tooltip("Major-axis diameter D in LOCAL units. World diameter = diameter * transform.lossyScale.")]
    public float diameter = 1.0f;
    [Tooltip("Aspect ratio a = minor/major, in (0,1]. a=1 is a face-on circle (zero implied depth).")]
    [Range(0.05f, 1f)]
    public float aspectRatio = 0.5f;
    public int   segments = 360;
    [Tooltip("Ribbon (outline) thickness in local units.")]
    public float width = 0.05f;

    [Header("Rotation")]
    public float rotationSpeed = 90f;       // deg/sec about local Z (line of sight)
    public int   direction = 1;             // 1 = CCW, -1 = CW

    public enum ShaderType { Binocular, RightEyeOnly }

    [Header("Rendering")]
    public ShaderType shaderType = ShaderType.RightEyeOnly;
    public Color color = Color.white;

    private Mesh mesh;
    private Vector3[] pathPoints;
    private MeshRenderer meshRenderer;
    private float currentAngle = 0f;
    private bool isRotating = true;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        SetupMaterial();

        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        GeneratePath();
        GenerateRibbonMesh();
    }

    void SetupMaterial()
    {
        Material mat = (shaderType == ShaderType.RightEyeOnly)
            ? new Material(Shader.Find("Custom/RightEyeOnly"))
            : new Material(Shader.Find("Custom/BinocularUnlit"));

        mat.color = color;
        if (mat.HasProperty("_Brightness")) mat.SetFloat("_Brightness", 1.0f);
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
        float majorR = diameter * 0.5f;
        float minorR = aspectRatio * majorR;

        pathPoints = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float t = i * 2 * Mathf.PI / segments;
            float x = majorR * Mathf.Cos(t);
            float y = minorR * Mathf.Sin(t);
            pathPoints[i] = new Vector3(x, y, 0f);
        }
    }

    // Same XY-perpendicular ribbon construction as TrefoilGenerator.GenerateRibbonMesh().
    void GenerateRibbonMesh()
    {
        Vector3[] vertices  = new Vector3[segments * 2];
        int[]     triangles = new int[segments * 6];

        for (int i = 0; i < segments; i++)
        {
            Vector3 point     = pathPoints[i];
            Vector3 nextPoint = pathPoints[(i + 1) % segments];
            Vector3 tangent   = (nextPoint - point).normalized;

            Vector3 perpendicular = new Vector3(-tangent.y, tangent.x, 0) * width * 0.5f;

            vertices[i * 2]     = point + perpendicular;
            vertices[i * 2 + 1] = point - perpendicular;
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
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    // ── Public API (mirrors TrefoilGenerator) ──────────────────────────────

    public void SetParameters(float diameterValue, float aspect, float speed, int dir)
    {
        diameter      = diameterValue;
        aspectRatio   = Mathf.Clamp(aspect, 0.001f, 1f);
        rotationSpeed = speed;
        direction     = dir;
        GeneratePath();
        GenerateRibbonMesh();
        ResetRotation();
    }

    public void ResetRotation()
    {
        currentAngle = 0f;
        transform.localRotation = Quaternion.identity;
    }

    public void SetVisibility(bool visible)
    {
        if (meshRenderer != null) meshRenderer.enabled = visible;
    }

    public void PauseRotation()  { isRotating = false; }
    public void ResumeRotation() { isRotating = true;  }

    public float GetCurrentAngle() => currentAngle;

    public void SetShaderType(ShaderType type)
    {
        shaderType = type;
        SetupMaterial();
    }

    // ── Geometry helpers for logging (world metres) ────────────────────────

    // World major-axis diameter. Uses lossyScale so the prediction is comparable
    // to the VIVE-tracker ΔZ (also world metres).
    public float GetWorldDiameter() => diameter * transform.lossyScale.x;

    // Implied slant σ = acos(a), in degrees.
    public float GetImpliedSlantDeg() => Mathf.Acos(Mathf.Clamp01(aspectRatio)) * Mathf.Rad2Deg;

    // Veridical (isotropic-scaling) depth extent D·sin(σ) = D·√(1−a²), world metres.
    public float GetImpliedDepthExtent()
        => GetWorldDiameter() * Mathf.Sqrt(Mathf.Max(0f, 1f - aspectRatio * aspectRatio));
}
