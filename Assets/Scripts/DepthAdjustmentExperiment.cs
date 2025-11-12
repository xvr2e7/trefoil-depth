using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
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
        Debug.Log("DepthAdjustmentExperiment: Starting initialization");

        practiceTrials = DepthAdjustmentTrialGenerator.GeneratePracticeTrials();
        mainTrials = DepthAdjustmentTrialGenerator.GenerateMainTrials();

        Debug.Log($"Generated {practiceTrials.Count} practice trials and {mainTrials.Count} main trials");

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetVisibility(false);
            Debug.Log("Stimulus trefoil hidden");
        }
        else
        {
            Debug.LogError("Stimulus trefoil reference is missing!");
        }

        if (adjustableModel != null)
        {
            adjustableModel.SetVisibility(false);
            Debug.Log("Adjustable model hidden");
        }
        else
        {
            Debug.LogError("Adjustable model reference is missing!");
        }

        ShowInstruction("Welcome to the Depth Adjustment Task!\n\nPress 'A' to begin.");

        if (autoStart)
        {
            experimentStarted = true;
            StartCoroutine(RunExperiment());
        }
    }

    void Update()
    {
        if (!experimentStarted && !experimentRunning)
        {
            if (OVRInput.GetDown(OVRInput.Button.One))
            {
                Debug.Log("Button A pressed - starting experiment");
                experimentStarted = true;
                experimentRunning = true;
                StartCoroutine(RunExperiment());
            }
        }
    }

    IEnumerator RunExperiment()
    {
        Debug.Log("RunExperiment started");
        yield return StartCoroutine(WelcomePhase());
        yield return StartCoroutine(PracticePhase());
        yield return StartCoroutine(MainExperimentPhase());
        yield return StartCoroutine(EndPhase());
    }

    IEnumerator WelcomePhase()
    {
        Debug.Log("WelcomePhase started");
        currentState = ExperimentState.Welcome;
        ShowInstruction("In this task, you will see a rotating black curve (right eye only).\n\n" +
                       "Adjust the white curve using the RIGHT joystick (Y-axis)\n" +
                       "to match the depth you perceive in the black curve.\n\n" +
                       "Use the LEFT joystick (X-axis) to indicate your confidence.\n\n" +
                       "Press 'A' to continue.");
        yield return new WaitUntil(() => OVRInput.GetDown(OVRInput.Button.One));
        yield return new WaitForSeconds(0.3f);
        Debug.Log("WelcomePhase completed");
    }

    IEnumerator PracticePhase()
    {
        Debug.Log("PracticePhase started");
        currentState = ExperimentState.PracticeIntro;
        ShowInstruction("Practice Trials\n\n" +
                       "You will now have 2 practice trials.\n\n" +
                       "When ready, press 'A' to submit your adjustment.\n\n" +
                       "Press 'A' to start practice.");
        yield return new WaitUntil(() => OVRInput.GetDown(OVRInput.Button.One));
        yield return new WaitForSeconds(0.5f);

        currentState = ExperimentState.Practice;
        isPractice = true;

        for (int i = 0; i < practiceTrials.Count; i++)
        {
            Debug.Log($"Starting practice trial {i + 1}/{practiceTrials.Count}");
            currentTrialIndex = i;
            yield return StartCoroutine(RunTrial(practiceTrials[i], true));
        }
        Debug.Log("PracticePhase completed");
    }

    IEnumerator MainExperimentPhase()
    {
        Debug.Log("MainExperimentPhase started");
        currentState = ExperimentState.MainIntro;
        ShowInstruction("Main Experiment\n\n" +
                       "The practice is complete.\n\n" +
                       "You will now complete " + mainTrials.Count + " trials.\n\n" +
                       "Press 'A' to begin.");
        yield return new WaitUntil(() => OVRInput.GetDown(OVRInput.Button.One));
        yield return new WaitForSeconds(0.5f);

        currentState = ExperimentState.Main;
        isPractice = false;

        for (int i = 0; i < mainTrials.Count; i++)
        {
            Debug.Log($"Starting main trial {i + 1}/{mainTrials.Count}");
            currentTrialIndex = i;
            yield return StartCoroutine(RunTrial(mainTrials[i], false));

            if ((i + 1) % 10 == 0 && i + 1 < mainTrials.Count)
            {
                ShowInstruction($"Break\n\nCompleted {i + 1} of {mainTrials.Count} trials.\n\n" +
                               "Take a short break if needed.\n\n" +
                               "Press 'A' to continue.");
                yield return new WaitUntil(() => OVRInput.GetDown(OVRInput.Button.One));
                yield return new WaitForSeconds(0.5f);
            }
        }
        Debug.Log("MainExperimentPhase completed");
    }

    IEnumerator EndPhase()
    {
        Debug.Log("EndPhase started");
        currentState = ExperimentState.End;
        SaveData();
        ShowInstruction("Experiment Complete!\n\n" +
                       "Thank you for your participation.\n\n" +
                       "Data has been saved.");
        yield return new WaitForSeconds(3f);
    }

    IEnumerator RunTrial(DepthAdjustmentTrial trial, bool practice)
    {
        Debug.Log($"RunTrial: R1={trial.R1}, R2={trial.R2}, speed={trial.rotationSpeed}, dir={trial.direction}");

        ShowInstruction("Adjust the white curve to match the black curve\n\n" +
                       "Press 'A' when ready to submit");

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
            Debug.Log("Stimulus visible");
        }

        if (adjustableModel != null)
        {
            adjustableModel.SetVisibility(true);
            Debug.Log("Adjustable model visible");
        }

        trialStartTime = Time.time;

        yield return new WaitUntil(() => OVRInput.GetDown(OVRInput.Button.One));

        float reactionTime = Time.time - trialStartTime;
        var (amplitude, confidence) = adjustableModel != null ? adjustableModel.GetAdjustmentValues() : (0f, 0f);

        Debug.Log($"Trial completed: amplitude={amplitude}, confidence={confidence}, RT={reactionTime}");

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
            Debug.Log($"Instruction shown: {text.Substring(0, Mathf.Min(50, text.Length))}...");
        }
        else
        {
            Debug.LogWarning("Instruction text reference is missing!");
        }
    }

    void SaveData()
    {
        string directory = Application.persistentDataPath + "/DepthAdjustmentData/";
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string filename = directory + participantID + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";

        StringBuilder csv = new StringBuilder();
        csv.AppendLine("TrialNumber,R1,R2,RotationSpeed,Direction,AdjustedAmplitude,Confidence,ReactionTime,Timestamp");

        foreach (var record in records)
        {
            csv.AppendLine($"{record.trialNumber},{record.R1},{record.R2},{record.rotationSpeed}," +
                          $"{record.direction},{record.adjustedAmplitude},{record.confidence}," +
                          $"{record.reactionTime},{record.timestamp}");
        }

        File.WriteAllText(filename, csv.ToString());
        Debug.Log("Data saved to: " + filename);
    }
}