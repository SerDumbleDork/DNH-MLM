using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine.Networking;

public class BridgeMLModel : MonoBehaviour
{
    [Header("Structure")]
    public int genesPerIndividual = 20;
    public int hiddenSize = 32;

    [Header("Training")]
    public float learningRate = 0.00005f;
    public int maxSamples = 5000;
    public int epochsPerTrain = 5;

    [Header("Logging / Web")]
    public bool saveLocally = true;
    public bool uploadToWeb = false;
    public bool loadDatasetFromSheetOnStart = true;
    public string googleSheetsUrl = "https://script.google.com/macros/s/YOUR_SCRIPT_ID/exec";
    public string csvFolderName = "bridge_training_data";
    public string csvFilePrefix = "bridge_data";

    [Header("Debug / Meta")]
    public float weightClamp = 5f;
    public int populationSize = 20; // used only for your own reference / UI
    public int globalGeneration = 0;
    public float lastLoss = 0f;        // last loss (for this sample or last epoch)
    public float lastAccuracy = 0f;    // per-sample accuracy (%)

    // internal data
    private List<float[]> inputs = new List<float[]>();
    private List<float> targets = new List<float>();

    // Normalization constants
    public const float POS_SCALE = 10f;       // divides x,y positions
    public const float DIST_SCALE = 20f;      // divides distance traveled
    public const float MAX_PREDICTION = 50f;  // safety clamp (world units)


    private int inputSize;
    private float[,] w1;
    private float[] b1;
    private float[] w2;
    private float b2;

    public int SampleCount => inputs.Count;

    void Awake()
    {
        inputSize = genesPerIndividual * 4;
        InitWeights();
    }

    void Start()
    {
        if (loadDatasetFromSheetOnStart && !string.IsNullOrEmpty(googleSheetsUrl))
            StartCoroutine(LoadDatasetFromSheet());
    }

    void InitWeights()
    {
        w1 = new float[hiddenSize, inputSize];
        b1 = new float[hiddenSize];
        w2 = new float[hiddenSize];
        b2 = 0f;

        for (int j = 0; j < hiddenSize; j++)
        {
            b1[j] = 0f;
            w2[j] = Random.Range(-0.1f, 0.1f);
            for (int i = 0; i < inputSize; i++)
                w1[j, i] = Random.Range(-0.1f, 0.1f);
        }
    }

    // ------------------------------------------------------------------------
    //  ADDING SAMPLES (GA + PLAYER)  —  also per-sample accuracy
    // ------------------------------------------------------------------------
    public void AddSample(BridgeGene[] genes, float distance)
    {
        if (genes == null) return;
        if (float.IsNaN(distance) || float.IsInfinity(distance)) return;

        float[] x = EncodeGenes(genes);

        // keep buffer bounded
        if (inputs.Count >= maxSamples)
        {
            inputs.RemoveAt(0);
            targets.RemoveAt(0);
        }

        inputs.Add(x);
        targets.Add(distance / DIST_SCALE);

        // mirror to disk / sheet
        SaveOneSampleRow(x, distance);
        globalGeneration++;

        // per-sample prediction & accuracy (Option A)
        float predicted = Predict(genes);
        if (!float.IsNaN(predicted) && !float.IsInfinity(predicted))
        {
            float diff = predicted - distance;
            float error = Mathf.Abs(diff);

            // loss for this sample (for debug only)
            lastLoss = 0.5f * diff * diff;

            // accuracy = 1 - (|pred - actual| / actual), clamped
            float denom = Mathf.Max(1f, Mathf.Abs(distance));
            lastAccuracy = Mathf.Clamp01(1f - (error / denom)) * 100f;
        }
    }

    // encode genes into flat feature vector
    float[] EncodeGenes(BridgeGene[] genes)
    {
        float[] x = new float[inputSize];
        int idx = 0;

        for (int i = 0; i < genesPerIndividual; i++)
        {
            if (genes != null && i < genes.Length && genes[i] != null)
            {
                x[idx++] = genes[i].start.x / POS_SCALE;
                x[idx++] = genes[i].start.y / POS_SCALE;
                x[idx++] = genes[i].end.x   / POS_SCALE;
                x[idx++] = genes[i].end.y   / POS_SCALE;
            }
            else
            {
                x[idx++] = 0f;
                x[idx++] = 0f;
                x[idx++] = 0f;
                x[idx++] = 0f;
            }
        }

        return x;
    }

