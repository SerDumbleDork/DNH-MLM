using UnityEngine;

[System.Serializable]
public class BridgePBTModel
{
    public BridgePolicyNetwork net;

    // Hyperparameters (mutated by PBT)
    public float learningRate;
    public float explorationNoise;

    public float lastFitness;
    public bool useBackprop = true;

    public BridgePBTModel(int genesPerBridge, int hiddenSize)
    {
        net = new BridgePolicyNetwork(genesPerBridge, hiddenSize);
        RandomizeHyperparameters();
        MutateWeights(0.01f);
    }

    public BridgeGene[] GenerateBridge(Point[] anchors)
    {
        return net.GenerateGenes(anchors, explorationNoise);
    }

    public void Train(Point[] anchors, BridgeGene[] genes, float fitness)
    {
        lastFitness = fitness;

        if (useBackprop && fitness > 0f)
        {
            net.Train(anchors, genes, fitness, learningRate);
        }
    }

    public void CopyFrom(BridgePBTModel better)
    {
        if (better == null) return;

        net.CopyWeightsFrom(better.net);
        learningRate     = better.learningRate;
        explorationNoise = better.explorationNoise;
        useBackprop      = better.useBackprop;

        // Remove the reset — let lastFitness update after real evaluation
        // lastFitness = 0f;
    }

    public void MutateHyperparameters()
    {
        learningRate *= Random.Range(0.8f, 1.2f);
        learningRate = Mathf.Clamp(learningRate, 0.00001f, 0.0015f);

        explorationNoise *= Random.Range(0.8f, 1.2f);
        explorationNoise = Mathf.Clamp(explorationNoise, 0.05f, 1.0f);
    }

    public void MutateWeights(float scale)
    {
        net.MutateWeights(scale);
    }

    void RandomizeHyperparameters()
    {
        learningRate     = Random.Range(0.0001f, 0.001f);
        explorationNoise = Random.Range(0.1f, 0.5f);
    }
}
