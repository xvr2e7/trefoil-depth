using UnityEngine;
using UnityEngine.InputSystem;

public class TrackerPoseProvider : MonoBehaviour
{
    [Header("Input Actions (new Input System)")]
    [Tooltip("Bind to <XRController>{HeldInHand}/devicePosition (or <ViveTracker>{HeldInHand}/devicePosition if available).")]
    public InputActionProperty positionAction;

    [Tooltip("Bind to <XRController>{HeldInHand}/deviceRotation.")]
    public InputActionProperty rotationAction;

    [Tooltip("Optional: bind to <XRController>{HeldInHand}/isTracked. If unbound, validity is inferred from whether the position action has resolved a control.")]
    public InputActionProperty isTrackedAction;

    [Header("Debug")]
    [Tooltip("Log binding status changes and first valid pose to Console.")]
    public bool verboseLogs = true;

    // Exposed for GUI panels / debug overlays.
    public bool   IsTracked        { get; private set; } = false;
    public string BoundDeviceName  { get; private set; } = "";
    public string LastScanReport   { get; private set; } = "(not initialized)";

    private Vector3    lastPos = Vector3.zero;
    private Quaternion lastRot = Quaternion.identity;
    private bool       loggedFirstPose = false;
    private string     lastBoundName   = "";

    void OnEnable()
    {
        TryEnable(positionAction);
        TryEnable(rotationAction);
        TryEnable(isTrackedAction);
    }

    void OnDisable()
    {
        TryDisable(positionAction);
        TryDisable(rotationAction);
        TryDisable(isTrackedAction);
    }

    static void TryEnable(InputActionProperty prop)
    {
        var a = prop.action;
        if (a != null && !a.enabled) a.Enable();
    }

    static void TryDisable(InputActionProperty prop)
    {
        var a = prop.action;
        if (a != null && a.enabled) a.Disable();
    }

    void Update()
    {
        var posAct = positionAction.action;
        var rotAct = rotationAction.action;
        var trkAct = isTrackedAction.action;

        // Pose
        if (posAct != null && posAct.enabled)
            lastPos = posAct.ReadValue<Vector3>();
        if (rotAct != null && rotAct.enabled)
            lastRot = rotAct.ReadValue<Quaternion>();

        // Tracked state
        bool isTrackedNow;
        if (trkAct != null && trkAct.enabled)
            isTrackedNow = trkAct.ReadValue<float>() > 0.5f;
        else
            isTrackedNow = posAct != null && posAct.activeControl != null;

        IsTracked = isTrackedNow;

        // Diagnostics
        string boundName = (posAct != null && posAct.activeControl != null)
            ? posAct.activeControl.device.name
            : "";

        BoundDeviceName = boundName;
        LastScanReport  = !string.IsNullOrEmpty(boundName)
            ? $"Bound to '{boundName}' via action '{posAct.name}'"
            : (posAct == null ? "positionAction not assigned"
                              : $"Action '{posAct.name}' has no active control (binding path or role tag mismatch?)");

        if (verboseLogs && boundName != lastBoundName)
        {
            Debug.Log($"[TrackerPoseProvider] {LastScanReport}");
            lastBoundName = boundName;
        }
        if (verboseLogs && IsTracked && !loggedFirstPose)
        {
            Debug.Log($"[TrackerPoseProvider] First valid pose: pos={lastPos} rot={lastRot.eulerAngles}");
            loggedFirstPose = true;
        }
    }

    public bool TryGetPose(out Vector3 position, out Quaternion rotation)
    {
        position = lastPos;
        rotation = lastRot;
        return IsTracked;
    }

    public bool TryGetPosition(out Vector3 position)
    {
        position = lastPos;
        return IsTracked;
    }
}
