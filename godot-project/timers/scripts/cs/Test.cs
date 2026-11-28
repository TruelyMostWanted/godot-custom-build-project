using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Timers;

/// <summary>
/// Full API test of Timers (Timer, TimerInfo, TimerInstance, TimerServer). Same tests as scripts/gd/Test.gd.
/// Time advances in real frames, so timers are short (0.1 to 0.5 s) and tests use tolerances.
/// </summary>
public partial class Test : FeatureTest
{
    private const double Tolerance = 0.15;

    /// <summary>True when Timer nodes run on TimerServer (project setting, read at startup). The checks pass in both states.</summary>
    private bool _on;

    /// <summary>Signal counters of one timer (a Timer node or a TimerInstance).</summary>
    private sealed class Counts
    {
        public int Started;
        public int Stopped;
        public int PauseChanged;
        public int Ticked;
        public int AlertReached;
        public int Timeout;
        public bool PausedLast;
        public double TickedLast;
        public double AlertLast;
    }

    protected override async Task RunTests()
    {
        _on = (bool)ProjectSettings.GetSetting("application/run/timer_nodes_use_timer_server", false);
        Expect(Engine.HasSingleton("TimerServer"), "TimerServer is a registered singleton");
        Expect(ClassDB.ClassExists("TimerInfo"), "TimerInfo is registered");
        Expect(ClassDB.ClassExists("TimerInstance"), "TimerInstance is registered");
        await TestCountdownAndLoop();
        await TestStopwatch();
        await TestPauseAndTime();
        await TestTimeScaleAndClocks();
        await TestTicksAndAlerts();
        TestTimerInfoOnNode();
        TestServerLookupAndAll();
        await TestInstanceAlive();
        TestFormatTime();
        await TestGodotCompatibility();
        await TestNodePaths();
    }

    private static bool Approx(double a, double b, double tolerance = 1e-4) => Math.Abs(a - b) < tolerance;

    private async Task Wait(double seconds)
    {
        ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000.0);
        while (Time.GetTicksMsec() < end)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static Counts Watch(TimerInstance timer)
    {
        var counts = new Counts();
        timer.Started += () => counts.Started++;
        timer.Stopped += () => counts.Stopped++;
        timer.Timeout += () => counts.Timeout++;
        timer.PauseChanged += paused =>
        {
            counts.PauseChanged++;
            counts.PausedLast = paused;
        };
        timer.Ticked += time =>
        {
            counts.Ticked++;
            counts.TickedLast = time;
        };
        timer.AlertReached += time =>
        {
            counts.AlertReached++;
            counts.AlertLast = time;
        };
        return counts;
    }

    private static Counts Watch(Timer timer)
    {
        var counts = new Counts();
        timer.Started += () => counts.Started++;
        timer.Stopped += () => counts.Stopped++;
        timer.Timeout += () => counts.Timeout++;
        timer.PauseChanged += paused =>
        {
            counts.PauseChanged++;
            counts.PausedLast = paused;
        };
        timer.Ticked += time =>
        {
            counts.Ticked++;
            counts.TickedLast = time;
        };
        timer.AlertReached += time =>
        {
            counts.AlertReached++;
            counts.AlertLast = time;
        };
        return counts;
    }

    private static TimerInfo MakeInfo(double total, TimerInfo.TimerTickMode tickMode = TimerInfo.TimerTickMode.Countdown, bool loop = false)
    {
        return new TimerInfo { TotalTime = total, TickMode = tickMode, Loop = loop };
    }

    private Timer AddTimer(TimerInfo info = null)
    {
        var timer = new Timer();
        AddChild(timer);
        if (info != null)
        {
            timer.TimerInfo = info;
        }
        return timer;
    }

