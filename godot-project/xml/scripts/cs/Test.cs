using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.XmlFormat;

/// <summary>Full API test of XML: every method of Xml, XmlElement and XmlWriter, and the XML text of a resource. Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string Root = "user://xml/cs/";
    private const string Level = "res://xml/resources/level.xml";
    private const string Scripts = "res://text-serialization/scripts/gd/";
    private const string Book = "<?xml version=\"1.0\"?>\n<!-- comment -->\n<book id=\"7\" lang=\"en\">\n\t<title>Fish &amp; Chips</title>\n\t<p>Hello <b>big</b> world</p>\n\t<code><![CDATA[a < b]]></code>\n\t<empty/>\n</book>\n";
    private const string Items = "<items version=\"2\"><item id=\"a\">Sword</item><group><item id=\"b\">Shield</item></group></items>";
    private const string Written = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!-- levels -->\n<level name=\"A &amp; &quot;B&quot;\">\n  <music>forest.ogg</music>\n  <note>x &lt; y<![CDATA[a < b]]></note>\n  <empty/>\n</level>\n";

    protected override Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        TestDataFile();
        TestParse();
        TestParseErrors();
        TestFind();
        TestElement();
        TestCreate();
        TestStringify();
        TestSerialize();
        TestWriter();
        TestWriterErrors();
        return Task.CompletedTask;
    }

    private static Resource New(string script)
    {
        var gdScript = GD.Load<GDScript>(Scripts + script);
        return (Resource)gdScript.New();
    }

    private void TestDataFile()
    {
        var level = GD.Load<Xml>(Level);
        Expect(level != null, "Load() of a plain .xml returns an Xml");
        ExpectEqual(ResourceLoader.GetResourceType(Level), "XML", "GetResourceType() of a plain .xml");
        Dictionary data = level.Data.AsGodotDictionary();
        Expect(data.Keys.Select(key => (string)key).SequenceEqual(["name", "attributes", "children", "text"]), "Data is the root element: name, attributes, children, text");
        Dictionary attributes = data["attributes"].AsGodotDictionary();
        Expect(attributes.Count == 2 && (string)attributes["name"] == "Forest" && (string)attributes["time"] == "120", "attribute values are Strings");
        ExpectEqual(data["children"].AsGodotArray().Count, 3, "the comment and the indentation are not part of Data");
    }

    private void TestParse()
    {
        var xml = new Xml();
        ExpectEqual(xml.Parse(Book), Error.Ok, "Parse() of a document");
        Array children = xml.Data.AsGodotDictionary()["children"].AsGodotArray();
        ExpectEqual(children.Count, 4, "indentation between elements is dropped");
        ExpectEqual((string)children[0].AsGodotDictionary()["text"], "Fish & Chips", "entities are decoded");
        Array mixed = children[1].AsGodotDictionary()["children"].AsGodotArray();
        ExpectEqual((string)mixed[0], "Hello ", "mixed content: text first");
        ExpectEqual((string)mixed[1].AsGodotDictionary()["name"], "b", "mixed content: the element in the middle");
        ExpectEqual((string)mixed[2], " world", "mixed content: text last");
        ExpectEqual((string)children[2].AsGodotDictionary()["text"], "a < b", "CDATA becomes text");
        Dictionary empty = children[3].AsGodotDictionary();
        ExpectEqual(empty["children"].AsGodotArray().Count, 0, "an empty element has no children");
        ExpectEqual(empty["attributes"].AsGodotDictionary().Count, 0, "an empty element has no attributes");
        Dictionary data = Xml.ParseString("<a x=\"1\"/>");
        Expect((string)data["name"] == "a" && (string)data["attributes"].AsGodotDictionary()["x"] == "1" && (string)data["text"] == "", "ParseString() returns the root element");
    }

    private void TestParseErrors()
    {
        var xml = new Xml();
        ExpectEqual(xml.Parse("<a>\n<b>\n</a>"), Error.ParseError, "a wrong end tag fails");
        ExpectEqual(xml.GetErrorLine(), 3, "error line of the wrong end tag");
        Expect(xml.GetErrorMessage().Contains("</a>"), "the error message names the end tag");
        ExpectEqual(xml.Parse("<a>\n<b/>"), Error.ParseError, "an unclosed element fails");
        ExpectEqual(xml.GetErrorLine(), 1, "error line of the unclosed element");
        ExpectEqual(xml.Parse("<a/>\n<b/>"), Error.ParseError, "a second root fails");
        ExpectEqual(xml.GetErrorLine(), 2, "error line of the second root");
        ExpectEqual(xml.Data.AsGodotDictionary().Count, 0, "Data is empty after an error");
        ExpectEqual(xml.Parse("<a/>\r\n"), Error.Ok, "a trailing line break is no second root");
    }

    private void TestFind()
    {
        var xml = new Xml();
        xml.Parse(Items);
        XmlElement root = xml.GetRoot();
        ExpectEqual(root.Name, "items", "GetRoot()");
        ExpectEqual(root.GetAttribute("version"), "2", "GetAttribute()");
        ExpectEqual(root.GetAttribute("missing", "none"), "none", "GetAttribute() returns the default");
        ExpectEqual(root.GetChildren().Count, 2, "GetChildren()");
        XmlElement shield = xml.FindFirst("group/item");
        ExpectEqual(shield.Text, "Shield", "FindFirst() with a path");
        Expect(xml.FindFirst("missing") == null, "FindFirst() of a missing path is null");
        ExpectEqual(xml.FindAll("item").Count, 1, "FindAll() finds direct children of the root");
        XmlElement group = root.FindFirst("group");
        ExpectEqual(group.FindAll("item").Count, 1, "XmlElement.FindAll()");
        ExpectEqual(root.GetChildrenByName("group").Count, 1, "GetChildrenByName()");
    }

    private void TestElement()
    {
        var xml = new Xml();
        xml.Parse(Items);
        XmlElement root = xml.GetRoot();
        XmlElement sword = root.FindFirst("item");
        sword.SetAttribute("id", "c");
        Expect(xml.EncodeToText().Contains("<item id=\"c\">Sword</item>"), "SetAttribute() writes through to the Xml resource");
        Expect(sword.HasAttribute("id"), "HasAttribute()");
        sword.RemoveAttribute("id");
        Expect(!sword.HasAttribute("id"), "RemoveAttribute()");
        Dictionary attributes = root.GetAttributes();
        Expect(attributes.Count == 1 && (string)attributes["version"] == "2", "GetAttributes()");
        sword.Text = "Axe";
        sword.Name = "weapon";
        Expect(xml.EncodeToText().Contains("<weapon>Axe</weapon>"), "Name and Text write through");
        var bow = new XmlElement { Name = "item", Text = "Bow" };
        root.AddChild(bow);
        ExpectEqual(root.GetChildren().Count, 3, "AddChild()");
        root.RemoveChild(sword);
        ExpectEqual(root.GetChildren().Count, 2, "RemoveChild()");
        Expect(!xml.EncodeToText().Contains("weapon"), "RemoveChild() writes through");
    }

    private void TestCreate()
    {
        var created = new Xml();
        XmlElement root = created.CreateRoot("level");
        root.SetAttribute("name", "Custom");
        var spawn = new XmlElement { Name = "spawn" };
        spawn.SetAttribute("enemy", "slime");
        root.AddChild(spawn);
        string path = Root + "custom_level.xml";
        ExpectEqual(created.Save(path), Error.Ok, "Save() of a created document");
        ExpectEqual(FileAccess.GetFileAsString(path), "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<level name=\"Custom\">\n  <spawn enemy=\"slime\"/>\n</level>\n", "the text of the wiki guide");
    }

    private void TestStringify()
    {
        Dictionary data = Xml.ParseString(Book);
        string written = Xml.Stringify(data);
        Expect(written.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>"), "Stringify() writes the declaration");
        ExpectEqual(Xml.Stringify(Xml.ParseString(written)), written, "Stringify() reads back");
        var compact = new SerializerOptions { Indent = "" };
        string oneLine = Xml.Stringify(data, compact);
        ExpectEqual(Xml.Stringify(Xml.ParseString(oneLine)), written, "Stringify() without indent reads back");
        var bare = new SerializerOptions { XmlDeclaration = false };
        string without = Xml.Stringify(data, bare);
        Expect(without.StartsWith("<book"), "XmlDeclaration = false leaves out the first line");
        Expect(without.Contains("<p>Hello <b>big</b> world</p>"), "mixed content stays on one line");
    }

    private void TestSerialize()
    {
        Resource sword = New("item.gd");
        sword.Set("name", "Sword");
        sword.Set("code", "007");
        string text = Xml.Serialize(sword);
        ExpectEqual(text, ResourceTextSerializer.Serialize(sword, ResourceTextSerializer.Format.Xml), "Serialize() is ResourceTextSerializer.Serialize(Xml)");
        Expect(text.Contains("<resource type=\"Item\" format_version=\"1\">"), "the root element carries the type");
        Expect(text.Contains("<code kind=\"string\">007</code>"), "kind=\"string\" for a String that would read back as a number");
        Resource copy = Xml.Deserialize(text);
        ExpectEqual((string)copy.Get("code"), "007", "Deserialize() restores the resource");

        Resource inventory = New("inventory.gd");
        var scores = (Dictionary)inventory.Get("scores");
        scores["my key"] = 20;
        var items = (Array)inventory.Get("items");
        items.Add(sword);
        string nested = Xml.Serialize(inventory);
        Expect(nested.Contains("<entry key=\"my key\">20</entry>"), "a key that is no valid tag becomes <entry key=...>");
        Expect(nested.Contains("<spare kind=\"null\"/>"), "null is kind=\"null\"");
        Expect(nested.Contains("<items kind=\"array\">\n    <item>"), "an array is kind=\"array\" with one <item> per element");

        var options = new SerializerOptions { Indent = "", EnumsAsNames = false };
        string oneLine = Xml.Serialize(sword, options);
        Expect(!oneLine.Contains('\n'), "Indent = \"\" writes one line");
        Expect(oneLine.Contains("<rarity>0</rarity>"), "EnumsAsNames = false writes numbers");
        options = new SerializerOptions { XmlDeclaration = false, IncludeTypeMarker = false };
        string bare = Xml.Serialize(sword, options);
        Expect(bare.StartsWith("<resource format_version=\"1\">"), "no declaration and no type");
        Resource hinted = Xml.Deserialize(bare, "Item");
        ExpectEqual((string)hinted.Get("code"), "007", "Deserialize() with a type hint");

        string path = Root + "sword.xml";
        ExpectEqual(ResourceSaver.Save(sword, path), Error.Ok, "ResourceSaver.Save() to .xml");
        ExpectEqual(ResourceLoader.GetResourceType(path), "Resource", "GetResourceType() reads the type (native base of Item)");
        Resource loaded = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        ExpectEqual((string)loaded.Get("code"), "007", "an .xml with a type loads as its class");
    }

    private void TestWriter()
    {
        var writer = new XmlWriter();
        ExpectEqual(writer.Indent, "  ", "Indent defaults to two spaces");
        ExpectEqual(writer.WriteDeclaration(), Error.Ok, "WriteDeclaration()");
        ExpectEqual(writer.WriteComment(" levels "), Error.Ok, "WriteComment()");
        ExpectEqual(writer.BeginElement("level"), Error.Ok, "BeginElement()");
        ExpectEqual(writer.GetDepth(), 1, "GetDepth() after BeginElement()");
        ExpectEqual(writer.AddAttribute("name", "A & \"B\""), Error.Ok, "AddAttribute()");
        ExpectEqual(writer.WriteElement("music", "forest.ogg"), Error.Ok, "WriteElement() with text");
        writer.BeginElement("note");
        ExpectEqual(writer.WriteText("x < y"), Error.Ok, "WriteText()");
        ExpectEqual(writer.WriteCdata("a < b"), Error.Ok, "WriteCdata()");
        ExpectEqual(writer.EndElement(), Error.Ok, "EndElement()");
        ExpectEqual(writer.WriteElement("empty"), Error.Ok, "WriteElement() without text");
        ExpectEqual(writer.EndAll(), Error.Ok, "EndAll()");
        ExpectEqual(writer.GetDepth(), 0, "GetDepth() after EndAll()");
        ExpectEqual(writer.GetOutput(), Written, "GetOutput()");
        Expect(writer.GetOutputBuffer().SequenceEqual(Written.ToUtf8Buffer()), "GetOutputBuffer() is the UTF-8 output");
        string path = Root + "written.xml";
        ExpectEqual(writer.Save(path), Error.Ok, "Save()");
        ExpectEqual(FileAccess.GetFileAsString(path), Written, "Save() writes the output");
        writer.Clear();
        ExpectEqual(writer.GetOutput(), "", "Clear()");
        writer.Indent = "";
        writer.BeginElement("a");
        writer.WriteElement("b", "1");
        writer.EndAll();
        ExpectEqual(writer.GetOutput(), "<a><b>1</b></a>", "Indent = \"\" writes one line");
        Expect(XmlWriter.IsValidName("ok_1"), "IsValidName() of a valid name");
        Expect(!XmlWriter.IsValidName("1bad"), "IsValidName() of a name that starts with a digit");
        ExpectEqual(XmlWriter.EscapeText("a<b&c>\"'"), "a&lt;b&amp;c&gt;\"'", "EscapeText()");
        ExpectEqual(XmlWriter.EscapeAttribute("a<b&c>\"'\n"), "a&lt;b&amp;c&gt;&quot;&apos;&#10;", "EscapeAttribute()");
    }

    private void TestWriterErrors()
    {
        var writer = new XmlWriter();
        ExpectEqual(writer.EndElement(), Error.DoesNotExist, "EndElement() without an open element");
        ExpectEqual(writer.BeginElement("1bad"), Error.InvalidParameter, "BeginElement() of an invalid name");
        writer.BeginElement("a");
        writer.WriteText("text");
        ExpectEqual(writer.AddAttribute("x", "1"), Error.Unconfigured, "AddAttribute() after content");
        ExpectEqual(writer.WriteComment("a -- b"), Error.InvalidParameter, "WriteComment() with \"--\"");
        ExpectEqual(writer.WriteCdata("x ]]> y"), Error.Ok, "WriteCdata() splits \"]]>\"");
        Expect(writer.GetOutput().EndsWith("<![CDATA[x ]]]]><![CDATA[> y]]>"), "the split CDATA sections");
    }
}
