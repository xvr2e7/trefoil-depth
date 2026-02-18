using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class HandTrackingTracer : MonoBehaviour
{
    [Header("Tracing Settings")]
    public Transform frozenTrefoil;
    public float minTraceDistance = 0.01f; // Minimum distance between trace points
    public float maxTraceDistance = 0.5f; // Maximum distance to consider valid trace
    public float pinchThreshold = 0.03f; // Distance in meters between thumb and index to trigger pinch
    public float pinchReleaseThreshold = 0.04f; // When fingers are this far apart, stop recording (before full release)
    public float pinchStartDelay = 0.3f; // Delay before starting to record trace after pinch
    public float eraseRadius = 0.02f; // Radius around left index finger to erase points

    [Header("Visual Feedback")]
    public GameObject tracePointPrefab;
    public LineRenderer traceLineRenderer; // Template for creating line renderers
    public Color traceColor = Color.cyan;
    public float traceLineWidth = 0.02f;

    // Store all traces (each pinch session creates a new trace)
    private List<List<Vector3>> allTraces = new List<List<Vector3>>();
    private List<List<GameObject>> allTraceVisuals = new List<List<GameObject>>();
    private List<LineRenderer> allLineRenderers = new List<LineRenderer>(); // One LineRenderer per trace

    // Current active trace being drawn
    private List<Vector3> currentTrace = null;
    private List<GameObject> currentTraceVisuals = null;
    private Vector3 lastTracedPoint;
    private bool tracingEnabled = false;

    // Right hand state
    private bool isRightTracing = false;
    private bool isRightRecording = false;
    private bool lastRightPinchState = false;
    private float rightPinchStartTime = 0f;

    // Left hand state
    private bool isLeftTracing = false;
    private bool isLeftRecording = false;
    private bool lastLeftPinchState = false;
    private float leftPinchStartTime = 0f;

    private XRHandSubsystem handSubsystem;

    void Start()
    {
        InitializeHandTracking();

        if (traceLineRenderer != null)
        {
            traceLineRenderer.startWidth = traceLineWidth;
            traceLineRenderer.endWidth = traceLineWidth;
            traceLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            traceLineRenderer.startColor = traceColor;
            traceLineRenderer.endColor = traceColor;
            traceLineRenderer.positionCount = 0;
        }
    }

    void InitializeHandTracking()
    {
        var handSubsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(handSubsystems);

        if (handSubsystems.Count > 0)
        {
            handSubsystem = handSubsystems[0];
            Debug.Log($"Hand tracking subsystem found: {handSubsystem.running}");
        }
        else
        {
            Debug.LogWarning("No XR Hand subsystem found! Hand tracking will not work.");
        }
    }

    void Update()
    {
        if (!tracingEnabled || handSubsystem == null)
            return;

        // Get both hands
        XRHand rightHand = handSubsystem.rightHand;
        XRHand leftHand = handSubsystem.leftHand;

        // Process right hand for drawing
        ProcessDrawingHand(rightHand, ref isRightTracing, ref isRightRecording, ref lastRightPinchState, ref rightPinchStartTime);

        // Process left hand for erasing
        ProcessErasingHand(leftHand);
    }

    void ProcessDrawingHand(XRHand hand, ref bool isTracing, ref bool isRecording, ref bool lastPinchState, ref float pinchStartTime)
    {
        if (!hand.isTracked)
            return;

        bool isPinching = CheckPinchGesture(hand);
        bool isReleasing = CheckPinchReleasing(hand);

        if (isPinching && !lastPinchState)
        {
            // Pinch just started - begin a new trace session
            pinchStartTime = Time.time;
            isTracing = true;
            isRecording = false;
            Debug.Log("Right hand: Pinch started, preparing new trace");
        }
        else if (!isPinching && lastPinchState)
        {
            // Pinch fully released - finalize the current trace
            if (isRecording && currentTrace != null && currentTrace.Count > 0)
            {
                Debug.Log($"Right hand: Pinch released, finalizing trace with {currentTrace.Count} points");
                // Finalize current trace (it's already in allTraces, just stop recording)
            }
            isTracing = false;
            isRecording = false;
            currentTrace = null;
            currentTraceVisuals = null;
        }
        else if (isPinching && isTracing)
        {
            float timeSincePinch = Time.time - pinchStartTime;

            // Check if fingers are separating (releasing) - stop recording early
            if (isRecording && isReleasing)
            {
                Debug.Log("Right hand: Pinch releasing detected - stopping recording early");
                isRecording = false;
                // Finalize trace but keep isTracing true so we don't restart
            }
            // Check if we should start recording after delay
            else if (!isRecording && timeSincePinch >= pinchStartDelay)
            {
                // Start a NEW trace for this pinch session
                isRecording = true;
                currentTrace = new List<Vector3>();
                currentTraceVisuals = new List<GameObject>();
                allTraces.Add(currentTrace);
                allTraceVisuals.Add(currentTraceVisuals);

                // Create a new LineRenderer for this trace
                if (traceLineRenderer != null)
                {
                    GameObject lineObj = new GameObject($"TraceLineRenderer_{allTraces.Count}");
                    lineObj.transform.SetParent(transform);
                    LineRenderer newLineRenderer = lineObj.AddComponent<LineRenderer>();

                    // Copy settings from template
                    newLineRenderer.startWidth = traceLineWidth;
                    newLineRenderer.endWidth = traceLineWidth;
                    newLineRenderer.material = traceLineRenderer.material != null ? traceLineRenderer.material : new Material(Shader.Find("Sprites/Default"));
                    newLineRenderer.startColor = traceColor;
                    newLineRenderer.endColor = traceColor;
                    newLineRenderer.positionCount = 0;

                    allLineRenderers.Add(newLineRenderer);
                }

                Debug.Log($"Right hand: Starting new trace #{allTraces.Count}");

                Vector3 startPoint = GetIndexTipPosition(hand);
                lastTracedPoint = startPoint;
                AddTracePoint(startPoint);
            }
            else if (isRecording && !isReleasing)
            {
                // Continue recording current trace
                UpdateTrace(hand);
            }
        }

        lastPinchState = isPinching;
    }

    void ProcessErasingHand(XRHand hand)
    {
        if (!hand.isTracked)
            return;

        // Get left index finger position
        Vector3 eraserPos = GetIndexTipPosition(hand);

        // Check if eraser is near any trace points
        for (int traceIdx = allTraces.Count - 1; traceIdx >= 0; traceIdx--)
        {
            var trace = allTraces[traceIdx];
            var traceVisuals = allTraceVisuals[traceIdx];

            for (int pointIdx = trace.Count - 1; pointIdx >= 0; pointIdx--)
            {
                float distance = Vector3.Distance(eraserPos, trace[pointIdx]);

                if (distance < eraseRadius)
                {
                    // Remove this point
                    trace.RemoveAt(pointIdx);

                    // Remove visual
                    if (pointIdx < traceVisuals.Count)
                    {
                        Destroy(traceVisuals[pointIdx]);
                        traceVisuals.RemoveAt(pointIdx);
                    }
                }
            }

            // If trace is now empty, remove it entirely
            if (trace.Count == 0)
            {
                allTraces.RemoveAt(traceIdx);
                allTraceVisuals.RemoveAt(traceIdx);

                // Also remove and destroy the line renderer
                if (traceIdx < allLineRenderers.Count)
                {
                    if (allLineRenderers[traceIdx] != null)
                    {
                        Destroy(allLineRenderers[traceIdx].gameObject);
                    }
                    allLineRenderers.RemoveAt(traceIdx);
                }

                Debug.Log("Left hand: Erased entire trace");
            }
        }

        // Update all line renderers to reflect erased points
        UpdateAllLineRenderers();
    }

    bool CheckPinchGesture(XRHand hand)
    {
        if (!hand.isTracked)
            return false;

        // Get thumb tip and index tip joints
        XRHandJoint thumbTip = hand.GetJoint(XRHandJointID.ThumbTip);
        XRHandJoint indexTip = hand.GetJoint(XRHandJointID.IndexTip);

        if (thumbTip.TryGetPose(out Pose thumbPose) && indexTip.TryGetPose(out Pose indexPose))
        {
            float distance = Vector3.Distance(thumbPose.position, indexPose.position);
            return distance < pinchThreshold;
        }

        return false;
    }

    bool CheckPinchReleasing(XRHand hand)
    {
        if (!hand.isTracked)
            return false;

        // Get thumb tip and index tip joints
        XRHandJoint thumbTip = hand.GetJoint(XRHandJointID.ThumbTip);
        XRHandJoint indexTip = hand.GetJoint(XRHandJointID.IndexTip);

        if (thumbTip.TryGetPose(out Pose thumbPose) && indexTip.TryGetPose(out Pose indexPose))
        {
            float distance = Vector3.Distance(thumbPose.position, indexPose.position);
            // Fingers are separating but not fully released yet
            return distance >= pinchReleaseThreshold && distance < (pinchReleaseThreshold * 2);
        }

        return false;
    }

    Vector3 GetIndexTipPosition(XRHand hand)
    {
        XRHandJoint indexTip = hand.GetJoint(XRHandJointID.IndexTip);

        if (indexTip.TryGetPose(out Pose indexPose))
        {
            return indexPose.position;
        }

        return Vector3.zero;
    }

    // Note: StartTracing logic is now handled in Update() with delay

    void UpdateTrace(XRHand hand)
    {
        Vector3 currentPoint = GetIndexTipPosition(hand);
        float distance = Vector3.Distance(currentPoint, lastTracedPoint);

        // Only add point if it's far enough from last point but not too far
        if (distance > minTraceDistance && distance < maxTraceDistance)
        {
            AddTracePoint(currentPoint);
            lastTracedPoint = currentPoint;
        }
    }

    // StopTracing logic is now handled in ProcessHand

    void AddTracePoint(Vector3 point)
    {
        if (currentTrace == null)
            return;

        currentTrace.Add(point);

        // Create visual feedback
        if (tracePointPrefab != null && currentTraceVisuals != null)
        {
            GameObject tracePoint = Instantiate(tracePointPrefab, point, Quaternion.identity, transform);
            currentTraceVisuals.Add(tracePoint);
        }

        // Update line renderer to show ALL traces
        UpdateLineRenderer();
    }

    void UpdateLineRenderer()
    {
        // Update the LineRenderer for the current trace being drawn
        if (currentTrace != null && currentTrace.Count > 0)
        {
            int traceIndex = allTraces.IndexOf(currentTrace);
            if (traceIndex >= 0 && traceIndex < allLineRenderers.Count)
            {
                LineRenderer lineRenderer = allLineRenderers[traceIndex];
                lineRenderer.positionCount = currentTrace.Count;
                lineRenderer.SetPositions(currentTrace.ToArray());
            }
        }
    }

    void UpdateAllLineRenderers()
    {
        // Update all line renderers (used after erasing)
        for (int i = 0; i < allTraces.Count && i < allLineRenderers.Count; i++)
        {
            var trace = allTraces[i];
            var lineRenderer = allLineRenderers[i];

            if (lineRenderer != null)
            {
                lineRenderer.positionCount = trace.Count;
                if (trace.Count > 0)
                {
                    lineRenderer.SetPositions(trace.ToArray());
                }
            }
        }
    }

    void ClearTraceVisuals()
    {
        foreach (var traceVisualList in allTraceVisuals)
        {
            foreach (GameObject visual in traceVisualList)
            {
                Destroy(visual);
            }
        }
        allTraceVisuals.Clear();

        // Destroy all line renderers
        foreach (var lineRenderer in allLineRenderers)
        {
            if (lineRenderer != null)
            {
                Destroy(lineRenderer.gameObject);
            }
        }
        allLineRenderers.Clear();
    }

    public void EnableTracing(bool enable)
    {
        tracingEnabled = enable;
        if (!enable)
        {
            isRightTracing = false;
            isLeftTracing = false;
            ClearTraceVisuals();
        }
    }

    public void ClearTrace()
    {
        allTraces.Clear();
        ClearTraceVisuals();
        currentTrace = null;
        currentTraceVisuals = null;
        isRightTracing = false;
        isRightRecording = false;
        isLeftTracing = false;
        isLeftRecording = false;
        Debug.Log("All traces cleared");
    }

    public List<Vector3> GetTracedPoints()
    {
        // Flatten all traces into a single list
        List<Vector3> allPoints = new List<Vector3>();
        foreach (var trace in allTraces)
        {
            allPoints.AddRange(trace);
        }
        return allPoints;
    }

    public int GetTracePointCount()
    {
        int totalPoints = 0;
        foreach (var trace in allTraces)
        {
            totalPoints += trace.Count;
        }
        Debug.Log($"GetTracePointCount called: {totalPoints} points across {allTraces.Count} traces");
        return totalPoints;
    }

    public bool IsCurrentlyTracing()
    {
        return isRightTracing || isLeftTracing;
    }

    // Convert traced points to trefoil parameter space
    public List<Vector2> GetTracedPointsInTrefoilSpace()
    {
        List<Vector2> trefoilSpacePoints = new List<Vector2>();

        if (frozenTrefoil == null)
            return trefoilSpacePoints;

        // Transform world space points to local trefoil space (all traces)
        foreach (var trace in allTraces)
        {
            foreach (Vector3 worldPoint in trace)
            {
                Vector3 localPoint = frozenTrefoil.InverseTransformPoint(worldPoint);
                trefoilSpacePoints.Add(new Vector2(localPoint.x, localPoint.y));
            }
        }

        return trefoilSpacePoints;
    }
}
