using Godot;

namespace TruelyInteractive.Issue09FeatureResourceDatabase;

/// <summary>Resource class from C# ([GlobalClass]) for the type filter of 09-feature-resource-database.</summary>
[GlobalClass]
public partial class Issue09CsItem : Resource
{
    [Export] public int Value { get; set; } = 1;
}
