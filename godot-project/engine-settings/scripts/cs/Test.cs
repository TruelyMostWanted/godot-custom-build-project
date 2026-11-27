using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.EngineSettings;

using EngineSettings = Godot.EngineSettings;

/// <summary>
/// Full API test of Engine Settings (EngineSettingsServer, EngineSettings). Same tests as scripts/gd/Test.gd.
/// The simulation values (time scale, physics rate, audio speed) are locked unless the project setting
/// settings/engine/allow_simulation_values is on. The tests switch it on at runtime where they change those values and off again.
/// The restart test starts the project again as its own process (desktop only): the probe runs with the project setting (locked).
/// </summary>
public partial class Test : FeatureTest
{
    private const string ProbeScene = "res://engine-settings/scenes/restart_probe.tscn";
    private const string EngineFile = "user://settings/engine.ini";
    private const string AllowSimulation = "settings/engine/allow_simulation_values";

    private static readonly string[] SimulationNames = ["time_scale", "physics_ticks_per_second", "max_physics_steps_per_frame", "physics_jitter_fix", "audio_playback_speed_scale"];

    private readonly Godot.Collections.Dictionary _received = [];

    protected override Task RunTests()
    {
        TestDefaults();
        TestValues();
        TestLock();
        TestValidation();
        TestSignals();
        TestResource();
        TestRestart();
        Reset();
        DirAccess.RemoveAbsolute(EngineFile);
        return Task.CompletedTask;
    }

    private static void SetAllowSimulation(bool allowed)
    {
        ProjectSettings.SetSetting(AllowSimulation, allowed);
    }

    private static void Reset()
    {
        SetAllowSimulation(true);
        EngineSettingsServer.Singleton.ResetToDefaults();
        SetAllowSimulation(false);
    }

    private void TestDefaults()
    {
        Reset();
        int projectFps = (int)ProjectSettings.GetSetting("application/run/max_fps");
        int projectTicks = (int)ProjectSettings.GetSetting("physics/common/physics_ticks_per_second");
        int projectSteps = (int)ProjectSettings.GetSetting("physics/common/max_physics_steps_per_frame");
        bool projectInterpolation = (bool)ProjectSettings.GetSetting("physics/common/physics_interpolation");
        bool projectLow = (bool)ProjectSettings.GetSetting("application/run/low_processor_mode");
        ExpectEqual((bool)ProjectSettings.GetSetting(AllowSimulation), false, "simulation values are locked by default");
        ExpectEqual(EngineSettingsServer.GetMaxFps(), projectFps, "the default max_fps is the project setting");
        ExpectEqual(EngineSettingsServer.GetPhysicsTicksPerSecond(), projectTicks, "the default physics rate is the project setting");
        ExpectEqual(EngineSettingsServer.GetMaxPhysicsStepsPerFrame(), projectSteps, "the default physics steps are the project setting");
        ExpectEqual(EngineSettingsServer.IsPhysicsInterpolationEnabled(), projectInterpolation, "the default physics interpolation is the project setting");
        ExpectEqual(EngineSettingsServer.IsLowProcessorModeEnabled(), projectLow, "the default low-processor mode is the project setting");
        ExpectEqual(EngineSettingsServer.GetTimeScale(), 1.0f, "the default time scale is 1");
        ExpectEqual(EngineSettingsServer.GetAudioPlaybackSpeedScale(), 1.0f, "the default audio speed is 1");
        ExpectEqual((float)Engine.TimeScale, 1.0f, "the engine runs at the project time scale");
    }

    private void TestValues()
    {
        EngineSettingsServer.SetMaxFps(45);
        ExpectEqual(EngineSettingsServer.GetMaxFps(), 45, "max_fps round trip");
        ExpectEqual(Engine.MaxFps, 45, "max_fps reaches Engine at once");
        EngineSettingsServer.SetPhysicsInterpolationEnabled(true);
        Expect(EngineSettingsServer.IsPhysicsInterpolationEnabled(), "physics_interpolation round trip");
        Expect(GetTree().PhysicsInterpolation, "physics_interpolation reaches the SceneTree");
        EngineSettingsServer.SetLowProcessorModeSleepUsec(10000);
        ExpectEqual(EngineSettingsServer.GetLowProcessorModeSleepUsec(), 10000, "low_processor_mode_sleep_usec round trip");
        ExpectEqual(OS.LowProcessorUsageModeSleepUsec, 10000, "the low-processor sleep reaches OS");
        EngineSettingsServer.SetLowProcessorModeEnabled(true);
        Expect(EngineSettingsServer.IsLowProcessorModeEnabled(), "low_processor_mode round trip");
        Expect(OS.LowProcessorUsageMode, "the low-processor mode reaches OS");
        EngineSettingsServer.SetLowProcessorModeEnabled(false);
        SetAllowSimulation(true);
        Expect(!EngineSettingsServer.IsSimulationLocked(), "IsSimulationLocked follows the project setting");
        EngineSettingsServer.SetTimeScale(0.5f);
        ExpectEqual(EngineSettingsServer.GetTimeScale(), 0.5f, "time_scale round trip");
        ExpectEqual((float)Engine.TimeScale, 0.5f, "time_scale reaches Engine at once");
        EngineSettingsServer.SetPhysicsTicksPerSecond(90);
        ExpectEqual(Engine.PhysicsTicksPerSecond, 90, "the physics rate reaches Engine");
        EngineSettingsServer.SetMaxPhysicsStepsPerFrame(4);
        ExpectEqual(Engine.MaxPhysicsStepsPerFrame, 4, "the physics steps reach Engine");
        EngineSettingsServer.SetPhysicsJitterFix(0.25f);
        ExpectEqual((float)Engine.PhysicsJitterFix, 0.25f, "the jitter fix reaches Engine");
        EngineSettingsServer.SetAudioPlaybackSpeedScale(1.5f);
        ExpectEqual(AudioServer.PlaybackSpeedScale, 1.5f, "the audio speed reaches AudioServer");
        Engine.MaxFps = 75;
        EngineSettingsServer.Singleton.ReadFromEngine();
        ExpectEqual(EngineSettingsServer.GetMaxFps(), 75, "ReadFromEngine reads Engine.MaxFps back");
        Reset();
        ExpectEqual((float)Engine.TimeScale, 1.0f, "ResetToDefaults gives the time scale back");
        int projectFps = (int)ProjectSettings.GetSetting("application/run/max_fps");
        ExpectEqual(Engine.MaxFps, projectFps, "ResetToDefaults gives max_fps back");
    }

