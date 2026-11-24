using System;
using System.Linq;
using Godot;

namespace TruelyInteractive.Issue53FeatureLoaderIndexScripts;

/// <summary>Loader by extension for 53-feature-loader-index-scripts: overrides _GetRecognizedExtensions() only, so the engine indexes it.
/// It loads an empty Resource named after its tag; TypeName is what get_resource_type() reports.
/// Extensions may change at runtime; the check then calls ResourceLoader.RebuildExtensionIndex().</summary>
public partial class Issue53ExtLoader : ResourceFormatLoader
{
    public string Tag { get; set; } = "";
    public string[] Extensions { get; set; } = [];
    public string TypeName { get; set; } = "Resource";

    public override string[] _GetRecognizedExtensions() => Extensions;

    public override bool _HandlesType(StringName type) => type == "Resource";

    public override string _GetResourceType(string path)
        => Extensions.Any(e => path.EndsWith("." + e, StringComparison.OrdinalIgnoreCase)) ? TypeName : "";

    public override Variant _Load(string path, string originalPath, bool useSubThreads, int cacheMode)
    {
        var res = new Resource { ResourceName = Tag };
        return res;
    }
}
