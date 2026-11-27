using Godot;

namespace TruelyInteractive.Camera3dSettings;

/// <summary>Minimal example of Camera 3D Settings: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API: set, every Camera3D in the group camera_3d_settings gets the value at once.
        Camera3DSettingsServer.SetFieldOfView(90.0f);
        // Using the API: game-side values are only stored.
        Camera3DSettingsServer.SetShakeIntensity(0.5f);

        // Using the API: get the pending values.
        GD.Print(Camera3DSettingsServer.GetFieldOfView());
        GD.Print(Camera3DSettingsServer.GetShakeIntensity());

        // Using the API: apply, save and load (methods of the base class go through Singleton).
        Camera3DSettingsServer.Singleton.Apply();
        Camera3DSettingsServer.Singleton.Save();
        Camera3DSettingsServer.Singleton.Load();
        Camera3DSettingsServer.Singleton.Apply();

        // A field of view slider: needs a player who drags it, so it is not run here.

        // Leave no player file behind.
        Camera3DSettingsServer.Singleton.ResetToDefaults();
        Camera3DSettingsServer.Singleton.Save();
        GetTree().Quit();
    }
}
