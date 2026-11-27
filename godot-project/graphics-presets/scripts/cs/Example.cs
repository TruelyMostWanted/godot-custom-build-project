using Godot;

namespace TruelyInteractive.GraphicsPresets;

/// <summary>Minimal example of Graphics Presets: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API: the built-in presets, then the ones from settings/graphics_preset/presets.
        GD.Print(GraphicsPresetSettingsServer.GetPresetNames());

        // Using the API: sets the values of the quality, texture, shadow and effect domains in one call.
        // The wiki applies "Cinematic", a project preset; this project has none, so the built-in "Ultra" is used.
        GraphicsPresetSettingsServer.ApplyPreset("Ultra");
        GD.Print(GraphicsPresetSettingsServer.GetCurrentPreset());

        // Using the API: one change by hand and the values no longer match.
        ShadowSettingsServer.SetDirectionalShadowAtlasSize(1024);
        GD.Print(GraphicsPresetSettingsServer.GetCurrentPreset());

        // Using the API: save, load and apply as usual (methods of the base class go through Singleton).
        GraphicsPresetSettingsServer.Singleton.Save();
        GraphicsPresetSettingsServer.Singleton.Load();
        GraphicsPresetSettingsServer.Singleton.Apply();

        // A preset button: needs a player who clicks it, so it is not run here.

        // Leave no player files behind.
        foreach (string domain in GraphicsPreset.GetDomains())
        {
            SettingsServer server = SettingsServerManager.GetServer(domain);
            server.ResetToDefaults();
            server.Save();
        }
        GraphicsPresetSettingsServer.Singleton.ResetToDefaults();
        GraphicsPresetSettingsServer.Singleton.Save();
        GetTree().Quit();
    }
}
