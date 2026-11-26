using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue346FeatureLocalizationEditor;

/// <summary>C# checks of 346-feature-localization-editor. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue346FeatureLocalizationEditorCheck : FeatureCheckCs
{
    private static readonly string[] Locales = ["en", "de", "fr", "es", "it", "pt", "ru", "ja", "zh", "ko", "pl", "nl"];
    private const int KeyCount = 20000;
    private const int PageRows = 500;

    protected override string CheckName => "346-feature-localization-editor";

    protected override Task RunChecks()
    {
        Expect(ClassDB.ClassExists("TextLocalizationCatalog"), "ClassDB knows TextLocalizationCatalog");
        if (!ClassDB.ClassExists("TextLocalizationCatalog"))
        {
            return Task.CompletedTask;
        }
        CheckEntries();
        CheckValidate();
        CheckLines();
        CheckDelimiters();
        CheckPo();
        CheckSummary();
        Measure();
        return Task.CompletedTask;
    }

    private TextLocalizationCatalog MakeCatalog()
    {
        string csv = "keys,?context,?plural,_notes,en,de\n"
            + "HELLO,,,greeting,Hello,Hallo\n"
            + "HELLO,menu,,,Hello (menu),Hallo (Menü)\n"
            + "?pluralrule,,,,,nplurals=2; plural=(n != 1);\n"
            + "APPLE,,APPLES,Inventory,%d apple,%d Apfel\n"
            + ",,,,%d apples,%d Äpfel\n"
            + "BYE,,,,Bye,\n";
        var catalog = new TextLocalizationCatalog();
        Expect(catalog.LoadFromText(csv, ResourceTextSerializer.Format.Csv) == Error.Ok, "load_from_text(CSV) returns OK");
        return catalog;
    }

    private void CheckEntries()
    {
        var catalog = MakeCatalog();
        var hello = catalog.GetEntry("HELLO", "");
        ExpectEqual(hello["en"].AsString(), "Hello", "get_entry: text");
        ExpectEqual(hello["_notes"].AsString(), "greeting", "get_entry: note");
        Expect(!hello.ContainsKey("_key"), "get_entry: no _key without context");
        Expect(!hello.ContainsKey("_context"), "get_entry: no _context without context");
        var helloMenu = catalog.GetEntry("HELLO", "menu");
        ExpectEqual(helloMenu["_context"].AsString(), "menu", "get_entry: _context with context");
        ExpectEqual(helloMenu["_key"].AsString(), "HELLO", "get_entry: _key with context");
        var apple = catalog.GetEntry("APPLE", "");
        ExpectEqual(apple["_plural"].AsString(), "APPLES", "get_entry: _plural");
        ExpectEqual(apple["_notes"].AsString(), "Inventory", "get_entry: plural note");
        Expect(apple["de"].VariantType == Variant.Type.Array, "get_entry: plural forms are an Array");
        if (apple["de"].VariantType == Variant.Type.Array)
        {
            var forms = apple["de"].AsGodotArray();
            ExpectEqual(forms.Count, 2, "get_entry: two plural forms");
            if (forms.Count == 2)
            {
                ExpectEqual(forms[1].AsString(), "%d Äpfel", "get_entry: second plural form");
            }
        }
        Expect(catalog.GetEntry("MISSING", "").Count == 0, "get_entry: missing key gives an empty Dictionary");

        catalog.SetEntry("HELLO", new Godot.Collections.Dictionary { { "de", "Servus" }, { "fr", "Salut" }, { "_notes", "informal" } }, "");
        ExpectEqual(catalog.GetText("HELLO", "de", ""), "Servus", "set_entry: text replaced");
        Expect(string.IsNullOrEmpty(catalog.GetText("HELLO", "en", "")), "set_entry: missing field cleared");
        ExpectEqual(catalog.GetText("HELLO", "fr", ""), "Salut", "set_entry: locale added");
        Expect(System.Array.IndexOf(catalog.GetLocales(), "fr") >= 0, "set_entry: locale listed");
        catalog.SetEntry("HELLO", hello, "");
        ExpectEqual(catalog.GetEntry("HELLO", "")["en"].AsString(), "Hello", "set_entry: undo restores en");
        ExpectEqual(catalog.GetText("HELLO", "fr", ""), "", "set_entry: undo clears the added text");
        catalog.SetEntry("APPLE", new Godot.Collections.Dictionary(), "");
        Expect(string.IsNullOrEmpty(catalog.GetPluralKey("APPLE", "")), "set_entry({}): plural key cleared");
        catalog.SetEntry("APPLE", apple, "");
        ExpectEqual(catalog.GetPluralText("APPLE", "de", 5, ""), "%d Äpfel", "set_entry: plural undo");
        ExpectEqual(catalog.GetPluralKey("APPLE", ""), "APPLES", "set_entry: plural key restored");
        catalog.SetEntry("NEW", new Godot.Collections.Dictionary { { "fr", "Salut" } }, "ctx");
        ExpectEqual(catalog.GetText("NEW", "fr", "ctx"), "Salut", "set_entry: new key with context");

        var entry = catalog.GetEntry("BYE", "");
        catalog.RemoveKey("BYE", "");
        catalog.SetEntry("GOODBYE", entry, "");
        Expect(!catalog.HasKey("BYE", ""), "rename: old key gone");
        ExpectEqual(catalog.GetText("GOODBYE", "en", ""), "Bye", "rename: texts kept");
        ExpectEqual(catalog.GetKeyCount() - 1, catalog.FindKey("GOODBYE", ""), "rename: moved to the end");
        Expect(catalog.NoteColumns.Length > 0, "note_columns is not empty");
        ExpectEqual(catalog.NoteColumns.Length, catalog.GetNoteColumns().Length, "note_columns equals get_note_columns()");
    }

    private void CheckValidate()
    {
        var catalog = MakeCatalog();
        var result = catalog.Validate(null);
        ExpectEqual(result.GetIssueCount(), 1, "validate: one missing translation");
        if (result.GetIssueCount() == 1)
        {
            ExpectEqual((int)result.GetIssueKind(0), (int)DataValidation.IssueKind.EmptyRequired, "validate: EMPTY_REQUIRED");
            ExpectEqual(result.GetIssueRow(0), catalog.FindKey("BYE", ""), "validate: row");
            ExpectEqual(result.GetIssueColumn(0), 1, "validate: column = locale index");
            ExpectEqual((string)result.GetIssueField(0), "de", "validate: field = locale");
            ExpectEqual(result.GetIssuePath(0), "BYE", "validate: path = entry name");
        }

        Expect(catalog.LoadFromText("keys,en,de\nA,a,\nB,b,\nA,a2,\n", ResourceTextSerializer.Format.Csv) == Error.Ok, "duplicate CSV loads");
        ExpectEqual(catalog.GetKeyCount(), 2, "duplicates are merged");
        result = catalog.Validate(null);
        ExpectEqual(result.GetIssueCount(), 3, "validate: duplicate + two missing");
        if (result.GetIssueCount() == 3)
        {
            ExpectEqual((int)result.GetIssueKind(0), (int)DataValidation.IssueKind.DuplicateKey, "validate: DUPLICATE_KEY first");
        }
        catalog.Clear();
        Expect(catalog.Validate(null).IsValid(), "validate: duplicates belong to the last load");

        var invalid = new TextLocalizationCatalog();
        invalid.SetText("A", "qqq", "a", "");
        result = invalid.Validate(null);
        ExpectEqual(result.GetIssueCount(), 1, "validate: invalid locale");
        if (result.GetIssueCount() == 1)
        {
            ExpectEqual((int)result.GetIssueKind(0), (int)DataValidation.IssueKind.InvalidLocale, "validate: INVALID_LOCALE");
        }

        var options = new DataValidationOptions { StopOnFirst = true };
        catalog.LoadFromText("keys,en,de\nA,,\nB,,\n", ResourceTextSerializer.Format.Csv);
        ExpectEqual(catalog.Validate(options).GetIssueCount(), 1, "validate: stop_on_first gives 1 issue");
        ExpectEqual(catalog.Validate(null).GetIssueCount(), 4, "validate: default options give all 4 issues");
    }

    private static void Write(string path, string text)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file?.StoreString(text);
    }

    private void CheckLines()
    {
        string csvPath = "user://catalog_346_lines_cs.csv";
        Write(csvPath, "keys,?plural,en,de\nA,,a,\"Zeile\nzwei\"\nAPPLE,APPLES,apple,\n,,apples,\nB,,b,\n");
        var catalog = ResourceLoader.Load(csvPath, "TextLocalizationCatalog", ResourceLoader.CacheMode.Ignore) as TextLocalizationCatalog;
        if (Expect(catalog != null, "CSV loads through ResourceLoader"))
        {
            var result = catalog!.Validate(null);
            ExpectEqual(result.GetIssueCount(), 2, "csv lines: two issues");
            if (result.GetIssueCount() == 2)
            {
                ExpectEqual(result.GetIssuePath(0), "APPLE", "csv lines: path 0");
                ExpectEqual(result.GetIssueLine(0), 4, "csv lines: APPLE after a quoted line break");
                ExpectEqual(result.GetIssuePath(1), "B", "csv lines: path 1");
                ExpectEqual(result.GetIssueLine(1), 6, "csv lines: B after plural rows");
            }
        }
        string jsonPath = "user://catalog_346_lines_cs.json";
        Write(jsonPath, "{\n\t\"A\": {\n\t\t\"en\": \"a\",\n\t\t\"de\": \"a\"\n\t},\n\t\"B\": {\n\t\t\"en\": \"b\"\n\t}\n}\n");
        var jsonCatalog = ResourceLoader.Load(jsonPath, "TextLocalizationCatalog", ResourceLoader.CacheMode.Ignore) as TextLocalizationCatalog;
        if (Expect(jsonCatalog != null, "JSON loads through ResourceLoader"))
        {
            var result = jsonCatalog!.Validate(null);
            ExpectEqual(result.GetIssueCount(), 1, "json lines: one issue");
            if (result.GetIssueCount() == 1)
            {
                ExpectEqual(result.GetIssueLine(0), 6, "json lines: line of entry B");
            }
        }
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(csvPath));
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(jsonPath));
    }

    private void CheckDelimiters()
    {
        var catalog = new TextLocalizationCatalog();
        Expect(catalog.LoadFromText("keys;en;de\nHELLO;Hello, world;Hallo, Welt\n", ResourceTextSerializer.Format.Csv) == Error.Ok, "semicolon CSV loads");
        ExpectEqual(catalog.GetText("HELLO", "de", ""), "Hallo, Welt", "semicolon CSV detected");
        Expect(catalog.LoadFromText("keys\ten\tde\nHELLO\tHello\tHallo\n", ResourceTextSerializer.Format.Csv) == Error.Ok, "tab CSV loads");
        ExpectEqual(catalog.GetText("HELLO", "de", ""), "Hallo", "tab CSV detected");
        var options = new SerializerOptions { CsvDelimiter = ";" };
        string text = catalog.ToText(ResourceTextSerializer.Format.Csv, options);
        Expect(text.StartsWith("keys;en;de"), "to_text uses the delimiter of the options");
        var copy = new TextLocalizationCatalog();
        Expect(copy.LoadFromText(text, ResourceTextSerializer.Format.Csv, options) == Error.Ok, "load_from_text with options");
        ExpectEqual(copy.GetText("HELLO", "en", ""), "Hello", "round trip with csv_delimiter");
        Expect(options.CsvHeaders, "caller options are not changed");
    }

    private void CheckPo()
    {
        var catalog = MakeCatalog();
        string pot = catalog.ToPo("");
        Expect(pot.Contains("msgctxt \"menu\""), "POT: msgctxt");
        Expect(pot.Contains("msgid \"HELLO\""), "POT: msgid");
        Expect(pot.Contains("msgid_plural \"APPLES\""), "POT: msgid_plural");
        Expect(pot.Contains("#. greeting"), "POT: translator note as #. comment");
        string path = "user://catalog_346_x_cs.po";
        Write(path, catalog.ToPo("de"));
        var translation = ResourceLoader.Load(path, "Translation", ResourceLoader.CacheMode.Ignore) as Translation;
        if (Expect(translation != null, "PO of de loads as Translation"))
        {
            ExpectEqual(translation!.Locale, "de", "PO: locale");
            ExpectEqual((string)translation.GetMessage("HELLO", ""), "Hallo", "PO: get_message");
            ExpectEqual((string)translation.GetMessage("HELLO", "menu"), "Hallo (Menü)", "PO: get_message with context");
            ExpectEqual((string)translation.GetPluralMessage("APPLE", "APPLES", 5, ""), "%d Äpfel", "PO: get_plural_message");
        }
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
    }

    private void CheckSummary()
    {
        var catalog = MakeCatalog();
        string summary = catalog.Get("summary/de").AsString();
        Expect(summary.Contains('(') && summary.Contains("%)"), $"inspector summary/de has a percentage: {summary}");
        Expect(summary.Contains("missing"), "inspector summary/de names the missing keys");
    }

    private static double Ms(ulong from) => (Time.GetTicksUsec() - from) / 1000.0;

    private void Measure()
    {
        GD.Print($"  -- measurement: {KeyCount} keys x {Locales.Length} locales (about 90 % filled)");
        var catalog = new TextLocalizationCatalog();
        foreach (string locale in Locales)
        {
            catalog.AddLocale(locale);
        }
        ulong t = Time.GetTicksUsec();
        for (int i = 0; i < KeyCount; i++)
        {
            string key = $"UI_KEY_{i:D5}";
            for (int l = 0; l < Locales.Length; l++)
            {
                if ((i * 7 + l * 3) % 10 == 0 && l > 0)
                {
                    catalog.SetText(key, Locales[l], "", "");
                    catalog.SetText(key, Locales[0], $"{Locales[0]} text {i}", "");
                }
                else
                {
                    catalog.SetText(key, Locales[l], $"{Locales[l]} text {i}: the quick brown fox", "");
                }
            }
        }
        double genMs = Ms(t);
        ExpectEqual(catalog.GetKeyCount(), KeyCount, "bench: key count");

        t = Time.GetTicksUsec();
        var result = catalog.Validate(null);
        double validateMs = Ms(t);
        int issues = result.GetIssueCount();
        Expect(issues > 0, $"bench: validate() finds issues ({issues})");

        var allOptions = new DataValidationOptions { MaxIssues = -1 };
        t = Time.GetTicksUsec();
        var allResult = catalog.Validate(allOptions);
        double validateAllMs = Ms(t);
        int allIssues = allResult.GetIssueCount();
        Expect(allIssues >= issues, $"bench: validate(max_issues = -1) finds at least as many issues ({allIssues})");

        t = Time.GetTicksUsec();
        int entryTotal = 0;
        for (int i = 0; i < PageRows; i++)
        {
            entryTotal += catalog.GetEntry($"UI_KEY_{i * 7:D5}", "").Count;
        }
        double entriesMs = Ms(t);
        Expect(entryTotal > 0, "bench: get_entry returns data");

        t = Time.GetTicksUsec();
        string po = catalog.ToPo("de");
        double poMs = Ms(t);
        Expect(po.Length > 0, "bench: to_po(de) is not empty");

        t = Time.GetTicksUsec();
        int missingTotal = 0;
        foreach (string locale in Locales)
        {
            missingTotal += catalog.GetMissingRows(locale).Length;
        }
        double missingMs = Ms(t);
        Expect(missingTotal > 0, $"bench: missing rows found ({missingTotal})");

        GD.Print($"  BENCH | {"operation",-34} | {"ms",10} | result");
        GD.Print($"  BENCH | {"generate catalog",-34} | {genMs,10:F1} | {KeyCount} keys");
        GD.Print($"  BENCH | {"validate() default options",-34} | {validateMs,10:F1} | {issues} issues");
        GD.Print($"  BENCH | {"validate() max_issues = -1",-34} | {validateAllMs,10:F1} | {allIssues} issues");
        GD.Print($"  BENCH | {PageRows + " x get_entry() (one page)",-34} | {entriesMs,10:F1} | {PageRows} entries");
        GD.Print($"  BENCH | {"to_po(\"de\")",-34} | {poMs,10:F1} | {po.Length / 1048576.0:F2} MB");
        GD.Print($"  BENCH | {"get_missing_rows x " + Locales.Length + " locales",-34} | {missingMs,10:F1} | {missingTotal} missing rows");
    }
}
