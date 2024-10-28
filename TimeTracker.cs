using System;
using System.Diagnostics;
using Godot;
public class TimeTracker 
{
    private static readonly string _Time = "T";
    private static readonly string _IsRunning = "IR";
    private static readonly string _RunInBackground = "RIB";

    //private Stopwatch stopwatch = new Stopwatch();

    public event Action<TimeTracker> TimeOut;

    public bool Loop { get; set; } = false;
    public double WaitTime { get; set; } = 0;

    // this is time which was Deserialized
    private double timeAddon;
    private double currentTime = 0;
    private bool isRunning = false;

    public bool IsRunning { get { return isRunning; } }
    public double ElapsedSeconds { get { return currentTime + timeAddon; } }
    public double ElapsedMilliseconds { get { return ElapsedSeconds * 1000; } }

    public TimeTracker() 
    {
        AppManager.Instance.AppUpdate += Update;
    }

    public TimeTracker(double waitTime, bool loop = false) : this()
    {
        WaitTime = waitTime;
        Loop = loop;
        AppManager.Instance.AppUpdate += Update;
    }
    
    public void Dispose()
    {
        AppManager.Instance.AppUpdate -= Update;
    }

    private void Update(double delta)
    {
        if (!isRunning)
            return;

        currentTime += delta;

        if (WaitTime == 0)
            return;

        if (ElapsedSeconds >= WaitTime)
        {
            TimeOut?.Invoke(this);
            if (Loop)
            {
                double dif = ElapsedSeconds - WaitTime;
                Restart();
                timeAddon = dif;
            }
            else
            {
                Reset();
            }
        }
    }

    public void Restart()
    {
        timeAddon = 0;
        currentTime = 0;
        Start();
    }

    public void Reset()
    {
        timeAddon = 0;
        currentTime = 0;
        Stop();
    }
    public void Start()
    {
        isRunning = true;
    }

    public void Stop()
    {
        isRunning = false;
    }

    public bool IsFinished()
    {
        if (!isRunning)
            return false;

        if (ElapsedSeconds >= WaitTime)
            return true;

        return false;
    }

    public JsonValue Serialize()
    {
        JsonValue data = new JsonValue();

        data[_IsRunning].Set(IsRunning);
        if (IsRunning)
        {
            data[_Time].Set(ElapsedSeconds);
        }
        return data;
    }
    public void Deserialize(JsonValue data)
    {
        if (data[_IsRunning].AsBool())
        {
            Restart();
            timeAddon = data[_Time].AsDouble();
        }
    }

}

