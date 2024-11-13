using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BMUtil
{
    public partial class SaveManager : Node
    {
        private static SaveManager instance;
        public static SaveManager Instance { get { return instance; } }
        public SaveManager()
        {
            if (instance == null)
                instance = this;
        }

        public event Action NewSaveGenerated;
        public event Action SaveEverything;
        public event Action ShutdownSave;
        public event Action LoadSave;
        public event Action<Node> LoadedNode;
        public event Action<Node> PreNodeSaved;
        public event Action<Node> PostNodeSaved;


        public static StringName NotPersistentGroup = new StringName("NotPersistent");
        public static StringName IgnoreGroup = new StringName("IgnoreGroup");
        public static StringName IgnoreChildrenGroup = new StringName("IgnoreChildren");


        /// <summary> Game starts up on 'static', this is the save file for no active game. </summary>
        public string CurrentSave { get; private set; } = null;

        public enum SaveDest
        {
            Resource,
            Level,
            Player
        }

        public bool CreateSave(string saveName)
        {
            string priorSave = CurrentSave;
            if (FileManager.DirExists("saves/" + saveName))
            {
                FileManager.RemoveDir("/saves/" + saveName);
                /* could modify to return false, etc
                */
            }
            CurrentSave = saveName;
            InstantiateFolders();
            NewSaveGenerated?.Invoke();
            return true;
        }

        private void InstantiateFolders()
        {
            string[] folderNames = Enum.GetNames(typeof(SaveDest));

            string userPath = OS.GetUserDataDir() + "/";


            foreach (string folderName in folderNames)
            {
                Directory.CreateDirectory(userPath + $"saves/{CurrentSave}/{folderName}");
            }

        }

        public void SaveAll()
        {
            SaveEverything?.Invoke();
        }

        public bool LoadInSave(string saveName)
        {
            if (!FileManager.DirExists("saves/" + saveName))
                return false;

            if (CurrentSave != String.Empty)
            {
                ShutdownSave?.Invoke();
            }
            CurrentSave = saveName;

            LoadSave?.Invoke();
            return true;
        }

        public void Save(Node rootNode, SaveDest dest, bool seperateThread = true)
        {
            PreNodeSaved?.Invoke(rootNode);
            JsonValue data = ConvertNodeToJson(rootNode);
            string name = rootNode.Name;
            SaveData(name, data, dest, seperateThread);
            PostNodeSaved?.Invoke(rootNode);
        }
        public void SaveData(string saveName, JsonValue data, SaveDest dest, bool sepertateThread = true)
        {
            string folderName = dest.ToString();

            //data[_RealTimeStamp].Set(TimeManager.Instance.SerializeRealTime());
            //data[_GameTimeStamp].Set(TimeManager.Instance.SerializeGameTime());
            if (sepertateThread)
                FileManager.SaveToFileFormattedAsync(data, $"saves/{CurrentSave}/{folderName}/{saveName}");
            else
                FileManager.SaveToFileFormatted(data, $"saves/{CurrentSave}/{folderName}/{saveName}");
        }
        public Node Load(string fileName, SaveDest dest)
        {
            JsonValue data = LoadData(fileName, dest);
            Node node = ConvertJsonToNode(data);
            // now can apply time
            
            LoadedNode?.Invoke(node);

            return node;
        }
        public JsonValue LoadData(string fileName, SaveDest dest)
        {
            string fileHolder = dest.ToString();

            return FileManager.LoadFromFile($"saves/{CurrentSave}/{fileHolder}/{fileName}");
        }



        #region HASH
        static string GetHash(string inputString)
        {
            byte[] hashBytes;
            using (HashAlgorithm algorithm = SHA256.Create())
                hashBytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));

            return BitConverter
                    .ToString(hashBytes)
                    .Replace("-", String.Empty);
        }

        static private void AddHash(ref JsonValue obj)
        {
            obj.Remove("hash");
            string hash = GetHash(obj.ToString());
            obj["hash"].Set(hash);
        }
        private bool HashMatches(JsonValue obj)
        {
            string hashStored = obj["hash"].AsString();
            obj.Remove("hash");

            return hashStored == GetHash(obj.ToString());
        }

        #endregion


        #region Converting Data

        private static readonly string _Name = "N";
        private static readonly string _Type = "T";
        private static readonly string _DerivedType = "DT";
        private static readonly string _Position = "P";
        private static readonly string _Rotation = "R";
        private static readonly string _Scale = "S";
        private static readonly string _Size = "SZ";
        private static readonly string _ZIndex = "ZI";
        private static readonly string _ZIsRelative = "ZIR";
        private static readonly string _YSortEnabled = "YSE";
        private static readonly string _Meta = "M";
        private static readonly string _Group = "G";
        private static readonly string _Children = "C";
        private static readonly string _ISaveData = "ISD";

        public static JsonValue ConvertNodeToJson(Node node)
        {
            JsonValue val = CollectNodeData(node);
            return val;
        }

        private static JsonValue CollectNodeData(Node node)
        {
            JsonValue jsonNode = new JsonValue();

            if (node.IsInGroup(NotPersistentGroup))
                return new JsonValue();
            if (node.GetParent().IsValid())
                if (node.GetParent().IsInGroup(IgnoreChildrenGroup) ||
                    node.GetParent().IsInGroup(IgnoreChildrenGroup))
                    return new JsonValue();

            jsonNode[_Name].Set(node.Name);
            jsonNode[_Type].Set(Globals.RemoveNamespace(node.GetType().ToString()));
            jsonNode[_DerivedType].Set(node.GetClass());

            if (node is Node2D)
            {
                Node2D node2d = (Node2D)node;
                jsonNode[_ZIsRelative].Set(node2d.ZAsRelative);
                jsonNode[_YSortEnabled].Set(node2d.YSortEnabled);
                jsonNode[_ZIndex].Set(node2d.ZIndex);

                jsonNode[_Position].Set(node2d.Position);
                jsonNode[_Rotation].Set(node2d.Rotation);
                jsonNode[_Scale].Set(node2d.Scale);
            }
            else if (node is Node3D)
            {
                Node3D node3d = (Node3D)node;

                jsonNode[_Position].Set(node3d.Position);
                jsonNode[_Rotation].Set(node3d.Rotation);
                jsonNode[_Scale].Set(node3d.Scale);
            }

            foreach (string meta in node.GetMetaList())
            {
                jsonNode[_Meta][meta].Set(node.GetMeta(meta).AsString());
            }
            foreach (string group in node.GetGroups()) // not accessible outside main
                jsonNode[_Group].Append(group);

            for (int i = 0; i < node.GetChildCount(); i++) // not accessible outside main
                jsonNode[_Children].Append(CollectNodeData(node.GetChild(i)));

            if (node is ISaveData)
                jsonNode[_ISaveData].Set((node as ISaveData).SerializeSaveData());

            return jsonNode;
        }

        public static Node ConvertJsonToNode(JsonValue data)
        {
            Node node = (Node)ClassDB.Instantiate(data[_DerivedType].AsString());
            // Set Basic Node Data
            node.Name = data[_Name].AsString();
            if (node is Node2D node2d)
            {
                node2d.Position = data[_Position].AsVector2();
                node2d.Rotation = data[_Rotation].AsFloat();
                node2d.Scale = data[_Scale].AsVector2();

                node2d.ZIndex = data[_ZIndex].AsInt();
                node2d.ZAsRelative = data[_ZIsRelative].AsBool();
                node2d.YSortEnabled = data[_YSortEnabled].AsBool();
            }
            else if (node is Node3D node3d)
            {
                node3d.Position = data[_Position].AsVector3();
                node3d.Rotation = data[_Rotation].AsVector3();
                node3d.Scale = data[_Scale].AsVector3();
            }

            // Save node instance id to re-reference after setting script
            ulong nodeID = node.GetInstanceId();
            // if type != derived-type, a script is attached
            if (!data[_Type].AsString().Equals(data[_DerivedType].AsString()))
            {
                node.SetScript(ResourceManager.Instance.GetResourceByName<Script>(data[_Type].AsString()));
            }

            node = GodotObject.InstanceFromId(nodeID) as Node;

            foreach (KeyValuePair<string, JsonValue> meta in data[_Meta].Object)
                node.SetMeta(meta.Key, meta.Value.AsString());
            foreach (JsonValue group in data[_Group].Array)
                node.AddToGroup(group.AsString());

            foreach (JsonValue child in data[_Children].Array)
                node.AddChild(ConvertJsonToNode(child));

            if (node is ISaveData isd)
                isd.DeserializeSaveData(data[_ISaveData]);
            return node;
        }

        #endregion

    }

    public interface ISaveData
    {
        public JsonValue SerializeSaveData();
        public void DeserializeSaveData(JsonValue data);
    }

}