    private async Task TestCountdownAndLoop()
    {
        TimerInstance instance = TimerServer.CreateTimer(MakeInfo(0.3));
        Counts counts = Watch(instance);
        Expect(instance.IsStopped(), "countdown: new timer is stopped");
        Expect(!instance.IsRunning(), "countdown: new timer is not running");
        instance.Start();
        ExpectEqual(counts.Started, 1, "countdown: started signal");
        Expect(instance.IsRunning(), "countdown: running after start");
        Expect(Approx(instance.GetTimeLeft(), 0.3, Tolerance), "countdown: time_left starts at total_time");
        await Wait(0.1);
        Expect(instance.GetTimeLeft() < 0.3, "countdown: time_left decreases");
        Expect(Approx(instance.GetCurrentTime(), instance.GetTimeLeft(), 0.01), "countdown: current_time is time_left");
        Expect(Approx(instance.GetElapsedTime() + instance.GetTimeLeft(), 0.3, 0.01), "countdown: elapsed + left = total");
        ExpectEqual(counts.Timeout, 0, "countdown: no timeout early");
        await Wait(0.5);
        ExpectEqual(counts.Timeout, 1, "countdown: exactly one timeout");
        Expect(instance.IsStopped(), "countdown: stopped after timeout");
        ExpectEqual(instance.GetTimeLeft(), 0.0, "countdown: time_left is 0 after timeout");

        // Loop: keeps running and fires again.
        TimerInstance looping = TimerServer.CreateTimer(MakeInfo(0.15, TimerInfo.TimerTickMode.Countdown, true));
        Counts loopCounts = Watch(looping);
        looping.Start();
        await Wait(0.6);
        Expect(loopCounts.Timeout >= 2, $"loop: several timeouts ({loopCounts.Timeout})");
        Expect(looping.IsRunning(), "loop: still running");
        looping.Stop();
        ExpectEqual(loopCounts.Stopped, 1, "loop: stopped signal");
        Expect(looping.IsStopped(), "loop: is_stopped after stop");

        // restart
        TimerInstance restarted = TimerServer.CreateTimer(MakeInfo(5.0));
        restarted.Start();
        await Wait(0.2);
        restarted.Restart();
        Expect(restarted.GetTimeLeft() > 4.9, "restart: time starts over");
        restarted.Stop();
    }

    private async Task TestStopwatch()
    {
        TimerInstance endless = TimerServer.CreateTimer(MakeInfo(0.0, TimerInfo.TimerTickMode.Stopwatch));
        Counts endlessCounts = Watch(endless);
        endless.Start();
        await Wait(0.4);
        Expect(Approx(endless.GetCurrentTime(), 0.4, Tolerance), $"stopwatch without end: counts up ({endless.GetCurrentTime()})");
        Expect(endless.IsRunning(), "stopwatch without end: keeps running");
        ExpectEqual(endlessCounts.Timeout, 0, "stopwatch without end: never times out");
        endless.Stop();
        double finalTime = endless.GetCurrentTime();
        await Wait(0.1);
        Expect(Approx(endless.GetCurrentTime(), finalTime), "stopwatch: keeps the final time after stop");

        TimerInstance limited = TimerServer.CreateTimer(MakeInfo(0.3, TimerInfo.TimerTickMode.Stopwatch));
        Counts limitedCounts = Watch(limited);
        limited.Start();
        await Wait(0.1);
        Expect(limited.GetCurrentTime() > 0.0 && limited.GetCurrentTime() < 0.3, "stopwatch with end: shows elapsed time");
        Expect(limited.GetTimeLeft() < 0.3, "stopwatch with end: time_left is the rest");
        await Wait(0.5);
        ExpectEqual(limitedCounts.Timeout, 1, "stopwatch with end: timeout at total_time");
        Expect(limited.IsStopped(), "stopwatch with end: stopped");
    }

