using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.CsvFormat;

/// <summary>
/// Full API test of CSV: every method and property of the Csv class, the import as data or translation, the checks of
/// translation tables and the CSV text of a resource. Same tests as scripts/gd/Test.gd.
/// Rows are 0-based data rows (header excluded).
/// </summary>
public partial class Test : FeatureTest
{
    private const string Res = "res://csv/resources/";
    private const string Scripts = "res://text-serialization/scripts/gd/";
    private const string Quoted = "name,note\nAnn,\"a, b\"\r\nBob,\"line\nbreak \"\"quoted\"\"\"\nCleo\n";
    private const string Teams = "id,name,team\n1,Ann,red\n2,Bob,blue\n3,Cleo,red\n";
    private const int ManyRows = 100000;
    private const double PageMs = 50.0;

    private int _changes;

    protected override Task RunTests()
    {
        TestDataFile();
        TestImport();
        TestParse();
        TestParseErrors();
        TestRead();
        TestEdit();
        TestColumns();
        TestBlocks();
        TestClipboard();
        TestTyped();
        TestStringify();
        TestTranslation();
        TestTranslationHeader();
        TestSerialize();
        TestManyRows();
        return Task.CompletedTask;
    }

    private static SerializerOptions TabOptions()
    {
        return new SerializerOptions { CsvDelimiter = "\t", CsvHeaders = false };
    }

    /// <summary>The rows of an Array of PackedStringArray, joined with '|' per row and '/' between rows.</summary>
    private static string Rows(Array rows)
    {
        return string.Join("/", rows.Select(row => string.Join("|", row.AsStringArray())));
    }

    private static Resource New(string script)
    {
        var gdScript = GD.Load<GDScript>(Scripts + script);
        return (Resource)gdScript.New();
    }

    private void OnChanged()
    {
        _changes++;
    }

    private void TestDataFile()
    {
        var units = GD.Load<Csv>(Res + "units.csv");
        Expect(units != null, "Load() of a .csv imported with Keep File returns a Csv");
        Expect(units.Header.SequenceEqual(["name", "class", "hp", "ranged"]), "Header is the first line");
        ExpectEqual(units.GetRowCount(), 4, "GetRowCount() without the header");
        ExpectEqual(units.GetColumnCount(), 4, "GetColumnCount()");
        Dictionary archer = units.GetRowAsDictionary(1);
        Expect(archer.Count == 4 && (string)archer["name"] == "archer" && (string)archer["class"] == "ranger" && (string)archer["hp"] == "60" && (string)archer["ranged"] == "true", "GetRowAsDictionary(): cells are Strings");
        ExpectEqual(Rows(units.Data.AsGodotArray().Slice(0, 1)), "knight|warrior|100|false", "Data is an Array of PackedStringArray rows");
    }

    private void TestImport()
    {
        Expect(!ResourceLoader.Exists(Res + "stats.health.translation"), "a data CSV with the translation importer creates no 'health' translation");
        Expect(!ResourceLoader.Exists(Res + "stats.speed.translation"), "a data CSV with the translation importer creates no 'speed' translation");
        Expect(ResourceLoader.Exists(Res + "greetings.en.translation"), "a translation CSV creates the 'en' translation");
        var german = GD.Load<Translation>(Res + "greetings.de.translation");
        ExpectEqual(german.Locale, "de", "the 'de' translation has the locale 'de'");
        ExpectEqual((string)german.GetMessage("HELLO"), "Hallo", "the 'de' translation contains HELLO");
    }

