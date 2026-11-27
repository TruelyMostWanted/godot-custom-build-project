using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Camera3dSettings;

/// <summary>
/// Full API test of Camera 3D Settings (Camera3DSettingsServer, Camera3DSettings). Same tests as scripts/gd/Test.gd.
/// The camera values go to the Camera3D nodes in the group "camera_3d_settings" and to added cameras, at once.
/// Cameras outside the group keep their authored values. The motion values are game-side (never pushed).
/// </summary>
public partial class Test : FeatureTest
{
    private const string CameraFile = "user://settings/camera_3d.ini";
    private const float AuthoredFov = 60.0f;
    private const float AuthoredNear = 0.2f;
    private const float AuthoredFar = 500.0f;

    private readonly Godot.Collections.Dictionary _received = [];
    private readonly List<string> _changedSettings = [];

    protected override async Task RunTests()
    {
        Camera3DSettingsServer.Singleton.ResetToDefaults();
        TestDefaults();
        TestValues();
        await TestGroup();
        TestAddedCamera();
        TestGameSide();
        TestValidation();
        TestSignals();
        await TestStorageAndSceneChange();
        TestResource();
        Camera3DSettingsServer.Singleton.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(CameraFile);
    }

    private static Camera3D AuthoredCamera()
    {
        var camera = new Camera3D();
        camera.Fov = AuthoredFov;
        camera.KeepAspect = Camera3D.KeepAspectEnum.Width;
        camera.Near = AuthoredNear;
        camera.Far = AuthoredFar;
        return camera;
    }

    private static bool IsAuthored(Camera3D camera)
    {
        bool fov = Mathf.IsEqualApprox(camera.Fov, AuthoredFov);
        bool keep = camera.KeepAspect == Camera3D.KeepAspectEnum.Width;
        bool near = Mathf.IsEqualApprox(camera.Near, AuthoredNear);
        bool far = Mathf.IsEqualApprox(camera.Far, AuthoredFar);
        return fov && keep && near && far;
    }

    private void TestDefaults()
    {
        float projectFov = (float)ProjectSettings.GetSetting("settings/camera_3d/field_of_view");
        int projectKeep = (int)ProjectSettings.GetSetting("settings/camera_3d/keep_aspect");
        float projectNear = (float)ProjectSettings.GetSetting("settings/camera_3d/near");
        float projectFar = (float)ProjectSettings.GetSetting("settings/camera_3d/far");
        ExpectEqual(Camera3DSettingsServer.GetFieldOfView(), projectFov, "the default FOV is the project setting");
        ExpectEqual((int)Camera3DSettingsServer.GetKeepAspect(), projectKeep, "the default keep_aspect is the project setting");
        ExpectEqual(Camera3DSettingsServer.GetNear(), projectNear, "the default near is the project setting");
        ExpectEqual(Camera3DSettingsServer.GetFar(), projectFar, "the default far is the project setting");
        Expect(Camera3DSettingsServer.IsEnabled(), "the settings are enabled by default");
        ExpectEqual(Camera3DSettingsServer.GetShakeIntensity(), 1.0f, "the default shake intensity is 1");
        ExpectEqual(Camera3DSettingsServer.GetCameraGroup(), new StringName("camera_3d_settings"), "GetCameraGroup returns the group name");
    }

    private void TestValues()
    {
        Camera3DSettingsServer.SetEnabled(false);
        Expect(!Camera3DSettingsServer.IsEnabled(), "enabled round trip");
        Camera3DSettingsServer.SetFieldOfView(90.0f);
        ExpectEqual(Camera3DSettingsServer.GetFieldOfView(), 90.0f, "field_of_view round trip");
        Camera3DSettingsServer.SetKeepAspect(Camera3D.KeepAspectEnum.Height);
        ExpectEqual(Camera3DSettingsServer.GetKeepAspect(), Camera3D.KeepAspectEnum.Height, "keep_aspect round trip");
        Camera3DSettingsServer.SetNear(0.5f);
        ExpectEqual(Camera3DSettingsServer.GetNear(), 0.5f, "near round trip");
        Camera3DSettingsServer.SetFar(2000.0f);
        ExpectEqual(Camera3DSettingsServer.GetFar(), 2000.0f, "far round trip");
        Camera3DSettingsServer.SetShakeIntensity(0.5f);
        ExpectEqual(Camera3DSettingsServer.GetShakeIntensity(), 0.5f, "shake_intensity round trip");
        Camera3DSettingsServer.SetHeadBobIntensity(0.25f);
        ExpectEqual(Camera3DSettingsServer.GetHeadBobIntensity(), 0.25f, "head_bob_intensity round trip");
        Camera3DSettingsServer.SetMotionBlurIntensity(0.75f);
        ExpectEqual(Camera3DSettingsServer.GetMotionBlurIntensity(), 0.75f, "motion_blur_intensity round trip");
        Camera3DSettingsServer.Singleton.ResetToDefaults();
    }