    private async Task TestPauseAndTime()
    {
        TimerInstance instance = TimerServer.CreateTimer(MakeInfo(2.0));
        Counts counts = Watch(instance);
        instance.Start();
        await Wait(0.2);
        instance.Pause();
        Expect(instance.IsPaused(), "pause: is_paused");
        Expect(instance.IsRunning(), "pause: still running while paused");
        ExpectEqual(counts.PauseChanged, 1, "pause: pause_changed emitted");
        ExpectEqual(counts.PausedLast, true, "pause: pause_changed(true)");
        double left = instance.GetTimeLeft();
        await Wait(0.3);
        Expect(Approx(instance.GetTimeLeft(), left), "pause: time does not advance");
        instance.Resume();
        Expect(!instance.IsPaused(), "resume: not paused");
        ExpectEqual(counts.PausedLast, false, "resume: pause_changed(false)");
        await Wait(0.2);
        Expect(instance.GetTimeLeft() < left, "resume: time advances again");
        instance.SetPaused(true);
        Expect(instance.IsPaused(), "set_paused(true)");
        instance.SetPaused(false);
        Expect(!instance.IsPaused(), "set_paused(false)");

        // set_current_time / add_time on a paused timer are exact.
        instance.Pause();
        instance.SetCurrentTime(1.0);
        Expect(Approx(instance.GetTimeLeft(), 1.0), "set_current_time: sets the time left");
        instance.AddTime(0.5);
        Expect(Approx(instance.GetTimeLeft(), 1.5), "add_time: adds to the shown time");
        instance.Resume();
        instance.Stop();

        // Stopwatch: shown time is elapsed time.
        TimerInstance stopwatch = TimerServer.CreateTimer(MakeInfo(0.0, TimerInfo.TimerTickMode.Stopwatch));
        stopwatch.Start();
        stopwatch.Pause();
        stopwatch.SetCurrentTime(10.0);
        Expect(Approx(stopwatch.GetCurrentTime(), 10.0), "stopwatch: set_current_time");
        stopwatch.AddTime(2.5);
        Expect(Approx(stopwatch.GetCurrentTime(), 12.5), "stopwatch: add_time");
        Expect(Approx(stopwatch.GetElapsedTime(), 12.5), "stopwatch: elapsed equals shown time");
        stopwatch.Stop();
    }

    private async Task TestTimeScaleAndClocks()
    {
        // TimerInfo.time_scale speeds one timer up.
        TimerInfo fastInfo = MakeInfo(0.8);
        fastInfo.TimeScale = 4.0;
        TimerInstance fast = TimerServer.CreateTimer(fastInfo);
        Counts fastCounts = Watch(fast);
        fast.Start();
        await Wait(0.5);
        ExpectEqual(fastCounts.Timeout, 1, "time_scale 4: 0.8 s timer ends within 0.5 s");

        // Physics clock.
        TimerInfo physicsInfo = MakeInfo(0.2);
        physicsInfo.ProcessCallback = TimerInfo.TimerProcessCallback.Physics;
        TimerInstance physics = TimerServer.CreateTimer(physicsInfo);
        Counts physicsCounts = Watch(physics);
        physics.Start();
        await Wait(0.6);
        ExpectEqual(physicsCounts.Timeout, 1, "physics clock: timeout");

        // ignore_time_scale: Engine.time_scale slows the scaled timer only.
        TimerInstance scaled = TimerServer.CreateTimer(MakeInfo(0.2));
        TimerInfo unscaledInfo = MakeInfo(0.2);
        unscaledInfo.IgnoreTimeScale = true;
        TimerInstance unscaled = TimerServer.CreateTimer(unscaledInfo);
        Counts scaledCounts = Watch(scaled);
        Counts unscaledCounts = Watch(unscaled);
        Engine.TimeScale = 0.1;
        scaled.Start();
        unscaled.Start();
        await Wait(0.6);
        Engine.TimeScale = 1.0;
        ExpectEqual(unscaledCounts.Timeout, 1, "ignore_time_scale: unscaled timer ends in real time");
        ExpectEqual(scaledCounts.Timeout, 0, "ignore_time_scale: scaled timer is slowed by Engine.time_scale");
        scaled.Stop();
    }

    private async Task TestTicksAndAlerts()
    {
        TimerInfo info = MakeInfo(1.0);
        info.TickInterval = 0.1;
        info.Alerts = new double[] { 0.7, 0.3 };
        TimerInstance instance = TimerServer.CreateTimer(info);
        Counts counts = Watch(instance);
        instance.Start();
        await Wait(0.45);
        Expect(counts.Ticked >= 2, $"ticks: several ticked signals ({counts.Ticked})");
        Expect(counts.TickedLast < 1.0, "ticks: ticked passes the shown time");
        ExpectEqual(counts.AlertReached, 1, "alerts: first alert (0.7 s left) reached");
        Expect(Approx(counts.AlertLast, 0.7), "alerts: alert_reached passes the alert time");
        await Wait(0.8);
        ExpectEqual(counts.AlertReached, 2, "alerts: second alert (0.3 s left) reached");
        Expect(Approx(counts.AlertLast, 0.3), "alerts: second alert time");
        ExpectEqual(counts.Timeout, 1, "ticks: timeout at the end");
    }

