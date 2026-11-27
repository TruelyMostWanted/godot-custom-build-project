using Godot;

namespace TruelyInteractive.SettingsServers;

/// <summary>Settings resource of the domain "test_cs": a float with a range hint and an enum.</summary>
[GlobalClass]
public partial class SettingsServersTestSettingsCs : Resource
{
    [Export(PropertyHint.Range, "0,1,0.01")]
    public double Volume { get; set; } = 0.5;

    [Export(PropertyHint.Enum, "Low,Medium,High")]
    public int Quality { get; set; } = 1;

    [Export]
    public string Label { get; set; } = "";

    [Export]
    public bool Hidden { get; set; }
}
