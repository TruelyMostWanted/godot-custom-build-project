using Godot;

namespace TruelyInteractive.CachedResourcePaths;

/// <summary>Resource class with [GlobalClass], for the type filter of the cached paths. Named WidgetCs because Widget is the GDScript class.</summary>
[GlobalClass]
public partial class WidgetCs : Resource
{
    [Export] public int Value { get; set; } = 1;
}
