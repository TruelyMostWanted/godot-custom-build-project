using Godot;

namespace TruelyInteractive.Issue34FeatureLoaderExtensionIndex;

/// <summary>Loader by extension for 34-feature-loader-extension-index. It loads an empty Resource named after its tag.
/// TypeName is what get_resource_type() reports, so the test can see which loader answers first.</summary>
public partial class Issue34ExtLoader : ResourceFormatLoader
{
    public string Tag { get; set; } = "";
    public string Extension { get; set; } = "";
    public string TypeName { get; set; } = "Resource";

    public override string[] _GetRecognizedExtensions() => [Extension];

    public override bool _HandlesType(StringName type) => type == "Resource";

    public override string _GetResourceType(string path) => path.GetExtension().ToLowerInvariant() == Extension ? TypeName : "";

    public override Variant _Load(string path, string originalPath, bool useSubThreads, ResourceFormatLoader.CacheMode cacheMode)
    {
        var res = new Resource { ResourceName = Tag };
        return res;
    }
}
