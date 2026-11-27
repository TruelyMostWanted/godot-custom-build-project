using Godot;

namespace TruelyInteractive.ResourceDatabase;

/// <summary>Resource class with [GlobalClass], for the type filter of the database. Named ItemCs because Item is the GDScript class.</summary>
[GlobalClass]
public partial class ItemCs : Resource
{
    [Export] public int Value { get; set; } = 1;
}
