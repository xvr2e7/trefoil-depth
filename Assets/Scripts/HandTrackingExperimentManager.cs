using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using TMPro;

public class HandTrackingExperimentManager : MonoBehaviour
{
    [Header("Depth Task")]
    public TrefoilGenerator frozenStimulus;
    public TrefoilGenerator rotatingReference;
    public HandTrackingTracer handTracer;

    [Header("Calibration")]
    public CubeCalibrator calibrationCube;

    [Header("UI")]
    public TextMeshProUGUI instructionText;
    public TextMeshProUGUI ExplainText;

    [Header("Experiment Settings")]
    public bool autoStart = false;

    private List<HandTrackingTrial> practiceTrials;
    private List<HandTrackingTrial> mainTrials;
    private List<HandTrackingRecord> allRecords = new List<HandTrackingRecord>();

    private int currentTrialIndex = 0;
    private bool isPractice = true;
    private float trialStartTime;
    private bool experimentStarted = false;
    private bool experimentRunning = false;
    private bool waitingForFreeze = false;

    private InputDevice rightHandDevice;
    private bool lastButtonState = false;
    private bool lastSecondaryButtonState = false;

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

        practiceTrials = HandTrackingTrialGenerator.GeneratePracticeTrials();
        mainTrials = HandTrackingTrialGenerator.GenerateMainTrials();

        if (frozenStimulus != null)
        {
            frozenStimulus.SetVisibility(false);
        }

        if (rotatingReference != null)
        {
            rotatingReference.SetVisibility(false);
        }

        if (handTracer != null)
        {
            handTracer.EnableTracing(false);
        }

        if (ExplainText != null)
        {
            ExplainText.text = "";
            ExplainText.gameObject.SetActive(false);
        }

        if (calibrationCube != null)
        {
            calibrationCube.SetVisibility(false);
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
        yield return StartCoroutine(PracticePhase());
        yield return StartCoroutine(MainExperimentPhase());
        yield return StartCoroutine(EndPhase());
    }

    IEnumerator CalibrationPhase()
    {
        // Phase 1: Hand tracking practice
        currentState = ExperimentState.CalibrationStage1;

        ShowEyeSpecificInstruction("CALIBRATION 1/3: Hand Tracking Test\n\n" +
                                   "Practice the hand controls:\n\n" +
                                   "RIGHT hand: Pinch and hold = Draw\n" +
                                   "LEFT hand: Hover index finger = Erase\n" +
                                   "'B' button = Clear all traces\n\n" +
                                   "Each pinch creates a separate trace.\n" +
                                   "Try drawing and erasing.\n\n" +
                                   "Press 'A' when ready to continue.", 1);

        if (handTracer != null)
        {
            handTracer.EnableTracing(true);
        }

        yield return new WaitForSeconds(0.5f);

        // Wait for 'A' button, but allow 'B' to clear during practice
        bool continuePressed = false;
        while (!continuePressed)
        {
            if (GetButtonDown())
            {
                continuePressed = true;
            }
            if (GetSecondaryButtonDown() && handTracer != null)
            {
                handTracer.ClearTrace();
                Debug.Log("Traces cleared during calibration practice");
            }
            yield return null;
        }

        yield return new WaitForSeconds(0.3f);

        if (handTracer != null)
        {
            handTracer.EnableTracing(false);
            handTracer.ClearTrace();
        }

        // Phase 2: Motor calibration with cube tracing
        currentState = ExperimentState.CalibrationStage2;

        if (calibrationCube != null)
        {
            calibrationCube.SetVisibility(true);
        }

        ShowEyeSpecificInstruction("CALIBRATION 2/3: Motor Test\n\n" +
                                   "You will see a wireframe cube.\n\n" +
                                   "Practice tracing along the highlighted edges.\n\n" +
                                   "RIGHT hand: Pinch and hold to trace\n" +
                                   "LEFT hand: Hover to erase\n" +
                                   "'B' button: Clear all traces\n\n" +
                                   "This tests hand tracking accuracy.\n\n" +
                                   "Press 'A' to continue.", 1);

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.3f);

