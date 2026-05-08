using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class TrackerPoseProvider : MonoBehaviour
{
    [Header("Device Selection")]
    [Tooltip("Match the InputDevice name or serial. Leave empty to auto-pick the first TrackedDevice that isn't an HMD or controller.")]
    public string deviceNameOrSerial = "";

    [Tooltip("How often to re-scan for the device when not yet found (seconds)")]
    public float rescanInterval = 1f;

    private InputDevice device;
    private float nextScanTime = 0f;

    public bool IsTracked => device.isValid;

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
            Debug.Log($"[TrackerPoseProvider] Bound to '{device.name}' (serial: {device.serialNumber})");
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
