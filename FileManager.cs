using Godot;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
public enum FileType
{
    User,
    Res
}
public static class FileManager
{

    public static JsonValue LoadFromFile(string filePath, FileType ft = FileType.User)
    {
        if (!filePath.Contains(".json"))
            filePath += ".json";

        JsonValue root = new JsonValue();

        if (ft == FileType.User)
        {
            string userPath = OS.GetUserDataDir() + "/";

            string file = File.ReadAllText(userPath + filePath);
            root.Parse(file);
        }
        else
        {
            using var file = Godot.FileAccess.Open("res://" + filePath, Godot.FileAccess.ModeFlags.Read);
            
            string content = file.GetAsText();
            root.Parse(content);
        }

        return root;
    }

    public static void SaveToFile(JsonValue obj, string filePath, FileType ft = FileType.User)
    {
        SaveToFileHelper(obj, filePath, ft, false);
    }

    public static void SaveToFileAsync(JsonValue obj, string filePath, FileType ft = FileType.User)
    {
        Task.Run(() => 
        { 
            SaveToFileHelper(obj, filePath, ft, false);
        });
    }

    public static void SaveToFileFormatted(JsonValue obj, string filePath, FileType ft = FileType.User)
    {
        SaveToFileHelper(obj, filePath, ft, true);
    }

    public static void SaveToFileFormattedAsync(JsonValue obj, string filePath, FileType ft = FileType.User)
    {
        Task.Run(() =>
        {
            SaveToFileHelper(obj, filePath, ft, true);
        });
    }

    public static List<string> GetFiles(string dirPath)
    {
        string userPath = OS.GetUserDataDir() + "/";
        string path = userPath + dirPath;

        List<string> result = Directory.GetFiles(path).ToList();
        for(int i = 0; i < result.Count; i++)
        {
            int index = Mathf.Max(result[i].RFind("/"), result[i].RFind("\\"));
            result[i] = result[i].Substring(index+1);
        }
        return result;
        
    }

    public static bool FileExists(string filePath, FileType ft = FileType.User)
    {
        if (!filePath.Contains(".json"))
            filePath += ".json";

        if (ft == FileType.User)
            return File.Exists(OS.GetUserDataDir() + "/" + filePath);
        else
            return Godot.FileAccess.FileExists("res://" + filePath);
    }

    public static bool DirExists(string filePath, FileType ft = FileType.User)
    {
        if (ft == FileType.User)
        {
            return Directory.Exists(OS.GetUserDataDir() + "/" + filePath);
        }
        else
            return Godot.DirAccess.DirExistsAbsolute("res://" + filePath);
    }

    public static void RemoveDir(string dirPath)
    {
        if (!Directory.Exists(OS.GetUserDataDir() + "/" + dirPath))
            return;

        Directory.Delete(OS.GetUserDataDir() + "/" + dirPath, true);
    }

    private static async Task SaveToFileHelper(JsonValue obj, string filePath, FileType ft, bool formatted)
    {
        if (!OS.HasFeature("editor") && ft == FileType.Res)
            throw new Exception("[FileManager][Error] Attempting to save to res outside editor");

        if (!filePath.Contains(".json"))
            filePath += ".json";

        if (ft == FileType.User)
        {
            string userPath = OS.GetUserDataDir() + "/";
            if (!File.Exists(userPath + filePath))
            {
                int lastSlash = filePath.RFind("/") + 1;
                string dirPath = OS.GetUserDataDir() + "/" + filePath.Substring(0, lastSlash);
                string fileName = filePath.Substring(lastSlash);


                Directory.CreateDirectory(dirPath);

                if (formatted)
                    File.WriteAllText(dirPath + fileName, obj.ToFormattedString());
                else
                    File.WriteAllText(dirPath + fileName, obj.ToString());

            }
            else
            {
                StreamWriter sw = new StreamWriter(userPath + filePath);
                if (formatted)
                    sw.Write(obj.ToFormattedString());
                else
                    sw.Write(obj.ToString());

                sw.Close();
            }
        }
        else
        {
            using var file = Godot.FileAccess.Open("res://" + filePath, Godot.FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.Print("[FileManager][Error]Res file undiscoverable");
                return;
            }
            else
            {
                if (formatted)
                    file.StoreString(obj.ToFormattedString());
                else
                    file.StoreString(obj.ToString());
            }
        }
    }


}
