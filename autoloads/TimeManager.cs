using Godot;
using System;
using System.Collections.Generic;
namespace BMUtil
{
    public partial class TimeManager : Node
    {
        private static TimeManager instance;
        public static TimeManager Instance { get { return instance; } }


        public TimeManager()
        {
            if (instance == null)
                instance = this;
        }

        public event Action GameTimeToggled;

        bool gameTimeRunning = true;
        public bool GameTimeRunning
        {
            get
            {
                return gameTimeRunning;
            }
            set
            {
                bool changed = false;
                if (gameTimeRunning != value)
                    changed = true;
                gameTimeRunning = value;
                if (changed)
                    GameTimeToggled?.Invoke();
            }
        }
        private GameTime gameTime = new GameTime();
        public GameTime GameTime { get { return gameTime; } }
        public override void _Ready()
        {
            base._Ready();

            SaveManager.Instance.NewSaveGenerated += OnSaveGame;
            SaveManager.Instance.LoadSave += OnSaveLoaded;
            SaveManager.Instance.SaveEverything += OnSaveGame;

            SaveManager.Instance.PreNodeSaved += OnPreNodeSaved;
            SaveManager.Instance.LoadedNode += OnNodeLoaded;

        }

        protected static readonly StringName GameTimeMeta = new StringName("GameTime");

        protected static readonly StringName RealYear = new StringName("RealYear");
        protected static readonly StringName RealMonth = new StringName("RealMonth");
        protected static readonly StringName RealDay = new StringName("RealDay");
        protected static readonly StringName RealHour = new StringName("RealHour");
        protected static readonly StringName RealMinute = new StringName("RealMinute");
        protected static readonly StringName RealSecond = new StringName("RealSecond");

        private void OnPreNodeSaved(Node node)
        {
            node.SetMeta(GameTimeMeta, gameTime.TotalGameSec);

            DateTime now = DateTime.Now;

            node.SetMeta(RealYear, now.Year);
            node.SetMeta(RealMonth, now.Month);
            node.SetMeta(RealDay, now.Day);
            node.SetMeta(RealHour, now.Hour);
            node.SetMeta(RealMinute, now.Minute);
            node.SetMeta(RealSecond, now.Second);
        }

        private void OnNodeLoaded(Node node)
        {
            double nodeGameTimeElapsed = node.GetMeta(GameTimeMeta).AsDouble();

            List<IGameTimeTracker> gameTimeTrackers = new List<IGameTimeTracker>();
            gameTimeTrackers = node.GetAllChildren<IGameTimeTracker>();
            if (node is IGameTimeTracker gtt)
                gameTimeTrackers.Add(gtt);

            DateTime pastTime = new DateTime(node.GetMeta(RealYear).AsInt32(), node.GetMeta(RealMonth).AsInt32(), node.GetMeta(RealDay).AsInt32(),
                node.GetMeta(RealHour).AsInt32(), node.GetMeta(RealMinute).AsInt32(), node.GetMeta(RealSecond).AsInt32());
            
            TimeSpan tm = DateTime.Now - pastTime;
            double nodeRealTimeElapsed = tm.TotalSeconds;

            List<IRealTimeTracker> realTimeTrackers = new List<IRealTimeTracker>();
            realTimeTrackers = node.GetAllChildren<IRealTimeTracker>();
            if (node is IRealTimeTracker rtt)
                realTimeTrackers.Add(rtt);

            while(nodeGameTimeElapsed > 0)
            {
                double time = 1;
                if(nodeGameTimeElapsed < time)
                    time = nodeGameTimeElapsed;

                foreach(var tracker in gameTimeTrackers)
                    tracker.ProgressGameTime(time);

                nodeGameTimeElapsed -= 1;
            }

            while(nodeRealTimeElapsed > 0)
            {
                double time = 1;
                if(nodeRealTimeElapsed < time)
                    time = nodeRealTimeElapsed;

                foreach(var tracker in realTimeTrackers)
                    tracker.ProgressRealTime(time);

                nodeRealTimeElapsed -= 1;
            }

        }

        private void OnSaveGame()
        {
            SaveManager.Instance.SaveData("Time", gameTime.Serialize(), SaveManager.SaveDest.Resource);
        }
        private void OnSaveLoaded()
        {
            gameTime.Deserialize(SaveManager.Instance.LoadData("Time", SaveManager.SaveDest.Resource));
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            if (GameTimeRunning)
                gameTime.Update(delta);
        }


        private static readonly string _Year = "Y";
        private static readonly string _Month = "M";
        private static readonly string _Day = "D";
        private static readonly string _Hour = "H";
        private static readonly string _Minute = "MI";
        private static readonly string _Second = "S";
        public JsonValue SerializeRealTime()
        {
            JsonValue data = new JsonValue();

            DateTime now = DateTime.Now;

            data[_Year].Set(now.Year);
            data[_Month].Set(now.Month);
            data[_Day].Set(now.Day);
            data[_Hour].Set(now.Hour);
            data[_Minute].Set(now.Minute);
            data[_Second].Set(now.Second);

            return data;
        }


