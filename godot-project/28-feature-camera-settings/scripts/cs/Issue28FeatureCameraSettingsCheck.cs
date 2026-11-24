using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue28FeatureCameraSettings;

/// <summary>
/// C# checks of 28-feature-camera-settings. Check the same things as scripts/gd/check.gd.
/// Typed methods are called on the generated static class, the methods of the base class through
/// <c>Singleton</c>. The camera values go to the Camera3D nodes in the group "camera_settings" and to added
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
        var server = CameraSettingsServer.Singleton;
        Expect(Engine.HasSingleton("CameraSettingsServer"), "CameraSettingsServer is a registered singleton");
        Expect(server is CameraSettingsServerInstance, "CameraSettingsServer.Singleton is the instance type");
        Expect(Engine.GetSingleton("CameraSettingsServer") is SettingsServer, "CameraSettingsServerInstance derives from SettingsServer");
        Expect(SettingsServerManager.GetServer("camera") == server, "the domain is registered as \"camera\"");
        Expect(server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(server.GetApplyPhase(), SettingsServer.ApplyPhase.Scene, "applied after the SceneTree exists");
        ExpectEqual(server.GetFilePath(), CameraFile, "file path");
        Expect(server.GetSettings() is CameraSettings, "the settings resource is a CameraSettings");
        ExpectEqual(CameraSettingsServer.GetCameraGroup(), (StringName)"camera_settings", "GetCameraGroup()");
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
        var server = CameraSettingsServer.Singleton;
        Expect(Approx(server.GetDefaultValue("field_of_view").AsDouble(), ProjectSettings.GetSetting("settings/camera/field_of_view").AsDouble()), "default FOV is the project setting");
        ExpectEqual(server.GetDefaultValue("keep_aspect").AsInt32(), ProjectSettings.GetSetting("settings/camera/keep_aspect").AsInt32(), "default keep_aspect is the project setting");
        Expect(Approx(server.GetDefaultValue("near").AsDouble(), ProjectSettings.GetSetting("settings/camera/near").AsDouble()), "default near is the project setting");
        Expect(Approx(server.GetDefaultValue("far").AsDouble(), ProjectSettings.GetSetting("settings/camera/far").AsDouble()), "default far is the project setting");
        Expect(CameraSettingsServer.IsEnabled(), "enabled by default");
        Expect(Approx(CameraSettingsServer.GetShakeIntensity(), 1.0), "default shake intensity 1");
    }

    private async Task CheckGroup()
    {
        CameraSettingsServer.SetFieldOfView(90.0f);
        CameraSettingsServer.SetKeepAspect(Camera3D.KeepAspectEnum.Height);
        var player = AuthoredCamera();
        player.AddToGroup(CameraSettingsServer.GetCameraGroup());
        AddChild(player);
        Expect(CameraSettingsServer.HasCamera(player), "a camera in the group is managed when it enters the tree");
        Expect(Approx(player.Fov, 90.0), "a camera in the group gets the FOV");
        ExpectEqual(player.KeepAspect, Camera3D.KeepAspectEnum.Height, "a camera in the group gets keep_aspect");
        CameraSettingsServer.SetFieldOfView(100.0f);
        Expect(Approx(player.Fov, 100.0), "a camera in the group gets later changes at once");
        CameraSettingsServer.SetNear(0.5f);
        CameraSettingsServer.SetFar(0.1f);
        Expect(Approx(player.Near, 0.5), "near reaches the camera");
        Expect(Approx(player.Far, 0.51), "far stays above near");
        CameraSettingsServer.SetFar(1000.0f);
        Expect(Approx(player.Far, 1000.0), "far reaches the camera");

        // A cutscene camera outside the group: untouched, also while it is current.
        var cutscene = AuthoredCamera();
        AddChild(cutscene);
        cutscene.MakeCurrent();
        CameraSettingsServer.SetFieldOfView(70.0f);
        Expect(!CameraSettingsServer.HasCamera(cutscene), "a camera outside the group is not managed");
        Expect(IsAuthored(cutscene), "a camera outside the group keeps its authored values");

        // Off: authored values back; on: the settings again.
        CameraSettingsServer.SetEnabled(false);
        Expect(IsAuthored(player), "enabled = false gives the authored values back");
        CameraSettingsServer.SetEnabled(true);
        Expect(Approx(player.Fov, 70.0), "enabled = true applies the settings again");

        // Joined the group while in the tree.
        cutscene.AddToGroup(CameraSettingsServer.GetCameraGroup());
        CameraSettingsServer.ApplyToCameras();
        Expect(Approx(cutscene.Fov, 70.0), "ApplyToCameras() picks up cameras that joined the group in the tree");
        CameraSettingsServer.RemoveCamera(cutscene);
        Expect(IsAuthored(cutscene), "RemoveCamera() gives the authored values back");

        player.QueueFree();
        cutscene.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        CameraSettingsServer.Singleton.ResetToDefaults();
    }

    private void CheckAddedCamera()
    {
        var camera = AuthoredCamera();
        CameraSettingsServer.AddCamera(camera);
        Expect(CameraSettingsServer.HasCamera(camera), "AddCamera()");
        Expect(CameraSettingsServer.GetManagedCameras().Contains(camera), "GetManagedCameras() has the added camera");
        CameraSettingsServer.SetFieldOfView(85.0f);
        Expect(Approx(camera.Fov, 85.0), "an added camera gets later changes");
        CameraSettingsServer.RemoveCamera(camera);
        Expect(!CameraSettingsServer.HasCamera(camera), "RemoveCamera()");
        Expect(IsAuthored(camera), "a removed camera has its authored values");
        CameraSettingsServer.SetFieldOfView(95.0f);
        Expect(Approx(camera.Fov, 60.0), "a removed camera is not changed any more");
        camera.Free();
        CameraSettingsServer.Singleton.ResetToDefaults();
    }

    private void CheckGameSide()
    {
        var server = CameraSettingsServer.Singleton;
        var changed = new List<string>();
        var callable = Callable.From((StringName name, Variant value) => changed.Add(name));
        server.Connect(SettingsServer.SignalName.ValueChanged, callable);
        CameraSettingsServer.SetShakeIntensity(0.0f);
        CameraSettingsServer.SetHeadBobIntensity(0.5f);
        CameraSettingsServer.SetMotionBlurIntensity(0.25f);
        server.Disconnect(SettingsServer.SignalName.ValueChanged, callable);
        Expect(Approx(CameraSettingsServer.GetShakeIntensity(), 0.0), "shake intensity is stored");
        Expect(Approx(CameraSettingsServer.GetHeadBobIntensity(), 0.5), "head bob intensity is stored");
        Expect(Approx(CameraSettingsServer.GetMotionBlurIntensity(), 0.25), "motion blur intensity is stored");
        Expect(changed.Contains("shake_intensity"), "value_changed is emitted for game-side values");
        server.ResetToDefaults();
    }

    private void CheckValidation()
    {
        var server = CameraSettingsServer.Singleton;
        CameraSettingsServer.SetFieldOfView(200.0f);
        Expect(Approx(CameraSettingsServer.GetFieldOfView(), 179.0), "the FOV is clamped to 179");
        CameraSettingsServer.SetNear(0.0f);
        Expect(Approx(CameraSettingsServer.GetNear(), 0.001), "near is clamped to 0.001");
        CameraSettingsServer.SetShakeIntensity(3.0f);
        Expect(Approx(CameraSettingsServer.GetShakeIntensity(), 1.0), "the shake intensity is clamped to 1");
        server.SetValue("keep_aspect", 7);
        ExpectEqual(server.GetValue("keep_aspect").AsInt32(), server.GetDefaultValue("keep_aspect").AsInt32(), "an invalid keep_aspect falls back");
        server.ResetToDefaults();
    }

    private async Task CheckStorageAndSceneChange()
    {
        var server = CameraSettingsServer.Singleton;
        // FOV changed and saved; a "restart" loads it again.
        CameraSettingsServer.SetFieldOfView(105.0f);
        CameraSettingsServer.SetKeepAspect(Camera3D.KeepAspectEnum.Width);
        ExpectEqual(server.Save(), Error.Ok, "Save()");
        Expect(FileAccess.FileExists(CameraFile), "the file exists");
        Expect(FileAccess.GetFileAsString(CameraFile).Contains("field_of_view"), "the INI file has field_of_view");
        server.ResetToDefaults();
        ExpectEqual(server.Load(), Error.Ok, "Load()");
        Expect(Approx(CameraSettingsServer.GetFieldOfView(), 105.0), "the FOV is loaded");
        server.Apply();

        // A scene change: the player camera is saved in the group in its scene.
        var level = new Node3D();
        var camera = AuthoredCamera();
        camera.Name = "PlayerCamera";
        camera.AddToGroup(CameraSettingsServer.GetCameraGroup(), true);
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
}
