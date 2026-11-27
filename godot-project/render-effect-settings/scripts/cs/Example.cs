using Godot;

namespace TruelyInteractive.RenderEffectSettings;

/// <summary>Minimal example of Render Effect Settings: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API: set, applied to every environment at once.
        RenderEffectSettingsServer.SetAllowVolumetricFog(false);
        RenderEffectSettingsServer.SetSsaoQuality(RenderingServer.EnvironmentSsaoQuality.High);
        RenderEffectSettingsServer.SetBrightness(1.2f);

        // Using the API: get the pending values.
        GD.Print(RenderEffectSettingsServer.IsVolumetricFogAllowed());
        GD.Print(RenderEffectSettingsServer.GetSsaoQuality());

        // Using the API: apply, save and load (methods of the base class go through Singleton).
        RenderEffectSettingsServer.Singleton.Apply();
        RenderEffectSettingsServer.Singleton.Save();
        RenderEffectSettingsServer.Singleton.Load();
        RenderEffectSettingsServer.Singleton.Apply();

        // An SSAO check box: needs a player who clicks it, so it is not run here.

        // Leave no player file behind.
        RenderEffectSettingsServer.Singleton.ResetToDefaults();
        RenderEffectSettingsServer.Singleton.Save();
        GetTree().Quit();
    }
}
