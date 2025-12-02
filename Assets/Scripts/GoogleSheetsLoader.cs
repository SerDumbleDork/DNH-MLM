using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MiniJSON;

public static class GoogleSheetsLoader
{
    private static string webAppUrl = "https://script.google.com/macros/s/AKfycbwurT0M2gtmLYB8YcOCnBr5VlclS0q1ff-ivmoeR5bpqmofEIUn2DPmhTO1A6K5tk-7wQ/exec";

    public static IEnumerator LoadAllEntries(System.Action<List<Dictionary<string, object>>> callback)
    {
        var request = UnityEngine.Networking.UnityWebRequest.Get(webAppUrl);

        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            Debug.LogError("SheetsLoader Error: " + request.error);
            callback(null);
            yield break;
        }

        string json = request.downloadHandler.text;

        var list = Json.Deserialize(json) as List<object>;
        var result = new List<Dictionary<string, object>>();

        foreach (var item in list)
        {
            result.Add(item as Dictionary<string, object>);
        }

        callback(result);
    }
}
