using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue26FeatureEngineSettings;

/// <summary>
/// C# checks of 26-feature-engine-settings. Check the same things as scripts/gd/check.gd.
/// Typed methods are called on the generated static class, the methods of the base class through
/// <c>Singleton</c>. The simulation values are locked unless the project setting
/// settings/engine/allow_simulation_values is enabled; the checks enable it at runtime where they change them.
/// The restart checks start the project again as their own processes (desktop only).
/// </summary>
public partial class Issue26FeatureEngineSettingsCheck : FeatureCheckCs
{
    private const string ProbeScene = "res://26-feature-engine-settings/scenes/restart_probe.tscn";
    private const string EngineFile = "user://settings/engine.ini";
    private const string AllowSimulation = "settings/engine/allow_simulation_values";

    protected override string CheckName => "26-feature-engine-settings";

    protected override async Task RunChecks()
    {
        var server = EngineSettingsServer.Singleton;
        Expect(Engine.HasSingleton("EngineSettingsServer"), "EngineSettingsServer is a registered singleton");
        Expect(server is EngineSettingsServerInstance, "EngineSettingsServer.Singleton is the instance type");
        Expect(Engine.GetSingleton("EngineSettingsServer") is SettingsServer, "EngineSettingsServerInstance derives from SettingsServer");
        Expect(SettingsServerManager.GetServer("engine") == server, "the domain is registered as \"engine\"");
        Expect(server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(server.GetApplyPhase(), SettingsServer.ApplyPhase.Scene, "applied after the SceneTree exists");
        ExpectEqual(server.GetFilePath(), EngineFile, "file path");
        var names = server.GetSettingNames();
        foreach (string setting in new[] { "max_fps", "time_scale", "physics_ticks_per_second", "physics_jitter_fix", "physics_interpolation", "max_physics_steps_per_frame", "audio_playback_speed_scale", "low_processor_mode", "low_processor_mode_sleep_usec" })
        {
            Expect(Array.IndexOf(names, setting) >= 0, $"setting \"{setting}\" exists");
        }

        CheckDefaults();
        CheckApply();
        CheckLock();
        CheckValidation();
        CheckSessionOverride();
        CheckResource();
        CheckRestart();

        // Leave no player files and no changed engine values behind.
        Reset();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(EngineFile);
    }

    private static void SetAllowSimulation(bool allowed) => ProjectSettings.SetSetting(AllowSimulation, allowed);

    private static void Reset()
    {
        SetAllowSimulation(true);
        EngineSettingsServer.Singleton.ResetToDefaults();
        SetAllowSimulation(false);
    }

    private static bool Approx(double a, double b) => Mathf.IsEqualApprox(a, b);

    private void CheckDefaults()
    {
        var server = EngineSettingsServer.Singleton;
        Reset();
        ExpectEqual(ProjectSettings.GetSetting(AllowSimulation).AsBool(), false, "simulation values are locked by default");
        ExpectEqual(server.GetDefaultValue("max_fps").AsInt32(), ProjectSettings.GetSetting("application/run/max_fps").AsInt32(), "default max_fps is the project setting");
        ExpectEqual(server.GetDefaultValue("physics_ticks_per_second").AsInt32(), ProjectSettings.GetSetting("physics/common/physics_ticks_per_second").AsInt32(), "default physics rate is the project setting");
        ExpectEqual(server.GetDefaultValue("physics_interpolation").AsBool(), ProjectSettings.GetSetting("physics/common/physics_interpolation").AsBool(), "default physics interpolation is the project setting");
        ExpectEqual(server.GetDefaultValue("low_processor_mode").AsBool(), ProjectSettings.GetSetting("application/run/low_processor_mode").AsBool(), "default low-processor mode is the project setting");
        ExpectEqual(server.GetDefaultValue("time_scale").AsDouble(), 1.0, "default time scale is 1.0");
        ExpectEqual(Engine.TimeScale, 1.0, "the engine runs at the project time scale");
    }

    private void CheckApply()
    {
        var server = EngineSettingsServer.Singleton;
        EngineSettingsServer.SetMaxFps(45);
        ExpectEqual(EngineSettingsServer.GetMaxFps(), 45, "GetMaxFps()");
        ExpectEqual(Engine.MaxFps, 45, "max_fps reaches Engine at once");
        EngineSettingsServer.SetPhysicsInterpolationEnabled(true);
        Expect(GetTree().PhysicsInterpolation, "physics interpolation reaches the SceneTree");
        EngineSettingsServer.SetLowProcessorModeSleepUsec(10000);
        ExpectEqual(OS.LowProcessorUsageModeSleepUsec, 10000, "the low-processor sleep reaches OS");
        EngineSettingsServer.SetLowProcessorModeEnabled(true);
        Expect(OS.LowProcessorUsageMode, "the low-processor mode reaches OS");
        EngineSettingsServer.SetLowProcessorModeEnabled(false);

        SetAllowSimulation(true);
        Expect(!EngineSettingsServer.IsSimulationLocked(), "IsSimulationLocked() follows the project setting");
        EngineSettingsServer.SetTimeScale(0.5);
        ExpectEqual(Engine.TimeScale, 0.5, "time_scale reaches Engine at once");
        EngineSettingsServer.SetPhysicsTicksPerSecond(90);
        ExpectEqual(Engine.PhysicsTicksPerSecond, 90, "the physics rate reaches Engine");
        EngineSettingsServer.SetMaxPhysicsStepsPerFrame(4);
        ExpectEqual(Engine.MaxPhysicsStepsPerFrame, 4, "the physics steps reach Engine");
        EngineSettingsServer.SetAudioPlaybackSpeedScale(1.5f);
        Expect(Approx(AudioServer.PlaybackSpeedScale, 1.5), "the audio speed reaches AudioServer");

        // Changed directly on the engine, read back.
        Engine.MaxFps = 75;
        server.ReadFromEngine();
        ExpectEqual(EngineSettingsServer.GetMaxFps(), 75, "ReadFromEngine() reads Engine.MaxFps back");
        Reset();
        ExpectEqual(Engine.TimeScale, 1.0, "ResetToDefaults() gives back the time scale");
    }

    private void CheckLock()
    {
        var server = EngineSettingsServer.Singleton;
        SetAllowSimulation(false);
        Expect(EngineSettingsServer.IsSimulationLocked(), "locked without the project setting");
        var simulation = EngineSettingsServer.GetSimulationSettingNames();
        foreach (string setting in new[] { "time_scale", "physics_ticks_per_second", "max_physics_steps_per_frame", "physics_jitter_fix", "audio_playback_speed_scale" })
        {
            Expect(Array.IndexOf(simulation, setting) >= 0, $"\"{setting}\" is a simulation value");
            Expect(EngineSettingsServer.IsSimulationSetting(setting), $"IsSimulationSetting(\"{setting}\")");
            Expect(!server.IsValueSupported(setting), $"locked: \"{setting}\" is reported as unsupported");
        }
        Expect(!EngineSettingsServer.IsSimulationSetting("max_fps"), "max_fps is not a simulation value");
        Expect(server.IsValueSupported("max_fps"), "max_fps is always supported");

        EngineSettingsServer.SetTimeScale(3.0);
        ExpectEqual(EngineSettingsServer.GetTimeScale(), 1.0, "locked: the time scale stays at the project value");
        ExpectEqual(Engine.TimeScale, 1.0, "locked: Engine.TimeScale is unchanged");
        Reset();
    }

    private void CheckValidation()
    {
        var server = EngineSettingsServer.Singleton;
        SetAllowSimulation(true);
        server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        EngineSettingsServer.SetMaxFps(-5);
        ExpectEqual(EngineSettingsServer.GetMaxFps(), 0, "max_fps is at least 0 (unlimited)");
        EngineSettingsServer.SetTimeScale(0.0);
        Expect(Approx(EngineSettingsServer.GetTimeScale(), 0.01), "the time scale is at least 0.01");
        EngineSettingsServer.SetPhysicsTicksPerSecond(0);
        ExpectEqual(EngineSettingsServer.GetPhysicsTicksPerSecond(), 1, "the physics rate is at least 1");
        EngineSettingsServer.SetMaxPhysicsStepsPerFrame(1000);
        ExpectEqual(EngineSettingsServer.GetMaxPhysicsStepsPerFrame(), 100, "the physics steps are at most 100");
        EngineSettingsServer.SetAudioPlaybackSpeedScale(10.0f);
        Expect(Approx(EngineSettingsServer.GetAudioPlaybackSpeedScale(), 4.0), "the audio speed is at most 4");
        ExpectEqual(Engine.TimeScale, 1.0, "manual mode: nothing reached the engine before Apply()");
        server.Revert();
        server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        Reset();
    }

    private void CheckSessionOverride()
    {
        var server = EngineSettingsServer.Singleton;
        EngineSettingsServer.SetMaxFps(120);
        server.SetSessionOverride("max_fps", 30);
        ExpectEqual(Engine.MaxFps, 30, "a session override reaches the engine");
        Expect(server.HasSessionOverride("max_fps"), "HasSessionOverride()");
        ExpectEqual(EngineSettingsServer.GetMaxFps(), 120, "the stored value stays");
        server.ReadFromEngine();
        ExpectEqual(EngineSettingsServer.GetMaxFps(), 120, "ReadFromEngine() does not store the override");
        server.ClearSessionOverride("max_fps");
        ExpectEqual(Engine.MaxFps, 120, "ClearSessionOverride() gives back the stored value");
        Reset();
    }

    private void CheckResource()
    {
        var settings = new EngineSettings();
        ExpectEqual(settings.MaxFps, 0, "EngineSettings: default max_fps");
        ExpectEqual(settings.TimeScale, 1.0, "EngineSettings: default time scale");
        ExpectEqual(settings.PhysicsTicksPerSecond, 60, "EngineSettings: default physics rate");
        ExpectEqual(settings.LowProcessorMode, false, "EngineSettings: default low-processor mode");
        settings.TimeScale = 250.0;
        ExpectEqual(settings.TimeScale, 250.0, "EngineSettings: the resource stores raw values; the server clamps");
    }

    private void CheckRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart check (needs a desktop platform to start a second process)");
            return;
        }
        var server = EngineSettingsServer.Singleton;

