using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.SimpleResourceFormats;

/// <summary>Full API test of Simple Resource Formats (RegisterExtensionLoader and RegisterExtensionSaver). Same tests as scripts/gd/Test.gd.
/// Some steps print an engine error on purpose (a failing load function, no loader found).</summary>
public partial class Test : FeatureTest
{
    private const string Root = "user://simple-resource-formats/cs/";
    private const string Extension = "csquiz";
    private const string QuizType = "QuizSetCs";
    private static readonly string[] Questions = ["Which engine is this?", "What is 6 * 7?", "Is the Web single-threaded?"];

    protected override async Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        TestSaveAndLoad();
        await TestThreadedLoad();
        TestTypeRules();
        TestAtFront();
        TestErrors();
        TestUnregister();
        Cleanup();
    }

    private static Variant LoadQuiz(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            return (int)FileAccess.GetOpenError();
        }
        return new QuizSetCs { Questions = file.GetAsText().Split('\n', StringSplitOptions.RemoveEmptyEntries) };
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

    private static Variant LoadCorrupt(string path)
    {
        return (int)Error.FileCorrupt;
    }

    private static QuizSetCs MakeQuiz()
    {
        return new QuizSetCs { Questions = Questions.ToArray() };
    }

    private void TestSaveAndLoad()
    {
        Error loadError = ResourceLoader.RegisterExtensionLoader(Extension, Callable.From<string, Variant>(LoadQuiz), QuizType);
        ExpectEqual(loadError, Error.Ok, "RegisterExtensionLoader registers a load function");
        Error saveError = ResourceSaver.RegisterExtensionSaver(Extension, Callable.From<Resource, string, int>(SaveQuiz), QuizType);
        ExpectEqual(saveError, Error.Ok, "RegisterExtensionSaver registers a save function");
        Expect(ResourceLoader.HasExtensionLoader(Extension), "HasExtensionLoader finds the extension");
        Expect(ResourceSaver.HasExtensionSaver("." + Extension.ToUpperInvariant()), "HasExtensionSaver ignores the dot and the case");

        string path = Root + "questions." + Extension;
        ExpectEqual(ResourceSaver.Save(MakeQuiz(), path), Error.Ok, "Save writes the file through the save function");
        ExpectEqual(FileAccess.GetFileAsString(path), string.Join('\n', Questions), "the file holds one question per line");
        Expect(ResourceLoader.Exists(path), "Exists finds the file");
        ExpectEqual(ResourceLoader.GetResourceType(path), "Resource", "GetResourceType reports the native base of the script class");

        Resource loaded = GD.Load<Resource>(path);
        Expect(loaded is QuizSetCs, "Load returns a QuizSetCs");
        var quiz = (QuizSetCs)loaded;
        Expect(quiz.Questions.SequenceEqual(Questions), "the loaded questions match");
        Expect(ResourceLoader.HasCached(path), "the loaded resource is cached like any other resource");
        Expect(GD.Load<Resource>(path) == loaded, "a second load returns the cached resource");
        Resource hinted = ResourceLoader.Load(path, QuizType, ResourceLoader.CacheMode.Ignore);
        Expect(hinted is QuizSetCs, "Load with the type hint returns a QuizSetCs");
        Expect(hinted != loaded, "Load with CacheMode.Ignore returns a new resource");
    }

    private async Task TestThreadedLoad()
    {
        string path = Root + "threaded." + Extension;
        ResourceSaver.Save(MakeQuiz(), path);
        ExpectEqual(ResourceLoader.LoadThreadedRequest(path), Error.Ok, "LoadThreadedRequest accepts the file");
        ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(path);
        int frames = 0;
        while (status == ResourceLoader.ThreadLoadStatus.InProgress && frames < 600)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            frames++;
            status = ResourceLoader.LoadThreadedGetStatus(path);
        }
        ExpectEqual(status, ResourceLoader.ThreadLoadStatus.Loaded, "the threaded load finishes");
        Resource loaded = ResourceLoader.LoadThreadedGet(path);
        Expect(loaded is QuizSetCs, "the threaded load calls the load function");
    }

    private void TestTypeRules()
    {
        string[] quizExtensions = ResourceLoader.GetRecognizedExtensionsForType(QuizType);
        Expect(quizExtensions.Contains(Extension), "GetRecognizedExtensionsForType lists the extension for the script class");
        string[] textureExtensions = ResourceLoader.GetRecognizedExtensionsForType("Texture2D");
        Expect(!textureExtensions.Contains(Extension), "the extension is not offered for unrelated types");
        string[] saverExtensions = ResourceSaver.GetRecognizedExtensions(MakeQuiz());
        Expect(saverExtensions.Contains(Extension), "ResourceSaver offers the extension for the script class");

        Error plainError = ResourceSaver.Save(new Resource(), Root + "plain." + Extension);
        ExpectEqual(plainError, Error.FileUnrecognized, "resources of another type are not saved with the format");

        Error nodeError = ResourceLoader.RegisterExtensionLoader("csquizbad", Callable.From<string, Variant>(LoadQuiz), "Node");
        ExpectEqual(nodeError, Error.InvalidParameter, "a type that is not a Resource is rejected");
        Error emptyError = ResourceLoader.RegisterExtensionLoader("", Callable.From<string, Variant>(LoadQuiz));
        ExpectEqual(emptyError, Error.InvalidParameter, "an empty extension is rejected");
        Error callableError = ResourceSaver.RegisterExtensionSaver("csquizbad", new Callable());
        ExpectEqual(callableError, Error.InvalidParameter, "an invalid callable is rejected");
        Expect(!ResourceLoader.HasExtensionLoader("csquizbad"), "a rejected loader leaves nothing behind");
        Expect(!ResourceSaver.HasExtensionSaver("csquizbad"), "a rejected saver leaves nothing behind");
    }

    private void TestAtFront()
    {
        string path = Root + "data.json";
        using (FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
        {
            file.StoreString("{}");
        }
        ResourceLoader.RegisterExtensionLoader("json", Callable.From<string, Variant>(LoadQuiz), QuizType);
        Resource behind = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        Expect(behind is Json, "without atFront the built-in JSON loader is asked first");
        ResourceLoader.UnregisterExtensionLoader("json");
        ResourceLoader.RegisterExtensionLoader("json", Callable.From<string, Variant>(LoadQuiz), QuizType, true);
        Resource front = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        Expect(front is QuizSetCs, "with atFront the new function is asked before the built-in loaders");
        ResourceLoader.UnregisterExtensionLoader("json");
    }

    private void TestErrors()
    {
        string path = Root + "broken.csquizerr";
        using (FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
        {
            file.StoreString("x");
        }
        Error registerError = ResourceLoader.RegisterExtensionLoader("csquizerr", Callable.From<string, Variant>(LoadCorrupt));
        ExpectEqual(registerError, Error.Ok, "register a load function that fails");
        Resource failed = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        Expect(failed == null, "an Error returned by the load function makes Load fail");

        Error replaceError = ResourceLoader.RegisterExtensionLoader("csquizerr", Callable.From<string, Variant>(LoadQuiz));
        ExpectEqual(replaceError, Error.Ok, "register the extension again with another function");
        Resource replaced = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        Expect(replaced is QuizSetCs, "the second registration replaces the first");
        var quiz = (QuizSetCs)replaced;
        Expect(quiz.Questions.SequenceEqual(["x"]), "the replaced function loaded the file");
        ExpectEqual(ResourceLoader.UnregisterExtensionLoader("csquizerr"), Error.Ok, "unregister the replaced extension");
    }

    private void TestUnregister()
    {
        string path = Root + "questions." + Extension;
        ExpectEqual(ResourceLoader.UnregisterExtensionLoader(Extension), Error.Ok, "UnregisterExtensionLoader removes the loader");
        ExpectEqual(ResourceSaver.UnregisterExtensionSaver(Extension), Error.Ok, "UnregisterExtensionSaver removes the saver");
        Expect(!ResourceLoader.HasExtensionLoader(Extension), "HasExtensionLoader is false afterwards");
        Expect(!ResourceSaver.HasExtensionSaver(Extension), "HasExtensionSaver is false afterwards");
        ExpectEqual(ResourceLoader.UnregisterExtensionLoader(Extension), Error.DoesNotExist, "unregistering twice returns Error.DoesNotExist");
        Resource failed = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        Expect(failed == null, "loading fails after unregistering");
        Error saveError = ResourceSaver.Save(MakeQuiz(), Root + "after." + Extension);
        ExpectEqual(saveError, Error.FileUnrecognized, "saving fails after unregistering");
    }

    private static void Cleanup()
    {
        foreach (string file in DirAccess.GetFilesAt(Root))
        {
            DirAccess.RemoveAbsolute(Root + file);
        }
    }
}
