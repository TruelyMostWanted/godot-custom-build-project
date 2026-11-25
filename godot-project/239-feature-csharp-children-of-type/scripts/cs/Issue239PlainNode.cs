using Godot;

namespace TruelyInteractive.Issue239FeatureCsharpChildrenOfType;

/// <summary>Marker interface for the checks of 239-feature-csharp-children-of-type.</summary>
public interface IIssue239Marker
{
}

/// <summary>A C# node class without [GlobalClass]: find_children() can't find it by name, FindChildren&lt;T&gt;() can.</summary>
public partial class Issue239PlainNode : Node2D, IIssue239Marker
{
}
