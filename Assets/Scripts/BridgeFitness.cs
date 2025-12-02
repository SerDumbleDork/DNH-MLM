using UnityEngine;
using System.Collections.Generic;

public static class BridgeFitness
{
    public static float Evaluate(Car[] cars, BridgeGene[] genes, Goal goal, BarCreator barCreator)
    {
        float fitness = 0f;

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
        else
        {
            distNorm = Mathf.Clamp01(maxDistance / 20f); 
        }

        fitness += distNorm * 150f;

        if (goal != null && goal.endReached)
            fitness += 100f;

        int connectedBars     = 0;
        int floatingBars      = 0;
        int brokenBars        = 0;
        int anchorConnections = 0;

        float totalSagPenalty = 0f;
        float totalSlopePenalty = 0f;

        var roadBars = Object.FindObjectsOfType<RoadBar>();
        var beamBars = Object.FindObjectsOfType<BeamBar>();

        foreach (var r in roadBars)
        {
            if (r == null || r.nodeA == null || r.nodeB == null) continue;

            bool connected = (r.jointA != null && r.jointB != null);
            if (connected)
                connectedBars++;
            else
                floatingBars++;

            if (r.geneReference != null && r.geneReference.broken)
                brokenBars++;

            Vector2 a = r.nodeA.rb.position;
            Vector2 b = r.nodeB.rb.position;
            Vector2 mid = (a + b) * 0.5f;

            if (mid.y < 0f)
                totalSagPenalty += (-mid.y) * 8f; 

            float slope = Mathf.Abs(a.y - b.y);
            totalSlopePenalty += slope * 4f;

            if (IsAnchor(r.jointA) || IsAnchor(r.jointB))
                anchorConnections++;
        }

        foreach (var beam in beamBars)
        {
            if (beam == null || beam.nodeA == null || beam.nodeB == null) continue;

            bool connected = (beam.jointA != null && beam.jointB != null);
            if (connected)
                connectedBars++;
            else
                floatingBars++;

            if (beam.geneReference != null && beam.geneReference.broken)
                brokenBars++;

            Vector2 a = beam.nodeA.rb.position;
            Vector2 b = beam.nodeB.rb.position;
            Vector2 mid = (a + b) * 0.5f;

            if (mid.y < 0f)
                totalSagPenalty += (-mid.y) * 3f;

            float slope = Mathf.Abs(a.y - b.y);
            totalSlopePenalty += slope * 1.5f;

            if (IsAnchor(beam.jointA) || IsAnchor(beam.jointB))
                anchorConnections++;
        }

        fitness -= totalSagPenalty;
        fitness -= totalSlopePenalty;

        fitness -= brokenBars * 6f;
        fitness += connectedBars * 1.5f;
        fitness -= floatingBars * 1f;

        fitness += anchorConnections * 4f;

        return fitness;
    }

    static bool IsAnchor(DistanceJoint2D j)
    {
        if (j == null) return false;
        var p = j.connectedBody?.GetComponent<Point>();
        return p != null && p.isAnchored;
    }
}