        // Trace a few edges for calibration
        int[] edgesToTrace = { 0, 4, 8 }; // Bottom edge, top edge, vertical edge

        if (calibrationCube != null && handTracer != null)
        {
            foreach (int edgeIndex in edgesToTrace)
            {
                calibrationCube.HighlightEdge(edgeIndex);
                handTracer.ClearTrace();
                handTracer.EnableTracing(true);

                ShowEyeSpecificInstruction($"Trace the YELLOW edge.\n\n" +
                                           "RIGHT hand: Pinch and hold to trace\n" +
                                           "LEFT hand: Hover to erase\n\n" +
                                           "'B' to clear, 'A' when done.", 1);

                yield return new WaitForSeconds(0.5f);

                // Wait for 'A' to continue, but allow 'B' to clear
                bool edgeDone = false;
                while (!edgeDone)
                {
                    if (GetButtonDown())
                    {
                        edgeDone = true;
                    }
                    if (GetSecondaryButtonDown() && handTracer != null)
                    {
                        handTracer.ClearTrace();
                        Debug.Log("Trace cleared during edge calibration");
                    }
                    yield return null;
                }

                // Calculate motor error
                List<Vector3> tracedPoints = handTracer.GetTracedPoints();
                float motorError = calibrationCube.CalculateMotorError(edgeIndex, tracedPoints);
                Debug.Log($"Motor error for edge {edgeIndex}: {motorError:F4}m");

                handTracer.ClearTrace();
                yield return new WaitForSeconds(0.3f);
            }

            calibrationCube.ClearHighlight();
            calibrationCube.SetVisibility(false);
            handTracer.EnableTracing(false);
        }

        // Phase 3: Depth perception with 2D rotating (right) and 3D reference (left)
        currentState = ExperimentState.CalibrationStage3;

        // Set up 2D rotating trefoil on the right (monocular - right eye only)
        if (rotatingReference != null)
        {
            rotatingReference.SetParameters(1.0f, 1.5f, 90f, 1);
            rotatingReference.SetShaderType(TrefoilGenerator.ShaderType.RightEyeOnly);
            rotatingReference.ResumeRotation();
            rotatingReference.SetVisibility(true);
        }

        // Set up 3D reference trefoil on the left (binocular)
        if (frozenStimulus != null)
        {
            frozenStimulus.SetParameters(1.0f, 1.5f, 90f, 1);
            frozenStimulus.SetShaderType(TrefoilGenerator.ShaderType.Binocular);
            frozenStimulus.ResumeRotation();
            frozenStimulus.SetVisibility(true);
        }