    private void TestTimerInfoOnNode()
    {
        TimerInfo info = MakeInfo(3.0, TimerInfo.TimerTickMode.Stopwatch, true);
        info.TimerName = "info-node";
        info.Description = "A stopwatch that loops every 3 s.";
        Timer timer = AddTimer(info);
        Expect(Approx(timer.WaitTime, 3.0), "TimerInfo: total_time maps to wait_time");
        Expect(!timer.OneShot, "TimerInfo: loop maps to one_shot = false");
        ExpectEqual(timer.TickMode, TimerInfo.TimerTickMode.Stopwatch, "TimerInfo: tick_mode on the node");
        if (_on)
        {
            Expect(timer.GetTimerRid().IsValid, "Timer node has a timer RID in the tree");
        }
        else
        {
            Expect(!timer.GetTimerRid().IsValid, "Timer node has no timer RID on Godot's own path");
        }
        Expect(timer.TimerInfo == info, "Timer node keeps the TimerInfo");
        timer.Start();
        timer.Pause();
        timer.CurrentTime = 1.25;
        Expect(Approx(timer.CurrentTime, 1.25), "Timer node: current_time setter");
        ExpectEqual(timer.GetTimeText("{s}.{cs}"), "1.25", "Timer node: get_time_text");
        timer.AddTime(1.0);
        Expect(Approx(timer.CurrentTime, 2.25), "Timer node: add_time");
        timer.Resume();
        timer.Stop();

        // The info applies live.
        info.TotalTime = 7.0;
        Expect(Approx(timer.WaitTime, 7.0), "TimerInfo edits apply live to the node");
        timer.TimerInfo = null;
        timer.QueueFree();

        // Info with defaults.
        var defaults = new TimerInfo();
        ExpectEqual(defaults.TickMode, TimerInfo.TimerTickMode.Countdown, "TimerInfo default tick_mode");
        Expect(Approx(defaults.TotalTime, 1.0), "TimerInfo default total_time");
        Expect(!defaults.Loop, "TimerInfo default loop");
        Expect(Approx(defaults.TimeScale, 1.0), "TimerInfo default time_scale");
        Expect(!defaults.IgnoreTimeScale, "TimerInfo default ignore_time_scale");
        ExpectEqual(defaults.ProcessMode, TimerInfo.TimerProcessMode.Pausable, "TimerInfo default process_mode");
        ExpectEqual(defaults.ProcessCallback, TimerInfo.TimerProcessCallback.Idle, "TimerInfo default process_callback");
        ExpectEqual(defaults.Alerts.Length, 0, "TimerInfo default alerts");
    }

