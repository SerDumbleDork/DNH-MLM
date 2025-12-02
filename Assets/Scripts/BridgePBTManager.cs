using UnityEngine;
using System.Collections.Generic;

public class BridgePBTManager : MonoBehaviour
{
    [Header("Population Settings")]
    public int populationSize = 12;
    public int genesPerBridge = 24;
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
                string rawModel   = entry.ContainsKey("modelIndex") ? entry["modelIndex"]?.ToString() : "0";
                string rawFitness = entry.ContainsKey("fitness") ? entry["fitness"]?.ToString() : "0";
                string rawLR      = entry.ContainsKey("learningRate") ? entry["learningRate"]?.ToString() : "0";
                string rawNoise   = entry.ContainsKey("noise") ? entry["noise"]?.ToString() : "0";

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
                    models[modelIndex].learningRate     = lr;
                    models[modelIndex].explorationNoise = noise;
                    models[modelIndex].lastFitness      = fitness;
                }
            }

            Debug.Log("Population restored from Google Sheets.");
            Debug.Log("Global Generation now: " + globalGeneration);
        });
    }

    void EvolvePopulation()
    {
        if (models == null || models.Count == 0) return;

        models.Sort((a, b) => b.lastFitness.CompareTo(a.lastFitness));

        int survivors = Mathf.Max(1, populationSize / 2);

        for (int i = survivors; i < populationSize; i++)
        {
            BridgePBTModel parent = models[Random.Range(0, survivors)];
            models[i].CopyFrom(parent);
            models[i].MutateHyperparameters();
            models[i].MutateWeights(0.00f);
        }

        Debug.Log($"PBT: Generation {generationIndex} evolved. Best fitness = {models[0].lastFitness:F2}");
    }
}

