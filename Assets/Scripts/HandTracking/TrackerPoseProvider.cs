using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR;

public class TrackerPoseProvider : MonoBehaviour
{
    [Header("Device Selection")]
    [Tooltip("Match the InputDevice name or serial. Leave empty to auto-pick the first TrackedDevice that isn't an HMD or controller.")]
    public string deviceNameOrSerial = "";

    [Tooltip("How often to re-scan for the device when not yet found (seconds)")]
    public float rescanInterval = 1f;

    [Header("Debug")]
    [Tooltip("On every failed scan, log every XR device Unity can see (name, serial, characteristics).")]
    public bool verboseScanLogs = true;

    // Exposed for GUI panels / debug overlays.
    public bool   IsTracked         => device.isValid;
    public string BoundDeviceName   { get; private set; } = "";
    public string BoundDeviceSerial { get; private set; } = "";
    public int    LastDeviceCount   { get; private set; } = 0;
    public string LastScanReport    { get; private set; } = "(no scan yet)";

    private InputDevice device;
    private float nextScanTime = 0f;

    void Update()
    {
        if (!device.isValid && Time.time >= nextScanTime)
        {
            FindDevice();
            nextScanTime = Time.time + rescanInterval;
        }
    }

    void FindDevice()
    {
        var all = new List<InputDevice>();
        InputDevices.GetDevices(all);
        LastDeviceCount = all.Count;

        InputDevice match = default;
        foreach (var d in all)
        {
            if (!d.isValid) continue;

            bool isHmd        = (d.characteristics & InputDeviceCharacteristics.HeadMounted)   != 0;
            bool isController = (d.characteristics & InputDeviceCharacteristics.Controller)    != 0;
            bool isTracked    = (d.characteristics & InputDeviceCharacteristics.TrackedDevice) != 0;

            if (!isTracked || isHmd || isController) continue;

            if (!string.IsNullOrEmpty(deviceNameOrSerial))
            {
                if (d.name != null && d.name.Contains(deviceNameOrSerial)) { match = d; break; }
                if (d.serialNumber != null && d.serialNumber.Contains(deviceNameOrSerial)) { match = d; break; }
            }
            else
            {
                match = d;
                break;
            }
        }

        device = match;
        if (device.isValid)
        {
            BoundDeviceName   = device.name;
            BoundDeviceSerial = device.serialNumber;
            LastScanReport    = $"Bound to '{device.name}' (serial: {device.serialNumber})";
            Debug.Log($"[TrackerPoseProvider] {LastScanReport}");
        }
        else
        {
            BoundDeviceName   = "";
            BoundDeviceSerial = "";

            var sb = new StringBuilder();
            sb.Append($"No tracker bound. {all.Count} XR device(s) visible");
            if (all.Count == 0)
            {
                sb.Append(". (Unity sees zero XR devices — is the OpenXR runtime active and SteamVR connected?)");
            }
            else
            {
                sb.AppendLine(":");
                foreach (var d in all)
                {
                    sb.AppendLine($"  - name='{d.name}' serial='{d.serialNumber}' chars=[{d.characteristics}] valid={d.isValid}");
                }
            }
            LastScanReport = sb.ToString();

            if (verboseScanLogs) Debug.Log($"[TrackerPoseProvider] {LastScanReport}");
        }
    }

    public bool TryGetPose(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (!device.isValid) return false;

        bool gotPos = device.TryGetFeatureValue(CommonUsages.devicePosition, out position);
        bool gotRot = device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
        return gotPos && gotRot;
    }

    public bool TryGetPosition(out Vector3 position)
    {
        position = Vector3.zero;
        if (!device.isValid) return false;
        return device.TryGetFeatureValue(CommonUsages.devicePosition, out position);
    }
}
