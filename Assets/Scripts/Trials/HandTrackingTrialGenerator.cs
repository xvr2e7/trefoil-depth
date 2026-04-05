using System.Collections.Generic;
using UnityEngine;

public static class HandTrackingTrialGenerator
{
    // 2 practice trials
    public static List<HandTrackingTrial> GeneratePracticeTrials()
    {
        List<HandTrackingTrial> trials = new List<HandTrackingTrial>();

        // Practice trial 1: R2 = 1.5, CCW
        trials.Add(new HandTrackingTrial(1.0f, 1.5f, 90f, 1));

        // Practice trial 2: R2 = 2.0, CW
        trials.Add(new HandTrackingTrial(1.0f, 2.0f, 90f, -1));

        return trials;
    }

    // Main experiment: 2x2x5 = 20 trials (R2 x Direction x Repetitions)
    public static List<HandTrackingTrial> GenerateMainTrials()
    {
        List<HandTrackingTrial> trials = new List<HandTrackingTrial>();

        float[] R2Values = { 1.5f, 2.0f };
        int[] directions = { 1, -1 }; // 1 = CCW, -1 = CW
        int repetitions = 5;
        int configId = 0;

        // Generate all combinations
        foreach (float R2 in R2Values)
        {
            foreach (int dir in directions)
            {
                for (int rep = 0; rep < repetitions; rep++)
                {
                    HandTrackingTrial trial = new HandTrackingTrial(1.0f, R2, 90f, dir);
                    trial.configurationId = configId;
                    trial.repetitionNumber = rep;
                    trials.Add(trial);
                }
                configId++;
            }
        }

        // Shuffle all trials globally
        for (int i = trials.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            HandTrackingTrial temp = trials[i];
            trials[i] = trials[j];
            trials[j] = temp;
        }

        return trials;
    }
}

[System.Serializable]
public class HandTrackingTrial
{
    public float R1;
    public float R2;
    public float rotationSpeed;
    public int direction; // 1 = CCW, -1 = CW
    public int configurationId;
    public int repetitionNumber;

    public HandTrackingTrial(float r1, float r2, float speed, int dir)
    {
        R1 = r1;
        R2 = r2;
        rotationSpeed = speed;
        direction = dir;
        configurationId = -1;
        repetitionNumber = -1;
    }
}
