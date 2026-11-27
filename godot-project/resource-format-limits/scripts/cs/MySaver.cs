using Godot;

namespace TruelyInteractive.ResourceFormatLimits;

/// <summary>Test saver: recognizes one extension and counts its calls instead of writing a file.</summary>
public partial class MySaver : ResourceFormatSaver
{
    public string Extension { get; set; } = "";
    public int Saved { get; private set; }

    public override bool _Recognize(Resource resource) => true;

    public override string[] _GetRecognizedExtensions(Resource resource) => [Extension];

    public override bool _RecognizePath(Resource resource, string path) => path.GetExtension() == Extension;

    public override Error _Save(Resource resource, string path, uint flags)
    {
        Saved++;
        return Error.Ok;
    }
}
