using System;
using Godot;

namespace TruelyInteractive.LoaderIndex;

/// <summary>Loader by extension: overrides _GetRecognizedExtensions() only, so the engine indexes it.
/// It loads an empty Resource named after its tag. TypeName is what GetResourceType() reports.
/// Extensions may change at runtime, then ResourceLoader.RebuildExtensionIndex() is needed.</summary>
public partial class MyLoader : ResourceFormatLoader
{
    public string Tag { get; set; } = "";
    public string[] Extensions { get; set; } = ["mydata"];
    public string TypeName { get; set; } = "Resource";

    public override string[] _GetRecognizedExtensions() => Extensions;

    public override bool _HandlesType(StringName type) => type == "Resource";

    public override string _GetResourceType(string path)
    {
        foreach (string extension in Extensions)
        {
            if (path.EndsWith("." + extension, StringComparison.OrdinalIgnoreCase))
            {
                return TypeName;
            }
        }
        return "";
    }

    public override Variant _Load(string path, string originalPath, bool useSubThreads, int cacheMode)
    {
        return new Resource { ResourceName = Tag };
    }
}