    private async Task TestGroup()
    {
        StringName group = Camera3DSettingsServer.GetCameraGroup();
        Camera3DSettingsServer.SetFieldOfView(90.0f);
        Camera3DSettingsServer.SetKeepAspect(Camera3D.KeepAspectEnum.Height);
        Camera3D player = AuthoredCamera();
        player.AddToGroup(group);
        AddChild(player);
        Expect(Camera3DSettingsServer.HasCamera(player), "a camera in the group is managed when it enters the tree");
        ExpectEqual(player.Fov, 90.0f, "a camera in the group gets the FOV");
        ExpectEqual(player.KeepAspect, Camera3D.KeepAspectEnum.Height, "a camera in the group gets keep_aspect");
        Camera3DSettingsServer.SetFieldOfView(100.0f);
        ExpectEqual(player.Fov, 100.0f, "a camera in the group gets later changes at once");
        Camera3DSettingsServer.SetNear(0.5f);
        Camera3DSettingsServer.SetFar(0.1f);
        ExpectEqual(player.Near, 0.5f, "near reaches the camera");
        Expect(Mathf.IsEqualApprox(player.Far, 0.51f), "far stays above near");
        Camera3DSettingsServer.SetFar(1000.0f);
        ExpectEqual(player.Far, 1000.0f, "far reaches the camera");
        Camera3D cutscene = AuthoredCamera();
        AddChild(cutscene);
        cutscene.MakeCurrent();
        Camera3DSettingsServer.SetFieldOfView(70.0f);
        Expect(!Camera3DSettingsServer.HasCamera(cutscene), "a camera outside the group is not managed");
        Expect(IsAuthored(cutscene), "a camera outside the group keeps its authored values, also while it is current");
        Camera3DSettingsServer.SetEnabled(false);
        Expect(IsAuthored(player), "enabled = false gives the authored values back");
        Camera3DSettingsServer.SetEnabled(true);
        ExpectEqual(player.Fov, 70.0f, "enabled = true applies the settings again");
        cutscene.AddToGroup(group);
        Camera3DSettingsServer.ApplyToCameras();
        ExpectEqual(cutscene.Fov, 70.0f, "ApplyToCameras picks up cameras that joined the group in the tree");
        Camera3DSettingsServer.RemoveCamera(cutscene);
        Expect(IsAuthored(cutscene), "RemoveCamera gives the authored values back");
        player.QueueFree();
        cutscene.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Camera3DSettingsServer.Singleton.ResetToDefaults();
    }

    private void TestAddedCamera()
    {
        Camera3D camera = AuthoredCamera();
        Camera3DSettingsServer.AddCamera(camera);
        Expect(Camera3DSettingsServer.HasCamera(camera), "AddCamera: HasCamera is true");
        Expect(Camera3DSettingsServer.GetManagedCameras().Contains(camera), "GetManagedCameras has the added camera");
        Camera3DSettingsServer.SetFieldOfView(85.0f);
        ExpectEqual(camera.Fov, 85.0f, "an added camera gets later changes");
        Camera3DSettingsServer.RemoveCamera(camera);
        Expect(!Camera3DSettingsServer.HasCamera(camera), "RemoveCamera: HasCamera is false");
        Expect(IsAuthored(camera), "a removed camera has its authored values");
        Camera3DSettingsServer.SetFieldOfView(95.0f);
        ExpectEqual(camera.Fov, AuthoredFov, "a removed camera is not changed any more");
        camera.Free();
        Camera3DSettingsServer.Singleton.ResetToDefaults();
    }

    private void TestGameSide()
    {
        Camera3DSettingsServerInstance server = Camera3DSettingsServer.Singleton;
        server.ValueChanged += OnValueChanged;
        Camera3DSettingsServer.SetShakeIntensity(0.0f);
        server.ValueChanged -= OnValueChanged;
        Expect(_changedSettings.Contains("shake_intensity"), "ValueChanged is emitted for game-side values");
        SettingsServer.SettingFlags flags = server.GetSettingFlags("shake_intensity");
        Expect(flags.HasFlag(SettingsServer.SettingFlags.NoEngineTarget), "shake_intensity is game-side");
        server.ResetToDefaults();
    }

    private void OnValueChanged(StringName setting, Variant value)
    {
        _changedSettings.Add(setting);
    }

    private void TestValidation()
    {
        Camera3DSettingsServer.SetFieldOfView(200.0f);
        ExpectEqual(Camera3DSettingsServer.GetFieldOfView(), 179.0f, "the FOV is clamped to 179");
        Camera3DSettingsServer.SetNear(0.0f);
        Expect(Mathf.IsEqualApprox(Camera3DSettingsServer.GetNear(), 0.001f), "near is clamped to 0.001");
        Camera3DSettingsServer.SetShakeIntensity(3.0f);
        ExpectEqual(Camera3DSettingsServer.GetShakeIntensity(), 1.0f, "the shake intensity is clamped to 1");
        Camera3DSettingsServer.Singleton.ResetToDefaults();
    }

