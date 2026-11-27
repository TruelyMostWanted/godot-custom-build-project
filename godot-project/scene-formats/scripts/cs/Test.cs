using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.SceneFormats;

/// <summary>Full API test of Scene Formats (runtime glTF/GLB/FBX loader, glTF/GLB saver, settings). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string Dir = "user://scene-formats-test";
    private const string Settings = "filesystem/scene_formats/";
    private static readonly Vector3 HullPosition = new(0, 1, 0);

    protected override async Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        TestSettings();
        TestExtensions();
        TestGlb();
        TestGltf();
        TestCache();
        await TestThreaded();
        TestFbxNotSaved();
    }

    // A Node3D "Ship" with one MeshInstance3D "Hull" (BoxMesh), packed.
    private PackedScene MakeShip()
    {
        var ship = new Node3D { Name = "Ship" };
        var hull = new MeshInstance3D { Name = "Hull", Mesh = new BoxMesh(), Position = HullPosition };
        ship.AddChild(hull);
        hull.Owner = ship;
        var scene = new PackedScene();
        ExpectEqual(scene.Pack(ship), Error.Ok, "the test scene packs");
        ship.Free();
        return scene;
    }

    private void ExpectShip(PackedScene scene, string label)
    {
        Expect(scene != null, $"{label} loads as a PackedScene");
        Node root = scene.Instantiate();
        var hull = root.FindChild("Hull", true, false) as MeshInstance3D;
        Expect(hull != null, $"{label} contains the MeshInstance3D Hull");
        ExpectEqual(hull.Mesh.GetSurfaceCount(), 1, $"{label}: Hull has a mesh with one surface");
        Expect(hull.Position.IsEqualApprox(HullPosition), $"{label}: Hull keeps its position");
        root.Free();
    }

    private void TestSettings()
    {
        ExpectEqual((int)ProjectSettings.GetSetting(Settings + "embedded_image_handling"), (int)GltfState.HandleBinaryImageModeEnum.EmbedAsUncompressed, "embedded_image_handling defaults to Embed as Uncompressed");
        ExpectEqual((bool)ProjectSettings.GetSetting(Settings + "import_animations"), true, "import_animations defaults to true");
        ExpectEqual((double)ProjectSettings.GetSetting(Settings + "animation_fps"), 30.0, "animation_fps defaults to 30");
        ExpectEqual((bool)ProjectSettings.GetSetting(Settings + "animation_trimming"), false, "animation_trimming defaults to false");
        ExpectEqual((bool)ProjectSettings.GetSetting(Settings + "remove_immutable_tracks"), true, "remove_immutable_tracks defaults to true");
    }

    private void TestExtensions()
    {
        string[] loadExtensions = ResourceLoader.GetRecognizedExtensionsForType("PackedScene");
        foreach (string extension in new[] { "gltf", "glb", "fbx" })
        {
            Expect(Array.IndexOf(loadExtensions, extension) >= 0, $"ResourceLoader lists .{extension} for PackedScene");
        }
        string[] saveExtensions = ResourceSaver.GetRecognizedExtensions(MakeShip());
        Expect(Array.IndexOf(saveExtensions, "glb") >= 0, "ResourceSaver lists .glb for a PackedScene");
        Expect(Array.IndexOf(saveExtensions, "gltf") >= 0, "ResourceSaver lists .gltf for a PackedScene");
        Expect(Array.IndexOf(saveExtensions, "fbx") < 0, "ResourceSaver does not list .fbx");
    }

    private void TestGlb()
    {
        string path = Dir + "/ship.glb";
        ExpectEqual(ResourceSaver.Save(MakeShip(), path), Error.Ok, "ResourceSaver.Save writes .glb");
        Expect(FileAccess.FileExists(path), "the .glb file exists");
        Expect(ResourceLoader.Exists(path, "PackedScene"), "ResourceLoader.Exists finds the .glb");
        ExpectShip(ResourceLoader.Load(path, "PackedScene", ResourceLoader.CacheMode.Ignore) as PackedScene, ".glb");
    }

    private void TestGltf()
    {
        string path = Dir + "/ship.gltf";
        ExpectEqual(ResourceSaver.Save(MakeShip(), path), Error.Ok, "ResourceSaver.Save writes .gltf");
        Expect(FileAccess.FileExists(Dir + "/ship0.bin"), ".gltf has an external .bin buffer");
        ExpectShip(ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) as PackedScene, ".gltf");
    }

    private void TestCache()
    {
        Resource first = ResourceLoader.Load(Dir + "/ship.glb");
        Resource second = ResourceLoader.Load(Dir + "/ship.glb");
        Expect(first == second, "cached loads return the same PackedScene");
    }

    private async Task TestThreaded()
    {
        string path = Dir + "/ship_threaded.glb";
        ResourceSaver.Save(MakeShip(), path);
        ExpectEqual(ResourceLoader.LoadThreadedRequest(path, "PackedScene"), Error.Ok, "LoadThreadedRequest accepts the .glb");
        while (ResourceLoader.LoadThreadedGetStatus(path) == ResourceLoader.ThreadLoadStatus.InProgress)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        ExpectEqual(ResourceLoader.LoadThreadedGetStatus(path), ResourceLoader.ThreadLoadStatus.Loaded, "the threaded load finishes");
        ExpectShip(ResourceLoader.LoadThreadedGet(path) as PackedScene, "threaded .glb");
    }

    // FBX is read-only (ufbx).
    private void TestFbxNotSaved()
    {
        Expect(ResourceSaver.Save(MakeShip(), Dir + "/ship.fbx") != Error.Ok, "ResourceSaver.Save to .fbx fails");
    }
}