    private void TestParse()
    {
        var csv = new Csv();
        ExpectEqual(csv.Parse(Quoted), Error.Ok, "Parse() of quoted cells");
        ExpectEqual(csv.GetRowCount(), 3, "a line break inside quotes is no new row");
        ExpectEqual(csv.GetCell(0, 1), "a, b", "a quoted cell with the delimiter");
        ExpectEqual(csv.GetCell(1, 1), "line\nbreak \"quoted\"", "a quoted cell with a line break and doubled quotes");
        ExpectEqual(csv.GetCell(2, 1), "", "a missing cell is empty");
        var semicolons = new SerializerOptions { CsvDelimiter = ";", CsvHeaders = false };
        Array rows = Csv.ParseString("007;x\n1;2\n", semicolons);
        ExpectEqual(Rows(rows), "007|x/1|2", "ParseString() without headers: rows of Strings, 007 stays \"007\"");
        Array records = Csv.ParseString("name,hp\nknight,100\n");
        Dictionary knight = records[0].AsGodotDictionary();
        Expect(records.Count == 1 && (string)knight["name"] == "knight" && (string)knight["hp"] == "100", "ParseString() with headers: Dictionaries keyed by column name");
    }

    private void TestParseErrors()
    {
        var csv = new Csv();
        ExpectEqual(csv.Parse("a,b\n1,\"open"), Error.ParseError, "an unclosed quote fails");
        ExpectEqual(csv.GetErrorMessage(), "Line 2: the quoted cell is not closed.", "error message of the unclosed quote");
        ExpectEqual(csv.Parse("a,b\n1,2\n3,\"open\n"), Error.ParseError, "an unclosed quote in line 3 fails");
        ExpectEqual(csv.GetErrorLine(), 3, "error line of the unclosed quote");
        ExpectEqual(csv.Parse("a,b\n1,2,3\n"), Error.ParseError, "a row with more cells than the header fails");
        ExpectEqual(csv.GetErrorLine(), 2, "error line of the long row");
    }

    private void TestRead()
    {
        var csv = new Csv();
        csv.Parse(Teams);
        int team = csv.FindColumn("team");
        ExpectEqual(team, 2, "FindColumn()");
        ExpectEqual(csv.FindColumn("missing"), -1, "FindColumn() of a missing name");
        Expect(csv.FindRows(team, "red").SequenceEqual([0, 2]), "FindRows()");
        ExpectEqual(csv.GetCell(1, 1), "Bob", "GetCell()");
        Expect(csv.GetRow(2).SequenceEqual(["3", "Cleo", "red"]), "GetRow()");
        Expect(csv.GetColumn(1).SequenceEqual(["Ann", "Bob", "Cleo"]), "GetColumn()");
    }

    private void TestEdit()
    {
        var csv = new Csv();
        csv.Parse(Teams);
        csv.SetCell(0, 1, "Anna");
        ExpectEqual(csv.GetCell(0, 1), "Anna", "SetCell()");
        csv.SetRow(1, ["2", "Ben", "blue"]);
        ExpectEqual(csv.GetCell(1, 1), "Ben", "SetRow()");
        csv.AppendRow(["4", "Dan", "blue"]);
        ExpectEqual(csv.GetRowCount(), 4, "AppendRow()");
        csv.InsertRow(0, ["0", "Zoe", "red"]);
        ExpectEqual(csv.GetCell(0, 1), "Zoe", "InsertRow()");
        csv.RemoveRow(0);
        ExpectEqual(csv.GetCell(0, 1), "Anna", "RemoveRow()");
        ExpectEqual(csv.EncodeToText(), "id,name,team\n1,Anna,red\n2,Ben,blue\n3,Cleo,red\n4,Dan,blue\n", "EncodeToText() writes the changes");

        var scripted = new Csv();
        scripted.Header = ["x", "y"];
        scripted.Data = new Array { new[] { "a", "b" } };
        ExpectEqual(scripted.FindColumn("y"), 1, "Header is scriptable");
        ExpectEqual(scripted.GetCell(0, 1), "b", "Data is scriptable");
        scripted.Clear();
        ExpectEqual(scripted.Header.Length, 0, "Clear() empties the header too");
    }

