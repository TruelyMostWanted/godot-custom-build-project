using Godot;

namespace TruelyInteractive.PathPrefixes;

/// <summary>A prefix for tests: test://a is user://pp_test/a. Overrides all four virtual methods.</summary>
public partial class TestPrefix : PathPrefix
{
    public bool Available { get; set; } = true;
    public bool ReadOnly { get; set; }

    public override string _Globalize(string path)
    {
        return ProjectSettings.GlobalizePath("user://pp_test/" + path.TrimPrefix("test://"));
    }

    public override string _Localize(string globalPath)
    {
        string root = ProjectSettings.GlobalizePath("user://pp_test/");
        if (globalPath.StartsWith(root))
        {
            return "test://" + globalPath.TrimPrefix(root);
        }
        return "";
    }

    public override bool _IsAvailable() => Available;

    public override bool _IsReadOnly() => ReadOnly;
}
