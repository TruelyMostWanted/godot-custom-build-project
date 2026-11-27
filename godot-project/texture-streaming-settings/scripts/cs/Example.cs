using Godot;

namespace TruelyInteractive.TextureStreamingSettings;

/// <summary>Minimal example of Texture Streaming Settings: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API: builds without texture streaming (the Web export) don't have this server.
        if (!Engine.HasSingleton("TextureStreamingSettingsServer"))
        {
            GetTree().Quit();
            return;
        }
        GodotObject streaming = Engine.GetSingleton("TextureStreamingSettingsServer");

        // Using the API: set, budget and mip levels apply at once, "enabled" at the next start.
        streaming.Call("set_enabled", true);
        streaming.Call("set_memory_budget_enabled", true);
        streaming.Call("set_memory_budget_mb", 1024);
        streaming.Call("set_min_lod", 1);

        // Using the API: get the pending values.
        GD.Print(streaming.Call("get_memory_budget_mb"));
        GD.Print(streaming.Call("get_restart_required_names"));

        // Using the API: apply, save and load.
        streaming.Call("apply");
        streaming.Call("save");
        streaming.Call("load");
        streaming.Call("apply");

        // A texture streaming check box: needs a player who clicks it, so it is not run here.

        // Leave no player file behind.
        streaming.Call("reset_to_defaults");
        streaming.Call("save");
        GetTree().Quit();
    }
}