        // 1) Saved values are applied before the main scene; a stored time scale is ignored while the project locks it.
        // Save() writes the applied values; the domain is reset only after the probes, so the file stays as saved.
        SetAllowSimulation(true);
        EngineSettingsServer.SetMaxFps(45);
        EngineSettingsServer.SetPhysicsInterpolationEnabled(true);
        EngineSettingsServer.SetLowProcessorModeSleepUsec(12000);
        EngineSettingsServer.SetTimeScale(0.5);
        ExpectEqual(server.Save(), Error.Ok, "Save() writes the player file");
        SetAllowSimulation(false);
        // Plain INI: read as text.
        string text = FileAccess.GetFileAsString(EngineFile);
        Expect(text.Contains("max_fps") && text.Contains("45"), "the file holds max_fps = 45");

        var probe = RunProbe([]);
        Expect(ProbeBool(probe, "started"), "restart: the domain was started");
        ExpectEqual(ProbeInt(probe, "ready_frame"), 0, "restart: applied before the first frame");
        ExpectEqual(ProbeInt(probe, "engine_max_fps"), 45, "restart: Engine.MaxFps is the saved value");
        Expect(ProbeBool(probe, "physics_interpolation"), "restart: the SceneTree has physics interpolation");
        ExpectEqual(ProbeInt(probe, "sleep_usec"), 12000, "restart: OS has the saved low-processor sleep");
        Expect(ProbeBool(probe, "simulation_locked"), "restart: the project locks the simulation values");
        Expect(Approx(ProbeDouble(probe, "engine_time_scale"), 1.0), "restart: a stored time scale is ignored while locked");

