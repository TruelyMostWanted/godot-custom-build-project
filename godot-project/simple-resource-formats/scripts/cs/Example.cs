using Godot;

namespace TruelyInteractive.SimpleResourceFormats;

/// <summary>Minimal example of Simple Resource Formats: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _EnterTree()
    {
        // Load and save a file format with two functions
        ResourceLoader.RegisterExtensionLoader("quiz", Callable.From<string, Variant>(LoadQuiz), "QuizSetCs");
        ResourceSaver.RegisterExtensionSaver("quiz", Callable.From<Resource, string, int>(SaveQuiz), "QuizSetCs");
    }

    public override void _Ready()
    {
        var quiz = GD.Load<QuizSetCs>("res://simple-resource-formats/resources/questions.quiz");
        ResourceSaver.Save(quiz, "user://my_questions.quiz");
        GD.Print("Questions: ", quiz.Questions.Length);

        // Limit a format to one resource type
        // (the guide uses LoadSettings and GameSettings; this example reuses the quiz)
        ResourceLoader.RegisterExtensionLoader("cfg.json", Callable.From<string, Variant>(LoadQuiz), "QuizSetCs", true);
        GD.Print("cfg.json registered: ", ResourceLoader.HasExtensionLoader("cfg.json"));

        // Report a broken file
        // (the guide shows LoadQuiz with a check for an empty file; registered again, it replaces the first one)
        ResourceLoader.RegisterExtensionLoader("quiz", Callable.From<string, Variant>(LoadCheckedQuiz), "QuizSetCs");
        var checkedQuiz = GD.Load<QuizSetCs>("res://simple-resource-formats/resources/questions.quiz");
        GD.Print("Checked questions: ", checkedQuiz.Questions.Length);

        ResourceLoader.UnregisterExtensionLoader("quiz");
        ResourceLoader.UnregisterExtensionLoader("cfg.json");
        ResourceSaver.UnregisterExtensionSaver("quiz");
        GetTree().Quit();
    }

    private static Variant LoadQuiz(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            return (int)FileAccess.GetOpenError();
        }
        return new QuizSetCs { Questions = file.GetAsText().Split('\n', System.StringSplitOptions.RemoveEmptyEntries) };
    }

    private static int SaveQuiz(Resource resource, string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            return (int)FileAccess.GetOpenError();
        }
        file.StoreString(string.Join('\n', ((QuizSetCs)resource).Questions));
        return (int)Error.Ok;
    }

    private static Variant LoadCheckedQuiz(string path)
    {
        string text = FileAccess.GetFileAsString(path);
        if (text.Length == 0)
        {
            GD.PushError($"'{path}' has no questions.");
            return (int)Error.FileCorrupt;
        }
        return new QuizSetCs { Questions = text.Split('\n', System.StringSplitOptions.RemoveEmptyEntries) };
    }
}
