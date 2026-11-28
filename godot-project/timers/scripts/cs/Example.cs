using Godot;

namespace TruelyInteractive.Timers;

/// <summary>Minimal example of Timers: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override async void _Ready()
    {
        // The nodes the snippets refer to.
        AddChild(new Timer { Name = "RoundTimer" });
        AddChild(new Label { Name = "Label" });

        // Quick start with a Timer node.
        var timer = new Timer { WaitTime = 3.0, OneShot = true };
        timer.Timeout += OnTimeout;
        AddChild(timer);
        timer.Start();

        // Countdown and stopwatch.
        var clock = new Timer { TickMode = TimerInfo.TimerTickMode.Stopwatch, WaitTime = 0.0 };
        AddChild(clock);
        clock.Start();
        await ToSignal(GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);
        GD.Print(clock.CurrentTime); // About 2.5

        // Describe a timer with TimerInfo.
        var info = new TimerInfo
        {
            TimerName = "round",
            TotalTime = 90.0,
            TickInterval = 1.0,
            Alerts = new double[] { 30.0, 10.0 },
        };
        GetNode<Timer>("RoundTimer").TimerInfo = info;
        info.TotalTime = 120.0; // Applies to RoundTimer at once.

        // Pause and resume.
        var round = GetNode<Timer>("RoundTimer");
        round.PauseChanged += paused => GD.Print("paused: ", paused);
        round.Pause();
        round.Resume();

        // Ticks and alerts.
        round.Ticked += time => GetNode<Label>("Label").Text = Mathf.CeilToInt(time).ToString();
        round.AlertReached += time => GD.Print($"{time} seconds left!");
        round.Start();

        // Show the time as text.
        GetNode<Label>("Label").Text = round.GetTimeText(); // "02:00"
        GetNode<Label>("Label").Text = round.GetTimeText("{m}:{ss}.{cs}"); // "2:00.00"
        GD.Print(TimerServer.FormatTime(4503.0, "{mm}:{ss}")); // "75:03"

        // Code timers.
        var cooldownInfo = new TimerInfo { TotalTime = 2.0 };
        var cooldown = TimerServer.CreateTimer(cooldownInfo);
        cooldown.Start();
        await ToSignal(cooldown, TimerInstance.SignalName.Timeout);
        GD.Print("Ready again.");

        // Use the RID API from GDExtension.
        Rid rid = TimerServer.TimerCreate();
        TimerServer.TimerSetTotalTime(rid, 0.1);
        TimerServer.TimerSetCallback(rid, Callable.From((int ev, double time) =>
        {
            if (ev == (int)TimerServer.TimerEvent.Timeout)
                GD.Print("RID timer ended");
        }));
        TimerServer.TimerStart(rid);
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        // Later:
        TimerServer.FreeRid(rid);

        // Pause menu: pause, resume and stop all.
        OnPauseMenuOpened();
        OnPauseMenuClosed();
        OnLevelExit();

        // Clocks.
        var clockInfo = new TimerInfo
        {
            TotalTime = 5.0,
            ProcessMode = TimerInfo.TimerProcessMode.Always, // Runs while the game is paused.
            IgnoreTimeScale = true, // Ignores Engine.time_scale.
            ProcessCallback = TimerInfo.TimerProcessCallback.Physics,
        };
        TimerServer.CreateTimer(clockInfo).Start();

        TimerServer.StopAll();
        GetTree().Quit();
    }

    private void OnTimeout() => GD.Print("Three seconds passed.");

    private void OnPauseMenuOpened() => TimerServer.PauseAll();

    private void OnPauseMenuClosed() => TimerServer.ResumeAll();

    private void OnLevelExit() => TimerServer.StopAll();
}
