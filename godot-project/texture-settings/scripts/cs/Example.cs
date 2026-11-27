using Godot;

namespace TruelyInteractive.TextureSettings;

/// <summary>Minimal example of Texture Settings: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API: set, pushed to the root viewport at once.
        TextureSettingsServer.SetAnisotropicFilteringLevel(Viewport.AnisotropicFiltering.Anisotropy16X);
        TextureSettingsServer.SetTextureMipmapBias(-0.5f);
        TextureSettingsServer.SetCanvasItemDefaultTextureFilter(Viewport.DefaultCanvasItemTextureFilter.Nearest);

        // Using the API: get the pending values.
        GD.Print(TextureSettingsServer.GetAnisotropicFilteringLevel());
        GD.Print(TextureSettingsServer.GetTextureMipmapBias());

        // Using the API: apply, save and load (methods of the base class go through Singleton).
        TextureSettingsServer.Singleton.Apply();
        TextureSettingsServer.Singleton.Save();
        TextureSettingsServer.Singleton.Load();
        TextureSettingsServer.Singleton.Apply();

        // An anisotropic filtering button: needs a player who clicks it, so it is not run here.

        // Leave no player file behind.
        TextureSettingsServer.Singleton.ResetToDefaults();
        TextureSettingsServer.Singleton.Save();
        GetTree().Quit();
    }
}
