using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.XrSettings;

using XRSettings = Godot.XRSettings;

/// <summary>
/// Full API test of XR Settings (XRSettingsServer, XRSettings). Same tests as scripts/gd/Test.gd.
/// The XR values go to the OpenXR interface of XRServer. The project runs without a headset, so the real interface is not
/// initialized: values are stored and deferred. A fake interface (fake_openxr_interface.gd) with the same API shows the
/// deferred application, the session handling and the unsupported values.
/// </summary>
public partial class Test : FeatureTest
{
    private const string XrFile = "user://settings/xr.ini";
    private const string FakeInterfacePath = "res://xr-settings/scripts/gd/fake_openxr_interface.gd";
    private const int PerformanceBoost = 3; // OpenXRInterface.PERF_SETTINGS_LEVEL_BOOST

    private readonly Godot.Collections.Dictionary _received = [];

    protected override Task RunTests()
    {
        XRSettingsServer.Singleton.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        XRSettingsServer.Singleton.ResetToDefaults();
        TestDefaults();
        TestValues();
        TestValidation();
        TestWithoutHeadset();
        TestDeferredUntilSession();
        TestRefreshRateMatching();
        TestUnsupported();
        TestSignals();
        TestStorage();
        TestResource();
        XRSettingsServer.ClearInterfaceOverride();
        XRSettingsServer.Singleton.ResetToDefaults();
        DirAccess.RemoveAbsolute(XrFile);
        return Task.CompletedTask;
    }

    private static GodotObject NewFakeInterface()
    {
        return (GodotObject)GD.Load<GDScript>(FakeInterfacePath).New();
    }

    private void TestDefaults()
    {
        int projectLevel = (int)ProjectSettings.GetSetting("xr/openxr/foveation_level", 0);
        bool projectDynamic = (bool)ProjectSettings.GetSetting("xr/openxr/foveation_dynamic", false);
        ExpectEqual((int)XRSettingsServer.GetFoveationLevel(), projectLevel, "the default foveation level is the project setting");
        ExpectEqual(XRSettingsServer.IsFoveationDynamic(), projectDynamic, "the default dynamic foveation is the project setting");
        ExpectEqual(XRSettingsServer.GetRenderTargetSizeMultiplier(), 1.0f, "the default render scale is 1");
        ExpectEqual(XRSettingsServer.GetDisplayRefreshRate(), 0.0f, "the default refresh rate is 0, the runtime default");
        ExpectEqual(XRSettingsServer.GetVrsMinRadius(), 20.0f, "the default VRS radius is 20");
        ExpectEqual(XRSettingsServer.GetVrsStrength(), 1.0f, "the default VRS strength is 1");
        ExpectEqual(XRSettingsServer.GetCpuLevel(), XRSettings.PerformanceLevel.RuntimeDefault, "the default CPU level is the runtime default");
        ExpectEqual(XRSettingsServer.GetGpuLevel(), XRSettings.PerformanceLevel.RuntimeDefault, "the default GPU level is the runtime default");
        ExpectEqual(XRSettingsServer.GetLeftHandMotionRange(), XRSettings.HandMotionRange.Unobstructed, "the default left motion range");
        Expect(XRSettingsServer.IsHandTrackingEnabled(), "hand tracking is on by default");
        ExpectEqual(XRSettingsServer.GetInterfaceName(), new StringName("OpenXR"), "GetInterfaceName returns OpenXR");
        SettingsServer.SettingFlags flags = XRSettingsServer.Singleton.GetSettingFlags("hand_tracking_enabled");
        Expect(flags.HasFlag(SettingsServer.SettingFlags.NoEngineTarget), "hand_tracking_enabled is game-side");
    }

