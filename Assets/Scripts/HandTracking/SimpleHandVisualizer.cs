using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// Simple hand visualizer that draws debug spheres at each joint position
/// For Quest 2 with OpenXR hand tracking
/// </summary>
public class SimpleHandVisualizer : MonoBehaviour
{
    [Header("Visualization Settings")]
    public bool showRightHand = true;
    public bool showLeftHand = true;
    public Material jointMaterial;
    public float jointRadius = 0.005f;

    [Header("Colors")]
    public Color rightHandColor = new Color(0.2f, 0.6f, 1f, 0.8f); // Light blue
    public Color leftHandColor = new Color(1f, 0.6f, 0.2f, 0.8f);  // Orange

    private XRHandSubsystem handSubsystem;
    private Dictionary<XRHandJointID, GameObject> rightHandJoints = new Dictionary<XRHandJointID, GameObject>();
    private Dictionary<XRHandJointID, GameObject> leftHandJoints = new Dictionary<XRHandJointID, GameObject>();

    void Start()
    {
        InitializeHandTracking();
        CreateJointVisuals();
    }

    void InitializeHandTracking()
    {
        var handSubsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(handSubsystems);

        if (handSubsystems.Count > 0)
        {
            handSubsystem = handSubsystems[0];
            Debug.Log("Hand visualizer initialized");
        }
        else
        {
            Debug.LogWarning("No XR Hand subsystem found for visualization!");
        }
    }

    void CreateJointVisuals()
    {
        // Create spheres for all hand joints
        foreach (XRHandJointID jointID in System.Enum.GetValues(typeof(XRHandJointID)))
        {
            if (jointID == XRHandJointID.Invalid || jointID == XRHandJointID.EndMarker)
                continue;

            if (showRightHand)
            {
                GameObject rightJoint = CreateJointSphere($"RightHand_{jointID}", rightHandColor);
                rightHandJoints[jointID] = rightJoint;
            }

            if (showLeftHand)
            {
                GameObject leftJoint = CreateJointSphere($"LeftHand_{jointID}", leftHandColor);
                leftHandJoints[jointID] = leftJoint;
            }
        }
    }

    GameObject CreateJointSphere(string name, Color color)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.parent = transform;
        sphere.transform.localScale = Vector3.one * jointRadius;

        // Remove collider
        Destroy(sphere.GetComponent<Collider>());

        // Set material and color
        Renderer renderer = sphere.GetComponent<Renderer>();
        if (jointMaterial != null)
        {
            renderer.material = jointMaterial;
        }
        else
        {
            renderer.material = new Material(Shader.Find("Standard"));
        }
        renderer.material.color = color;

        sphere.SetActive(false);
        return sphere;
    }

    void Update()
    {
        if (handSubsystem == null)
            return;

        // Update right hand
        if (showRightHand)
        {
            UpdateHandJoints(handSubsystem.rightHand, rightHandJoints);
        }

        // Update left hand
        if (showLeftHand)
        {
            UpdateHandJoints(handSubsystem.leftHand, leftHandJoints);
        }
    }

    void UpdateHandJoints(XRHand hand, Dictionary<XRHandJointID, GameObject> jointObjects)
    {
        if (!hand.isTracked)
        {
            // Hide all joints if hand not tracked
            foreach (var joint in jointObjects.Values)
            {
                joint.SetActive(false);
            }
            return;
        }

        // Update all joint positions
        foreach (var kvp in jointObjects)
        {
            XRHandJointID jointID = kvp.Key;
            GameObject jointObject = kvp.Value;

            XRHandJoint joint = hand.GetJoint(jointID);

            if (joint.TryGetPose(out Pose pose))
            {
                jointObject.SetActive(true);
                jointObject.transform.position = pose.position;
                jointObject.transform.rotation = pose.rotation;
            }
            else
            {
                jointObject.SetActive(false);
            }
        }
    }

    void OnDestroy()
    {
        // Clean up joint objects
        foreach (var joint in rightHandJoints.Values)
        {
            if (joint != null)
                Destroy(joint);
        }

        foreach (var joint in leftHandJoints.Values)
        {
            if (joint != null)
                Destroy(joint);
        }

        rightHandJoints.Clear();
        leftHandJoints.Clear();
    }
}
