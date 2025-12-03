using UnityEngine;
using System.Collections.Generic;

public static class BridgeFitness
{
    public static float Evaluate(
        Car[] cars,
        BridgeGene[] genes,
        Goal goal,
        Checkpoint checkpoint,
        BarCreator barCreator)
    {
        int generation = 0;
        Point[] anchors = null;

        if (barCreator != null && barCreator.ai != null && barCreator.ai.pbtManager != null)
        {
            generation = barCreator.ai.pbtManager.generationIndex;
            anchors    = barCreator.ai.pbtManager.anchorPoints;
        }

        bool softMode = generation < 150;

        float fitness = 0f;
        float targetBarCount = 16f;

        float maxDistance = 0f;
        float startX = 0f;
        bool haveCar = false;

        if (cars != null)
        {
            foreach (var c in cars)
            {
                if (c == null) continue;
                haveCar = true;
                maxDistance = Mathf.Max(maxDistance, c.DistanceTravelled);
                startX = c.startX;
            }
        }

        if (goal != null && haveCar)
        {
            float targetDist = goal.transform.position.x - startX;
            if (targetDist > 0.1f)
            {
                float distNorm = Mathf.Clamp01(maxDistance / targetDist);
                fitness += distNorm * 30f;
            }
        }

        if (goal != null && goal.endReached)
            fitness += 40f;

        if (checkpoint != null && checkpoint.checkpointReached)
            fitness += 12f;

        RoadBar[] roads = Object.FindObjectsOfType<RoadBar>();
        BeamBar[] beams = Object.FindObjectsOfType<BeamBar>();

        int totalBars      = 0;
        int connectedBars  = 0;
        int floatingBars   = 0;
        int anchoredBars   = 0;
        int crossSpanBars  = 0;
        int brokenBars     = 0;

        float sagPenalty   = 0f;
        float steepPenalty = 0f;
        float oobPenalty   = 0f;

        float leftX  = haveCar ? startX : -10f;
        float rightX = goal != null ? goal.transform.position.x : 10f;

        float spanMid   = (leftX + rightX) * 0.5f;
        float spanWidth = Mathf.Max(Mathf.Abs(rightX - leftX), 1f);
        float extraMargin = spanWidth * 0.4f + 4f;

        void ScoreBar(Point nodeA, Point nodeB, DistanceJoint2D jA, DistanceJoint2D jB, BridgeGene gene)
        {
            if (nodeA == null || nodeB == null) return;

            totalBars++;

            bool isConnected = (jA != null && jB != null);
            if (isConnected) connectedBars++;
            else floatingBars++;

            if (gene != null && gene.broken)
                brokenBars++;

            bool anchored = nodeA.isAnchored || nodeB.isAnchored;
            if (anchored) anchoredBars++;

            Vector2 a = nodeA.rb.position;
            Vector2 b = nodeB.rb.position;
            Vector2 mid = (a + b) * 0.5f;

            float nearestAnchorDist = 999f;
            if (anchors != null && anchors.Length > 0)
            {
                for (int i = 0; i < anchors.Length; i++)
                {
                    var pt = anchors[i];
                    if (pt == null) continue;
                    float d = Vector2.Distance(mid, pt.rb.position);
                    if (d < nearestAnchorDist) nearestAnchorDist = d;
                }
            }

            if (isConnected)
                fitness += softMode ? 0.1f : 0.2f;
            else
                fitness -= softMode ? 0.1f : 0.3f;

            if (anchored)
                fitness += softMode ? 0.1f : 0.3f;

            float length = Vector2.Distance(a, b);
            float horizontalness = 1f - Mathf.Abs(a.y - b.y) / (length + 0.0001f);
            float lengthScore = Mathf.Clamp01(1f - Mathf.Abs(length - 2f) / 2f);
            float angleScore = Mathf.Clamp01(horizontalness * 2f);
            float triangleScore = lengthScore * angleScore;
            fitness += triangleScore * (softMode ? 0.4f : 0.8f);

            if (softMode)
            {
                if (nearestAnchorDist > 3f)
                    fitness -= Mathf.Clamp(nearestAnchorDist * 0.1f, 0f, 1.5f);
            }
            else
            {
                if (nearestAnchorDist > 4f && !anchored)
                    fitness -= Mathf.Clamp(nearestAnchorDist * 0.2f, 0f, 3f);
            }

            bool crossesSpan =
                (a.x < spanMid && b.x > spanMid) ||
                (a.x > spanMid && b.x < spanMid);
            if (crossesSpan)
                crossSpanBars++;

            float dMid = Mathf.Abs(mid.x - spanMid);
            float spanProx = Mathf.Clamp01(1f - dMid / (spanWidth * 0.5f + 0.0001f));
            fitness += spanProx * (softMode ? 0.5f : 1.0f);

            if (mid.x < leftX - 1f || mid.x > rightX + 1f)
                fitness -= softMode ? 0.2f : 0.5f;

            float sag = Mathf.Max(0f, 0f - mid.y);
            if (sag > 0f)
                sagPenalty += sag * (softMode ? 0.05f : 0.2f);

            float dy = Mathf.Abs(a.y - b.y);
            if (dy > 1.5f)
                steepPenalty += (dy - 1.5f) * (softMode ? 0.05f : 0.2f);

            if (mid.x < leftX - extraMargin ||
                mid.x > rightX + extraMargin ||
                mid.y < -10f ||
                mid.y > 14f)
            {
                oobPenalty += softMode ? 0.1f : 0.3f;
            }

            float spanDir = Mathf.Sign((goal != null ? goal.transform.position.x : rightX) - a.x);
            float barDir = Mathf.Sign(b.x - a.x);
            if (barDir == spanDir)
                fitness += 0.1f;
            else
                fitness -= 0.1f;
        }

        foreach (var r in roads)
            ScoreBar(r.nodeA, r.nodeB, r.jointA, r.jointB, r.geneReference);

        foreach (var b in beams)
            ScoreBar(b.nodeA, b.nodeB, b.jointA, b.jointB, b.geneReference);

        if (totalBars > 0)
        {
            float barCount = totalBars;

            float connectedRatio = connectedBars / barCount;
            float anchoredRatio  = anchoredBars  / barCount;
            float spanRatio      = crossSpanBars / barCount;

            fitness += connectedRatio * (softMode ? 3f : 6f);
            fitness += anchoredRatio  * (softMode ? 2f : 4.5f);
            fitness += spanRatio      * (softMode ? 1.5f : 4f);

            float densityNorm = Mathf.Clamp01(totalBars / targetBarCount);
            fitness += densityNorm * (softMode ? 1f : 2f);
        }

        float structurePenalty =
            brokenBars  * (softMode ? 0.3f : 1.2f) +
            floatingBars * (softMode ? 0.1f : 0.3f) +
            sagPenalty +
            steepPenalty +
            oobPenalty;

        structurePenalty = Mathf.Clamp(structurePenalty, 0f, softMode ? 12f : 30f);
        fitness -= structurePenalty;

        fitness = Mathf.Clamp(fitness, -50f, 200f);

        return fitness;
    }
}
