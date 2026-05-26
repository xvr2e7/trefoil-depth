using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Displays a wireframe cube for motor calibration.
/// Participants trace along designated edges to establish baseline motor error.
/// </summary>
public class CubeCalibrator : MonoBehaviour
{
    [Header("Cube Parameters")]
    public float edgeLength = 0.3f; // 30cm cube
    public Color cubeColor = Color.black;
    public float lineWidth = 0.005f;

    [Header("Edge Highlighting")]
    public Color highlightColor = Color.yellow;
    public float highlightWidth = 0.008f;

    private LineRenderer[] edgeRenderers;
    private int highlightedEdgeIndex = -1;
    private bool isRotating = false;
    private float rotSpeed = 0f;

    // Cube has 12 edges
    private static readonly int[,] edgeIndices = new int[12, 2]
    {
        // Bottom face
        {0, 1}, {1, 2}, {2, 3}, {3, 0},
        // Top face
        {4, 5}, {5, 6}, {6, 7}, {7, 4},
        // Vertical edges
        {0, 4}, {1, 5}, {2, 6}, {3, 7}
    };

    void Start()
    {
        GenerateWireframeCube();
    }
    void Update()
{
    if (isRotating)
        transform.Rotate(Vector3.up, rotSpeed * Time.deltaTime);
}

public void StartRotating(float speed)
{
    rotSpeed = speed;
    isRotating = true;
}

public void StopRotating()
{
    isRotating = false;
    rotSpeed = 0f;
}
    void GenerateWireframeCube()
    {
        // Calculate 8 vertices of the cube centered at origin
        Vector3[] vertices = new Vector3[8];
        float half = edgeLength * 0.5f;

        vertices[0] = new Vector3(-half, -half, -half); // Bottom face
        vertices[1] = new Vector3(half, -half, -half);
        vertices[2] = new Vector3(half, half, -half);
        vertices[3] = new Vector3(-half, half, -half);
        vertices[4] = new Vector3(-half, -half, half);  // Top face
        vertices[5] = new Vector3(half, -half, half);
        vertices[6] = new Vector3(half, half, half);
        vertices[7] = new Vector3(-half, half, half);

        // Create LineRenderers for each edge
        edgeRenderers = new LineRenderer[12];

        for (int i = 0; i < 12; i++)
        {
            GameObject edgeObj = new GameObject($"Edge_{i}");
            edgeObj.transform.SetParent(transform);
            edgeObj.transform.localPosition = Vector3.zero;
            edgeObj.transform.localRotation = Quaternion.identity;

            LineRenderer lr = edgeObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = cubeColor;
            lr.endColor = cubeColor;
            lr.useWorldSpace = false;

            // Set positions for this edge
            int v0 = edgeIndices[i, 0];
            int v1 = edgeIndices[i, 1];
            lr.SetPosition(0, vertices[v0]);
            lr.SetPosition(1, vertices[v1]);

            edgeRenderers[i] = lr;
        }
    }

    public void SetVisibility(bool visible)
    {
        if (edgeRenderers != null)
        {
            foreach (var lr in edgeRenderers)
            {
                if (lr != null)
                    lr.enabled = visible;
            }
        }
    }

    public void HighlightEdge(int edgeIndex)
    {
        // Reset previous highlight
        if (highlightedEdgeIndex >= 0 && highlightedEdgeIndex < edgeRenderers.Length)
        {
            edgeRenderers[highlightedEdgeIndex].startColor = cubeColor;
            edgeRenderers[highlightedEdgeIndex].endColor = cubeColor;
            edgeRenderers[highlightedEdgeIndex].startWidth = lineWidth;
            edgeRenderers[highlightedEdgeIndex].endWidth = lineWidth;
        }

        // Set new highlight
        if (edgeIndex >= 0 && edgeIndex < edgeRenderers.Length)
        {
            edgeRenderers[edgeIndex].startColor = highlightColor;
            edgeRenderers[edgeIndex].endColor = highlightColor;
            edgeRenderers[edgeIndex].startWidth = highlightWidth;
            edgeRenderers[edgeIndex].endWidth = highlightWidth;
            highlightedEdgeIndex = edgeIndex;
        }
        else
        {
            highlightedEdgeIndex = -1;
        }
    }

    public void ClearHighlight()
    {
        HighlightEdge(-1);
    }

    public Vector3 GetEdgeStart(int edgeIndex)
    {
        if (edgeIndex < 0 || edgeIndex >= edgeRenderers.Length)
            return Vector3.zero;

        Vector3 localPos = edgeRenderers[edgeIndex].GetPosition(0);
        return transform.TransformPoint(localPos);
    }

    public Vector3 GetEdgeEnd(int edgeIndex)
    {
        if (edgeIndex < 0 || edgeIndex >= edgeRenderers.Length)
            return Vector3.zero;

        Vector3 localPos = edgeRenderers[edgeIndex].GetPosition(1);
        return transform.TransformPoint(localPos);
    }

    public float GetEdgeLength()
    {
        return edgeLength;
    }

    public int GetEdgeCount()
    {
        return 12;
    }

    /// <summary>
    /// Calculate motor error for a traced edge segment.
    /// Returns average perpendicular distance from traced points to the true edge line.
    /// </summary>
    public float CalculateMotorError(int edgeIndex, List<Vector3> tracedPoints)
    {
        if (edgeIndex < 0 || edgeIndex >= edgeRenderers.Length || tracedPoints.Count == 0)
            return 0f;

        Vector3 edgeStart = GetEdgeStart(edgeIndex);
        Vector3 edgeEnd = GetEdgeEnd(edgeIndex);
        Vector3 edgeDir = (edgeEnd - edgeStart).normalized;

        float totalError = 0f;

        foreach (Vector3 point in tracedPoints)
        {
            // Project point onto the edge line
            Vector3 toPoint = point - edgeStart;
            float projection = Vector3.Dot(toPoint, edgeDir);
            Vector3 closestPointOnEdge = edgeStart + edgeDir * Mathf.Clamp(projection, 0, edgeLength);

            // Calculate perpendicular distance
            float distance = Vector3.Distance(point, closestPointOnEdge);
            totalError += distance;
        }

        return totalError / tracedPoints.Count;
    }
}
