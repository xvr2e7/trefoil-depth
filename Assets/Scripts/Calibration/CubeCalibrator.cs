using System.Collections.Generic;
using UnityEngine;

public class CubeCalibrator : MonoBehaviour
{
    [Header("Cube Parameters")]
    public float edgeLength = 0.3f;
    public Color cubeColor = Color.black;
    public float lineWidth = 0.005f;

    [Header("Secondary Edges")]
    public Color dimColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    public float dimWidth = 0.002f;

    [Header("Edge Highlighting")]
    public Color highlightColor = Color.yellow;
    public float highlightWidth = 0.008f;

    // Polyline through all 9 primary edges: front face → connecting edge → back face
    private static readonly int[] polylineOrder = { 0, 1, 2, 3, 0, 4, 5, 6, 7, 4 };

    // Secondary dim depth edges (the 3 not in the primary polyline): v1→v5, v2→v6, v3→v7
    private static readonly int[,] secondaryVerts = { { 1, 5 }, { 2, 6 }, { 3, 7 } };

    private Vector3[] localVertices;
    private LineRenderer outlineRenderer;
    private LineRenderer[] secondaryRenderers;

    private bool isRotating = false;
    private float rotSpeed = 0f;

    void Start() => BuildCube();

    void Update()
    {
        if (isRotating)
            transform.Rotate(Vector3.forward, rotSpeed * Time.deltaTime);
        UpdatePolylinePositions();
    }

    void UpdatePolylinePositions()
    {
        if (outlineRenderer == null || localVertices == null) return;
        for (int i = 0; i < polylineOrder.Length; i++)
            outlineRenderer.SetPosition(i, transform.TransformPoint(localVertices[polylineOrder[i]]));
        for (int i = 0; i < 3; i++)
        {
            secondaryRenderers[i].SetPosition(0, transform.TransformPoint(localVertices[secondaryVerts[i, 0]]));
            secondaryRenderers[i].SetPosition(1, transform.TransformPoint(localVertices[secondaryVerts[i, 1]]));
        }
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

        // Single LineRenderer for the 9 primary edges as one continuous path
        GameObject outlineObj = new GameObject("CubeOutline");
        outlineObj.transform.SetParent(transform);
        outlineObj.transform.localPosition = Vector3.zero;
        outlineObj.transform.localRotation = Quaternion.identity;
        outlineRenderer = outlineObj.AddComponent<LineRenderer>();
        outlineRenderer.useWorldSpace = true;
        outlineRenderer.positionCount = polylineOrder.Length;
        outlineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        outlineRenderer.startColor = outlineRenderer.endColor = cubeColor;
        outlineRenderer.startWidth = outlineRenderer.endWidth = lineWidth;
        outlineRenderer.numCornerVertices = 4;
        outlineRenderer.alignment = LineAlignment.TransformZ;

        // Three dim secondary depth edges
        secondaryRenderers = new LineRenderer[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject secObj = new GameObject($"SecondaryEdge_{i}");
            secObj.transform.SetParent(transform);
            secObj.transform.localPosition = Vector3.zero;
            secObj.transform.localRotation = Quaternion.identity;
            LineRenderer lr = secObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = dimColor;
            lr.startWidth = lr.endWidth = dimWidth;
            lr.alignment = LineAlignment.TransformZ;
            secondaryRenderers[i] = lr;
        }

        UpdatePolylinePositions();
    }

    public void StartRotating(float speed) { rotSpeed = speed; isRotating = true; }
    public void StopRotating() { isRotating = false; rotSpeed = 0f; }

    public void SetVisibility(bool visible)
    {
        if (outlineRenderer != null) outlineRenderer.enabled = visible;
        if (secondaryRenderers != null)
            foreach (var lr in secondaryRenderers)
                if (lr != null) lr.enabled = visible;
    }

    // Returns the nearest point on the primary polyline in world space.
    public Vector3 GetNearestCurveWorldPoint(Vector3 worldPos)
    {
        float minDist = float.MaxValue;
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

    // Kept for HandTrackingExperimentManager — stub until that scene is updated.
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
        float totalError  = 0f;
        foreach (Vector3 point in tracedPoints)
        {
            float   proj    = Vector3.Dot(point - edgeStart, edgeDir);
            Vector3 closest = edgeStart + edgeDir * Mathf.Clamp(proj, 0, edgeLength);
            totalError += Vector3.Distance(point, closest);
        }
        return totalError / tracedPoints.Count;
    }
}