        // 2) --max-fps and --time-scale are session overrides: active (even when locked), but never saved.
        probe = RunProbe(["--max-fps", "30", "--time-scale", "2"]);
        ExpectEqual(ProbeInt(probe, "engine_max_fps"), 30, "--max-fps reaches the engine");
        Expect(Approx(ProbeDouble(probe, "engine_time_scale"), 2.0), "--time-scale reaches the engine, even when locked");
        Expect(ProbeBool(probe, "max_fps_overridden"), "--max-fps is a session override");
        Expect(ProbeBool(probe, "time_scale_overridden"), "--time-scale is a session override");
        ExpectEqual(ProbeInt(probe, "max_fps"), 45, "--max-fps does not change the stored value");
        Expect(FileAccess.GetFileAsString(EngineFile).Contains("45"), "--max-fps is not saved: the file still has 45");
    }

    private static int ProbeInt(Godot.Collections.Dictionary probe, string key) => probe.ContainsKey(key) ? probe[key].AsInt32() : -1;

    private static double ProbeDouble(Godot.Collections.Dictionary probe, string key) => probe.ContainsKey(key) ? probe[key].AsDouble() : -1.0;

    private static bool ProbeBool(Godot.Collections.Dictionary probe, string key) => probe.ContainsKey(key) && probe[key].AsBool();

    private Godot.Collections.Dictionary RunProbe(string[] extraArgs)
    {
        var output = new Godot.Collections.Array();
        var args = new System.Collections.Generic.List<string> { "--headless" };
        args.AddRange(extraArgs);
        args.AddRange(["--path", ProjectSettings.GlobalizePath("res://"), ProbeScene]);
        int code = OS.Execute(OS.GetExecutablePath(), args.ToArray(), output, true);
        ExpectEqual(code, 0, "restart: the probe process ran");
        foreach (Variant chunk in output)
        {
            foreach (string line in chunk.AsString().Split('\n'))
            {
                if (line.StartsWith("CHECK26_PROBE "))
                {
                    Variant parsed = Json.ParseString(line["CHECK26_PROBE ".Length..]);
                    if (parsed.VariantType == Variant.Type.Dictionary)
                    {
                        return parsed.AsGodotDictionary();
                    }
                }
            }
        }
        Expect(false, "restart: the probe printed its result");
        return new Godot.Collections.Dictionary();
    }
}
