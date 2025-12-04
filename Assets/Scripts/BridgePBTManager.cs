using UnityEngine;
using System.Collections.Generic;

public class BridgePBTManager : MonoBehaviour
{
    [Header("Population Settings")]
    public int populationSize = 12;
    public int genesPerBridge = 16;
    public int hiddenSize     = 48;

    [Header("Anchors (drag 3 Point objects here)")]
    public Point[] anchorPoints;

    [HideInInspector]
    public List<BridgePBTModel> models = new List<BridgePBTModel>();

    [HideInInspector]
    public int currentModelIndex = 0;

    [HideInInspector]
    public int generationIndex = 0;
    [HideInInspector]
    public int globalGeneration = 0;

    private System.Collections.IEnumerator LoadPopulationThenInit()
    {
        InitPopulation();

        yield return LoadPopulationFromSheet();

        Debug.Log("PBT Populated. Starting at Global Generation: " + globalGeneration);
    }

    void Awake()
    {
        if (anchorPoints == null || anchorPoints.Length < 3)
        {
            Debug.LogWarning("BridgePBTManager: please assign 3 anchor Points in the Inspector.");
        }

        StartCoroutine(LoadPopulationThenInit());
    }

    void InitPopulation()
    {
        models.Clear();
        for (int i = 0; i < populationSize; i++)
        {
            var m = new BridgePBTModel(genesPerBridge, hiddenSize);
            models.Add(m);
        }
        currentModelIndex = 0;
        generationIndex = globalGeneration;
    }

    public BridgePBTModel CurrentModel
    {
        get
        {
            if (models == null || models.Count == 0) return null;
            if (currentModelIndex < 0 || currentModelIndex >= models.Count) currentModelIndex = 0;
            return models[currentModelIndex];
        }
    }

    public void AdvanceToNextModel()
    {
        if (models == null || models.Count == 0) return;

        currentModelIndex++;
        if (currentModelIndex >= models.Count)
        {
            currentModelIndex = 0;
            EvolvePopulation();
            globalGeneration++;
            generationIndex = globalGeneration;
        }
    }

    public System.Collections.IEnumerator LoadPopulationFromSheet()
    {
        Debug.Log("Loading population from Google Sheets...");

        yield return GoogleSheetsLoader.LoadAllEntries((rows) =>
        {
            if (rows == null || rows.Count == 0)
            {
                Debug.LogWarning("No saved data found. Starting fresh.");
                return;
            }

            foreach (var entry in rows)
            {
                string rawGen     = entry.ContainsKey("globalGeneration") ? entry["globalGeneration"]?.ToString() : "0";
                string rawModel   = entry.ContainsKey("modelIndex")       ? entry["modelIndex"]?.ToString()      : "0";
                string rawFitness = entry.ContainsKey("fitness")          ? entry["fitness"]?.ToString()         : "0";
                string rawLR      = entry.ContainsKey("learningRate")     ? entry["learningRate"]?.ToString()    : "0.0003";
                string rawNoise   = entry.ContainsKey("noise")            ? entry["noise"]?.ToString()           : "0.3";
                string rawWeights = entry.ContainsKey("weights")          ? entry["weights"]?.ToString()         : "";

                Debug.Log($"Row => Gen:'{rawGen}', Model:'{rawModel}', Fit:'{rawFitness}', LR:'{rawLR}', Noise:'{rawNoise}'");

                int gen         = int.TryParse(rawGen, out var g) ? g : 0;
                int modelIndex  = int.TryParse(rawModel, out var mi) ? mi : 0;
                float fitness   = float.TryParse(rawFitness, out var ft) ? ft : 0f;
                float lr        = float.TryParse(rawLR, out var lrVal) ? lrVal : 0.0003f;
                float noise     = float.TryParse(rawNoise, out var nz) ? nz : 0.3f;

                globalGeneration = Mathf.Max(globalGeneration, gen);
                generationIndex = globalGeneration;

                if (modelIndex < models.Count)
                {
                    var m = models[modelIndex];

                    m.learningRate     = lr;
                    m.explorationNoise = noise;
                    m.lastFitness      = fitness;

                    // Import weights (if saved)
                    if (!string.IsNullOrEmpty(rawWeights))
                    {
                        m.ImportWeightsString(rawWeights);
                    }
                }
            }

            Debug.Log("Population restored from Google Sheets.");
            Debug.Log("Global Generation now: " + globalGeneration);
        });

        for (int i = 0; i < models.Count; i++)
        {
            string file = $"model_{i}_gen_{globalGeneration}.txt";
            string weights = WeightStorage.Load(file);
            if (!string.IsNullOrEmpty(weights))
            {
                models[i].ImportWeightsString(weights);
                Debug.Log($"Loaded local weights for model {i}");
            }
        }
    }

    public void EvolvePopulation()
    {
        models.Sort((a, b) => b.lastFitness.CompareTo(a.lastFitness));

        BridgePBTModel elite1 = CloneModel(models[0]);
        BridgePBTModel elite2 = CloneModel(models[1]);

        BridgePBTModel[] newPop = new BridgePBTModel[populationSize];

        newPop[0] = elite1;
        newPop[1] = elite2;

        for (int i = 2; i < populationSize; i++)
        {
            int parentIndex = Random.Range(0, populationSize / 2);

            BridgePBTModel child = CloneModel(models[parentIndex]);

            child.MutateHyperparameters();
            child.MutateWeights(child.explorationNoise * 0.02f);

            newPop[i] = child;
        }

        models = new List<BridgePBTModel>(newPop);

        generationIndex++;
    }

    private BridgePBTModel CloneModel(BridgePBTModel src)
    {
        BridgePBTModel m = new BridgePBTModel(genesPerBridge, hiddenSize);
        m.CopyFrom(src);
        return m;
    }
}

