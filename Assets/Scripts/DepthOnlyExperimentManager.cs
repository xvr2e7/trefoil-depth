using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using TMPro;

public class DepthOnlyExperimentManager : MonoBehaviour
{
    [Header("Depth Task")]
    public TrefoilGenerator stimulusTrefoil;
    public FourierTrefoil3D adjustableModel;

    [Header("Calibration Task")]
    public WireframeSphere referenceSphere;
    public FourierTrefoil3D calibrationModel;

    [Header("UI")]
    public TextMeshProUGUI instructionText;
    public TextMeshProUGUI ExplainText;

    [Header("Experiment Settings")]
    public bool autoStart = false;

    private List<CalibrationTrial> calibrationTrials;
    private List<DepthTrial> practiceTrials;
    private List<DepthTrial> depthTrials;
    private List<CalibrationRecord> calibrationRecords = new List<CalibrationRecord>();
    private List<DepthOnlyRecord> allRecords = new List<DepthOnlyRecord>();

    private int currentTrialIndex = 0;
    private bool isPractice = true;
    private float trialStartTime;
    private bool experimentStarted = false;
    private bool experimentRunning = false;

    private InputDevice rightHandDevice;
    private bool lastButtonState = false;
    private bool lastSecondaryButtonState = false;

    private enum ExperimentState
    {
        Welcome,
        CalibrationStage1,
        CalibrationStage2,
        CalibrationStage3,
        SphereCalibrationIntro,
        SphereCalibration,
        PracticeIntro,
        Practice,
        MainIntro,
        Main,
        End
    }

    private ExperimentState currentState = ExperimentState.Welcome;

    void Start()
    {
        StartCoroutine(InitializeExperiment());
    }

    IEnumerator InitializeExperiment()
    {
        yield return null;

        InitializeInputDevices();

        calibrationTrials = DepthOnlyTrialGenerator.GenerateCalibrationTrials();
        practiceTrials = DepthOnlyTrialGenerator.GeneratePracticeTrials();
        depthTrials = DepthOnlyTrialGenerator.GenerateDepthTrials();

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetVisibility(false);
        }

        if (adjustableModel != null)
        {
            adjustableModel.SetVisibility(false);
        }

        if (referenceSphere != null)
        {
            referenceSphere.SetVisibility(false);
        }

        if (calibrationModel != null)
        {
            calibrationModel.SetVisibility(false);
        }

        if (ExplainText != null)
        {
            ExplainText.text = "";
            ExplainText.gameObject.SetActive(false);
        }

        ShowInstruction("Welcome to the Experiment!\n\nPress 'A' to begin.");