    private void TestValues()
    {
        XRSettingsServer.SetFoveationLevel(XRSettings.FoveationLevelEnum.High);
        ExpectEqual(XRSettingsServer.GetFoveationLevel(), XRSettings.FoveationLevelEnum.High, "foveation_level round trip");
        XRSettingsServer.SetFoveationDynamic(true);
        Expect(XRSettingsServer.IsFoveationDynamic(), "foveation_dynamic round trip");
        XRSettingsServer.SetFoveationWithSubsampledImages(false);
        Expect(!XRSettingsServer.IsFoveationWithSubsampledImages(), "foveation_with_subsampled_images round trip");
        XRSettingsServer.SetRenderTargetSizeMultiplier(1.5f);
        ExpectEqual(XRSettingsServer.GetRenderTargetSizeMultiplier(), 1.5f, "render_target_size_multiplier round trip");
        XRSettingsServer.SetDisplayRefreshRate(90.0f);
        ExpectEqual(XRSettingsServer.GetDisplayRefreshRate(), 90.0f, "display_refresh_rate round trip");
        XRSettingsServer.SetVrsMinRadius(40.0f);
        ExpectEqual(XRSettingsServer.GetVrsMinRadius(), 40.0f, "vrs_min_radius round trip");
        XRSettingsServer.SetVrsStrength(2.0f);
        ExpectEqual(XRSettingsServer.GetVrsStrength(), 2.0f, "vrs_strength round trip");
        XRSettingsServer.SetCpuLevel(XRSettings.PerformanceLevel.SustainedHigh);
        ExpectEqual(XRSettingsServer.GetCpuLevel(), XRSettings.PerformanceLevel.SustainedHigh, "cpu_level round trip");
        XRSettingsServer.SetGpuLevel(XRSettings.PerformanceLevel.Boost);
        ExpectEqual(XRSettingsServer.GetGpuLevel(), XRSettings.PerformanceLevel.Boost, "gpu_level round trip");
        XRSettingsServer.SetLeftHandMotionRange(XRSettings.HandMotionRange.ConformToController);
        ExpectEqual(XRSettingsServer.GetLeftHandMotionRange(), XRSettings.HandMotionRange.ConformToController, "left_hand_motion_range round trip");
        XRSettingsServer.SetRightHandMotionRange(XRSettings.HandMotionRange.ConformToController);
        ExpectEqual(XRSettingsServer.GetRightHandMotionRange(), XRSettings.HandMotionRange.ConformToController, "right_hand_motion_range round trip");
        XRSettingsServer.SetHandTrackingEnabled(false);
        Expect(!XRSettingsServer.IsHandTrackingEnabled(), "hand_tracking_enabled round trip");
        XRSettingsServer.Singleton.ResetToDefaults();
    }

    private void TestValidation()
    {
        XRSettingsServer.SetRenderTargetSizeMultiplier(10.0f);
        ExpectEqual(XRSettingsServer.GetRenderTargetSizeMultiplier(), 3.0f, "the render scale is clamped to 3");
        XRSettingsServer.SetRenderTargetSizeMultiplier(0.0f);
        Expect(Mathf.IsEqualApprox(XRSettingsServer.GetRenderTargetSizeMultiplier(), 0.1f), "the render scale is clamped to 0.1");
        XRSettingsServer.SetDisplayRefreshRate(-60.0f);
        ExpectEqual(XRSettingsServer.GetDisplayRefreshRate(), 0.0f, "a negative refresh rate is clamped to 0");
        XRSettingsServer.SetVrsStrength(50.0f);
        ExpectEqual(XRSettingsServer.GetVrsStrength(), 10.0f, "the VRS strength is clamped to 10");
        XRSettingsServer.Singleton.ResetToDefaults();
    }

    private void TestWithoutHeadset()
    {
        XRSettingsServerInstance server = XRSettingsServer.Singleton;
        Expect(!XRSettingsServer.IsInterfaceReady(), "there is no initialized OpenXR interface in the test project");
        Expect(!XRSettingsServer.IsSessionRunning(), "there is no XR session");
        XRSettingsServer.SetFoveationLevel(XRSettings.FoveationLevelEnum.High);
        XRSettingsServer.SetGpuLevel(XRSettings.PerformanceLevel.Boost);
        ExpectEqual(server.GetAppliedValue("foveation_level").AsInt32(), (int)XRSettings.FoveationLevelEnum.High, "the value counts as applied without a headset");
        string[] deferred = XRSettingsServer.GetDeferredSettingNames();
        Expect(System.Array.IndexOf(deferred, "foveation_level") >= 0, "foveation_level waits for the headset");
        Expect(System.Array.IndexOf(deferred, "gpu_level") >= 0, "gpu_level waits for the headset");
        Expect(XRSettingsServer.GetUnsupportedSettingNames().Length == 0, "nothing is unsupported before the runtime is known");
        Expect(XRSettingsServer.GetAvailableDisplayRefreshRates().Length == 0, "there are no refresh rates without a runtime");
        Expect(XRSettingsServer.GetInterface() != null, "GetInterface returns the OpenXR interface");
        server.ResetToDefaults();
    }

