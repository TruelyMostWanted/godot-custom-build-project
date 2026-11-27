using System.Threading.Tasks;
using Godot;

namespace TruelyInteractive.SceneFormats;

/// <summary>Minimal example of Scene Formats: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override async void _Ready()
    {
        Setup();
        ChangeImportSettings();
        SpawnModel();
        ExportScene();
        await LoadInBackground();
        GetTree().Quit();
    }

    // Setup: sample models in user://mods/ships (the guide assumes them).
    private static void Setup()
    {
        DirAccess.MakeDirRecursiveAbsolute("user://mods/ships");
        DirAccess.MakeDirRecursiveAbsolute("user://exports");
        var ship = new Node3D();
        var hull = new MeshInstance3D { Mesh = new BoxMesh() };
        ship.AddChild(hull);
        hull.Owner = ship;
        var scene = new PackedScene();
        scene.Pack(ship);
        ship.Free();
        ResourceSaver.Save(scene, "user://mods/ships/cutter.glb");
        ResourceSaver.Save(scene, "user://mods/ships/carrier.glb");
    }

    private static void ChangeImportSettings()
    {
        // Change how models are imported at runtime
        ProjectSettings.SetSetting("filesystem/scene_formats/import_animations", false);
        GD.Print("import_animations: ", ProjectSettings.GetSetting("filesystem/scene_formats/import_animations"));
    }

    private void SpawnModel()
    {
        // Spawn a model from the mods folder
        var scene = GD.Load<PackedScene>("user://mods/ships/cutter.glb");
        AddChild(scene.Instantiate());
        GD.Print("Spawned children: ", GetChildCount());
    }

    private static void ExportScene()
    {
        // Export a scene as GLB
        var ship = new Node3D();
        var hull = new MeshInstance3D { Mesh = new BoxMesh() };
        ship.AddChild(hull);
        hull.Owner = ship;

        var scene = new PackedScene();
        scene.Pack(ship);
        ship.Free();
        Error error = ResourceSaver.Save(scene, "user://exports/ship.glb");
        GD.Print("Exported GLB: ", error == Error.Ok);
    }

    private async Task LoadInBackground()
    {
        // Load a large model in the background
        ResourceLoader.LoadThreadedRequest("user://mods/ships/carrier.glb");
        while (ResourceLoader.LoadThreadedGetStatus("user://mods/ships/carrier.glb") == ResourceLoader.ThreadLoadStatus.InProgress)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var scene = (PackedScene)ResourceLoader.LoadThreadedGet("user://mods/ships/carrier.glb");
        AddChild(scene.Instantiate());
        GD.Print("Background load: ", GetChildCount());
    }
}
