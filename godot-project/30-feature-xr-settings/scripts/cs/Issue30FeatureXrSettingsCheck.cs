using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue30FeatureXrSettings;

/// <summary>
/// C# checks of 30-feature-xr-settings. Check the same things as scripts/gd/check.gd.
/// Typed methods are called on the generated static class, the methods of the base class through
/// <c>Singleton</c>. The project runs without a headset: values are stored and deferred. The fake interface
/// (scripts/gd/fake_openxr_interface.gd) has the API of OpenXRInterface and shows the session handling.
/// </summary>
public partial class Issue30FeatureXrSettingsCheck : FeatureCheckCs
{
    private const string XrFile = "user://settings/xr.ini";
    private const string FakeInterfacePath = "res://30-feature-xr-settings/scripts/gd/fake_openxr_interface.gd";

    private static readonly string[] Names =
    {
        "foveation_level", "foveation_dynamic", "foveation_with_subsampled_images", "render_target_size_multiplier",
        "display_refresh_rate", "vrs_min_radius", "vrs_strength", "cpu_level", "gpu_level", "left_hand_motion_range",
        "right_hand_motion_range", "hand_tracking_enabled",
    };

    protected override string CheckName => "30-feature-xr-settings";

    protected override async Task RunChecks()
    {
        Expect(Engine.HasSingleton("XRSettingsServer"), "XRSettingsServer is a registered singleton (the editor build has the openxr module)");
        if (!Engine.HasSingleton("XRSettingsServer"))
        {
            return;
        }
        var server = XRSettingsServer.Singleton;
        Expect(server is XRSettingsServerInstance, "XRSettingsServer.Singleton is the instance type");
        Expect(Engine.GetSingleton("XRSettingsServer") is SettingsServer, "XRSettingsServerInstance derives from SettingsServer");
        Expect(SettingsServerManager.GetServer("xr") == server, "the domain is registered as \"xr\"");
        Expect(server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(server.GetApplyPhase(), SettingsServer.ApplyPhase.Scene, "applied after the SceneTree exists");
        ExpectEqual(server.GetFilePath(), XrFile, "file path");
        Expect(server.GetSettings() is XRSettings, "the settings resource is an XRSettings");
        ExpectEqual(XRSettingsServer.GetInterfaceName(), (StringName)"OpenXR", "GetInterfaceName()");
        var names = server.GetSettingNames();
        ExpectEqual(names.Length, Names.Length, "12 values");
        foreach (string setting in Names)
        {
            Expect(Array.IndexOf(names, setting) >= 0, $"setting \"{setting}\" exists");
            Expect(server.HasSignal(setting + "_changed"), $"typed signal {setting}_changed exists");
        }
        Expect(server.GetSettingFlags("hand_tracking_enabled").HasFlag(SettingsServer.SettingFlags.NoEngineTarget), "hand_tracking_enabled is game-side");
        Expect(!server.GetSettingFlags("foveation_level").HasFlag(SettingsServer.SettingFlags.NoEngineTarget), "foveation_level goes to the engine");

        server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        server.ResetToDefaults();
        CheckDefaults();
        CheckValidation();
        CheckWithoutHeadset();
        CheckDeferredUntilSession();
        CheckRefreshRateMatching();
        CheckUnsupported();
        CheckTypedSignals();
        CheckStorage();

        // Leave no player file behind.
        XRSettingsServer.ClearInterfaceOverride();
        server.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(XrFile);
    }

    // XR values are floats: compare at float precision (the double overload uses 1e-14).
    private static bool Approx(double a, double b) => Mathf.IsEqualApprox((float)a, (float)b);

    private static GodotObject NewFakeInterface() => (GodotObject)GD.Load<GDScript>(FakeInterfacePath).New();

    private void CheckDefaults()
    {
        var server = XRSettingsServer.Singleton;
        ExpectEqual(server.GetDefaultValue("foveation_level").AsInt32(), ProjectSettings.GetSetting("xr/openxr/foveation_level", 0).AsInt32(), "default foveation level is the project setting");
        ExpectEqual(server.GetDefaultValue("foveation_dynamic").AsBool(), ProjectSettings.GetSetting("xr/openxr/foveation_dynamic", false).AsBool(), "default dynamic foveation is the project setting");
        Expect(Approx(XRSettingsServer.GetRenderTargetSizeMultiplier(), 1.0), "default render scale 1");
        Expect(Approx(XRSettingsServer.GetDisplayRefreshRate(), 0.0), "default refresh rate 0 (runtime default)");
        Expect(Approx(XRSettingsServer.GetVrsMinRadius(), 20.0), "default VRS radius 20");
        Expect(Approx(XRSettingsServer.GetVrsStrength(), 1.0), "default VRS strength 1");
        ExpectEqual(XRSettingsServer.GetCpuLevel(), XRSettings.PerformanceLevel.RuntimeDefault, "default CPU level: runtime default");
        ExpectEqual(XRSettingsServer.GetGpuLevel(), XRSettings.PerformanceLevel.RuntimeDefault, "default GPU level: runtime default");
        ExpectEqual(XRSettingsServer.GetLeftHandMotionRange(), XRSettings.HandMotionRange.Unobstructed, "default left motion range");
        Expect(XRSettingsServer.IsHandTrackingEnabled(), "hand tracking on by default");
    }

    private void CheckValidation()
    {
        XRSettingsServer.SetRenderTargetSizeMultiplier(10.0f);
        Expect(Approx(XRSettingsServer.GetRenderTargetSizeMultiplier(), 3.0), "render scale clamped to 3");
        XRSettingsServer.SetRenderTargetSizeMultiplier(0.0f);
        Expect(Approx(XRSettingsServer.GetRenderTargetSizeMultiplier(), 0.1), "render scale clamped to 0.1");
        XRSettingsServer.SetDisplayRefreshRate(-60.0f);
        Expect(Approx(XRSettingsServer.GetDisplayRefreshRate(), 0.0), "negative refresh rate clamped to 0");
        XRSettingsServer.SetVrsStrength(50.0f);
        Expect(Approx(XRSettingsServer.GetVrsStrength(), 10.0), "VRS strength clamped to 10");
        XRSettingsServer.Singleton.ResetToDefaults();
    }

    private void CheckWithoutHeadset()
    {
        var server = XRSettingsServer.Singleton;
        // The real OpenXR interface exists but is not initialized (no xr/openxr/enabled, no headset).
        Expect(!XRSettingsServer.IsInterfaceReady(), "no initialized OpenXR interface in the test project");
        Expect(!XRSettingsServer.IsSessionRunning(), "no XR session");
        XRSettingsServer.SetFoveationLevel(XRSettings.FoveationLevelEnum.High);
        XRSettingsServer.SetGpuLevel(XRSettings.PerformanceLevel.Boost);
        ExpectEqual(XRSettingsServer.GetFoveationLevel(), XRSettings.FoveationLevelEnum.High, "the value is stored without a headset");
        ExpectEqual(server.GetAppliedValue("foveation_level").AsInt32(), (int)XRSettings.FoveationLevelEnum.High, "the value counts as applied");
        var deferred = XRSettingsServer.GetDeferredSettingNames();
        Expect(Array.IndexOf(deferred, "foveation_level") >= 0, "foveation_level waits for the headset");
        Expect(Array.IndexOf(deferred, "gpu_level") >= 0, "gpu_level waits for the headset");
        ExpectEqual(XRSettingsServer.GetUnsupportedSettingNames().Length, 0, "nothing is unsupported before the runtime is known");
        ExpectEqual(XRSettingsServer.GetAvailableDisplayRefreshRates().Length, 0, "no refresh rates without a runtime");
        server.ResetToDefaults();
    }

    private void CheckDeferredUntilSession()
    {
        var fake = NewFakeInterface();
        fake.Set("refresh_rates", new Godot.Collections.Array { 72.0, 90.0, 120.0 });
        XRSettingsServer.SetInterfaceOverride(fake);
        Expect(XRSettingsServer.GetInterface() == fake, "SetInterfaceOverride()");

        XRSettingsServer.SetRenderTargetSizeMultiplier(1.5f);
        XRSettingsServer.SetFoveationLevel(XRSettings.FoveationLevelEnum.Low);
        XRSettingsServer.SetFoveationDynamic(true);
        XRSettingsServer.SetDisplayRefreshRate(90.0f);
        XRSettingsServer.SetGpuLevel(XRSettings.PerformanceLevel.Boost);
        XRSettingsServer.SetLeftHandMotionRange(XRSettings.HandMotionRange.ConformToController);
        Expect(Approx(fake.Get("render_target_size_multiplier").AsDouble(), 1.5), "the render scale goes to the interface before it is initialized");
        ExpectEqual(fake.Get("foveation_level").AsInt32(), 0, "foveation waits for the session");
        ExpectEqual(fake.Get("gpu_level").AsInt32(), -1, "the GPU level waits for the session");
        var deferred = XRSettingsServer.GetDeferredSettingNames();
        Expect(Array.IndexOf(deferred, "render_target_size_multiplier") < 0, "the render scale is not deferred");
        Expect(Array.IndexOf(deferred, "foveation_level") >= 0 && Array.IndexOf(deferred, "display_refresh_rate") >= 0, "foveation and refresh rate are deferred");

        fake.Call("begin_session");
        Expect(XRSettingsServer.IsInterfaceReady(), "IsInterfaceReady() after initialize");
        Expect(XRSettingsServer.IsSessionRunning(), "IsSessionRunning() after session_begun");
        ExpectEqual(XRSettingsServer.GetDeferredSettingNames().Length, 0, "nothing deferred after the session began");
        ExpectEqual(fake.Get("foveation_level").AsInt32(), 1, "foveation level pushed when the session begins");
        Expect(fake.Get("foveation_dynamic").AsBool(), "dynamic foveation pushed");
        Expect(Approx(fake.Get("display_refresh_rate").AsDouble(), 90.0), "refresh rate pushed");
        ExpectEqual(fake.Get("gpu_level").AsInt32(), 3, "GPU level pushed as OpenXRInterface.PERF_SETTINGS_LEVEL_BOOST");
        ExpectEqual(fake.Get("cpu_level").AsInt32(), -1, "the runtime default CPU level is never pushed");
        ExpectEqual(fake.Call("get_motion_range", 0).AsInt32(), 1, "left motion range pushed");

        XRSettingsServer.SetFoveationLevel(XRSettings.FoveationLevelEnum.High);
        ExpectEqual(fake.Get("foveation_level").AsInt32(), 3, "changes during the session reach the interface at once");

        fake.Call("stop_session");
        Expect(!XRSettingsServer.IsSessionRunning(), "session_stopping ends the session");
        fake.Set("foveation_level", 0);
        fake.Call("begin_session");
        ExpectEqual(fake.Get("foveation_level").AsInt32(), 3, "a new session gets the values again");

        XRSettingsServer.ClearInterfaceOverride();
        fake.Free();
        XRSettingsServer.Singleton.ResetToDefaults();
    }

    private void CheckRefreshRateMatching()
    {
        var fake = NewFakeInterface();
        fake.Set("initialized", true);
        fake.Set("refresh_rates", new Godot.Collections.Array { 120.0, 72.0, 90.0 });
        XRSettingsServer.SetInterfaceOverride(fake);
        var rates = XRSettingsServer.GetAvailableDisplayRefreshRates();
        ExpectEqual(string.Join(",", rates), "72,90,120", "the rates of the runtime, sorted");
        XRSettingsServer.SetDisplayRefreshRate(100.0f);
        Expect(Approx(fake.Get("display_refresh_rate").AsDouble(), 90.0), "100 Hz goes to the nearest rate, 90 Hz");
        Expect(Approx(XRSettingsServer.GetDisplayRefreshRate(), 100.0), "the player's choice stays stored");
        XRSettingsServer.SetDisplayRefreshRate(0.0f);
        Expect(Approx(fake.Get("display_refresh_rate").AsDouble(), 90.0), "0 (runtime default) is not pushed");
        XRSettingsServer.ClearInterfaceOverride();
        fake.Free();
        XRSettingsServer.Singleton.ResetToDefaults();
    }

    private void CheckUnsupported()
    {
        var server = XRSettingsServer.Singleton;
        var fake = NewFakeInterface();
        fake.Set("initialized", true);
        fake.Set("foveation_supported", false);
        fake.Set("hand_tracking_supported", false);
        XRSettingsServer.SetInterfaceOverride(fake);
        var unsupported = XRSettingsServer.GetUnsupportedSettingNames();
        foreach (string setting in new[] { "foveation_level", "foveation_dynamic", "foveation_with_subsampled_images", "display_refresh_rate", "left_hand_motion_range", "right_hand_motion_range", "hand_tracking_enabled" })
        {
            Expect(Array.IndexOf(unsupported, setting) >= 0, $"\"{setting}\" is unsupported by this runtime");
        }
        Expect(Array.IndexOf(unsupported, "render_target_size_multiplier") < 0, "the render scale works with every runtime");
        Expect(server.IsValueSupported("vrs_strength"), "IsValueSupported(\"vrs_strength\")");
        Expect(!server.IsValueSupported("foveation_level"), "IsValueSupported(\"foveation_level\") is false");

        XRSettingsServer.SetFoveationLevel(XRSettings.FoveationLevelEnum.High);
        ExpectEqual(XRSettingsServer.GetFoveationLevel(), XRSettings.FoveationLevelEnum.High, "an unsupported value is stored");
        ExpectEqual(fake.Get("foveation_level").AsInt32(), 0, "an unsupported value is not pushed");
        Expect(Array.IndexOf(XRSettingsServer.GetDeferredSettingNames(), "foveation_level") < 0, "an unsupported value is not deferred");
        XRSettingsServer.SetVrsMinRadius(40.0f);
        Expect(Approx(fake.Get("vrs_min_radius").AsDouble(), 40.0), "supported values still reach the interface");
        XRSettingsServer.ClearInterfaceOverride();
        fake.Free();
        server.ResetToDefaults();
    }

    /// <summary>Issue #173: the typed signals fire once per applied change, also while the value waits for the headset.</summary>
    private void CheckTypedSignals()
    {
        var server = XRSettingsServer.Singleton;
        var levels = new List<Variant>();
        var hands = new List<Variant>();
        var onLevel = Callable.From((Variant v) => levels.Add(v));
        var onHands = Callable.From((Variant v) => hands.Add(v));
        server.Connect("foveation_level_changed", onLevel);
        server.Connect("hand_tracking_enabled_changed", onHands);

        server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        XRSettingsServer.SetFoveationLevel(XRSettings.FoveationLevelEnum.Medium);
        ExpectEqual(levels.Count, 0, "foveation_level_changed does not fire for a pending change in manual mode");
        server.Apply();
        ExpectEqual(levels.Count, 1, "foveation_level_changed fires once on Apply(), also without a headset");
        if (levels.Count > 0)
        {
            ExpectEqual(levels[0].AsInt32(), (int)XRSettings.FoveationLevelEnum.Medium, "foveation_level_changed passes the applied value");
        }
        server.Apply();
        ExpectEqual(levels.Count, 1, "no signal when nothing changed");
        XRSettingsServer.SetHandTrackingEnabled(false);
        server.Apply();
        ExpectEqual(hands.Count, 1, "hand_tracking_enabled_changed fires for the game-side value");
        if (hands.Count > 0)
        {
            Expect(!hands[0].AsBool(), "hand_tracking_enabled_changed passes false");
        }
        server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;

        server.Disconnect("foveation_level_changed", onLevel);
        server.Disconnect("hand_tracking_enabled_changed", onHands);
        server.ResetToDefaults();
    }

    private void CheckStorage()
    {
        var server = XRSettingsServer.Singleton;
        XRSettingsServer.SetFoveationLevel(XRSettings.FoveationLevelEnum.Low);
        XRSettingsServer.SetRenderTargetSizeMultiplier(1.25f);
        XRSettingsServer.SetCpuLevel(XRSettings.PerformanceLevel.SustainedHigh);
        XRSettingsServer.SetRightHandMotionRange(XRSettings.HandMotionRange.ConformToController);
        XRSettingsServer.SetHandTrackingEnabled(false);
        ExpectEqual(server.Save(), Error.Ok, "Save() without a headset");
        Expect(FileAccess.FileExists(XrFile), "the file exists");

        server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        server.ResetToDefaults();
        ExpectEqual(server.Load(), Error.Ok, "Load()");
        ExpectEqual(XRSettingsServer.GetFoveationLevel(), XRSettings.FoveationLevelEnum.Low, "foveation level loaded");
        Expect(Approx(XRSettingsServer.GetRenderTargetSizeMultiplier(), 1.25), "render scale loaded");
        ExpectEqual(XRSettingsServer.GetCpuLevel(), XRSettings.PerformanceLevel.SustainedHigh, "CPU level loaded");
        ExpectEqual(XRSettingsServer.GetRightHandMotionRange(), XRSettings.HandMotionRange.ConformToController, "right motion range loaded");
        Expect(!XRSettingsServer.IsHandTrackingEnabled(), "hand tracking choice loaded");
        server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
    }
}
