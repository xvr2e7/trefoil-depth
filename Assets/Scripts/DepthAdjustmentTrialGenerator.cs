using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DepthAdjustmentTrial
{
    public float R1;
    public float R2;
    public float rotationSpeed;
    public int direction;

    public DepthAdjustmentTrial(float r1, float r2, float speed, int dir)
    {
        R1 = r1;
        R2 = r2;
        rotationSpeed = speed;
        direction = dir;
    }
}

[Serializable]
public class TrialRecord
{
    public int trialNumber;
    public float R1;
    public float R2;
    public float rotationSpeed;
    public int direction;
    public float adjustedAmplitude;
    public float confidence;
    public float reactionTime;
    public string timestamp;

    public TrialRecord(int num, DepthAdjustmentTrial trial, float amp, float conf, float rt)
    {
        trialNumber = num;
        R1 = trial.R1;
        R2 = trial.R2;
        rotationSpeed = trial.rotationSpeed;
        direction = trial.direction;
        adjustedAmplitude = amp;
        confidence = conf;
        reactionTime = rt;
        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

public class DepthAdjustmentTrialGenerator
{
    public static List<DepthAdjustmentTrial> GeneratePracticeTrials()
    {
        List<DepthAdjustmentTrial> trials = new List<DepthAdjustmentTrial>();
        trials.Add(new DepthAdjustmentTrial(1.0f, 1.5f, 60f, 1));
        trials.Add(new DepthAdjustmentTrial(1.0f, 1.5f, 60f, -1));
        return trials;
    }

    public static List<DepthAdjustmentTrial> GenerateMainTrials()
    {
        List<DepthAdjustmentTrial> trials = new List<DepthAdjustmentTrial>();

        float[] shapes = { 1.5f, 2.0f };
        int[] directions = { 1, -1 };
        float[] speeds = { 90f, 180f };
        int repeats = 5;

        foreach (float r2 in shapes)
        {
            foreach (int dir in directions)
            {
                foreach (float speed in speeds)
                {
                    for (int r = 0; r < repeats; r++)
                    {
                        trials.Add(new DepthAdjustmentTrial(1.0f, r2, speed, dir));
                    }
                }
            }
        }

        Shuffle(trials);
        return trials;
    }

    private static void Shuffle<T>(List<T> list)
    {
        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}