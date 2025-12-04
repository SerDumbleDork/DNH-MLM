using UnityEngine;
using System;
using System.Collections.Generic;

public class BridgePolicyNetwork
{
    int geneCount;
    int hiddenSize;
    int inputSize;

    float[,] w1;
    float[]  b1;
    float[,] w2;
    float[]  b2;

    int outputSize;

    float[] lastInput;
    float[] lastHidden;
    float[] lastMu;
    float[] lastAction;

    static float baseline = 0f;

    public List<float[]> obsList  = new List<float[]>();
    public List<float[]> muList   = new List<float[]>();
    public List<float[]> actList  = new List<float[]>();
    public List<float[]> hidList  = new List<float[]>();

    public BridgePolicyNetwork(int genesPerBridge, int hidden)
    {
        geneCount  = genesPerBridge;
        hiddenSize = hidden;
        inputSize  = 20;
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
                w1[j, i] = UnityEngine.Random.Range(-0.1f, 0.1f);
        }

        for (int o = 0; o < outputSize; o++)
        {
            b2[o] = 0f;
            for (int j = 0; j < hiddenSize; j++)
                w2[o, j] = UnityEngine.Random.Range(-0.1f, 0.1f);
        }
    }

    void EnsureArray(ref float[] arr, int size)
    {
        if (arr == null || arr.Length != size)
            arr = new float[size];
    }

    float Tanh(float x) => (float)Math.Tanh(x);

    float[] Forward(float[] input)
    {
        EnsureArray(ref lastInput,  inputSize);
        EnsureArray(ref lastHidden, hiddenSize);
        EnsureArray(ref lastMu,     outputSize);

        Array.Copy(input, lastInput, inputSize);

        for (int j = 0; j < hiddenSize; j++)
        {
            float sum = b1[j];
            for (int i = 0; i < inputSize; i++)
                sum += w1[j, i] * input[i];

            lastHidden[j] = Mathf.Max(0f, sum);
        }

        for (int o = 0; o < outputSize; o++)
        {
            float sum = b2[o];
            for (int j = 0; j < hiddenSize; j++)
                sum += w2[o, j] * lastHidden[j];

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

        EnsureArray(ref lastAction, outputSize);

        float sigma = Mathf.Clamp(explorationNoise, 0.001f, 0.2f);

        for (int i = 0; i < outputSize; i++)
        {
            float eps = UnityEngine.Random.Range(-1f, 1f);
            float a   = mu[i] + eps * sigma;
            lastAction[i] = Mathf.Clamp(a, -1f, 1f);
        }

        float leftX  = Mathf.Min(a0.x, Mathf.Min(a1.x, a2.x));
        float rightX = Mathf.Max(a0.x, Mathf.Max(a1.x, a2.x));

        float minX = leftX - 2f;
        float maxX = rightX + 2f;
        float minY = -2f;
        float maxY = 6f;

        float maxLength = 5f;

        BridgeGene[] genes = new BridgeGene[geneCount];
        int idx = 0;

        for (int g = 0; g < geneCount; g++)
        {
            if (idx + 4 >= outputSize) break;

            float sxNorm     = lastAction[idx++];
            float syNorm     = lastAction[idx++];
            float angleNorm  = lastAction[idx++];
            float lengthNorm = lastAction[idx++];
            float typeLogit  = lastAction[idx++];

            Vector2 start;

            if (g == 0)
            {
                start = anchors[0].rb.position;
            }
            else
            {
                float sx01 = (sxNorm + 1f) * 0.5f;
                float sy01 = (syNorm + 1f) * 0.5f;

                start = new Vector2(
                    Mathf.Lerp(minX, maxX, sx01),
                    Mathf.Lerp(minY, maxY, sy01)
                );
            }

            float angle = angleNorm * Mathf.PI;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
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

    public void Train(
        List<float[]> obsListParam,
        List<float[]> muListParam,
        List<float[]> actionListParam,
        List<float[]> hiddenListParam,
        float returnR,
        float learningRate,
        float sigma
    )
    {
        if (obsListParam == null || muListParam == null ||
            actionListParam == null || hiddenListParam == null)
            return;

        int T = obsListParam.Count;
        if (T == 0) return;

        baseline = baseline * 0.9f + returnR * 0.1f;
        float advantage = returnR - baseline;

        float invSigma2 = 1f / (sigma * sigma + 1e-6f);

        float[,] dW2 = new float[outputSize, hiddenSize];
        float[]  dB2 = new float[outputSize];

        float[,] dW1 = new float[hiddenSize, inputSize];
        float[]  dB1 = new float[hiddenSize];

        for (int t = 0; t < T; t++)
        {
            float[] mu     = muListParam[t];
            float[] action = actionListParam[t];
            float[] hid    = hiddenListParam[t];
            float[] inp    = obsListParam[t];

            float[] dL_dz2 = new float[outputSize];

            for (int o = 0; o < outputSize; o++)
            {
                float a = action[o];
                float m = mu[o];

                float gradLogPi = (a - m) * invSigma2;
                float dL_dmu = advantage * gradLogPi;

                float dz = (1f - m * m);
                dL_dz2[o] = dL_dmu * dz;
            }

            float[] dL_dh = new float[hiddenSize];

            for (int o = 0; o < outputSize; o++)
            {
                float grad = dL_dz2[o];

                for (int j = 0; j < hiddenSize; j++)
                {
                    dW2[o, j] += grad * hid[j];
                    dL_dh[j]  += grad * w2[o, j];
                }

                dB2[o] += grad;
            }

            for (int j = 0; j < hiddenSize; j++)
            {
                float h = hid[j];
                if (h <= 0f) continue;

                float grad_h = dL_dh[j];

                for (int i = 0; i < inputSize; i++)
                    dW1[j, i] += grad_h * inp[i];

                dB1[j] += grad_h;
            }
        }

        for (int o = 0; o < outputSize; o++)
        {
            for (int j = 0; j < hiddenSize; j++)
                w2[o, j] -= learningRate * dW2[o, j];

            b2[o] -= learningRate * dB2[o];
        }

        for (int j = 0; j < hiddenSize; j++)
        {
            for (int i = 0; i < inputSize; i++)
                w1[j, i] -= learningRate * dW1[j, i];

            b1[j] -= learningRate * dB1[j];
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
                w1[j, i] += UnityEngine.Random.Range(-scale, scale);

            b1[j] += UnityEngine.Random.Range(-scale, scale);
        }

        for (int o = 0; o < outputSize; o++)
        {
            for (int j = 0; j < hiddenSize; j++)
                w2[o, j] += UnityEngine.Random.Range(-scale, scale);

            b2[o] += UnityEngine.Random.Range(-scale, scale);
        }
    }

    public float[] GetAllWeights()
    {
        int total =
            w1.Length + b1.Length +
            w2.Length + b2.Length;

        var list = new List<float>(total);

        foreach (float v in w1) list.Add(v);
        foreach (float v in b1) list.Add(v);
        foreach (float v in w2) list.Add(v);
        foreach (float v in b2) list.Add(v);

        return list.ToArray();
    }

    public void SetAllWeights(float[] arr)
    {
        int idx = 0;

        for (int j = 0; j < hiddenSize; j++)
            for (int i = 0; i < inputSize; i++)
                w1[j, i] = arr[idx++];

        for (int j = 0; j < hiddenSize; j++)
            b1[j] = arr[idx++];

        for (int o = 0; o < outputSize; o++)
            for (int j = 0; j < hiddenSize; j++)
                w2[o, j] = arr[idx++];

        for (int o = 0; o < outputSize; o++)
            b2[o] = arr[idx++];
    }

    public BridgeGene[] GenerateGenesSequential(
        Point[] anchors,
        BarCreator bc,
        Goal goal,
        float explorationNoise
    )
    {
        int totalBars = geneCount;
        BridgeGene[] genes = new BridgeGene[totalBars];

        Point lastEnd = anchors[0];
        bool lastSuccess = true;
        int brokenBars = 0;
        float carProg = 0f;

        for (int g = 0; g < totalBars; g++)
        {
            float[] obs = BridgeObservation.Build(
                anchors,
                lastEnd,
                g,
                totalBars,
                carProg,
                (float)brokenBars / 20f,
                lastSuccess,
                goal,
                bc
            );

            float[] mu = Forward(obs);
            float[] a  = SampleAction(mu, explorationNoise);

            obsList.Add((float[])obs.Clone());

            float[] muShort = new float[5];
            Array.Copy(mu, 0, muShort, 0, 5);
            muList.Add(muShort);

            actList.Add((float[])a.Clone());
            hidList.Add((float[])lastHidden.Clone());

            float sxNorm     = a[0];
            float syNorm     = a[1];
            float angleNorm  = a[2];
            float lengthNorm = a[3];
            float typeLogit  = a[4];

            // Forced start logic
            Vector2 start;

            if (g == 0)
            {
                // Always start at anchor 0
                start = anchors[0].rb.position;
            }
            else if (g == 1)
            {
                // Force bar 1 to extend from bar 0’s end
                start = lastEnd.rb.position;
            }
            else
            {
                // Mostly continue from lastEnd
                if (UnityEngine.Random.value < 0.70f)
                {
                    start = lastEnd.rb.position;
                }
                else
                {
                    // Sometimes allow branching to create supports
                    start = DecodeStart(bc, sxNorm, syNorm);
                }
            }

            Vector2 end = DecodeEnd(start, angleNorm, lengthNorm);

            AIController.BarType type =
                (typeLogit > 0f ? AIController.BarType.Road : AIController.BarType.Beam);

            genes[g] = new BridgeGene { start = start, end = end, type = type };

            MonoBehaviour bar = TryPlaceBar(bc, genes[g]);

            lastSuccess = (bar != null);

            if (lastSuccess)
            {
                lastEnd = (bar is RoadBar r) ? r.nodeB
                         : (bar is BeamBar bBeam) ? bBeam.nodeB
                         : lastEnd;
            }
            else
            {
                brokenBars++;
            }
        }

        return genes;
    }

    float[] SampleAction(float[] mu, float explorationNoise)
    {
        EnsureArray(ref lastAction, outputSize);

        float[] a = new float[5];
        float sigma = Mathf.Clamp(explorationNoise, 0.001f, 0.2f);

        for (int i = 0; i < 5; i++)
        {
            float eps = UnityEngine.Random.Range(-1f, 1f);
            float val = mu[i] + eps * sigma;
            a[i] = Mathf.Clamp(val, -1f, 1f);
        }

        return a;
    }

    Vector2 DecodeStart(BarCreator bc, float sx, float sy)
    {
        float leftX  = bc.leftBound.position.x;
        float rightX = bc.rightBound.position.x;

        float minX = leftX - 1f;
        float maxX = rightX + 1f;
        float minY = -2f;
        float maxY = 6f;

        float nx = (sx + 1f) * 0.5f;
        float ny = (sy + 1f) * 0.5f;

        return new Vector2(
            Mathf.Lerp(minX, maxX, nx),
            Mathf.Lerp(minY, maxY, ny)
        );
    }

    Vector2 DecodeEnd(Vector2 start, float angleNorm, float lengthNorm)
    {
        float angle = angleNorm * Mathf.PI;
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        dir.Normalize();

        float length = Mathf.Clamp01(Mathf.Abs(lengthNorm)) * 5f;

        return start + dir * length;
    }

    MonoBehaviour TryPlaceBar(BarCreator bc, BridgeGene gene)
    {
        bc.aiMousePos = gene.start;
        bc.mode = (gene.type == AIController.BarType.Road)
            ? BarCreator.Mode.Road
            : BarCreator.Mode.Beam;

        bc.LeftClick();

        bc.aiMousePos = gene.end;
        MonoBehaviour bar = bc.LeftClick();

        return bar;
    }
}
