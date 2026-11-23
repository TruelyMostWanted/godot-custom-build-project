using Godot;

namespace TruelyInteractive.Issue35FeatureSimpleResourceFormat;

/// <summary>Resource class from C# ([GlobalClass]) for 35-feature-simple-resource-format: one question per line in a ".csquiz" file.</summary>
[GlobalClass]
public partial class Issue35CsQuizSet : Resource
{
    [Export] public string[] Questions { get; set; } = System.Array.Empty<string>();
}
