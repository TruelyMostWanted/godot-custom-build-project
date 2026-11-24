using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue28FeatureCameraSettings;

/// <summary>
/// C# checks of 28-feature-camera-settings. Check the same things as scripts/gd/check.gd.
/// Typed methods are called on the generated static class, the methods of the base class through
/// <c>Singleton</c>. The camera values go to the Camera3D nodes in the group "camera_3d_settings" and to added
/// cameras; cameras outside the group keep their authored values. The motion values are game-side.
/// </summary>
public partial class Issue28FeatureCameraSettingsCheck : FeatureCheckCs
{
    private const string CameraFile = "user://settings/camera.ini";

    private static readonly string[] Names =
    {
        "enabled", "field_of_view", "keep_aspect", "near", "far", "shake_intensity", "head_bob_intensity", "motion_blur_intensity",
    };

    private static readonly string[] GameSide = { "shake_intensity", "head_bob_intensity", "motion_blur_intensity" };

    protected override string CheckName => "28-feature-camera-settings";

    protected override async Task RunChecks()
    {
        await CheckTypedSignals();

        var server = Camera3DSettingsServer.Singleton;
        Expect(Engine.HasSingleton("Camera3DSettingsServer"), "Camera3DSettingsServer is a registered singleton");
        Expect(server is Camera3DSettingsServerInstance, "Camera3DSettingsServer.Singleton is the instance type");
        Expect(Engine.GetSingleton("Camera3DSettingsServer") is SettingsServer, "Camera3DSettingsServerInstance derives from SettingsServer");
        Expect(SettingsServerManager.GetServer("camera_3d") == server, "the domain is registered as \"camera\"");
        Expect(server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(server.GetApplyPhase(), SettingsServer.ApplyPhase.Scene, "applied after the SceneTree exists");
        ExpectEqual(server.GetFilePath(), CameraFile, "file path");
        Expect(server.GetSettings() is Camera3DSettings, "the settings resource is a Camera3DSettings");
        ExpectEqual(Camera3DSettingsServer.GetCameraGroup(), (StringName)"camera_3d_settings", "GetCameraGroup()");
        var names = server.GetSettingNames();
        ExpectEqual(names.Length, Names.Length, "8 values");
        foreach (string setting in Names)
        {
            Expect(Array.IndexOf(names, setting) >= 0, $"setting \"{setting}\" exists");
        }
        foreach (string setting in GameSide)
        {
            Expect(server.GetSettingFlags(setting).HasFlag(SettingsServer.SettingFlags.NoEngineTarget), $"\"{setting}\" is game-side");
        }

        server.ResetToDefaults();
        CheckDefaults();
        await CheckGroup();
        CheckAddedCamera();
        CheckGameSide();
        CheckValidation();
        await CheckStorageAndSceneChange();

        // Leave no player file behind.
        server.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(CameraFile);
    }

    // Camera values are floats: compare at float precision (the double overload uses 1e-14).
    private static bool Approx(double a, double b) => Mathf.IsEqualApprox((float)a, (float)b);

    private static Camera3D AuthoredCamera()
    {
        return new Camera3D { Fov = 60.0f, KeepAspect = Camera3D.KeepAspectEnum.Width, Near = 0.2f, Far = 500.0f };
    }

    private static bool IsAuthored(Camera3D camera)
    {
        return Approx(camera.Fov, 60.0) && camera.KeepAspect == Camera3D.KeepAspectEnum.Width && Approx(camera.Near, 0.2) && Approx(camera.Far, 500.0);
    }

    private void CheckDefaults()
    {
        var server = Camera3DSettingsServer.Singleton;
        Expect(Approx(server.GetDefaultValue("field_of_view").AsDouble(), ProjectSettings.GetSetting("settings/camera_3d/field_of_view").AsDouble()), "default FOV is the project setting");
        ExpectEqual(server.GetDefaultValue("keep_aspect").AsInt32(), ProjectSettings.GetSetting("settings/camera_3d/keep_aspect").AsInt32(), "default keep_aspect is the project setting");
        Expect(Approx(server.GetDefaultValue("near").AsDouble(), ProjectSettings.GetSetting("settings/camera_3d/near").AsDouble()), "default near is the project setting");
        Expect(Approx(server.GetDefaultValue("far").AsDouble(), ProjectSettings.GetSetting("settings/camera_3d/far").AsDouble()), "default far is the project setting");
        Expect(Camera3DSettingsServer.IsEnabled(), "enabled by default");
        Expect(Approx(Camera3DSettingsServer.GetShakeIntensity(), 1.0), "default shake intensity 1");
    }

    private async Task CheckGroup()
    {
        Camera3DSettingsServer.SetFieldOfView(90.0f);
        Camera3DSettingsServer.SetKeepAspect(Camera3D.KeepAspectEnum.Height);
        var player = AuthoredCamera();
        player.AddToGroup(Camera3DSettingsServer.GetCameraGroup());
        AddChild(player);
        Expect(Camera3DSettingsServer.HasCamera(player), "a camera in the group is managed when it enters the tree");
        Expect(Approx(player.Fov, 90.0), "a camera in the group gets the FOV");
        ExpectEqual(player.KeepAspect, Camera3D.KeepAspectEnum.Height, "a camera in the group gets keep_aspect");
        Camera3DSettingsServer.SetFieldOfView(100.0f);
        Expect(Approx(player.Fov, 100.0), "a camera in the group gets later changes at once");
        Camera3DSettingsServer.SetNear(0.5f);
        Camera3DSettingsServer.SetFar(0.1f);
        Expect(Approx(player.Near, 0.5), "near reaches the camera");
        Expect(Approx(player.Far, 0.51), "far stays above near");
        Camera3DSettingsServer.SetFar(1000.0f);
        Expect(Approx(player.Far, 1000.0), "far reaches the camera");

        // A cutscene camera outside the group: untouched, also while it is current.
        var cutscene = AuthoredCamera();
        AddChild(cutscene);
        cutscene.MakeCurrent();
        Camera3DSettingsServer.SetFieldOfView(70.0f);
        Expect(!Camera3DSettingsServer.HasCamera(cutscene), "a camera outside the group is not managed");
        Expect(IsAuthored(cutscene), "a camera outside the group keeps its authored values");

        // Off: authored values back; on: the settings again.
        Camera3DSettingsServer.SetEnabled(false);
        Expect(IsAuthored(player), "enabled = false gives the authored values back");
        Camera3DSettingsServer.SetEnabled(true);
        Expect(Approx(player.Fov, 70.0), "enabled = true applies the settings again");

        // Joined the group while in the tree.
        cutscene.AddToGroup(Camera3DSettingsServer.GetCameraGroup());
        Camera3DSettingsServer.ApplyToCameras();
        Expect(Approx(cutscene.Fov, 70.0), "ApplyToCameras() picks up cameras that joined the group in the tree");
        Camera3DSettingsServer.RemoveCamera(cutscene);
        Expect(IsAuthored(cutscene), "RemoveCamera() gives the authored values back");

        player.QueueFree();
        cutscene.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Camera3DSettingsServer.Singleton.ResetToDefaults();
    }

    private void CheckAddedCamera()
    {
        var camera = AuthoredCamera();
        Camera3DSettingsServer.AddCamera(camera);
        Expect(Camera3DSettingsServer.HasCamera(camera), "AddCamera()");
        Expect(Camera3DSettingsServer.GetManagedCameras().Contains(camera), "GetManagedCameras() has the added camera");
        Camera3DSettingsServer.SetFieldOfView(85.0f);
        Expect(Approx(camera.Fov, 85.0), "an added camera gets later changes");
        Camera3DSettingsServer.RemoveCamera(camera);
        Expect(!Camera3DSettingsServer.HasCamera(camera), "RemoveCamera()");
        Expect(IsAuthored(camera), "a removed camera has its authored values");
        Camera3DSettingsServer.SetFieldOfView(95.0f);
        Expect(Approx(camera.Fov, 60.0), "a removed camera is not changed any more");
        camera.Free();
        Camera3DSettingsServer.Singleton.ResetToDefaults();
    }

    private void CheckGameSide()
    {
        var server = Camera3DSettingsServer.Singleton;
        var changed = new List<string>();
        var callable = Callable.From((StringName name, Variant value) => changed.Add(name));
        server.Connect(SettingsServer.SignalName.ValueChanged, callable);
        Camera3DSettingsServer.SetShakeIntensity(0.0f);
        Camera3DSettingsServer.SetHeadBobIntensity(0.5f);
        Camera3DSettingsServer.SetMotionBlurIntensity(0.25f);
        server.Disconnect(SettingsServer.SignalName.ValueChanged, callable);
        Expect(Approx(Camera3DSettingsServer.GetShakeIntensity(), 0.0), "shake intensity is stored");
        Expect(Approx(Camera3DSettingsServer.GetHeadBobIntensity(), 0.5), "head bob intensity is stored");
        Expect(Approx(Camera3DSettingsServer.GetMotionBlurIntensity(), 0.25), "motion blur intensity is stored");
        Expect(changed.Contains("shake_intensity"), "value_changed is emitted for game-side values");
        server.ResetToDefaults();
    }

    private void CheckValidation()
    {
        var server = Camera3DSettingsServer.Singleton;
        Camera3DSettingsServer.SetFieldOfView(200.0f);
        Expect(Approx(Camera3DSettingsServer.GetFieldOfView(), 179.0), "the FOV is clamped to 179");
        Camera3DSettingsServer.SetNear(0.0f);
        Expect(Approx(Camera3DSettingsServer.GetNear(), 0.001), "near is clamped to 0.001");
        Camera3DSettingsServer.SetShakeIntensity(3.0f);
        Expect(Approx(Camera3DSettingsServer.GetShakeIntensity(), 1.0), "the shake intensity is clamped to 1");
        server.SetValue("keep_aspect", 7);
        ExpectEqual(server.GetValue("keep_aspect").AsInt32(), server.GetDefaultValue("keep_aspect").AsInt32(), "an invalid keep_aspect falls back");
        server.ResetToDefaults();
    }

    private async Task CheckStorageAndSceneChange()
    {
        var server = Camera3DSettingsServer.Singleton;
        // FOV changed and saved; a "restart" loads it again.
        Camera3DSettingsServer.SetFieldOfView(105.0f);
        Camera3DSettingsServer.SetKeepAspect(Camera3D.KeepAspectEnum.Width);
        ExpectEqual(server.Save(), Error.Ok, "Save()");
        Expect(FileAccess.FileExists(CameraFile), "the file exists");
        Expect(FileAccess.GetFileAsString(CameraFile).Contains("field_of_view"), "the INI file has field_of_view");
        server.ResetToDefaults();
        ExpectEqual(server.Load(), Error.Ok, "Load()");
        Expect(Approx(Camera3DSettingsServer.GetFieldOfView(), 105.0), "the FOV is loaded");
        server.Apply();

        // A scene change: the player camera is saved in the group in its scene.
        var level = new Node3D();
        var camera = AuthoredCamera();
        camera.Name = "PlayerCamera";
        camera.AddToGroup(Camera3DSettingsServer.GetCameraGroup(), true);
        level.AddChild(camera);
        camera.Owner = level;
        var packed = new PackedScene();
        ExpectEqual(packed.Pack(level), Error.Ok, "pack the level");
        level.Free();
        var instance = packed.Instantiate();
        AddChild(instance);
        var loaded = instance.GetNode<Camera3D>("PlayerCamera");
        Expect(Approx(loaded.Fov, 105.0), "the camera of a new scene gets the loaded FOV");
        ExpectEqual(loaded.KeepAspect, Camera3D.KeepAspectEnum.Width, "the camera of a new scene gets keep_aspect");
        instance.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>Issue #173: the typed signal fires once per applied change, not for pending changes in manual mode.</summary>
    private async Task CheckTypedSignals()
    {
        if (!Engine.HasSingleton("Camera3DSettingsServer"))
        {
            return;
        }
        var s = (SettingsServer)Engine.GetSingleton("Camera3DSettingsServer");
        Expect(s.HasSignal("field_of_view_changed"), "Camera3DSettingsServer has the typed signal field_of_view_changed");
        string filePath = s.GetFilePath();
        bool hadFile = FileAccess.FileExists(filePath);
        var previousMode = s.ApplyMode;
        s.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        s.ResetValue("field_of_view");
        double newValue = s.GetAppliedValue("field_of_view").AsDouble() != 90.0 ? 90.0 : 60.0;
        var received = new System.Collections.Generic.List<Variant>();
        var onChanged = Callable.From((Variant v) => received.Add(v));
        s.Connect("field_of_view_changed", onChanged);

        s.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        s.SetValue("field_of_view", newValue);
        ExpectEqual(received.Count, 0, "field_of_view_changed does not fire for a pending change in manual mode");
        s.Apply();
        ExpectEqual(received.Count, 1, "field_of_view_changed fires once on Apply()");
        if (received.Count > 0)
        {
            ExpectEqual(received[0].VariantType, Variant.Type.Float, "field_of_view_changed passes a typed value");
            ExpectEqual((float)received[0].AsDouble(), (float)newValue, "field_of_view_changed passes the applied value");
        }
        s.Apply();
        ExpectEqual(received.Count, 1, "field_of_view_changed does not fire when nothing changed");
        s.ResetValue("field_of_view");
        ExpectEqual(received.Count, 1, "field_of_view_changed waits for Apply() after a reset in manual mode");
        s.Apply();
        ExpectEqual(received.Count, 2, "field_of_view_changed fires again when the default is applied");

        s.Disconnect("field_of_view_changed", onChanged);
        s.ApplyMode = previousMode;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!hadFile)
        {
            DirAccess.RemoveAbsolute(filePath);
        }
    }
}
