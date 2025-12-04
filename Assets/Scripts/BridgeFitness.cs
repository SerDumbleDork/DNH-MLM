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
        float fitness = 0f;

        float leftX  = barCreator.leftBound  != null ? barCreator.leftBound.position.x  : -10f;
        float rightX = barCreator.rightBound != null ? barCreator.rightBound.position.x :  10f;

        float spanMid   = (leftX + rightX) * 0.5f;
        float spanWidth = Mathf.Max(1f, Mathf.Abs(rightX - leftX));
        float extraMargin = spanWidth * 0.3f + 3f;

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
            float targetDist = rightX - startX;
            float distNorm = Mathf.Clamp01(maxDistance / (targetDist + 0.0001f));
            fitness += distNorm * 80f;
        }

        if (checkpoint != null && checkpoint.checkpointReached)
            fitness += 35f;

        if (goal != null && goal.endReached)
            fitness += 100f;

        RoadBar[] roads = Object.FindObjectsOfType<RoadBar>();
        BeamBar[] beams = Object.FindObjectsOfType<BeamBar>();

        int totalBars = 0;
        int connectedBars = 0;
        int floatingBars = 0;
        int anchoredBars = 0;
        int crossSpanBars = 0;
        int brokenBars = 0;

        float sagPenalty = 0f;
        float steepPenalty = 0f;
        float oobPenalty = 0f;

        Point[] anchorPoints = null;
        if (barCreator != null && barCreator.ai != null && barCreator.ai.pbtManager != null)
            anchorPoints = barCreator.ai.pbtManager.anchorPoints;

        void ScoreBar(Point nodeA, Point nodeB, DistanceJoint2D jA, DistanceJoint2D jB, BridgeGene gene)
        {
            if (nodeA == null || nodeB == null) return;
            totalBars++;

            bool connected = (jA != null && jB != null);
            bool anchored  = nodeA.isAnchored || nodeB.isAnchored;

            if (connected) fitness += 1.0f;
            else
            {
                floatingBars++;
                fitness -= 0.1f;
            }

            if (anchored)
            {
                anchoredBars++;
                fitness += 1.5f;
            }

            if (gene != null && gene.broken)
            {
                brokenBars++;
            }

            Vector2 a = nodeA.rb.position;
            Vector2 b = nodeB.rb.position;
            Vector2 mid = (a + b) * 0.5f;

            float length = Vector2.Distance(a, b);
            float horizontalness = 1f - Mathf.Abs(a.y - b.y) / (length + 0.0001f);

            float lengthScore  = Mathf.Clamp01(1f - Mathf.Abs(length - 2f) / 2f);
            float angleScore   = Mathf.Clamp01(horizontalness * 2f);
            float geoScore     = (lengthScore * angleScore);

            fitness += geoScore * 2.5f;

            float nearestAnchorDist = 999f;
            if (anchorPoints != null)
            {
                foreach (var p in anchorPoints)
                {
                    if (p == null) continue;
                    float d = Vector2.Distance(mid, p.rb.position);
                    if (d < nearestAnchorDist) nearestAnchorDist = d;
                }
            }

            if (!anchored)
            {
                float penalty = Mathf.Clamp(nearestAnchorDist * 0.15f, 0f, 2f);
                fitness -= penalty;
            }

            bool crossesSpan =
                (a.x < spanMid && b.x > spanMid) ||
                (a.x > spanMid && b.x < spanMid);

            if (crossesSpan)
                crossSpanBars++;

            float dMid = Mathf.Abs(mid.x - spanMid);
            float spanProx = Mathf.Clamp01(1f - dMid / (spanWidth * 0.5f));
            fitness += spanProx * 1.8f;

            bool simpleOOB = mid.x < leftX - 1f || mid.x > rightX + 1f;
            if (simpleOOB) fitness -= 0.2f;

            bool hardOOB =
                mid.x < leftX - extraMargin ||
                mid.x > rightX + extraMargin ||
                mid.y < -10f ||
                mid.y > 14f;

            if (hardOOB) oobPenalty += 0.3f;

            float sag = Mathf.Max(0f, 0f - mid.y);
            sagPenalty += sag * 0.05f;

            float dy = Mathf.Abs(a.y - b.y);
            if (dy > 1.5f)
                steepPenalty += (dy - 1.5f) * 0.05f;

            float barDir = Mathf.Sign(b.x - a.x);
            fitness += (barDir > 0f ? 0.3f : -0.2f);
        }

        foreach (var r in roads) ScoreBar(r.nodeA, r.nodeB, r.jointA, r.jointB, r.geneReference);
        foreach (var b in beams) ScoreBar(b.nodeA, b.nodeB, b.jointA, b.jointB, b.geneReference);

        if (totalBars > 0)
        {
            float barCount = totalBars;
            float connectedRatio = connectedBars / barCount;
            float anchoredRatio  = anchoredBars  / barCount;
            float spanRatio      = crossSpanBars / barCount;
            float densityNorm    = Mathf.Clamp01(totalBars / 16f);

            fitness += connectedRatio * 12f;
            fitness += anchoredRatio  * 10f;
            fitness += spanRatio      * 10f;
            fitness += densityNorm    * 4f;
        }

        float structurePenalty =
            brokenBars * 0.6f +
            floatingBars * 0.2f +
            sagPenalty +
            steepPenalty +
            oobPenalty;

        structurePenalty = Mathf.Clamp(structurePenalty, 0f, 15f);
        fitness -= structurePenalty;

        return Mathf.Clamp(fitness, -9999f, 9999f);
    }
}
