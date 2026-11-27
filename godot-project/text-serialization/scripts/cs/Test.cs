using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.TextSerialization;

/// <summary>
/// Full API test of Text Serialization: ResourceTextSerializer, SerializerOptions and the TextDataFormat base.
/// Same tests as scripts/gd/Test.gd. The resources are the GDScript classes of scripts/gd (Item, ItemTable, Inventory).
/// </summary>
public partial class Test : FeatureTest
{
    private const string Root = "user://text-serialization/cs/";
    private const string Scripts = "res://text-serialization/scripts/gd/";
    private const string OwnerName = "Ada & <Bob> \"the\" 2nd = ok: yes\nline two";
    private const string CompactOptions = "res://text-serialization/resources/compact_options.tres";
    private const string Settings = "filesystem/resources/text_formats/";
    private const int Epic = 3;

    // The texts of the wiki section "Compare the formats".
    private const string SwordJson = "{\n  \"$type\": \"Item\",\n  \"$format_version\": 1,\n  \"name\": \"Sword\",\n  \"count\": 1,\n  \"weight\": 0.5,\n  \"rarity\": \"Epic\",\n  \"offset\": [\n    0.5,\n    -2.0\n  ],\n  \"code\": \"007\"\n}\n";
    private const string SwordJsonCompact = "{\"$format_version\":1,\"$type\":\"Item\",\"code\":\"007\",\"count\":1,\"name\":\"Sword\",\"offset\":[0.5,-2.0],\"rarity\":\"Epic\",\"weight\":0.5}";
    private const string SwordIni = "[root]\n$type=\"Item\"\n$format_version=1\nname=\"Sword\"\ncount=1\nweight=0.5\nrarity=\"Epic\"\noffset=Vector2(0.5, -2)\ncode=\"007\"\n";
    private const string SwordXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<resource type=\"Item\" format_version=\"1\">\n  <name>Sword</name>\n  <count>1</count>\n  <weight>0.5</weight>\n  <rarity>Epic</rarity>\n  <offset kind=\"array\">0.5, -2.0</offset>\n  <code kind=\"string\">007</code>\n</resource>\n";
    private const string SwordToml = "\"$type\" = \"Item\"\n\"$format_version\" = 1\nname = \"Sword\"\ncount = 1\nweight = 0.5\nrarity = \"Epic\"\noffset = [0.5, -2.0]\ncode = \"007\"\n";
    private const string SwordYaml = "$type: Item\n$format_version: 1\nname: Sword\ncount: 1\nweight: 0.5\nrarity: Epic\noffset: [0.5, -2.0]\ncode: \"007\"\n";
    private const string TableCsv = "name,count,weight,rarity,offset,code\nSword,1,0.5,Epic,\"0.5, -2.0\",007\nPotion,3,0.5,Common,\"0.0, 0.0\",\n";

    private static readonly ResourceTextSerializer.Format[] ResourceFormats =
    [
        ResourceTextSerializer.Format.Json, ResourceTextSerializer.Format.Ini, ResourceTextSerializer.Format.Xml,
        ResourceTextSerializer.Format.Toml, ResourceTextSerializer.Format.Yaml,
    ];