    private void TestColumns()
    {
        var csv = new Csv();
        ExpectEqual(csv.Parse("id,name\n1,Ann\n2,Bob\n"), Error.Ok, "Parse() of two rows");
        csv.InsertColumn(1, "team", "red");
        ExpectEqual(string.Join(",", csv.Header), "id,team,name", "InsertColumn() adds the name to the header");
        ExpectEqual(string.Join(",", csv.GetRow(0)), "1,red,Ann", "InsertColumn() fills every row with the value");
        ExpectEqual(csv.GetColumnCount(), 3, "InsertColumn() grows the column count by one");
        csv.InsertColumn(3);
        ExpectEqual(csv.Header.Length, 4, "InsertColumn() at the end appends a column");
        csv.RemoveColumn(3);
        csv.RemoveColumn(0);
        ExpectEqual(string.Join(",", csv.Header), "team,name", "RemoveColumn() removes the header name");
        ExpectEqual(string.Join(",", csv.GetRow(1)), "red,Bob", "RemoveColumn() removes the cell of every row");
        ExpectEqual(csv.EncodeToText(), "team,name\nred,Ann\nred,Bob\n", "EncodeToText() writes the column edits");
        var created = new Csv();
        created.InsertColumn(0, "id");
        created.InsertColumn(1, "name");
        created.AppendRow(new[] { "1", "Ann" });
        ExpectEqual(created.EncodeToText(), "id,name\n1,Ann\n", "a new CSV gets its header from InsertColumn()");
    }

    private void TestBlocks()
    {
        var csv = new Csv();
        csv.Parse("name,hp,speed\nknight,100,4\narcher,60\n");
        Array block = csv.GetCells(0, 1, 5, 2);
        ExpectEqual(block.Count, 2, "GetCells() leaves out rows past the end");
        ExpectEqual(Rows(block.Slice(0, 1)), "100|4", "GetCells() row 0");
        ExpectEqual(Rows(block.Slice(1, 2)), "60|", "GetCells() fills missing cells with empty strings");

        csv.Changed += OnChanged;
        csv.SetCells(1, 1, new Array { new[] { "70", "5" }, new[] { "50", "3" } });
        csv.Changed -= OnChanged;
        ExpectEqual(_changes, 1, "SetCells() emits Changed once per block");
        ExpectEqual(csv.GetRowCount(), 3, "SetCells() appends rows");
        Expect(csv.GetRow(1).SequenceEqual(["archer", "70", "5"]), "SetCells() extends a short row");
        Expect(csv.GetRow(2).SequenceEqual(["", "50", "3"]), "SetCells() appended row");

        // The undo of a paste in the CSV Table dock: the old block back, the appended row removed.
        csv.SetCells(1, 1, new Array { new[] { "60", "" } });
        csv.RemoveRow(2);
        ExpectEqual(csv.GetCell(1, 1), "60", "the old block restores the cell");
        ExpectEqual(csv.GetRowCount(), 2, "the appended row is removed");
    }

    private void TestClipboard()
    {
        // Spreadsheets copy tab-separated rows with CRLF and quote cells with line breaks.
        string text = "Sword\t10\r\nBow\t\"long\nrange\"\r\n";
        Array rows = Csv.ParseString(text, TabOptions());
        ExpectEqual(Rows(rows), "Sword|10/Bow|long\nrange", "tab-separated clipboard text");
        var csv = new Csv();
        csv.Parse("item,value\nAxe,1\n");
        csv.SetCells(0, 0, rows);
        string copied = Csv.Stringify(csv.GetCells(0, 0, 2, 2), TabOptions());
        Array pasted = Csv.ParseString(copied, TabOptions());
        ExpectEqual(Rows(pasted), Rows(rows), "copy out and paste back gives the same block");
    }

