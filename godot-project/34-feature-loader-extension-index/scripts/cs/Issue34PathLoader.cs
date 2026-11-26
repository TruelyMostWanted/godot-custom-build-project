using Godot;

namespace TruelyInteractive.Issue34FeatureLoaderExtensionIndex;

/// <summary>Path-based loader for 34-feature-loader-extension-index: overrides _RecognizePath() and declares no extension.
/// It recognizes every path that ends with PathEnding.</summary>
public partial class Issue34PathLoader : ResourceFormatLoader
{
    public string Tag { get; set; } = "";
    public string PathEnding { get; set; } = "";
    public string TypeName { get; set; } = "Resource";

    public override string[] _GetRecognizedExtensions() => [];

    public override bool _RecognizePath(string path, StringName type) => path.EndsWith(PathEnding);

    public override bool _HandlesType(StringName type) => type == "Resource";

    public override string _GetResourceType(string path) => path.EndsWith(PathEnding) ? TypeName : "";

    public override Variant _Load(string path, string originalPath, bool useSubThreads, ResourceFormatLoader.CacheMode cacheMode)
    {
        var res = new Resource { ResourceName = Tag };
        return res;
    }
}
