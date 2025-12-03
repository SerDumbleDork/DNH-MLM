using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AIController : MonoBehaviour
{
    public BarCreator bc;
    public Point firstNode;
    public BridgeMLModel mlModel;

    public enum BarType { Road, Beam }

    [System.Serializable]
    public class BridgeAIIndividual
    {
        public BridgeGene[] genes;
        public float fitness;
    }

    public List<BridgeAIIndividual> population = new List<BridgeAIIndividual>();
    public int populationSize = 10;
    public int genesPerIndividual = 20;

    public int eliteCount = 2;
    public float mutationChance = 0.1f;
    public float mutationOffset = 0.5f;

    public float buildTime = 3f;
    public float simulationTime = 5f;
    public int maxGenerations = 10;

    public Vector2 geneMinPos = new Vector2(-15f, -5f);
    public Vector2 geneMaxPos = new Vector2(15f, 5f);

    public float aiTimeScale = 3f;
    public int aiTargetFrameRate = 30;
    public float gridStep = 0.5f;

    bool aiRunning = false;
    float prevTimeScale = 1f;
    float prevFixedDeltaTime = 0.02f;
    int prevTargetFrameRate;
    int prevVSyncCount;

    float bestFitnessOverall = float.NegativeInfinity;
    float bestFitnessThisGeneration = float.NegativeInfinity;
    int currentGeneration = -1;
    BridgeAIIndividual bestIndividualOverall;

    bool lastSimState = false;

    bool hasPlayerPrediction = false;
    float lastPlayerPrediction = 0f;
    float lastPlayerActualDistance = 0f;
    BridgeGene[] lastPlayerGenes;

    public float lastPrediction = 0f;
    public float lastActualDistance = 0f;
    public bool hasPrediction = false;
    BridgeGene[] lastEvaluatedGenes;

    public float targetDistanceForFitness = 25f;
    public float distanceWeight = 100f;
    public float spanWeight = 5f;
    public float brokenPenalty = 20f;
    public float successBonus = 500f;

    public bool AI = true;

    void Start()
    {
        // if (bc != null) bc.aiControl = true;
        StartCoroutine(RunGenerationLoop());

        if (population.Count == 0)
        {
            for (int i = 0; i < populationSize; i++)
            {
                var individual = CreateRandomIndividual(genesPerIndividual, geneMinPos, geneMaxPos);
                population.Add(individual);
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F) || AI == true)
        {
            if (!aiRunning)
                StartCoroutine(RunGenerationLoop());
            else
            {
                StopAllCoroutines();
                ExitAIMode();
            }
        }

        Sim sim = FindObjectOfType<Sim>();
        bool simRunning = (sim != null && sim.simulationRunning);

        if (!aiRunning && mlModel != null)
        {
            if (simRunning && !lastSimState)
            {
                lastPlayerGenes = BuildGenesFromScene();
                lastPlayerPrediction = mlModel.Predict(lastPlayerGenes);
                lastPlayerActualDistance = 0f;
                hasPlayerPrediction = true;
            }

            if (!simRunning && lastSimState && hasPlayerPrediction && lastPlayerGenes != null)
            {
                Car[] cars = FindObjectsOfType<Car>();
                float bestD = 0f;
                for (int i = 0; i < cars.Length; i++)
                {
                    if (cars[i] == null) continue;
                    float d = cars[i].DistanceTravelled;
                    if (d > bestD)
                        bestD = d;
                }

                lastPlayerActualDistance = Mathf.Max(0f, bestD);
                mlModel.AddSample(lastPlayerGenes, lastPlayerActualDistance);
            }
        }

        lastSimState = simRunning;
    }

    void AIClick(BridgeGene gene)
    {
        if (bc == null) return;

        Point startNode = bc.GetNearestOrCreateNode(gene.start);
        Point endNode   = bc.GetNearestOrCreateNode(gene.end);

        if (startNode == null) return;
        if (endNode == null) return;

        bc.aiMousePos = startNode.rb.position;
        bc.mode = (gene.type == AIController.BarType.Road)
            ? BarCreator.Mode.Road
            : BarCreator.Mode.Beam;
        bc.LeftClick();

        bc.aiMousePos = endNode.rb.position;
        MonoBehaviour bar = bc.LeftClick();

        if (bar != null)
        {
            RoadBar road = bar as RoadBar;
            if (road != null) road.geneReference = gene;

            BeamBar beam = bar as BeamBar;
            if (beam != null) beam.geneReference = gene;
        }
    }

    public IEnumerator RunGenerationLoop()
    {
        aiRunning = true;
        hasPlayerPrediction = false;

        if (bc != null)
            bc.aiControl = true;

        Sim sim = FindObjectOfType<Sim>();
        if (sim != null)
            sim.hideUI = true;

        prevTimeScale = Time.timeScale;
        prevFixedDeltaTime = Time.fixedDeltaTime;
        prevTargetFrameRate = Application.targetFrameRate;
        prevVSyncCount = QualitySettings.vSyncCount;

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = aiTargetFrameRate;
        Time.timeScale = aiTimeScale;
        Time.fixedDeltaTime = prevFixedDeltaTime / aiTimeScale;

        bestFitnessOverall = float.NegativeInfinity;
        bestIndividualOverall = null;

        int generation = 0;
        while (generation < maxGenerations && aiRunning)
        {
            currentGeneration = generation;
            bestFitnessThisGeneration = float.NegativeInfinity;

            foreach (var individual in population)
            {
                if (!aiRunning)
                    break;

                yield return RunIndividualSimulation(individual);

                if (individual.fitness > bestFitnessThisGeneration)
                    bestFitnessThisGeneration = individual.fitness;

                if (individual.fitness > bestFitnessOverall)
                {
                    bestFitnessOverall = individual.fitness;
                    bestIndividualOverall = individual;
                }
            }

            if (!aiRunning)
                break;

            if (mlModel != null && mlModel.SampleCount > 0)
                mlModel.Train(mlModel.epochsPerTrain);

            CreateNextGeneration(population, eliteCount, mutationChance, mutationOffset);
            generation++;
        }

        ExitAIMode();
    }

    IEnumerator RunIndividualSimulation(BridgeAIIndividual individual)
    {
        Sim sim = FindObjectOfType<Sim>();
        if (sim != null)
        {
            sim.simulationRunning = false;
            sim.SetSimulationState(false);
        }

        float buildTimer = 0f;
        foreach (var gene in individual.genes)
        {
            AIClick(gene);
            yield return null;
            buildTimer += Time.deltaTime;
            if (buildTimer >= buildTime) break;
        }

        while (buildTimer < buildTime)
        {
            buildTimer += Time.deltaTime;
            yield return null;
        }

        if (sim != null)
        {
            sim.simulationRunning = true;
            sim.SetSimulationState(true);
        }

        float simTimer = 0f;
        while (simTimer < simulationTime)
        {
            simTimer += Time.deltaTime;
            yield return null;
        }

        individual.fitness = EvaluateFitness(individual);

        if (sim != null)
        {
            sim.simulationRunning = false;
            sim.SetSimulationState(false);
            sim.ResetEverything();
        }
    }

    float EvaluateFitness(BridgeAIIndividual individual)
    {
        Car[] cars = FindObjectsOfType<Car>();
        float distance = 0f;

        for (int i = 0; i < cars.Length; i++)
        {
            if (cars[i] == null) continue;
            float d = cars[i].DistanceTravelled;
            if (d > distance)
                distance = d;
        }

        distance = Mathf.Max(0f, distance);
        lastActualDistance = distance;

        float predicted = 0f;
        if (mlModel != null)
        {
            predicted = mlModel.Predict(individual.genes);
            mlModel.AddSample(individual.genes, distance);
            lastPrediction = predicted;
            hasPrediction = true;
        }
        else
        {
            hasPrediction = false;
        }

        int brokenCount = 0;
        foreach (var gene in individual.genes)
        {
            if (gene != null && gene.broken)
                brokenCount++;
        }

        float bridgeSpan = 0f;
        if (individual.genes != null && individual.genes.Length > 0)
        {
            int firstIdx = -1;
            int lastIdx = -1;
            for (int i = 0; i < individual.genes.Length; i++)
            {
                if (individual.genes[i] != null)
                {
                    if (firstIdx == -1) firstIdx = i;
                    lastIdx = i;
                }
            }

            if (firstIdx != -1 && lastIdx != -1)
            {
                float firstX = individual.genes[firstIdx].start.x;
                float lastX = individual.genes[lastIdx].end.x;
                bridgeSpan = Mathf.Max(0f, lastX - firstX);
            }
        }

        float target = Mathf.Max(1f, targetDistanceForFitness);
        float normalizedDistance = Mathf.Clamp01(distance / target);

        bool reachedGoal = distance >= target;

        float distanceScore = normalizedDistance * distanceWeight;
        float spanScore = bridgeSpan * spanWeight;
        float breakPenalty = brokenCount * brokenPenalty;
        float successScore = reachedGoal ? successBonus : 0f;

        return distanceScore + spanScore + successScore - breakPenalty;
    }

    void CreateNextGeneration(List<BridgeAIIndividual> population, int eliteCount, float mutationChance, float maxOffset)
    {
        int size = population.Count;
        List<BridgeAIIndividual> elites = SelectTopIndividuals(population, eliteCount);
        List<BridgeAIIndividual> newPopulation = new List<BridgeAIIndividual>(elites);

        while (newPopulation.Count < size)
        {
            BridgeAIIndividual parentA = elites[Random.Range(0, elites.Count)];
            BridgeAIIndividual parentB = elites[Random.Range(0, elites.Count)];

            BridgeAIIndividual child = Crossover(parentA, parentB);
            Mutate(child, mutationChance, maxOffset);

            newPopulation.Add(child);
        }

        population.Clear();
        population.AddRange(newPopulation);
    }

    List<BridgeAIIndividual> SelectTopIndividuals(List<BridgeAIIndividual> population, int topCount)
    {
        population.Sort((a, b) => b.fitness.CompareTo(a.fitness));
        List<BridgeAIIndividual> selected = new List<BridgeAIIndividual>();
        for (int i = 0; i < topCount && i < population.Count; i++)
            selected.Add(population[i]);
        return selected;
    }

    BridgeAIIndividual Crossover(BridgeAIIndividual parentA, BridgeAIIndividual parentB)
    {
        int geneCount = parentA.genes.Length;
        BridgeAIIndividual child = new BridgeAIIndividual();
        child.genes = new BridgeGene[geneCount];

        for (int i = 0; i < geneCount; i++)
        {
            BridgeGene geneFromParent = Random.value < 0.5f ? parentA.genes[i] : parentB.genes[i];
            child.genes[i] = new BridgeGene
            {
                start = geneFromParent.start,
                end = geneFromParent.end,
                type = geneFromParent.type,
                broken = false
            };
        }

        return child;
    }

    void Mutate(BridgeAIIndividual individual, float mutationChance, float maxOffset)
    {
        for (int i = 0; i < individual.genes.Length; i++)
        {
            BridgeGene gene = individual.genes[i];

            if (Random.value < mutationChance)
            {
                Vector2 offsetStart = Random.insideUnitCircle * maxOffset;
                Vector2 offsetEnd = Random.insideUnitCircle * maxOffset;

                Vector2 newStart = gene.start + offsetStart;
                Vector2 newEnd = gene.end + offsetEnd;

                newStart.x = GridSnap(newStart.x, gridStep);
                newStart.y = GridSnap(newStart.y, gridStep);
                newEnd.x = GridSnap(newEnd.x, gridStep);
                newEnd.y = GridSnap(newEnd.y, gridStep);

                gene.start = newStart;
                gene.end = newEnd;
            }

            if (Random.value < mutationChance)
                gene.type = gene.type == BarType.Road ? BarType.Beam : BarType.Road;

            individual.genes[i] = gene;
        }
    }

    public BridgeAIIndividual CreateRandomIndividual(int geneCount, Vector2 minPos, Vector2 maxPos)
    {
        BridgeAIIndividual individual = new BridgeAIIndividual();
        individual.genes = new BridgeGene[geneCount];

        Vector2 currentPos;

        if (firstNode != null && firstNode.rb != null)
        {
            currentPos = firstNode.rb.position;
        }
        else
        {
            currentPos = new Vector2(
                Random.Range(minPos.x, maxPos.x),
                Random.Range(minPos.y, maxPos.y)
            );
        }

        for (int i = 0; i < geneCount; i++)
        {
            Vector2 offset = new Vector2(
                Random.Range(-2f, 2f),
                Random.Range(-1f, 2f)
            );

            Vector2 nextPos = currentPos + offset;

            nextPos.x = GridSnap(nextPos.x, gridStep);
            nextPos.y = GridSnap(nextPos.y, gridStep);

            individual.genes[i] = new BridgeGene
            {
                start = currentPos,
                end = nextPos,
                type = Random.value < 0.5f ? BarType.Road : BarType.Beam,
                broken = false
            };

            currentPos = nextPos;
        }

        return individual;
    }

    BridgeGene[] BuildGenesFromScene()
    {
        List<BridgeGene> result = new List<BridgeGene>();

        RoadBar[] roads = FindObjectsOfType<RoadBar>();
        for (int i = 0; i < roads.Length; i++)
        {
            RoadBar rb = roads[i];
            if (rb == null || rb.geneReference == null) continue;
            result.Add(rb.geneReference);
        }

        BeamBar[] beams = FindObjectsOfType<BeamBar>();
        for (int i = 0; i < beams.Length; i++)
        {
            BeamBar bb = beams[i];
            if (bb == null || bb.geneReference == null) continue;
            result.Add(bb.geneReference);
        }

        return result.ToArray();
    }

    void ExitAIMode()
    {
        Sim sim = FindObjectOfType<Sim>();
        if (sim != null)
        {
            sim.simulationRunning = false;
            sim.SetSimulationState(false);
            sim.hideUI = false;
            sim.ResetEverything();
        }

        Time.timeScale = prevTimeScale;
        Time.fixedDeltaTime = prevFixedDeltaTime;
        QualitySettings.vSyncCount = prevVSyncCount;
        Application.targetFrameRate = prevTargetFrameRate;

        if (bc != null)
            bc.aiControl = false;

        aiRunning = false;
    }

    void OnGUI()
    {
        BridgeMLModel mlm = mlModel;
        int sampleCount = (mlm != null ? mlm.SampleCount : 0);
        float lastLoss = (mlm != null ? mlm.lastLoss : 0f);
        int globalGen = (mlm != null ? mlm.globalGeneration : 0);
        float lastAcc = (mlm != null ? mlm.lastAccuracy : 0f);


        if (aiRunning)
        {
            string text =
                "GA MODE RUNNING\n" +
                "Generation: " + (currentGeneration + 1) + " / " + maxGenerations + "\n" +
                "Best fitness: " + bestFitnessOverall.ToString("0.00") + "\n" +
                "Best this gen: " + bestFitnessThisGeneration.ToString("0.00") + "\n" +
                "Global generations: " + globalGen;

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 20;

            GUI.Label(new Rect(10, 10, 400, 140), text, style);

            if (hasPrediction)
            {
                string text2 =
                    "Predicted distance: " + lastPrediction.ToString("0.00") + "\n" +
                    "Actual distance: " + lastActualDistance.ToString("0.00");

                GUIStyle style2 = new GUIStyle(GUI.skin.label);
                style2.fontSize = 16;
                style2.alignment = TextAnchor.UpperRight;

                float w = 260f;
                float h = 60f;
                Rect r = new Rect(Screen.width - w - 10f, 10f, w, h);

                GUI.Label(r, text2, style2);
            }

            if (mlm != null)
            {
                string mlText =
                    "ML Samples: " + sampleCount + "\n" +
                    "Accuracy: " + lastAcc.ToString("0.0") + "%";

                GUIStyle style3 = new GUIStyle(GUI.skin.label);
                style3.fontSize = 16;

                GUI.Label(new Rect(10, 160, 300, 60), mlText, style3);
            }
        }
        else if (hasPlayerPrediction)
        {
            string text =
                "Player bridge predicted dist: " + lastPlayerPrediction.ToString("0.00") +
                "\nActual: " + lastPlayerActualDistance.ToString("0.00");

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 18;
            style.alignment = TextAnchor.UpperRight;

            float w = 420f;
            float h = 60f;
            Rect r = new Rect(Screen.width - w - 10f, 10f, w, h);

            GUI.Label(r, text, style);
        }
        else if (mlm != null)
        {
            string mlText =
                "ML Samples: " + sampleCount + "\n" +
                "Accuracy: " + lastAcc.ToString("0.0") + "%\n" +
                "Global generations: " + globalGen;

            GUIStyle style3 = new GUIStyle(GUI.skin.label);
            style3.fontSize = 16;
            style3.alignment = TextAnchor.UpperRight;

            float w = 320f;
            float h = 80f;
            Rect r = new Rect(Screen.width - w - 10f, 10f, w, h);

            GUI.Label(r, mlText, style3);
        }
    }

    float GridSnap(float v, float step)
    {
        if (step <= 0f) return v;
        return Mathf.Round(v / step) * step;
    }
}