    protected override Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        TestFormats();
        TestCompareFormats();
        TestFiles();
        TestRoundTrip();
        TestVariantValues();
        TestCsvTable();
        TestTypeHint();
        TestDictionaries();
        TestOptionDefaults();
        TestOptions();
        TestPreset();
        TestProjectSettings();
        TestTextDataFormat();
        return Task.CompletedTask;
    }

    private static Resource New(string script)
    {
        var gdScript = GD.Load<GDScript>(Scripts + script);
        return (Resource)gdScript.New();
    }

    private static bool IsScript(Resource resource, string script)
    {
        return resource?.GetScript().As<Script>()?.ResourcePath == Scripts + script;
    }

    private static Resource MakeSword()
    {
        Resource sword = New("item.gd");
        sword.Set("name", "Sword");
        sword.Set("rarity", Epic);
        sword.Set("offset", new Vector2(0.5f, -2.0f));
        sword.Set("code", "007");
        return sword;
    }

    private static Resource MakeInventory()
    {
        Resource potion = New("item.gd");
        potion.Set("name", "Potion");
        potion.Set("count", 3);
        Resource space = New("item.gd");
        space.Set("name", " ");
        Resource inventory = New("inventory.gd");
        inventory.Set("owner_name", OwnerName);
        inventory.Set("gold", 250);
        inventory.Set("tint", new Color(0.5f, 0.25f, 1.0f, 0.5f));
        inventory.Set("cell", new Vector3I(1, -2, 3));
        inventory.Set("tags", new[] { "a", "b c" });
        inventory.Set("main_hand", MakeSword());
        var items = (Array)inventory.Get("items");
        items.Add(potion);
        items.Add(space);
        var scores = (Dictionary)inventory.Get("scores");
        scores["ann"] = 10;
        scores["my key"] = 20;
        return inventory;
    }

    private void ExpectInventory(Resource inventory, string label)
    {
        if (!Expect(IsScript(inventory, "inventory.gd"), $"{label}: loads as Inventory"))
        {
            return;
        }
        ExpectEqual((string)inventory.Get("owner_name"), OwnerName, $"{label}: string with & < > quotes = : and a line break");
        ExpectEqual((int)inventory.Get("gold"), 250, $"{label}: int");
        Expect(((Color)inventory.Get("tint")).IsEqualApprox(new Color(0.5f, 0.25f, 1.0f, 0.5f)), $"{label}: Color");
        ExpectEqual((Vector3I)inventory.Get("cell"), new Vector3I(1, -2, 3), $"{label}: Vector3i");
        Expect(((string[])inventory.Get("tags")).SequenceEqual(["a", "b c"]), $"{label}: PackedStringArray");
        var mainHand = (Resource)inventory.Get("main_hand");
        ExpectEqual((string)mainHand.Get("code"), "007", $"{label}: nested resource, a string that looks like a number");
        ExpectEqual((int)mainHand.Get("rarity"), Epic, $"{label}: enum");
        Expect(((Vector2)mainHand.Get("offset")).IsEqualApprox(new Vector2(0.5f, -2.0f)), $"{label}: Vector2");
        Expect(inventory.Get("spare").VariantType == Variant.Type.Nil, $"{label}: a missing resource stays null");
        var items = (Array)inventory.Get("items");
        ExpectEqual(items.Count, 2, $"{label}: typed array of resources");
        ExpectEqual((int)items[0].As<Resource>().Get("count"), 3, $"{label}: array element");
        ExpectEqual((string)items[1].As<Resource>().Get("name"), " ", $"{label}: a name that is a space");
        var scores = (Dictionary)inventory.Get("scores");
        Expect(scores.Count == 2 && (int)scores["ann"] == 10 && (int)scores["my key"] == 20, $"{label}: typed Dictionary, a key with a space");
    }

    private void TestFormats()
    {
        int[] formats = ResourceTextSerializer.GetAvailableFormats();
        Expect(formats.SequenceEqual([0, 1, 2, 3, 4, 5]), "GetAvailableFormats() lists all six formats");
        Expect(ResourceTextSerializer.IsFormatAvailable(ResourceTextSerializer.Format.Toml), "IsFormatAvailable(Toml)");
        Expect(ResourceTextSerializer.IsFormatAvailable(ResourceTextSerializer.Format.Yaml), "IsFormatAvailable(Yaml)");
        Expect(!ResourceTextSerializer.IsFormatAvailable(ResourceTextSerializer.Format.Max), "IsFormatAvailable(Max) is false");
        ExpectEqual(ResourceTextSerializer.GetFormatExtension(ResourceTextSerializer.Format.Json), "json", "GetFormatExtension(Json)");
        ExpectEqual(ResourceTextSerializer.GetFormatExtension(ResourceTextSerializer.Format.Ini), "ini", "GetFormatExtension(Ini)");
        ExpectEqual(ResourceTextSerializer.GetFormatExtension(ResourceTextSerializer.Format.Csv), "csv", "GetFormatExtension(Csv)");
        ExpectEqual(ResourceTextSerializer.GetFormatExtension(ResourceTextSerializer.Format.Xml), "xml", "GetFormatExtension(Xml)");
        ExpectEqual(ResourceTextSerializer.GetFormatExtension(ResourceTextSerializer.Format.Toml), "toml", "GetFormatExtension(Toml)");
        ExpectEqual(ResourceTextSerializer.GetFormatExtension(ResourceTextSerializer.Format.Yaml), "yaml", "GetFormatExtension(Yaml)");
        ExpectEqual(ResourceTextSerializer.GetFormatFromExtension(".YML"), ResourceTextSerializer.Format.Yaml, "GetFormatFromExtension(\".YML\") ignores case and dot");
        ExpectEqual(ResourceTextSerializer.GetFormatFromExtension("ini"), ResourceTextSerializer.Format.Ini, "GetFormatFromExtension(\"ini\")");
        ExpectEqual(ResourceTextSerializer.GetFormatFromExtension("tres"), ResourceTextSerializer.Format.Max, "GetFormatFromExtension() of an unknown extension is Max");
        ExpectEqual((int)ResourceTextSerializer.Format.Max, 6, "Format.Max is 6");
        ExpectEqual(ResourceTextSerializer.TextLayoutVersion, 1, "TextLayoutVersion is 1");
    }

    private void TestCompareFormats()
    {
        Resource sword = MakeSword();
        ExpectEqual(ResourceTextSerializer.Serialize(sword, ResourceTextSerializer.Format.Json), SwordJson, "JSON text of the sword");
        ExpectEqual(ResourceTextSerializer.Serialize(sword, ResourceTextSerializer.Format.Ini), SwordIni, "INI text of the sword");
        ExpectEqual(ResourceTextSerializer.Serialize(sword, ResourceTextSerializer.Format.Xml), SwordXml, "XML text of the sword");
        ExpectEqual(ResourceTextSerializer.Serialize(sword, ResourceTextSerializer.Format.Toml), SwordToml, "TOML text of the sword");
        ExpectEqual(ResourceTextSerializer.Serialize(sword, ResourceTextSerializer.Format.Yaml), SwordYaml, "YAML text of the sword");
        Resource potion = New("item.gd");
        potion.Set("name", "Potion");
        potion.Set("count", 3);
        Resource table = New("item_table.gd");
        var entries = (Array)table.Get("entries");
        entries.Add(sword);
        entries.Add(potion);
        ExpectEqual(ResourceTextSerializer.Serialize(table, ResourceTextSerializer.Format.Csv), TableCsv, "CSV text of the item table");
    }

    private void TestFiles()
    {
        Resource inventory = MakeInventory();
        foreach (string extension in new[] { "json", "ini", "xml", "toml", "yaml", "yml" })
        {
            string path = Root + "inventory." + extension;
            ResourceTextSerializer.Format format = ResourceTextSerializer.GetFormatFromExtension(extension);
            ExpectEqual(ResourceSaver.Save(inventory, path), Error.Ok, $".{extension}: ResourceSaver.Save()");
            string text = ResourceTextSerializer.Serialize(inventory, format);
            ExpectEqual(FileAccess.GetFileAsString(path), text, $".{extension}: the file text is Serialize()");
            ExpectEqual(ResourceLoader.GetResourceType(path), "Resource", $".{extension}: GetResourceType() reads the type marker (native base)");
            Resource loaded = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
            ExpectInventory(loaded, "." + extension);
        }
    }

    private void TestRoundTrip()
    {
        Resource inventory = MakeInventory();
        foreach (ResourceTextSerializer.Format format in ResourceFormats)
        {
            string text = ResourceTextSerializer.Serialize(inventory, format);
            Resource copy = ResourceTextSerializer.Deserialize(text, format);
            ExpectInventory(copy, $"Deserialize({ResourceTextSerializer.GetFormatExtension(format)})");
        }
    }

    private void TestVariantValues()
    {
        Resource inventory = New("inventory.gd");
        inventory.Set("notes", new Dictionary { ["lives"] = 3, ["ratio"] = 1.0, ["empty"] = new Dictionary(), ["none"] = default });
        inventory.Set("anything", new Array { 1, 2.5, "3", true, default });
        foreach (ResourceTextSerializer.Format format in new[] { ResourceTextSerializer.Format.Xml, ResourceTextSerializer.Format.Yaml })
        {
            string label = ResourceTextSerializer.GetFormatExtension(format);
            string text = ResourceTextSerializer.Serialize(inventory, format);
            Resource copy = ResourceTextSerializer.Deserialize(text, format);
            var notes = (Dictionary)copy.Get("notes");
            ExpectEqual(notes["lives"].VariantType, Variant.Type.Int, $"{label}: an int in an untyped Dictionary stays int");
            ExpectEqual(notes["ratio"].VariantType, Variant.Type.Float, $"{label}: a float without fraction stays float");
            ExpectEqual(((Dictionary)notes["empty"]).Count, 0, $"{label}: an empty Dictionary");
            Expect(notes.ContainsKey("none"), $"{label}: a null value keeps its key");
            var anything = (Array)copy.Get("anything");
            var types = anything.Select(value => value.VariantType);
            Expect(types.SequenceEqual([Variant.Type.Int, Variant.Type.Float, Variant.Type.String, Variant.Type.Bool, Variant.Type.Nil]), $"{label}: a Variant array keeps int, float, String, bool and null");
        }
    }

    private void TestCsvTable()
    {
        Resource table = New("item_table.gd");
        var entries = (Array)table.Get("entries");
        entries.Add(MakeSword());
        Expect(ResourceTextSerializer.CanSerialize(table, ResourceTextSerializer.Format.Csv), "CanSerialize(): a typed array of flat records fits CSV");
        Expect(!ResourceTextSerializer.CanSerialize(MakeInventory(), ResourceTextSerializer.Format.Csv), "CanSerialize(): nested data does not fit CSV");
        Expect(ResourceTextSerializer.CanSerialize(MakeInventory(), ResourceTextSerializer.Format.Json), "CanSerialize(): any resource fits JSON");
        string path = Root + "items.csv";
        ExpectEqual(ResourceSaver.Save(table, path), Error.Ok, "save an ItemTable as .csv");
        Resource loaded = ResourceLoader.Load(path, "ItemTable", ResourceLoader.CacheMode.Ignore);
        var loadedEntries = (Array)loaded.Get("entries");
        ExpectEqual((string)loadedEntries[0].As<Resource>().Get("code"), "007", ".csv loads as ItemTable with the type hint");
        Resource plain = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        Expect(plain is Csv, ".csv without a type hint loads as Csv");
    }

    private void TestTypeHint()
    {
        var options = new SerializerOptions { IncludeTypeMarker = false };
        foreach (ResourceTextSerializer.Format format in ResourceFormats)
        {
            string label = ResourceTextSerializer.GetFormatExtension(format);
            string text = ResourceTextSerializer.Serialize(MakeSword(), format, options);
            Expect(!text.Contains("Item"), $"{label}: IncludeTypeMarker = false writes no marker");
            Resource copy = ResourceTextSerializer.Deserialize(text, format, "Item");
            ExpectEqual((string)copy.Get("code"), "007", $"{label}: text without a marker reads with the type hint");
            string path = Root + "untyped." + label;
            using (var file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
            {
                file.StoreString(text);
            }
            Resource dataFile = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
            Expect(dataFile is Json or TextDataFormat, $"{label}: a file without a marker loads as the data file class");
        }
    }

    private void TestDictionaries()
    {
        Dictionary tree = ResourceTextSerializer.ResourceToDictionary(MakeSword());
        ExpectEqual((string)tree["$type"], "Item", "ResourceToDictionary(): type marker");
        ExpectEqual((string)tree["name"], "Sword", "ResourceToDictionary(): property");
        ExpectEqual((string)tree["rarity"], "Epic", "ResourceToDictionary(): enum by name");
        Resource copy = ResourceTextSerializer.DictionaryToResource(tree);
        ExpectEqual((string)copy.Get("code"), "007", "DictionaryToResource() restores the resource");
        var options = new SerializerOptions { IncludeTypeMarker = false };
        Dictionary untyped = ResourceTextSerializer.ResourceToDictionary(MakeSword(), options);
        Expect(!untyped.ContainsKey("$type"), "ResourceToDictionary() passes the options");
        Resource hinted = ResourceTextSerializer.DictionaryToResource(untyped, "Item");
        ExpectEqual((string)hinted.Get("name"), "Sword", "DictionaryToResource() with a type hint");
    }

    private void TestOptionDefaults()
    {
        var options = new SerializerOptions();
        ExpectEqual(options.Indent, "  ", "Indent defaults to two spaces");
        Expect(options.EnumsAsNames, "EnumsAsNames defaults to true");
        Expect(!options.SortKeys, "SortKeys defaults to false");
        Expect(!options.FullPrecisionFloats, "FullPrecisionFloats defaults to false");
        Expect(options.IncludeTypeMarker, "IncludeTypeMarker defaults to true");
        Expect(options.Defaults == null, "Defaults defaults to null");
        ExpectEqual(options.CsvDelimiter, ",", "CsvDelimiter defaults to ','");
        Expect(options.CsvHeaders, "CsvHeaders defaults to true");
        ExpectEqual(options.IniValueStyle, SerializerOptions.IniValueStyleEnum.GodotVariant, "IniValueStyle defaults to GodotVariant");
        Expect(options.XmlDeclaration, "XmlDeclaration defaults to true");
        ExpectEqual((int)SerializerOptions.IniValueStyleEnum.Plain, 0, "IniValueStyle.Plain is 0");
        ExpectEqual((int)SerializerOptions.IniValueStyleEnum.Max, 2, "IniValueStyle.Max is 2");
        string text = ResourceTextSerializer.Serialize(MakeSword(), ResourceTextSerializer.Format.Json, options);
        ExpectEqual(text, SwordJson, "new options give the same text as null (project settings)");
    }

    private void TestOptions()
    {
        Resource sword = MakeSword();
        var options = new SerializerOptions { Indent = "\t" };
        string tabs = ResourceTextSerializer.Serialize(sword, ResourceTextSerializer.Format.Json, options);
        Expect(tabs.Contains("\n\t\"name\""), "Indent");

        options = new SerializerOptions { EnumsAsNames = false };
        string numbers = ResourceTextSerializer.Serialize(sword, ResourceTextSerializer.Format.Json, options);
        Expect(numbers.Contains("\"rarity\": 3"), "EnumsAsNames = false writes numbers");
        Resource fromNumbers = ResourceTextSerializer.Deserialize(numbers, ResourceTextSerializer.Format.Json);
        ExpectEqual((int)fromNumbers.Get("rarity"), Epic, "enum numbers read back");

        options = new SerializerOptions { SortKeys = true, Indent = "" };
        string compact = ResourceTextSerializer.Serialize(sword, ResourceTextSerializer.Format.Json, options);
        ExpectEqual(compact, SwordJsonCompact, "SortKeys and an empty Indent write one sorted line");

        options = new SerializerOptions { Defaults = New("item.gd") };
        string changed = ResourceTextSerializer.Serialize(sword, ResourceTextSerializer.Format.Json, options);
        Expect(!changed.Contains("\"count\""), "Defaults: unchanged values are not written");
        Expect(changed.Contains("\"code\""), "Defaults: changed values are written");

        Resource precise = New("item.gd");
        precise.Set("offset", new Vector2(0.1f, 0.5f));
        options = new SerializerOptions { FullPrecisionFloats = true };
        string full = ResourceTextSerializer.Serialize(precise, ResourceTextSerializer.Format.Json, options);
        Expect(full.Contains("0.10000000149011612"), "FullPrecisionFloats writes every digit");
        string shortText = ResourceTextSerializer.Serialize(precise, ResourceTextSerializer.Format.Json);
        Expect(!shortText.Contains("0.10000000149011612"), "without FullPrecisionFloats the shortest text");

        Resource table = New("item_table.gd");
        var entries = (Array)table.Get("entries");
        entries.Add(sword);
        options = new SerializerOptions { CsvDelimiter = ";" };
        string semicolons = ResourceTextSerializer.Serialize(table, ResourceTextSerializer.Format.Csv, options);
        Expect(semicolons.Contains("\nSword;1;"), "CsvDelimiter");
        options.CsvHeaders = false;
        string rows = ResourceTextSerializer.Serialize(table, ResourceTextSerializer.Format.Csv, options);
        Expect(rows.StartsWith("Sword;"), "CsvHeaders = false writes no header");
        Resource fromRows = ResourceTextSerializer.Deserialize(rows, ResourceTextSerializer.Format.Csv, "ItemTable", options);
        var rowEntries = (Array)fromRows.Get("entries");
        ExpectEqual((string)rowEntries[0].As<Resource>().Get("code"), "007", "CsvHeaders = false reads back with the same options");

        options = new SerializerOptions { IniValueStyle = SerializerOptions.IniValueStyleEnum.Plain };
        string plain = ResourceTextSerializer.Serialize(sword, ResourceTextSerializer.Format.Ini, options);
        Expect(plain.Contains("name=Sword\n"), "IniValueStyle Plain writes strings without quotes");
        Expect(plain.Contains("code=\"007\""), "IniValueStyle Plain quotes a string that looks like a number");
        Resource fromPlain = ResourceTextSerializer.Deserialize(plain, ResourceTextSerializer.Format.Ini);
        Expect(((Vector2)fromPlain.Get("offset")).IsEqualApprox(new Vector2(0.5f, -2.0f)), "plain INI reads back");

        options = new SerializerOptions { XmlDeclaration = false };
        string bare = ResourceTextSerializer.Serialize(sword, ResourceTextSerializer.Format.Xml, options);
        Expect(bare.StartsWith("<resource"), "XmlDeclaration = false");
    }

    private void TestPreset()
    {
        var preset = GD.Load<SerializerOptions>(CompactOptions);
        string text = ResourceTextSerializer.Serialize(MakeSword(), ResourceTextSerializer.Format.Json, preset);
        ExpectEqual(text, SwordJsonCompact, "a SerializerOptions .tres works as a preset");
        string path = Root + "options.tres";
        ExpectEqual(ResourceSaver.Save(preset, path), Error.Ok, "save SerializerOptions as .tres");
        var again = (SerializerOptions)ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        Expect(again.SortKeys, "the saved preset keeps SortKeys");
    }

    private void TestProjectSettings()
    {
        ExpectEqual((string)ProjectSettings.GetSetting(Settings + "indent"), "  ", "project setting indent");
        ExpectEqual((bool)ProjectSettings.GetSetting(Settings + "enums_as_names"), true, "project setting enums_as_names");
        ExpectEqual((bool)ProjectSettings.GetSetting(Settings + "sort_keys"), false, "project setting sort_keys");
        ExpectEqual((bool)ProjectSettings.GetSetting(Settings + "full_precision_floats"), false, "project setting full_precision_floats");
        ExpectEqual((string)ProjectSettings.GetSetting(Settings + "csv_delimiter"), ",", "project setting csv_delimiter");
        ExpectEqual((bool)ProjectSettings.GetSetting(Settings + "csv_headers"), true, "project setting csv_headers");
        ExpectEqual((int)ProjectSettings.GetSetting(Settings + "ini_value_style"), (int)SerializerOptions.IniValueStyleEnum.GodotVariant, "project setting ini_value_style");
        ExpectEqual((bool)ProjectSettings.GetSetting(Settings + "xml_declaration"), true, "project setting xml_declaration");
        Expect(!ProjectSettings.HasSetting(Settings + "ini_plain_values"), "the old bool ini_plain_values is not registered");
    }

    private void TestTextDataFormat()
    {
        Expect(!ClassDB.CanInstantiate("TextDataFormat"), "TextDataFormat is abstract");
        Expect(ClassDB.IsParentClass("TextDataFormat", "Resource"), "TextDataFormat is a Resource");
        foreach (string formatClass in new[] { "INI", "CSV", "XML", "TOML", "YAML" })
        {
            Expect(ClassDB.IsParentClass(formatClass, "TextDataFormat"), $"{formatClass} is a TextDataFormat");
        }
        Expect(!ClassDB.IsParentClass("JSON", "TextDataFormat"), "JSON is Godot's own class");
        Expect(new Ini().GetRecognizedExtensions().SequenceEqual(["ini", "cfg"]), "Ini extensions (#392)");
        Expect(new Csv().GetRecognizedExtensions().SequenceEqual(["csv", "tsv"]), "Csv extensions (#392)");
        Expect(new Xml().GetRecognizedExtensions().SequenceEqual(["xml"]), "Xml extensions");
        Expect(new Toml().GetRecognizedExtensions().SequenceEqual(["toml"]), "Toml extensions");
        Expect(new Yaml().GetRecognizedExtensions().SequenceEqual(["yaml", "yml"]), "Yaml extensions");

        string text = "; comment\n[a]\nb=1\n";
        var ini = new Ini();
        ExpectEqual(ini.Parse(text, true), Error.Ok, "Parse() with keepText");
        ExpectEqual(ini.GetParsedText(), text, "GetParsedText() returns the kept text");
        ExpectEqual(ini.EncodeToText(), "[a]\nb=1\n", "EncodeToText() writes the data, not the kept text");
        string path = Root + "kept.ini";
        ExpectEqual(ini.Save(path), Error.Ok, "Save()");
        ExpectEqual(FileAccess.GetFileAsString(path), text, "Save() of unchanged data keeps the comment");
        ini.SetValue("a", "b", 2);
        ExpectEqual(ini.GetParsedText(), "", "a change of Data drops the kept text");
        Expect(!ini.EncodeToText().Contains("comment"), "EncodeToText() after a change has no comment");

        var loaded = new Ini();
        ExpectEqual(loaded.Load(path), Error.Ok, "Load()");
        ExpectEqual((int)loaded.GetValue("a", "b"), 1, "Load() reads the file");
        var options = new SerializerOptions();
        loaded.Options = options;
        Expect(loaded.Options == options, "Options");
        ExpectEqual(loaded.Parse("[broken"), Error.ParseError, "Parse() of broken text");
        ExpectEqual(loaded.GetErrorLine(), 1, "GetErrorLine()");
        ExpectEqual(loaded.GetErrorColumn(), 0, "GetErrorColumn(): INI reports no column");
        ExpectEqual(loaded.GetErrorMessage(), "Line 1: ']' expected.", "GetErrorMessage()");
        loaded.Data = new Dictionary { ["s"] = new Dictionary { ["k"] = 1 } };
        ExpectEqual(loaded.GetErrorMessage(), "", "setting Data clears the error");
        loaded.Clear();
        ExpectEqual(((Dictionary)loaded.Data).Count, 0, "Clear() empties Data");
        loaded.SetValue("s", "k", 1);
        ExpectEqual(loaded.Save(path), Error.Ok, "Save() of changed data");
        string saved = FileAccess.GetFileAsString(path);
        ExpectEqual(saved, "[s]\nk=1\n", "a data file saves as plain text without a type marker");
    }
}