        /// <summary>
        /// Takes in a new loaded node and applies real time progress to any node extending the IRealTimeTracker interface.
        /// </summary>
        public void ApplyRealTimeProgress(Node root, JsonValue data)
        {
            DateTime pastTime = DeserializeRealTime(data);
            TimeSpan tm = DateTime.Now - pastTime;
            ApplyRealTimeProgress(root, tm.TotalSeconds);
        }

        private void ApplyRealTimeProgress(Node n, double timeDif)
        {
            if (n is IRealTimeTracker ir)
            {
                ir.ProgressRealTime(timeDif);
            }
            foreach (var child in n.GetChildren())
            {
                ApplyRealTimeProgress(child, timeDif);
            }
        }

        private DateTime DeserializeRealTime(JsonValue data)
        {
            return new DateTime(data[_Year].AsInt(), data[_Month].AsInt(), data[_Day].AsInt(),
                data[_Hour].AsInt(), data[_Minute].AsInt(), data[_Second].AsInt());
        }

        private static readonly string _GameTime = "GT";
        public JsonValue SerializeGameTime()
        {
            JsonValue data = new JsonValue();

            //data[_GameTime].Set(gameTime.Serialize());

            return data;
        }

        /// <summary>
        /// Takes in a new loaded node and applies real time progress to any node extending the IRealTimeTracker interface.
        /// </summary>
        public void ApplyGameTimeProgress(Node root, JsonValue data)
        {
            GameTime pastTime = new GameTime();
           // pastTime.Deserialize(data[_GameTime]);
            double tm = gameTime.TotalGameSec - pastTime.TotalGameSec;
            ApplyGameTimeProgress(root, tm);
        }

        private void ApplyGameTimeProgress(Node n, double timeDif)
        {
            if (n is IGameTimeTracker ir)
            {
                ir.ProgressGameTime(timeDif);
            }
            foreach (var child in n.GetChildren())
            {
                ApplyGameTimeProgress(child, timeDif);
            }
        }


    }

    public class GameTime
    {
        private static readonly string _TotalGameSeconds = "TGS";
        int day;
        int hour;
        int minute;
        double totalGameSec;

        public event Action GameMinuteChanged;
        public event Action GameHourChanged;
        public event Action GameDayChanged;

        public double TimeScale { get; set; } = 1f;
        public int Day { get { return day; } }
        public int Hour { get { return hour; } }
        public int Minute { get { return minute; } }
        public double TotalGameSec { get { return totalGameSec; } }

        public void SetDay(int day)
        {
            if (day > this.day)
                totalGameSec += (day * 24 * 60);
            else
                totalGameSec -= (this.day - day) * 24 * 60;
        }
        public void SetHour(int hour)
        {
            if (hour > this.hour)
                totalGameSec += hour * 60;
            else
                totalGameSec -= (this.hour - hour) * 60;
        }
        public void SetMinute(int minute)
        {
            if (minute > this.minute)
                totalGameSec += minute;
            else
                totalGameSec -= this.minute - minute;
        }

        public void Update(double delta)
        {
            totalGameSec += delta * TimeScale;

            var m = minute;
            var h = hour;
            var d = day;

            minute = Mathf.FloorToInt(totalGameSec) % 60;
            hour = Mathf.FloorToInt(totalGameSec) / 60 % 24;
            day = Mathf.FloorToInt(totalGameSec) / 60 / 24;

            if (m != minute)
                GameMinuteChanged?.Invoke();
            if (h != hour)
                GameHourChanged?.Invoke();
            if (d != day)
                GameDayChanged?.Invoke();
        }

        
        public JsonValue Serialize()
        {
            JsonValue data = new JsonValue();
            data[_TotalGameSeconds].Set(totalGameSec);
            return data;
        }
        public void Deserialize(JsonValue data)
        {
            totalGameSec = data[_TotalGameSeconds].AsDouble();
        }
        
    }


    public interface IRealTimeTracker
    {
        /// <summary>
        /// Should be used to progress actions which would've changed over real world duration. 
        /// Called when a script has been loaded in after being saved.
        /// </summary>
        /// <param name="duration">Duration in seconds, in real world time, since the script was last instantiated. </param>
        public void ProgressRealTime(double duration);
    }
    public interface IGameTimeTracker
    {
        /// <summary>
        /// Should be used to progress actions which would've changed over game duration. Called when a script has been loaded in after being saved.
        /// </summary>
        /// <param name="duration">Duration in seconds, in game world time, since the script was last instantiated. </param>
        public void ProgressGameTime(double duration);
    }
}
