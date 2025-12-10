using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using TMPro;

public class ExperimentManager : MonoBehaviour
{
    [Header("Depth Task")]
    public TrefoilGenerator stimulusTrefoil;
    public FourierTrefoil3D adjustableModel;

    [Header("Curvature Task")]
    public TrefoilGenerator curvatureTrefoil;
    public CurvatureSegment curvatureSegment;
    public CurvatureMarker curvatureMarker;

    [Header("Curvature Task Settings")]
    public float markerNormalOffset = 0.3f;

    [Header("UI")]
    public TextMeshProUGUI instructionText;
    public TextMeshProUGUI leftEyeText;
    public TextMeshProUGUI rightEyeText;

    [Header("Experiment Settings")]
    public bool autoStart = false;

    private List<DepthTrial> practiceTrials;
    private List<DepthTrial> depthTrials;
    private List<CurvatureTrial> minimalCurvatureTrials;
    private List<CurvatureTrial> maximalCurvatureTrials;
    private List<UnifiedRecord> allRecords = new List<UnifiedRecord>();

    private int currentTrialIndex = 0;
    private bool isPractice = true;
    private float trialStartTime;
    private bool experimentStarted = false;
    private bool experimentRunning = false;

    private InputDevice rightHandDevice;
    private bool lastButtonState = false;

    private enum ExperimentState
    {
        Welcome,
        CalibrationStage1,
        CalibrationStage2,
        CalibrationStage3,
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

        practiceTrials = StudyTrialGenerator.GeneratePracticeTrials();
        depthTrials = StudyTrialGenerator.GenerateDepthTrials();
        minimalCurvatureTrials = StudyTrialGenerator.GenerateMinimalCurvatureTrials();
        maximalCurvatureTrials = StudyTrialGenerator.GenerateMaximalCurvatureTrials();

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetVisibility(false);
        }

        if (adjustableModel != null)
        {
            adjustableModel.SetVisibility(false);
        }

        if (curvatureTrefoil != null)
        {
            curvatureTrefoil.SetVisibility(false);
        }

        if (curvatureSegment != null)
        {
            curvatureSegment.SetVisibility(false);
        }

        if (curvatureMarker != null && curvatureTrefoil != null)
        {
            curvatureMarker.Initialize(curvatureTrefoil.transform);
            curvatureMarker.SetVisibility(false);
        }

        if (leftEyeText != null)
        {
            leftEyeText.text = "";
            leftEyeText.gameObject.SetActive(false);
        }

        if (rightEyeText != null)
        {
            rightEyeText.text = "";
            rightEyeText.gameObject.SetActive(false);
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

    IEnumerator RunExperiment()
    {
        yield return StartCoroutine(CalibrationPhase());
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
            adjustableModel.ResetParameters(1.0f, 1.5f, 0f);
            adjustableModel.SetRotationMode(true);
            adjustableModel.SetVisibility(true);
        }

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.3f);

        if (adjustableModel != null)
        {
            adjustableModel.SetVisibility(false);
            adjustableModel.SetRotationMode(false);
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

    IEnumerator PracticePhase()
    {
        currentState = ExperimentState.PracticeIntro;
        ShowInstruction("PRACTICE\n\n" +
                        "TASK 1: Depth Adjustment\n\n" +
                        "You will see a rotating white curve.\n\n" +
                        "Adjust the black curve by moving the joystick UP/DOWN to match the 3D shape you perceive.\n\n" +
                        "Moving UP will increase depth, DOWN will decrease depth.\n\n" +
                        "Press 'A' to submit your adjustment.\n" +
                        "Press 'A' to start practice.");

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.5f);

        currentState = ExperimentState.Practice;
        isPractice = true;

        currentTrialIndex = 0;
        yield return StartCoroutine(RunDepthTrial(practiceTrials[0], true));

        ShowInstruction("PRACTICE\n\n" +
                        "TASK 2: Curvature Judgment\n\n" +
                        "You will see a rotating white curve and a red marker near the tip of one lobe.\n" +
                        "Stick to ONE 3D interpretation of the 2D curve as you watch the rotation.\n\n" +
                        "Press 'A' when you see the MINIMAL curvature (i.e., the least bent) at the marked tip.\n" +
                        "Then adjust the black segment to match what you perceived.\n" +
                        "Moving UP will increase depth, DOWN will decrease depth.\n\n" +
                        "Press 'A' again to submit.\n\n" +
                        "Press 'A' to start practice.");

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.5f);

        CurvatureTrial practiceCurvatureTrial = new CurvatureTrial(true, 0f);
        yield return StartCoroutine(RunCurvatureTrial(practiceCurvatureTrial, true));
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

