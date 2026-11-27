using Godot;

namespace TruelyInteractive.ShadowSettings;

/// <summary>Minimal example of Shadow Settings: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API: set, pushed to the renderer and the root viewport at once.
        ShadowSettingsServer.SetDirectionalShadowAtlasSize(8192);
        ShadowSettingsServer.SetDirectionalSoftShadowFilterQuality(RenderingServer.ShadowQuality.SoftHigh);
        ShadowSettingsServer.SetPositionalShadowAtlasSize(2048);

        // Using the API: get the pending values.
        GD.Print(ShadowSettingsServer.GetDirectionalShadowAtlasSize());
        GD.Print(ShadowSettingsServer.GetDirectionalSoftShadowFilterQuality());

        // Using the API: apply, save and load (methods of the base class go through Singleton).
        ShadowSettingsServer.Singleton.Apply();
        ShadowSettingsServer.Singleton.Save();
        ShadowSettingsServer.Singleton.Load();
        ShadowSettingsServer.Singleton.Apply();

        // A shadow quality button: needs a player who clicks it, so it is not run here.

        // Leave no player file behind.
        ShadowSettingsServer.Singleton.ResetToDefaults();
        ShadowSettingsServer.Singleton.Save();
        GetTree().Quit();
    }
}
