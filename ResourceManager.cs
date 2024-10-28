using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Util
{
    public partial class ResourceManager : Node
    {
        private static ResourceManager instance = null;

        private Dictionary<string, string> scriptPaths = new Dictionary<string, string>();

        private Dictionary<string, string> levelPaths = new Dictionary<string, string>();
        private Dictionary<string, string> scenePaths = new Dictionary<string, string>();

        private Dictionary<string, string> texturePaths = new Dictionary<string, string>();
        private Dictionary<string, string> modelPaths = new Dictionary<string, string>();

        private Dictionary<string, string> audioPaths = new Dictionary<string, string>();

        private Dictionary<string, string> resourcePaths = new Dictionary<string, string>();

        private Dictionary<string, string> shaderPaths = new Dictionary<string, string>();

        public Dictionary<string, string> LevelPaths { get { return levelPaths; } }

        private List<(Resource, int)> LoadedResources = new List<(Resource, int)>();
        /// <summary>
        /// Adds a resource to a List of registered resources. These resources will remain readily available in memory until 
        /// reference count is zero (by calling RemoveResource).
        /// </summary>
        public void AddResource(Resource resource)
        {
            for (int i = 0; i < LoadedResources.Count; i++)
            {
                if (LoadedResources[i].Item1 == resource)
                {
                    var x = LoadedResources[i];
                    x.Item2 += 1;
                    LoadedResources[i] = x;
                    return;
                }
            }
            LoadedResources.Add((resource, 1));
        }
        /// <summary>
        /// Reduce reference count of resource stored in memory. If reaches zero, the resource will be removed from memory.
        /// </summary>
        public void RemoveResource(Resource resource)
        {
            for (int i = 0; i < LoadedResources.Count; i++)
            {
                if (LoadedResources[i].Item1 == resource)
                {
                    var x = LoadedResources[i];
                    x.Item2 -= 1;
                    if (x.Item2 > 0)
                        LoadedResources[i] = x;
                    else
                        LoadedResources.RemoveAt(i);
                    return;
                }
            }
        }
        /// <summary>
        /// Returns a resource by the full file path provided.
        /// </summary>
        public T GetResourceByPath<T>(string path) where T : Resource
        {
            IEnumerable<(Resource, int)> x = LoadedResources.Where(s => s.Item1.ResourcePath == path);
            if (x.Count() == 0)
                return GD.Load<T>(path);

            return (T)x.First().Item1;
        }
        /// <summary>
        /// Returns a resource by the name of the resource file.
        /// </summary>
        public T GetResourceByName<T>(string name) where T : Resource
        {
            IEnumerable<(Resource, int)> x = LoadedResources.Where(s => s.Item1.ResourcePath.RemovePathAndFileType() == name && typeof(T).IsAssignableFrom(s.Item1.GetType()));

            if (x.Count() == 0)
            {
                // need to determine what type of resource this is
                if (scenePaths.ContainsKey(name) && (typeof(T).IsAssignableFrom(typeof(PackedScene)) || typeof(T).IsAssignableTo(typeof(PackedScene))))
                {
                    return GD.Load<T>(scenePaths[name]);
                }
                else if (modelPaths.ContainsKey(name) && (typeof(T).IsAssignableFrom(typeof(PackedScene)) || typeof(T).IsAssignableTo(typeof(PackedScene))))
                {
                    return GD.Load<T>(modelPaths[name]);
                }
                else if (texturePaths.ContainsKey(name) && (typeof(T).IsAssignableFrom(typeof(Texture)) || typeof(T).IsAssignableTo(typeof(Texture))))
                {
                    return GD.Load<T>(texturePaths[name]);
                }
                else if (audioPaths.ContainsKey(name) && (typeof(T).IsAssignableFrom(typeof(Resource)) || typeof(T).IsAssignableTo(typeof(Resource))))
                {
                    return GD.Load<T>(audioPaths[name]);
                }
                else if (levelPaths.ContainsKey(name) && (typeof(T).IsAssignableFrom(typeof(PackedScene)) || typeof(T).IsAssignableTo(typeof(PackedScene))))
                {
                    return GD.Load<T>(levelPaths[name]);
                }
                else if (shaderPaths.ContainsKey(name) && (typeof(T).IsAssignableFrom(typeof(Shader)) || typeof(T).IsAssignableTo(typeof(Shader))))
                {
                    return GD.Load<T>(shaderPaths[name]);
                }
                else if (scriptPaths.ContainsKey(name) && (typeof(T).IsAssignableFrom(typeof(Script)) || typeof(T).IsAssignableTo(typeof(Script))))
                {
                    return GD.Load<T>(scriptPaths[name]);
                }
                else if (resourcePaths.ContainsKey(name) && (typeof(T).IsAssignableFrom(typeof(Resource)) || typeof(T).IsAssignableTo(typeof(Resource))))
                {
                    return GD.Load<T>(resourcePaths[name]);
                }
                else
                {
                    GD.Print(name);
                    return default(T);
                }
            }
            else
            {
                return (T)x.First().Item1;
            }
        }

        private ResourceManager()
        {
            if (instance == null)
                instance = this;
        }

        public static ResourceManager Instance { get { return instance; } }

        public override void _Ready()
        {
            if (OS.HasFeature("editor"))
            {
                SetDicts();
                JsonValue root = new JsonValue();

                foreach (KeyValuePair<string, string> script in Instance.scriptPaths)
                    root["scripts"].Add(script.Key, script.Value);
                foreach (KeyValuePair<string, string> scene in Instance.scenePaths)
                    root["scenes"].Add(scene.Key, scene.Value);
                foreach (KeyValuePair<string, string> level in Instance.levelPaths)
                    root["levels"].Add(level.Key, level.Value);
                foreach (KeyValuePair<string, string> texture in Instance.texturePaths)
                    root["textures"].Add(texture.Key, texture.Value);
                foreach (KeyValuePair<string, string> model in Instance.modelPaths)
                    root["models"].Add(model.Key, model.Value);
                foreach (KeyValuePair<string, string> resource in Instance.resourcePaths)
                    root["resources"].Add(resource.Key, resource.Value);
                foreach (KeyValuePair<string, string> audio in Instance.audioPaths)
                    root["audio"].Add(audio.Key, audio.Value);
                foreach (KeyValuePair<string, string> shader in Instance.shaderPaths)
                    root["shaders"].Add(shader.Key, shader.Value);

                FileManager.SaveToFileFormatted(root, "ResourceManager.json", FileType.Res);
            }
            else
            {
                JsonValue root = FileManager.LoadFromFile("ResourceManager.json", FileType.Res);

                foreach (KeyValuePair<string, JsonValue> pair in root["scripts"].Object)
                    Instance.scriptPaths[pair.Key] = pair.Value.AsString();
                foreach (KeyValuePair<string, JsonValue> pair in root["scenes"].Object)
                    Instance.scenePaths[pair.Key] = pair.Value.AsString();
                foreach (KeyValuePair<string, JsonValue> pair in root["levels"].Object)
                    Instance.levelPaths[pair.Key] = pair.Value.AsString();
                foreach (KeyValuePair<string, JsonValue> pair in root["textures"].Object)
                    Instance.texturePaths[pair.Key] = pair.Value.AsString();
                foreach (KeyValuePair<string, JsonValue> pair in root["models"].Object)
                    Instance.modelPaths[pair.Key] = pair.Value.AsString();
                foreach (KeyValuePair<string, JsonValue> pair in root["resources"].Object)
                    Instance.resourcePaths[pair.Key] = pair.Value.AsString();
                foreach (KeyValuePair<string, JsonValue> pair in root["audio"].Object)
                    Instance.audioPaths[pair.Key] = pair.Value.AsString();
                foreach (KeyValuePair<string, JsonValue> pair in root["shaders"].Object)
                    Instance.shaderPaths[pair.Key] = pair.Value.AsString();
            }

            foreach (var c in scriptPaths)
            {
                if (c.Value.Contains("autload"))
                    continue;
                if (c.Value.Contains("classes"))
                    continue;
                if (c.Value.Contains("ui"))
                    continue;
                if (c.Value.Contains("resources"))
                    continue;
                AddResource(GD.Load<Script>(c.Value));
            }
        }

        private void SetDicts()
        {
            string[] filePaths = Directory.GetFiles(Directory.GetCurrentDirectory() + "\\", "*", SearchOption.AllDirectories);

            foreach (string filePath in filePaths)
            {
                // ignore hidden files
                if (filePath[Directory.GetCurrentDirectory().Length + 1] == '.')
                    continue;

                int suffixStart = filePath.RFind(".");
                string suffix = filePath.Substring(suffixStart);

                int index = filePath.RFind("\\") + 1;
                if (index == -1) continue;

                string key = "";
                string value = "";

                if (suffix == ".cs")
                {
                    // script
                    key = filePath.Substring(index, filePath.Length - index - 3);
                    value = filePath.Replace(Directory.GetCurrentDirectory() + "\\", "");
                    value = value.Replace("\\", "/");
                    if (!Instance.scriptPaths.TryAdd(key, value))
                        GD.Print("[ReferenceManager] Error: Duplicate script names: " + key);
                }
                else if (suffix == ".tscn")
                {
                    key = filePath.Substring(index, filePath.Length - index - 5);
                    value = filePath.Replace(Directory.GetCurrentDirectory() + "\\", "");
                    value = value.Replace("\\", "/");
                    if (value.Contains("levels"))
                    {
                        if (!Instance.levelPaths.TryAdd(key, value))
                            GD.Print("[ReferenceManager] Error: Duplicate prefab names: " + key);
                    }
                    else
                    {
                        if (!Instance.scenePaths.TryAdd(key, value))
                            GD.Print("[ReferenceManager] Error: Duplicate scene names: " + key);
                    }
                }
                else if (suffix == ".png")
                {
                    // texture
                    key = filePath.Substring(index, filePath.Length - index - 4);
                    value = filePath.Replace(Directory.GetCurrentDirectory() + "\\", "");
                    value = value.Replace("\\", "/");
                    if (!Instance.texturePaths.TryAdd(key, value))
                        GD.Print("[ReferenceManager] Error: Duplicate texture names: " + key);
                }
                else if (suffix == ".glb" || suffix == ".obj")
                {
                    // models
                    key = filePath.Substring(index, filePath.Length - index - 4);
                    value = filePath.Replace(Directory.GetCurrentDirectory() + "\\", "");
                    value = value.Replace("\\", "/");
                    if (!Instance.modelPaths.TryAdd(key, value))
                        GD.Print("[ReferenceManager] Error: Duplicate model names: " + key);
                }
                else if (suffix == ".tres")
                {
                    // resource
                    key = filePath.Substring(index, filePath.Length - index - 5);
                    value = filePath.Replace(Directory.GetCurrentDirectory() + "\\", "");
                    value = value.Replace("\\", "/");
                    if (!Instance.resourcePaths.TryAdd(key, value))
                        GD.Print("[ReferenceManager] Error: Duplicate resource names: " + key);
                }
                else if (suffix == ".wav" || suffix == ".ogg")
                {
                    // audio
                    key = filePath.Substring(index, filePath.Length - index - 4);
                    value = filePath.Replace(Directory.GetCurrentDirectory() + "\\", "");
                    value = value.Replace("\\", "/");
                    if (!Instance.audioPaths.TryAdd(key, value))
                        GD.Print("[ReferenceManager] Error: Duplicate audio names: " + key);
                }
                else if (suffix == ".gdshader")
                {
                    // shader
                    key = filePath.Substring(index, filePath.Length - index - 9);
                    value = filePath.Replace(Directory.GetCurrentDirectory() + "\\", "");
                    value = value.Replace("\\", "/");
                    if (!Instance.shaderPaths.TryAdd(key, value))
                        GD.Print("[ReferenceManager] Error: Duplicate shader names: " + key);
                }
            }
        }
    }
}