        // Break after depth trials
        ShowInstruction("Depth adjustment task complete.\n\n" +
                       "Take a short break if needed.\n\n" +
                       "Press 'A' to continue to the next task.");

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.5f);

        // Instruction for minimal curvature block
        ShowInstruction("MINIMAL Curvature Judgment\n\n" +
                       "Press 'A' when you see MINIMAL curvature (i.e., the least bent) at the marked lobe tip.\n\n" +
                       "Then adjust the black segment's depth to match.\n\n" +
                       "Press 'A' to begin.");

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.5f);

        // Run all minimal curvature trials
        for (int i = 0; i < minimalCurvatureTrials.Count; i++)
        {
            yield return StartCoroutine(RunCurvatureTrial(minimalCurvatureTrials[i], false));
            currentTrialIndex++;
        }

        // Break between minimal and maximal curvature
        ShowInstruction("Take a short break if needed.\n\n" +
                       "Press 'A' to continue.");

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.5f);

        // Instruction for maximal curvature block
        ShowInstruction("MAXIMAL Curvature Judgment\n\n" +
                       "Press 'A' when you see MAXIMAL curvature (i.e., the most bent) at the marked lobe tip.\n\n" +
                       "Then adjust the black segment's depth to match.\n\n" +
                       "Press 'A' to begin.");

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.5f);

        // Run all maximal curvature trials
        for (int i = 0; i < maximalCurvatureTrials.Count; i++)
        {
            yield return StartCoroutine(RunCurvatureTrial(maximalCurvatureTrials[i], false));
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
            adjustableModel.ResetParameters(trial.R1, trial.R2, 0f);
        }

        yield return new WaitForSeconds(0.5f);

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetVisibility(true);
        }

        if (adjustableModel != null)
        {
            adjustableModel.SetVisibility(true);
        }

        trialStartTime = Time.time;

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());

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
        }

        if (!practice)
        {
            allRecords.Add(new UnifiedRecord(currentTrialIndex, trial, amplitude, reactionTime));
        }

        yield return new WaitForSeconds(1f);
    }

    IEnumerator RunCurvatureTrial(CurvatureTrial trial, bool practice)
    {
        ShowInstruction("");

        if (curvatureTrefoil != null)
        {
            curvatureTrefoil.SetParameters(1.0f, 2.0f, 90f, 1);
            curvatureTrefoil.SetStartingAngle(trial.startingAngle);
            curvatureTrefoil.ResumeRotation();
            curvatureTrefoil.SetVisibility(true);
        }

        if (curvatureMarker != null && curvatureTrefoil != null)
        {
            Vector3 tipPoint = curvatureTrefoil.GetPointAt(0f);
            Vector3 normal = curvatureTrefoil.GetNormalAt(0f);
            curvatureMarker.SetPosition(tipPoint + normal * markerNormalOffset);
            curvatureMarker.SetVisibility(true);
        }

        if (curvatureSegment != null)
        {
            curvatureSegment.SetVisibility(false);
        }

        yield return new WaitForSeconds(0.5f);

        trialStartTime = Time.time;

        yield return new WaitUntil(() => GetButtonDown());

        float capturedAngle = curvatureTrefoil != null ? curvatureTrefoil.GetCurrentAngle() : 0f;
        float phase1Time = Time.time - trialStartTime;

        if (curvatureTrefoil != null)
        {
            curvatureTrefoil.PauseRotation();
            curvatureTrefoil.SetColor(Color.white);
        }

        if (curvatureMarker != null)
        {
            curvatureMarker.SetVisibility(false);
        }

        if (curvatureSegment != null)
        {
            curvatureSegment.SetParameters(1.0f, 2.0f);
            curvatureSegment.ResetAmplitude(0f);
            curvatureSegment.SetColor(Color.black);
            curvatureSegment.SetAdjustmentEnabled(true);
            curvatureSegment.SetVisibility(true);
        }

        yield return new WaitForSeconds(0.3f);

        float phase2StartTime = Time.time;

        yield return new WaitUntil(() => GetButtonDown());

        float phase2Time = Time.time - phase2StartTime;
        float totalTime = phase1Time + phase2Time;
        float amplitude = curvatureSegment != null ? curvatureSegment.GetAmplitude() : 0f;

        if (curvatureTrefoil != null)
        {
            curvatureTrefoil.SetVisibility(false);
        }

        if (curvatureSegment != null)
        {
            curvatureSegment.SetAdjustmentEnabled(false);
            curvatureSegment.SetVisibility(false);
        }

        HideEyeSpecificInstructions();

        if (!practice)
        {
            allRecords.Add(new UnifiedRecord(currentTrialIndex, trial, capturedAngle, amplitude, totalTime));
        }

        yield return new WaitForSeconds(1f);
    }

    void ShowInstruction(string text)
    {
        if (instructionText != null)
        {
            instructionText.text = text;
        }
    }

    void ShowEyeSpecificInstruction(string text, int eye)
    {
        if (instructionText != null)
        {
            instructionText.text = "";
        }

        if (leftEyeText != null)
        {
            leftEyeText.gameObject.SetActive(false);
        }
        if (rightEyeText != null)
        {
            rightEyeText.gameObject.SetActive(false);
        }

        if (eye == 0 && leftEyeText != null)
        {
            leftEyeText.text = text;
            leftEyeText.gameObject.SetActive(true);
        }
        else if (eye == 1 && rightEyeText != null)
        {
            rightEyeText.text = text;
            rightEyeText.gameObject.SetActive(true);
        }
    }

    void HideEyeSpecificInstructions()
    {
        if (leftEyeText != null)
        {
            leftEyeText.gameObject.SetActive(false);
        }
        if (rightEyeText != null)
        {
            rightEyeText.gameObject.SetActive(false);
        }
    }

    void SaveData()
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"Trefoil_Experiment_{timestamp}.csv";
        string path = Path.Combine(Application.persistentDataPath, filename);

        StringBuilder csv = new StringBuilder();
        csv.AppendLine("TrialNumber,TaskType,R1,R2,RotationSpeed,Direction,AdjustedAmplitude,ReactionTime,IsMinimalCurvature,StartingAngle,CapturedAngle,Timestamp");

        foreach (var record in allRecords)
        {
            csv.AppendLine($"{record.trialNumber},{record.taskType},{record.R1},{record.R2},{record.rotationSpeed},{record.direction},{record.adjustedAmplitude},{record.reactionTime},{record.isMinimalCurvature},{record.startingAngle},{record.capturedAngle},{record.timestamp}");
        }

        File.WriteAllText(path, csv.ToString());
    }
}