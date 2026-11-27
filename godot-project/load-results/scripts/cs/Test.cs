using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.LoadResults;

/// <summary>Full API test of Load Results (ResourceLoadResult, ResourceSaveResult, LoadResult, SaveResult, LoadThreadedGetResult).
/// Same tests as scripts/gd/Test.gd, plus TestTryLoad for TryLoad and TrySave, which exist in C# only.
/// Some steps print an engine error on purpose: the result collects it.</summary>
public partial class Test : FeatureTest
{
    private const string Root = "user://load-results/cs/";
    private const string QuizText = "What is 6 * 7?";
    private const string EmptyMessage = "is empty.";

    private readonly QuizLoader _loader = new();

    protected override async Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        ResourceLoader.AddResourceFormatLoader(_loader);
        TestMissingFile();
        TestEmptyFile();
        TestLoadedFile();
        TestTypeMismatch();
        TestSaveResult();
        await TestThreadedResult();
        TestTryLoad();
        Cleanup();
    }

    private static void Write(string path, string text)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreString(text);
    }

    private void TestMissingFile()
    {
        string path = Root + "missing.lrquiz";
        ResourceLoadResult result = ResourceLoader.LoadResult(path);
        Expect(!result.Ok, "LoadResult of a missing file is not ok");
        ExpectEqual(result.Error, Error.FileNotFound, "a missing file gives Error.FileNotFound");
        Expect(result.ErrorMessage.Contains("missing.lrquiz"), "the message names the path: " + result.ErrorMessage);
        Expect(result.Resource == null, "a failed load has no resource");
        ExpectEqual(result.Path, path, "the result reports the path");
    }

    private void TestEmptyFile()
    {
        string path = Root + "empty.lrquiz";
        Write(path, "");
        ResourceLoadResult result = ResourceLoader.LoadResult(path, "", ResourceLoader.CacheMode.Ignore);
        Expect(!result.Ok, "LoadResult of an empty file is not ok");
        ExpectEqual(result.Error, Error.FileCorrupt, "an empty file gives the loader's code");
        string[] lines = result.ErrorMessage.Split('\n');
        Expect(lines[0].Contains(EmptyMessage), "the oldest line is the loader's message: " + lines[0]);
        Expect(lines.Length >= 2, "the engine adds its own lines after it");
    }

    private void TestLoadedFile()
    {
        string path = Root + "quiz.lrquiz";
        Write(path, QuizText);
        ResourceLoadResult result = ResourceLoader.LoadResult(path);
        Expect(result.Ok, "LoadResult of a valid file is ok");
        ExpectEqual(result.Error, Error.Ok, "a valid file gives Error.Ok");
        Expect(result.ErrorMessage.Length == 0, "a valid file has no message");
        string[] questions = (string[])result.Resource.GetMeta("questions");
        Expect(questions.SequenceEqual([QuizText]), "the result holds the loaded resource");
        Resource cached = GD.Load<Resource>(path);
        Expect(result.Resource == cached, "the resource is the cached instance that Load returns");
        ExpectEqual(result.Path, path, "the result reports the path");
    }

    private void TestTypeMismatch()
    {
        string path = Root + "image.lrquiz";
        Write(path, QuizText);
        ResourceLoadResult result = ResourceLoader.LoadResult(path, "Image", ResourceLoader.CacheMode.Ignore);
        Expect(!result.Ok, "a resource of another type than the type hint is not ok");
        ExpectEqual(result.Error, Error.InvalidData, "a type mismatch gives Error.InvalidData");
        Expect(result.ErrorMessage.Contains("expected Image, got Resource"), "the message names both types: " + result.ErrorMessage);
    }

    private void TestSaveResult()
    {
        var resource = new Resource();
        string path = Root + "saved.tres";
        ResourceSaveResult saved = ResourceSaver.SaveResult(resource, path);
        Expect(saved.Ok, "SaveResult to a writable path is ok");
        ExpectEqual(saved.Error, Error.Ok, "a saved file gives Error.Ok");
        Expect(saved.ErrorMessage.Length == 0, "a saved file has no message");
        ExpectEqual(saved.Path, path, "SaveResult reports the path");

        ResourceSaveResult failed = ResourceSaver.SaveResult(resource, Root + "no_such_folder/saved.tres");
        Expect(!failed.Ok, "SaveResult to a path that cannot be written is not ok");
        ExpectEqual(failed.Error, Error.CantOpen, "the error is the saver's code");
        Expect(failed.ErrorMessage.Contains("Cannot save"), "the message is the saver's message: " + failed.ErrorMessage);

        ResourceSaveResult nullResult = ResourceSaver.SaveResult(null, Root + "null.tres");
        ExpectEqual(nullResult.Error, Error.InvalidParameter, "saving null gives Error.InvalidParameter");
    }

    private async Task TestThreadedResult()
    {
        string path = Root + "threaded.lrquiz";
        Write(path, "");
        Error requestError = ResourceLoader.LoadThreadedRequest(path, "", false, ResourceLoader.CacheMode.Ignore);
        ExpectEqual(requestError, Error.Ok, "LoadThreadedRequest accepts the empty file");
        ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(path);
        int frames = 0;
        while (status == ResourceLoader.ThreadLoadStatus.InProgress && frames < 600)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            frames++;
            status = ResourceLoader.LoadThreadedGetStatus(path);
        }
        ExpectEqual(status, ResourceLoader.ThreadLoadStatus.Failed, "the threaded load fails");
        ResourceLoadResult result = ResourceLoader.LoadThreadedGetResult(path);
        Expect(!result.Ok, "LoadThreadedGetResult of an empty file is not ok");
        ExpectEqual(result.Error, Error.FileCorrupt, "the threaded load gives the loader's code");
        Expect(result.ErrorMessage.Contains(EmptyMessage), "the message printed on the worker thread is captured: " + result.ErrorMessage);
        ResourceLoadResult again = ResourceLoader.LoadThreadedGetResult(path);
        ExpectEqual(again.Error, Error.InvalidParameter, "a result that was already collected gives Error.InvalidParameter");
    }

    private void TestTryLoad()
    {
        string path = Root + "quiz.lrquiz";
        string missing = Root + "missing.lrquiz";
        Expect(ResourceLoader.TryLoad<Resource>(path, out Resource loaded), "TryLoad of a valid file returns true");
        Expect(loaded == GD.Load<Resource>(path), "TryLoad returns the cached resource");
        Expect(!ResourceLoader.TryLoad<Resource>(missing, out Resource none), "TryLoad of a missing file returns false");
        Expect(none == null, "TryLoad of a missing file returns no resource");

        bool missingOk = ResourceLoader.TryLoad<Resource>(missing, out _, out Error missingError);
        Expect(!missingOk, "TryLoad with an error of a missing file returns false");
        ExpectEqual(missingError, Error.FileNotFound, "TryLoad of a missing file gives Error.FileNotFound");

        bool wrongOk = ResourceLoader.TryLoad<Texture2D>(path, out Texture2D texture, out Error wrongError);
        Expect(!wrongOk && texture == null, "TryLoad<Texture2D> of a plain Resource returns false");
        ExpectEqual(wrongError, Error.InvalidData, "TryLoad of a resource of another type gives Error.InvalidData");

        bool brokenOk = ResourceLoader.TryLoad<Resource>(Root + "empty.lrquiz", out _, out Error brokenError);
        Expect(!brokenOk, "TryLoad of an empty file returns false");
        ExpectEqual(brokenError, Error.FileCorrupt, "TryLoad of an empty file gives the loader's code");

        bool savedOk = ResourceSaver.TrySave(new Resource(), Root + "again.tres", out Error saveError);
        Expect(savedOk, "TrySave to a writable path returns true");
        ExpectEqual(saveError, Error.Ok, "TrySave to a writable path gives Error.Ok");
        bool failedOk = ResourceSaver.TrySave(new Resource(), Root + "no_such_folder/again.tres", out Error failedError);
        Expect(!failedOk, "TrySave to a path that cannot be written returns false");
        ExpectEqual(failedError, Error.CantOpen, "TrySave gives the saver's code");
    }

    private void Cleanup()
    {
        ResourceLoader.RemoveResourceFormatLoader(_loader);
        foreach (string file in DirAccess.GetFilesAt(Root))
        {
            DirAccess.RemoveAbsolute(Root + file);
        }
    }
}
