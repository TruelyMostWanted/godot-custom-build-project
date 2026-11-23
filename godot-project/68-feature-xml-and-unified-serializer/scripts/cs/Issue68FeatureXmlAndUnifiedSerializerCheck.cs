using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue68FeatureXmlAndUnifiedSerializer;

/// <summary>C# checks of 68-feature-xml-and-unified-serializer. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue68FeatureXmlAndUnifiedSerializerCheck : FeatureCheckCs
{
    private const string Root = "user://68-feature-xml-and-unified-serializer/cs/";
    private const string Dir = "res://68-feature-xml-and-unified-serializer/scripts/gd/";
    private const string Owner = "Ada & <Bob> \"the\" 2nd\nline two";

    protected override string CheckName => "68-feature-xml-and-unified-serializer";

    protected override Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        CheckApi();
        CheckXmlText();
        CheckXmlRoundTrip();
        CheckXmlFiles();
        CheckSameTextAsFiles();
        Cleanup();
        return Task.CompletedTask;
    }

    private static Resource New(string script) => (Resource)GD.Load<GDScript>(Dir + script).New();

    private static Resource MakeItem(string name, int count)
    {
        Resource item = New("item.gd");
        item.Set("name", name);
        item.Set("count", count);
        item.Set("weight", 0.5);
        item.Set("rarity", 5);
        item.Set("offset", new Vector2(0.5f, -2.0f));
        item.Set("code", "007");
        return item;
    }

    private static Resource MakeInventory()
    {
        Resource inv = New("inventory.gd");
        inv.Set("owner_name", Owner);
        inv.Set("gold", 250);
        inv.Set("tint", new Color(1, 0, 0, 1));
        inv.Set("main_hand", MakeItem("Sword", 1));
        var items = (Array)inv.Get("items");
        items.Add(MakeItem("Potion", 3));
        items.Add(MakeItem(" ", 2));
        var scores = (Dictionary)inv.Get("scores");
        scores["ann"] = 10;
        scores["my key"] = 20;
        inv.Set("notes", new Dictionary { { "lives", 3 }, { "ratio", 1.0 }, { "empty", new Dictionary() }, { "none", default } });
        inv.Set("anything", new Array { 1, 2.5, "3", true, default });
        return inv;
    }

    private static bool SameItem(Variant a, Variant b)
    {
        var x = a.As<Resource>();
        var y = b.As<Resource>();
        return x != null && y != null && (string)x.Get("name") == (string)y.Get("name") && (int)x.Get("count") == (int)y.Get("count")
            && Mathf.IsEqualApprox((double)x.Get("weight"), (double)y.Get("weight")) && (int)x.Get("rarity") == (int)y.Get("rarity")
            && ((Vector2)x.Get("offset")).IsEqualApprox((Vector2)y.Get("offset")) && (string)x.Get("code") == (string)y.Get("code");
    }

    private static bool IsScript(Resource resource, string script) => resource != null && resource.GetScript().As<Script>()?.ResourcePath == Dir + script;

    private void CheckApi()
    {
        string[] formats = ResourceTextSerializer.GetFormats();
        foreach (string f in new[] { "json", "ini", "csv", "xml" })
        {
            Expect(System.Array.IndexOf(formats, f) >= 0, $"GetFormats() lists {f}");
        }
        Expect((System.Array.IndexOf(formats, "toml") >= 0) == ClassDB.ClassExists("TOML"), "GetFormats() lists toml exactly when the TOML module is built");
        Expect(ResourceTextSerializer.HasFormat("xml") && ResourceTextSerializer.HasFormat(".JSON") && !ResourceTextSerializer.HasFormat("yaml"), "HasFormat()");
        Expect(!ClassDB.ClassHasMethod("ResourceTextSerializer", "resource_to_json") && !ClassDB.ClassHasMethod("ResourceTextSerializer", "resource_to_xml"), "per-format methods are replaced by Serialize()/Deserialize()");
        if (ClassDB.ClassExists("TOML"))
        {
            Expect(ClassDB.ClassHasMethod("TOML", "serialize") && ClassDB.ClassHasMethod("TOML", "deserialize"), "TOML.Serialize()/Deserialize() exist");
        }
    }

    private void CheckXmlText()
    {
        string text = ResourceTextSerializer.Serialize(MakeItem("Sword", 1), "xml");
        string expected = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + "<resource type=\"Issue68GdItem\" format_version=\"1\">\n"
            + "  <name>Sword</name>\n"
            + "  <count>1</count>\n"
            + "  <weight>0.5</weight>\n"
            + "  <rarity>Epic</rarity>\n"
            + "  <offset kind=\"array\">0.5, -2.0</offset>\n"
            + "  <code kind=\"string\">007</code>\n"
            + "</resource>\n";
        ExpectEqual(text, expected, "XML text of a flat resource");
        string oneLine = ResourceTextSerializer.Serialize(MakeItem("Sword", 1), "xml", new Dictionary { { "indent", "" }, { "enums_as_names", false } });
        Expect(!oneLine.Contains('\n') && oneLine.Contains("<rarity>5</rarity>"), "XML options indent and enums_as_names");
    }

    private void CheckInventory(Resource loaded, string label)
    {
        Resource e = MakeInventory();
        bool ok = IsScript(loaded, "inventory.gd");
        Expect(ok, $"{label}: loads as Issue68GdInventory");
        if (!ok)
        {
            return;
        }
        Expect((string)loaded.Get("owner_name") == Owner && (int)loaded.Get("gold") == 250 && ((Color)loaded.Get("tint")).IsEqualApprox(new Color(1, 0, 0, 1)), $"{label}: string with & < > quotes and line break, int, Color");
        Expect(SameItem(loaded.Get("main_hand"), e.Get("main_hand")), $"{label}: nested resource");
        var li = (Array)loaded.Get("items");
        var ei = (Array)e.Get("items");
        Expect(li.Count == 2 && SameItem(li[0], ei[0]) && SameItem(li[1], ei[1]), $"{label}: typed array of resources (one name is a space)");
        var scores = (Dictionary)loaded.Get("scores");
        Expect(scores.Count == 2 && (int)scores["ann"] == 10 && (int)scores["my key"] == 20, $"{label}: Dictionary (typed in GDScript check), key that is not an XML name");
        var n = (Dictionary)loaded.Get("notes");
        Expect(n.ContainsKey("lives") && n["lives"].VariantType == Variant.Type.Int && n.ContainsKey("ratio") && n["ratio"].VariantType == Variant.Type.Float
            && n.ContainsKey("empty") && n["empty"].VariantType == Variant.Type.Dictionary && n.ContainsKey("none") && n["none"].VariantType == Variant.Type.Nil,
            $"{label}: untyped Dictionary keeps int, float, {{}} and null");
        Variant anything = loaded.Get("anything");
        var a = anything.VariantType == Variant.Type.Array ? (Array)anything : new Array();
        Expect(a.Count == 5 && a[0].VariantType == Variant.Type.Int && a[1].VariantType == Variant.Type.Float && a[2].VariantType == Variant.Type.String
            && a[3].VariantType == Variant.Type.Bool && a[4].VariantType == Variant.Type.Nil, $"{label}: Variant array keeps int, float, String, bool, null");
    }

    private void CheckXmlRoundTrip()
    {
        string text = ResourceTextSerializer.Serialize(MakeInventory(), "xml");
        Expect(text.Contains("<resource type=\"Issue68GdInventory\" format_version=\"1\">"), "XML root element with type marker");
        Expect(text.Contains("<entry key=\"my key\">20</entry>") && text.Contains("<none kind=\"null\"/>"), "XML entry key and null");
        CheckInventory(ResourceTextSerializer.Deserialize(text, "xml"), "deserialize xml");
        CheckInventory(ResourceTextSerializer.Deserialize(text, "xml", "Issue68GdInventory"), "deserialize xml with type hint");
    }

    private void CheckXmlFiles()
    {
        string path = Root + "inventory.xml";
        ExpectEqual(ResourceSaver.Save(MakeInventory(), path), Error.Ok, "save as .xml");
        ExpectEqual(ResourceLoader.GetResourceType(path), "Resource", "GetResourceType() of an .xml resource (native base of the script class)");
        CheckInventory(ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore), ".xml file");

        string other = Root + "other.xml";
        var f = FileAccess.Open(other, FileAccess.ModeFlags.Write);
        f.StoreString("<?xml version=\"1.0\"?>\n<config><volume>3</volume></config>\n");
        f.Close();
        ExpectEqual(ResourceLoader.GetResourceType(other), "", "XML without the type marker is not a resource file");
    }

    private void CheckSameTextAsFiles()
    {
        Resource inv = MakeInventory();
        foreach (string ext in new[] { "json", "ini", "xml", "toml" })
        {
            if (!ResourceTextSerializer.HasFormat(ext))
            {
                continue;
            }
            string path = Root + "same." + ext;
            ExpectEqual(ResourceSaver.Save(inv, path), Error.Ok, $"save as .{ext}");
            ExpectEqual(ResourceTextSerializer.Serialize(inv, ext), FileAccess.GetFileAsString(path), $"Serialize(res, \"{ext}\") gives the file text");
        }

        Resource list = New("item_list.gd");
        var entries = (Array)list.Get("entries");
        entries.Add(MakeItem("Potion", 3));
        entries.Add(MakeItem("Bread, \"fresh\"", 2));
        ExpectEqual(ResourceSaver.Save(list, Root + "same.csv"), Error.Ok, "save as .csv");
        ExpectEqual(ResourceTextSerializer.Serialize(list, "csv"), FileAccess.GetFileAsString(Root + "same.csv"), "Serialize(res, \"csv\") gives the file text");
        string semicolons = ResourceTextSerializer.Serialize(list, "csv", new Dictionary { { "delimiter", ";" } });
        Resource back = ResourceTextSerializer.Deserialize(semicolons, "csv", "Issue68GdItemList", new Dictionary { { "delimiter", ";" } });
        Expect(IsScript(back, "item_list.gd") && ((Array)back.Get("entries")).Count == 2 && SameItem(((Array)back.Get("entries"))[1], entries[1]), "csv with type hint and delimiter option");
    }

    private static void Cleanup()
    {
        foreach (string file in DirAccess.GetFilesAt(Root))
        {
            DirAccess.RemoveAbsolute(Root + file);
        }
    }
}
