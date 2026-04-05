using System.Collections.Generic;
using UnityEngine;

public enum StrategicPointType { LobeTip, CrossSection }

[System.Serializable]
public struct StrategicPoint
{
    public int index;               // 0–5 (0–2 lobe tips, 3–5 cross-sections)
    public StrategicPointType type;
    public float phi;               // primary φ on curve
    public float phiB;              // second branch φ (cross-sections only, else float.NaN)
}

public static class StrategicPinchTrialGenerator
{
    // Grid resolution for cross-section search
    private const int GridSteps = 2000;
    private const float CrossSectionEpsilon = 0.05f;  // search threshold (curve units)
    private const float CrossSectionRefineEps = 1e-5f;

    // -----------------------------------------------------------------------
    // Public entry point: compute 6 strategic points for given R1, R2
    // Order: [LobeTip0, LobeTip1, LobeTip2, CrossSection0, CrossSection1, CrossSection2]
    // -----------------------------------------------------------------------
    public static StrategicPoint[] ComputeStrategicPoints(float R1, float R2)
    {
        StrategicPoint[] points = new StrategicPoint[6];

        // --- Lobe tips (indices 0–2) ---
        float[] tipSeeds = { 0f, 2f * Mathf.PI / 3f, 4f * Mathf.PI / 3f };
        for (int i = 0; i < 3; i++)
        {
            float phi = FindLobeTip(R1, R2, tipSeeds[i]);
            points[i] = new StrategicPoint
            {
                index = i,
                type = StrategicPointType.LobeTip,
                phi = phi,
                phiB = float.NaN
            };
        }

        // --- Cross-sections (indices 3–5) ---
        List<(float phiA, float phiB)> crossings = FindCrossSections(R1, R2);
        for (int i = 0; i < 3; i++)
        {
            float phiA = i < crossings.Count ? crossings[i].phiA : 0f;
            float phiB = i < crossings.Count ? crossings[i].phiB : 0f;
            points[3 + i] = new StrategicPoint
            {
                index = 3 + i,
                type = StrategicPointType.CrossSection,
                phi = phiA,
                phiB = phiB
            };
        }

        return points;
    }

    // -----------------------------------------------------------------------
    // Interleaved presentation order within a stop:
    //   [Tip0, Cross0, Tip1, Cross1, Tip2, Cross2]
    // -----------------------------------------------------------------------
    public static int[] PresentationOrder()
    {
        return new int[] { 0, 3, 1, 4, 2, 5 };
    }

    // -----------------------------------------------------------------------
    // Lobe tip: gradient ascent on ||P(φ)||^2 from seed
    // -----------------------------------------------------------------------
    private static float FindLobeTip(float R1, float R2, float seed)
    {
        float phi = seed;
        float step = 0.001f;
        float lr = 0.01f;

        for (int iter = 0; iter < 500; iter++)
        {
            float fPlus  = DistSqFromOrigin(R1, R2, phi + step);
            float fMinus = DistSqFromOrigin(R1, R2, phi - step);
            float grad = (fPlus - fMinus) / (2f * step);
            phi += lr * grad;
            phi = Mathf.Repeat(phi, 2f * Mathf.PI);

            if (Mathf.Abs(grad) < 1e-6f) break;
        }
        return phi;
    }

    private static float DistSqFromOrigin(float R1, float R2, float phi)
    {
        float x = R1 * Mathf.Cos(phi) + R2 * Mathf.Cos(2f * phi);
        float y = R1 * Mathf.Sin(phi) - R2 * Mathf.Sin(2f * phi);
        return x * x + y * y;
    }

    // -----------------------------------------------------------------------
    // Cross-section detection
    // Exhaustive pair search over φ grid, then bisection refinement.
    // Returns exactly 3 (phi_a, phi_b) pairs, sorted by phi_a.
    // -----------------------------------------------------------------------
    private static List<(float, float)> FindCrossSections(float R1, float R2)
    {
        float[] phi = new float[GridSteps];
        Vector2[] pts = new Vector2[GridSteps];

        for (int i = 0; i < GridSteps; i++)
        {
            phi[i] = i * 2f * Mathf.PI / GridSteps;
            pts[i] = EvalCurve(R1, R2, phi[i]);
        }

        // Collect candidate pairs: phi[i] < phi[j], ||P(i)-P(j)|| < epsilon
        // Deduplicate by clustering
        List<(float, float)> candidates = new List<(float, float)>();

        for (int i = 0; i < GridSteps - 1; i++)
        {
            // Only search j in range (i + GridSteps/4) to avoid matching nearby points on same branch
            int jStart = i + GridSteps / 6;
            for (int j = jStart; j < GridSteps; j++)
            {
                float dx = pts[i].x - pts[j].x;
                float dy = pts[i].y - pts[j].y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist < CrossSectionEpsilon)
                {
                    candidates.Add((phi[i], phi[j]));
                }
            }
        }

