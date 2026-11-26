using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;
using GArray = Godot.Collections.Array;

namespace TruelyInteractive.Issue279FeatureCsvTableEditor;

/// <summary>
/// C# checks of 279-feature-csv-table-editor (and 280, translation CSV checks). Check the same things as scripts/gd/check.gd.
/// The CSV Table dock is editor UI; these checks cover the API it is built on: Csv.GetCells()/SetCells() (copy, paste,
/// undo of a block), tab-separated clipboard text, the schema issues it marks and Csv.ValidateTranslation() (#280).
/// resources/units.csv is a data CSV ("Keep File") with units_schema.tres; resources/ui.csv is a translation CSV.
/// Rows are 0-based data rows (header excluded).
/// </summary>
public partial class Issue279FeatureCsvTableEditorCheck : FeatureCheckCs
{
    private const string Res = "res://279-feature-csv-table-editor/resources/";
    private const int PositionNone = -1;

    protected override string CheckName => "279-feature-csv-table-editor";

    protected override Task RunChecks()
    {
        CheckApi();
        CheckBlocks();
        CheckTsvClipboardText();
        CheckSchemaIssues();
        CheckTranslation();
        CheckTranslationHeader();
        CheckManyRows();
        return Task.CompletedTask;
    }

    private static SerializerOptions TsvOptions() => new SerializerOptions { CsvDelimiter = "\t", CsvHeaders = false };

    private static bool Same(Variant row, params string[] cells) => Same(row.AsStringArray(), cells);

    private static bool Same(string[] row, params string[] cells)
    {
        if (row.Length != cells.Length)
        {
            return false;
        }
        for (int i = 0; i < cells.Length; i++)
        {
            if (row[i] != cells[i])
            {
                return false;
            }
        }
        return true;
    }

    private void CheckApi()
    {
        foreach (string method in new[] { "get_cells", "set_cells", "validate_translation" })
        {
            Expect(ClassDB.ClassHasMethod("CSV", method), $"CSV.{method}()");
        }
    }

    private void CheckBlocks()
    {
        var csv = new Csv();
        ExpectEqual(csv.Parse("name,hp,speed\nknight,100,4\narcher,60\n"), Error.Ok, "blocks: parse");
        GArray block = csv.GetCells(0, 1, 5, 2);
        Expect(block.Count == 2, "GetCells() leaves out rows past the end");
        Expect(block.Count == 2 && Same(block[0], "100", "4"), "GetCells() row 0");
        Expect(block.Count == 2 && Same(block[1], "60", ""), "GetCells() fills missing cells with empty strings");

        int changes = 0;
        void Count() => changes++;
        csv.Changed += Count;
        csv.SetCells(1, 1, new GArray { new string[] { "70", "5" }, new string[] { "50", "3" } });
        csv.Changed -= Count;
        ExpectEqual(changes, 1, "SetCells() emits Changed once per block");
        ExpectEqual(csv.GetRowCount(), 3, "SetCells() appends rows");
        Expect(Same(csv.GetRow(1), "archer", "70", "5"), "SetCells() extends a short row");
        Expect(Same(csv.GetRow(2), "", "50", "3"), "SetCells() appended row");

        // The table editor's undo of a paste: old block back, appended rows removed.
        csv.SetCells(1, 1, new GArray { new string[] { "60", "" } });
        csv.RemoveRow(2);
        Expect(csv.GetCell(1, 1) == "60" && csv.GetRowCount() == 2, "old block restores the data");
    }

    private void CheckTsvClipboardText()
    {
        // Spreadsheets copy tab-separated rows with CRLF and quote cells with line breaks.
        GArray rows = Csv.ParseString("Sword\t10\r\nBow\t\"long\nrange\"\r\n", TsvOptions());
        ExpectEqual(rows.Count, 2, "TSV clipboard text: 2 rows");
        Expect(rows.Count == 2 && Same(rows[1], "Bow", "long\nrange"), "TSV clipboard text: quoted multi-line cell");
        var csv = new Csv();
        csv.Parse("item,value\nAxe,1\n");
        csv.SetCells(0, 0, rows);
        string copied = Csv.Stringify(csv.GetCells(0, 0, 2, 2), TsvOptions());
        GArray pasted = Csv.ParseString(copied, TsvOptions());
        Expect(pasted.Count == 2 && Same(pasted[0], "Sword", "10") && Same(pasted[1], "Bow", "long\nrange"), "copy out and paste back round-trips");
    }

