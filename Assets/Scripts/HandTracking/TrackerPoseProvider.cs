using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

// Add this component to the same GameObject as a TrackedPoseDriver (Input System).
// The TrackedPoseDriver writes pose into transform; this provider just exposes it
// (plus a friendly status string) for the experiment manager and finger cursor.
[RequireComponent(typeof(TrackedPoseDriver))]
public class TrackerPoseProvider : MonoBehaviour
{
    [Header("Optional status source")]
    [Tooltip("Bind to <HTCViveTrackerOpenXR>/isTracked to drive the IsTracked flag and status panel. If unbound, IsTracked falls back to 'has the transform moved away from the origin'.")]
    public InputActionProperty isTrackedAction;

    [Header("Debug")]
    public bool verboseLogs = true;

    public bool   IsTracked        { get; private set; } = false;
    public string BoundDeviceName  { get; private set; } = "";
    public string LastScanReport   { get; private set; } = "(not initialized)";

    private TrackedPoseDriver tpd;
    private bool loggedFirstPose = false;
    private string lastBoundName = "";

    void Awake()
    {
        tpd = GetComponent<TrackedPoseDriver>();
    }

    void OnEnable()
    {
        var a = isTrackedAction.action;
        if (a != null && !a.enabled) a.Enable();
    }

    void OnDisable()
    {
        var a = isTrackedAction.action;
        if (a != null && a.enabled) a.Disable();
    }

    void Update()
    {
        var trkAct = isTrackedAction.action;

        bool trackedNow;
        string boundName = "";
        string report;

        if (trkAct != null && trkAct.enabled && trkAct.activeControl != null)
        {
            trackedNow = trkAct.ReadValue<float>() > 0.5f;
            boundName  = trkAct.activeControl.device.name;
            report     = trackedNow
                ? $"Bound to '{boundName}' (isTracked action active)"
                : $"Bound to '{boundName}' but isTracked == false";
        }
        else
        {
            // Fallback: infer from whether TrackedPoseDriver is writing a non-origin pose.
            // Identity rotation alone isn't a useful signal (could be the rest pose),
            // so we key on position drift instead.
            trackedNow = transform.position != Vector3.zero;
            boundName  = trackedNow ? "TrackedPoseDriver (transform)" : "";
            if (trackedNow)
            {
                report = $"Pose source: TrackedPoseDriver — pos=({transform.position.x:F2},{transform.position.y:F2},{transform.position.z:F2})";
            }
            else if (trkAct == null)
            {
                report = "isTracked action not assigned AND transform at origin — check TrackedPoseDriver bindings";
            }
            else
            {
                report = "isTracked action has no active control AND transform at origin — check TrackedPoseDriver + OpenXR tracker profile";
            }
        }

        IsTracked       = trackedNow;
        BoundDeviceName = boundName;
        LastScanReport  = report;

        if (verboseLogs && boundName != lastBoundName)
        {
            Debug.Log($"[TrackerPoseProvider] {report}");
            lastBoundName = boundName;
        }
        if (verboseLogs && IsTracked && !loggedFirstPose)
        {
            Debug.Log($"[TrackerPoseProvider] First valid pose: pos={transform.position} rot={transform.rotation.eulerAngles}");
            loggedFirstPose = true;
        }
    }

    public bool TryGetPose(out Vector3 position, out Quaternion rotation)
    {
        position = transform.position;
        rotation = transform.rotation;
        return IsTracked;
    }

    public bool TryGetPosition(out Vector3 position)
    {
        position = transform.position;
        return IsTracked;
    }
}
