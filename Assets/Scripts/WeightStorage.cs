using UnityEngine;
using System.IO;

public static class WeightStorage
{
    static string Folder => Application.persistentDataPath + "/PBTWeights/";

    public static void Save(string filename, string content)
    {
        if (!Directory.Exists(Folder))
            Directory.CreateDirectory(Folder);

        File.WriteAllText(Folder + filename, content);
    }

    public static string Load(string filename)
    {
        string path = Folder + filename;
        if (!File.Exists(path))
            return null;

        return File.ReadAllText(path);
    }
}
