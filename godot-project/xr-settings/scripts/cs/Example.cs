using Godot;

namespace TruelyInteractive.XrSettings;

/// <summary>Minimal example of XR Settings: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API: set, pushed to the OpenXR interface at once, or stored until the interface starts.
        XRSettingsServer.SetFoveationLevel(XRSettings.FoveationLevelEnum.Medium);
        XRSettingsServer.SetRenderTargetSizeMultiplier(1.5f);

        // Using the API: get the pending values.
        GD.Print(XRSettingsServer.GetFoveationLevel());
        GD.Print(XRSettingsServer.GetRenderTargetSizeMultiplier());
        // Using the API: without a headset, these values wait for the OpenXR interface.
        GD.Print(XRSettingsServer.GetDeferredSettingNames());

        // Using the API: apply, save and load (methods of the base class go through Singleton).
        XRSettingsServer.Singleton.Apply();
        XRSettingsServer.Singleton.Save();
        XRSettingsServer.Singleton.Load();
        XRSettingsServer.Singleton.Apply();

        // A foveation option: needs a player who picks it, so it is not run here.

        // Leave no player file behind.
        XRSettingsServer.Singleton.ResetToDefaults();
        XRSettingsServer.Singleton.Save();
        GetTree().Quit();
    }
}
