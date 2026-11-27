using Godot;

namespace TruelyInteractive.RenderQualitySettings;

/// <summary>Minimal example of Render Quality Settings: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API: set, pushed to the root viewport at once.
        RenderQualitySettingsServer.SetScaling3DScale(0.75f);
        RenderQualitySettingsServer.SetMsaa3D(Viewport.Msaa.Msaa4X);
        RenderQualitySettingsServer.SetScreenSpaceAA(Viewport.ScreenSpaceAAEnum.Fxaa);

        // Using the API: get the pending values.
        GD.Print(RenderQualitySettingsServer.GetScaling3DScale());
        GD.Print(RenderQualitySettingsServer.GetMsaa3D());

        // Using the API: values the renderer can't use are stored but not applied (methods of the base class go through Singleton).
        GD.Print(RenderQualitySettingsServer.Singleton.IsValueSupported("use_taa"));

        // Using the API: apply, save and load.
        RenderQualitySettingsServer.Singleton.Apply();
        RenderQualitySettingsServer.Singleton.Save();
        RenderQualitySettingsServer.Singleton.Load();
        RenderQualitySettingsServer.Singleton.Apply();

        // A render scale slider: needs a player who drags it, so it is not run here.

        // Leave no player file behind.
        RenderQualitySettingsServer.Singleton.ResetToDefaults();
        RenderQualitySettingsServer.Singleton.Save();
        GetTree().Quit();
    }
}