    private void TestTyped()
    {
        var shape = new Dictionary { ["id"] = (int)Variant.Type.Int, ["name"] = (int)Variant.Type.String, ["hp"] = (int)Variant.Type.Int };
        DataSchema schema = DataSchema.CreateFromDictionary(shape);
        DataField hpField = schema.GetField("hp");
        hpField.DefaultValue = 10;
        var csv = new Csv();
        csv.Parse("id,name,hp\n1,Sword,20\n2,Axe,\n");
        Dictionary row = csv.GetTypedRow(1, schema);
        Expect((int)row["id"] == 2 && (string)row["name"] == "Axe" && (int)row["hp"] == 10, "GetTypedRow(): typed values, an empty cell gives the default");
        Variant ids = csv.GetTypedColumn(0, schema);
        ExpectEqual(ids.VariantType, Variant.Type.PackedInt64Array, "GetTypedColumn() of an int field is a PackedInt64Array");
        Expect(ids.AsInt64Array().SequenceEqual([1L, 2L]), "GetTypedColumn() values");
        Variant names = csv.GetTypedColumn(1, schema);
        ExpectEqual(names.VariantType, Variant.Type.PackedStringArray, "GetTypedColumn() of a String field is a PackedStringArray");
        var bow = new Dictionary { ["id"] = 3, ["name"] = "Bow", ["hp"] = 30.0 };
        ExpectEqual(csv.AppendTypedRow(bow, schema), Error.Ok, "AppendTypedRow()");
        Expect(csv.GetRow(2).SequenceEqual(["3", "Bow", "30"]), "AppendTypedRow() writes plain text");
        var staff = new Dictionary { ["id"] = 4, ["name"] = "Staff", ["hp"] = "lots" };
        ExpectEqual(csv.AppendTypedRow(staff, schema), Error.InvalidData, "AppendTypedRow() of an invalid value");
        ExpectEqual(csv.GetRowCount(), 3, "the invalid row is not added");
    }

    private void TestStringify()
    {
        var headers = new SerializerOptions();
        var data = new Array
        {
            new Dictionary { ["name"] = "Ann", ["note"] = "a, \"b\"" },
            new Dictionary { ["name"] = "Bob", ["level"] = 3 },
        };
        string written = Csv.Stringify(data, headers);
        Expect(written.StartsWith("name,note,level\n"), "Stringify(): the header has every key");
        Expect(written.Contains("Ann,\"a, \"\"b\"\"\",\n"), "Stringify(): quotes and doubled quotes");
        Array back = Csv.ParseString(written, headers);
        ExpectEqual((string)back[1].AsGodotDictionary()["level"], "3", "Stringify() reads back, cells are Strings");
        var table = new Array { new[] { "a", "b c", "" }, new[] { "1", "2", "3" } };
        string tabbed = Csv.Stringify(table, TabOptions());
        ExpectEqual(tabbed, "a\tb c\t\n1\t2\t3\n", "Stringify() of rows with a tab delimiter");
    }

    private void TestTranslation()
    {
        var ui = new Csv();
        ExpectEqual(ui.Load(Res + "ui.csv"), Error.Ok, "Load() of a translation CSV");
        DataValidation check = ui.ValidateTranslation();
        ExpectEqual(check.GetIssueCount(), 3, "ui.csv: three missing translations, no other issue");
        var missing = new System.Collections.Generic.Dictionary<string, int>();
        for (int i = 0; i < check.GetIssueCount(); i++)
        {
            ExpectEqual(check.GetIssueKind(i), DataValidation.IssueKind.EmptyRequired, $"issue {i} is a missing translation");
            string locale = check.GetIssueField(i);
            missing.TryGetValue(locale, out int count);
            missing[locale] = count + 1;
        }
        Expect(missing.Count == 2 && missing["fr"] == 2 && missing["de"] == 1, "missing translations per locale, the _notes column is ignored");
        ui.SetCell(1, 3, "Quitter");
        ui.SetCell(2, 2, "Optionen");
        ui.SetCell(2, 3, "Options");
        DataValidation complete = ui.ValidateTranslation();
        Expect(complete.IsValid(), "a complete translation table is valid");
        var options = new DataValidationOptions { StopOnFirst = true };
        DataValidation first = new Csv().ValidateTranslation(options);
        Expect(first.GetIssueCount() <= 1, "ValidateTranslation() takes DataValidationOptions");
    }

