using Godot;

namespace TruelyInteractive.ResourceFormatLimits;

/// <summary>Test loader: recognizes one extension and returns a new Resource that records which loader created it.</summary>
public partial class MyLoader : ResourceFormatLoader
{
    public string Extension { get; set; } = "";
    public int Id { get; set; }

    public override string[] _GetRecognizedExtensions() => [Extension];

    public override bool _HandlesType(StringName type) => type == "Resource";

    public override string _GetResourceType(string path)
    {
        if (path.GetExtension() == Extension)
        {
            return "Resource";
        }
        return "";
    }

    public override Variant _Load(string path, string originalPath, bool useSubThreads, int cacheMode)
    {
        var resource = new Resource();
        resource.SetMeta("loader_id", Id);
        return resource;
    }
}
