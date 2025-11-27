using UnityEngine;
using System.Collections;

public class Sim : MonoBehaviour
{
    public bool simulationRunning = false;
    public GameObject carPrefab;
    public bool hideUI = false;
    private GameObject carInstance;
    private Vector3 carStartPos = new Vector3(-20f, 5f, 0f);

    void Start()
    {
        SpawnCar();
        SetSimulationState(false);
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

    /// <summary>
    /// Toggle between BUILD (no physics on bridge) and SIMULATION (full physics).
    /// </summary>
    public void SetSimulationState(bool runPhysics)
    {
        // 1. NODES (Points)
        foreach (Point p in FindObjectsOfType<Point>())
        {
            if (p == null || p.rb == null) continue;

            Rigidbody2D rb = p.rb;

            if (p.isAnchored)
            {
                // Anchors never move
                rb.bodyType = RigidbodyType2D.Static;
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
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

        // 2. ROAD PLANKS
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
                // Hold roads still in build mode
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.simulated = true;
                rb.isKinematic = true;
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        // 3. CAR (and any other loose rigidbodies)
        foreach (Rigidbody2D rb in FindObjectsOfType<Rigidbody2D>())
        {
            if (rb == null) continue;

            // Skip things we already handled
            if (rb.CompareTag("Terrain"))              continue;
            if (rb.GetComponent<Point>() != null)      continue;
            if (rb.GetComponent<RoadBar>() != null)    continue;
            if (rb.GetComponent<BeamBar>() != null)    continue;

            // Assume everything else is the car or similar dynamic object
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

    /// <summary>
    /// Nuke bridge + car and go back to build mode.
    /// </summary>
    public void ResetEverything()
    {
        StopAllCoroutines();

        // Destroy RoadBars and BeamBars
        foreach (var r in FindObjectsOfType<RoadBar>())
            if (r != null) Destroy(r.gameObject);

        foreach (var b in FindObjectsOfType<BeamBar>())
            if (b != null) Destroy(b.gameObject);

        // Destroy nodes (except non-destructible starter anchors)
        foreach (Point node in FindObjectsOfType<Point>())
        {
            if (node != null && node.destructable)
                Destroy(node.gameObject);
        }

        // Reset car
        if (carInstance != null)
            Destroy(carInstance);

        SpawnCar();

        simulationRunning = false;
        SetSimulationState(false);

        // Clean BarCreator node list to remove destroyed references
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
