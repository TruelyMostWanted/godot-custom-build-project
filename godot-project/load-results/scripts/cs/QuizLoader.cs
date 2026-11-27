using Godot;

namespace TruelyInteractive.LoadResults;

/// <summary>Loader of .lrquiz files: one question per line, stored in the meta "questions" of a plain Resource.
/// It accepts every type hint, so the engine does the type check. An empty file is reported like in the wiki guide.</summary>
public partial class QuizLoader : ResourceFormatLoader
{
    public override string[] _GetRecognizedExtensions() => ["lrquiz"];

    public override bool _HandlesType(StringName type) => true;

    public override string _GetResourceType(string path)
    {
        if (path.GetExtension() == "lrquiz")
        {
            return "Resource";
        }
        return "";
    }

    public override Variant _Load(string path, string originalPath, bool useSubThreads, int cacheMode)
    {
        string text = FileAccess.GetFileAsString(path);
        if (text.Length == 0)
        {
            GD.PushError($"'{path}' is empty.");
            return (int)Error.FileCorrupt;
        }
        var quiz = new Resource();
        quiz.SetMeta("questions", text.Split('\n', System.StringSplitOptions.RemoveEmptyEntries));
        return quiz;
    }
}
