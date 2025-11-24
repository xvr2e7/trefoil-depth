// Archived script

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using TMPro;

public class ConfidenceSlider : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TextMeshProUGUI leftLabel;
    [SerializeField] private TextMeshProUGUI rightLabel;

    [Header("Control Settings")]
    public float confidenceSpeed = 1f;

    private float currentValue = 0.5f;
    private bool isActive = false;
    private InputDevice rightHandDevice;

    void Start()
    {
        if (slider == null)
            slider = GetComponentInChildren<Slider>();

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.5f;

        if (leftLabel != null)
            leftLabel.text = "Not Confident";

        if (rightLabel != null)
            rightLabel.text = "Very Confident";

        Hide();
    }

    void Update()
    {
        if (!isActive)
            return;

        if (!rightHandDevice.isValid)
        {
            var devices = new System.Collections.Generic.List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
            if (devices.Count > 0)
                rightHandDevice = devices[0];
        }

        if (rightHandDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 joystick))
        {
            currentValue += joystick.x * confidenceSpeed * Time.deltaTime;
            currentValue = Mathf.Clamp01(currentValue);
            slider.value = currentValue;
        }
    }

    public void Show()
    {
        currentValue = 0.5f;
        slider.value = 0.5f;
        isActive = true;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        isActive = false;
        gameObject.SetActive(false);
    }

    public float GetConfidence()
    {
        return currentValue;
    }
}