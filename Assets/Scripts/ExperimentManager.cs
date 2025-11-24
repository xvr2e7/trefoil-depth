using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using TMPro;

public class ExperimentManager : MonoBehaviour
{
    [Header("Scene References")]
    public TrefoilGenerator stimulusTrefoil;
    public FourierTrefoil3D adjustableModel;
    public CurvatureSphere curvatureSphere;
    public CurvatureMarker curvatureMarker;
    public TextMeshProUGUI instructionText;
    public TextMeshProUGUI leftEyeText;
    public TextMeshProUGUI rightEyeText;

    [Header("Experiment Settings")]
    public bool autoStart = false;

    private List<DepthAdjustmentTrial> practiceTrials;
    private List<DepthAdjustmentTrial> mainTrials;
    private List<CurvatureTrial> curvatureTrials;
    private List<TrialRecord> depthRecords = new List<TrialRecord>();
    private List<CurvatureRecord> curvatureRecords = new List<CurvatureRecord>();

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
        CurvatureIntro,
        Curvature,
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
        mainTrials = StudyTrialGenerator.GenerateMainTrials();
        curvatureTrials = StudyTrialGenerator.GenerateCurvatureTrials();

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetVisibility(false);
        }

        if (curvatureMarker != null && stimulusTrefoil != null)
        {
            curvatureMarker.Initialize(stimulusTrefoil.transform);
            curvatureMarker.SetVisibility(false);
        }

        if (adjustableModel != null)
        {
            adjustableModel.SetVisibility(false);
        }

        if (curvatureSphere != null)
        {
            curvatureSphere.SetVisibility(false);
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
        yield return StartCoroutine(CurvaturePhase());
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
                        "You will see a rotating black curve.\n\n" +
                        "Adjust the white curve by moving the joystick UP/DOWN to match the 3D shape you perceive.\n\n" +
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
                        "You will see a red dot marking a location on the curve.\n\n" +
                        "First, adjust the white sphere to match the curvature at that location.\n\n" +
                        "When you're ready, press 'A'. The curve will start to rotate.\n" +
                        "Continuously adjust the sphere to match that curvature you perceive.\n\n" +
                        "Joystick UP/DOWN to increase/decrease sphere size.\n\n" +
                        "Press 'A' to start practice.");

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.5f);

        CurvatureTrial practiceCurvatureTrial = new CurvatureTrial(1.0f, 1.5f, 60f, 1, 0f);
        yield return StartCoroutine(RunCurvatureTrial(practiceCurvatureTrial, -1, true));
    }

    IEnumerator MainExperimentPhase()
    {
        currentState = ExperimentState.MainIntro;
        ShowInstruction("Practice complete.\n\n" +
                       "Press 'A' to begin.");

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.5f);

        currentState = ExperimentState.Main;
        isPractice = false;

        for (int i = 0; i < mainTrials.Count; i++)
        {
            currentTrialIndex = i;
            yield return StartCoroutine(RunDepthTrial(mainTrials[i], false));

            if ((i + 1) % 10 == 0 && i + 1 < mainTrials.Count)
            {
                ShowInstruction("Take a short break if needed.\n\n" +
                               "Press 'A' to continue.");

                yield return new WaitForSeconds(0.5f);
                yield return new WaitUntil(() => GetButtonDown());
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    IEnumerator CurvaturePhase()
    {
        currentState = ExperimentState.CurvatureIntro;
        ShowInstruction("Press 'A' to begin.");

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.5f);

        currentState = ExperimentState.Curvature;

        for (int i = 0; i < curvatureTrials.Count; i++)
        {
            currentTrialIndex = i;
            yield return StartCoroutine(RunCurvatureTrial(curvatureTrials[i], i));

            if ((i + 1) % 10 == 0 && i + 1 < curvatureTrials.Count)
            {
                ShowInstruction("Take a short break if needed.\n\n" +
                               "Press 'A' to continue.");

                yield return new WaitForSeconds(0.5f);
                yield return new WaitUntil(() => GetButtonDown());
                yield return new WaitForSeconds(0.5f);
            }
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

    IEnumerator RunDepthTrial(DepthAdjustmentTrial trial, bool practice)
    {
        ShowInstruction("");

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetParameters(trial.R1, trial.R2, trial.rotationSpeed, trial.direction);
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
            stimulusTrefoil.SetVisibility(false);
        }

        if (adjustableModel != null)
        {
            adjustableModel.SetVisibility(false);
        }

        if (!practice)
        {
            depthRecords.Add(new TrialRecord(currentTrialIndex, trial, amplitude, reactionTime));
        }

        yield return new WaitForSeconds(1f);
    }

    IEnumerator RunCurvatureTrial(CurvatureTrial trial, int trialNumber, bool practice = false)
    {
        ShowInstruction("");

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetParameters(trial.R1, trial.R2, trial.rotationSpeed, trial.direction);
            stimulusTrefoil.ResetRotation();
            stimulusTrefoil.PauseRotation();
        }

        if (curvatureSphere != null)
        {
            curvatureSphere.ResetRadius(0.5f);
        }

        if (curvatureMarker != null)
        {
            Vector3 probePoint = stimulusTrefoil.GetPointAt(trial.probePhi);
            Vector3 probeNormal = stimulusTrefoil.GetNormalAt(trial.probePhi);
            curvatureMarker.SetPosition(probePoint + probeNormal * 0.15f);
        }

        yield return new WaitForSeconds(0.5f);

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetVisibility(true);
        }

        if (curvatureMarker != null)
        {
            curvatureMarker.SetVisibility(true);
        }

        if (curvatureSphere != null)
        {
            curvatureSphere.SetVisibility(true);
            curvatureSphere.SetAdjustmentEnabled(true);
        }

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.3f);

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.ResumeRotation();
        }

        float cycleDuration = 360f / trial.rotationSpeed;
        float numCycles = practice ? 3f : 10f;
        float totalDuration = cycleDuration * numCycles;

        if (!practice)
        {
            yield return StartCoroutine(LogCurvatureData(trial, trialNumber, totalDuration));
        }
        else
        {
            yield return new WaitForSeconds(totalDuration);
        }

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.PauseRotation();
            stimulusTrefoil.SetVisibility(false);
        }

        if (curvatureSphere != null)
        {
            curvatureSphere.SetAdjustmentEnabled(false);
            curvatureSphere.SetVisibility(false);
        }

        if (curvatureMarker != null)
        {
            curvatureMarker.SetVisibility(false);
        }

        yield return new WaitForSeconds(1f);
    }

    IEnumerator LogCurvatureData(CurvatureTrial trial, int trialNumber, float duration)
    {
        float startTime = Time.time;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float radius = curvatureSphere != null ? curvatureSphere.GetRadius() : 0f;
            float angle = stimulusTrefoil != null ? stimulusTrefoil.GetCurrentAngle() : 0f;

            curvatureRecords.Add(new CurvatureRecord(trialNumber, trial, radius, angle, elapsed));

            yield return new WaitForSeconds(0.01f);
            elapsed = Time.time - startTime;
        }
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

        string depthFilename = $"Trefoil_Depth_{timestamp}.csv";
        string depthPath = Path.Combine(Application.persistentDataPath, depthFilename);

        StringBuilder depthCsv = new StringBuilder();
        depthCsv.AppendLine("TrialNumber,R1,R2,RotationSpeed,Direction,AdjustedAmplitude,ReactionTime,Timestamp");

        foreach (var record in depthRecords)
        {
            depthCsv.AppendLine($"{record.trialNumber},{record.R1},{record.R2},{record.rotationSpeed},{record.direction},{record.adjustedAmplitude},{record.reactionTime},{record.timestamp}");
        }

        File.WriteAllText(depthPath, depthCsv.ToString());
        Debug.Log($"Depth data saved to: {depthPath}");

        string curvatureFilename = $"Trefoil_Curvature_{timestamp}.csv";
        string curvaturePath = Path.Combine(Application.persistentDataPath, curvatureFilename);

        StringBuilder curvatureCsv = new StringBuilder();
        curvatureCsv.AppendLine("TrialNumber,R1,R2,RotationSpeed,Direction,ProbePhi,SphereRadius,RotationAngle,TimeInTrial");

        foreach (var record in curvatureRecords)
        {
            curvatureCsv.AppendLine($"{record.trialNumber},{record.R1},{record.R2},{record.rotationSpeed},{record.direction},{record.probePhi},{record.sphereRadius},{record.rotationAngle},{record.timestamp}");
        }

        File.WriteAllText(curvaturePath, curvatureCsv.ToString());
        Debug.Log($"Curvature data saved to: {curvaturePath}");
    }
}