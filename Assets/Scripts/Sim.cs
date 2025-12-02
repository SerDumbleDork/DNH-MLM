using UnityEngine;
using System.Collections;

public class Sim : MonoBehaviour
{
    public Goal goal;

    public bool simulationRunning = false;
    public GameObject carPrefab;
    public bool hideUI = false;
    private GameObject carInstance;
    private Vector3 carStartPos = new Vector3(-20f, 5f, 0f);

    void Start()
    {
        simulationRunning = true;

        SpawnCar();
        SetSimulationState(true);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            simulationRunning = !simulationRunning;
            SetSimulationState(simulationRunning);
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            StopAllCoroutines();
            ResetEverything();
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            Application.Quit();
        }
    }

    public void SetSimulationState(bool runPhysics)
    {
        foreach (Point p in FindObjectsOfType<Point>())
        {
            if (p == null || p.rb == null) continue;

            Rigidbody2D rb = p.rb;

            if (p.isAnchored)
            {
                rb.bodyType = RigidbodyType2D.Static;
                rb.gravityScale = 0f;
            }
            else
            {
                if (runPhysics)
                {
                    // Free nodes in sim
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.gravityScale = 1f;
                }
                else
                {
                    // Locked while building
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    rb.gravityScale = 0f;
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
            }
        }

        foreach (RoadBar road in FindObjectsOfType<RoadBar>())
        {
            if (road == null) continue;

            Rigidbody2D rb = road.GetComponent<Rigidbody2D>();
            if (rb == null) continue;

            if (runPhysics)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.simulated = true;
                rb.isKinematic = false;
                rb.gravityScale = 1f;
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.simulated = true;
                rb.isKinematic = true;
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        foreach (Rigidbody2D rb in FindObjectsOfType<Rigidbody2D>())
        {
            if (rb == null) continue;

            if (rb.CompareTag("Terrain"))              continue;
            if (rb.GetComponent<Point>() != null)      continue;
            if (rb.GetComponent<RoadBar>() != null)    continue;
            if (rb.GetComponent<BeamBar>() != null)    continue;

            if (runPhysics)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.simulated = true;
                rb.isKinematic = false;
                rb.gravityScale = 1f;
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.gravityScale = 0f;
                rb.isKinematic = true;
                rb.simulated = false;
            }
        }

        Debug.Log(runPhysics ? "Simulation started" : "Simulation stopped");
    }

    public void ResetEverything()
    {
        StopAllCoroutines();

        goal.endReached = false;

        foreach (var r in FindObjectsOfType<RoadBar>())
            if (r != null) Destroy(r.gameObject);

        foreach (var b in FindObjectsOfType<BeamBar>())
            if (b != null) Destroy(b.gameObject);

        foreach (Point node in FindObjectsOfType<Point>())
        {
            if (node != null && node.destructable && !node.isAnchored)
                Destroy(node.gameObject);
        }

        if (carInstance != null)
            Destroy(carInstance);

        SpawnCar();

        simulationRunning = false;
        SetSimulationState(false);

        foreach (var builder in FindObjectsOfType<BarCreator>())
            builder.CleanNodes();

        Debug.Log("Bridge and car reset.");
    }

    private void SpawnCar()
    {
        if (carPrefab != null)
            carInstance = Instantiate(carPrefab, carStartPos, Quaternion.identity);
    }

    void OnGUI()
    {
        if (hideUI) return;

        string modeText = simulationRunning ? "SIMULATION" : "BUILD";

        BarCreator builder = FindObjectOfType<BarCreator>();
        string buildModeText = "";

        if (!simulationRunning && builder != null)
            buildModeText = $"   |   Build Type: {builder.mode}";

        string text;

        if (simulationRunning)
        {
            text =
                $"Mode: {modeText}\n" +
                "(Space = Stop,  R = Reset,  Q = Quit)";
        }
        else
        {
            text =
                $"Mode: {modeText}{buildModeText}\n" +
                "Left Click = Place\n" +
                "Right Click = Cancel\n" +
                "E = Toggle Road/Beam\n" +
                "Space = Simulate\n" +
                "R = Reset\n" +
                "Q = Quit";
        }

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;

        GUI.Label(new Rect(10, 10, 600, 200), text, style);
    }
}
