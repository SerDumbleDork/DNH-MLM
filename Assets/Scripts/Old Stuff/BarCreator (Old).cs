/*
using UnityEngine;
using System.Collections.Generic;

public class BarCreator : MonoBehaviour
{
    public GameObject nodePrefab;
    public GameObject roadPrefab;
    public GameObject beamPrefab;
    
    public float snapRadius = 0.4f;
    public Vector2 snapOffset = Vector2.zero;
    public float maxBarLength = 5f;

    private List<Point> allPoints = new List<Point>();
    private Point startPoint;
    private bool placing;

    private enum BuildMode { Road, Beam }
    private BuildMode currentMode = BuildMode.Road;

    private Road previewRoad;
    private Beam previewBeam;

    void Start()
    {
        foreach (Point p in FindObjectsOfType<Point>())
            if (p != null && !allPoints.Contains(p))
                allPoints.Add(p);
    }

    void Update()
    {
        if (FindObjectOfType<Sim>()?.simulationRunning == true)
            return;

        if (Input.GetKeyDown(KeyCode.E))
            currentMode = (currentMode == BuildMode.Road) ? BuildMode.Beam : BuildMode.Road;

        if (Input.GetMouseButtonDown(0))
            HandleLeftClick();

        if (Input.GetMouseButtonDown(1))
            HandleRightClick();

        if (placing)
            UpdatePreview();
    }

    //──────────────────────────────────────────────────────────────
    // LEFT CLICK LOGIC
    void HandleLeftClick()
    {
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 snapped = GetSnappedPosition(mouseWorld, out Point existingNode);

        if (!placing)
        {
            startPoint = existingNode ?? CreateNode(snapped);

            GameObject prefab = (currentMode == BuildMode.Road) ? roadPrefab : beamPrefab;
            if (prefab == null) return;

            GameObject obj = Instantiate(prefab);

            if (currentMode == BuildMode.Road)
            {
                previewRoad = obj.GetComponent<Road>();
                previewRoad.startPoint = startPoint;
                previewRoad.SetPreviewMode(true);
            }
            else
            {
                previewBeam = obj.GetComponent<Beam>();
                previewBeam.startPoint = startPoint;
            }

            placing = true;
        }
        else
        {
            //──────────── MAX LENGTH CLAMP ON FINAL PLACEMENT ────────────
            Vector2 start = startPoint.transform.position;
            Vector2 dir = (snapped - start).normalized;
            float dist = Mathf.Min(Vector2.Distance(snapped, start), maxBarLength);
            Vector2 clampedEnd = start + dir * dist;

            Point endPoint = existingNode ?? CreateNode(clampedEnd);

            if (currentMode == BuildMode.Road)
            {
                if (previewRoad == null) { placing = false; return; }

                previewRoad.endPoint = endPoint;
                previewRoad.UpdateBar(start, endPoint.transform.position); // final visual update
                previewRoad.FinalizeBar();
                previewRoad = null;
            }
            else
            {
                if (previewBeam == null) { placing = false; return; }

                previewBeam.endPoint = endPoint;
                previewBeam.UpdateBar(start, endPoint.transform.position);
                previewBeam.FinalizeBar();
                previewBeam = null;
            }

            placing = false;
        }
    }

    //──────────────────────────────────────────────────────────────
    // PREVIEW UPDATE
    void UpdatePreview()
    {
        if (startPoint == null)
        {
            placing = false;
            return;
        }

        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 snapped = GetSnappedPosition(mouseWorld, out _);

        Vector2 start = startPoint.transform.position;
        Vector2 dir = (snapped - start).normalized;
        float dist = Mathf.Min(Vector2.Distance(snapped, start), maxBarLength);
        Vector2 end = start + dir * dist;

        if (currentMode == BuildMode.Road && previewRoad != null)
            previewRoad.UpdateBar(start, end);

        if (currentMode == BuildMode.Beam && previewBeam != null)
            previewBeam.UpdateBar(start, end);
    }

    //──────────────────────────────────────────────────────────────
    void HandleRightClick()
    {
        if (placing)
        {
            if (previewRoad != null) Destroy(previewRoad.gameObject);
            if (previewBeam != null) Destroy(previewBeam.gameObject);

            previewRoad = null;
            previewBeam = null;
            placing = false;
            return;
        }

        DeleteMostRecentNode();
    }

    //──────────────────────────────────────────────────────────────
    private Point CreateNode(Vector2 pos)
    {
        allPoints.RemoveAll(p => p == null);

        foreach (var p in allPoints)
            if (Vector2.Distance(p.transform.position, pos) < 0.05f)
                return p;

        GameObject node = Instantiate(nodePrefab, pos, Quaternion.identity);
        Point newPoint = node.GetComponent<Point>();
        allPoints.Add(newPoint);
        return newPoint;
    }

    private Vector2 GetSnappedPosition(Vector2 rawPos, out Point snappedNode)
    {
        snappedNode = null;
        allPoints.RemoveAll(p => p == null);

        foreach (var p in allPoints)
        {
            if (!p) continue;
            if (Vector2.Distance(rawPos, p.transform.position) <= snapRadius)
            {
                snappedNode = p;
                return p.transform.position;
            }
        }

        return new Vector2(Mathf.Round(rawPos.x), Mathf.Round(rawPos.y)) + snapOffset;
    }

    private void DeleteMostRecentNode()
    {
        if (allPoints.Count == 0) return;

        Point nodeToRemove = allPoints[allPoints.Count - 1];
        allPoints.RemoveAt(allPoints.Count - 1);

        if (nodeToRemove.CompareTag("Starter Node"))
            return;


        foreach (Road r in FindObjectsOfType<Road>())
            if (r.startPoint == nodeToRemove || r.endPoint == nodeToRemove)
                Destroy(r.gameObject);

        foreach (Beam b in FindObjectsOfType<Beam>())
            if (b.startPoint == nodeToRemove || b.endPoint == nodeToRemove)
                Destroy(b.gameObject);

        Destroy(nodeToRemove.gameObject);
    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 300, 30), $"Piece: {currentMode} (Press E to switch)");
        GUI.Label(new Rect(10, 70, 500, 30), "Left Click to Place, Right Click to Undo");
    }

    void SetSimulationState(bool runPhysics)
    {
        Rigidbody2D[] allBodies = FindObjectsOfType<Rigidbody2D>();

        foreach (Rigidbody2D rb in allBodies)
        {
            if (rb == null) continue;

            if (rb.CompareTag("Terrain"))
                continue;

            Point point = rb.GetComponent<Point>();

            if (!runPhysics)
            {
                rb.gravityScale = 0;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0;

                rb.isKinematic = false;
                rb.simulated = true;
                rb.constraints = RigidbodyConstraints2D.FreezeAll;
                continue;
            }
            else
            {
                if (point != null && point.isAnchored)
                {
                    rb.isKinematic = true;
                    rb.simulated = true;
                    rb.constraints = RigidbodyConstraints2D.FreezeAll;
                    continue;
                }
                rb.isKinematic = false;
                rb.simulated = true;
                rb.gravityScale = 0.6f;
                rb.constraints = RigidbodyConstraints2D.None;
            }
        }

        Debug.Log(runPhysics ? "Simulation started" : "Simulation stopped");
    }
}
*/