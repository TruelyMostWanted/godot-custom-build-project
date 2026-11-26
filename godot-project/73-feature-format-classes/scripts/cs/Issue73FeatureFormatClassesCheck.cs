using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue73FeatureFormatClasses;

/// <summary>C# checks of 73-feature-format-classes. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue73FeatureFormatClassesCheck : FeatureCheckCs
{
    private const string Dir = "res://73-feature-format-classes/scripts/gd/";
    private const string Title = "Ann & <Bob> \"the\" 2nd";

    protected override string CheckName => "73-feature-format-classes";

    protected override Task RunChecks()
    {
        CheckApi();
        CheckXml();
        CheckIni();
        CheckCsv();
        CheckSerialize();
        return Task.CompletedTask;
    }

    private static Resource New(string script) => (Resource)GD.Load<GDScript>(Dir + script).New();

    private static Resource MakeItem(string title, int level)
    {
        Resource item = New("item.gd");
        item.Set("title", title);
        item.Set("level", level);
        item.Set("offset", new Vector2(0.5f, -2.0f));
        return item;
    }

    private static bool SameItem(Variant a, Resource b)
    {
        var x = a.As<Resource>();
        return x != null && b != null && (string)x.Get("title") == (string)b.Get("title") && (int)x.Get("level") == (int)b.Get("level")
            && ((Vector2)x.Get("offset")).IsEqualApprox((Vector2)b.Get("offset"));
    }

    private void CheckApi()
    {
        foreach (string cls in new[] { "XML", "INI", "CSV" })
        {
            Expect(ClassDB.ClassExists(cls) && ClassDB.IsParentClass(cls, "Resource"), $"{cls} exists and is a Resource");
            foreach (string method in new[] { "parse", "get_data", "get_error_line", "get_error_message", "parse_string", "stringify", "serialize", "deserialize" })
            {
                Expect(ClassDB.ClassHasMethod(cls, method), $"{cls}.{method}()");
            }
        }
        Expect(ClassDB.ClassHasMethod("JSON", "serialize") && ClassDB.ClassHasMethod("JSON", "deserialize"), "Json.Serialize()/Deserialize()");
    }

    private void CheckXml()
    {
        const string text = "<?xml version=\"1.0\"?>\n<!-- comment -->\n<book id=\"7\" lang=\"en\">\n\t<title>Fish &amp; Chips</title>\n\t<p>Hello <b>big</b> world</p>\n\t<code><![CDATA[a < b]]></code>\n\t<empty/>\n</book>\n";
        var xml = new Xml();
        ExpectEqual(xml.Parse(text), Error.Ok, "Xml.Parse() of a document");
        Dictionary root = xml.GetData();
        ExpectEqual((string)root["name"], "book", "XML root name");
        var attributes = (Dictionary)root["attributes"];
        Expect(attributes.Count == 2 && (string)attributes["id"] == "7" && (string)attributes["lang"] == "en", "XML attributes");
        var children = (Array)root["children"];
        ExpectEqual(children.Count, 4, "XML indentation between elements is dropped");
        if (children.Count == 4)
        {
            ExpectEqual((string)((Dictionary)children[0])["text"], "Fish & Chips", "XML entities are decoded");
            var mixed = (Array)((Dictionary)children[1])["children"];
            Expect(mixed.Count == 3 && (string)mixed[0] == "Hello " && (string)((Dictionary)mixed[1])["name"] == "b" && (string)mixed[2] == " world", "XML mixed content keeps text and elements in order");
            ExpectEqual((string)((Dictionary)children[2])["text"], "a < b", "XML CDATA becomes text");
            var empty = (Dictionary)children[3];
            Expect(((Array)empty["children"]).Count == 0 && ((Dictionary)empty["attributes"]).Count == 0, "XML empty element");
        }

        Dictionary data = Xml.ParseString(text);
        foreach (string indent in new[] { "", "  " })
        {
            string written = Xml.Stringify(data, new SerializerOptions { Indent = indent });
            Expect(written.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>"), $"Xml.Stringify() writes the declaration (indent \"{indent}\")");
            Expect(Xml.ParseString(written).RecursiveEqual(data), $"XML stringify and parse again gives the same data (indent \"{indent}\")");
        }
        string bare = Xml.Stringify(data, new SerializerOptions { XmlDeclaration = false });
        Expect(bare.StartsWith("<book") && bare.Contains("<p>Hello <b>big</b> world</p>"), "XML mixed content stays on one line");

        ExpectEqual(xml.Parse("<a>\n<b>\n</a>"), Error.ParseError, "XML wrong end tag fails");
        ExpectEqual(xml.GetErrorLine(), 3, "XML error line of the wrong end tag");
        Expect(xml.GetErrorMessage().Contains("</a>"), "XML error message names the end tag");
        ExpectEqual(xml.Parse("<a>\n<b/>"), Error.ParseError, "XML unclosed element fails");
        ExpectEqual(xml.GetErrorLine(), 1, "XML error line of the unclosed element");
        ExpectEqual(xml.Parse("<a/>\n<b/>"), Error.ParseError, "XML second root fails");
        ExpectEqual(xml.GetErrorLine(), 2, "XML error line of the second root");
        Expect(xml.GetData().Count == 0, "XML GetData() is empty after an error");
        ExpectEqual(xml.Parse("<a/>\r\n"), Error.Ok, "XML trailing line break is no second root");
    }

    private void CheckIni()
    {
        var ini = new Ini();
        const string text = "; comment\ntop=1\n\n[player]\nname=\"Ann\"\npos=Vector2(1, 2)\nplain=just text\nflags=[1, 2]\n\n[player]\nlevel=3\n";
        ExpectEqual(ini.Parse(text), Error.Ok, "Ini.Parse()");
        Dictionary parsed = ini.GetData();
        ExpectEqual((int)((Dictionary)parsed[""])["top"], 1, "INI keys before the first section are in section \"\"");
        var player = (Dictionary)parsed["player"];
        Expect((string)player["name"] == "Ann" && (Vector2)player["pos"] == new Vector2(1, 2) && (string)player["plain"] == "just text"
            && ((Array)player["flags"]).RecursiveEqual(new Array { 1, 2 }), "INI typed and plain values");
        Expect((int)player["level"] == 3 && player.Count == 5, "INI repeated sections are merged");

        var data = new Dictionary
        {
            { "", new Dictionary { { "version", 2 } } },
            { "audio", new Dictionary { { "volume", 0.5 }, { "device", "Speakers (USB)" }, { "mute", false } } },
            { "video", new Dictionary { { "size", new Vector2I(1280, 720) } } },
        };
        string typed = Ini.Stringify(data);
        Expect(typed.StartsWith("version=2\n") && typed.Contains("[audio]\n") && typed.Contains("device=\"Speakers (USB)\""), "Ini.Stringify() typed values");
        Expect(Ini.ParseString(typed).RecursiveEqual(data), "INI typed stringify and parse again");
        string plainText = Ini.Stringify(data, new SerializerOptions { IniValueStyle = SerializerOptions.IniValueStyleEnum.Plain });
        Expect(plainText.Contains("device=Speakers (USB)") && plainText.Contains("mute=false"), "Ini.Stringify() plain values");
        Expect(Ini.ParseString(plainText).RecursiveEqual(data), "INI plain stringify and parse again");

        ExpectEqual(ini.Parse("a=1\n[broken\n"), Error.ParseError, "INI unclosed section header fails");
        ExpectEqual(ini.GetErrorLine(), 2, "INI error line of the section header");
        ExpectEqual(ini.Parse("[s]\nno value here\n"), Error.ParseError, "INI line without '=' fails");
        Expect(ini.GetErrorLine() == 2 && ini.GetErrorMessage().Length > 0, "INI error line and message of the key line");
    }

    private void CheckCsv()
    {
        var headers = new SerializerOptions { CsvHeaders = true };
        var csv = new Csv();
        const string text = "name,note\nAnn,\"a, b\"\r\nBob,\"line\nbreak \"\"quoted\"\"\"\nCleo\n";
        ExpectEqual(csv.Parse(text, headers), Error.Ok, "Csv.Parse() with headers");
        Array records = csv.GetData();
        ExpectEqual(records.Count, 3, "CSV records");
        if (records.Count == 3)
        {
            Expect((string)((Dictionary)records[0])["note"] == "a, b" && (string)((Dictionary)records[1])["note"] == "line\nbreak \"quoted\"", "CSV quoted cells");
            Expect((string)((Dictionary)records[2])["name"] == "Cleo" && (string)((Dictionary)records[2])["note"] == "", "CSV missing cells are empty");
        }

        var rowsOnly = new SerializerOptions { CsvHeaders = false, CsvDelimiter = ";" };
        Array rows = Csv.ParseString("007;x\n1;2\n", rowsOnly);
        Expect(rows.Count == 2 && rows[0].VariantType == Variant.Type.PackedStringArray && ((string[])rows[0])[0] == "007" && ((string[])rows[1])[1] == "2", "CSV rows without headers stay strings");

        var data = new Array
        {
            new Dictionary { { "name", "Ann" }, { "note", "a, \"b\"" } },
            new Dictionary { { "name", "Bob" }, { "level", 3 } },
        };
        string written = Csv.Stringify(data, headers);
        Expect(written.StartsWith("name,note,level\n") && written.Contains("Ann,\"a, \"\"b\"\"\",\n"), "Csv.Stringify() header and quoting");
        Array back = Csv.ParseString(written, headers);
        Expect(back.Count == 2 && (string)((Dictionary)back[0])["note"] == "a, \"b\"" && (string)((Dictionary)back[1])["level"] == "3", "CSV stringify and parse again");
        var tabs = new SerializerOptions { CsvHeaders = false, CsvDelimiter = "\t" };
        var table = new Array { new string[] { "a", "b c", "" }, new string[] { "1", "2", "3" } };
        string tabbed = Csv.Stringify(table, tabs);
        ExpectEqual(tabbed, "a\tb c\t\n1\t2\t3\n", "Csv.Stringify() rows with tab delimiter");
        Expect(Csv.ParseString(tabbed, tabs).RecursiveEqual(table), "CSV rows stringify and parse again");

        ExpectEqual(csv.Parse("a,b\n1,2\n3,\"open\n", headers), Error.ParseError, "CSV unclosed quote fails");
        ExpectEqual(csv.GetErrorLine(), 3, "CSV error line of the unclosed quote");
        ExpectEqual(csv.Parse("a,b\n1,2,3\n", headers), Error.ParseError, "CSV too many cells fails");
        Expect(csv.GetErrorLine() == 2 && csv.GetErrorMessage().Length > 0, "CSV error line and message of the long row");
    }

    private void CheckSerialize()
    {
        Resource item = MakeItem(Title, 3);
        ExpectEqual(Json.Serialize(item), ResourceTextSerializer.Serialize(item, ResourceTextSerializer.Format.Json), "Json.Serialize() equals ResourceTextSerializer");
        ExpectEqual(Xml.Serialize(item), ResourceTextSerializer.Serialize(item, ResourceTextSerializer.Format.Xml), "Xml.Serialize() equals ResourceTextSerializer");
        ExpectEqual(Ini.Serialize(item), ResourceTextSerializer.Serialize(item, ResourceTextSerializer.Format.Ini), "Ini.Serialize() equals ResourceTextSerializer");
        var options = new SerializerOptions { Indent = "", SortKeys = true };
        ExpectEqual(Json.Serialize(item, options), ResourceTextSerializer.Serialize(item, ResourceTextSerializer.Format.Json, options), "Json.Serialize() passes the options");
        ExpectEqual(Xml.Serialize(item, options), ResourceTextSerializer.Serialize(item, ResourceTextSerializer.Format.Xml, options), "Xml.Serialize() passes the options");

        Expect(SameItem(Json.Deserialize(Json.Serialize(item)), item), "Json.Deserialize() restores the item");
        Expect(SameItem(Xml.Deserialize(Xml.Serialize(item)), item), "Xml.Deserialize() restores the item");
        Expect(SameItem(Ini.Deserialize(Ini.Serialize(item)), item), "Ini.Deserialize() restores the item");
        Expect(SameItem(Xml.Deserialize(Xml.Serialize(item), "Issue73GdItem"), item), "Xml.Deserialize() with a type hint");

        Resource list = New("item_list.gd");
        var items = (Array)list.Get("items");
        items.Add(MakeItem("Ann", 3));
        items.Add(MakeItem("Bob, \"the\" 2nd", 7));
        string csvText = Csv.Serialize(list);
        ExpectEqual(csvText, ResourceTextSerializer.Serialize(list, ResourceTextSerializer.Format.Csv), "Csv.Serialize() equals ResourceTextSerializer");
        Resource loaded = Csv.Deserialize(csvText, "Issue73GdItemList");
        var loadedItems = loaded != null ? (Array)loaded.Get("items") : new Array();
        Expect(loadedItems.Count == 2 && SameItem(loadedItems[1], MakeItem("Bob, \"the\" 2nd", 7)), "Csv.Deserialize() restores the list");
    }
}
