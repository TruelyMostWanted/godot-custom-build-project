using Godot;

namespace TruelyInteractive.ViewportRegistry;

/// <summary>Minimal example of Viewport Registry: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Listing viewports and windows.
        SetMsaaEverywhere(Viewport.Msaa.Msaa2X);
        PrintWindows();

        // From an RID or a window ID back to the node.
        Rid rid = GetViewport().GetViewportRid();
        Viewport viewport = ViewportServer.GetViewportByRid(rid);
        if (viewport != null)
        {
            GD.Print(viewport.GetPath());
        }

        foreach (int windowId in DisplayServer.GetWindowList())
        {
            Window window = ViewportServer.GetWindowById(windowId);
            if (window != null)
            {
                GD.Print(windowId, " -> ", window.Title);
            }
        }

        // Reacting to new viewports.
        ViewportServer.ViewportAdded += OnViewportAdded;
        // The viewports that are there already.
        foreach (Viewport existing in ViewportServer.GetViewports())
        {
            OnViewportAdded(existing);
        }

        AddChild(new SubViewport());

        // Graphics settings for every viewport.
        // Split screen: every SubViewport gets the player's anti-aliasing and texture settings.
        RenderQualitySettingsServer.ViewportTarget = SettingsServer.ViewportTargetMode.AllViewports;
        TextureSettingsServer.ViewportTarget = SettingsServer.ViewportTargetMode.AllViewports;
        // Shadows only for the root and the viewports in the group.
        ShadowSettingsServer.ViewportTarget = SettingsServer.ViewportTargetMode.Group;
        GetTree().Quit();
    }

    private static void SetMsaaEverywhere(Viewport.Msaa msaa)
    {
        foreach (Viewport viewport in ViewportServer.GetViewports())
        {
            viewport.Msaa3D = msaa;
        }
    }

    private static void PrintWindows()
    {
        foreach (Window window in ViewportServer.GetWindows())
        {
            GD.Print(window.Title, ": ", window.Size);
        }
    }

    private void OnViewportAdded(Viewport viewport)
    {
        viewport.Msaa3D = Viewport.Msaa.Msaa4X;
        GD.Print("Viewport added: ", viewport);
    }
}
