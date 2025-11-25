// Archived script

using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(MeshRenderer))]
public class CurvatureSphere : MonoBehaviour
{
    [Header("Sphere Parameters")]
    public float radius = 0.3f;
    public float radiusSpeed = 0.1f;
    public float minRadius = 0.1f;
    public float maxRadius = 1f;

    private MeshRenderer meshRenderer;
    private InputDevice rightHandDevice;
    private bool adjustmentEnabled = false;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        InitializeInputDevice();
        UpdateScale();
        meshRenderer.enabled = false;
    }

    void InitializeInputDevice()
    {
        var devices = new System.Collections.Generic.List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
        if (devices.Count > 0)
        {
            rightHandDevice = devices[0];
        }
    }

    void Update()
    {
        if (!rightHandDevice.isValid)
        {
            InitializeInputDevice();
        }

        if (!adjustmentEnabled)
            return;

        if (rightHandDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 joystick))
        {
            float oldRadius = radius;
            radius += joystick.y * radiusSpeed * Time.deltaTime;
            radius = Mathf.Clamp(radius, minRadius, maxRadius);

            if (Mathf.Abs(radius - oldRadius) > 0.001f)
            {
                UpdateScale();
            }
        }
    }

    void UpdateScale()
    {
        transform.localScale = Vector3.one * radius * 2f;
    }

    public void ResetRadius(float initialRadius)
    {
        radius = Mathf.Clamp(initialRadius, minRadius, maxRadius);
        UpdateScale();
    }

    public void SetAdjustmentEnabled(bool enabled)
    {
        adjustmentEnabled = enabled;
    }

    public float GetRadius()
    {
        return radius;
    }

    public void SetVisibility(bool visible)
    {
        meshRenderer.enabled = visible;
    }
}