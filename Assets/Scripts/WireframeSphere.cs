using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WireframeSphere : MonoBehaviour
{
    [Header("Sphere Parameters")]
    public float radius = 0.15f;  // Sphere radius in meters (controlled by depth parameter)
    public float depth = 1.0f;  // The depth/size of the sphere (corresponds to trefoil amplitude)

    [Header("Trefoil Calibration")]
    public float trefoilBaseZExtent = 7.068497f;  // Base z-extent of R2=1.5 trefoil (calculated from CSV)
    public int latitudeSegments = 32;  // High segment count for smooth 2D circle projection
    public int longitudeSegments = 48;  // High segment count for smooth 2D circle projection
    public float lineWidth = 0.015f;
    public int tubeSegments = 6;  // Number of sides for each tube

    [Header("Rotation Settings")]
    public float rotationSpeed = 60f;  // Rotation speed in degrees per second (matches trefoil)
    private float currentRotationAngle = 0f;
    private bool isRotating = false;

    [Header("Viewing Mode")]
    [Tooltip("Use standard shader (binocular) for debugging, or right-eye-only shader (monocular) for experiment")]
    public bool useBinocularView = true;  // Toggle for easy debugging
    public Shader rightEyeOnlyShader;  // Assign custom right-eye-only shader for monocular viewing

    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private Vector3 basePosition;  // Store the initial position

    // From CSV analysis: max z-value in coords_R2_1.5.csv
    private const float TREFOIL_MAX_Z_BASE = 3.5243f;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        UpdateMaterial();

        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        basePosition = transform.localPosition;
        UpdateSphere();
        meshRenderer.enabled = false;
        currentRotationAngle = 0f;
    }

    void UpdateMaterial()
    {
        Material mat;
        if (useBinocularView || rightEyeOnlyShader == null)
        {
            // Use standard shader for binocular viewing (debugging mode)
            mat = new Material(Shader.Find("Standard"));
        }
        else
        {
            // Use right-eye-only shader for monocular viewing (experiment mode)
            mat = new Material(rightEyeOnlyShader);
        }
        mat.color = Color.white;
        meshRenderer.material = mat;
    }

    void Update()
    {
        // Continuous rotation around Z-axis (in-plane) when enabled, matching 2D trefoil rotation
        if (isRotating)
        {
            currentRotationAngle += rotationSpeed * Time.deltaTime;
            if (currentRotationAngle >= 360f)
            {
                currentRotationAngle -= 360f;
            }
            transform.localRotation = Quaternion.Euler(0f, 0f, currentRotationAngle);
        }
    }

    void GenerateWireframeSphere()
    {
        System.Collections.Generic.List<Vector3> vertices = new System.Collections.Generic.List<Vector3>();
        System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();

        // Generate latitude circles (horizontal rings around the sphere)
        for (int lat = 1; lat < latitudeSegments; lat++)
        {
            float theta = (lat / (float)latitudeSegments) * Mathf.PI;

            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                float phi = (lon / (float)longitudeSegments) * 2 * Mathf.PI;
                float nextPhi = ((lon + 1) / (float)longitudeSegments) * 2 * Mathf.PI;

                Vector3 p1 = SphericalToCartesian(radius, theta, phi);
                Vector3 p2 = SphericalToCartesian(radius, theta, nextPhi);

                AddTube(vertices, triangles, p1, p2, lineWidth);
            }
        }

        // Generate longitude circles (meridians from pole to pole)
        for (int lon = 0; lon < longitudeSegments; lon++)
        {
            float phi = (lon / (float)longitudeSegments) * 2 * Mathf.PI;

            for (int lat = 0; lat < latitudeSegments; lat++)
            {
                float theta1 = (lat / (float)latitudeSegments) * Mathf.PI;
                float theta2 = ((lat + 1) / (float)latitudeSegments) * Mathf.PI;

                Vector3 p1 = SphericalToCartesian(radius, theta1, phi);
                Vector3 p2 = SphericalToCartesian(radius, theta2, phi);

                AddTube(vertices, triangles, p1, p2, lineWidth);
            }
        }

        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    Vector3 SphericalToCartesian(float r, float theta, float phi)
    {
        // Generate 2D projection (orthographic projection from front view)
        // Project sphere onto XY plane (z=0), as if viewing from +Z direction
        // This creates a flat circle that represents the perceived depth through size
        float x = r * Mathf.Sin(theta) * Mathf.Cos(phi);
        float y = r * Mathf.Cos(theta);
        float z = 0f;  // Flatten to z=0 for true 2D projection
        return new Vector3(x, y, z);
    }

    void UpdateSphere()
    {
        // The 'depth' parameter corresponds to trefoil amplitude
        // Scale sphere diameter to match the z-extent of the trefoil at that amplitude
        // trefoil z-extent = depth * trefoilBaseZExtent
        // sphere diameter = z-extent, so radius = z-extent / 2
        radius = (depth * trefoilBaseZExtent) / 2.0f;
        GenerateWireframeSphere();

        // The 2D sphere (flattened to z=0 in local space) should be positioned so that
        // its plane aligns with where the trefoil's front face would be.
        // This is handled by the scene setup - the sphere GameObject should be positioned
        // at the same z-coordinate as the trefoil GameObject in world space.
        // No position adjustment needed here since the sphere is already a flat 2D projection.
        transform.localPosition = basePosition;
    }

    void AddTube(System.Collections.Generic.List<Vector3> vertices,
                 System.Collections.Generic.List<int> triangles,
                 Vector3 start, Vector3 end, float width)
    {
        Vector3 direction = (end - start).normalized;

        // Find two perpendicular vectors to the tube direction
        Vector3 perp1;
        if (Mathf.Abs(Vector3.Dot(direction, Vector3.up)) < 0.99f)
        {
            perp1 = Vector3.Cross(direction, Vector3.up).normalized;
        }
        else
        {
            perp1 = Vector3.Cross(direction, Vector3.right).normalized;
        }
        Vector3 perp2 = Vector3.Cross(direction, perp1).normalized;

        int startVertexIndex = vertices.Count;

        // Create vertices around the tube at both start and end points
        for (int i = 0; i < tubeSegments; i++)
        {
            float angle = (i / (float)tubeSegments) * 2 * Mathf.PI;
            Vector3 offset = (perp1 * Mathf.Cos(angle) + perp2 * Mathf.Sin(angle)) * width * 0.5f;

            vertices.Add(start + offset);
            vertices.Add(end + offset);
        }

        // Create triangles connecting the vertices
        for (int i = 0; i < tubeSegments; i++)
        {
            int next = (i + 1) % tubeSegments;

            int v0 = startVertexIndex + i * 2;
            int v1 = startVertexIndex + i * 2 + 1;
            int v2 = startVertexIndex + next * 2 + 1;
            int v3 = startVertexIndex + next * 2;

            // Two triangles per quad
            triangles.Add(v0);
            triangles.Add(v1);
            triangles.Add(v2);

            triangles.Add(v0);
            triangles.Add(v2);
            triangles.Add(v3);
        }
    }

    public void SetDepth(float depthValue)
    {
        depth = depthValue;
        UpdateSphere();
    }

    public void SetVisibility(bool visible)
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = visible;
        }

        // Automatically pause rotation when hidden, resume when visible (per spec line 162)
        if (visible)
        {
            ResumeRotation();
        }
        else
        {
            PauseRotation();
        }
    }

    public float GetDepth()
    {
        return depth;
    }

    public void SetRadius(float radiusValue)
    {
        radius = radiusValue;
        GenerateWireframeSphere();
    }

    /// <summary>
    /// Start continuous rotation at specified speed (default 60 deg/s per spec)
    /// </summary>
    public void StartRotation()
    {
        isRotating = true;
    }

    /// <summary>
    /// Pause rotation while maintaining current angle
    /// </summary>
    public void PauseRotation()
    {
        isRotating = false;
    }

    /// <summary>
    /// Resume rotation from current angle
    /// </summary>
    public void ResumeRotation()
    {
        isRotating = true;
    }

    /// <summary>
    /// Stop rotation and reset angle to zero
    /// </summary>
    public void StopRotation()
    {
        isRotating = false;
        currentRotationAngle = 0f;
        transform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// Set viewing mode (binocular for debugging, monocular for experiment)
    /// </summary>
    public void SetBinocularView(bool binocular)
    {
        useBinocularView = binocular;
        UpdateMaterial();
    }
}
