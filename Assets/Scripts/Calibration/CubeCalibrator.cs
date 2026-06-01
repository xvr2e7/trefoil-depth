using System.Collections.Generic;
using UnityEngine;

public class CubeCalibrator : MonoBehaviour
{
    [Header("Cube Parameters")]
    public float edgeLength = 0.3f;
    public Color cubeColor = Color.black;
    public float lineWidth = 0.005f;

    [Header("Edge Highlighting")]
    public Color highlightColor = Color.yellow;
    public float highlightWidth = 0.008f;
    public Color dimColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    public float dimWidth = 0.002f;

    private LineRenderer[] edgeRenderers;
    private int highlightedEdgeIndex = -1;
    private bool isRotating = false;
    private float rotSpeed = 0f;

   
    private static readonly int[,] edgeIndices = new int[12, 2]
    {
        {0, 1}, {1, 2}, {2, 3}, {3, 0},   
        {4, 5}, {5, 6}, {6, 7}, {7, 4},   
        {0, 4}, {1, 5}, {2, 6}, {3, 7}   
    };

    
    private static readonly int[] primaryEdges   = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
    private static readonly int[] secondaryEdges = { 9, 10, 11 };

    void Start()
    {
        GenerateWireframeCube();
    }

    void Update()
    {
        if (isRotating)
            transform.Rotate(Vector3.forward, rotSpeed * Time.deltaTime);
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
        Vector3[] vertices = new Vector3[8];
        float half = edgeLength * 0.5f;

        vertices[0] = new Vector3(-half, -half, -half);
        vertices[1] = new Vector3( half, -half, -half);
        vertices[2] = new Vector3( half,  half, -half);
        vertices[3] = new Vector3(-half,  half, -half);
        vertices[4] = new Vector3(-half, -half,  half);
        vertices[5] = new Vector3( half, -half,  half);
        vertices[6] = new Vector3( half,  half,  half);
        vertices[7] = new Vector3(-half,  half,  half);

        edgeRenderers = new LineRenderer[12];

        for (int i = 0; i < 12; i++)
        {
            GameObject edgeObj = new GameObject($"Edge_{i}");
            edgeObj.transform.SetParent(transform);
            edgeObj.transform.localPosition = Vector3.zero;
            edgeObj.transform.localRotation = Quaternion.identity;

            LineRenderer lr = edgeObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.useWorldSpace = false;

            int v0 = edgeIndices[i, 0];
            int v1 = edgeIndices[i, 1];
            lr.SetPosition(0, vertices[v0]);
            lr.SetPosition(1, vertices[v1]);

            // Secondary edges start dim
            bool isDim = System.Array.IndexOf(secondaryEdges, i) >= 0;
            lr.startColor = isDim ? dimColor : cubeColor;
            lr.endColor   = isDim ? dimColor : cubeColor;
            lr.startWidth = isDim ? dimWidth : lineWidth;
            lr.endWidth   = isDim ? dimWidth : lineWidth;

            edgeRenderers[i] = lr;
        }
    }

    public void SetVisibility(bool visible)
    {
        if (edgeRenderers == null) return;
        foreach (var lr in edgeRenderers)
            if (lr != null) lr.enabled = visible;
    }

    public void HighlightEdge(int edgeIndex)
    {
        // Reset previous highlight
        if (highlightedEdgeIndex >= 0 && highlightedEdgeIndex < edgeRenderers.Length)
        {
            bool wasDim = System.Array.IndexOf(secondaryEdges, highlightedEdgeIndex) >= 0;
            edgeRenderers[highlightedEdgeIndex].startColor = wasDim ? dimColor : cubeColor;
            edgeRenderers[highlightedEdgeIndex].endColor   = wasDim ? dimColor : cubeColor;
            edgeRenderers[highlightedEdgeIndex].startWidth = wasDim ? dimWidth : lineWidth;
            edgeRenderers[highlightedEdgeIndex].endWidth   = wasDim ? dimWidth : lineWidth;
        }

        if (edgeIndex >= 0 && edgeIndex < edgeRenderers.Length)
        {
            edgeRenderers[edgeIndex].startColor = highlightColor;
            edgeRenderers[edgeIndex].endColor   = highlightColor;
            edgeRenderers[edgeIndex].startWidth = highlightWidth;
            edgeRenderers[edgeIndex].endWidth   = highlightWidth;
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
        if (edgeIndex < 0 || edgeIndex >= edgeRenderers.Length) return Vector3.zero;
        return transform.TransformPoint(edgeRenderers[edgeIndex].GetPosition(0));
    }

    public Vector3 GetEdgeEnd(int edgeIndex)
    {
        if (edgeIndex < 0 || edgeIndex >= edgeRenderers.Length) return Vector3.zero;
        return transform.TransformPoint(edgeRenderers[edgeIndex].GetPosition(1));
    }

    public float GetEdgeLength() => edgeLength;
    public int GetEdgeCount() => 12;

    public float CalculateMotorError(int edgeIndex, List<Vector3> tracedPoints)
    {
        if (edgeIndex < 0 || edgeIndex >= edgeRenderers.Length || tracedPoints.Count == 0)
            return 0f;

        Vector3 edgeStart = GetEdgeStart(edgeIndex);
        Vector3 edgeEnd   = GetEdgeEnd(edgeIndex);
        Vector3 edgeDir   = (edgeEnd - edgeStart).normalized;
        float totalError  = 0f;

        foreach (Vector3 point in tracedPoints)
        {
            Vector3 toPoint = point - edgeStart;
            float projection = Vector3.Dot(toPoint, edgeDir);
            Vector3 closest = edgeStart + edgeDir * Mathf.Clamp(projection, 0, edgeLength);
            totalError += Vector3.Distance(point, closest);
        }

        return totalError / tracedPoints.Count;
    }
}