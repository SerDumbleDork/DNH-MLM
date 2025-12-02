using UnityEngine;
using System.Collections;

public class PBTBridgeAIController : MonoBehaviour
{
    [Header("References")]
    public BarCreator bc;
    public Sim sim;
    public BridgePBTManager pbtManager;
    public Goal goal;
    private float lastEvaluatedFitness = 0f;

    [Header("Timing")]
    public float simulationTime = 5f;

    [Header("Performance")]
    public float aiTimeScale = 3f;
    public int   aiTargetFrameRate = 60;

    [Header("Control")]
    public KeyCode toggleKey = KeyCode.F;

    bool pbtRunning = false;

    float prevTimeScale = 1f;
    float prevFixedDeltaTime = 0.02f;
    int   prevTargetFrameRate;
    int   prevVSyncCount;

    void Start()
    {
        if (bc != null)
            bc.aiControl = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (!pbtRunning)
                StartCoroutine(RunPBTLoop());
            else
            {
                StopAllCoroutines();
                ExitPBTMode();
            }
        }
    }

    public IEnumerator RunPBTLoop()
    {
        if (bc == null || sim == null || pbtManager == null)
        {
            Debug.LogError("PBTBridgeAIController: missing references (bc, sim, or pbtManager).");
            yield break;
        }

        pbtRunning = true;
        bc.aiControl = true;
        sim.hideUI = true;

        prevTimeScale      = Time.timeScale;
        prevFixedDeltaTime = Time.fixedDeltaTime;
        prevTargetFrameRate = Application.targetFrameRate;
        prevVSyncCount      = QualitySettings.vSyncCount;

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = aiTargetFrameRate;
        Time.timeScale   = aiTimeScale;
        Time.fixedDeltaTime = prevFixedDeltaTime / aiTimeScale;

        Debug.Log("PBT: started training loop.");

        while (pbtRunning)
        {
            BridgePBTModel model = pbtManager.CurrentModel;
            if (model == null)
            {
                Debug.LogWarning("PBTBridgeAIController: no current model, stopping.");
                break;
            }

            yield return RunSingleModelEpisode(model);

            pbtManager.AdvanceToNextModel();
        }

        ExitPBTMode();
    }

    IEnumerator RunSingleModelEpisode(BridgePBTModel model)
    {
        sim.ResetEverything();

        bc.CleanNodes();
        bc.aiControl = true;

        yield return null;

        Point[] anchors = pbtManager.anchorPoints;
        BridgeGene[] genes = model.GenerateBridge(anchors);

        if (genes != null)
        {
            for (int i = 0; i < genes.Length; i++)
            {
                AIClick(genes[i]);
                yield return null;
            }
        }

        sim.simulationRunning = true;
        sim.SetSimulationState(true);

        float t = 0f;
        while (t < simulationTime)
        {
            t += Time.deltaTime;
            yield return null;
        }

        sim.simulationRunning = false;
        sim.SetSimulationState(false);

        Car[] cars = FindObjectsOfType<Car>();
        float fitness = BridgeFitness.Evaluate(cars, genes, goal, bc);

        model.Train(anchors, genes, fitness);

        lastEvaluatedFitness = fitness;

        StartCoroutine(GoogleSheetsLogger.LogEpisode(
            pbtManager.generationIndex,
            pbtManager.currentModelIndex,
            fitness,
            model.learningRate,
            model.explorationNoise,
            genes
        ));

        Debug.Log($"PBT: Model fitness = {fitness:F2}");
    }

    void AIClick(BridgeGene gene)
    {
        if (bc == null) return;

        bc.aiMousePos = gene.start;
        bc.mode = (gene.type == AIController.BarType.Road)
            ? BarCreator.Mode.Road
            : BarCreator.Mode.Beam;
        bc.LeftClick();

        bc.aiMousePos = gene.end;
        MonoBehaviour bar = bc.LeftClick();

        if (bar != null)
        {
            RoadBar road = bar as RoadBar;
            if (road != null) road.geneReference = gene;

            BeamBar beam = bar as BeamBar;
            if (beam != null) beam.geneReference = gene;
        }
    }

    void ExitPBTMode()
    {
        if (!pbtRunning) return;

        pbtRunning = false;

        if (sim != null)
        {
            sim.simulationRunning = false;
            sim.SetSimulationState(false);
            sim.hideUI = false;
        }

        if (bc != null)
            bc.aiControl = false;

        Time.timeScale      = prevTimeScale;
        Time.fixedDeltaTime = prevFixedDeltaTime;
        Application.targetFrameRate = prevTargetFrameRate;
        QualitySettings.vSyncCount  = prevVSyncCount;

        Debug.Log("PBT: stopped training loop.");
    }

    void OnGUI()
    {
        if (!pbtRunning || pbtManager == null) return;

        var model = pbtManager.CurrentModel;
        if (model == null) return;

        string text =
            "PBT MODE RUNNING\n" +
            $"Model index: {pbtManager.currentModelIndex + 1} / {pbtManager.populationSize}\n" +
            $"Brains: {model.learningRate.ToString("F6")}  Mutation: {model.explorationNoise:F2}%";

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 18;

        GUI.Label(new Rect(10, 50, 420, 120), text, style);

        GUIStyle style2 = new GUIStyle(GUI.skin.label);

        style2.fontSize = 18;
        style2.normal.textColor = Color.orange;

        Rect boxRect = new Rect(450, 0, 180, 50);

        GUI.Box(boxRect, GUIContent.none);

        string genText = "Generation: "+ pbtManager.generationIndex.ToString();
        GUI.Label(new Rect(boxRect.x + 10, boxRect.y + 10, boxRect.width, boxRect.height),genText, style2);

        float shownFitness = Mathf.Clamp(lastEvaluatedFitness, -500f, 500f);
        string fitnessText = $"Last Fitness: {shownFitness:F2}";
        Rect boxRect2 = new Rect(0, 0, 250, 50);
        GUIStyle style3 = new GUIStyle(GUI.skin.label);
        style3.fontSize = 18;
        style3.normal.textColor = Color.red;
        GUI.Box(boxRect2 , GUIContent.none);
        GUI.Label(new Rect(boxRect2.x + 10, boxRect2.y + 10, boxRect2.width, boxRect2.height), fitnessText, style3);
    }
}