    private void TestDeferredUntilSession()
    {
        GodotObject fake = NewFakeInterface();
        fake.Set("refresh_rates", new Godot.Collections.Array { 72.0, 90.0, 120.0 });
        XRSettingsServer.SetInterfaceOverride(fake);
        Expect(XRSettingsServer.GetInterface() == fake, "SetInterfaceOverride replaces the interface");
        XRSettingsServer.SetRenderTargetSizeMultiplier(1.5f);
        XRSettingsServer.SetFoveationLevel(XRSettings.FoveationLevelEnum.Low);
        XRSettingsServer.SetFoveationDynamic(true);
        XRSettingsServer.SetDisplayRefreshRate(90.0f);
        XRSettingsServer.SetGpuLevel(XRSettings.PerformanceLevel.Boost);
        XRSettingsServer.SetLeftHandMotionRange(XRSettings.HandMotionRange.ConformToController);
        ExpectEqual(fake.Get("render_target_size_multiplier").AsSingle(), 1.5f, "the render scale goes to the interface before it is initialized");
        ExpectEqual(fake.Get("foveation_level").AsInt32(), 0, "foveation waits for the session");
        ExpectEqual(fake.Get("gpu_level").AsInt32(), -1, "the GPU level waits for the session");
        string[] deferred = XRSettingsServer.GetDeferredSettingNames();
        Expect(System.Array.IndexOf(deferred, "render_target_size_multiplier") < 0, "the render scale is not deferred");
        Expect(System.Array.IndexOf(deferred, "foveation_level") >= 0, "foveation is deferred");
        Expect(System.Array.IndexOf(deferred, "display_refresh_rate") >= 0, "the refresh rate is deferred");
        fake.Call("begin_session");
        Expect(XRSettingsServer.IsInterfaceReady(), "IsInterfaceReady is true after initialize");
        Expect(XRSettingsServer.IsSessionRunning(), "IsSessionRunning is true after session_begun");
        Expect(XRSettingsServer.GetDeferredSettingNames().Length == 0, "nothing is deferred after the session began");
        ExpectEqual(fake.Get("foveation_level").AsInt32(), 1, "the foveation level is pushed when the session begins");
        Expect(fake.Get("foveation_dynamic").AsBool(), "dynamic foveation is pushed");
        ExpectEqual(fake.Get("display_refresh_rate").AsSingle(), 90.0f, "the refresh rate is pushed");
        ExpectEqual(fake.Get("gpu_level").AsInt32(), PerformanceBoost, "the GPU level is pushed as the OpenXR boost level");
        ExpectEqual(fake.Get("cpu_level").AsInt32(), -1, "the runtime default CPU level is never pushed");
        ExpectEqual(fake.Call("get_motion_range", 0).AsInt32(), 1, "the left motion range is pushed");
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

    private void TestRefreshRateMatching()
    {
        GodotObject fake = NewFakeInterface();
        fake.Set("initialized", true);
        fake.Set("refresh_rates", new Godot.Collections.Array { 120.0, 72.0, 90.0 });
        XRSettingsServer.SetInterfaceOverride(fake);
        double[] rates = XRSettingsServer.GetAvailableDisplayRefreshRates();
        Expect(rates.AsSpan().SequenceEqual([72.0, 90.0, 120.0]), "the rates of the runtime are sorted");
        XRSettingsServer.SetDisplayRefreshRate(100.0f);
        ExpectEqual(fake.Get("display_refresh_rate").AsSingle(), 90.0f, "100 Hz goes to the nearest rate, 90 Hz");
        ExpectEqual(XRSettingsServer.GetDisplayRefreshRate(), 100.0f, "the choice of the player stays stored");
        XRSettingsServer.SetDisplayRefreshRate(0.0f);
        ExpectEqual(fake.Get("display_refresh_rate").AsSingle(), 90.0f, "0, the runtime default, is not pushed");
        XRSettingsServer.ClearInterfaceOverride();
        fake.Free();
        XRSettingsServer.Singleton.ResetToDefaults();
    }

    private void TestUnsupported()
    {
        XRSettingsServerInstance server = XRSettingsServer.Singleton;
        GodotObject fake = NewFakeInterface();
        fake.Set("initialized", true);
        fake.Set("foveation_supported", false);
        fake.Set("hand_tracking_supported", false);
        XRSettingsServer.SetInterfaceOverride(fake);
        string[] unsupported = XRSettingsServer.GetUnsupportedSettingNames();
        foreach (string setting in new[] { "foveation_level", "foveation_dynamic", "foveation_with_subsampled_images", "display_refresh_rate", "left_hand_motion_range", "right_hand_motion_range", "hand_tracking_enabled" })
        {
            Expect(System.Array.IndexOf(unsupported, setting) >= 0, $"{setting} is unsupported by this runtime");
        }
        Expect(System.Array.IndexOf(unsupported, "render_target_size_multiplier") < 0, "the render scale works with every runtime");
        Expect(server.IsValueSupported("vrs_strength"), "vrs_strength is supported");
        Expect(!server.IsValueSupported("foveation_level"), "foveation_level is not supported");
        XRSettingsServer.SetFoveationLevel(XRSettings.FoveationLevelEnum.High);
        ExpectEqual(XRSettingsServer.GetFoveationLevel(), XRSettings.FoveationLevelEnum.High, "an unsupported value is stored");
        ExpectEqual(fake.Get("foveation_level").AsInt32(), 0, "an unsupported value is not pushed");
        Expect(System.Array.IndexOf(XRSettingsServer.GetDeferredSettingNames(), "foveation_level") < 0, "an unsupported value is not deferred");
        XRSettingsServer.SetVrsMinRadius(40.0f);
        ExpectEqual(fake.Get("vrs_min_radius").AsSingle(), 40.0f, "supported values still reach the interface");
        XRSettingsServer.ClearInterfaceOverride();
        fake.Free();
        server.ResetToDefaults();
    }

    private void TestSignals()
    {
        XRSettingsServerInstance server = XRSettingsServer.Singleton;
        server.FoveationLevelChanged += level => _received["foveation_level"] = (int)level;
        server.FoveationDynamicChanged += enabled => _received["foveation_dynamic"] = enabled;
        server.FoveationWithSubsampledImagesChanged += enabled => _received["subsampled"] = enabled;
        server.RenderTargetSizeMultiplierChanged += multiplier => _received["multiplier"] = multiplier;
        server.DisplayRefreshRateChanged += rate => _received["refresh_rate"] = rate;
        server.VrsMinRadiusChanged += radius => _received["vrs_radius"] = radius;
        server.VrsStrengthChanged += strength => _received["vrs_strength"] = strength;
        server.CpuLevelChanged += level => _received["cpu_level"] = (int)level;
        server.GpuLevelChanged += level => _received["gpu_level"] = (int)level;
        server.LeftHandMotionRangeChanged += range => _received["left_range"] = (int)range;
        server.RightHandMotionRangeChanged += range => _received["right_range"] = (int)range;
        server.HandTrackingEnabledChanged += enabled => _received["hand_tracking"] = enabled;
        server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        XRSettingsServer.SetFoveationLevel(XRSettings.FoveationLevelEnum.Medium);
        XRSettingsServer.SetFoveationDynamic(true);
        XRSettingsServer.SetFoveationWithSubsampledImages(false);
        XRSettingsServer.SetRenderTargetSizeMultiplier(1.5f);
        XRSettingsServer.SetDisplayRefreshRate(90.0f);
        XRSettingsServer.SetVrsMinRadius(40.0f);
        XRSettingsServer.SetVrsStrength(2.0f);
        XRSettingsServer.SetCpuLevel(XRSettings.PerformanceLevel.SustainedLow);
        XRSettingsServer.SetGpuLevel(XRSettings.PerformanceLevel.SustainedHigh);
        XRSettingsServer.SetLeftHandMotionRange(XRSettings.HandMotionRange.ConformToController);
        XRSettingsServer.SetRightHandMotionRange(XRSettings.HandMotionRange.ConformToController);
        XRSettingsServer.SetHandTrackingEnabled(false);
        Expect(_received.Count == 0, "no typed signal fires for pending changes in manual mode");
        server.Apply();
        ExpectEqual((int)_received["foveation_level"], (int)XRSettings.FoveationLevelEnum.Medium, "FoveationLevelChanged passes the level, also without a headset");
        Expect((bool)_received["foveation_dynamic"], "FoveationDynamicChanged passes the flag");
        Expect(!(bool)_received["subsampled"], "FoveationWithSubsampledImagesChanged passes the flag");
        ExpectEqual((float)_received["multiplier"], 1.5f, "RenderTargetSizeMultiplierChanged passes the multiplier");
        ExpectEqual((float)_received["refresh_rate"], 90.0f, "DisplayRefreshRateChanged passes the rate");
        ExpectEqual((float)_received["vrs_radius"], 40.0f, "VrsMinRadiusChanged passes the radius");
        ExpectEqual((float)_received["vrs_strength"], 2.0f, "VrsStrengthChanged passes the strength");
        ExpectEqual((int)_received["cpu_level"], (int)XRSettings.PerformanceLevel.SustainedLow, "CpuLevelChanged passes the level");
        ExpectEqual((int)_received["gpu_level"], (int)XRSettings.PerformanceLevel.SustainedHigh, "GpuLevelChanged passes the level");
        ExpectEqual((int)_received["left_range"], (int)XRSettings.HandMotionRange.ConformToController, "LeftHandMotionRangeChanged passes the range");
        ExpectEqual((int)_received["right_range"], (int)XRSettings.HandMotionRange.ConformToController, "RightHandMotionRangeChanged passes the range");
        Expect(!(bool)_received["hand_tracking"], "HandTrackingEnabledChanged passes the flag, a game-side value");
        server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        server.ResetToDefaults();
    }

    private void TestStorage()
    {
        XRSettingsServerInstance server = XRSettingsServer.Singleton;
        XRSettingsServer.SetFoveationLevel(XRSettings.FoveationLevelEnum.Low);
        XRSettingsServer.SetRenderTargetSizeMultiplier(1.25f);
        XRSettingsServer.SetCpuLevel(XRSettings.PerformanceLevel.SustainedHigh);
        XRSettingsServer.SetRightHandMotionRange(XRSettings.HandMotionRange.ConformToController);
        XRSettingsServer.SetHandTrackingEnabled(false);
        ExpectEqual(server.Save(), Error.Ok, "Save works without a headset");
        server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        server.ResetToDefaults();
        ExpectEqual(server.Load(), Error.Ok, "Load reads the file");
        ExpectEqual(XRSettingsServer.GetFoveationLevel(), XRSettings.FoveationLevelEnum.Low, "the foveation level is loaded");
        ExpectEqual(XRSettingsServer.GetRenderTargetSizeMultiplier(), 1.25f, "the render scale is loaded");
        ExpectEqual(XRSettingsServer.GetCpuLevel(), XRSettings.PerformanceLevel.SustainedHigh, "the CPU level is loaded");
        ExpectEqual(XRSettingsServer.GetRightHandMotionRange(), XRSettings.HandMotionRange.ConformToController, "the right motion range is loaded");
        Expect(!XRSettingsServer.IsHandTrackingEnabled(), "the hand tracking choice is loaded");
        server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
    }

    private void TestResource()
    {
        var settings = new XRSettings();
        ExpectEqual(settings.FoveationLevel, XRSettings.FoveationLevelEnum.Off, "XRSettings: default FoveationLevel");
        Expect(settings.FoveationWithSubsampledImages, "XRSettings: FoveationWithSubsampledImages by default");
        ExpectEqual(settings.RenderTargetSizeMultiplier, 1.0f, "XRSettings: default RenderTargetSizeMultiplier");
        ExpectEqual(settings.DisplayRefreshRate, 0.0f, "XRSettings: default DisplayRefreshRate");
        ExpectEqual(settings.VrsMinRadius, 20.0f, "XRSettings: default VrsMinRadius");
        ExpectEqual(settings.VrsStrength, 1.0f, "XRSettings: default VrsStrength");
        ExpectEqual(settings.CpuLevel, XRSettings.PerformanceLevel.RuntimeDefault, "XRSettings: default CpuLevel");
        Expect(settings.HandTrackingEnabled, "XRSettings: HandTrackingEnabled by default");
        settings.VrsStrength = 2.0f;
        ExpectEqual(settings.VrsStrength, 2.0f, "XRSettings: VrsStrength is a plain property");
    }
}
