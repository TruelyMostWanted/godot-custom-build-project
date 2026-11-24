using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue35FeatureSimpleResourceFormat;

/// <summary>C# checks of 35-feature-simple-resource-format. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue35FeatureSimpleResourceFormatCheck : FeatureCheckCs
{
    private const string Root = "user://35-feature-simple-resource-format/cs/";
    private const string Ext = "csquiz";
    private const string QuizType = "Issue35CsQuizSet";
    private static readonly string[] QuizQuestions = { "Which engine is this?", "What is 6 * 7?", "Is the Web single-threaded?" };

    protected override string CheckName => "35-feature-simple-resource-format";

    protected override async Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        CheckSaveAndLoad();
        await CheckThreadedLoad();
        CheckTypeRules();
        CheckErrors();
        CheckUnregister();
        Cleanup();
    }

    private static Variant LoadQuiz(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            return (int)FileAccess.GetOpenError();
        }
        var quiz = new Issue35CsQuizSet();
        quiz.Questions = file.GetAsText().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return quiz;
    }

    private static int SaveQuiz(Resource resource, string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            return (int)FileAccess.GetOpenError();
        }
        file.StoreString(string.Join('\n', ((Issue35CsQuizSet)resource).Questions));
        return (int)Error.Ok;
    }

    private static Variant LoadCorrupt(string path) => (int)Error.FileCorrupt;

    private static Issue35CsQuizSet MakeQuiz() => new() { Questions = QuizQuestions.ToArray() };

    private void CheckSaveAndLoad()
    {
        ExpectEqual(ResourceLoader.RegisterExtension(Ext, Callable.From<string, Variant>(LoadQuiz), QuizType), Error.Ok, "register a load function with one call");
        ExpectEqual(ResourceSaver.RegisterExtension(Ext, Callable.From<Resource, string, int>(SaveQuiz), QuizType), Error.Ok, "register a save function with one call");
        Expect(ResourceLoader.IsExtensionRegistered(Ext) && ResourceSaver.IsExtensionRegistered("." + Ext.ToUpperInvariant()), "IsExtensionRegistered() ignores the dot and the case");

        string path = Root + "questions." + Ext;
        ExpectEqual(ResourceSaver.Save(MakeQuiz(), path), Error.Ok, "ResourceSaver.Save() writes the file through the save function");
        ExpectEqual(FileAccess.GetFileAsString(path), string.Join('\n', QuizQuestions), "the file holds one question per line");
        Expect(ResourceLoader.Exists(path), "ResourceLoader.Exists() finds the file");
        ExpectEqual(ResourceLoader.GetResourceType(path), "Resource", "GetResourceType() reports the native base of the script class");

        var loaded = GD.Load<Resource>(path);
        Expect(loaded is Issue35CsQuizSet, "GD.Load() returns an Issue35CsQuizSet");
        if (loaded is Issue35CsQuizSet quiz)
        {
            Expect(quiz.Questions.SequenceEqual(QuizQuestions), "the loaded questions match");
        }
        Expect(ResourceLoader.HasCached(path), "the loaded resource is cached like any other resource");
        var hinted = ResourceLoader.Load(path, QuizType, ResourceLoader.CacheMode.Ignore);
        Expect(hinted is Issue35CsQuizSet && hinted != loaded, "Load with the type hint and CacheMode.Ignore returns a new resource");
    }

    private async Task CheckThreadedLoad()
    {
        string path = Root + "threaded." + Ext;
        ResourceSaver.Save(MakeQuiz(), path);
        ExpectEqual(ResourceLoader.LoadThreadedRequest(path), Error.Ok, "LoadThreadedRequest() accepts the file");
        var status = ResourceLoader.LoadThreadedGetStatus(path);
        for (int frames = 0; status == ResourceLoader.ThreadLoadStatus.InProgress && frames < 600; frames++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            status = ResourceLoader.LoadThreadedGetStatus(path);
        }
        ExpectEqual(status, ResourceLoader.ThreadLoadStatus.Loaded, "the threaded load finishes");
        var loaded = ResourceLoader.LoadThreadedGet(path);
        Expect(loaded is Issue35CsQuizSet quiz && quiz.Questions.Length == QuizQuestions.Length, "the threaded load calls the load function");
    }

    private void CheckTypeRules()
    {
        Expect(ResourceLoader.GetRecognizedExtensionsForType(QuizType).Contains(Ext), "GetRecognizedExtensionsForType() lists the extension for the script class");
        Expect(!ResourceLoader.GetRecognizedExtensionsForType("Texture2D").Contains(Ext), "the extension is not offered for unrelated types");
        Expect(ResourceSaver.GetRecognizedExtensions(MakeQuiz()).Contains(Ext), "ResourceSaver offers the extension for the script class");

        ExpectEqual(ResourceSaver.Save(new Resource(), Root + "plain." + Ext), Error.FileUnrecognized, "resources of another type are not saved with the format");

        ExpectEqual(ResourceLoader.RegisterExtension("csquizbad", Callable.From<string, Variant>(LoadQuiz), "Node"), Error.InvalidParameter, "a type that is not a Resource is rejected");
        ExpectEqual(ResourceLoader.RegisterExtension("", Callable.From<string, Variant>(LoadQuiz)), Error.InvalidParameter, "an empty extension is rejected");
        ExpectEqual(ResourceSaver.RegisterExtension("csquizbad", new Callable()), Error.InvalidParameter, "an invalid callable is rejected");
        Expect(!ResourceLoader.IsExtensionRegistered("csquizbad") && !ResourceSaver.IsExtensionRegistered("csquizbad"), "rejected registrations leave nothing behind");
    }

    private void CheckErrors()
    {
        string path = Root + "broken.csquizerr";
        using (var file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
        {
            file.StoreString("x");
        }
        ExpectEqual(ResourceLoader.RegisterExtension("csquizerr", Callable.From<string, Variant>(LoadCorrupt)), Error.Ok, "register a load function that fails");
        Expect(ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) == null, "an Error returned by the load function makes Load() fail (one error message expected)");

        // Registering again replaces the function.
        ExpectEqual(ResourceLoader.RegisterExtension("csquizerr", Callable.From<string, Variant>(p => LoadQuiz(p))), Error.Ok, "register the extension again with a lambda");
        var loaded = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        Expect(loaded is Issue35CsQuizSet quiz && quiz.Questions.SequenceEqual(new[] { "x" }), "the second registration replaces the first");
        ExpectEqual(ResourceLoader.UnregisterExtension("csquizerr"), Error.Ok, "unregister the replaced extension");
    }

    private void CheckUnregister()
    {
        string path = Root + "questions." + Ext;
        ExpectEqual(ResourceLoader.UnregisterExtension(Ext), Error.Ok, "ResourceLoader.UnregisterExtension() removes the format");
        ExpectEqual(ResourceSaver.UnregisterExtension(Ext), Error.Ok, "ResourceSaver.UnregisterExtension() removes the format");
        Expect(!ResourceLoader.IsExtensionRegistered(Ext) && !ResourceSaver.IsExtensionRegistered(Ext), "IsExtensionRegistered() is false afterwards");
        ExpectEqual(ResourceLoader.UnregisterExtension(Ext), Error.DoesNotExist, "unregistering twice returns Error.DoesNotExist");
        Expect(ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) == null, "loading fails after unregistering (one 'No loader found' error expected)");
        ExpectEqual(ResourceSaver.Save(MakeQuiz(), Root + "after." + Ext), Error.FileUnrecognized, "saving fails after unregistering");
    }

    private static void Cleanup()
    {
        foreach (string file in DirAccess.GetFilesAt(Root))
        {
            DirAccess.RemoveAbsolute(Root + file);
        }
    }
}
