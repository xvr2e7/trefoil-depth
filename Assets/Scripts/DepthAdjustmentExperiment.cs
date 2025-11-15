using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using TMPro;

public class DepthAdjustmentExperiment : MonoBehaviour
{
    [Header("Scene References")]
    public TrefoilGenerator stimulusTrefoil;
    public AdjustableTrefoil3D adjustableModel;
    public TextMeshProUGUI instructionText;

    [Header("Experiment Settings")]
    public string participantID = "P001";
    public bool autoStart = false;

    private List<DepthAdjustmentTrial> practiceTrials;
    private List<DepthAdjustmentTrial> mainTrials;
    private List<TrialRecord> records = new List<TrialRecord>();

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

        practiceTrials = DepthAdjustmentTrialGenerator.GeneratePracticeTrials();
        mainTrials = DepthAdjustmentTrialGenerator.GenerateMainTrials();

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetVisibility(false);
        }

        if (adjustableModel != null)
        {
            adjustableModel.SetVisibility(false);
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
        yield return StartCoroutine(WelcomePhase());
        yield return StartCoroutine(PracticePhase());
        yield return StartCoroutine(MainExperimentPhase());
        yield return StartCoroutine(EndPhase());
    }

    IEnumerator WelcomePhase()
    {
        currentState = ExperimentState.Welcome;
        ShowInstruction("In this task, you will see a rotating black curve (right eye only).\n\n" +
                       "Adjust the white curve by moving the joystick up or down.\n" +
                       "Press 'A' to continue.");

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.3f);
    }

    IEnumerator PracticePhase()
    {
        currentState = ExperimentState.PracticeIntro;
        ShowInstruction("You will now have 2 practice trials.\n\n" +
                       "When ready, press 'A' to submit your adjustment.\n\n" +
                       "Press 'A' to start practice.");

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.5f);

        currentState = ExperimentState.Practice;
        isPractice = true;

        for (int i = 0; i < practiceTrials.Count; i++)
        {
            currentTrialIndex = i;
            yield return StartCoroutine(RunTrial(practiceTrials[i], true));
        }
    }

    IEnumerator MainExperimentPhase()
    {
        currentState = ExperimentState.MainIntro;
        ShowInstruction("The practice is complete.\n\n" +
                       "Press 'A' to begin main experiment.");

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.5f);

        currentState = ExperimentState.Main;
        isPractice = false;

        for (int i = 0; i < mainTrials.Count; i++)
        {
            currentTrialIndex = i;
            yield return StartCoroutine(RunTrial(mainTrials[i], false));

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

    IEnumerator EndPhase()
    {
        currentState = ExperimentState.End;
        SaveData();
        ShowInstruction("Experiment Complete!\n\n" +
                       "Thank you for your participation.\n\n");
        yield return new WaitForSeconds(3f);
    }

    IEnumerator RunTrial(DepthAdjustmentTrial trial, bool practice)
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
        var (amplitude, confidence) = adjustableModel != null ? adjustableModel.GetAdjustmentValues() : (0f, 0f);

        if (!practice)
        {
            records.Add(new TrialRecord(currentTrialIndex, trial, amplitude, confidence, reactionTime));
        }

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetVisibility(false);
        }

        if (adjustableModel != null)
        {
            adjustableModel.SetVisibility(false);
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

    void SaveData()
    {
        string filename = $"DepthAdjustment_{participantID}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
        string path = Path.Combine(Application.persistentDataPath, filename);

        StringBuilder csv = new StringBuilder();
        csv.AppendLine("TrialNumber,R1,R2,RotationSpeed,Direction,AdjustedAmplitude,Confidence,ReactionTime,Timestamp");

        foreach (var record in records)
        {
            csv.AppendLine($"{record.trialNumber},{record.R1},{record.R2},{record.rotationSpeed},{record.direction},{record.adjustedAmplitude},{record.confidence},{record.reactionTime},{record.timestamp}");
        }

        File.WriteAllText(path, csv.ToString());
    }
}