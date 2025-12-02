using UnityEngine;
using System.Collections.Generic;

public class BarCreator : MonoBehaviour
{
    public GameObject nodePrefab;
    public GameObject roadPrefab;
    public GameObject beamPrefab;

    public float snapRadius = 0.8f;
    public float maxLength = 5f;

    private List<Point> nodes = new List<Point>();
    private Point startNode;
    private bool placing;

    [HideInInspector] public enum Mode { Road, Beam }
    [HideInInspector] public Mode mode = Mode.Road;

    private GameObject previewObj;
    private SpriteRenderer previewSprite;

    public bool aiControl = false;
    public Vector2 aiMousePos = Vector2.zero;

    public PBTBridgeAIController ai;

    Vector2 GetMouseWorld()
    {
        if (aiControl)
            return aiMousePos;

        return Camera.main.ScreenToWorldPoint(Input.mousePosition);
    }


    void Start()
    {
        aiControl = false;

        foreach (var p in FindObjectsOfType<Point>())
            nodes.Add(p);

            ai.StartCoroutine(ai.RunPBTLoop());
    }

    void Update()
    {
        /*
        // Can't build during simulation
        if (FindObjectOfType<Sim>()?.simulationRunning == true)
            return;

        // Toggle Road/Beam
        if (Input.GetKeyDown(KeyCode.E))
            mode = (mode == Mode.Road) ? Mode.Beam : Mode.Road;

        // Left click = place
        if (Input.GetMouseButtonDown(0))
            LeftClick();

        // Right click = cancel / undo
        if (Input.GetMouseButtonDown(1))
        {
            if (placing)
            {
                EndPreview();
                placing = false;
                return;
            }
            UndoLast();
        }
        */

        // Preview update
        if (placing)
            UpdatePreview();
    }

    public Point GetNearestNode(Vector2 pos, float maxDist = 999f)
    {
        Point best = null;
        float bestDist = maxDist;

        foreach (var n in nodes)
        {
            float d = Vector2.Distance(n.rb.position, pos);
            if (d < bestDist)
            {
                bestDist = d;
                best = n;
            }
        }
        return best;
    }

    public MonoBehaviour LeftClick()
    {
        Vector2 mouse = GetMouseWorld();
        Vector2 snapped = Snap(mouse, out Point snappedNode);

        if (!placing)
        {
            startNode = snappedNode ?? CreateNode(snapped);
            BeginPreview(startNode.transform.position);
            placing = true;
            return null;
        }

        Vector2 s = startNode.transform.position;
        Vector2 dir = (snapped - s).normalized;
        float len = Mathf.Min(Vector2.Distance(snapped, s), maxLength);
        Vector2 endPos = s + dir * len;

        Point endNode = snappedNode ?? CreateNode(endPos);

        MonoBehaviour createdBar = CreateConnection(startNode, endNode);

        EndPreview();
        placing = false;

        return createdBar;
    }

    MonoBehaviour CreateConnection(Point a, Point b)
    {
        float dist = Vector2.Distance(a.rb.position, b.rb.position);

        var j1 = a.gameObject.AddComponent<DistanceJoint2D>();
        j1.connectedBody = b.rb;
        j1.autoConfigureDistance = false;
        j1.distance = dist;

        var j2 = b.gameObject.AddComponent<DistanceJoint2D>();
        j2.connectedBody = a.rb;
        j2.autoConfigureDistance = false;
        j2.distance = dist;

        if (mode == Mode.Road)
        {
            var go = Instantiate(roadPrefab);
            var bar = go.GetComponent<RoadBar>();
            bar.nodeA = a;
            bar.nodeB = b;
            return bar;
        }
        else
        {
            var go = Instantiate(beamPrefab);
            var bar = go.GetComponent<BeamBar>();
            bar.nodeA = a;
            bar.nodeB = b;
            return bar;
        }
    }

    void BeginPreview(Vector2 start)
    {
        if (previewObj != null)
            Destroy(previewObj);

        previewObj = new GameObject("PreviewBar");
        previewSprite = previewObj.AddComponent<SpriteRenderer>();
        previewSprite.color = new Color(1f, 1f, 1f, 0.35f);

        Sprite sprite =
            (mode == Mode.Road)
            ? roadPrefab.GetComponent<SpriteRenderer>().sprite
            : beamPrefab.GetComponent<SpriteRenderer>().sprite;

        previewSprite.sprite = sprite;
    }

    void UpdatePreview()
    {
        if (!previewObj || !startNode) return;

        Vector2 mouse = GetMouseWorld();
        Vector2 s = startNode.transform.position;

        Vector2 dir = mouse - s;
        float len = Mathf.Min(dir.magnitude, maxLength);
        dir.Normalize();

        previewObj.transform.position = s + dir * len * 0.5f;
        previewObj.transform.rotation = Quaternion.FromToRotation(Vector3.right, dir);
        previewObj.transform.localScale = new Vector3(len, 1f, 1f);
    }


    void EndPreview()
    {
        if (previewObj != null)
            Destroy(previewObj);

        previewObj = null;
    }

    Point CreateNode(Vector2 pos)
    {
        foreach (var p in nodes)
            if (p && Vector2.Distance(p.transform.position, pos) < 0.05f)
                return p;

        GameObject obj = Instantiate(nodePrefab, pos, Quaternion.identity);
        Point pt = obj.GetComponent<Point>();
        nodes.Add(pt);

        return pt;
    }


    Vector2 Snap(Vector2 raw, out Point snapped)
    {
        snapped = null;
        float best = snapRadius;

        foreach (var p in nodes)
        {
            if (p == null) continue;

            float d = Vector2.Distance(raw, p.transform.position);
            if (d <= best)
            {
                best = d;
                snapped = p;
            }
        }

        return snapped ? snapped.transform.position : raw;
    }

    void UndoLast()
    {
        nodes.RemoveAll(n => n == null);

        if (nodes.Count == 0)
            return;

        Point nodeToRemove = nodes[nodes.Count - 1];

        // Skip starter nodes
        if (nodeToRemove.CompareTag("Starter Node"))
            return;

        nodes.RemoveAt(nodes.Count - 1);

        // Destroy all visuals connected to this node
        foreach (var bar in FindObjectsOfType<RoadBar>())
        {
            if (bar.nodeA == nodeToRemove || bar.nodeB == nodeToRemove)
                Destroy(bar.gameObject);
        }

        foreach (var bar in FindObjectsOfType<BeamBar>())
        {
            if (bar.nodeA == nodeToRemove || bar.nodeB == nodeToRemove)
                Destroy(bar.gameObject);
        }

        // Destroy node-based joints
        foreach (var j in nodeToRemove.GetComponents<DistanceJoint2D>())
            Destroy(j);

        // Destroy matching joints on neighboring nodes
        foreach (var neighbor in nodes)
        {
            foreach (var j in neighbor.GetComponents<DistanceJoint2D>())
            {
                if (j.connectedBody == nodeToRemove.rb)
                    Destroy(j);
            }
        }
        // Finally remove node
        Destroy(nodeToRemove.gameObject);
    }

    public void CleanNodes()
    {
        nodes.RemoveAll(n => n == null);
    }

}
