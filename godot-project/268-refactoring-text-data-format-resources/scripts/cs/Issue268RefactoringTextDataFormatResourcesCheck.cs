using System.Threading.Tasks;
using System.Xml; // Imported on purpose: XmlElement would be ambiguous, so the checks use Godot.XmlElement.
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue268RefactoringTextDataFormatResources;

/// <summary>C# checks of 268-refactoring-text-data-format-resources. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue268RefactoringTextDataFormatResourcesCheck : FeatureCheckCs
{
    private const string Res = "res://268-refactoring-text-data-format-resources/resources/";
    private const string Root = "user://268-refactoring-text-data-format-resources/cs/";

    protected override string CheckName => "268-refactoring-text-data-format-resources";

    protected override Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        CheckApi();
        CheckPlainFiles();
        CheckSavePlain();
        CheckIni();
        CheckIniValueStyle();
        CheckCsv();
        CheckXml();
        CheckToml();
        CheckYaml();
        CheckYamlDocuments();
        CheckErrors();
        CheckIniValueStyleSetting();
        return Task.CompletedTask;
    }

    private void CheckApi()
    {
        foreach (string cls in new[] { "INI", "CSV", "XML", "TOML", "YAML" })
        {
            Expect(ClassDB.ClassExists(cls) && ClassDB.IsParentClass(cls, "TextDataFormat"), $"{cls} is a TextDataFormat");
        }
        Expect(ClassDB.IsParentClass("TextDataFormat", "Resource"), "TextDataFormat is a Resource");
        Expect(!ClassDB.CanInstantiate("TextDataFormat"), "TextDataFormat is abstract");
        Expect(ClassDB.IsParentClass("XMLElement", "RefCounted"), "XMLElement is RefCounted");
        foreach (string method in new[] { "parse", "encode_to_text", "clear", "load", "save", "get_parsed_text", "get_error_message", "get_error_line", "get_error_column", "get_recognized_extensions" })
        {
            Expect(ClassDB.ClassHasMethod("TextDataFormat", method), $"TextDataFormat.{method}()");
        }
        Expect(string.Join(",", new Ini().GetRecognizedExtensions()) == "ini,cfg", "INI extensions: ini and cfg (#392)");
        string[] yamlExtensions = new Yaml().GetRecognizedExtensions();
        Expect(yamlExtensions.Length == 2 && yamlExtensions[0] == "yaml" && yamlExtensions[1] == "yml", "YAML extensions include yml");
    }

    private void CheckPlainFiles()
    {
        var ini = GD.Load<Ini>(Res + "sample.ini");
        Expect(ini != null, "GD.Load<Ini>() of a plain .ini");
        if (ini != null)
        {
            ExpectEqual((int)ini.GetValue("", "version"), 2, "plain INI value outside a section");
            ExpectEqual((Vector2I)ini.GetValue("video", "size"), new Vector2I(1280, 720), "plain INI Godot syntax value");
            ExpectEqual((string)ini.GetValue("video", "title"), "Demo", "plain INI string");
            ExpectEqual((float)ini.GetValue("audio", "volume"), 0.5f, "plain INI float");
            ExpectEqual(ini.GetSections().Length, 3, "plain INI sections");
        }

        var csv = GD.Load<Csv>(Res + "sample.csv");
        Expect(csv != null, "GD.Load<Csv>() of a kept .csv");
        if (csv != null)
        {
            ExpectEqual(csv.GetRowCount(), 3, "plain CSV row count");
            ExpectEqual(csv.GetCell(0, 1), "Ann", "plain CSV cell");
        }

        var xml = GD.Load<Xml>(Res + "sample.xml");
        Expect(xml != null, "GD.Load<Xml>() of a plain .xml");
        if (xml != null)
        {
            ExpectEqual(xml.GetRoot().Name, "items", "plain XML root name");
        }

        var toml = GD.Load<Toml>(Res + "sample.toml");
        Expect(toml != null, "GD.Load<Toml>() of a plain .toml");
        if (toml != null)
        {
            ExpectEqual((string)toml.GetValue("", "title"), "Demo", "plain TOML root key");
            ExpectEqual((int)toml.GetValue("server", "port"), 8080, "plain TOML table key");
        }

        var yaml = GD.Load<Yaml>(Res + "sample.yaml");
        Expect(yaml != null, "GD.Load<Yaml>() of a plain .yaml");
        if (yaml != null)
        {
            ExpectEqual((string)yaml.GetValue("name"), "Demo", "plain YAML value");
        }
        var yml = GD.Load<Yaml>(Res + "sample.yml");
        Expect(yml != null, "GD.Load<Yaml>() of a plain .yml");
        if (yml != null)
        {
            ExpectEqual((string)yml.GetValue("name"), "Short", "plain .yml value");
        }
    }

    private string SaveAndRead(Resource resource, string file)
    {
        string path = Root + file;
        ExpectEqual(ResourceSaver.Save(resource, path), Error.Ok, "ResourceSaver.Save() to " + file);
        return FileAccess.GetFileAsString(path);
    }

    private void CheckSavePlain()
    {
        var ini = GD.Load<Ini>(Res + "sample.ini");
        if (ini != null)
        {
            ini.SetValue("video", "fps", 60);
            string text = SaveAndRead(ini, "saved.ini");
            Expect(!text.Contains("$type") && text.Contains("fps=60"), "saved INI is plain text without $type");
            var again = GD.Load<Ini>(Root + "saved.ini");
            Expect(again != null && (int)again.GetValue("video", "fps") == 60, "saved INI loads as INI again");
        }

        var csv = GD.Load<Csv>(Res + "sample.csv");
        if (csv != null)
        {
            csv.AppendRow(new[] { "4", "Dan", "blue" });
            string text = SaveAndRead(csv, "saved.csv");
            Expect(!text.Contains("$type") && text.EndsWith("4,Dan,blue\n"), "saved CSV is plain text");
        }

        var xml = GD.Load<Xml>(Res + "sample.xml");
        if (xml != null)
        {
            xml.GetRoot().SetAttribute("version", "3");
            string text = SaveAndRead(xml, "saved.xml");
            Expect(!text.Contains("$type") && text.Contains("version=\"3\""), "saved XML is plain text");
        }

        var toml = GD.Load<Toml>(Res + "sample.toml");
        if (toml != null)
        {
            toml.SetValue("server", "port", 9090);
            string text = SaveAndRead(toml, "saved.toml");
            Expect(!text.Contains("$type") && text.Contains("port = 9090"), "saved TOML is plain text");
        }

        var yaml = GD.Load<Yaml>(Res + "sample.yaml");
        if (yaml != null)
        {
            yaml.SetValue("name", "Changed");
            string text = SaveAndRead(yaml, "saved.yaml");
            Expect(!text.Contains("$type") && text.Contains("Changed"), "saved YAML is plain text");
        }
    }

    private void CheckIni()
    {
        var ini = new Ini();
        ini.SetValue("player", "name", "Ann");
        ini.SetValue("player", "pos", new Vector2(1, 2));
        ConfigFile config = ini.ToConfigFile();
        Expect(config != null && (string)config.GetValue("player", "name") == "Ann", "ToConfigFile() copies values");
        config.SetValue("audio", "volume", 0.5);
        var back = new Ini();
        ExpectEqual(back.FromConfigFile(config), Error.Ok, "FromConfigFile()");
        ExpectEqual((Vector2)back.GetValue("player", "pos"), new Vector2(1, 2), "ConfigFile round trip keeps Vector2");
        ExpectEqual((float)back.GetValue("audio", "volume"), 0.5f, "ConfigFile round trip keeps float");
        ExpectEqual((int)back.GetValue("audio", "missing", 7), 7, "GetValue() default");

        // Instance Save() and Load() like ConfigFile.
        ExpectEqual(back.Save(Root + "instance.ini"), Error.Ok, "Ini.Save()");
        var other = new Ini();
        ExpectEqual(other.Load(Root + "instance.ini"), Error.Ok, "Ini.Load()");
        ExpectEqual((string)other.GetValue("player", "name"), "Ann", "Ini.Load() reads the saved value");

        // Changing data drops the kept text (and with it the comments).
        var commented = new Ini();
        const string text = "; comment\n[a]\nb=1\n";
        ExpectEqual(commented.Parse(text, true), Error.Ok, "Parse() with keepText");
        ExpectEqual(commented.GetParsedText(), text, "keepText keeps the text");
        commented.SetValue("a", "b", 2);
        Expect(commented.GetParsedText().Length == 0, "SetValue() drops the kept text");
        Expect(!commented.EncodeToText().Contains("comment"), "EncodeToText() has no comments after a change");
    }

    private void CheckIniValueStyle()
    {
        var ini = new Ini();
        ini.SetValue("s", "name", "Ann");
        var options = new SerializerOptions();
        ExpectEqual(options.IniValueStyle, SerializerOptions.IniValueStyleEnum.GodotVariant, "IniValueStyle defaults to GodotVariant");
        ini.Options = options;
        Expect(ini.EncodeToText().Contains("name=\"Ann\""), "GodotVariant quotes strings");
        options.IniValueStyle = SerializerOptions.IniValueStyleEnum.Plain;
        Expect(ini.EncodeToText().Contains("name=Ann\n"), "Plain writes strings without quotes");
    }

    private void CheckCsv()
    {
        var csv = new Csv();
        ExpectEqual(csv.Parse("id,name,team\n1,Ann,red\n2,Bob,blue\n3,Cleo,red\n"), Error.Ok, "Csv.Parse()");
        Expect(csv.Header.Length == 3 && csv.Header[0] == "id" && csv.Header[2] == "team", "CSV header");
        ExpectEqual(csv.GetColumnCount(), 3, "CSV column count");
        int team = csv.FindColumn("team");
        ExpectEqual(team, 2, "FindColumn()");
        ExpectEqual(csv.FindColumn("missing"), -1, "FindColumn() of a missing name");
        int[] red = csv.FindRows(team, "red");
        Expect(red.Length == 2 && red[0] == 0 && red[1] == 2, "FindRows()");
        ExpectEqual(csv.GetCell(1, 1), "Bob", "GetCell()");
        string[] names = csv.GetColumn(1);
        Expect(names.Length == 3 && names[0] == "Ann" && names[2] == "Cleo", "GetColumn()");
        ExpectEqual((string)csv.GetRowAsDictionary(0)["name"], "Ann", "GetRowAsDictionary()");
        csv.SetCell(0, 1, "Anna");
        Expect(csv.EncodeToText().Contains("1,Anna,red"), "SetCell() is written");
    }

    private void CheckXml()
    {
        var xml = new Xml();
        ExpectEqual(xml.Parse("<items version=\"2\"><item id=\"a\">Sword</item><group><item id=\"b\">Shield</item></group></items>"), Error.Ok, "Xml.Parse()");
        // Godot.XmlElement, qualified: System.Xml.XmlElement has the same name.
        Godot.XmlElement root = xml.GetRoot();
        Expect(root != null && root.Name == "items", "GetRoot() returns the root element");
        ExpectEqual(root.GetAttribute("version"), "2", "GetAttribute()");
        ExpectEqual(root.GetAttribute("missing", "none"), "none", "GetAttribute() default");
        ExpectEqual(root.GetChildren().Count, 2, "GetChildren()");
        Godot.XmlElement shield = xml.FindFirst("group/item");
        Expect(shield != null && shield.Text == "Shield", "FindFirst() with a path");
        ExpectEqual(xml.FindAll("item").Count, 1, "FindAll() of direct children");
        // Writes through an element change the tree of the Xml resource.
        shield.SetAttribute("id", "c");
        Expect(xml.EncodeToText().Contains("id=\"c\""), "attribute written, then EncodeToText()");
        var created = new Xml();
        created.CreateRoot("config").SetAttribute("name", "test");
        Expect(created.EncodeToText().Contains("<config name=\"test\""), "CreateRoot()");
    }

    private void CheckToml()
    {
        var toml = new Toml();
        ExpectEqual(toml.Parse("title = \"Demo\"\n[server]\nport = 8080\n[server.limits]\nmax = 64\n"), Error.Ok, "Toml.Parse()");
        ExpectEqual((int)toml.GetValue("server.limits", "max"), 64, "GetValue() with a dotted table path");
        Expect(toml.HasTable("server.limits") && toml.HasTableKey("server", "port"), "HasTable(), HasTableKey()");
        toml.SetValue("server.limits", "max", 128);
        Expect(toml.EncodeToText().Contains("max = 128"), "SetValue() with a dotted table path");
    }

    private void CheckYaml()
    {
        var yaml = new Yaml();
        ExpectEqual(yaml.Parse("a:\n  - b: 1\n  - b: 2\n\"x/y\": 3\n"), Error.Ok, "Yaml.Parse()");
        ExpectEqual((int)yaml.GetValue("a/0/b"), 1, "GetValue(\"a/0/b\")");
        ExpectEqual((int)yaml.GetValue("a/1/b"), 2, "GetValue(\"a/1/b\")");
        ExpectEqual((int)yaml.GetValue("x~1y"), 3, "~1 escapes a slash in a key");
        Expect(yaml.HasValue("a/1") && !yaml.HasValue("a/2"), "HasValue()");
        yaml.SetValue("a/0/b", 10);
        ExpectEqual((int)yaml.GetValue("a/0/b"), 10, "SetValue()");
    }

    private void CheckErrors()
    {
        var ini = new Ini();
        ExpectEqual(ini.Parse("[broken\n"), Error.ParseError, "Ini.Parse() of broken text");
        Expect(ini.GetErrorLine() > 0, "INI error line is set");
        Expect(ini.GetErrorMessage().Length > 0, "INI error message is set");
        ini.Data = new Godot.Collections.Dictionary { { "s", new Godot.Collections.Dictionary { { "k", 1 } } } };
        Expect(ini.GetErrorMessage().Length == 0 && ini.GetErrorLine() == 0, "setting Data clears the parse error");
        var toml = new Toml();
        Expect(toml.Parse("a = 1\nb = = 2\n") != Error.Ok, "Toml.Parse() of broken text");
        Expect(toml.GetErrorLine() > 0, "TOML error line is set");
        var xml = new Xml();
        Expect(xml.Parse("<a><b></a>") != Error.Ok && xml.GetErrorLine() > 0, "XML error line is set");
    }

    private void CheckIniValueStyleSetting()
    {
        ExpectEqual((int)ProjectSettings.GetSetting("filesystem/resources/text_formats/ini_value_style"), (int)SerializerOptions.IniValueStyleEnum.GodotVariant, "project setting ini_value_style defaults to GodotVariant");
        Expect(!ProjectSettings.HasSetting("filesystem/resources/text_formats/ini_plain_values"), "the old bool ini_plain_values is not registered");
    }

    private void CheckYamlDocuments()
    {
        ExpectEqual(new Yaml().GetDocumentCount(), 1, "a new Yaml has one document");
        var yaml = new Yaml();
        ExpectEqual(yaml.Parse("a: 1\n---\n- x\n...\n---\nb: 2\n"), Error.Ok, "Yaml.Parse() of several documents");
        ExpectEqual(yaml.GetDocumentCount(), 3, "GetDocumentCount()");
        ExpectEqual((int)yaml.GetValue("a"), 1, "Data is the first document");
        var second = (Godot.Collections.Array)yaml.GetDocument(1);
        Expect(second.Count == 1 && (string)second[0] == "x", "GetDocument(1)");
        yaml.AddDocument(new Godot.Collections.Dictionary { { "c", 3 } });
        ExpectEqual(yaml.GetDocumentCount(), 4, "AddDocument()");
        yaml.RemoveDocument(1);
        ExpectEqual(yaml.EncodeToText(), "a: 1\n---\nb: 2\n---\nc: 3\n", "documents are written with ---");
        string path = Root + "documents.yaml";
        ExpectEqual(yaml.Save(path), Error.Ok, "Save() of several documents");
        var loaded = new Yaml();
        ExpectEqual(loaded.Load(path), Error.Ok, "Load() of several documents");
        ExpectEqual(loaded.GetDocumentCount(), 3, "documents survive Save() and Load()");
        yaml.RemoveDocument(0);
        ExpectEqual((int)yaml.GetValue("b"), 2, "RemoveDocument(0) makes the next document Data");
    }
}
