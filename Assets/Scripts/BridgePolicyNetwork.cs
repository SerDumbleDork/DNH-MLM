using UnityEngine;
using System;

public class BridgePolicyNetwork
{
    int geneCount;
    int hiddenSize;
    int inputSize;

    float[,] w1;
    float[] b1;
    float[,] w2;
    float[] b2;

    int outputSize;

    const float CoordScaleX = 15f;
    const float CoordScaleY = 6f;

    System.Random rng = new System.Random();

    public BridgePolicyNetwork(int genesPerBridge, int hidden)
    {
        geneCount  = genesPerBridge;
        hiddenSize = hidden;
        inputSize  = 6;
        outputSize = geneCount * 5;

        w1 = new float[hiddenSize, inputSize];
        b1 = new float[hiddenSize];

        w2 = new float[outputSize, hiddenSize];
        b2 = new float[outputSize];

        InitWeights();
    }

    void InitWeights()
    {
        for (int j = 0; j < hiddenSize; j++)
        {
            b1[j] = 0f;
            for (int i = 0; i < inputSize; i++)
            {
                w1[j, i] = UnityEngine.Random.Range(-0.1f, 0.1f);
            }
        }

        for (int o = 0; o < outputSize; o++)
        {
            b2[o] = 0f;
            for (int j = 0; j < hiddenSize; j++)
            {
                w2[o, j] = UnityEngine.Random.Range(-0.1f, 0.1f);
            }
        }
    }

    float Tanh(float x)
    {
        return (float)System.Math.Tanh(x);
    }

    float[] Forward(float[] input, out float[] hidden)
    {
        hidden = new float[hiddenSize];
        float[] output = new float[outputSize];

        for (int j = 0; j < hiddenSize; j++)
        {
            float sum = b1[j];
            for (int i = 0; i < inputSize; i++)
            {
                sum += w1[j, i] * input[i];
            }
            hidden[j] = Mathf.Max(0f, sum);
        }

        for (int o = 0; o < outputSize; o++)
        {
            float sum = b2[o];
            for (int j = 0; j < hiddenSize; j++)
            {
                sum += w2[o, j] * hidden[j];
            }
            output[o] = Tanh(sum);
        }

        return output;
    }

    public BridgeGene[] GenerateGenes(Point[] anchors, float explorationNoise)
    {
        if (anchors == null || anchors.Length < 3)
        {
            Debug.LogWarning("BridgePolicyNetwork.GenerateGenes: need 3 anchors.");
            return new BridgeGene[0];
        }

        Vector2 a0 = anchors[0].rb.position;
        Vector2 a1 = anchors[1].rb.position;
        Vector2 a2 = anchors[2].rb.position;

        float[] input = new float[6]
        {
            a0.x, a0.y,
            a1.x, a1.y,
            a2.x, a2.y
        };

        float[] hidden;
        float[] raw = Forward(input, out hidden);

        if (explorationNoise > 0f)
        {
            for (int i = 0; i < raw.Length; i++)
            {
                float n = UnityEngine.Random.Range(-explorationNoise, explorationNoise);
                raw[i] = Tanh(raw[i] + n);
            }
        }

        BridgeGene[] genes = new BridgeGene[geneCount];
        int idx = 0;

        for (int g = 0; g < geneCount; g++)
        {
            float sxNorm = raw[idx++];
            float syNorm = raw[idx++];
            float exNorm = raw[idx++];
            float eyNorm = raw[idx++];
            float typeLogit = raw[idx++];

            Vector2 start = new Vector2(sxNorm * CoordScaleX, syNorm * CoordScaleY);
            Vector2 end   = new Vector2(exNorm * CoordScaleX, eyNorm * CoordScaleY);

            AIController.BarType type =
                (typeLogit > 0f ? AIController.BarType.Road : AIController.BarType.Beam);

            genes[g] = new BridgeGene
            {
                start = start,
                end   = end,
                type  = type,
                broken = false
            };
        }

        return genes;
    }

    public void Train(Point[] anchors, BridgeGene[] genes, float fitness, float learningRate)
    {
        // Placeholder for different training methods that aren't needed'
    }

    public void CopyWeightsFrom(BridgePolicyNetwork other)
    {
        if (other == null) return;

        Array.Copy(other.w1, w1, other.w1.Length);
        Array.Copy(other.w2, w2, other.w2.Length);
        Array.Copy(other.b1, b1, other.b1.Length);
        Array.Copy(other.b2, b2, other.b2.Length);
    }

    public void MutateWeights(float scale)
    {
        if (scale <= 0f) return;

        for (int j = 0; j < hiddenSize; j++)
        {
            for (int i = 0; i < inputSize; i++)
            {
                w1[j, i] += UnityEngine.Random.Range(-scale, scale);
            }
            b1[j] += UnityEngine.Random.Range(-scale, scale);
        }

        for (int o = 0; o < outputSize; o++)
        {
            for (int j = 0; j < hiddenSize; j++)
            {
                w2[o, j] += UnityEngine.Random.Range(-scale, scale);
            }
            b2[o] += UnityEngine.Random.Range(-scale, scale);
        }
    }
}