    private void TestTranslationHeader()
    {
        var csv = new Csv();
        csv.Parse("keys,en,,xx,?context,?context\nHI,Hi,,hi,,\nHI,Hello,,hello,,\nHI,Hey,,hey,formal,\n");
        DataValidation check = csv.ValidateTranslation();
        var kinds = Enumerable.Range(0, check.GetIssueCount()).Select(check.GetIssueKind).ToList();
        ExpectEqual(kinds.Count(kind => kind == DataValidation.IssueKind.InvalidLocale), 2, "an empty and an unknown locale column");
        ExpectEqual(kinds.Count(kind => kind == DataValidation.IssueKind.DuplicateKey), 2, "a second ?context column and a repeated key");

        csv.Parse("name,health,speed\nknight,100,4\n");
        DataValidation dataCheck = csv.ValidateTranslation();
        ExpectEqual(dataCheck.GetIssueCount(), 3, "a data CSV: one issue per unknown locale column and one for the table");
        ExpectEqual(dataCheck.GetInvalidRows().Length, 0, "a data CSV gets no row checks");
        ExpectEqual(dataCheck.GetIssueKind(2), DataValidation.IssueKind.InvalidLocale, "the table issue is InvalidLocale");
        ExpectEqual(dataCheck.GetIssueColumn(2), (int)DataValidation.PositionNone, "the table issue has no column");
    }

    private void TestSerialize()
    {
        Resource sword = New("item.gd");
        sword.Set("name", "Bob, \"the\" 2nd");
        sword.Set("code", "007");
        Resource table = New("item_table.gd");
        var entries = (Array)table.Get("entries");
        entries.Add(sword);
        string text = Csv.Serialize(table);
        ExpectEqual(text, ResourceTextSerializer.Serialize(table, ResourceTextSerializer.Format.Csv), "Serialize() is ResourceTextSerializer.Serialize(Csv)");
        Expect(text.Contains("\"Bob, \"\"the\"\" 2nd\""), "Serialize() quotes a cell with the delimiter and quotes");
        Resource copy = Csv.Deserialize(text, "ItemTable");
        var copyEntries = (Array)copy.Get("entries");
        ExpectEqual((string)copyEntries[0].As<Resource>().Get("name"), "Bob, \"the\" 2nd", "Deserialize() with the type hint");
        var options = new SerializerOptions { CsvDelimiter = ";" };
        string semicolons = Csv.Serialize(table, options);
        Resource fromSemicolons = Csv.Deserialize(semicolons, "ItemTable", options);
        var semicolonEntries = (Array)fromSemicolons.Get("entries");
        ExpectEqual((string)semicolonEntries[0].As<Resource>().Get("code"), "007", "Serialize() and Deserialize() with a delimiter option");
    }

    private void TestManyRows()
    {
        var rows = new Array();
        for (int i = 0; i < ManyRows; i++)
        {
            rows.Add(new[] { i.ToString(), $"unit_{i}", (i % 100).ToString() });
        }
        var csv = new Csv();
        csv.Parse("id,name,hp\n");
        csv.SetCells(0, 0, rows);
        ExpectEqual(csv.GetRowCount(), ManyRows, "SetCells() appends 100 000 rows");
        ulong start = Time.GetTicksUsec();
        Array page = csv.GetCells(50000, 0, 500, 3);
        csv.SetCell(50000, 2, "99");
        double elapsedMs = (Time.GetTicksUsec() - start) / 1000.0;
        ExpectEqual(page.Count, 500, "GetCells() reads a page of 500 rows");
        Expect(elapsedMs < PageMs, $"page read and cell edit in 100 000 rows under {PageMs} ms ({elapsedMs:F2} ms)");
    }
}
