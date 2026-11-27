using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.LocalizationEditor;

/// <summary>
/// Full API test of Localization Editor: entries, notes, Validate(), CSV delimiters, POT/PO and the Inspector summary
/// of LocalizationCatalog and TextLocalizationCatalog. Same tests as scripts/gd/Test.gd.
/// Expected error in the output: the reserved note column "plural".
/// </summary>
public partial class Test : FeatureTest
{
    private const string DataDir = "user://localization_editor_cs";
    private const string CatalogCsv = "keys,?context,?plural,_notes,en,de\nHELLO,,,greeting,Hello,Hallo\nHELLO,menu,,,Hello (menu),Hallo (Menü)\n?pluralrule,,,,,nplurals=2; plural=(n != 1);\nAPPLE,,APPLES,Inventory,%d apple,%d Apfel\n,,,,%d apples,%d Äpfel\nBYE,,,,Bye,\n";
    private const string DuplicateCsv = "keys,en,de\nA,a,\nB,b,\nA,a2,\n";
    private const string EmptyCsv = "keys,en,de\nA,,\nB,,\n";
    private const string LinesCsv = "keys,?plural,en,de\nA,,a,\"Zeile\nzwei\"\nAPPLE,APPLES,apple,\n,,apples,\nB,,b,\n";
    private const string LinesJson = "{\n\t\"A\": {\n\t\t\"en\": \"a\",\n\t\t\"de\": \"a\"\n\t},\n\t\"B\": {\n\t\t\"en\": \"b\"\n\t}\n}\n";
    private const string SemicolonCsv = "keys;en;de\nHELLO;Hello, world;Hallo, Welt\n";
    private const string TabCsv = "keys\ten\tde\nHELLO\tHello\tHallo\n";
    private static readonly string[] FormatClasses = ["JSON", "INI", "CSV", "XML", "TOML", "YAML"];