        // Cluster candidates into 3 crossings
        List<(float, float)> clustered = ClusterCrossings(candidates);

        // Bisection refinement for each cluster representative
        List<(float, float)> refined = new List<(float, float)>();
        foreach (var (pA, pB) in clustered)
        {
            var (rA, rB) = RefineCrossing(R1, R2, pA, pB);
            refined.Add((rA, rB));
        }

        // Sort by phiA
        refined.Sort((a, b) => a.Item1.CompareTo(b.Item1));

        // Pad to exactly 3 if fewer found (fallback to evenly spaced guess)
        while (refined.Count < 3)
        {
            float guess = refined.Count * 2f * Mathf.PI / 3f + 0.5f;
            refined.Add((Mathf.Repeat(guess, 2f * Mathf.PI),
                         Mathf.Repeat(guess + Mathf.PI, 2f * Mathf.PI)));
            Debug.LogWarning($"StrategicPinchTrialGenerator: fewer than 3 cross-sections found; using fallback for crossing {refined.Count}");
        }

        return refined;
    }

    private static List<(float, float)> ClusterCrossings(List<(float, float)> candidates)
    {
        List<(float, float)> result = new List<(float, float)>();
        bool[] used = new bool[candidates.Count];
        float clusterRadius = 2f * Mathf.PI / GridSteps * 20f; // ~20 grid steps

        for (int i = 0; i < candidates.Count; i++)
        {
            if (used[i]) continue;

            // Gather cluster around candidates[i]
            float sumA = candidates[i].Item1;
            float sumB = candidates[i].Item2;
            int count = 1;
            used[i] = true;

            for (int j = i + 1; j < candidates.Count; j++)
            {
                if (used[j]) continue;
                float dA = Mathf.Abs(candidates[j].Item1 - candidates[i].Item1);
                float dB = Mathf.Abs(candidates[j].Item2 - candidates[i].Item2);
                if (dA < clusterRadius && dB < clusterRadius)
                {
                    sumA += candidates[j].Item1;
                    sumB += candidates[j].Item2;
                    count++;
                    used[j] = true;
                }
            }

            result.Add((sumA / count, sumB / count));

            if (result.Count == 3) break;
        }

        return result;
    }

    // Newton / bisection refinement: find φ_a, φ_b such that P(φ_a) = P(φ_b)
    private static (float, float) RefineCrossing(float R1, float R2, float pA0, float pB0)
    {
        float step = 2f * Mathf.PI / GridSteps;
        float pA = pA0, pB = pB0;

        for (int iter = 0; iter < 100; iter++)
        {
            Vector2 ptA = EvalCurve(R1, R2, pA);
            Vector2 ptB = EvalCurve(R1, R2, pB);
            Vector2 diff = ptA - ptB;

            if (diff.magnitude < CrossSectionRefineEps) break;

            // Gradient step: move pA and pB toward each other in curve space
            Vector2 dA = EvalCurveDeriv(R1, R2, pA);
            Vector2 dB = EvalCurveDeriv(R1, R2, pB);

            // Minimise ||P(pA) - P(pB)||^2
            float gA =  2f * Vector2.Dot(diff, dA);
            float gB = -2f * Vector2.Dot(diff, dB);

            float lr = 0.001f;
            pA -= lr * gA;
            pB -= lr * gB;
            pA = Mathf.Repeat(pA, 2f * Mathf.PI);
            pB = Mathf.Repeat(pB, 2f * Mathf.PI);
        }

        return (pA, pB);
    }

    public static Vector2 EvalCurve(float R1, float R2, float phi)
    {
        float x = R1 * Mathf.Cos(phi) + R2 * Mathf.Cos(2f * phi);
        float y = R1 * Mathf.Sin(phi) - R2 * Mathf.Sin(2f * phi);
        return new Vector2(x, y);
    }

    private static Vector2 EvalCurveDeriv(float R1, float R2, float phi)
    {
        float dx = -R1 * Mathf.Sin(phi) - 2f * R2 * Mathf.Sin(2f * phi);
        float dy =  R1 * Mathf.Cos(phi) - 2f * R2 * Mathf.Cos(2f * phi);
        return new Vector2(dx, dy);
    }
}
