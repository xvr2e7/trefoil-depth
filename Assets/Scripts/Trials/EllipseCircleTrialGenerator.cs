using System.Collections.Generic;
using UnityEngine;

// Trial list for the Ellipse→Circle depth-scale control. Mirrors the structure of
// HandTrackingTrialGenerator: a static generator + a serializable trial struct +
// Fisher–Yates shuffle. The experimenter sweeps the ellipse aspect ratio; the
// participant only reports perceived depth (reach front/back).
public static class EllipseCircleTrialGenerator
{
    // A couple of unscored warm-up trials.
    public static List<EllipseCircleTrial> GeneratePracticeTrials(float diameter, float rotationSpeed)
    {
        return new List<EllipseCircleTrial>
        {
            new EllipseCircleTrial(0.5f, diameter, rotationSpeed, 1),
        };
    }

    // Full cross of aspectRatios × directions × repetitions, globally shuffled.
    // Note: a = 1 predicts zero depth (face-on circle); keep it out of the main
    // sweep, or include a single near-1 value as a "should look flat" catch trial.
    public static List<EllipseCircleTrial> GenerateMainTrials(
        float[] aspectRatios, int[] directions, int repetitions,
        float diameter, float rotationSpeed)
    {
        if (aspectRatios == null || aspectRatios.Length == 0)
            aspectRatios = new[] { 0.3f, 0.5f, 0.7f, 0.9f };
        if (directions == null || directions.Length == 0)
            directions = new[] { 1, -1 };

        var trials = new List<EllipseCircleTrial>();
        int configId = 0;

        foreach (float a in aspectRatios)
        {
            foreach (int dir in directions)
            {
                for (int rep = 0; rep < repetitions; rep++)
                {
                    var trial = new EllipseCircleTrial(a, diameter, rotationSpeed, dir)
                    {
                        configurationId  = configId,
                        repetitionNumber = rep
                    };
                    trials.Add(trial);
                }
                configId++;
            }
        }

        // Global Fisher–Yates shuffle.
        for (int i = trials.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (trials[i], trials[j]) = (trials[j], trials[i]);
        }

        return trials;
    }
}

[System.Serializable]
public class EllipseCircleTrial
{
    public float aspectRatio;    // a = minor/major, in (0,1]
    public float diameter;       // major-axis D, local units
    public float rotationSpeed;  // deg/sec about the view axis
    public int   direction;      // 1 = CCW, -1 = CW
    public int   configurationId;
    public int   repetitionNumber;

    public EllipseCircleTrial(float aspect, float d, float speed, int dir)
    {
        aspectRatio      = aspect;
        diameter         = d;
        rotationSpeed    = speed;
        direction        = dir;
        configurationId  = -1;
        repetitionNumber = -1;
    }
}
