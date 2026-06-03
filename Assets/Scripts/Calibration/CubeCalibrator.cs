using System.Collections.Generic;
using UnityEngine;

public class CubeCalibrator : MonoBehaviour
{
    [Header("Cube Parameters")]
    public float edgeLength = 0.3f;

    [Header("Material")]
    [Tooltip("Assign a material using Custom/WireframeCube shader. Created from shader defaults if left empty.")]
    public Material wireframeMaterial;

    // 9 primary traced edges as a vertex-index path: front face → one depth edge → back face
    private static readonly int[] polylineOrder = { 0, 1, 2, 3, 0, 4, 5, 6, 7, 4 };

    // 6 faces: each row lists 4 local vertex indices arranged so that UV (0,0)→(1,0)→(1,1)→(0,1)
    // aligns to actual cube edges (no diagonals). Faces 0-1 = front/back (primary, all edges drawn).
    // Faces 2-5 = side faces (secondary): UV V-axis = depth direction so the shader can draw only
    // depth edges in the secondary color without redrawing face edges already covered by front/back.
    private static readonly int[,] faceVertices = {
        { 0, 1, 2, 3 }, // front  (z = -h) — primary
        { 5, 4, 7, 6 }, // back   (z = +h) — primary
        { 4, 0, 3, 7 }, // left   (x = -h) — secondary, V=depth (V0=edge 4-0, V1=edge 7-3)
        { 1, 5, 6, 2 }, // right  (x = +h) — secondary, V=depth (V0=edge 1-5, V1=edge 2-6)
        { 4, 0, 1, 5 }, // bottom (y = -h) — secondary, V=depth (V0=edge 4-0, V1=edge 5-1)
        { 7, 3, 2, 6 }, // top    (y = +h) — secondary, V=depth (V0=edge 7-3, V1=edge 6-2)
    };

    private Vector3[] localVertices;
    private MeshRenderer meshRenderer;

    private bool isRotating = false;
    private float rotSpeed = 0f;

    void Start() => BuildCube();

    void Update()
    {
        if (isRotating)
            transform.Rotate(Vector3.forward, rotSpeed * Time.deltaTime);
    }

    void BuildCube()
    {
        float h = edgeLength * 0.5f;

        localVertices = new Vector3[]
        {
            new Vector3(-h, -h, -h), // 0 front-left-bottom
            new Vector3( h, -h, -h), // 1 front-right-bottom
            new Vector3( h,  h, -h), // 2 front-right-top
            new Vector3(-h,  h, -h), // 3 front-left-top
            new Vector3(-h, -h,  h), // 4 back-left-bottom
            new Vector3( h, -h,  h), // 5 back-right-bottom
            new Vector3( h,  h,  h), // 6 back-right-top
            new Vector3(-h,  h,  h), // 7 back-left-top
        };

        // 6 faces × 4 verts, each face gets its own UV (0,0)→(1,1) range so the
        // shader's UV-edge detection maps to exactly the 12 cube edges.
        // UV2.x = 0 for front/back (primary), 1 for side faces (secondary/depth-only).
        var verts = new Vector3[24];
        var uvs   = new Vector2[24];
        var uv2s  = new Vector2[24];
        var faceUVCorners = new Vector2[] {
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)
        };

        for (int f = 0; f < 6; f++)
        {
            float secondary = f >= 2 ? 1f : 0f;
            for (int v = 0; v < 4; v++)
            {
                verts[f * 4 + v] = localVertices[faceVertices[f, v]];
                uvs  [f * 4 + v] = faceUVCorners[v];
                uv2s [f * 4 + v] = new Vector2(secondary, 0f);
            }
        }

        var tris = new int[36];
        for (int f = 0; f < 6; f++)
        {
            int b = f * 4;
            tris[f * 6 + 0] = b;     tris[f * 6 + 1] = b + 1; tris[f * 6 + 2] = b + 2;
            tris[f * 6 + 3] = b;     tris[f * 6 + 4] = b + 2; tris[f * 6 + 5] = b + 3;
        }

        var mesh = new Mesh();
        mesh.vertices  = verts;
        mesh.uv        = uvs;
        mesh.uv2       = uv2s;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        var mf = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        meshRenderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        if (wireframeMaterial != null)
        {
            meshRenderer.material = wireframeMaterial;
        }
        else
        {
            var mat = new Material(Shader.Find("Custom/WireframeCube"));
            mat.SetColor("_EdgeColor",      Color.white);
            mat.SetColor("_SecondaryColor", new Color(0.4f, 0.4f, 0.4f, 1f));
            mat.SetFloat("_LineThickness",  0.02f);
            meshRenderer.material = mat;
        }
    }

    public void StartRotating(float speed) { rotSpeed = speed; isRotating = true; }
    public void StopRotating() { isRotating = false; rotSpeed = 0f; }

    public void SetVisibility(bool visible)
    {
        if (meshRenderer != null) meshRenderer.enabled = visible;
    }

    // Returns the nearest point on the 9 primary traced edges in world space.
    public Vector3 GetNearestCurveWorldPoint(Vector3 worldPos)
    {
        float   minDist = float.MaxValue;
        Vector3 nearest = worldPos;
        for (int i = 0; i < polylineOrder.Length - 1; i++)
        {
            Vector3 segStart = transform.TransformPoint(localVertices[polylineOrder[i]]);
            Vector3 segEnd   = transform.TransformPoint(localVertices[polylineOrder[i + 1]]);
            Vector3 segDir   = (segEnd - segStart).normalized;
            float   segLen   = Vector3.Distance(segStart, segEnd);
            float   proj     = Mathf.Clamp(Vector3.Dot(worldPos - segStart, segDir), 0f, segLen);
            Vector3 closest  = segStart + segDir * proj;
            float   d        = Vector3.Distance(worldPos, closest);
            if (d < minDist) { minDist = d; nearest = closest; }
        }
        return nearest;
    }

    // Stubs kept for HandTrackingExperimentManager compatibility.
    public void HighlightEdge(int edgeIndex) { }
    public void ClearHighlight() { }

    public float GetEdgeLength() => edgeLength;

    // Legacy accessors used by CalculateMotorError.
    public Vector3 GetEdgeStart(int edgeIndex)
    {
        if (localVertices == null || edgeIndex < 0) return Vector3.zero;
        int v = polylineOrder[Mathf.Clamp(edgeIndex, 0, polylineOrder.Length - 1)];
        return transform.TransformPoint(localVertices[v]);
    }

    public Vector3 GetEdgeEnd(int edgeIndex)
    {
        if (localVertices == null || edgeIndex < 0) return Vector3.zero;
        int v = polylineOrder[Mathf.Clamp(edgeIndex + 1, 0, polylineOrder.Length - 1)];
        return transform.TransformPoint(localVertices[v]);
    }

    public float CalculateMotorError(int edgeIndex, List<Vector3> tracedPoints)
    {
        if (tracedPoints.Count == 0) return 0f;
        Vector3 edgeStart = GetEdgeStart(edgeIndex);
        Vector3 edgeEnd   = GetEdgeEnd(edgeIndex);
        Vector3 edgeDir   = (edgeEnd - edgeStart).normalized;
        float   totalError = 0f;
        foreach (Vector3 point in tracedPoints)
        {
            float   proj    = Vector3.Dot(point - edgeStart, edgeDir);
            Vector3 closest = edgeStart + edgeDir * Mathf.Clamp(proj, 0, edgeLength);
            totalError += Vector3.Distance(point, closest);
        }
        return totalError / tracedPoints.Count;
    }
}
