using Godot;

namespace TruelyInteractive.PathPrefixes;

/// <summary>A prefix in C#: <c>levels://a/b.tres</c> is <c>user://levels/a/b.tres</c>.</summary>
public partial class LevelsPrefix : PathPrefix
{
    public override string _Globalize(string path) => "user://levels/" + path.TrimPrefix("levels://");
}
