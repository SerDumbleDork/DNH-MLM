using UnityEngine;

public static class BridgeObservation
{
    public static float[] Build(
        Point[] anchors,
        Point lastEnd,
        int barIndex,
        int totalBars,
        float carProgress01,
        float brokenBarsNorm,
        bool prevBarSuccess,
        Goal goal,
        BarCreator bc
    )
    {
        float[] obs = new float[20];
        int k = 0;

        // ----- ANCHORS (6)
        obs[k++] = anchors[0].rb.position.x;
        obs[k++] = anchors[0].rb.position.y;
        obs[k++] = anchors[1].rb.position.x;
        obs[k++] = anchors[1].rb.position.y;
        obs[k++] = anchors[2].rb.position.x;
        obs[k++] = anchors[2].rb.position.y;

        // ----- SPAN (2)
        float leftX  = bc.leftBound.position.x;
        float rightX = bc.rightBound.position.x;

        float spanWidth = rightX - leftX;
        float spanMid   = (leftX + rightX) * 0.5f;

        obs[k++] = spanWidth;
        obs[k++] = spanMid;

        // ----- GOAL X (1)
        obs[k++] = goal.transform.position.x;

        // ----- LAST BAR END (2)
        obs[k++] = lastEnd.rb.position.x;
        obs[k++] = lastEnd.rb.position.y;

        // ----- PROGRESS (1)
        obs[k++] = (float)barIndex / Mathf.Max(1, totalBars);

        // ----- CAR PROGRESS (1)
        obs[k++] = carProgress01;

        // ----- STRUCTURE HEALTH (1)
        obs[k++] = brokenBarsNorm;

        // ----- PREV BAR SUCCESS (1)
        obs[k++] = prevBarSuccess ? 1f : 0f;

        // ----- EXTRAS FOR STABILITY (5)
        obs[k++] = leftX;
        obs[k++] = rightX;
        obs[k++] = Mathf.Clamp01((lastEnd.rb.position.y + 2f) / 8f);
        obs[k++] = 1f;      
        obs[k++] = goal.transform.position.y;

        return obs;
    }
}
