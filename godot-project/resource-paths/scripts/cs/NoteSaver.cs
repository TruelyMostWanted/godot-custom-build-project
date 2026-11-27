using Godot;

namespace TruelyInteractive.ResourcePaths;

/// <summary>Saves the name of a Resource as a .note file.
/// LastFlags records the saver flags that ResourceSaver passed to _Save().</summary>
public partial class NoteSaver : ResourceFormatSaver
{
    public ResourceSaver.SaverFlags LastFlags { get; private set; }

    public override bool _Recognize(Resource resource) => true;

    public override string[] _GetRecognizedExtensions(Resource resource) => ["note"];

    public override Error _Save(Resource resource, string path, uint flags)
    {
        LastFlags = (ResourceSaver.SaverFlags)flags;
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            return FileAccess.GetOpenError();
        }
        file.StoreString(resource.ResourceName);
        return Error.Ok;
    }
}
