using Godot;

namespace TruelyInteractive.Issue15FeatureSettingsServer;

/// <summary>
/// C# settings domain "check15_cs" of the 15-feature-settings-server checks.
/// Listed in the project setting settings/startup/custom_servers, so SettingsServerManager creates it
/// and applies its file at startup. It "applies" its values to Engine metadata.
/// </summary>
public partial class Issue15CheckDomainCs : SettingsServer
{
    public override StringName _GetDomain() => "check15_cs";

    public override ApplyPhase _GetApplyPhase() => ApplyPhase.Scene;

    public override Resource _CreateSettings() => new Issue15CheckSettingsCs();

    public override void _Apply(Resource settings, string[] names)
    {
        var values = (Issue15CheckSettingsCs)settings;
        // The static class Engine has only its own methods; Object methods go through Engine.Singleton.
        Engine.Singleton.SetMeta("check15_cs_applied", new Godot.Collections.Dictionary
        {
            { "volume", values.Volume },
            { "quality", values.Quality },
            { "frame", Engine.GetProcessFrames() },
        });
    }
}
