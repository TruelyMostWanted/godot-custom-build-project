using Godot;
using Godot.Collections;

namespace TruelyInteractive.JsonFormat;

/// <summary>Minimal example of JSON: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
/// <remarks>Item is the GDScript class of text-serialization/scripts/gd/item.gd.</remarks>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API
        var file = GD.Load<Json>("res://json/resources/enemies.json");
        foreach (var enemy in file.Data.AsGodotDictionary()["enemies"].AsGodotArray())
        {
            var e = enemy.AsGodotDictionary();
            GD.Print(e["name"], " ", (int)e["hp"].AsDouble());
        }

        var json = new Json();
        if (json.Parse("{\"name\": ") != Error.Ok)
            GD.Print(json.GetErrorLine(), ": ", json.GetErrorMessage());

        // Load enemies from a web service: the body of a request, here the shipped file.
        byte[] body = FileAccess.GetFileAsBytes("res://json/resources/enemies.json");
        OnRequestCompleted((long)HttpRequest.Result.Success, 200, [], body);

        // Save your own resource as JSON
        var itemScript = GD.Load<GDScript>("res://text-serialization/scripts/gd/item.gd");
        var sword = (Resource)itemScript.New();
        sword.Set("name", "Sword");
        ResourceSaver.Save(sword, "user://sword.json");
        var loaded = GD.Load<Resource>("user://sword.json");
        string text = Json.Serialize(sword);
        var copy = Json.Deserialize(text);
        GD.Print(loaded.Get("name"), " ", copy.Get("name"));
        GD.Print(text);
        GetTree().Quit();
    }

    private void OnRequestCompleted(long result, long code, string[] headers, byte[] body)
    {
        Variant data = Json.ParseString(body.GetStringFromUtf8());
        if (code != 200 || data.VariantType == Variant.Type.Nil)
            return;
        var schema = GD.Load<DataSchema>("res://json/resources/enemies_schema.tres");
        DataValidation check = schema.Validate(data);
        if (!check.IsValid())
        {
            GD.PushWarning(check.GetIssueMessage(0));
            return;
        }
        SpawnEnemies(data.AsGodotDictionary()["enemies"].AsGodotArray());
    }

    private static void SpawnEnemies(Array enemies)
    {
        GD.Print("Spawned ", enemies.Count, " enemies");
    }
}
