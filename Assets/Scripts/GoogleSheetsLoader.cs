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

        var parsed = Json.Deserialize(json);
        List<object> rawList = null;
        if (parsed is List<object> directList)
        {
            rawList = directList;
        }
        else if (parsed is Dictionary<string, object> dict)
        {
            foreach (var kv in dict)
            {
                if (kv.Value is List<object> innerList)
                {
                    rawList = innerList;
                    break;
                }
            }

            if (rawList == null)
            {                
                Debug.LogError("SheetsLoader Error: JSON dictionary did not contain a list.");


                callback(null);
                yield break;
            }                
        }
        else
        {
            Debug.LogError("SheetsLoader Error: JSON was neither a list nor dictionary.");
            callback(null);
            yield break;
        }
        List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();

        foreach (var item in rawList)
        {
            if (item is Dictionary<string, object> rowDict)
            {
                rows.Add(rowDict);
            }
        }

        callback(rows);
    }
}
