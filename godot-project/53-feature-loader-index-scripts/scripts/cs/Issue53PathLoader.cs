using Godot;

namespace TruelyInteractive.Issue53FeatureLoaderIndexScripts;

/// <summary>Path-based loader for 53-feature-loader-index-scripts: overrides _RecognizePath(), so the engine asks it for every path
/// (the way every script loader was asked before #53). It recognizes every path that ends with PathEnding.</summary>
public partial class Issue53PathLoader : ResourceFormatLoader
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