    protected override Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(DataDir);
        TestGetEntry();
        TestSetEntry();
        TestRename();
        TestNotes();
        TestNoteFormats();
        TestNoteFiles();
        TestValidate();
        TestValidateOptions();
        TestValidateLines();
        TestDelimiters();
        TestPo();
        TestSummary();
        return Task.CompletedTask;
    }

    private static TextLocalizationCatalog Catalog()
    {
        var catalog = new TextLocalizationCatalog();
        catalog.LoadFromText(CatalogCsv, ResourceTextSerializer.Format.Csv);
        return catalog;
    }

    private static TextLocalizationCatalog NotesCatalog()
    {
        var catalog = new TextLocalizationCatalog();
        catalog.SetText("HELLO", "en", "Hello");
        catalog.SetText("HELLO", "de", "Hallo");
        catalog.SetText("HELLO", "de", "Hallo (Menü)", "menu");
        catalog.SetText("APPLE", "de", "Apfel");
        catalog.SetText("LINE", "de", "Eins");
        catalog.SetNote("HELLO", "greeting");
        catalog.SetNote("HELLO", "Main menu button", "menu");
        catalog.SetNote("APPLE", "12", "", "max_length");
        catalog.SetNote("LINE", "Two lines\nkeep it short");
        return catalog;
    }

    private static void Write(string path, string text)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreString(text);
    }

    private void TestGetEntry()
    {
        TextLocalizationCatalog catalog = Catalog();
        Dictionary hello = catalog.GetEntry("HELLO");
        ExpectEqual((string)hello["en"], "Hello", "GetEntry has the text per locale");
        ExpectEqual((string)hello["_notes"], "greeting", "GetEntry has the note");
        Expect(!hello.ContainsKey("_key"), "GetEntry has no _key without a context");
        Expect(!hello.ContainsKey("_context"), "GetEntry has no _context without a context");
        Dictionary helloMenu = catalog.GetEntry("HELLO", "menu");
        ExpectEqual((string)helloMenu["_context"], "menu", "GetEntry with a context has _context");
        ExpectEqual((string)helloMenu["_key"], "HELLO", "GetEntry with a context has _key");
        Dictionary apple = catalog.GetEntry("APPLE");
        ExpectEqual((string)apple["_plural"], "APPLES", "GetEntry has _plural");
        string[] forms = apple["de"].AsStringArray();
        Expect(forms is ["%d Apfel", "%d Äpfel"], "GetEntry: plural forms are an Array");
        Expect(catalog.GetEntry("MISSING").Count == 0, "GetEntry of a missing key is empty");
    }

    private void TestSetEntry()
    {
        TextLocalizationCatalog catalog = Catalog();
        Dictionary hello = catalog.GetEntry("HELLO");
        catalog.SetEntry("HELLO", new Dictionary { { "de", "Servus" }, { "fr", "Salut" }, { "_notes", "informal" } });
        ExpectEqual(catalog.GetText("HELLO", "de"), "Servus", "SetEntry replaces a text");
        ExpectEqual(catalog.GetText("HELLO", "en"), "", "SetEntry clears a field the entry lacks");
        ExpectEqual(catalog.GetText("HELLO", "fr"), "Salut", "SetEntry adds a text");
        Expect(catalog.HasLocale("fr"), "SetEntry adds the locale");
        ExpectEqual(catalog.GetNote("HELLO"), "informal", "SetEntry replaces the note");
        catalog.SetEntry("HELLO", hello);
        ExpectEqual(catalog.GetText("HELLO", "en"), "Hello", "SetEntry with the old entry restores a text (undo)");
        ExpectEqual(catalog.GetText("HELLO", "fr"), "", "SetEntry with the old entry clears the added text (undo)");
        Dictionary apple = catalog.GetEntry("APPLE");
        catalog.SetEntry("APPLE", []);
        ExpectEqual(catalog.GetPluralKey("APPLE"), "", "SetEntry({}) clears the plural key");
        catalog.SetEntry("APPLE", apple);
        ExpectEqual(catalog.GetPluralText("APPLE", "de", 5), "%d Äpfel", "SetEntry restores the plural forms");
        ExpectEqual(catalog.GetPluralKey("APPLE"), "APPLES", "SetEntry restores the plural key");
        catalog.SetEntry("NEW", new Dictionary { { "fr", "Salut" } }, "ctx");
        ExpectEqual(catalog.GetText("NEW", "fr", "ctx"), "Salut", "SetEntry adds a new key with a context");
    }

    private void TestRename()
    {
        TextLocalizationCatalog catalog = Catalog();
        Dictionary entry = catalog.GetEntry("BYE");
        catalog.RemoveKey("BYE");
        catalog.SetEntry("GOODBYE", entry);
        Expect(!catalog.HasKey("BYE"), "rename: the old key is gone");
        ExpectEqual(catalog.GetText("GOODBYE", "en"), "Bye", "rename: the texts are kept");
        ExpectEqual(catalog.FindKey("GOODBYE"), catalog.KeyCount - 1, "rename: the key moves to the end");
    }

    private void TestNotes()
    {
        var empty = new TextLocalizationCatalog();
        Expect(empty.NoteColumns.Length == 0, "a new catalog has no note columns");
        TextLocalizationCatalog catalog = NotesCatalog();
        Expect(catalog.NoteColumns is ["notes", "max_length"], "NoteColumns lists the columns without the underscore");
        ExpectEqual(catalog.GetNote("HELLO"), "greeting", "GetNote reads the notes column");
        ExpectEqual(catalog.GetNote("HELLO", "menu"), "Main menu button", "GetNote with a context");
        ExpectEqual(catalog.GetNote("APPLE", "", "max_length"), "12", "GetNote with a column");
        ExpectEqual(catalog.GetNote("MISSING"), "", "GetNote of a missing key is empty");
        ExpectEqual(catalog.GetText("HELLO", "de"), "Hallo", "notes do not change the texts");
        catalog.SetNote("HELLO", "x", "", "plural");
        ExpectEqual(catalog.GetNote("HELLO", "", "plural"), "", "SetNote refuses the reserved column plural");
        ExpectEqual(catalog.NoteColumns.Length, 2, "a refused column is not added");
        catalog.SetNote("LINE", "");
        Expect(!catalog.GetEntry("LINE").ContainsKey("_notes"), "an empty note removes the note");
    }

    private void TestNoteFormats()
    {
        TextLocalizationCatalog catalog = NotesCatalog();
        for (int format = 0; format < (int)ResourceTextSerializer.Format.Max; format++)
        {
            string formatClass = FormatClasses[format];
            if (!ClassDB.ClassExists(formatClass))
            {
                GD.Print($"  SKIP  notes in {formatClass} (module disabled)");
                continue;
            }
            string text = catalog.ToText((ResourceTextSerializer.Format)format);
            var copy = new TextLocalizationCatalog();
            ExpectEqual(copy.LoadFromText(text, (ResourceTextSerializer.Format)format), Error.Ok, $"{formatClass}: LoadFromText of ToText returns Ok");
            ExpectEqual(copy.GetNote("HELLO"), "greeting", $"{formatClass}: the note round-trips");
            ExpectEqual(copy.GetNote("HELLO", "menu"), "Main menu button", $"{formatClass}: the note with a context round-trips");
            ExpectEqual(copy.GetNote("APPLE", "", "max_length"), "12", $"{formatClass}: the max_length column round-trips");
            ExpectEqual(copy.GetNote("LINE"), "Two lines\nkeep it short", $"{formatClass}: a note with a line break round-trips");
            ExpectEqual(copy.GetText("HELLO", "de", "menu"), "Hallo (Menü)", $"{formatClass}: the texts are kept");
        }
        string csv = catalog.ToText(ResourceTextSerializer.Format.Csv);
        string[] header = csv.Split('\n')[0].Trim().Split(',');
        int notesColumn = System.Array.IndexOf(header, "_notes");
        Expect(notesColumn > System.Array.IndexOf(header, "de"), "CSV header: the notes column comes after the locales");
        Expect(System.Array.IndexOf(header, "_max_length") > notesColumn, "CSV header: max_length comes after notes");
    }

    private void TestNoteFiles()
    {
        TextLocalizationCatalog catalog = NotesCatalog();
        string lcatPath = DataDir + "/notes.lcat";
        ExpectEqual(catalog.SaveToFile(lcatPath), Error.Ok, "SaveToFile(.lcat) returns Ok");
        var fromLcat = new TextLocalizationCatalog();
        ExpectEqual(fromLcat.LoadFromFile(lcatPath), Error.Ok, "LoadFromFile(.lcat) returns Ok");
        Expect(fromLcat.NoteColumns.Length == 0, ".lcat stores no notes");
        ExpectEqual(fromLcat.GetText("HELLO", "de"), "Hallo", ".lcat keeps the texts");
        string csvPath = DataDir + "/notes.csv";
        ExpectEqual(catalog.SaveToFile(csvPath), Error.Ok, "SaveToFile(.csv) returns Ok");
        var loaded = ResourceLoader.Load<TextLocalizationCatalog>(csvPath, "TextLocalizationCatalog", ResourceLoader.CacheMode.Ignore);
        ExpectEqual(loaded.GetText("HELLO", "de", "menu"), "Hallo (Menü)", "ResourceLoader loads the texts");
        Expect(loaded.NoteColumns.Length == 0, "ResourceLoader outside the editor skips the notes");
        var toolCatalog = new TextLocalizationCatalog();
        toolCatalog.LoadFromFile(csvPath);
        ExpectEqual(toolCatalog.GetNote("HELLO", "menu"), "Main menu button", "LoadFromFile keeps the notes");
    }

    private void TestValidate()
    {
        TextLocalizationCatalog catalog = Catalog();
        DataValidation result = catalog.Validate();
        ExpectEqual(result.GetIssueCount(), 1, "Validate finds the one missing text");
        ExpectEqual(result.GetIssueKind(0), DataValidation.IssueKind.EmptyRequired, "a missing text is IssueKind.EmptyRequired");
        ExpectEqual(result.GetIssueRow(0), catalog.FindKey("BYE"), "the issue row is the row of the key");
        ExpectEqual(result.GetIssueColumn(0), 1, "the issue column is the locale index");
        ExpectEqual(result.GetIssueField(0), new StringName("de"), "the issue field is the locale");
        ExpectEqual(result.GetIssuePath(0), "BYE", "the issue path is the key");
        ExpectEqual(catalog.LoadFromText(DuplicateCsv, ResourceTextSerializer.Format.Csv), Error.Ok, "a CSV with a duplicate key loads");
        ExpectEqual(catalog.KeyCount, 2, "duplicate keys are merged");
        result = catalog.Validate();
        ExpectEqual(result.GetIssueCount(), 3, "Validate finds the duplicate and two missing texts");
        ExpectEqual(result.GetIssueKind(0), DataValidation.IssueKind.DuplicateKey, "the duplicate comes first as IssueKind.DuplicateKey");
        catalog.Clear();
        Expect(catalog.Validate().IsValid(), "Clear forgets the duplicates of the last load");
        var invalid = new TextLocalizationCatalog();
        invalid.SetText("A", "qqq", "a");
        result = invalid.Validate();
        ExpectEqual(result.GetIssueKind(0), DataValidation.IssueKind.InvalidLocale, "an unknown locale is IssueKind.InvalidLocale");
    }

    private void TestValidateOptions()
    {
        var catalog = new TextLocalizationCatalog();
        catalog.LoadFromText(EmptyCsv, ResourceTextSerializer.Format.Csv);
        var options = new DataValidationOptions { StopOnFirst = true };
        ExpectEqual(catalog.Validate(options).GetIssueCount(), 1, "StopOnFirst gives one issue");
        ExpectEqual(catalog.Validate().GetIssueCount(), 4, "the default options give all 4 issues");
        var limited = new DataValidationOptions { MaxIssues = 2 };
        ExpectEqual(catalog.Validate(limited).GetIssueCount(), 2, "MaxIssues limits the issues");
    }

    private void TestValidateLines()
    {
        string csvPath = DataDir + "/lines.csv";
        Write(csvPath, LinesCsv);
        var catalog = ResourceLoader.Load<TextLocalizationCatalog>(csvPath, "TextLocalizationCatalog", ResourceLoader.CacheMode.Ignore);
        DataValidation result = catalog.Validate();
        ExpectEqual(result.GetIssueCount(), 2, "CSV lines: two issues");
        ExpectEqual(result.GetIssuePath(0), "APPLE", "CSV lines: the first issue is APPLE");
        ExpectEqual(result.GetIssueLine(0), 4, "CSV lines: APPLE is on line 4, after a quoted line break");
        ExpectEqual(result.GetIssueLine(1), 6, "CSV lines: B is on line 6, after the plural row");
        string jsonPath = DataDir + "/lines.json";
        Write(jsonPath, LinesJson);
        var jsonCatalog = ResourceLoader.Load<TextLocalizationCatalog>(jsonPath, "TextLocalizationCatalog", ResourceLoader.CacheMode.Ignore);
        result = jsonCatalog.Validate();
        ExpectEqual(result.GetIssueLine(0), 6, "JSON lines: B is on line 6");
        var textCatalog = new TextLocalizationCatalog();
        textCatalog.LoadFromText(LinesCsv, ResourceTextSerializer.Format.Csv);
        ExpectEqual((long)textCatalog.Validate().GetIssueLine(0), DataValidation.PositionNone, "a catalog from LoadFromText has no lines");
    }

    private void TestDelimiters()
    {
        var catalog = new TextLocalizationCatalog();
        ExpectEqual(catalog.LoadFromText(SemicolonCsv, ResourceTextSerializer.Format.Csv), Error.Ok, "a semicolon CSV loads");
        ExpectEqual(catalog.GetText("HELLO", "de"), "Hallo, Welt", "the semicolon is detected");
        ExpectEqual(catalog.LoadFromText(TabCsv, ResourceTextSerializer.Format.Csv), Error.Ok, "a tab CSV loads");
        ExpectEqual(catalog.GetText("HELLO", "de"), "Hallo", "the tab is detected");
        var options = new SerializerOptions { CsvDelimiter = ";" };
        string text = catalog.ToText(ResourceTextSerializer.Format.Csv, options);
        Expect(text.StartsWith("keys;en;de"), "ToText uses CsvDelimiter of the options");
        var copy = new TextLocalizationCatalog();
        ExpectEqual(copy.LoadFromText(text, ResourceTextSerializer.Format.Csv, options), Error.Ok, "LoadFromText with options returns Ok");
        ExpectEqual(copy.GetText("HELLO", "en"), "Hello", "the text round-trips with CsvDelimiter");
        Expect(options.CsvHeaders, "the options of the caller are not changed");
    }

    private void TestPo()
    {
        TextLocalizationCatalog catalog = Catalog();
        string pot = catalog.ToPo();
        Expect(pot.Contains("msgctxt \"menu\""), "POT has msgctxt for the context");
        Expect(pot.Contains("msgid \"HELLO\""), "POT has msgid for the key");
        Expect(pot.Contains("msgid_plural \"APPLES\""), "POT has msgid_plural for the plural key");
        Expect(pot.Contains("#. greeting"), "POT has the note as a #. comment");
        Expect(pot.Contains("msgstr \"\""), "POT has empty msgstr");
        string poPath = DataDir + "/de.po";
        Write(poPath, catalog.ToPo("de"));
        var translation = ResourceLoader.Load<Translation>(poPath, "Translation", ResourceLoader.CacheMode.Ignore);
        ExpectEqual(translation.Locale, "de", "the PO file has the locale");
        ExpectEqual(translation.GetMessage("HELLO"), new StringName("Hallo"), "the PO file has the text");
        ExpectEqual(translation.GetMessage("HELLO", "menu"), new StringName("Hallo (Menü)"), "the PO file has the text with a context");
        ExpectEqual(translation.GetPluralMessage("APPLE", "APPLES", 5), new StringName("%d Äpfel"), "the PO file has the plural forms");
    }

    private void TestSummary()
    {
        TextLocalizationCatalog catalog = Catalog();
        string summary = catalog.Get("summary/de").AsString();
        Expect(summary.Contains("%)"), "the Inspector property summary/de has a percentage: " + summary);
        Expect(summary.Contains("missing"), "the Inspector property summary/de names the missing keys");
    }
}
