using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue95FeatureSceneFormatLoaders;

/// <summary>C# checks of 95-feature-scene-format-loaders. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue95FeatureSceneFormatLoadersCheck : FeatureCheckCs
{
    private const string Folder = "user://95-feature-scene-format-loaders-cs";
    private static readonly Vector3 HullPosition = new(0, 1, 0);

    protected override string CheckName => "95-feature-scene-format-loaders";

    // A Node3D "Ship" with one MeshInstance3D "Hull" (BoxMesh), packed.
    private PackedScene MakeShipScene()
    {
        var ship = new Node3D { Name = "Ship" };
        var hull = new MeshInstance3D { Name = "Hull", Mesh = new BoxMesh(), Position = HullPosition };
        ship.AddChild(hull);
        hull.Owner = ship;
        var scene = new PackedScene();
        Expect(scene.Pack(ship) == Error.Ok, "the test scene packs");
        ship.Free();
        return scene;
    }

    private static MeshInstance3D FindHull(Node root)
    {
        if (root.Name == "Hull")
        {
            return root as MeshInstance3D;
        }
        return root.FindChild("Hull", true, false) as MeshInstance3D;
    }

    private void CheckShip(PackedScene scene, string label)
    {
        if (!Expect(scene != null, $"{label}: loads as a PackedScene"))
        {
            return;
        }
        var root = scene.Instantiate();
        if (!Expect(root != null, $"{label}: instantiates"))
        {
            return;
        }
        var hull = FindHull(root);
        if (Expect(hull != null, $"{label}: contains the MeshInstance3D 'Hull'"))
        {
            Expect(hull.Mesh != null && hull.Mesh.GetSurfaceCount() == 1, $"{label}: Hull has a mesh with one surface");
            Expect(hull.Position.IsEqualApprox(HullPosition), $"{label}: Hull keeps its position");
        }
        root.Free();
    }

    protected override async Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Folder);
        var ship = MakeShipScene();

        // Settings: typed, with the documented defaults.
        Expect((int)ProjectSettings.GetSetting("filesystem/scene_formats/embedded_image_handling") == (int)GltfState.HandleBinaryImageModeEnum.EmbedAsUncompressed, "embedded_image_handling defaults to Embed as Uncompressed");
        Expect((bool)ProjectSettings.GetSetting("filesystem/scene_formats/import_animations"), "import_animations defaults to true");
        Expect(Mathf.IsEqualApprox((double)ProjectSettings.GetSetting("filesystem/scene_formats/animation_fps"), 30.0), "animation_fps defaults to 30");
        Expect(!(bool)ProjectSettings.GetSetting("filesystem/scene_formats/animation_trimming"), "animation_trimming defaults to false");
        Expect((bool)ProjectSettings.GetSetting("filesystem/scene_formats/remove_immutable_tracks"), "remove_immutable_tracks defaults to true");

        // Extensions are known to ResourceLoader and ResourceSaver.
        var loadExtensions = ResourceLoader.GetRecognizedExtensionsForType("PackedScene");
        foreach (var extension in new[] { "gltf", "glb", "fbx" })
        {
            Expect(loadExtensions.Contains(extension), $"ResourceLoader lists .{extension} for PackedScene");
        }
        var saveExtensions = ResourceSaver.GetRecognizedExtensions(ship);
        Expect(saveExtensions.Contains("glb") && saveExtensions.Contains("gltf"), "ResourceSaver lists .glb and .gltf for a PackedScene");
        Expect(!saveExtensions.Contains("fbx"), "ResourceSaver does not list .fbx");

        // .glb round trip.
        var glbPath = Folder.PathJoin("ship.glb");
        Expect(ResourceSaver.Save(ship, glbPath) == Error.Ok, "ResourceSaver.Save writes .glb");
        Expect(FileAccess.FileExists(glbPath), ".glb file exists");
        Expect(ResourceLoader.Exists(glbPath, "PackedScene"), "ResourceLoader.Exists finds the .glb");
        CheckShip(ResourceLoader.Load<PackedScene>(glbPath, "PackedScene", ResourceLoader.CacheMode.Ignore), ".glb");

        // .gltf round trip (external .bin buffer).
        var gltfPath = Folder.PathJoin("ship.gltf");
        Expect(ResourceSaver.Save(ship, gltfPath) == Error.Ok, "ResourceSaver.Save writes .gltf");
        Expect(FileAccess.FileExists(Folder.PathJoin("ship.bin")), ".gltf has an external .bin buffer");
        CheckShip(ResourceLoader.Load<PackedScene>(gltfPath, "", ResourceLoader.CacheMode.Ignore), ".gltf");

        // Cache: two loads give the same resource.
        var cached = ResourceLoader.Load(glbPath);
        Expect(cached != null && ResourceLoader.Load(glbPath) == cached, "cached loads return the same PackedScene");

        // Threaded load.
        var threadedPath = Folder.PathJoin("ship_threaded.glb");
        Expect(ResourceSaver.Save(ship, threadedPath) == Error.Ok, "ResourceSaver.Save writes a second .glb");
        Expect(ResourceLoader.LoadThreadedRequest(threadedPath, "PackedScene") == Error.Ok, "LoadThreadedRequest accepts the .glb");
        var status = ResourceLoader.LoadThreadedGetStatus(threadedPath);
        while (status == ResourceLoader.ThreadLoadStatus.InProgress)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            status = ResourceLoader.LoadThreadedGetStatus(threadedPath);
        }
        Expect(status == ResourceLoader.ThreadLoadStatus.Loaded, "threaded load finishes");
        CheckShip(ResourceLoader.LoadThreadedGet(threadedPath) as PackedScene, "threaded .glb");

        // FBX: loading only. Saving is not supported (ufbx is read-only).
        Expect(ResourceSaver.Save(ship, Folder.PathJoin("ship.fbx")) != Error.Ok, "ResourceSaver.Save to .fbx fails");
    }
}