    private void TestLock()
    {
        Expect(EngineSettingsServer.IsSimulationLocked(), "the simulation is locked without the project setting");
        string[] simulation = EngineSettingsServer.GetSimulationSettingNames();
        foreach (string setting in SimulationNames)
        {
            Expect(System.Array.IndexOf(simulation, setting) >= 0, $"{setting} is a simulation value");
            Expect(EngineSettingsServer.IsSimulationSetting(setting), $"IsSimulationSetting({setting}) is true");
            Expect(!EngineSettingsServer.Singleton.IsValueSupported(setting), $"locked: {setting} is reported as unsupported");
        }
        Expect(!EngineSettingsServer.IsSimulationSetting("max_fps"), "max_fps is not a simulation value");
        Expect(EngineSettingsServer.Singleton.IsValueSupported("max_fps"), "max_fps is always supported");
        EngineSettingsServer.SetTimeScale(3.0f);
        ExpectEqual(EngineSettingsServer.GetTimeScale(), 1.0f, "locked: the time scale stays at the project value");
        ExpectEqual((float)Engine.TimeScale, 1.0f, "locked: Engine.TimeScale is unchanged");
        EngineSettingsServer.SetPhysicsTicksPerSecond(30);
        int projectTicks = (int)ProjectSettings.GetSetting("physics/common/physics_ticks_per_second");
        ExpectEqual(Engine.PhysicsTicksPerSecond, projectTicks, "locked: the physics rate is unchanged");
        Reset();
    }

    private void TestValidation()
    {
        SetAllowSimulation(true);
        EngineSettingsServerInstance server = EngineSettingsServer.Singleton;
        server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        EngineSettingsServer.SetMaxFps(-5);
        ExpectEqual(EngineSettingsServer.GetMaxFps(), 0, "max_fps is at least 0, unlimited");
        EngineSettingsServer.SetTimeScale(0.0f);
        Expect(Mathf.IsEqualApprox((float)EngineSettingsServer.GetTimeScale(), 0.01f), "the time scale is at least 0.01");
        EngineSettingsServer.SetTimeScale(500.0f);
        ExpectEqual(EngineSettingsServer.GetTimeScale(), 100.0f, "the time scale is at most 100");
        EngineSettingsServer.SetPhysicsTicksPerSecond(0);
        ExpectEqual(EngineSettingsServer.GetPhysicsTicksPerSecond(), 1, "the physics rate is at least 1");
        EngineSettingsServer.SetMaxPhysicsStepsPerFrame(1000);
        ExpectEqual(EngineSettingsServer.GetMaxPhysicsStepsPerFrame(), 100, "the physics steps are at most 100");
        EngineSettingsServer.SetAudioPlaybackSpeedScale(10.0f);
        ExpectEqual(EngineSettingsServer.GetAudioPlaybackSpeedScale(), 4.0f, "the audio speed is at most 4");
        EngineSettingsServer.SetLowProcessorModeSleepUsec(5000000);
        ExpectEqual(EngineSettingsServer.GetLowProcessorModeSleepUsec(), 1000000, "the low-processor sleep is at most one second");
        ExpectEqual((float)Engine.TimeScale, 1.0f, "manual mode: nothing reached the engine before Apply");
        server.Revert();
        server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        Reset();
    }

