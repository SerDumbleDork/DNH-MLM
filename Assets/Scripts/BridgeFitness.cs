using UnityEngine;

public static class BridgeFitness
{
    public static float Evaluate(
        Car[] cars,
        BridgeGene[] genes,
        Goal goal,
        Checkpoint checkpoint,
        BarCreator bc)
    {
        if (bc == null) return 0f;

        // ----------------------------------------------------------
        // WORLD
        // ----------------------------------------------------------
        float leftX  = bc.leftBound.position.x;
        float rightX = bc.rightBound.position.x;
        float spanWidth = Mathf.Abs(rightX - leftX);

        // ----------------------------------------------------------
        // 1. CAR PROGRESS
        // ----------------------------------------------------------
        float forwardProgress = 0f;
        bool carFellEarly = false;

        if (cars != null)
        {
            foreach (var c in cars)
            {
                if (c == null) continue;

                float dist = c.transform.position.x - leftX;

                if (c.transform.position.y < -1f && dist < spanWidth * 0.2f)
                    carFellEarly = true;

                forwardProgress = Mathf.Max(forwardProgress, dist);
            }
        }

        float progress01 = spanWidth > 0.5f
            ? Mathf.Clamp01(forwardProgress / spanWidth)
            : 0f;

        // ----------------------------------------------------------
        // 2. STRUCTURE ANALYSIS
        // ----------------------------------------------------------
        RoadBar[] roads = Object.FindObjectsOfType<RoadBar>();
        BeamBar[] beams = Object.FindObjectsOfType<BeamBar>();

        int totalBars = 0;
        int connectedBars = 0;
        int anchoredBars = 0;
        int properNodeConnections = 0;

        float sagTotal = 0f;
        float oobBars  = 0f;

        const float sagLimit = 0.6f;

        void ScoreBar(Point a, Point b, DistanceJoint2D ja, DistanceJoint2D jb)
        {
            if (a == null || b == null) return;

            totalBars++;

            bool connected = (ja != null && jb != null);
            if (connected)
            {
                connectedBars++;

                if (!a.isFloating && !b.isFloating)
                    properNodeConnections++;
            }

            // Anchor usage
            if (a.isAnchored || b.isAnchored)
                anchoredBars++;

            // Sag penalty
            Vector2 mid = (a.rb.position + b.rb.position) * 0.5f;
            if (mid.y < -3f)
                sagTotal += Mathf.Abs(mid.y + 3f);

            // Out-of-bounds
            if (mid.x < leftX - 1.5f || mid.x > rightX + 1.5f)
                oobBars++;
        }

        foreach (var r in roads) ScoreBar(r.nodeA, r.nodeB, r.jointA, r.jointB);
        foreach (var b in beams) ScoreBar(b.nodeA, b.nodeB, b.jointA, b.jointB);

        float stability = 0f;
        float anchorUse = 0f;
        float properConnectionRatio = 0f;
        float sagPenalty = 0f;

        if (totalBars > 0)
        {
            stability             = Mathf.Clamp01(connectedBars / (float)totalBars);
            anchorUse             = Mathf.Clamp01(anchoredBars / (float)totalBars);
            properConnectionRatio = Mathf.Clamp01(properNodeConnections / (float)totalBars);

            sagPenalty = Mathf.Clamp01(sagTotal / (totalBars * sagLimit));
        }

        // ----------------------------------------------------------
        // 3. CHECKPOINT / GOAL
        // ----------------------------------------------------------
        float checkpointScore =
            (checkpoint != null && checkpoint.checkpointReached) ? 1f : 0f;

        float goalScore =
            (goal != null && goal.endReached) ? 1f : 0f;

        // ----------------------------------------------------------
        // 4. FINAL FITNESS
        // ----------------------------------------------------------
        float fitness = 0f;

        // Success bonuses
        fitness += goalScore * 40f;
        fitness += checkpointScore * 20f;

        // Progress
        fitness += progress01 * 20f;

        // Structure rewards
        fitness += stability * 12f;
        fitness += properConnectionRatio * 20f;
        fitness += anchorUse * 12f;

        // Penalties
        if (carFellEarly) fitness -= 10f;
        if (stability < 0.35f && totalBars > 0) fitness -= 8f;

        fitness -= sagPenalty * 15f;
        fitness -= (oobBars / Mathf.Max(1f, totalBars)) * 6f;

        // No structure at all = very bad
        if (totalBars == 0)
            fitness -= 50f;

        return Mathf.Clamp(fitness, -100f, 100f);
    }
}
