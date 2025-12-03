using UnityEngine;
using System.Collections.Generic;

public static class BridgeFitness
{
    public static float Evaluate(Car[] cars, BridgeGene[] genes, Goal goal, Checkpoint checkpoint, BarCreator barCreator)
    {
        float targetBarCount = 16f;
        float fitness = 0f;

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

        float distReward = 0f;
        float goalBonus = 0f;
        float checkpointBonus = 0f;

        if (goal != null && haveCar)
        {
            float targetDist = goal.transform.position.x - startX;
            if (targetDist > 0.1f)
            {
                float distNorm = Mathf.Clamp01(maxDistance / targetDist);
                distReward = distNorm * 80f;
            }
        }

        if (goal != null && goal.endReached)
            goalBonus = 300f;

        if (checkpoint != null && checkpoint.checkpointReached)
            checkpointBonus = 80f;

        fitness += distReward + goalBonus + checkpointBonus;

        RoadBar[] roads = Object.FindObjectsOfType<RoadBar>();
        BeamBar[] beams = Object.FindObjectsOfType<BeamBar>();

        int totalBars = 0;
        int connectedBars = 0;
        int floatingBars = 0;
        int brokenBars = 0;
        int anchoredBars = 0;
        int crossSpanBars = 0;

        float sagPenalty = 0f;
        float steepPenalty = 0f;
        float oobPenalty = 0f;

        float leftX  = haveCar ? startX : -10f;
        float rightX = goal != null ? goal.transform.position.x : 10f;

        if (Mathf.Abs(rightX - leftX) < 1f)
            rightX = leftX + 10f;

        float spanMid = (leftX + rightX) * 0.5f;
        float spanWidth = Mathf.Abs(rightX - leftX);
        float extraMargin = spanWidth * 0.5f + 5f;

        void ScoreBar(Point nodeA, Point nodeB,
                      DistanceJoint2D jointA, DistanceJoint2D jointB,
                      BridgeGene geneRef)
        {
            if (nodeA == null || nodeB == null) return;

            totalBars++;

            bool isConnected = (jointA != null && jointB != null);
            if (isConnected) connectedBars++;
            else floatingBars++;

            if (geneRef != null && geneRef.broken)
                brokenBars++;

            bool anchored = (nodeA.isAnchored || nodeB.isAnchored);
            if (anchored) anchoredBars++;

            Vector2 a = nodeA.rb.position;
            Vector2 b = nodeB.rb.position;
            Vector2 mid = (a + b) * 0.5f;

            bool crossesSpan =
                (a.x < spanMid && b.x > spanMid) ||
                (a.x > spanMid && b.x < spanMid);

            if (crossesSpan && mid.x > leftX && mid.x < rightX)
                crossSpanBars++;

            if (mid.y < 0f)
                sagPenalty += Mathf.Min(-mid.y * 0.8f, 6f);

            float dy = Mathf.Abs(a.y - b.y);
            if (dy > 1.5f)
                steepPenalty += Mathf.Min((dy - 1.5f) * 1.5f, 6f);

            if (mid.x < leftX - extraMargin ||
                mid.x > rightX + extraMargin ||
                mid.y < -8f ||
                mid.y > 15f)
            {
                oobPenalty += 2.0f;
            }

            if (goal != null)
            {
                float spanDir = Mathf.Sign(goal.transform.position.x - a.x);
                float barDir  = Mathf.Sign(b.x - a.x);

                if (spanDir == barDir) fitness += 0.6f;
                else fitness -= 0.6f;
            }
        }

        foreach (var r in roads)
            ScoreBar(r.nodeA, r.nodeB, r.jointA, r.jointB, r.geneReference);

        foreach (var b in beams)
            ScoreBar(b.nodeA, b.nodeB, b.jointA, b.jointB, b.geneReference);

        if (totalBars > 0)
        {
            float barCount = totalBars;

            float connectedRatio = connectedBars / barCount;
            float anchoredRatio  = anchoredBars / barCount;
            float spanRatio      = crossSpanBars / barCount;

            float connectivityScore = connectedRatio * 40f;
            float anchorScore       = anchoredRatio * 25f;
            float spanScore         = spanRatio * 40f;

            float densityNorm = Mathf.Clamp01(totalBars / targetBarCount);
            float densityScore = densityNorm * 15f;

            fitness += connectivityScore + anchorScore + spanScore + densityScore;

            fitness += connectedBars * 0.7f;
            fitness += anchoredBars  * 1.4f;

            if (crossSpanBars >= 1)
                fitness += 12f;
            if (crossSpanBars >= 3)
                fitness += 20f;
        }

        float penalty = 0f;

        penalty += brokenBars   * 8f;
        penalty += floatingBars * 1.5f;

        penalty += sagPenalty + steepPenalty + oobPenalty;

        penalty = Mathf.Clamp(penalty, 0f, 220f);

        fitness -= penalty;


        return fitness;
    }
}