    private void TestSignals()
    {
        EngineSettingsServerInstance server = EngineSettingsServer.Singleton;
        server.MaxFpsChanged += fps => _received["max_fps"] = fps;
        server.LowProcessorModeChanged += enabled => _received["low_processor_mode"] = enabled;
        server.PhysicsInterpolationChanged += enabled => _received["physics_interpolation"] = enabled;
        server.PhysicsTicksPerSecondChanged += ticks => _received["physics_ticks"] = ticks;
        server.TimeScaleChanged += scale => _received["time_scale"] = scale;
        SetAllowSimulation(true);
        EngineSettingsServer.SetMaxFps(30);
        EngineSettingsServer.SetLowProcessorModeEnabled(true);
        EngineSettingsServer.SetPhysicsInterpolationEnabled(true);
        EngineSettingsServer.SetPhysicsTicksPerSecond(90);
        EngineSettingsServer.SetTimeScale(0.5f);
        ExpectEqual((int)_received["max_fps"], 30, "MaxFpsChanged passes the limit");
        Expect((bool)_received["low_processor_mode"], "LowProcessorModeChanged passes the flag");
        Expect((bool)_received["physics_interpolation"], "PhysicsInterpolationChanged passes the flag");
        ExpectEqual((int)_received["physics_ticks"], 90, "PhysicsTicksPerSecondChanged passes the rate");
        ExpectEqual((float)_received["time_scale"], 0.5f, "TimeScaleChanged passes the scale");
        Reset();
    }

    private void TestResource()
    {
        var settings = new EngineSettings();
        ExpectEqual(settings.MaxFps, 0, "EngineSettings: default MaxFps");
        ExpectEqual(settings.TimeScale, 1.0f, "EngineSettings: default TimeScale");
        ExpectEqual(settings.PhysicsTicksPerSecond, 60, "EngineSettings: default PhysicsTicksPerSecond");
        ExpectEqual(settings.MaxPhysicsStepsPerFrame, 8, "EngineSettings: default MaxPhysicsStepsPerFrame");
        ExpectEqual(settings.PhysicsJitterFix, 0.5f, "EngineSettings: default PhysicsJitterFix");
        ExpectEqual(settings.AudioPlaybackSpeedScale, 1.0f, "EngineSettings: default AudioPlaybackSpeedScale");
        Expect(!settings.LowProcessorMode, "EngineSettings: LowProcessorMode is off by default");
        Expect(!settings.PhysicsInterpolation, "EngineSettings: PhysicsInterpolation is off by default");
        ExpectEqual(settings.LowProcessorModeSleepUsec, 6900, "EngineSettings: default LowProcessorModeSleepUsec");
        settings.TimeScale = 250.0f;
        ExpectEqual(settings.TimeScale, 250.0f, "EngineSettings: the resource stores raw values, the server clamps");
    }

    private void TestRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart test (needs a desktop platform to start a second process)");
            return;
        }
        SetAllowSimulation(true);
        EngineSettingsServer.SetMaxFps(45);
        EngineSettingsServer.SetPhysicsInterpolationEnabled(true);
        EngineSettingsServer.SetLowProcessorModeSleepUsec(12000);
        EngineSettingsServer.SetTimeScale(0.5f);
        ExpectEqual(EngineSettingsServer.Singleton.Save(), Error.Ok, "Save writes the player file");
        SetAllowSimulation(false);
        Godot.Collections.Dictionary probe = RunProbe([]);
        ExpectEqual((int)probe["ready_frame"], 0, "restart: applied before the first frame");
        ExpectEqual((int)probe["engine_max_fps"], 45, "restart: Engine.MaxFps is the saved value");
        ExpectEqual((int)probe["max_fps"], 45, "restart: GetMaxFps is the saved value");
        Expect((bool)probe["physics_interpolation"], "restart: the SceneTree has physics interpolation");
        ExpectEqual((int)probe["sleep_usec"], 12000, "restart: OS has the saved low-processor sleep");
        Expect((bool)probe["simulation_locked"], "restart: the project locks the simulation values");
        ExpectEqual((float)probe["engine_time_scale"], 1.0f, "restart: a stored time scale is ignored while locked");
        probe = RunProbe(["--max-fps", "30", "--time-scale", "2"]);
        ExpectEqual((int)probe["engine_max_fps"], 30, "restart: --max-fps reaches the engine");
        ExpectEqual((float)probe["engine_time_scale"], 2.0f, "restart: --time-scale reaches the engine, even when locked");
        Expect((bool)probe["max_fps_overridden"], "restart: --max-fps is a session override");
        Expect((bool)probe["time_scale_overridden"], "restart: --time-scale is a session override");
        ExpectEqual((int)probe["max_fps"], 45, "restart: --max-fps does not change the stored value");
    }

    private Godot.Collections.Dictionary RunProbe(string[] extraArgs)
    {
        var args = new System.Collections.Generic.List<string> { "--headless" };
        args.AddRange(extraArgs);
        args.AddRange(["--path", ProjectSettings.GlobalizePath("res://"), ProbeScene]);
        var output = new Godot.Collections.Array();
        int code = OS.Execute(OS.GetExecutablePath(), args.ToArray(), output, true);
        ExpectEqual(code, 0, "restart: the probe process ran");
        foreach (string line in output[0].AsString().Split('\n'))
        {
            if (line.StartsWith("PROBE "))
            {
                return Json.ParseString(line["PROBE ".Length..]).AsGodotDictionary();
            }
        }
        Expect(false, "restart: the probe printed its result");
        return [];
    }
}
