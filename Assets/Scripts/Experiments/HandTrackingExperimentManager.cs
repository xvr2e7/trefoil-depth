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
    public TrefoilGenerator stimulusTrefoil;
    public FourierTrefoil3D calibrationModel;

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
        HandednessSelection,
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

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetVisibility(false);
        }

        if (calibrationModel != null)
        {
            calibrationModel.SetVisibility(false);
        }

        ShowInstruction("Welcome!\n\nPress 'A' to begin.");

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
        yield return StartCoroutine(HandednessSelectionPhase());
        yield return StartCoroutine(CalibrationPhase());
        yield return StartCoroutine(PracticePhase());
        yield return StartCoroutine(MainExperimentPhase());
        yield return StartCoroutine(EndPhase());
    }

    IEnumerator HandednessSelectionPhase()
    {
        currentState = ExperimentState.HandednessSelection;

        ShowInstruction("What is your dominant hand?\n\n" +
                       "Press 'A' for RIGHT hand\n" +
                       "Press 'B' for LEFT hand");

        yield return new WaitForSeconds(0.5f);

        bool handednessSelected = false;
        while (!handednessSelected)
        {
            // 'A' button = Right handed
            if (GetButtonDown())
            {
                HandednessManager.Instance.SetDominantHand(HandednessManager.Handedness.RightHanded);
                handednessSelected = true;
                ShowInstruction("Right-handed selected.\n\nPreparing experiment...");
            }
            // 'B' button = Left handed
            else if (GetSecondaryButtonDown())
            {
                HandednessManager.Instance.SetDominantHand(HandednessManager.Handedness.LeftHanded);
                handednessSelected = true;
                ShowInstruction("Left-handed selected.\n\nPreparing experiment...");
            }

            yield return null;
        }

        yield return new WaitForSeconds(1.5f);
    }

    IEnumerator CalibrationPhase()
    {
        // Stage 1: Hand tracking practice
        currentState = ExperimentState.CalibrationStage1;

        ShowEyeSpecificInstruction("CALIBRATION 1/3: Hand Controls\n\n" +
                                   GetHandControlInstructions() + "\n" +
                                   "'B' button = Clear all\n\n" +
                                   "Each pinch creates a new trace.\n" +
                                   "Try drawing and erasing.\n\n" +
                                   "Press 'A' when ready.", 1);

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
                                   "Trace the highlighted edges of the cube.\n\n" +
                                   GetHandControlInstructions() + "\n" +
                                   "'B' button: Clear all\n\n" +
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
                                           GetHandControlInstructions() + "\n\n" +
                                           "'B' to clear | 'A' when done.", 1);

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

        // Stage 3: Stereokinetic depth perception (matching DepthOnly pattern)
        currentState = ExperimentState.CalibrationStage3;

        // Part 1: Show rotating trefoil
        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetParameters(1.0f, 1.5f, 60f, 1);
            stimulusTrefoil.SetShaderType(TrefoilGenerator.ShaderType.RightEyeOnly);
            stimulusTrefoil.ResumeRotation();
            stimulusTrefoil.SetVisibility(true);
        }

        ShowEyeSpecificInstruction("CALIBRATION 3/3: Depth Perception\n\n" +
                                 "Stare at the rotating curve until you perceive something interesting.\n\n" +
                                 "Press 'A' when ready to continue.", 0);

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.3f);

        if (stimulusTrefoil != null)
        {
            stimulusTrefoil.SetVisibility(false);
        }

        // Part 2: Show 3D model for manual exploration
        ShowEyeSpecificInstruction("This is one possible 3D interpretation of the 2D stimulus you saw.\n" +
                                   "It is the general shape that you should be able to perceive.\n\n" +
                                   "Use the joystick left/right to rotate and explore.\n\n" +
                                   "Press 'A' when you are ready to continue.", 1);

        if (calibrationModel != null)
        {
            // Random depth in range [0.5, 1.0] for exploration
            float randomDepth = Random.Range(0.5f, 1.0f);
            calibrationModel.ResetParameters(1.0f, 1.5f, 0f, randomDepth);
            calibrationModel.SetManualRotationMode(true);  // Enable manual rotation with joystick
            calibrationModel.SetVisibility(true);
        }

        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => GetButtonDown());
        yield return new WaitForSeconds(0.3f);

        if (calibrationModel != null)
        {
            calibrationModel.SetVisibility(false);
            calibrationModel.SetManualRotationMode(false);
        }

        // Part 3: Show rotating trefoil again
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
        ShowInstruction("PRACTICE TRIALS\n\n" +
                        "Task: Trace the 3D shape you perceive.\n\n" +
                        "Two curves will rotate side by side.\n" +
                        "Press 'A' to freeze one for tracing.\n" +
                        "The other keeps rotating.\n\n" +
                        "CONTROLS:\n" +
                        "• " + GetHandControlInstructions().Replace("\n", "\n• ") + "\n" +
                        "• 'B' button = Clear all\n" +
                        "• 'A' button = Submit\n\n" +
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
                       "CONTROLS:\n" +
                       "• " + GetHandControlInstructions().Replace("\n", "\n• ") + "\n" +
                       "• 'A' = Freeze / Submit\n" +
                       "• 'B' = Clear all\n\n" +
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
        }
    }

    IEnumerator EndPhase()
    {
        currentState = ExperimentState.End;
        SaveData();
        ShowInstruction("Complete!\n\n" +
                       "Thank you for participating.\n\n");
        yield return new WaitForSeconds(3f);
    }

    IEnumerator RunHandTracingTrial(HandTrackingTrial trial, bool practice)
    {
        ShowExplainText("Watch the curves.\n\nPress 'A' to freeze and trace.");

        // Determine which trefoil should be on which side based on handedness
        // Right-handed: rotating (monocular) on left, drawing area (binocular) on right
        // Left-handed: drawing area (binocular) on left, rotating (monocular) on right
        bool isRightHanded = HandednessManager.Instance.IsRightHanded();

        TrefoilGenerator drawingArea = isRightHanded ? frozenStimulus : rotatingReference;
        TrefoilGenerator rotatingDisplay = isRightHanded ? rotatingReference : frozenStimulus;

        // Set up the drawing area (binocular, will be frozen for tracing)
        if (drawingArea != null)
        {
            drawingArea.SetParameters(trial.R1, trial.R2, trial.rotationSpeed, trial.direction);
            drawingArea.SetShaderType(TrefoilGenerator.ShaderType.Binocular); // Binocular for tracing
            drawingArea.ResumeRotation();
            drawingArea.SetVisibility(true);
        }

        // Set up rotating display (monocular, keeps rotating)
        if (rotatingDisplay != null)
        {
            rotatingDisplay.SetParameters(trial.R1, trial.R2, trial.rotationSpeed, trial.direction);
            rotatingDisplay.SetShaderType(TrefoilGenerator.ShaderType.RightEyeOnly); // Monocular for stereokinetic effect
            rotatingDisplay.ResumeRotation();
            rotatingDisplay.SetVisibility(true);
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
                // Freeze the drawing area trefoil
                if (drawingArea != null)
                {
                    freezeAngle = drawingArea.GetCurrentAngle();
                    drawingArea.PauseRotation();
                }

                waitingForFreeze = false;

                // Enable tracing
                if (handTracer != null)
                {
                    handTracer.EnableTracing(true);
                }

                ShowExplainText("Trace the 3D shape you perceive.\n\n" +
                               GetHandControlInstructions() + "\n" +
                               "'B' = Clear all\n\nPress 'A' to submit.");
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

        if (drawingArea != null)
        {
            drawingArea.SetVisibility(false);
        }

        if (rotatingDisplay != null)
        {
            rotatingDisplay.SetVisibility(false);
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
            string handedness = HandednessManager.Instance.IsRightHanded() ? "Right" : "Left";
            allRecords.Add(new HandTrackingRecord(currentTrialIndex, trial, tracedPointsWorld, trefoilSpacePoints,
                                                   freezeAngle, tracingDuration, handedness));
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

    // Helper method to get hand-specific control instructions
    string GetHandControlInstructions()
    {
        bool isRightHanded = HandednessManager.Instance.IsRightHanded();

        if (isRightHanded)
        {
            return "RIGHT hand: Pinch & hold = Draw\nLEFT hand: Hover finger = Erase";
        }
        else
        {
            return "LEFT hand: Pinch & hold = Draw\nRIGHT hand: Hover finger = Erase";
        }
    }

    void SaveData()
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string handedness = HandednessManager.Instance.IsRightHanded() ? "RH" : "LH";
        string filename = $"Trefoil_HandTracking_{handedness}_Experiment_{timestamp}.csv";
        string path = Path.Combine(Application.persistentDataPath, filename);

        StringBuilder csv = new StringBuilder();
        csv.AppendLine("TrialNumber,ConfigurationId,RepetitionNumber,Handedness,R1,R2,RotationSpeed,Direction,FreezeAngle,NumTracePoints,TracingDuration,Timestamp,TracedPointsWorldJSON,TrefoilSpacePointsJSON");

        foreach (var record in allRecords)
        {
            string worldPointsJson = JsonUtility.ToJson(new SerializableVector3List(record.tracedPointsWorld));
            string trefoilPointsJson = JsonUtility.ToJson(new SerializableVector2List(record.trefoilSpacePoints));
            csv.AppendLine($"{record.trialNumber},{record.configurationId},{record.repetitionNumber},{record.handedness},{record.R1},{record.R2},{record.rotationSpeed},{record.direction},{record.freezeAngle},{record.numTracePoints},{record.tracingDuration},{record.timestamp},\"{worldPointsJson}\",\"{trefoilPointsJson}\"");
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
    public string handedness;
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
                              float freeze, float duration, string hand)
    {
        trialNumber = num;
        configurationId = trial.configurationId;
        repetitionNumber = trial.repetitionNumber;
        handedness = hand;
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
