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

    const float CoordX = 30f;
    const float CoordY = 10f;

    // Stored for training (last episode)
    float[] lastInput;    // size = inputSize
    float[] lastHidden;   // size = hiddenSize
    float[] lastMu;       // size = outputSize (means)
    float[] lastAction;   // size = outputSize (actual used actions)

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

    float Tanh(float x) => (float)System.Math.Tanh(x);

    float[] Forward(float[] input)
    {
        if (lastInput == null || lastInput.Length != inputSize)
            lastInput = new float[inputSize];
        if (lastHidden == null || lastHidden.Length != hiddenSize)
            lastHidden = new float[hiddenSize];
        if (lastMu == null || lastMu.Length != outputSize)
            lastMu = new float[outputSize];

        Array.Copy(input, lastInput, inputSize);

        for (int j = 0; j < hiddenSize; j++)
        {
            float sum = b1[j];
            for (int i = 0; i < inputSize; i++)
            {
                sum += w1[j, i] * input[i];
            }
            lastHidden[j] = Mathf.Max(0f, sum);
        }

        for (int o = 0; o < outputSize; o++)
        {
            float sum = b2[o];
            for (int j = 0; j < hiddenSize; j++)
            {
                sum += w2[o, j] * lastHidden[j];
            }
            lastMu[o] = Tanh(sum);
        }

        return lastMu;
    }

    public BridgeGene[] GenerateGenes(Point[] anchors, float explorationNoise)
    {
        Vector2 a0 = anchors[0].rb.position;
        Vector2 a1 = anchors[1].rb.position;
        Vector2 a2 = anchors[2].rb.position;

        float[] input = new float[6]
        {
            a0.x, a0.y,
            a1.x, a1.y,
            a2.x, a2.y
        };

        float[] mu = Forward(input);

        if (lastAction == null || lastAction.Length != outputSize)
            lastAction = new float[outputSize];

        float sigma = Mathf.Clamp(explorationNoise, 0.001f, 0.2f);

        for (int i = 0; i < outputSize; i++)
        {
            float eps = UnityEngine.Random.Range(-1f, 1f);
            float a = mu[i] + eps * sigma;
            lastAction[i] = Mathf.Clamp(a, -1f, 1f);
        }

        BridgeGene[] genes = new BridgeGene[geneCount];
        int idx = 0;

        float leftX  = Mathf.Min(a0.x, Mathf.Min(a1.x, a2.x));
        float rightX = Mathf.Max(a0.x, Mathf.Max(a1.x, a2.x));

        float minX = leftX - 2f;
        float maxX = rightX + 2f;

        float minY = -2f;
        float maxY = 6f;

        float maxLength = 5f;

        for (int g = 0; g < geneCount; g++)
        {
            if (idx + 4 >= outputSize)
                break;

            float sxNorm     = lastAction[idx++]; // start x [-1,1]
            float syNorm     = lastAction[idx++]; // start y [-1,1]
            float angleNorm  = lastAction[idx++]; // angle [-1,1]
            float lengthNorm = lastAction[idx++]; // length [-1,1]
            float typeLogit  = lastAction[idx++]; // type

            float sx01 = (sxNorm + 1f) * 0.5f;
            float sy01 = (syNorm + 1f) * 0.5f;

            Vector2 start = new Vector2(
                Mathf.Lerp(minX, maxX, sx01),
                Mathf.Lerp(minY, maxY, sy01)
            );

            float angle = angleNorm * Mathf.PI;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.right;

            dir.Normalize();

            float length = Mathf.Clamp01(Mathf.Abs(lengthNorm)) * maxLength;

            Vector2 end = start + dir * length;

            AIController.BarType type =
                (typeLogit > 0f ? AIController.BarType.Road : AIController.BarType.Beam);

            genes[g] = new BridgeGene
            {
                start  = start,
                end    = end,
                type   = type,
                broken = false
            };
        }

        return genes;
    }

    public void Train(Point[] anchors, BridgeGene[] genes, float fitness, float learningRate)
    {
        if (fitness <= 0f) return;
        if (lastInput == null || lastHidden == null || lastMu == null || lastAction == null)
            return;

        float R = fitness;

        float sigma = 0.3f;
        float invSigma2 = 1.0f / (sigma * sigma + 1e-6f);

        float[] dL_dmu = new float[outputSize];
        float[] dL_dz2 = new float[outputSize];

        for (int o = 0; o < outputSize; o++)
        {
            float a  = lastAction[o];
            float mu = lastMu[o];

            float gradLogPi_mu = (a - mu) * invSigma2;

            dL_dmu[o] = -R * gradLogPi_mu;

            float dmu_dz2 = 1f - mu * mu;
            dL_dz2[o] = dL_dmu[o] * dmu_dz2;
        }

        float[] dL_dh = new float[hiddenSize];

        for (int o = 0; o < outputSize; o++)
        {
            float grad = dL_dz2[o];

            for (int j = 0; j < hiddenSize; j++)
            {
                float dw2 = grad * lastHidden[j];
                w2[o, j] -= learningRate * dw2;

                dL_dh[j] += grad * w2[o, j];
            }

            b2[o] -= learningRate * grad;
        }

        for (int j = 0; j < hiddenSize; j++)
        {
            if (lastHidden[j] <= 0f)
            {
                dL_dh[j] = 0f;
            }

            float grad_h = dL_dh[j];

            for (int i = 0; i < inputSize; i++)
            {
                float dw1 = grad_h * lastInput[i];
                w1[j, i] -= learningRate * dw1;
            }

            b1[j] -= learningRate * grad_h;
        }
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