    private void TestSignals()
    {
        Camera3DSettingsServerInstance server = Camera3DSettingsServer.Singleton;
        server.FieldOfViewChanged += fov => _received["fov"] = fov;
        server.KeepAspectChanged += keepAspect => _received["keep_aspect"] = (int)keepAspect;
        server.NearChanged += near => _received["near"] = near;
        server.FarChanged += far => _received["far"] = far;
        server.ShakeIntensityChanged += intensity => _received["shake"] = intensity;
        server.HeadBobIntensityChanged += intensity => _received["head_bob"] = intensity;
        server.MotionBlurIntensityChanged += intensity => _received["motion_blur"] = intensity;
        Camera3DSettingsServer.SetFieldOfView(90.0f);
        Camera3DSettingsServer.SetKeepAspect(Camera3D.KeepAspectEnum.Width);
        Camera3DSettingsServer.SetNear(0.5f);
        Camera3DSettingsServer.SetFar(2000.0f);
        Camera3DSettingsServer.SetShakeIntensity(0.5f);
        Camera3DSettingsServer.SetHeadBobIntensity(0.25f);
        Camera3DSettingsServer.SetMotionBlurIntensity(0.75f);
        ExpectEqual((float)_received["fov"], 90.0f, "FieldOfViewChanged passes the FOV");
        ExpectEqual((int)_received["keep_aspect"], (int)Camera3D.KeepAspectEnum.Width, "KeepAspectChanged passes the mode");
        ExpectEqual((float)_received["near"], 0.5f, "NearChanged passes the distance");
        ExpectEqual((float)_received["far"], 2000.0f, "FarChanged passes the distance");
        ExpectEqual((float)_received["shake"], 0.5f, "ShakeIntensityChanged passes the intensity");
        ExpectEqual((float)_received["head_bob"], 0.25f, "HeadBobIntensityChanged passes the intensity");
        ExpectEqual((float)_received["motion_blur"], 0.75f, "MotionBlurIntensityChanged passes the intensity");
        server.ResetToDefaults();
    }

    private async Task TestStorageAndSceneChange()
    {
        Camera3DSettingsServerInstance server = Camera3DSettingsServer.Singleton;
        Camera3DSettingsServer.SetFieldOfView(105.0f);
        Camera3DSettingsServer.SetKeepAspect(Camera3D.KeepAspectEnum.Width);
        ExpectEqual(server.Save(), Error.Ok, "Save writes the file");
        server.ResetToDefaults();
        ExpectEqual(server.Load(), Error.Ok, "Load reads the file");
        ExpectEqual(Camera3DSettingsServer.GetFieldOfView(), 105.0f, "the FOV is loaded");
        server.Apply();
        var level = new Node3D();
        Camera3D camera = AuthoredCamera();
        camera.Name = "PlayerCamera";
        camera.AddToGroup(Camera3DSettingsServer.GetCameraGroup(), true);
        level.AddChild(camera);
        camera.Owner = level;
        var packed = new PackedScene();
        ExpectEqual(packed.Pack(level), Error.Ok, "pack the level");
        level.Free();
        Node instance = packed.Instantiate();
        AddChild(instance);
        var loaded = instance.GetNode<Camera3D>("PlayerCamera");
        ExpectEqual(loaded.Fov, 105.0f, "the camera of a new scene gets the loaded FOV");
        ExpectEqual(loaded.KeepAspect, Camera3D.KeepAspectEnum.Width, "the camera of a new scene gets keep_aspect");
        instance.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void TestResource()
    {
        var settings = new Camera3DSettings();
        Expect(settings.Enabled, "Camera3DSettings: enabled by default");
        ExpectEqual(settings.FieldOfView, 75.0f, "Camera3DSettings: default FieldOfView");
        ExpectEqual(settings.KeepAspect, Camera3D.KeepAspectEnum.Height, "Camera3DSettings: default KeepAspect");
        Expect(Mathf.IsEqualApprox(settings.Near, 0.05f), "Camera3DSettings: default Near");
        ExpectEqual(settings.Far, 4000.0f, "Camera3DSettings: default Far");
        ExpectEqual(settings.ShakeIntensity, 1.0f, "Camera3DSettings: default ShakeIntensity");
        ExpectEqual(settings.HeadBobIntensity, 1.0f, "Camera3DSettings: default HeadBobIntensity");
        ExpectEqual(settings.MotionBlurIntensity, 1.0f, "Camera3DSettings: default MotionBlurIntensity");
        settings.FieldOfView = 90.0f;
        ExpectEqual(settings.FieldOfView, 90.0f, "Camera3DSettings: FieldOfView is a plain property");
    }
}