    // ------------------------------------------------------------------------
    //  SAVE ONE ROW (local CSV + Google Sheet)
    // ------------------------------------------------------------------------
    void SaveOneSampleRow(float[] x, float distance)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.Append(",");
        sb.Append(distance.ToString("G9"));

        for (int i = 0; i < x.Length; i++)
        {
            sb.Append(",");
            sb.Append(x[i].ToString("G9"));
        }

        string row = sb.ToString();

        if (saveLocally)
            SaveRowLocal(row);

        if (uploadToWeb && !string.IsNullOrEmpty(googleSheetsUrl))
            StartCoroutine(UploadRow(row));
    }

    void SaveRowLocal(string row)
    {
        string folder = Path.Combine(Application.persistentDataPath, csvFolderName);
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string path = Path.Combine(folder, csvFilePrefix + "_log.csv");
        File.AppendAllText(path, row + "\n");
    }

    IEnumerator UploadRow(string row)
    {
        WWWForm form = new WWWForm();
        form.AddField("data", row);

        using (UnityWebRequest www = UnityWebRequest.Post(googleSheetsUrl, form))
        {
            yield return www.SendWebRequest();
        }
    }

    // ------------------------------------------------------------------------
    //  TRAINING — epochs over in-memory dataset
    // ------------------------------------------------------------------------
    public void Train(int epochs)
    {
        if (inputs.Count == 0) return;

        float[] z1 = new float[hiddenSize];
        float[] h = new float[hiddenSize];

        for (int e = 0; e < epochs; e++)
        {
            float totalLoss = 0f;

            for (int n = 0; n < inputs.Count; n++)
            {
                float[] x = inputs[n];
                float target = targets[n];

                float y = Forward(x, z1, h);
                float diff = y - target;
                float loss = 0.5f * diff * diff;
                totalLoss += loss;

                Backprop(x, z1, h, y, target);
            }

            lastLoss = totalLoss / inputs.Count; // epoch avg
        }
    }

    float Forward(float[] x, float[] z1, float[] h)
    {
        for (int j = 0; j < hiddenSize; j++)
        {
            float sum = b1[j];
            for (int i = 0; i < inputSize; i++)
                sum += w1[j, i] * x[i];
            z1[j] = sum;
            h[j] = sum > 0f ? sum : 0f;
        }

        float y = b2;
        for (int j = 0; j < hiddenSize; j++)
            y += w2[j] * h[j];

        return y;
    }

    void Backprop(float[] x, float[] z1, float[] h, float y, float target)
    {
        float dL_dy = y - target;

        for (int j = 0; j < hiddenSize; j++)
        {
            float gradW2 = dL_dy * h[j];
            w2[j] -= learningRate * gradW2;
            w2[j] = Mathf.Clamp(w2[j], -weightClamp, weightClamp);
        }

        b2 -= learningRate * dL_dy;
        b2 = Mathf.Clamp(b2, -weightClamp, weightClamp);

        for (int j = 0; j < hiddenSize; j++)
        {
            float dL_dhj = dL_dy * w2[j];
            float reluGrad = (z1[j] > 0f ? 1f : 0f);
            float dL_dz = dL_dhj * reluGrad;

            for (int i = 0; i < inputSize; i++)
            {
                float gradW1 = dL_dz * x[i];
                w1[j, i] -= learningRate * gradW1;
                w1[j, i] = Mathf.Clamp(w1[j, i], -weightClamp, weightClamp);
            }

            b1[j] -= learningRate * dL_dz;
            b1[j] = Mathf.Clamp(b1[j], -weightClamp, weightClamp);
        }
    }

    public float Predict(BridgeGene[] genes)
    {
        if (genes == null) return 0f;

        float[] x = EncodeGenes(genes);
        float[] z1 = new float[hiddenSize];
        float[] h = new float[hiddenSize];

        float yNorm = Forward(x, z1, h);

        if (float.IsNaN(yNorm) || float.IsInfinity(yNorm))
            return 0f;

        // keep normalized output in a sane range
        yNorm = Mathf.Clamp(yNorm, 0f, 2f);    // allow up to 2x target

        float y = yNorm * DIST_SCALE;          // back to world units

        return y;
    }


    // ------------------------------------------------------------------------
    //  LOAD DATASET FROM GOOGLE SHEET AT STARTUP
    // ------------------------------------------------------------------------
    IEnumerator LoadDatasetFromSheet()
    {
        if (string.IsNullOrEmpty(googleSheetsUrl))
            yield break;

        string url = googleSheetsUrl + "?mode=read";

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("BridgeMLModel: Failed to load sheet: " + www.error);
                yield break;
            }

            string json = www.downloadHandler.text;
            int loaded = ParseSheetJsonIntoDataset(json);
            Debug.Log("BridgeMLModel: Loaded " + loaded + " samples from sheet");

            if (loaded > 0)
            {
                // optional warm-up training on sheet data
                Train(epochsPerTrain);
            }
        }
    }

    // JSON from Apps Script is a 2D array like: [[timestamp, distance, x0, x1,...],[...],...]
    int ParseSheetJsonIntoDataset(string json)
    {
        if (string.IsNullOrEmpty(json)) return 0;

        json = json.Trim();
        if (!json.StartsWith("[") || !json.EndsWith("]"))
            return 0;

        // strip outer [ ]
        json = json.Substring(1, json.Length - 2).Trim();
        if (json.Length == 0) return 0;

        // split rows on "],["
        string[] rowParts = json.Split(new string[] { "],[" }, System.StringSplitOptions.RemoveEmptyEntries);
        int added = 0;

        for (int r = 0; r < rowParts.Length; r++)
        {
            string row = rowParts[r];

            // strip any leading [ or trailing ]
            if (row.StartsWith("[")) row = row.Substring(1);
            if (row.EndsWith("]")) row = row.Substring(0, row.Length - 1);

            string[] cols = row.Split(',');

            if (cols.Length < 2) continue;

            // col0 = timestamp (ignore)
            // col1 = distance
            float distance;
            if (!TryParseFloat(cols[1], out distance))
                continue;

            // remaining columns are feature vector
            int featureCount = cols.Length - 2;
            if (featureCount <= 0) continue;

            // must match our input size to be usable
            if (featureCount != inputSize)
                continue;

            float[] x = new float[inputSize];
            bool ok = true;

            for (int i = 0; i < inputSize; i++)
            {
                if (!TryParseFloat(cols[i + 2], out x[i]))
                {
                    ok = false;
                    break;
                }
            }

            if (!ok) continue;

            // append to dataset WITHOUT re-uploading / re-saving
            if (inputs.Count >= maxSamples)
            {
                inputs.RemoveAt(0);
                targets.RemoveAt(0);
            }

            inputs.Add(x);
            targets.Add(distance / DIST_SCALE);
            added++;
        }

        globalGeneration += added;
        return added;
    }

    bool TryParseFloat(string s, out float v)
    {
        s = s.Trim();
        if (s.StartsWith("\"")) s = s.Trim('"');
        return float.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out v);
    }

    // OPTIONAL: bulk save of current dataset to CSV
    public void SaveToCsv()
    {
        if (inputs.Count == 0) return;

        string folder = Path.Combine(Application.persistentDataPath, csvFolderName);
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = csvFilePrefix + "_" + timestamp + ".csv";
        string path = Path.Combine(folder, fileName);

        StringBuilder sb = new StringBuilder();

        sb.Append("distance");
        for (int i = 0; i < inputSize; i++)
        {
            sb.Append(",x");
            sb.Append(i);
        }
        sb.AppendLine();

        for (int n = 0; n < inputs.Count; n++)
        {
            float distance = targets[n] * DIST_SCALE;
            float[] x = inputs[n];

            sb.Append(distance.ToString("G9"));
            for (int i = 0; i < inputSize; i++)
            {
                sb.Append(",");
                sb.Append(x[i].ToString("G9"));
            }
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString());
    }
}
