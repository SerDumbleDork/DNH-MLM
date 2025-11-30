using UnityEngine;
using System.Collections.Generic;

public static class BridgeFitness
{
    public static float Evaluate(Car[] cars, BridgeGene[] genes, Goal goal, BarCreator barCreator)
    {
        float fitness = 0f;

        if (goal != null && goal.endReached)
            fitness += 200f;

        float distance = 0f;
        if (cars != null)
        {
            foreach (var c in cars)
            {
                if (c == null) continue;
                distance = Mathf.Max(distance, c.DistanceTravelled);
            }
        }
        fitness += distance;

        int connectedBars = 0;
        int floatingBars = 0;
        int brokenBars = 0;
        int anchorConnections = 0;
        int belowYCount = 0;

        List<MonoBehaviour> bars = new List<MonoBehaviour>();
        bars.AddRange(Object.FindObjectsOfType<RoadBar>());
        bars.AddRange(Object.FindObjectsOfType<BeamBar>());

        foreach (var b in bars)
        {
            DistanceJoint2D start = null;
            DistanceJoint2D end = null;
            Point nodeA = null;
            Point nodeB = null;

            if (b is RoadBar r)
            {
                start = r.jointA;
                end = r.jointB;
                nodeA = r.nodeA;
                nodeB = r.nodeB;
                if (r.geneReference != null && r.geneReference.broken)
                    brokenBars++;
            }

            if (b is BeamBar beam)
            {
                start = beam.jointA;
                end = beam.jointB;
                nodeA = beam.nodeA;
                nodeB = beam.nodeB;
                if (beam.geneReference != null && beam.geneReference.broken)
                    brokenBars++;
            }

            if (nodeA != null && nodeB != null)
            {
                Vector2 mid = (nodeA.rb.position + nodeB.rb.position) * 0.5f;
                if (mid.y < -5f)
                    belowYCount++;
            }

            bool connected = (start != null && end != null);
            if (connected)
                connectedBars++;
            else
                floatingBars++;

            if (connected)
            {
                if (IsJointOnAnchor(start) || IsJointOnAnchor(end))
                    anchorConnections++;
            }
        }

        fitness += connectedBars * 5f;
        fitness -= floatingBars * 3f;
        fitness += anchorConnections * 10f;
        if (anchorConnections == 0)
            fitness -= 50f;
        fitness -= brokenBars * 15f;
        fitness -= belowYCount * 20f;

        return fitness;
    }

    static bool IsJointOnAnchor(DistanceJoint2D j)
    {
        if (j == null) return false;
        Point p = j.connectedBody?.GetComponent<Point>();
        if (p == null) return false;
        return p.isAnchored;
    }
}