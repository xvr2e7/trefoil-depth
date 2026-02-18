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
        Material mat;
        if (shaderType == ShaderType.RightEyeOnly)
        {
            mat = new Material(Shader.Find("Custom/RightEyeOnly"));
        }
        else
        {
            mat = new Material(Shader.Find("Custom/BinocularUnlit"));
        }
        mat.color = Color.white;
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

            pathPoints[i] = new Vector3(x, y, 0);
        }
    }

    void GenerateRibbonMesh()
    {
        Vector3[] vertices = new Vector3[segments * 2];
        int[] triangles = new int[segments * 6];

        for (int i = 0; i < segments; i++)
        {
            Vector3 point = pathPoints[i];
            Vector3 nextPoint = pathPoints[(i + 1) % segments];
            Vector3 tangent = (nextPoint - point).normalized;

            Vector3 perpendicular = new Vector3(-tangent.y, tangent.x, 0) * width * 0.5f;

            vertices[i * 2] = point + perpendicular;
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
        mesh.vertices = vertices;
        mesh.triangles = triangles;
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
        GeneratePath();
        GenerateRibbonMesh();
        ResetRotation();
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