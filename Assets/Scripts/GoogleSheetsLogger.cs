using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MiniJSON;

public static class GoogleSheetsLogger
{
    private static string webAppUrl = "https://script.google.com/macros/s/AKfycbwurT0M2gtmLYB8YcOCnBr5VlclS0q1ff-ivmoeR5bpqmofEIUn2DPmhTO1A6K5tk-7wQ/exec";

    public static IEnumerator LogEpisode(
        int globalGeneration,
        int modelIndex,
        float fitness,
        float learningRate,
        float noise,
        string weightsString,
        BridgeGene[] genes)
    {
        Dictionary<string, object> payload = new Dictionary<string, object>();
        payload["globalGeneration"] = globalGeneration;
        payload["modelIndex"] = modelIndex;
        payload["fitness"] = fitness;
        payload["learningRate"] = learningRate;
        payload["noise"] = noise;
        payload["weights"] = weightsString;

        // Serialize genes properly
        List<object> geneList = new List<object>();
        foreach (var g in genes)
        {
            var d = new Dictionary<string, object>();
            d["sx"] = g.start.x;
            d["sy"] = g.start.y;
            d["ex"] = g.end.x;
            d["ey"] = g.end.y;
            d["type"] = g.type.ToString();
            geneList.Add(d);
        }
        payload["rawGenes"] = geneList;

        string json = MiniJSON.Json.Serialize(payload);

        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

        var request = new UnityEngine.Networking.UnityWebRequest(webAppUrl, "POST");
        request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(body);
        request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        Debug.Log("SheetsLogger: HTTP " + request.responseCode);
        Debug.Log("SheetsLogger sent JSON: " + json);
    }
}
