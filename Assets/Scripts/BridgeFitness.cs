using UnityEngine;
using System.Collections.Generic;

public static class BridgeFitness
{
    public static float Evaluate(Car[] cars, BridgeGene[] genes, Goal goal, BarCreator barCreator)
    {
        float fitness = 0f;

        // ============================================================
        // 1. CAR DISTANCE REWARD
        // ============================================================
        float maxDistance = 0f;
        float startX = 0f;

        if (cars != null)
        {
            foreach (var c in cars)
            {
                if (c == null) continue;
                maxDistance = Mathf.Max(maxDistance, c.DistanceTravelled);
                startX = c.startX;
            }
        }

        float distNorm = 0f;
        if (goal != null)
        {
            float targetDist = goal.transform.position.x - startX;
            if (targetDist > 0.1f)
                distNorm = Mathf.Clamp01(maxDistance / targetDist);
        }

        fitness += distNorm * 200f;     // Strong signal


        // ============================================================
        // 2. GOAL REACHED BONUS
        // ============================================================
        if (goal != null && goal.endReached)
            fitness += 150f;


        // ============================================================
        // 3. STRUCTURE SCORING
        // ============================================================
        int connected = 0;
        int floating = 0;
        int broken = 0;
        int anchorsUsed = 0;

        float sagPenalty = 0f;
        float slopePenalty = 0f;
        float oobPenalty = 0f;

        RoadBar[] roads = Object.FindObjectsOfType<RoadBar>();
        BeamBar[] beams = Object.FindObjectsOfType<BeamBar>();


        // ============================================================
        // Helper: Score a single bar (RoadBar or BeamBar)
        // ============================================================
        void ScoreBar(Point nodeA, Point nodeB, DistanceJoint2D jointA, DistanceJoint2D jointB, BridgeGene geneRef, float sagScale, float slopeScale)
        {
            if (nodeA == null || nodeB == null) return;

            bool isConnected = (jointA != null && jointB != null);

            if (isConnected) connected++;
            else floating++;

            if (geneRef != null && geneRef.broken)
                broken++;

            Vector2 a = nodeA.rb.position;
            Vector2 b = nodeB.rb.position;

            Vector2 mid = (a + b) * 0.5f;

            // ========= SAG (clamped) =========
            if (mid.y < 0f)
                sagPenalty += Mathf.Min((-mid.y) * sagScale, 20f);

            // ========= SLOPE (clamped) =========
            float slope = Mathf.Abs(a.y - b.y);
            slopePenalty += Mathf.Min(slope * slopeScale, 15f);

            // ========= OUT OF BOUNDS =========
            if (Mathf.Abs(mid.x) > 25f || Mathf.Abs(mid.y) > 12f)
                oobPenalty += 10f;

            // ========= ANCHORS =========
            if (IsAnchor(jointA) || IsAnchor(jointB))
                anchorsUsed++;
        }


        // Score RoadBars
        foreach (var r in roads)
        {
            ScoreBar(
                r.nodeA, r.nodeB,
                r.jointA, r.jointB,
                r.geneReference,
                sagScale: 6f,
                slopeScale: 4f
            );
        }

        // Score BeamBars
        foreach (var b in beams)
        {
            ScoreBar(
                b.nodeA, b.nodeB,
                b.jointA, b.jointB,
                b.geneReference,
                sagScale: 3f,
                slopeScale: 2f
            );
        }


        // ============================================================
        // 4. APPLY STRUCTURE BONUSES / PENALTIES
        // ============================================================
        fitness += connected * 2f;
        fitness -= floating * 1f;
        fitness -= broken * 5f;

        fitness += anchorsUsed * 5f;

        // Clamp global penalty so learning is stable
        float totalPenalty = sagPenalty + slopePenalty + oobPenalty;
        totalPenalty = Mathf.Clamp(totalPenalty, 0f, 400f);

        fitness -= totalPenalty;


        // ============================================================
        // 5. RETURN RAW FITNESS (VERY IMPORTANT!)
        // ============================================================
        return fitness;
    }


    static bool IsAnchor(DistanceJoint2D j)
    {
        if (j == null) return false;

        var p = j.connectedBody?.GetComponent<Point>();
        return p != null && p.isAnchored;
    }
}
