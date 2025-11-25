using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DepthTrial
{
    public float R1;
    public float R2;
    public float rotationSpeed;
    public int direction;

    public DepthTrial(float r1, float r2, float speed, int dir)
    {
        R1 = r1;
        R2 = r2;
        rotationSpeed = speed;
        direction = dir;
    }
}

[Serializable]
public class CurvatureTrial
{
    public bool isMinimalCurvature;
    public float startingAngle;

    public CurvatureTrial(bool isMinimal, float startAngle)
    {
        isMinimalCurvature = isMinimal;
        startingAngle = startAngle;
    }
}

[Serializable]
public class UnifiedTrial
{
    public enum TaskType { Depth, Curvature }

    public TaskType taskType;
    public DepthTrial depthTrial;
    public CurvatureTrial curvatureTrial;

    public UnifiedTrial(DepthTrial trial)
    {
        taskType = TaskType.Depth;
        depthTrial = trial;
        curvatureTrial = null;
    }

    public UnifiedTrial(CurvatureTrial trial)
    {
        taskType = TaskType.Curvature;
        depthTrial = null;
        curvatureTrial = trial;
    }
}

[Serializable]
public class UnifiedRecord
{
    public int trialNumber;
    public string taskType;
    public float R1;
    public float R2;
    public float rotationSpeed;
    public int direction;
    public float adjustedAmplitude;
    public float reactionTime;
    public bool isMinimalCurvature;
    public float startingAngle;
    public float capturedAngle;
    public string timestamp;

    public UnifiedRecord(int num, DepthTrial trial, float amp, float rt)
    {
        trialNumber = num;
        taskType = "Depth";
        R1 = trial.R1;
        R2 = trial.R2;
        rotationSpeed = trial.rotationSpeed;
        direction = trial.direction;
        adjustedAmplitude = amp;
        reactionTime = rt;
        isMinimalCurvature = false;
        startingAngle = float.NaN;
        capturedAngle = float.NaN;
        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public UnifiedRecord(int num, CurvatureTrial trial, float captAngle, float amp, float rt)
    {
        trialNumber = num;
        taskType = "Curvature";
        R1 = 1.0f;
        R2 = 2.0f;
        rotationSpeed = 90f;
        direction = 1;
        adjustedAmplitude = amp;
        reactionTime = rt;
        isMinimalCurvature = trial.isMinimalCurvature;
        startingAngle = trial.startingAngle;
        capturedAngle = captAngle;
        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

public class StudyTrialGenerator
{
    public static List<DepthTrial> GeneratePracticeTrials()
    {
        List<DepthTrial> trials = new List<DepthTrial>();
        trials.Add(new DepthTrial(1.0f, 1.5f, 60f, 1));
        return trials;
    }

    public static List<DepthTrial> GenerateDepthTrials()
    {
        List<DepthTrial> trials = new List<DepthTrial>();

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
                        trials.Add(new DepthTrial(1.0f, r2, speed, dir));
                    }
                }
            }
        }

        return trials;
    }

    public static List<CurvatureTrial> GenerateCurvatureTrials()
    {
        List<CurvatureTrial> trials = new List<CurvatureTrial>();
        System.Random rng = new System.Random();

        for (int i = 0; i < 10; i++)
        {
            float startAngle = (float)(rng.NextDouble() * 360.0);
            trials.Add(new CurvatureTrial(true, startAngle));
        }

        for (int i = 0; i < 10; i++)
        {
            float startAngle = (float)(rng.NextDouble() * 360.0);
            trials.Add(new CurvatureTrial(false, startAngle));
        }

        Shuffle(trials);
        return trials;
    }

    public static List<UnifiedTrial> GenerateAllTrials()
    {
        List<UnifiedTrial> allTrials = new List<UnifiedTrial>();

        List<DepthTrial> depthTrials = GenerateDepthTrials();
        List<CurvatureTrial> curvatureTrials = GenerateCurvatureTrials();

        foreach (var trial in depthTrials)
        {
            allTrials.Add(new UnifiedTrial(trial));
        }

        foreach (var trial in curvatureTrials)
        {
            allTrials.Add(new UnifiedTrial(trial));
        }

        Shuffle(allTrials);
        return allTrials;
    }

    private static void Shuffle<T>(List<T> list)
    {
        System.Random rng = new System.Random(System.Guid.NewGuid().GetHashCode());
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