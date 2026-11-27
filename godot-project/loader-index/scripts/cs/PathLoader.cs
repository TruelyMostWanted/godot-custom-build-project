using Godot;

namespace TruelyInteractive.LoaderIndex;

/// <summary>Path-based loader: overrides _RecognizePath(), so the engine asks it for every path.
/// It recognizes every path that ends with PathEnding.</summary>
public partial class PathLoader : ResourceFormatLoader
{
    public string Tag { get; set; } = "";
    public string PathEnding { get; set; } = "";
    public string TypeName { get; set; } = "Resource";

    public override string[] _GetRecognizedExtensions() => [];

    public override bool _RecognizePath(string path, StringName type) => path.EndsWith(PathEnding);

    public override bool _HandlesType(StringName type) => type == "Resource";

    public override string _GetResourceType(string path)
    {
        if (path.EndsWith(PathEnding))
        {
            return TypeName;
        }
        return "";
    }

    public override Variant _Load(string path, string originalPath, bool useSubThreads, int cacheMode)
    {
        return new Resource { ResourceName = Tag };
    }
}
