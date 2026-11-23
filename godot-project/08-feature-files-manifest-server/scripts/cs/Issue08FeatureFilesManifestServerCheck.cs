using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue08FeatureFilesManifestServer;

/// <summary>C# checks of 08-feature-files-manifest-server. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue08FeatureFilesManifestServerCheck : FeatureCheckCs
{
    private const string Root = "user://08-feature-files-manifest-server/cs/";
    private const string Name = "fms08cs";
    private readonly List<string> _added = new();
    private readonly List<string> _removed = new();

    protected override string CheckName => "08-feature-files-manifest-server";

    private static void Write(string path)
    {
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        f.StoreString("[gd_resource type=\"Resource\" format=3]\n\n[resource]\n");
    }

    private async Task<bool> WaitFor(List<string> list, double seconds)
    {
        ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000.0);
        while (list.Count == 0 && Time.GetTicksMsec() < end)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        return list.Count > 0;
    }

    private void OnAdded(string name, string path)
    {
        if (name == Name)
        {
            _added.Add(path);
        }
    }

    private void OnRemoved(string name, string path)
    {
        if (name == Name)
        {
            _removed.Add(path);
        }
    }

    protected override async Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root + "mods");
        Write(Root + "mods/a.tres");
        Expect(Engine.HasSingleton("FilesManifestServer"), "FilesManifestServer singleton exists");

        ExpectEqual(FilesManifestServer.RegisterManifest(Name, new FilesManifest()), Error.Ok, "RegisterManifest()");
        Expect(System.Array.IndexOf(FilesManifestServer.GetManifestNames(), Name) >= 0, "GetManifestNames() lists the manifest");
        ExpectEqual(FilesManifestServer.AddFolder(Name, Root + "mods"), Error.Ok, "AddFolder()");
        ExpectEqual(FilesManifestServer.AddFolder(Name, Root + "mods"), Error.AlreadyExists, "AddFolder() twice: AlreadyExists");
        ExpectEqual(FilesManifestServer.AddExtension(Name, "TRES"), Error.Ok, "AddExtension()");
        ExpectEqual(FilesManifestServer.GetPaths(Name).Length, 1, "GetPaths(): one file");
        ExpectEqual(FilesManifestServer.SaveManifest(Name, Root + "manifest.tres"), Error.Ok, "SaveManifest()");
        var loaded = ResourceLoader.Load(Root + "manifest.tres", "", ResourceLoader.CacheMode.Ignore) as FilesManifest;
        Expect(loaded != null && loaded.Extensions.Length == 1 && loaded.Extensions[0] == ".tres" && loaded.FolderPaths.Length == 1, "saved manifest has the changes");

        FilesManifestServer.AddFolder(Name, Root + "missing");
        FilesManifestServer.AddExtension(Name, "nosuchext");
        var result = FilesManifestServer.Validate(Name);
        Expect(!(bool)result["valid"] && ((string[])result["missing_folders"]).Length == 1, "Validate() reports a missing folder");
        var unknown = (string[])result["unknown_extensions"];
        Expect(unknown.Length == 1 && unknown[0] == ".nosuchext", "Validate() reports an unknown extension");
        ExpectEqual(FilesManifestServer.CreateMissingFolders(Name), Error.Ok, "CreateMissingFolders()");
        Expect(DirAccess.DirExistsAbsolute(Root + "missing"), "missing folder created");

        FilesManifestServer.Singleton.FileAdded += OnAdded;
        FilesManifestServer.Singleton.FileRemoved += OnRemoved;
        FilesManifestServer.WatchInterval = 0.1;
        ExpectEqual(FilesManifestServer.SetWatching(Name, true), Error.Ok, "SetWatching()");
        Write(Root + "mods/b.tres");
        Expect(await WaitFor(_added, 1.0), "FileAdded within one second");
        DirAccess.RemoveAbsolute(Root + "mods/b.tres");
        Expect(await WaitFor(_removed, 1.0), "FileRemoved within one second");
        FilesManifestServer.Singleton.FileAdded -= OnAdded;
        FilesManifestServer.Singleton.FileRemoved -= OnRemoved;

        ExpectEqual(FilesManifestServer.UnregisterManifest(Name), Error.Ok, "UnregisterManifest()");
        Expect(!FilesManifestServer.HasManifest(Name), "manifest is gone");
        foreach (string f in new[] { "mods/a.tres", "manifest.tres" })
        {
            DirAccess.RemoveAbsolute(Root + f);
        }
        foreach (string d in new[] { "mods", "missing", "" })
        {
            DirAccess.RemoveAbsolute(Root + d);
        }
    }
}
