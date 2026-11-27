using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.JsonFormat;

/// <summary>
/// Full API test of JSON: what this engine adds to Godot's JSON (Serialize(), Deserialize(), the .json resource loader and
/// saver) and the behavior the wiki guide promises. Same tests as scripts/gd/Test.gd.
/// </summary>
public partial class Test : FeatureTest
{
    private const string Root = "user://json/cs/";
    private const string Enemies = "res://json/resources/enemies.json";
    private const string Schema = "res://json/resources/enemies_schema.tres";
    private const string Scripts = "res://text-serialization/scripts/gd/";
    private const string PlainText = "{\"answer\": 42}";
    private const int Epic = 3;

    protected override Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        TestDataFile();
        TestParseError();
        TestSchema();
        TestSerialize();
        TestDeserialize();
        TestFiles();
        TestNumbers();
        return Task.CompletedTask;
    }

    private static Resource New(string script)
    {
        var gdScript = GD.Load<GDScript>(Scripts + script);
        return (Resource)gdScript.New();
    }

    private static Resource MakeSword()
    {
        Resource sword = New("item.gd");
        sword.Set("name", "Sword");
        sword.Set("rarity", Epic);
        sword.Set("code", "007");
        return sword;
    }

    private void TestDataFile()
    {
        var file = GD.Load<Json>(Enemies);
        Expect(file != null, "Load() of a .json without \"$type\" returns a Json");
        Dictionary data = file.Data.AsGodotDictionary();
        Expect(data.Keys.Select(key => (string)key).SequenceEqual(["enemies", "boss"]), "Data is the parsed Dictionary");
        Dictionary slime = data["enemies"].AsGodotArray()[0].AsGodotDictionary();
        ExpectEqual(slime["hp"].VariantType, Variant.Type.Float, "every JSON number is a float");
        ExpectEqual((string)slime["drops"].AsGodotArray()[0], "gel", "arrays are parsed");
        ExpectEqual(data["boss"].VariantType, Variant.Type.Nil, "null is parsed");
        ExpectEqual(ResourceLoader.GetResourceType(Enemies), "JSON", "GetResourceType() of a plain .json is JSON");
    }

    private void TestParseError()
    {
        var json = new Json();
        ExpectEqual(json.Parse("{\"name\": "), Error.ParseError, "Parse() of broken text");
        ExpectEqual(json.GetErrorLine(), 0, "GetErrorLine(): the text has one line, counted from 0");
        ExpectEqual(json.GetErrorMessage(), "Expected value, got 'EOF'", "GetErrorMessage()");
        ExpectEqual(Json.ParseString("{\"name\": ").VariantType, Variant.Type.Nil, "ParseString() of broken text is null");
    }

    private void TestSchema()
    {
        string text = FileAccess.GetFileAsString(Enemies);
        Variant data = Json.ParseString(text);
        var schema = GD.Load<DataSchema>(Schema);
        DataValidation check = schema.Validate(data);
        Expect(check.IsValid(), "enemies.json matches enemies_schema.tres (int fields accept JSON floats without a fraction)");
        Dictionary bat = data.AsGodotDictionary()["enemies"].AsGodotArray()[1].AsGodotDictionary();
        bat["hp"] = 6.5;
        DataValidation fraction = schema.Validate(data);
        ExpectEqual(fraction.GetIssueKind(0), DataValidation.IssueKind.TypeMismatch, "an int field rejects a JSON float with a fraction");
    }

    private void TestSerialize()
    {
        Resource sword = MakeSword();
        string text = Json.Serialize(sword);
        ExpectEqual(text, ResourceTextSerializer.Serialize(sword, ResourceTextSerializer.Format.Json), "Serialize() is ResourceTextSerializer.Serialize(Json)");
        Expect(text.StartsWith("{\n  \"$type\": \"Item\",\n  \"$format_version\": 1,\n"), "Serialize() writes \"$type\" and \"$format_version\" first");
        var options = new SerializerOptions { Indent = "", SortKeys = true };
        string compact = Json.Serialize(sword, options);
        ExpectEqual(compact, ResourceTextSerializer.Serialize(sword, ResourceTextSerializer.Format.Json, options), "Serialize() passes the options");
        Expect(!compact.Contains('\n'), "Indent = \"\" writes one line");
    }

    private void TestDeserialize()
    {
        string text = Json.Serialize(MakeSword());
        Resource copy = Json.Deserialize(text);
        ExpectEqual((string)copy.Get("code"), "007", "Deserialize() restores the resource");
        ExpectEqual((int)copy.Get("rarity"), Epic, "Deserialize() reads enum names");
        string handWritten = "{\"name\": \"Shield\", \"count\": 2}";
        Resource shield = Json.Deserialize(handWritten, "Item");
        ExpectEqual((string)shield.Get("name"), "Shield", "Deserialize() with a type hint reads a file without \"$type\"");
        ExpectEqual((double)shield.Get("weight"), 0.5, "missing values keep the defaults");
        var options = new SerializerOptions { EnumsAsNames = false };
        string numbers = Json.Serialize(MakeSword(), options);
        Resource fromNumbers = Json.Deserialize(numbers, "", options);
        ExpectEqual((int)fromNumbers.Get("rarity"), Epic, "Deserialize() with options reads enum numbers");
    }

    private void TestFiles()
    {
        string path = Root + "sword.json";
        ExpectEqual(ResourceSaver.Save(MakeSword(), path), Error.Ok, "ResourceSaver.Save() to .json");
        ExpectEqual(ResourceLoader.GetResourceType(path), "Resource", "GetResourceType() reads \"$type\" (native base of Item)");
        Resource loaded = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        ExpectEqual((string)loaded.Get("code"), "007", "a .json with \"$type\" loads as its class");

        string plainPath = Root + "shield.json";
        using (var file = FileAccess.Open(plainPath, FileAccess.ModeFlags.Write))
        {
            file.StoreString("{\"name\": \"Shield\"}");
        }
        Resource plain = ResourceLoader.Load(plainPath, "", ResourceLoader.CacheMode.Ignore);
        Expect(plain is Json, "a .json without \"$type\" loads as Json");
        Resource shield = ResourceLoader.Load(plainPath, "Item", ResourceLoader.CacheMode.Ignore);
        ExpectEqual((string)shield.Get("name"), "Shield", "a .json without \"$type\" loads as Item with the type hint");

        string dataPath = Root + "plain.json";
        var json = new Json();
        json.Parse(PlainText);
        ExpectEqual(ResourceSaver.Save(json, dataPath), Error.Ok, "ResourceSaver.Save() of a Json resource");
        var again = (Json)ResourceLoader.Load(dataPath, "", ResourceLoader.CacheMode.Ignore);
        ExpectEqual((double)again.Data.AsGodotDictionary()["answer"], 42.0, "a saved Json resource loads as Json again");
    }

    private void TestNumbers()
    {
        Resource inventory = New("inventory.gd");
        inventory.Set("gold", 7);
        inventory.Set("anything", 3);
        string text = Json.Serialize(inventory);
        Resource copy = Json.Deserialize(text);
        ExpectEqual(copy.Get("gold").VariantType, Variant.Type.Int, "a typed int property stays int");
        ExpectEqual(copy.Get("anything").VariantType, Variant.Type.Float, "a Variant property can't tell int from float: 3 reads back as 3.0");
    }
}
