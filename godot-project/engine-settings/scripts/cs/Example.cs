using Godot;

namespace TruelyInteractive.EngineSettings;

/// <summary>Minimal example of Engine Settings: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API: set, Engine.MaxFps changes at once.
        EngineSettingsServer.SetMaxFps(60);
        EngineSettingsServer.SetPhysicsInterpolationEnabled(true);

        // Using the API: get the pending values.
        GD.Print(EngineSettingsServer.GetMaxFps());
        GD.Print(Engine.MaxFps);

        // Using the API: simulation values are locked unless Settings > Engine > Allow Simulation Values is on.
        GD.Print(EngineSettingsServer.IsSimulationLocked());
        // SetTimeScale(0.75f) is ignored with a warning while locked; not called here, the runner counts the C# warning backtrace as an ERROR.
        GD.Print(EngineSettingsServer.GetTimeScale());

        // Using the API: apply, save and load (methods of the base class go through Singleton).
        EngineSettingsServer.Singleton.Apply();
        EngineSettingsServer.Singleton.Save();
        EngineSettingsServer.Singleton.Load();
        EngineSettingsServer.Singleton.Apply();

        // A frame rate limit option: needs a player who picks it, so it is not run here.

        // Leave no player file behind.
        EngineSettingsServer.Singleton.ResetToDefaults();
        EngineSettingsServer.Singleton.Save();
        GetTree().Quit();
    }
}
