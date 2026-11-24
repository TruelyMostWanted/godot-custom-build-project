using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue70FeatureLoadResult;

/// <summary>C# checks of 70-feature-load-result. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue70FeatureLoadResultCheck : FeatureCheckCs
{
    private const string Root = "user://70-feature-load-result/cs/";
    private const string Ext = "cslr";
    private const string ExtBroken = "cslrbroken";
    private const string ExtImage = "cslrimage";
    private const string BrokenMessage = "Broken cslr file";

    protected override string CheckName => "70-feature-load-result";

    protected override async Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        CheckApi();
        RegisterFormats();
        CheckMissingFile();
        CheckBrokenFile();
        CheckSuccess();
        CheckTypeMismatch();
        CheckTryLoad();
        CheckSave();
        await CheckThreadedLoad();
        Cleanup();
    }

    // A file of the test format holds one line of text, stored as the "text" meta of a plain Resource.
    private static Variant LoadText(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            return (int)FileAccess.GetOpenError();
        }
        var resource = new Resource();
        resource.SetMeta("text", file.GetAsText());
        return resource;
    }

    private static int SaveText(Resource resource, string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            return (int)Error.CantCreate;
        }
        file.StoreString((string)resource.GetMeta("text", ""));
        return (int)Error.Ok;
    }

    // Like a loader that finds a broken file: prints the reason, then returns the code.
    private static Variant LoadBroken(string path)
    {
        GD.PushError($"{BrokenMessage}: '{path}'.");
        return (int)Error.FileCorrupt;
    }

    private static Resource MakeResource(string text)
    {
        var resource = new Resource();
        resource.SetMeta("text", text);
        return resource;
    }

    private static void Write(string path, string text)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file?.StoreString(text);
    }

    private void CheckApi()
    {
        Expect(ClassDB.ClassExists("ResourceLoadResult"), "ResourceLoadResult exists");
        Expect(ClassDB.ClassExists("ResourceSaveResult"), "ResourceSaveResult exists");
        Expect(ClassDB.ClassHasMethod("ResourceLoader", "load_result"), "ResourceLoader.load_result() is bound");
        Expect(ClassDB.ClassHasMethod("ResourceLoader", "load_threaded_get_result"), "ResourceLoader.load_threaded_get_result() is bound");
        Expect(ClassDB.ClassHasMethod("ResourceSaver", "save_result"), "ResourceSaver.save_result() is bound");
    }

    private void RegisterFormats()
    {
        ExpectEqual(ResourceLoader.RegisterExtension(Ext, Callable.From<string, Variant>(LoadText)), Error.Ok, "register the text format loader");
        ExpectEqual(ResourceSaver.RegisterExtension(Ext, Callable.From<Resource, string, int>(SaveText)), Error.Ok, "register the text format saver");
        ExpectEqual(ResourceLoader.RegisterExtension(ExtBroken, Callable.From<string, Variant>(LoadBroken)), Error.Ok, "register the broken format loader");
        ExpectEqual(ResourceLoader.RegisterExtension(ExtImage, Callable.From<string, Variant>(LoadText), "Image"), Error.Ok, "register a format that claims to load Images");
    }

    private void CheckMissingFile()
    {
        string path = Root + "missing." + Ext;
        DirAccess.RemoveAbsolute(path);
        ResourceLoadResult result = ResourceLoader.LoadResult(path);
        Expect(result != null && !result.Ok, "LoadResult() of a missing file is not ok");
        ExpectEqual(result.Error, Error.FileNotFound, "a missing file gives Error.FileNotFound");
        Expect(result.ErrorMessage.Contains("missing." + Ext), $"the message names the path: {result.ErrorMessage}");
        Expect(result.Resource == null, "a failed load has no resource");
    }

    private void CheckBrokenFile()
    {
        string path = Root + "broken." + ExtBroken;
        Write(path, "broken");
        ResourceLoadResult result = ResourceLoader.LoadResult(path, "", ResourceLoader.CacheMode.Ignore);
        Expect(!result.Ok, "LoadResult() of a broken file is not ok");
        ExpectEqual(result.Error, Error.FileCorrupt, "a broken file gives the loader's code");
        Expect(result.ErrorMessage.Contains(BrokenMessage), $"the message is the loader's message: {result.ErrorMessage}");
    }

    private void CheckSuccess()
    {
        string path = Root + "ok." + Ext;
        ResourceSaveResult saved = ResourceSaver.SaveResult(MakeResource("Hello"), path);
        Expect(saved.Ok && saved.Error == Error.Ok && saved.ErrorMessage.Length == 0, "SaveResult() of a writable path is ok");
        ExpectEqual(saved.Path, path, "SaveResult() reports the path");
        ResourceLoadResult result = ResourceLoader.LoadResult(path);
        Expect(result.Ok && result.Error == Error.Ok && result.ErrorMessage.Length == 0, "LoadResult() of a valid file is ok");
        Expect(result.Resource != null && (string)result.Resource.GetMeta("text", "") == "Hello", "the result holds the loaded resource");
        Expect(result.Resource == ResourceLoader.Load(path), "the resource is the cached instance that Load() returns");
        ExpectEqual(result.Path, path, "LoadResult() reports the path");
    }

    private void CheckTypeMismatch()
    {
        string path = Root + "plain." + ExtImage;
        Write(path, "not an image");
        ResourceLoadResult result = ResourceLoader.LoadResult(path, "Image", ResourceLoader.CacheMode.Ignore);
        Expect(!result.Ok, "a resource of another type than the type hint is not ok");
        ExpectEqual(result.Error, Error.InvalidData, "a type mismatch gives Error.InvalidData");
        Expect(result.ErrorMessage.Contains("expected Image, got Resource"), $"the message names both types: {result.ErrorMessage}");
    }

    private void CheckTryLoad()
    {
        string path = Root + "ok." + Ext;
        string missing = Root + "missing." + Ext;

        Expect(ResourceLoader.TryLoad<Resource>(path, out Resource loaded) && loaded == ResourceLoader.Load(path), "TryLoad<Resource>() of a valid file returns true and the cached resource");
        Expect(!ResourceLoader.TryLoad<Resource>(missing, out Resource none) && none == null, "TryLoad<Resource>() of a missing file returns false");

        bool missingOk = ResourceLoader.TryLoad<Resource>(missing, out _, out Error missingError);
        Expect(!missingOk, "TryLoad<Resource>(out error) of a missing file returns false");
        ExpectEqual(missingError, Error.FileNotFound, "TryLoad<Resource>() of a missing file gives Error.FileNotFound");

        bool wrongOk = ResourceLoader.TryLoad<Texture2D>(path, out Texture2D texture, out Error wrongError);
        Expect(!wrongOk && texture == null, "TryLoad<Texture2D>() of a plain Resource returns false");
        ExpectEqual(wrongError, Error.InvalidData, "TryLoad<T>() of a resource of another type gives Error.InvalidData");

        bool brokenOk = ResourceLoader.TryLoad<Resource>(Root + "broken." + ExtBroken, out _, out Error brokenError);
        Expect(!brokenOk, "TryLoad<Resource>() of a broken file returns false");
        ExpectEqual(brokenError, Error.FileCorrupt, "TryLoad<Resource>() of a broken file gives the loader's code");

        Expect(ResourceSaver.TrySave(MakeResource("Again"), Root + "again." + Ext, out Error saveError) && saveError == Error.Ok, "TrySave() to a writable path returns true");
        Expect(!ResourceSaver.TrySave(MakeResource("Again"), Root + "no_such_folder/again." + Ext, out Error failedSaveError), "TrySave() to a path that cannot be written returns false");
        ExpectEqual(failedSaveError, Error.CantCreate, "TrySave() gives the saver's code");
    }

    private void CheckSave()
    {
        string path = Root + "no_such_folder/settings." + Ext;
        ResourceSaveResult result = ResourceSaver.SaveResult(MakeResource("Hello"), path);
        Expect(!result.Ok, "SaveResult() to a path that cannot be written is not ok");
        ExpectEqual(result.Error, Error.CantCreate, "the error is the saver's code");
        Expect(result.ErrorMessage.Contains("Can't save"), $"the message is the saver's message: {result.ErrorMessage}");
    }

    private async Task CheckThreadedLoad()
    {
        string path = Root + "threaded." + ExtBroken;
        Write(path, "broken");
        ExpectEqual(ResourceLoader.LoadThreadedRequest(path, "", false, ResourceLoader.CacheMode.Ignore), Error.Ok, "LoadThreadedRequest() accepts the broken file");
        var status = ResourceLoader.LoadThreadedGetStatus(path);
        for (int frames = 0; status == ResourceLoader.ThreadLoadStatus.InProgress && frames < 600; frames++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            status = ResourceLoader.LoadThreadedGetStatus(path);
        }
        ExpectEqual(status, ResourceLoader.ThreadLoadStatus.Failed, "the threaded load fails");
        ResourceLoadResult result = ResourceLoader.LoadThreadedGetResult(path);
        Expect(!result.Ok, "LoadThreadedGetResult() of a broken file is not ok");
        ExpectEqual(result.Error, Error.FileCorrupt, "the threaded load gives the loader's code");
        Expect(result.ErrorMessage.Contains(BrokenMessage), $"the message printed on the worker thread is captured: {result.ErrorMessage}");
        ResourceLoadResult again = ResourceLoader.LoadThreadedGetResult(path);
        ExpectEqual(again.Error, Error.InvalidParameter, "a result that was already collected gives Error.InvalidParameter");
    }

    private void Cleanup()
    {
        ResourceLoader.UnregisterExtension(Ext);
        ResourceSaver.UnregisterExtension(Ext);
        ResourceLoader.UnregisterExtension(ExtBroken);
        ResourceLoader.UnregisterExtension(ExtImage);
        foreach (string file in DirAccess.GetFilesAt(Root))
        {
            DirAccess.RemoveAbsolute(Root + file);
        }
    }
}
