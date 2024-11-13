using Godot;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
namespace BMUtil
{
    public partial class LevelManager : Node
    {
        private static LevelManager instance;
        public static LevelManager Instance { get { return instance; } }

        public LevelManager()
        {
            if (instance == null)
                instance = this;
            PositionsOccupied = new bool[MaxConcurrentLevels];
        }

        public static readonly StringName LevelMeta = new StringName("Level");

        public event Action<Node, Node> PlayerChangeLevel;

        /// <summary> Contains a list of all levels which exist in the current save. </summary>
        private List<string> AllLevels = new List<string>();

        private Dictionary<string, LevelPartition> ActiveLevels = new Dictionary<string, LevelPartition>();

        bool[] PositionsOccupied;
        /// <summary> Max number of levels open at the same time. Limiting this number will improve performance. </summary>
        private int MaxConcurrentLevels { get; set; } = 4;
        private bool UseOffsets { get; set; } = true;

        public override void _Ready()
        {
            base._Ready();
            SaveManager.Instance.NewSaveGenerated += CreateLevelDir;
            SaveManager.Instance.SaveEverything += SaveAllLevels;
            SaveManager.Instance.ShutdownSave += CloseAllLevels;
        }

        private void CreateLevelDir()
        {
            foreach (KeyValuePair<string, string> filePath in ResourceManager.Instance.LevelPaths)
            {
                Node root = ResourceManager.Instance.GetResourceByPath<PackedScene>(filePath.Value).Instantiate();
                if (!root.IsInGroup(LevelMeta))
                    root.AddToGroup(LevelMeta);

                SaveManager.Instance.Save(root, SaveManager.SaveDest.Level, false);
                root.QueueFree();
            }
        }

        public bool LevelExists(string name)
        {
            return AllLevels.Contains(name);
        }
        public bool LevelActive(string name)
        {
            return ActiveLevels.ContainsKey(name);
        }


        public void SaveAllLevels()
        {
            foreach (var val in ActiveLevels.Keys)
                SaveLevelPartition(val);
        }

        private void CloseAllLevels()
        {
            foreach (var val in ActiveLevels.Keys)
                CloseLevelPartition(val);
        }


        public void SaveLevelPartition(string levelName)
        {
            if (!ActiveLevels.ContainsKey(levelName)) { return; }

            var level = ActiveLevels[levelName];
            level.LevelRoot.Position -= level.Offset;


            SaveManager.Instance.Save(level.LevelRoot, SaveManager.SaveDest.Level);
            level.LevelRoot.Position += level.Offset;

        }
        public void LoadLevelPartition(string levelName)
        {
            if (ActiveLevels.ContainsKey(levelName))
                return;

            if (ActiveLevels.Count >= MaxConcurrentLevels)
            {
                GD.Print("Attempting to load too many scenes, reach MaxConcurrentScenes limit of " + MaxConcurrentLevels);
                return;
            }
            Node3D level = SaveManager.Instance.Load(levelName, SaveManager.SaveDest.Level) as Node3D;

            Vector3 offset = Vector3.Zero;
            // only apply a offset if this scene is not a Control and we want to use offsets
            int offsetIndex = -1;


            LevelPartition lp = new LevelPartition(level, offset);
            lp.PositionIndex = offsetIndex;

            ActiveLevels.Add(levelName, lp);

            level.Position += offset;
            GetTree().CurrentScene.AddChild(level);
        }

        public void AddLevelNameAttachment(Node node, string levelName)
        {
            node.SetMeta(LevelPartition.LevelPartitionNameMeta, levelName);
        }

        public void SaveAndCloseLevelPartition(string levelName)
        {
            if (!ActiveLevels.ContainsKey(levelName)) { return; }
            SaveLevelPartition(levelName);
            CloseLevelPartition(levelName);
        }

        public void CloseLevelPartition(string levelName)
        {
            if (!ActiveLevels.ContainsKey(levelName)) { return; }
            if (ActiveLevels[levelName].PositionIndex != -1)
                PositionsOccupied[ActiveLevels[levelName].PositionIndex] = false;
            ActiveLevels[levelName].LevelRoot.QueueFree();
            ActiveLevels.Remove(levelName);
        }

        /// <summary>
        /// Converts a location in local units to scene's true position.
        /// Eg. Local Pos: (0, 20, 0), Scene Pos: (5000, 20, 0)
        /// </summary>
        /// <returns></returns>
        public Vector3 LocalPos2ScenePos(Vector3 position, string scene)
        {
            return position + ActiveLevels[scene].Offset;
        }

    }


    public class LevelPartition
    {
        public readonly static StringName LevelPartitionNameMeta = new StringName("LPN");
        public LevelPartition(Node3D root, Vector3 offset)
        {
            LevelRoot = root;
            Offset = offset;
            PositionIndex = (int)(offset.X / 5000f);
        }

        public Node3D LevelRoot { get; private set; }
        public Vector3 Offset { get; private set; }

        public int PositionIndex { get; set; } = -1;

    }

}