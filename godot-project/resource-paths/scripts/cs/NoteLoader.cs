using Godot;

namespace TruelyInteractive.ResourcePaths;

/// <summary>Loads a .note file as a Resource whose name is the text of the file.
/// LastCacheMode records the cache mode that ResourceLoader passed to _Load().</summary>
public partial class NoteLoader : ResourceFormatLoader
{
    public ResourceFormatLoader.CacheMode LastCacheMode { get; private set; }

    public override string[] _GetRecognizedExtensions() => ["note"];

    public override bool _HandlesType(StringName type) => type == "Resource";

    public override string _GetResourceType(string path)
    {
        if (path.GetExtension() == "note")
        {
            return "Resource";
        }
        return "";
    }

    public override Variant _Load(string path, string originalPath, bool useSubThreads, int cacheMode)
    {
        LastCacheMode = (ResourceFormatLoader.CacheMode)cacheMode;
        string text = FileAccess.GetFileAsString(path);
        if (text.Length == 0)
        {
            return (long)FileAccess.GetOpenError();
        }
        return new Resource { ResourceName = text };
    }
}