    private void CheckSchemaIssues()
    {
        var csv = new Csv();
        ExpectEqual(csv.Load(Res + "units.csv"), Error.Ok, "units.csv loads");
        var schema = GD.Load<DataSchema>(Res + "units_schema.tres");
        Expect(schema != null, "units_schema.tres loads");
        if (schema == null)
        {
            return;
        }
        Expect(schema.FilePatterns.Length == 1 && (Res + "units.csv").MatchN(schema.FilePatterns[0]), "FilePatterns match units.csv");
        DataValidation validation = csv.Validate(schema);
        int[] invalid = validation.GetInvalidRows();
        Expect(invalid.Length == 1 && invalid[0] == 3, "only the ghost row is invalid");
        var columns = new HashSet<int>();
        for (int i = 0; i < validation.GetIssueCount(); i++)
        {
            columns.Add(validation.GetIssueColumn(i));
        }
        Expect(columns.Contains(1) && columns.Contains(2) && columns.Contains(3), "issues have the cell column (class, hp, ranged)");
    }

    private static Dictionary<string, int> MissingPerLocale(DataValidation validation, Csv csv)
    {
        var missing = new Dictionary<string, int>();
        string[] header = csv.Header;
        for (int i = 0; i < validation.GetIssueCount(); i++)
        {
            if (validation.GetIssueKind(i) == DataValidation.IssueKind.EmptyRequired)
            {
                string locale = header[validation.GetIssueColumn(i)];
                missing[locale] = missing.GetValueOrDefault(locale) + 1;
            }
        }
        return missing;
    }

    private void CheckTranslation()
    {
        var csv = new Csv();
        ExpectEqual(csv.Load(Res + "ui.csv"), Error.Ok, "ui.csv loads as CSV");
        DataValidation validation = csv.ValidateTranslation();
        ExpectEqual(validation.GetIssueCount(), 3, "ui.csv: 3 missing translations, no other issue");
        Dictionary<string, int> missing = MissingPerLocale(validation, csv);
        Expect(missing.Count == 2 && missing.GetValueOrDefault("de") == 1 && missing.GetValueOrDefault("fr") == 2, "missing translations per locale");
        for (int i = 0; i < validation.GetIssueCount(); i++)
        {
            Expect(validation.GetIssueRow(i) >= 1 && validation.GetIssueField(i) != "_notes", $"issue {i} has a row and is no ignored column");
        }

        csv.SetCell(1, 3, "Quitter");
        csv.SetCell(2, 2, "Optionen");
        csv.SetCell(2, 3, "Options");
        Expect(csv.ValidateTranslation().IsValid(), "complete translation table is valid");
    }

    private void CheckTranslationHeader()
    {
        var csv = new Csv();
        csv.Parse("keys,en,,xx,?context,?context\nHI,Hi,,hi,,\nHI,Hello,,hello,,\nHI,Hey,,hey,formal,\n");
        DataValidation validation = csv.ValidateTranslation();
        int invalidLocales = 0;
        int duplicates = 0;
        for (int i = 0; i < validation.GetIssueCount(); i++)
        {
            switch (validation.GetIssueKind(i))
            {
                case DataValidation.IssueKind.InvalidLocale:
                    invalidLocales++;
                    break;
                case DataValidation.IssueKind.DuplicateKey:
                    duplicates++;
                    break;
            }
        }
        ExpectEqual(invalidLocales, 2, "empty and unknown locale columns");
        ExpectEqual(duplicates, 2, "second ?context column and repeated key");

        csv.Parse("name,health,speed\nknight,100,4\n");
        validation = csv.ValidateTranslation();
        int last = validation.GetIssueCount() - 1;
        Expect(validation.GetIssueKind(last) == DataValidation.IssueKind.InvalidLocale && validation.GetIssueColumn(last) == PositionNone, "data CSV: not a translation table");
    }

    private void CheckManyRows()
    {
        // The dock shows one page (500 rows) of a large file; reading a page and editing a cell must stay fast.
        var rows = new GArray();
        for (int i = 0; i < 100000; i++)
        {
            rows.Add(new string[] { i.ToString(), $"unit_{i}", (i % 100).ToString() });
        }
        var csv = new Csv();
        csv.Parse("id,name,hp\n");
        csv.SetCells(0, 0, rows);
        ExpectEqual(csv.GetRowCount(), 100000, "100k rows");
        var watch = Stopwatch.StartNew();
        GArray page = csv.GetCells(50000, 0, 500, 3);
        csv.SetCell(50000, 2, "99");
        double elapsedMs = watch.Elapsed.TotalMilliseconds;
        Expect(page.Count == 500 && csv.GetCell(50000, 2) == "99", "page read and cell edit");
        Expect(elapsedMs < 50.0, $"page read + cell edit in 100k rows under 50 ms ({elapsedMs:F2} ms)");
    }
}
