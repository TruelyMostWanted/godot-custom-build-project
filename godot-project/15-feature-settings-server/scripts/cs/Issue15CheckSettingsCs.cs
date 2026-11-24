using Godot;

namespace TruelyInteractive.Issue15FeatureSettingsServer;

/// <summary>Settings resource of the C# check domain "check15_cs": two values.</summary>
[GlobalClass]
public partial class Issue15CheckSettingsCs : Resource
{
    [Export(PropertyHint.Range, "0,1,0.01")]
    public double Volume { get; set; } = 0.5;

    [Export(PropertyHint.Enum, "Low,Medium,High")]
    public int Quality { get; set; } = 1;
}
