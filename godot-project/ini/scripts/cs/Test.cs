using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.IniFormat;

/// <summary>Full API test of INI: every method of the Ini class, its two value styles and the INI text of a resource. Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string Game = "res://ini/resources/game.ini";
    private static readonly string[] PlayerKeys = ["name", "speed", "lives", "invincible", "spawn"];
    private const string Root = "user://ini/cs/";
    private const string Settings = "res://ini/resources/settings.ini";
    private const string Scripts = "res://text-serialization/scripts/gd/";
    private const string HandWritten = "; comment\ntop=1\n\n[player]\nname=\"Ann\"\npos=Vector2(1, 2)\nplain=just text\nflags=[1, 2]\n\n[player]\nlevel=3\n";
    private const string Password = "secret";

    protected override Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        TestDataFile();
        TestParse();
        TestParseErrors();
        TestSections();
        TestConfigFile();
        TestLoadSave();
        TestEncrypted();
        TestStringify();
        TestValueStyles();
        TestSerialize();
        TestDockApi();
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
        sword.Set("offset", new Vector2(0.5f, -2.0f));
        sword.Set("code", "007");
        return sword;
    }

    private void TestDataFile()
    {
        var settings = GD.Load<Ini>(Settings);
        Expect(settings != null, "Load() of a plain .ini returns an Ini");
        Expect(settings.GetSections().SequenceEqual(["video", "audio"]), "GetSections()");
        Expect(settings.GetSectionKeys("video").SequenceEqual(["width", "height", "vsync"]), "GetSectionKeys()");
        ExpectEqual((int)settings.GetValue("video", "width"), 1280, "an int value");
        ExpectEqual((bool)settings.GetValue("video", "vsync"), true, "a bool value");
        ExpectEqual((double)settings.GetValue("audio", "music"), 0.8, "a float value");
        ExpectEqual((string)settings.GetValue("audio", "device"), "Speakers", "a quoted String value");
        ExpectEqual((int)settings.GetValue("audio", "missing", 7), 7, "GetValue() returns the default of a missing key");
        string[] extensions = ResourceLoader.GetRecognizedExtensionsForType("INI");
        Expect(extensions.Contains("ini"), "Ini loads .ini files");
        Expect(extensions.Contains("cfg"), "Ini also loads .cfg files (#392)");
    }

    private void TestParse()
    {
        var ini = new Ini();
        ExpectEqual(ini.Parse(HandWritten), Error.Ok, "Parse() of a hand-written file");
        ExpectEqual((int)ini.GetValue("", "top"), 1, "keys before the first header are in section \"\"");
        ExpectEqual((string)ini.GetValue("player", "name"), "Ann", "a quoted string");
        ExpectEqual((Vector2)ini.GetValue("player", "pos"), new Vector2(1, 2), "Godot syntax");
        ExpectEqual((string)ini.GetValue("player", "plain"), "just text", "a plain value is a String");
        Expect(((Array)ini.GetValue("player", "flags")).Select(value => (int)value).SequenceEqual([1, 2]), "an array");
        ExpectEqual((int)ini.GetValue("player", "level"), 3, "a section that appears twice is merged");
        ExpectEqual(ini.GetSectionKeys("player").Length, 5, "the merged section has all keys");
        Dictionary sections = Ini.ParseString("[a]\nb=1\n");
        ExpectEqual((int)sections["a"].AsGodotDictionary()["b"], 1, "ParseString() returns the sections");
    }

    private void TestParseErrors()
    {
        var ini = new Ini();
        ExpectEqual(ini.Parse("[video"), Error.ParseError, "an unclosed section header fails");
        ExpectEqual(ini.GetErrorMessage(), "Line 1: ']' expected.", "error message of the unclosed header");
        ExpectEqual(ini.Parse("a=1\n[broken\n"), Error.ParseError, "an unclosed header in line 2 fails");
        ExpectEqual(ini.GetErrorLine(), 2, "error line of the header");
        ExpectEqual(ini.Parse("[s]\nno value here\n"), Error.ParseError, "a line without '=' fails");
        ExpectEqual(ini.GetErrorLine(), 2, "error line of the key line");
        Expect(ini.GetErrorMessage() != "", "error message of the key line");
    }

    private void TestSections()
    {
        var ini = new Ini();
        ini.SetValue("video", "width", 1280);
        ini.SetValue("video", "height", 720);
        ini.SetValue("audio", "music", 0.8);
        Expect(ini.HasSection("video"), "HasSection()");
        Expect(!ini.HasSection("input"), "HasSection() of a missing section");
        Expect(ini.HasSectionKey("video", "width"), "HasSectionKey()");
        Expect(!ini.HasSectionKey("video", "depth"), "HasSectionKey() of a missing key");
        ini.EraseSectionKey("video", "height");
        Expect(!ini.HasSectionKey("video", "height"), "EraseSectionKey()");
        ini.SetValue("video", "width", default);
        Expect(!ini.HasSection("video"), "SetValue() with null erases the key and the emptied section");
        ini.EraseSection("audio");
        ExpectEqual(ini.GetSections().Length, 0, "EraseSection()");
        ExpectEqual(ini.Data.AsGodotDictionary().Count, 0, "Data is empty");
    }

    private void TestConfigFile()
    {
        var ini = new Ini();
        ini.SetValue("player", "name", "Ann");
        ini.SetValue("player", "pos", new Vector2(1, 2));
        ConfigFile config = ini.ToConfigFile();
        ExpectEqual((string)config.GetValue("player", "name"), "Ann", "ToConfigFile() copies the values");
        config.SetValue("audio", "volume", 0.5);
        var back = new Ini();
        ExpectEqual(back.FromConfigFile(config), Error.Ok, "FromConfigFile()");
        ExpectEqual((Vector2)back.GetValue("player", "pos"), new Vector2(1, 2), "the round trip keeps a Vector2");
        ExpectEqual((double)back.GetValue("audio", "volume"), 0.5, "the round trip keeps a float");
    }

    private void TestLoadSave()
    {
        string path = Root + "settings.ini";
        var ini = new Ini();
        ini.SetValue("slot1", "level", 3);
        ExpectEqual(ini.Save(path), Error.Ok, "Save()");
        ExpectEqual(FileAccess.GetFileAsString(path), "[slot1]\nlevel=3\n", "Save() writes plain INI");
        var other = new Ini();
        ExpectEqual(other.Load(path), Error.Ok, "Load()");
        ExpectEqual((int)other.GetValue("slot1", "level"), 3, "Load() reads the saved value");
        ExpectEqual(other.Load(Root + "missing.ini"), Error.FileNotFound, "Load() of a missing file");
        var saved = (Ini)ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        ExpectEqual((int)saved.GetValue("slot1", "level"), 3, "ResourceLoader.Load() of a saved Ini");
    }

    private void TestEncrypted()
    {
        var ini = new Ini();
        ini.SetValue("save", "gold", 250);
        string passPath = Root + "pass.ini";
        ExpectEqual(ini.SaveEncryptedPass(passPath, Password), Error.Ok, "SaveEncryptedPass()");
        Expect(!FileAccess.GetFileAsString(passPath).Contains("gold"), "the file is encrypted");
        var withPass = new Ini();
        ExpectEqual(withPass.LoadEncryptedPass(passPath, Password), Error.Ok, "LoadEncryptedPass()");
        ExpectEqual((int)withPass.GetValue("save", "gold"), 250, "LoadEncryptedPass() reads the value");

        byte[] key = "0123456789abcdef0123456789abcdef".ToUtf8Buffer();
        string keyPath = Root + "key.ini";
        ExpectEqual(ini.SaveEncrypted(keyPath, key), Error.Ok, "SaveEncrypted()");
        var withKey = new Ini();
        ExpectEqual(withKey.LoadEncrypted(keyPath, key), Error.Ok, "LoadEncrypted()");
        ExpectEqual((int)withKey.GetValue("save", "gold"), 250, "LoadEncrypted() reads the value");
    }

    private void TestStringify()
    {
        var data = new Dictionary
        {
            [""] = new Dictionary { ["version"] = 2 },
            ["audio"] = new Dictionary { ["volume"] = 0.5, ["device"] = "Speakers (USB)", ["mute"] = false },
            ["video"] = new Dictionary { ["size"] = new Vector2I(1280, 720) },
        };
        string typed = Ini.Stringify(data);
        Expect(typed.StartsWith("version=2\n"), "Stringify(): keys of section \"\" first, without a header");
        Expect(typed.Contains("[audio]\n"), "Stringify(): a section header");
        Expect(typed.Contains("device=\"Speakers (USB)\""), "Stringify(): Godot Variant style quotes strings");
        Dictionary typedBack = Ini.ParseString(typed);
        ExpectEqual((Vector2I)typedBack["video"].AsGodotDictionary()["size"], new Vector2I(1280, 720), "Godot Variant style reads back");
        var plain = new SerializerOptions { IniValueStyle = SerializerOptions.IniValueStyleEnum.Plain };
        string plainText = Ini.Stringify(data, plain);
        Expect(plainText.Contains("device=Speakers (USB)"), "Stringify() with the Plain style: no quotes");
        Expect(plainText.Contains("mute=false"), "Stringify() with the Plain style: bool");
        Dictionary plainBack = Ini.ParseString(plainText);
        Dictionary audio = plainBack["audio"].AsGodotDictionary();
        Expect((string)audio["device"] == "Speakers (USB)" && (bool)audio["mute"] == false, "Plain style reads back");
    }

    private void TestValueStyles()
    {
        var settings = new Ini();
        settings.Parse("[audio]\ndevice=\"Speakers\"\n");
        Expect(settings.EncodeToText().Contains("device=\"Speakers\""), "Godot Variant style is the default");
        settings.Options = new SerializerOptions { IniValueStyle = SerializerOptions.IniValueStyleEnum.Plain };
        ExpectEqual(settings.EncodeToText(), "[audio]\ndevice=Speakers\n", "Options.IniValueStyle = Plain writes device=Speakers");
    }

    private void TestSerialize()
    {
        Resource sword = MakeSword();
        string text = Ini.Serialize(sword);
        ExpectEqual(text, ResourceTextSerializer.Serialize(sword, ResourceTextSerializer.Format.Ini), "Serialize() is ResourceTextSerializer.Serialize(Ini)");
        Expect(text.StartsWith("[root]\n$type=\"Item\"\n$format_version=1\n"), "Serialize(): section [root] and $type first");
        Resource copy = Ini.Deserialize(text);
        ExpectEqual((string)copy.Get("code"), "007", "Deserialize() restores the resource");
        Resource shield = Ini.Deserialize("[root]\nname=\"Shield\"\n", "Item");
        ExpectEqual((string)shield.Get("name"), "Shield", "Deserialize() with a type hint reads a file without $type");

        Resource inventory = New("inventory.gd");
        inventory.Set("main_hand", sword);
        var items = (Array)inventory.Get("items");
        items.Add(New("item.gd"));
        string sections = Ini.Serialize(inventory);
        Expect(sections.Contains("\n[root/main_hand]\n"), "a nested resource is a section [root/main_hand]");
        Expect(sections.Contains("\n[root/items/0]\n"), "an array element is a section [root/items/0]");

        var plain = new SerializerOptions { IniValueStyle = SerializerOptions.IniValueStyleEnum.Plain };
        string plainText = Ini.Serialize(sword, plain);
        Expect(plainText.Contains("\nname=Sword\n"), "Plain style: name=Sword");
        Expect(plainText.Contains("\noffset=0.5, -2.0\n"), "Plain style: offset=0.5, -2.0");
        Expect(plainText.Contains("\ncode=\"007\"\n"), "Plain style keeps a string that looks like a number quoted");
        Resource fromPlain = Ini.Deserialize(plainText, "", plain);
        ExpectEqual((string)fromPlain.Get("code"), "007", "Deserialize() of the Plain style");
    }

    /// <summary>The INI dock is editor UI without own API: these tests cover the API it uses.</summary>
    private void TestDockApi()
    {
        var ini = GD.Load<Ini>(Game);
        Expect(ini != null, "game.ini loads as INI");
        Expect(ini.GetSections().SequenceEqual(new[] { "", "player", "audio" }), "sections in file order, keys before the first header in \"\"");
        Expect(ini.GetSectionKeys("player").SequenceEqual(PlayerKeys), "keys in file order");
        ExpectEqual(ini.GetValue("player", "lives").VariantType, Variant.Type.Int, "lives is an int");
        ExpectEqual(ini.GetValue("player", "speed").VariantType, Variant.Type.Float, "speed is a float");
        ExpectEqual(ini.GetValue("player", "invincible").VariantType, Variant.Type.Bool, "invincible is a bool");
        ExpectEqual(ini.GetValue("player", "spawn").AsVector2(), new Vector2(10, 20), "spawn is a Vector2");

        // Rename a key like the dock: a copy of data with the key replaced at the same position.
        Dictionary before = ini.Data.AsGodotDictionary().Duplicate(true);
        var renamed = new Dictionary();
        Dictionary player = before["player"].AsGodotDictionary();
        foreach (Variant key in player.Keys)
        {
            renamed[key.AsString() == "speed" ? "walk_speed" : key] = player[key];
        }
        Dictionary next = before.Duplicate(true);
        next["player"] = renamed;
        int changedCount = 0;
        ini.Changed += () => changedCount++;
        ini.Data = next;
        ExpectEqual(changedCount, 1, "setting Data emits changed once");
        Expect(ini.GetSectionKeys("player").SequenceEqual(new[] { "name", "walk_speed", "lives", "invincible", "spawn" }), "a renamed key keeps its position");
        Expect(ini.GetParsedText().Length == 0, "an edit drops the kept text, so the text editor reloads");
        Expect(ini.EncodeToText().Contains("walk_speed=4.5"), "EncodeToText() shows the renamed key");

        // Undo: the copy taken before the edit.
        ini.Data = before;
        Expect(ini.GetSectionKeys("player").SequenceEqual(PlayerKeys), "undo restores the order");

        // Typed edits and an empty section.
        ini.SetValue("player", "lives", 5);
        ExpectEqual(ini.GetValue("player", "lives").AsInt32(), 5, "SetValue() keeps the int");
        Dictionary withSection = ini.Data.AsGodotDictionary().Duplicate(true);
        withSection["network"] = new Dictionary();
        ini.Data = withSection;
        Expect(ini.HasSection("network"), "an empty section stays in Data");
        Expect(ini.EncodeToText().Contains("[network]"), "an empty section is written with its header");
        ini.SetValue("network", "port", 8080);
        ini.EraseSectionKey("network", "port");
        Expect(!ini.HasSection("network"), "EraseSectionKey() removes a section without keys, like ConfigFile");

        // A broken file: the dock does not edit, it shows the error.
        var broken = new Ini();
        broken.Parse("[player
name=\"Ann\"
", true);
        Expect(broken.GetErrorMessage().Length > 0, "a broken header reports an error");
        Expect(broken.GetErrorLine() > 0, "the error has a line");
    }
}
