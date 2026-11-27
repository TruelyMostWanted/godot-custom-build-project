using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.VideoFiles;

/// <summary>Full API test of Video Files (runtime loaders of .ogv and .webm). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string Dir = "user://video-files-test";
    private const string Res = "res://video-files/resources";
    private const double Length = 1.0;
    private const double LengthTolerance = 0.2;

    // File in resources, extension, class, video size. Both files are 1 s long, without audio.
    private static readonly (string File, string Extension, string ClassName, Vector2 Size)[] Videos =
    [
        ("clip.ogv", "ogv", "VideoStreamTheora", new Vector2(64, 48)),
        ("testsrc.webm", "webm", "VideoStreamWebm", new Vector2(64, 64)),
    ];

    protected override async Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        foreach ((string file, string extension, string className, Vector2 size) in Videos)
        {
            CopyToUser(file, extension);
            TestLoad(extension, className);
            TestSaverRefused(extension);
            await TestPlayback(extension, size);
            await TestThreaded(extension, className);
        }
        TestInvalidFiles();
        TestResFiles();
    }

    private static void CopyToUser(string fileName, string extension)
    {
        byte[] bytes = FileAccess.GetFileAsBytes($"{Res}/{fileName}");
        using FileAccess file = FileAccess.Open($"{Dir}/video.{extension}", FileAccess.ModeFlags.Write);
        file.StoreBuffer(bytes);
    }

    private void TestLoad(string extension, string expectedClass)
    {
        string path = $"{Dir}/video.{extension}";
        Expect(ResourceLoader.Exists(path), $"ResourceLoader.Exists finds the .{extension} in user://");
        Expect(ResourceLoader.Exists(path, "VideoStream"), $".{extension} exists with the hint VideoStream");
        Expect(ResourceLoader.Exists(path, "Resource"), $".{extension} exists with the hint Resource");
        Expect(!ResourceLoader.Exists($"{Dir}/missing.{extension}"), $"a missing .{extension} does not exist");
        Expect(Array.IndexOf(ResourceLoader.GetRecognizedExtensionsForType(expectedClass), extension) >= 0, $"{extension} is a recognized extension");
        var stream = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) as VideoStream;
        ExpectEqual(stream.GetClass(), expectedClass, $".{extension} loads as {expectedClass}");
        ExpectEqual((string)stream.Get("file"), path, $".{extension} stream points to the loaded file");
        Resource asResource = ResourceLoader.Load(path, "Resource", ResourceLoader.CacheMode.Ignore);
        ExpectEqual(asResource.GetClass(), expectedClass, $".{extension} loads with the hint Resource");
        Resource first = GD.Load(path);
        Resource second = GD.Load(path);
        Expect(first == second, $".{extension} is cached");
        Expect(ResourceLoader.HasCached(path), $".{extension} path is in the resource cache");
    }

    private void TestSaverRefused(string extension)
    {
        var stream = GD.Load<VideoStream>($"{Dir}/video.{extension}");
        Expect(Array.IndexOf(ResourceSaver.GetRecognizedExtensions(stream), extension) < 0, $"ResourceSaver offers no .{extension}");
        Expect(ResourceSaver.Save(stream, $"{Dir}/copy.{extension}") != Error.Ok, $"ResourceSaver.Save cannot write .{extension}");
    }

    private async Task TestPlayback(string extension, Vector2 videoSize)
    {
        var player = new VideoStreamPlayer { Size = videoSize };
        AddChild(player);
        player.Stream = GD.Load<VideoStream>($"{Dir}/video.{extension}");
        Expect(Math.Abs(player.GetStreamLength() - Length) < LengthTolerance, $".{extension} length is about 1 s");
        player.Play();
        for (int i = 0; i < 5; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Expect(player.IsPlaying(), $".{extension} plays");
        Texture2D texture = player.GetVideoTexture();
        ExpectEqual(texture.GetSize(), videoSize, $".{extension} video size");
        player.QueueFree();
    }

    private async Task TestThreaded(string extension, string expectedClass)
    {
        string path = $"{Dir}/video.{extension}";
        ExpectEqual(ResourceLoader.LoadThreadedRequest(path, "VideoStream"), Error.Ok, $"LoadThreadedRequest accepts the .{extension}");
        while (ResourceLoader.LoadThreadedGetStatus(path) == ResourceLoader.ThreadLoadStatus.InProgress)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        ExpectEqual(ResourceLoader.LoadThreadedGetStatus(path), ResourceLoader.ThreadLoadStatus.Loaded, $"the threaded load of the .{extension} finishes");
        Resource stream = ResourceLoader.LoadThreadedGet(path);
        ExpectEqual(stream.GetClass(), expectedClass, $"LoadThreadedGet returns {expectedClass}");
    }

    private void TestInvalidFiles()
    {
        foreach (string extension in new[] { "ogv", "webm" })
        {
            string path = $"{Dir}/broken.{extension}";
            using (FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
            {
                file.StoreString("not a video");
            }
            Resource broken = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
            Expect(broken == null, $"a .{extension} that is not a video does not load");
        }
    }

    // Videos are never imported: files in res:// without .import and with import mode Keep File use the runtime loader.
    private void TestResFiles()
    {
        Expect(GD.Load(Res + "/clip.ogv") is VideoStreamTheora, "res:// .ogv without import loads");
        Expect(GD.Load(Res + "/kept_clip.ogv") is VideoStreamTheora, ".ogv with import mode Keep File loads");
        Expect(GD.Load(Res + "/testsrc.webm") is VideoStreamWebm, "res:// .webm loads");
    }
}