        ShowEyeSpecificInstruction("CALIBRATION 3/3: Depth Perception\n\n" +
                                   "LEFT: 3D reference (both eyes)\n" +
                                   "RIGHT: 2D rotating curve (one eye)\n\n" +
                                   "The flat curve on the right should appear\n" +
                                   "to pop out in depth, similar to the 3D model.\n\n" +
                                   "Press 'A' to confirm you see the 3D effect.", 0);

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.3f);

        if (rotatingReference != null)
        {
            rotatingReference.SetVisibility(false);
        }

        if (frozenStimulus != null)
        {
            frozenStimulus.SetVisibility(false);
        }

        HideEyeSpecificInstructions();
    }

    IEnumerator PracticePhase()
    {
        currentState = ExperimentState.PracticeIntro;
        ShowInstruction("PRACTICE - Hand Tracing Task\n\n" +
                        "You will see two rotating curves side by side.\n\n" +
                        "CONTROLS:\n" +
                        "• 'A' button = Freeze curve when ready\n" +
                        "• RIGHT hand: Pinch & hold = Draw trace\n" +
                        "• LEFT hand: Hover = Erase\n" +
                        "• 'B' button = Clear all traces\n" +
                        "• 'A' button = Submit traces\n\n" +
                        "Each pinch creates a separate trace.\n" +
                        "One curve will freeze for tracing.\n" +
                        "The other keeps rotating to maintain the 3D effect.\n\n" +
                        "Press 'A' to start practice.");

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.5f);

        currentState = ExperimentState.Practice;
        isPractice = true;

        // Hide instruction text now that trials are starting
        ShowInstruction("");

        // Run both practice trials
        for (int i = 0; i < practiceTrials.Count; i++)
        {
            yield return StartCoroutine(RunHandTracingTrial(practiceTrials[i], true));
        }
    }

    IEnumerator MainExperimentPhase()
    {
        currentState = ExperimentState.MainIntro;
        ShowInstruction("Practice complete!\n\n" +
                       "The main experiment has 20 trials.\n\n" +
                       "CONTROLS:\n" +
                       "• 'A' = Freeze / Submit\n" +
                       "• RIGHT hand: Pinch & hold = Draw\n" +
                       "• LEFT hand: Hover = Erase\n" +
                       "• 'B' = Clear all traces\n\n" +
                       "Press 'A' to begin.");

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.5f);

        currentState = ExperimentState.Main;
        isPractice = false;
        currentTrialIndex = 0;

        // Hide instruction text now that trials are starting
        ShowInstruction("");

        // Run all hand tracing trials
        for (int i = 0; i < mainTrials.Count; i++)
        {
            yield return StartCoroutine(RunHandTracingTrial(mainTrials[i], false));
            currentTrialIndex++;

            // Show progress every 5 trials
            if ((i + 1) % 5 == 0 && (i + 1) < mainTrials.Count)
            {
                ShowInstruction($"Progress: {i + 1}/{mainTrials.Count} complete.\n\nPress 'A' to continue.");
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

    IEnumerator RunHandTracingTrial(HandTrackingTrial trial, bool practice)
    {
        ShowExplainText("Watch the rotating curves.\n\nPress 'A' when ready to freeze and trace.");

        // Set up both trefoils to rotate together initially
        if (frozenStimulus != null)
        {
            frozenStimulus.SetParameters(trial.R1, trial.R2, trial.rotationSpeed, trial.direction);
            frozenStimulus.SetShaderType(TrefoilGenerator.ShaderType.Binocular); // Binocular for tracing
            frozenStimulus.ResumeRotation();
            frozenStimulus.SetVisibility(true);
        }

        // Set up rotating reference
        if (rotatingReference != null)
        {
            rotatingReference.SetParameters(trial.R1, trial.R2, trial.rotationSpeed, trial.direction);
            rotatingReference.SetShaderType(TrefoilGenerator.ShaderType.RightEyeOnly); // Monocular for stereokinetic effect
            rotatingReference.ResumeRotation();
            rotatingReference.SetVisibility(true);
        }

        if (handTracer != null)
        {
            handTracer.ClearTrace();
            handTracer.EnableTracing(false); // Don't enable tracing yet
        }

        trialStartTime = Time.time;
        waitingForFreeze = true;
        float freezeAngle = 0f;

        yield return new WaitForSeconds(0.5f);

        // Wait for 'A' button to freeze the tracing target
        while (waitingForFreeze)
        {
            if (GetButtonDown())
            {
                // Freeze the frozen stimulus
                if (frozenStimulus != null)
                {
                    freezeAngle = frozenStimulus.GetCurrentAngle();
                    frozenStimulus.PauseRotation();
                }

                waitingForFreeze = false;

                // Enable tracing
                if (handTracer != null)
                {
                    handTracer.EnableTracing(true);
                }

                ShowExplainText("Trace the 3D shape you perceive.\n\nRIGHT hand: Pinch and hold to draw\nLEFT hand: Hover to erase\n'B' button: Clear all traces\n\nPress 'A' to submit.");
            }

            yield return null;
        }

        // Wait for either 'A' (submit) or 'B' (clear) button
        bool submitted = false;
        while (!submitted)
        {
            // Check for 'A' button (submit)
            if (GetButtonDown())
            {
                // Check if participant has traced enough points
                int pointCount = handTracer != null ? handTracer.GetTracePointCount() : 0;
                Debug.Log($"Submit attempt: {pointCount} points traced (need >= 10)");

                if (pointCount >= 10)
                {
                    submitted = true;
                    Debug.Log("Submission accepted!");
                }
                else
                {
                    Debug.LogWarning($"Not enough points: {pointCount} < 10");
                    StartCoroutine(FlashWarningText("Please trace the shape with your hand first!"));
                }
            }

            // Check for 'B' button (clear)
            if (GetSecondaryButtonDown())
            {
                if (handTracer != null)
                {
                    handTracer.ClearTrace();
                    Debug.Log("Traces cleared by B button");
                }
            }

            yield return null;
        }

        float tracingDuration = Time.time - trialStartTime;
        List<Vector3> tracedPointsWorld = handTracer != null ? handTracer.GetTracedPoints() : new List<Vector3>();
        List<Vector2> trefoilSpacePoints = handTracer != null ? handTracer.GetTracedPointsInTrefoilSpace() : new List<Vector2>();

        if (frozenStimulus != null)
        {
            frozenStimulus.SetVisibility(false);
        }

        if (rotatingReference != null)
        {
            rotatingReference.SetVisibility(false);
        }

        if (handTracer != null)
        {
            handTracer.EnableTracing(false);
            handTracer.ClearTrace();
        }

        // Hide the trial instructions
        HideExplainText();

        if (!practice)
        {
            allRecords.Add(new HandTrackingRecord(currentTrialIndex, trial, tracedPointsWorld, trefoilSpacePoints,
                                                   freezeAngle, tracingDuration));
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

    void ShowExplainText(string text)
    {
        if (ExplainText != null)
        {
            ExplainText.text = text;
            ExplainText.gameObject.SetActive(true);
        }
    }

    void HideExplainText()
    {
        if (ExplainText != null)
        {
            ExplainText.gameObject.SetActive(false);
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
        string filename = $"Trefoil_HandTracking_Experiment_{timestamp}.csv";
        string path = Path.Combine(Application.persistentDataPath, filename);

        StringBuilder csv = new StringBuilder();
        csv.AppendLine("TrialNumber,ConfigurationId,RepetitionNumber,R1,R2,RotationSpeed,Direction,FreezeAngle,NumTracePoints,TracingDuration,Timestamp,TracedPointsWorldJSON,TrefoilSpacePointsJSON");

        foreach (var record in allRecords)
        {
            string worldPointsJson = JsonUtility.ToJson(new SerializableVector3List(record.tracedPointsWorld));
            string trefoilPointsJson = JsonUtility.ToJson(new SerializableVector2List(record.trefoilSpacePoints));
            csv.AppendLine($"{record.trialNumber},{record.configurationId},{record.repetitionNumber},{record.R1},{record.R2},{record.rotationSpeed},{record.direction},{record.freezeAngle},{record.numTracePoints},{record.tracingDuration},{record.timestamp},\"{worldPointsJson}\",\"{trefoilPointsJson}\"");
        }

        File.WriteAllText(path, csv.ToString());
        Debug.Log($"Data saved to: {path}");
    }
}

[System.Serializable]
public class HandTrackingRecord
{
    public int trialNumber;
    public int configurationId;
    public int repetitionNumber;
    public float R1;
    public float R2;
    public float rotationSpeed;
    public int direction;
    public float freezeAngle;
    public int numTracePoints;
    public float tracingDuration;
    public string timestamp;
    public List<Vector3> tracedPointsWorld;
    public List<Vector2> trefoilSpacePoints;

    public HandTrackingRecord(int num, HandTrackingTrial trial, List<Vector3> tracedWorld, List<Vector2> trefoilPoints,
                              float freeze, float duration)
    {
        trialNumber = num;
        configurationId = trial.configurationId;
        repetitionNumber = trial.repetitionNumber;
        R1 = trial.R1;
        R2 = trial.R2;
        rotationSpeed = trial.rotationSpeed;
        direction = trial.direction;
        freezeAngle = freeze;
        tracedPointsWorld = tracedWorld;
        trefoilSpacePoints = trefoilPoints;
        numTracePoints = tracedWorld.Count;
        tracingDuration = duration;
        timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

[System.Serializable]
public class SerializableVector2List
{
    public List<Vector2> points;

    public SerializableVector2List(List<Vector2> pts)
    {
        points = pts;
    }
}

[System.Serializable]
public class SerializableVector3List
{
    public List<Vector3> points;

    public SerializableVector3List(List<Vector3> pts)
    {
        points = pts;
    }
}
