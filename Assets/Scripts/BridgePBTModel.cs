using UnityEngine;

[System.Serializable]
public class BridgePBTModel
{
    public BridgePolicyNetwork net;
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

    public void Train(float fitness)
    {
        lastFitness = fitness;
    }

    public void CopyFrom(BridgePBTModel better)
    {
        net.CopyWeightsFrom(better.net);
        learningRate = better.learningRate;
        explorationNoise = better.explorationNoise;
        useBackprop = better.useBackprop;
        lastFitness = 0f;
    }

    public void MutateHyperparameters()
    {
        learningRate *= Random.Range(0.8f, 1.2f);
        learningRate = Mathf.Clamp(learningRate, 0.0001f, 0.002f);

        explorationNoise *= Random.Range(0.8f, 1.3f);
        explorationNoise = Mathf.Clamp(explorationNoise, 0.05f, 0.15f);

    }

    public void MutateWeights(float scale)
    {
        net.MutateWeights(scale);
    }

    void RandomizeHyperparameters()
    {
        learningRate = Random.Range(0.005f, 0.01f);
        explorationNoise = Random.Range(0.02f, 0.06f);
    }

    public string ExportWeightsString()
    {
        float[] w = net.GetAllWeights();
        byte[] bytes = FloatArrayToBytes(w);
        return System.Convert.ToBase64String(bytes);
    }

    public void ImportWeightsString(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return;

        try
        {
            byte[] bytes = System.Convert.FromBase64String(base64);
            float[] w = BytesToFloatArray(bytes);
            net.SetAllWeights(w);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("ImportWeightsString failed: " + ex.Message);
        }
    }

    // Converts float[] → byte[] using IEEE 754
    private byte[] FloatArrayToBytes(float[] arr)
    {
        byte[] result = new byte[arr.Length * 4];
        for (int i = 0; i < arr.Length; i++)
        {
            System.Buffer.BlockCopy(
                System.BitConverter.GetBytes(arr[i]),
                0,
                result,
                i * 4,
                4
            );
        }
        return result;
    }

    // Converts byte[] → float[]
    private float[] BytesToFloatArray(byte[] bytes)
    {
        int count = bytes.Length / 4;
        float[] result = new float[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = System.BitConverter.ToSingle(bytes, i * 4);
        }
        return result;
    }
}