        if (autoStart)
        {
            experimentStarted = true;
            experimentRunning = true;
            StartCoroutine(RunExperiment());
        }
    }

    void InitializeInputDevices()
    {
        var devices = new List<InputDevice>();
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
            InitializeInputDevices();
        }

        if (!experimentStarted && !experimentRunning)
        {
            if (GetButtonDown())
            {
                experimentStarted = true;
                experimentRunning = true;
                StartCoroutine(RunExperiment());
            }
        }
    }

    bool GetButtonDown()
    {
        if (rightHandDevice.isValid)
        {
            if (rightHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool currentState))
            {
                bool pressed = currentState && !lastButtonState;
                lastButtonState = currentState;
                return pressed;
            }
        }
        return false;
    }

    bool GetSecondaryButtonDown()
    {
        if (rightHandDevice.isValid)
        {
            if (rightHandDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out bool currentState))
            {
                bool pressed = currentState && !lastSecondaryButtonState;
                lastSecondaryButtonState = currentState;
                return pressed;
            }
        }
        return false;
    }

    IEnumerator RunExperiment()
    {
        yield return StartCoroutine(CalibrationPhase());
        yield return StartCoroutine(SphereCalibrationPhase());
        yield return StartCoroutine(PracticePhase());
        yield return StartCoroutine(MainExperimentPhase());
        yield return StartCoroutine(EndPhase());
    }

    IEnumerator CalibrationPhase()
    {
        currentState = ExperimentState.CalibrationStage1;

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetParameters(1.0f, 1.5f, 60f, 1);
            stimulusTrefoil.SetVisibility(true);
        }

        ShowEyeSpecificInstruction("Stare at the rotating curve until you perceive something interesting.\n\n" +
                                 "Press 'A' when ready to continue.", 0);

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.3f);

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetVisibility(false);
        }

        currentState = ExperimentState.CalibrationStage2;
        ShowEyeSpecificInstruction("This is one possible 3D interpretation of the 2D stimulus you saw.\n" +
                                   "It is the general shape that you should be able to perceive.\n\n" +
                                   "Use the joystick left/right to rotate and explore.\n\n" +
                                   "Press 'A' when you are ready to continue.", 1);

        if (adjustableModel != null)
        {
            // Random depth in range [0.5, 1.0] for exploration
            float randomDepth = Random.Range(0.5f, 1.0f);
            adjustableModel.ResetParameters(1.0f, 1.5f, 0f, randomDepth);
            adjustableModel.SetManualRotationMode(true);  // Enable manual rotation with joystick
            adjustableModel.SetVisibility(true);
        }

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.3f);

        if (adjustableModel != null)
        {
            adjustableModel.SetVisibility(false);
            adjustableModel.SetManualRotationMode(false);
        }

        currentState = ExperimentState.CalibrationStage3;
        ShowEyeSpecificInstruction("Now look at the rotating curve again.\n\n" +
                                 "Can you perceive a 3D shape?\n\n" +
                                 "Press 'A' when ready to begin the study.", 0);

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetVisibility(true);
        }

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.3f);

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetVisibility(false);
        }

        HideEyeSpecificInstructions();
    }

    IEnumerator SphereCalibrationPhase()
    {
        currentState = ExperimentState.SphereCalibrationIntro;
        ShowInstruction("CALIBRATION\n\n" +
                        "Depth Matching Task\n\n" +
                        "You will see a wireframe sphere with a fixed depth.\n\n" +
                        "Press 'B' to toggle between the sphere and a 3D model.\n\n" +
                        "When the 3D model is visible, adjust it by moving the joystick UP/DOWN to match the depth of the sphere.\n\n" +
                        "Moving UP increases depth, DOWN decreases depth.\n\n" +
                        "Press 'A' to preview your response rotating.\n\n" +
                        "Press 'A' again to confirm or 'B' to reset and adjust again.\n\n" +
                        "Press 'A' to start calibration.");

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.5f);

        currentState = ExperimentState.SphereCalibration;
        currentTrialIndex = 0;

        // Run all calibration trials
        for (int i = 0; i < calibrationTrials.Count; i++)
        {
            yield return StartCoroutine(RunCalibrationTrial(calibrationTrials[i]));
            currentTrialIndex++;
        }

        ShowInstruction("Calibration complete.\n\nPress 'A' to continue.");
        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.5f);
    }

    IEnumerator PracticePhase()
    {
        currentState = ExperimentState.PracticeIntro;
        ShowInstruction("PRACTICE\n\n" +
                        "Depth Adjustment Task\n\n" +
                        "You will see a rotating 2D white curve.\n\n" +
                        "Press 'B' to toggle between the 2D view and a 3D model.\n\n" +
                        "When the 3D model is visible, adjust it by moving the joystick UP/DOWN to match the depth you perceive.\n\n" +
                        "Moving UP increases depth, DOWN decreases depth.\n\n" +
                        "Press 'A' to preview your response rotating.\n\n" +
                        "Press 'A' again to confirm or 'B' to reset and adjust again.\n\n" +
                        "Press 'A' to start practice.");

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.5f);

        currentState = ExperimentState.Practice;
        isPractice = true;

        currentTrialIndex = 0;
        yield return StartCoroutine(RunDepthTrial(practiceTrials[0], true));
    }

    IEnumerator MainExperimentPhase()
    {
        currentState = ExperimentState.MainIntro;
        ShowInstruction("Practice complete.\n\n" +
                       "Press 'A' to begin the main experiment.");

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.5f);

        currentState = ExperimentState.Main;
        isPractice = false;
        currentTrialIndex = 0;

        // Run all depth adjustment trials
        for (int i = 0; i < depthTrials.Count; i++)
        {
            yield return StartCoroutine(RunDepthTrial(depthTrials[i], false));
            currentTrialIndex++;
        }
    }

    IEnumerator EndPhase()
    {
        currentState = ExperimentState.End;
        SaveData();
        ShowInstruction("Experiment Complete!\n\n" +
                       "Thank you for your participation.\n\n");
        yield return new WaitForSeconds(3f);
    }

    IEnumerator RunDepthTrial(DepthTrial trial, bool practice)
    {
        ShowInstruction("");

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetParameters(trial.R1, trial.R2, trial.rotationSpeed, trial.direction);
            stimulusTrefoil.ResumeRotation();
        }

        if (adjustableModel != null)
        {
            adjustableModel.ResetParameters(trial.R1, trial.R2, 0f, trial.startingAmplitude);
        }

        yield return new WaitForSeconds(0.5f);

        // Start with only 2D trefoil visible
        bool show3D = false;
        bool hasViewedModel = false; // Track whether 'B' has been pressed

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetVisibility(true);
        }

        if (adjustableModel != null)
        {
            adjustableModel.SetVisibility(false);
            adjustableModel.SetAdjustmentEnabled(false);
        }

        // Show control reminder for 2D view
        if (ExplainText != null)
        {
            ExplainText.text = "Press B to toggle view";
            ExplainText.gameObject.SetActive(true);
        }

        trialStartTime = Time.time;

        yield return new WaitForSeconds(0.5f);

        // Wait for either 'A' (submit) or 'B' (toggle) button
        bool submitted = false;
        while (!submitted)
        {
            if (GetSecondaryButtonDown())
            {
                // Toggle between 2D and 3D
                show3D = !show3D;
                hasViewedModel = true; // Mark that participant has pressed 'B'

                if (show3D)
                {
                    // Show 3D model, hide 2D trefoil
                    if (stimulusTrefoil != null)
                    {
                        stimulusTrefoil.PauseRotation();
                        stimulusTrefoil.SetVisibility(false);
                    }

                    if (adjustableModel != null)
                    {
                        adjustableModel.SetVisibility(true);
                        adjustableModel.SetAdjustmentEnabled(true);
                    }

                    // Update control reminder for 3D adjustment view
                    if (ExplainText != null)
                    {
                        ExplainText.text = "Press B to toggle view. Move joystick up/down. Press A to submit";
                    }
                }
                else
                {
                    // Show 2D trefoil, hide 3D model (rotation resumes from where it was paused)
                    if (stimulusTrefoil != null)
                    {
                        stimulusTrefoil.ResumeRotation();
                        stimulusTrefoil.SetVisibility(true);
                    }

                    if (adjustableModel != null)
                    {
                        adjustableModel.SetVisibility(false);
                        adjustableModel.SetAdjustmentEnabled(false);
                    }

                    // Update control reminder for 2D view
                    if (ExplainText != null)
                    {
                        ExplainText.text = "Press B to toggle view";
                    }
                }
            }

            if (GetButtonDown())
            {
                // Only allow submission if participant has viewed the model
                if (hasViewedModel)
                {
                    submitted = true;
                }
                else
                {
                    // Flash warning text briefly
                    StartCoroutine(FlashWarningText("Please press 'B' to view and adjust the model first!"));
                }
            }

            yield return null;
        }

        // Confirmation stage: rotate the adjustable model like the stimulus
        bool confirmed = false;
        bool resetRequested = false;

        while (!confirmed)
        {
            // Hide 2D stimulus
            if (stimulusTrefoil != null)
            {
                stimulusTrefoil.PauseRotation();
                stimulusTrefoil.SetVisibility(false);
            }

            // Show 3D model rotating (match stimulus rotation speed)
            if (adjustableModel != null)
            {
                adjustableModel.SetVisibility(true);
                adjustableModel.SetAdjustmentEnabled(false);
                adjustableModel.SetRotationMode(true, trial.rotationSpeed);  // Match stimulus speed
            }

            // Update instruction text
            if (ExplainText != null)
            {
                ExplainText.text = "Press A to confirm, B to reset";
            }

            yield return new WaitForSeconds(0.5f);

            // Wait for confirmation (A) or reset (B)
            bool waitingForInput = true;
            while (waitingForInput)
            {
                if (GetButtonDown())
                {
                    // Confirm
                    confirmed = true;
                    waitingForInput = false;
                }
                else if (GetSecondaryButtonDown())
                {
                    // Reset - go back to adjustment
                    resetRequested = true;
                    waitingForInput = false;
                }
                yield return null;
            }

            if (resetRequested)
            {
                // Stop rotating mode and reset amplitude to starting value
                if (adjustableModel != null)
                {
                    adjustableModel.SetRotationMode(false);
                    // Reset amplitude to trial's starting value
                    adjustableModel.ResetParameters(trial.R1, trial.R2, 0f, trial.startingAmplitude);
                    adjustableModel.SetVisibility(true);
                    adjustableModel.SetAdjustmentEnabled(true);
                }

                // Keep 2D stimulus hidden initially
                if (stimulusTrefoil != null)
                {
                    stimulusTrefoil.PauseRotation();
                    stimulusTrefoil.SetVisibility(false);
                }

                // Update control reminder for 3D adjustment view
                if (ExplainText != null)
                {
                    ExplainText.text = "Press B to toggle view. Move joystick up/down. Press A to submit";
                }

                yield return new WaitForSeconds(0.5f);

                // Re-enter adjustment phase - start in 3D view but allow toggling
                show3D = true;
                submitted = false;
                resetRequested = false;

                while (!submitted)
                {
                    if (GetSecondaryButtonDown())
                    {
                        // Toggle between 2D and 3D
                        show3D = !show3D;

                        if (show3D)
                        {
                            // Show 3D model, hide 2D trefoil
                            if (stimulusTrefoil != null)
                            {
                                stimulusTrefoil.PauseRotation();
                                stimulusTrefoil.SetVisibility(false);
                            }

                            if (adjustableModel != null)
                            {
                                adjustableModel.SetVisibility(true);
                                adjustableModel.SetAdjustmentEnabled(true);
                            }

                            // Update control reminder for 3D adjustment view
                            if (ExplainText != null)
                            {
                                ExplainText.text = "Press B to toggle view. Move joystick up/down. Press A to submit";
                            }
                        }
                        else
                        {
                            // Show 2D trefoil, hide 3D model
                            if (stimulusTrefoil != null)
                            {
                                stimulusTrefoil.ResumeRotation();
                                stimulusTrefoil.SetVisibility(true);
                            }

                            if (adjustableModel != null)
                            {
                                adjustableModel.SetVisibility(false);
                                adjustableModel.SetAdjustmentEnabled(false);
                            }

                            // Update control reminder for 2D view
                            if (ExplainText != null)
                            {
                                ExplainText.text = "Press B to toggle view";
                            }
                        }
                    }

                    if (GetButtonDown())
                    {
                        submitted = true;
                    }

                    yield return null;
                }
            }
        }

        float reactionTime = Time.time - trialStartTime;
        float amplitude = adjustableModel != null ? adjustableModel.GetAdjustmentValue() : 0f;

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.PauseRotation();
            stimulusTrefoil.SetVisibility(false);
        }

        if (adjustableModel != null)
        {
            adjustableModel.SetVisibility(false);
            adjustableModel.SetRotationMode(false);
            adjustableModel.SetAdjustmentEnabled(false);
        }

        // Hide the control reminder
        if (ExplainText != null)
        {
            ExplainText.gameObject.SetActive(false);
        }

        if (!practice)
        {
            allRecords.Add(new DepthOnlyRecord(currentTrialIndex, trial, amplitude, reactionTime));
        }

        yield return new WaitForSeconds(1f);
    }

    IEnumerator RunCalibrationTrial(CalibrationTrial trial)
    {
        ShowInstruction("");

        // Set up the reference sphere with the ground truth depth
        if (referenceSphere != null)
        {
            referenceSphere.SetDepth(trial.sphereDepth);
        }

        // Set up the adjustable calibration model
        // Use R2=1.5 for calibration trials (consistent reference)
        if (calibrationModel != null)
        {
            calibrationModel.ResetParameters(1.0f, 1.5f, 0f, trial.startingAmplitude);
        }

        yield return new WaitForSeconds(0.5f);

        // Start with only sphere visible (rotating per spec line 13-14, 157-164)
        bool show3D = false;
        bool hasViewedModel = false; // Track whether 'B' has been pressed

        if (referenceSphere != null)
        {
            referenceSphere.SetVisibility(true);
            referenceSphere.StartRotation();  // Start continuous rotation at 60 deg/s
        }

        if (calibrationModel != null)
        {
            calibrationModel.SetVisibility(false);
            calibrationModel.SetAdjustmentEnabled(false);
        }

        // Show control reminder for sphere view
        if (ExplainText != null)
        {
            ExplainText.text = "Press B to toggle view";
            ExplainText.gameObject.SetActive(true);
        }

        trialStartTime = Time.time;

        yield return new WaitForSeconds(0.5f);

        // Wait for either 'A' (submit) or 'B' (toggle) button
        bool submitted = false;
        while (!submitted)
        {
            if (GetSecondaryButtonDown())
            {
                // Toggle between sphere and 3D model
                show3D = !show3D;
                hasViewedModel = true; // Mark that participant has pressed 'B'

                if (show3D)
                {
                    // Show 3D model, hide sphere (pause sphere rotation per spec line 162)
                    if (referenceSphere != null)
                    {
                        referenceSphere.PauseRotation();
                        referenceSphere.SetVisibility(false);
                    }

                    if (calibrationModel != null)
                    {
                        calibrationModel.SetVisibility(true);
                        calibrationModel.SetAdjustmentEnabled(true);
                    }

                    // Update control reminder for 3D adjustment view
                    if (ExplainText != null)
                    {
                        ExplainText.text = "Press B to toggle view. Move joystick up/down. Press A to submit";
                    }
                }
                else
                {
                    // Show sphere, hide 3D model (resume sphere rotation per spec line 163)
                    if (referenceSphere != null)
                    {
                        referenceSphere.SetVisibility(true);
                        referenceSphere.ResumeRotation();
                    }

                    if (calibrationModel != null)
                    {
                        calibrationModel.SetVisibility(false);
                        calibrationModel.SetAdjustmentEnabled(false);
                    }

                    // Update control reminder for sphere view
                    if (ExplainText != null)
                    {
                        ExplainText.text = "Press B to toggle view";
                    }
                }
            }

            if (GetButtonDown())
            {
                // Only allow submission if participant has viewed the model
                if (hasViewedModel)
                {
                    submitted = true;
                }
                else
                {
                    // Flash warning text briefly
                    StartCoroutine(FlashWarningText("Please press 'B' to view and adjust the model first!"));
                }
            }

            yield return null;
        }

        // Confirmation stage: rotate the calibration model like the sphere
        bool confirmed = false;
        bool resetRequested = false;

        while (!confirmed)
        {
            // Hide sphere
            if (referenceSphere != null)
            {
                referenceSphere.PauseRotation();
                referenceSphere.SetVisibility(false);
            }

            // Show 3D model rotating (match sphere rotation speed: 60 deg/s)
            if (calibrationModel != null)
            {
                calibrationModel.SetVisibility(true);
                calibrationModel.SetAdjustmentEnabled(false);
                calibrationModel.SetRotationMode(true, 60f);  // Match sphere rotation speed
            }

            // Update instruction text
            if (ExplainText != null)
            {
                ExplainText.text = "Press A to confirm, B to reset";
            }

            yield return new WaitForSeconds(0.5f);

            // Wait for confirmation (A) or reset (B)
            bool waitingForInput = true;
            while (waitingForInput)
            {
                if (GetButtonDown())
                {
                    // Confirm
                    confirmed = true;
                    waitingForInput = false;
                }
                else if (GetSecondaryButtonDown())
                {
                    // Reset - go back to adjustment
                    resetRequested = true;
                    waitingForInput = false;
                }
                yield return null;
            }

            if (resetRequested)
            {
                // Stop rotating mode and reset amplitude to starting value
                if (calibrationModel != null)
                {
                    calibrationModel.SetRotationMode(false);
                    // Reset amplitude to trial's starting value
                    calibrationModel.ResetParameters(1.0f, 1.5f, 0f, trial.startingAmplitude);
                    calibrationModel.SetVisibility(true);
                    calibrationModel.SetAdjustmentEnabled(true);
                }

                // Keep sphere hidden initially
                if (referenceSphere != null)
                {
                    referenceSphere.PauseRotation();
                    referenceSphere.SetVisibility(false);
                }

                // Update control reminder for 3D adjustment view
                if (ExplainText != null)
                {
                    ExplainText.text = "Press B to toggle view. Move joystick up/down. Press A to submit";
                }

                yield return new WaitForSeconds(0.5f);

                // Re-enter adjustment phase - start in 3D view but allow toggling
                show3D = true;
                submitted = false;
                resetRequested = false;

                while (!submitted)
                {
                    if (GetSecondaryButtonDown())
                    {
                        // Toggle between sphere and 3D model
                        show3D = !show3D;

                        if (show3D)
                        {
                            // Show 3D model, hide sphere
                            if (referenceSphere != null)
                            {
                                referenceSphere.PauseRotation();
                                referenceSphere.SetVisibility(false);
                            }

                            if (calibrationModel != null)
                            {
                                calibrationModel.SetVisibility(true);
                                calibrationModel.SetAdjustmentEnabled(true);
                            }

                            // Update control reminder for 3D adjustment view
                            if (ExplainText != null)
                            {
                                ExplainText.text = "Press B to toggle view. Move joystick up/down. Press A to submit";
                            }
                        }
                        else
                        {
                            // Show sphere, hide 3D model
                            if (referenceSphere != null)
                            {
                                referenceSphere.SetVisibility(true);
                                referenceSphere.ResumeRotation();
                            }

                            if (calibrationModel != null)
                            {
                                calibrationModel.SetVisibility(false);
                                calibrationModel.SetAdjustmentEnabled(false);
                            }

                            // Update control reminder for sphere view
                            if (ExplainText != null)
                            {
                                ExplainText.text = "Press B to toggle view";
                            }
                        }
                    }

                    if (GetButtonDown())
                    {
                        submitted = true;
                    }

                    yield return null;
                }
            }
        }

        float reactionTime = Time.time - trialStartTime;
        float amplitude = calibrationModel != null ? calibrationModel.GetAdjustmentValue() : 0f;

        // Hide both objects and stop sphere rotation
        if (referenceSphere != null)
        {
            referenceSphere.StopRotation();
            referenceSphere.SetVisibility(false);
        }

        if (calibrationModel != null)
        {
            calibrationModel.SetVisibility(false);
            calibrationModel.SetRotationMode(false);
            calibrationModel.SetAdjustmentEnabled(false);
        }

        // Hide the control reminder
        if (ExplainText != null)
        {
            ExplainText.gameObject.SetActive(false);
        }

        // Record the calibration trial data
        calibrationRecords.Add(new CalibrationRecord(currentTrialIndex, trial, amplitude, reactionTime));

        yield return new WaitForSeconds(1f);
    }

    void ShowInstruction(string text)
    {
        if (instructionText != null)
        {
            instructionText.text = text;
        }
    }

    IEnumerator FlashWarningText(string text)
    {
        ShowInstruction(text);
        yield return new WaitForSeconds(2f);
        ShowInstruction("");
    }

    void ShowEyeSpecificInstruction(string text, int eye)
    {
        if (instructionText != null)
        {
            instructionText.text = "";
        }

        if (ExplainText != null)
        {
            ExplainText.text = text;
            ExplainText.gameObject.SetActive(true);
        }
    }

    void HideEyeSpecificInstructions()
    {
        if (ExplainText != null)
        {
            ExplainText.gameObject.SetActive(false);
        }
    }

    void SaveData()
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"Trefoil_DepthOnly_Experiment_{timestamp}.csv";
        string path = Path.Combine(Application.persistentDataPath, filename);

        StringBuilder csv = new StringBuilder();
        csv.AppendLine("TrialNumber,TrialType,ConfigID,SphereDepth,R1,R2,RotationSpeed,Direction,StartingAmplitude,AdjustedAmplitude,ReactionTime,Timestamp");

        // Add calibration records
        foreach (var record in calibrationRecords)
        {
            csv.AppendLine($"{record.trialNumber},{record.trialType},{record.configID},{record.sphereDepth},,,,,{record.startingAmplitude},{record.adjustedAmplitude},{record.reactionTime},{record.timestamp}");
        }

        // Add depth adjustment records
        foreach (var record in allRecords)
        {
            csv.AppendLine($"{record.trialNumber},{record.trialType},{record.configID},,{record.R1},{record.R2},{record.rotationSpeed},{record.direction},{record.startingAmplitude},{record.adjustedAmplitude},{record.reactionTime},{record.timestamp}");
        }

        File.WriteAllText(path, csv.ToString());
    }
}
