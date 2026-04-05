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
    public float startingAmplitude;
    public int configID;

    public DepthTrial(float r1, float r2, float speed, int dir, float startAmp = 0f, int configId = -1)
    {
        R1 = r1;
        R2 = r2;
        rotationSpeed = speed;
        direction = dir;
        startingAmplitude = startAmp;
        configID = configId;
    }
}

[Serializable]
public class CalibrationTrial
{
    public float sphereDepth;  // Ground truth depth of the reference sphere
    public float startingAmplitude;  // Starting amplitude for the adjustable trefoil
    public int configID;

    public CalibrationTrial(float depth, float startAmp, int configId)
    {
        sphereDepth = depth;
        startingAmplitude = startAmp;
        configID = configId;
    }
}

[Serializable]
public class CalibrationRecord
{
    public int trialNumber;
    public string trialType;
    public int configID;
    public float sphereDepth;
    public float startingAmplitude;
    public float adjustedAmplitude;
    public float reactionTime;
    public string timestamp;

    public CalibrationRecord(int num, CalibrationTrial trial, float amp, float rt)
    {
        trialNumber = num;
        trialType = "Calibration";
        configID = trial.configID;
        sphereDepth = trial.sphereDepth;
        startingAmplitude = trial.startingAmplitude;
        adjustedAmplitude = amp;
        reactionTime = rt;
        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

[Serializable]
public class DepthOnlyRecord
{
    public int trialNumber;
    public string trialType;
    public int configID;
    public float R1;
    public float R2;
    public float rotationSpeed;
    public int direction;
    public float startingAmplitude;
    public float adjustedAmplitude;
    public float reactionTime;
    public string timestamp;

    public DepthOnlyRecord(int num, DepthTrial trial, float amp, float rt)
    {
        trialNumber = num;
        trialType = "DepthAdjustment";
        configID = trial.configID;
        R1 = trial.R1;
        R2 = trial.R2;
        rotationSpeed = trial.rotationSpeed;
        direction = trial.direction;
        startingAmplitude = trial.startingAmplitude;
        adjustedAmplitude = amp;
        reactionTime = rt;
        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

public class DepthOnlyTrialGenerator
{
    public static List<CalibrationTrial> GenerateCalibrationTrials()
    {
        List<CalibrationTrial> trials = new List<CalibrationTrial>();

        // 5 different sphere depths (corresponds to trefoil amplitude values)
        // These values will be scaled by trefoilBaseZExtent (7.068) to get actual size
        // Smaller range to ensure spheres are fully visible in VR
        float[] sphereDepths = { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };

        // 4 different starting amplitudes for variety (match sphere depth range)
        float[] startingAmplitudes = { -0.5f, -0.2f, 0.2f, 0.5f };

        int configID = 0;
        foreach (float depth in sphereDepths)
        {
            foreach (float startAmp in startingAmplitudes)
            {
                trials.Add(new CalibrationTrial(depth, startAmp, configID));
                configID++;
            }
        }

        // Shuffle to randomize order
        Shuffle(trials);
        return trials;
    }

    public static List<DepthTrial> GeneratePracticeTrials()
    {
        List<DepthTrial> trials = new List<DepthTrial>();
        trials.Add(new DepthTrial(1.0f, 1.5f, 90f, 1));
        return trials;
    }

    public static List<DepthTrial> GenerateDepthTrials()
    {
        List<DepthTrial> trials = new List<DepthTrial>();

        float[] shapes = { 1.5f, 2.0f };
        int[] directions = { 1, -1 };
        float[] startingAmplitudes = { -1.5f, -0.5f, 0.5f, 1.5f };
        float speed = 90f;
        int repeats = 3;  // 3 repetitions of each configuration

        int configID = 0;
        foreach (float r2 in shapes)
        {
            foreach (int dir in directions)
            {
                foreach (float startAmp in startingAmplitudes)
                {
                    for (int r = 0; r < repeats; r++)
                    {
                        trials.Add(new DepthTrial(1.0f, r2, speed, dir, startAmp, configID));
                    }
                    configID++;  // Increment config ID for each unique configuration
                }
            }
        }

        Shuffle(trials);
        return trials;
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
