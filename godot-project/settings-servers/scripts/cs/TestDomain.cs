using Godot;

namespace TruelyInteractive.SettingsServers;

/// <summary>
/// Domain "test_cs". Listed in the project setting settings/startup/custom_servers, so SettingsServerManager
/// starts it before the main scene. It "applies" its values to Engine metadata.
/// The setting "Label" is trimmed by _ValidateValue(), the setting "Hidden" is reported as unsupported.
/// </summary>
public partial class TestDomain : SettingsServer
{
    public override StringName _GetDomain() => "test_cs";

    public override ApplyPhase _GetApplyPhase() => ApplyPhase.PreDisplay;

    public override Resource _CreateSettings() => new SettingsServersTestSettingsCs();

    public override void _Apply(Resource settings, string[] names)
    {
        var values = (SettingsServersTestSettingsCs)settings;
        Engine.Singleton.SetMeta("test_cs_applied", new Godot.Collections.Dictionary
        {
            { "volume", values.Volume },
            { "quality", values.Quality },
            { "frame", Engine.GetProcessFrames() },
        });
    }

    public override Variant _ValidateValue(StringName name, Variant value)
    {
        if (name == "Label")
        {
            return value.AsString().StripEdges();
        }
        return value;
    }

    public override bool _IsValueSupported(StringName name) => name != "Hidden";

    public override void _ReadFromEngine(Resource settings)
    {
        ((SettingsServersTestSettingsCs)settings).Label = "engine";
    }

    public override int _GetFormatVersion() => 2;

    public override Godot.Collections.Dictionary _Migrate(int fromVersion, Godot.Collections.Dictionary data)
    {
        if (fromVersion < 2 && data.ContainsKey("LegacyVolume"))
        {
            data["Volume"] = data["LegacyVolume"];
            data.Remove("LegacyVolume");
        }
        return data;
    }
}
