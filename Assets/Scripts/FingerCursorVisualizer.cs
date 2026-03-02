using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
/// <summary>
/// Renders a small sphere at the dominant hand's index-finger tip.
///
/// States:
///   Active (light green)  — follows finger live
///   Confirmed (dark green) — frozen at recorded position for 2s feedback
///
/// Enable with gameObject.SetActive(true) before pointing begins.
/// Call SetConfirmed(pos) when dwell completes.
/// Call ResetCursor() + gameObject.SetActive(false) when done with a point.
/// </summary>
public class FingerCursorVisualizer : MonoBehaviour
{
    [Header("Appearance")]
    public float cursorScale = 0.015f;
    public Color lightGreen = new Color(0.5f, 1f, 0.35f);

    private static readonly Color DarkGreen = new Color(0.05f, 0.55f, 0.1f);

    private MeshRenderer meshRenderer;
    private XRHandSubsystem handSubsystem;
    private bool frozen = false;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        Material mat = new Material(Shader.Find("Custom/BinocularUnlit"));
        mat.color = lightGreen;
        meshRenderer.material = mat;

        transform.localScale = Vector3.one * cursorScale;
        gameObject.SetActive(false);
    }

    void OnEnable()
    {
        frozen = false;
        meshRenderer.material.color = lightGreen;

        if (handSubsystem == null)
        {
            var list = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(list);
            if (list.Count > 0) handSubsystem = list[0];
        }
    }

    void Update()
    {
        if (frozen) return;   // stay at confirmed position, don't chase finger

        if (handSubsystem == null) return;

        XRHand hand = HandednessManager.Instance.IsRightHanded()
            ? handSubsystem.rightHand
            : handSubsystem.leftHand;

        if (!hand.isTracked) return;

        XRHandJoint index = hand.GetJoint(XRHandJointID.IndexTip);
        if (index.TryGetPose(out Pose pose))
            transform.position = pose.position;
    }

    // ------------------------------------------------------------------
    // Freeze cursor at the confirmed position and turn dark green.
    // Called when 3-second dwell (or manual A press) completes.
    // ------------------------------------------------------------------
    public void SetConfirmed(Vector3 pos)
    {
        frozen = true;
        transform.position = pos;
        meshRenderer.material.color = DarkGreen;
    }

    // ------------------------------------------------------------------
    // Restore cursor to live light-green tracking state.
    // Call after confirmation feedback period, before SetActive(false).
    // ------------------------------------------------------------------
    public void ResetCursor()
    {
        frozen = false;
        meshRenderer.material.color = lightGreen;
    }

    // ------------------------------------------------------------------
    // Returns current index-finger tip world position if tracked.
    // ------------------------------------------------------------------
    public bool TryGetIndexTipPosition(out Vector3 pos)
    {
        pos = Vector3.zero;
        if (handSubsystem == null) return false;

        XRHand hand = HandednessManager.Instance.IsRightHanded()
            ? handSubsystem.rightHand
            : handSubsystem.leftHand;

        if (!hand.isTracked) return false;

        XRHandJoint index = hand.GetJoint(XRHandJointID.IndexTip);
        if (index.TryGetPose(out Pose pose))
        {
            pos = pose.position;
            return true;
        }
        return false;
    }
}
