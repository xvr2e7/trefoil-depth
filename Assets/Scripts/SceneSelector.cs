using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using TMPro;

public class SceneSelector : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI instructionText;

    private InputDevice leftHandDevice;
    private bool lastXButtonState = false;
    private bool lastYButtonState = false;
    private bool sceneSelected = false;

    void Start()
    {
        InitializeInputDevices();
        ShowInstruction("Press 'X' for Depth Only\n" +
                       "Press 'Y' for Hand Tracking");
    }

    void InitializeInputDevices()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, devices);
        if (devices.Count > 0)
        {
            leftHandDevice = devices[0];
        }
    }

    void Update()
    {
        if (!leftHandDevice.isValid)
        {
            InitializeInputDevices();
        }

        if (!sceneSelected)
        {
            // Check for X button press
            if (GetXButtonDown())
            {
                sceneSelected = true;
                LoadScene(1); // DepthOnly scene
            }
            // Check for Y button press
            else if (GetYButtonDown())
            {
                sceneSelected = true;
                LoadScene(2); // DepthHandTracking scene
            }
        }
    }

    bool GetXButtonDown()
    {
        if (leftHandDevice.isValid)
        {
            if (leftHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool currentState))
            {
                bool pressed = currentState && !lastXButtonState;
                lastXButtonState = currentState;
                return pressed;
            }
        }
        return false;
    }

    bool GetYButtonDown()
    {
        if (leftHandDevice.isValid)
        {
            if (leftHandDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out bool currentState))
            {
                bool pressed = currentState && !lastYButtonState;
                lastYButtonState = currentState;
                return pressed;
            }
        }
        return false;
    }

    void LoadScene(int sceneIndex)
    {
        ShowInstruction("Loading...");
        SceneManager.LoadScene(sceneIndex);
    }

    void ShowInstruction(string text)
    {
        if (instructionText != null)
        {
            instructionText.text = text;
        }
    }
}