    private void TestServerLookupAndAll()
    {
        int count = TimerServer.GetTimerCount();
        TimerInfo info = MakeInfo(5.0);
        info.TimerName = "lookup-timer";
        TimerInstance instance = TimerServer.CreateTimer(info);
        ExpectEqual(TimerServer.GetTimerCount(), count + 1, "get_timer_count grows");
        ExpectEqual(TimerServer.GetTimer("lookup-timer"), instance.GetRid(), "get_timer finds the timer by name");
        Expect(TimerServer.GetTimers().Contains(instance.GetRid()), "get_timers lists the timer");
        Expect(TimerServer.TimerIsValid(instance.GetRid()), "timer_is_valid");
        Expect(TimerServer.TimerGetObject(instance.GetRid()) == instance, "timer_get_object returns the TimerInstance");
        ExpectEqual(TimerServer.TimerGetName(instance.GetRid()), (StringName)"lookup-timer", "timer_get_name");
        ExpectEqual((int)TimerServer.TimerEvent.Timeout, 6, "TimerEvent.Timeout is the last event");

        // RID endpoints.
        Rid rid = TimerServer.TimerCreate();
        TimerServer.TimerSetTotalTime(rid, 4.0);
        TimerServer.TimerSetTickMode(rid, TimerInfo.TimerTickMode.Countdown);
        Expect(Approx(TimerServer.TimerGetTotalTime(rid), 4.0), "RID endpoint: total time");
        TimerServer.TimerStart(rid);
        Expect(TimerServer.TimerIsRunning(rid), "RID endpoint: running");
        TimerServer.TimerSetPaused(rid, true);
        Expect(TimerServer.TimerIsPaused(rid), "RID endpoint: paused");
        TimerServer.TimerSetPaused(rid, false);
        TimerServer.TimerAddTime(rid, 1.0);
        Expect(TimerServer.TimerGetTimeLeft(rid) > 4.5, "RID endpoint: add_time");
        TimerServer.TimerStop(rid);
        Expect(!TimerServer.TimerIsRunning(rid), "RID endpoint: stopped");
        TimerServer.FreeRid(rid);
        Expect(!TimerServer.TimerIsValid(rid), "free_rid invalidates the RID");

        // pause_all / resume_all / stop_all act on instances, and on Timer nodes only when they run on the server.
        Timer timer = AddTimer();
        Counts nodeCounts = Watch(timer);
        timer.Start(5.0);
        instance.Start();
        TimerServer.PauseAll();
        Expect(instance.IsPaused(), "pause_all pauses the instance");
        if (_on)
        {
            Expect(timer.Paused, "pause_all pauses the Timer node");
            Expect(timer.IsPaused(), "pause_all: is_paused on the node");
            ExpectEqual(nodeCounts.PauseChanged, 1, "pause_all: node emits pause_changed");
            ExpectEqual(nodeCounts.PausedLast, true, "pause_all: node pause_changed(true)");
        }
        else
        {
            Expect(!timer.Paused, "pause_all does not pause the Timer node on Godot's own path");
            ExpectEqual(nodeCounts.PauseChanged, 0, "pause_all: node emits no pause_changed");
        }
        TimerServer.ResumeAll();
        Expect(!timer.IsPaused(), "resume_all leaves the node running");
        Expect(!instance.IsPaused(), "resume_all resumes the instance");
        TimerServer.StopAll();
        Expect(instance.IsStopped(), "stop_all stops the instance");
        if (_on)
        {
            Expect(timer.IsStopped(), "stop_all stops the node");
            ExpectEqual(nodeCounts.Stopped, 1, "stop_all: node emits stopped");
        }
        else
        {
            Expect(!timer.IsStopped(), "stop_all does not stop the node on Godot's own path");
            ExpectEqual(nodeCounts.Stopped, 0, "stop_all: node emits no stopped");
            timer.Stop();
        }
        timer.QueueFree();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Rid StartAndDrop(Counts counts)
    {
        TimerInstance instance = TimerServer.CreateTimer(MakeInfo(0.2));
        Rid rid = instance.GetRid();
        instance.Started += () => counts.Started++;
        instance.Timeout += () => counts.Timeout++;
        instance.Start();
        return rid;
    }

    private async Task TestInstanceAlive()
    {
        var counts = new Counts();
        Rid rid = StartAndDrop(counts);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Expect(TimerServer.TimerIsValid(rid), "running TimerInstance stays alive without a reference");
        await Wait(0.5);
        ExpectEqual(counts.Timeout, 1, "dropped running TimerInstance still times out");
        Expect(!TimerServer.TimerIsValid(rid), "TimerInstance is freed after it ends");
    }

    private void TestFormatTime()
    {
        ExpectEqual(TimerServer.FormatTime(75.4), "01:15", "format_time default");
        ExpectEqual(TimerServer.FormatTime(4503.25), "75:03", "format_time: largest unit carries the amount");
        ExpectEqual(TimerServer.FormatTime(3723.456, "{h}:{mm}:{ss}.{cs}"), "1:02:03.45", "format_time hours");
        ExpectEqual(TimerServer.FormatTime(3723.456, "{s}.{ms}"), "3723.456", "format_time seconds and milliseconds");
        ExpectEqual(TimerServer.FormatTime(59.99, "{mm}:{ss}"), "00:59", "format_time truncates");
        ExpectEqual(TimerServer.FormatTime(59.01, "{mm}:{ss}", true), "01:00", "format_time rounds up");
        ExpectEqual(TimerServer.FormatTime(0.2, "{s}", true), "1", "format_time rounds up to 1");
        ExpectEqual(TimerServer.FormatTime(-3.0, "{s}"), "0", "format_time clamps negative");
        TimerInstance instance = TimerServer.CreateTimer(MakeInfo(2.0));
        ExpectEqual(instance.GetTimeText(), "00:02", "TimerInstance.get_time_text");
    }

    private async Task TestGodotCompatibility()
    {
        Timer timer = AddTimer();
        Counts counts = Watch(timer);
        Expect(timer.IsStopped(), "compat: new Timer is stopped");
        timer.Start(5);
        Expect(Approx(timer.WaitTime, 5.0), "compat: start(5) sets wait_time");
        Expect(Approx(timer.TimeLeft, 5.0, 0.1), "compat: time_left is 5 after start(5)");
        Expect(!timer.IsStopped(), "compat: running after start");
        ExpectEqual(timer.IsProcessingInternal(), !_on, "compat: node processes internally only on Godot's own path");
        timer.Paused = true;
        Expect(timer.Paused, "compat: paused property");
        timer.Paused = false;
        timer.Stop();
        Expect(timer.IsStopped(), "compat: stop -> is_stopped");
        ExpectEqual(counts.Started, 1, "compat: started signal on the node");
        ExpectEqual(counts.Stopped, 1, "compat: stopped signal on the node");
        timer.QueueFree();

        // One shot and autostart as before.
        var oneShot = new Timer { WaitTime = 0.15, OneShot = true, Autostart = true };
        Counts shotCounts = Watch(oneShot);
        AddChild(oneShot);
        Expect(!oneShot.IsStopped(), "compat: autostart starts in the tree");
        await Wait(0.5);
        ExpectEqual(shotCounts.Timeout, 1, "compat: one_shot fires once");
        Expect(oneShot.IsStopped(), "compat: one_shot stops after timeout");
        oneShot.QueueFree();

        // Physics process callback on the node.
        var physics = new Timer { WaitTime = 0.15, OneShot = true, ProcessCallback = Timer.TimerProcessCallback.Physics };
        Counts physicsCounts = Watch(physics);
        AddChild(physics);
        physics.Start();
        await Wait(0.5);
        ExpectEqual(physicsCounts.Timeout, 1, "compat: physics process_callback fires");
        physics.QueueFree();
    }

    // Timer nodes on both paths (setting on or off).
    private async Task TestNodePaths()
    {
        // Changing wait_time on a running node keeps the time left of the current cycle.
        Timer looping = AddTimer();
        Counts loopCounts = Watch(looping);
        looping.OneShot = false;
        looping.Start(1.0);
        await Wait(0.25);
        looping.WaitTime = 0.5;
        Expect((float)looping.TimeLeft > 0.5f, $"node: wait_time change keeps time_left ({looping.TimeLeft})");
        await Wait(0.8);
        Expect(loopCounts.Timeout >= 1, $"node: loop fires after the wait_time change ({loopCounts.Timeout})");
        looping.Stop();
        looping.QueueFree();

        // Signals started / stopped / pause_changed, and no stopped at a one-shot's natural end.
        Timer timer = AddTimer();
        timer.OneShot = true;
        Counts counts = Watch(timer);
        timer.Start(0.2);
        ExpectEqual(counts.Started, 1, "node signals: started on start()");
        timer.Pause();
        ExpectEqual(counts.PauseChanged, 1, "node signals: pause_changed on pause()");
        ExpectEqual(counts.PausedLast, true, "node signals: pause_changed(true)");
        timer.Resume();
        ExpectEqual(counts.PausedLast, false, "node signals: pause_changed(false)");
        timer.Stop();
        ExpectEqual(counts.Stopped, 1, "node signals: stopped on stop()");
        timer.Start(0.15);
        await Wait(0.5);
        ExpectEqual(counts.Timeout, 1, "node signals: one-shot emits timeout at its end");
        ExpectEqual(counts.Stopped, 1, "node signals: no stopped at the natural end");
        timer.QueueFree();

        // Ticks and alerts on a node work only on the server path.
        TimerInfo info = MakeInfo(1.0);
        info.TickInterval = 0.1;
        info.Alerts = new double[] { 0.7, 0.3 };
        Timer ticking = AddTimer(info);
        Counts tickCounts = Watch(ticking);
        ticking.Start();
        ExpectEqual(ticking.IsProcessingInternal(), !_on, "node: internal processing only on Godot's own path");
        await Wait(0.45);
        if (_on)
        {
            Expect(tickCounts.Ticked >= 2, $"node: ticked on the server path ({tickCounts.Ticked})");
            Expect(tickCounts.AlertReached >= 1, "node: alert_reached on the server path");
        }
        else
        {
            ExpectEqual(tickCounts.Ticked, 0, "node: no ticked on Godot's own path");
            ExpectEqual(tickCounts.AlertReached, 0, "node: no alert_reached on Godot's own path");
        }
        ticking.Stop();
        ticking.TimerInfo = null;
        ticking.QueueFree();
    }
}
