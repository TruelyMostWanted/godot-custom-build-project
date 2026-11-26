using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue278FeatureDataFileTextEditor;

/// <summary>
/// C# checks of 278-feature-data-file-text-editor. Check the same things as scripts/gd/check.gd.
/// The feature is editor-only, so these checks cover the runtime parts: file patterns, issue lines, the issue cap, the project setting.
/// </summary>
public partial class Issue278FeatureDataFileTextEditorCheck : FeatureCheckCs
{
    private const string Dir = "res://278-feature-data-file-text-editor/data/";

    protected override string CheckName => "278-feature-data-file-text-editor";

    protected override Task RunChecks()
    {
        CheckSchemaPatterns();
        CheckCsvOk();
        CheckCsvBad();
        CheckIssueCap();
        CheckJsonLines();
        CheckProjectSetting();
        return Task.CompletedTask;
    }

    private static string Read(string path) => FileAccess.GetFileAsString(path).Replace("\r\n", "\n");

    private static DataValidationOptions Positions()
    {
        var options = new DataValidationOptions();
        options.KeepPositions = true;
        options.MaxIssues = 200;
        return options;
    }

    private void CheckSchemaPatterns()
    {
        DataSchema schema = GD.Load<DataSchema>(Dir + "items_schema.tres");
        Expect(schema != null, "items_schema.tres loads as a DataSchema");
        if (schema == null)
        {
            return;
        }
        ExpectEqual(schema.Fields.Count, 3, "schema has three fields");
        ExpectEqual(schema.FilePatterns.Length, 1, "schema has one file pattern");
        bool matchesBad = false;
        bool matchesOk = false;
        bool matchesOther = false;
        foreach (string pattern in schema.FilePatterns)
        {
            matchesBad = matchesBad || (Dir + "items_bad.csv").Match(pattern);
            matchesOk = matchesOk || (Dir + "items_ok.csv").Match(pattern);
            matchesOther = matchesOther || (Dir + "sample.xml").Match(pattern) || "res://other/items_bad.csv".Match(pattern);
        }
        Expect(matchesBad, "file_patterns match items_bad.csv");
        Expect(matchesOk, "file_patterns match items_ok.csv");
        Expect(!matchesOther, "file_patterns do not match unrelated paths");
    }

    private void CheckCsvOk()
    {
        DataSchema schema = GD.Load<DataSchema>(Dir + "items_schema.tres");
        if (schema == null)
        {
            return;
        }
        var csv = new Csv();
        ExpectEqual(csv.Parse(Read(Dir + "items_ok.csv"), true), Error.Ok, "items_ok.csv parses");
        DataValidation result = csv.Validate(schema, Positions());
        Expect(result.IsValid(), $"items_ok.csv is valid ({result.GetIssueCount()} issues)");
    }

    private void CheckCsvBad()
    {
        DataSchema schema = GD.Load<DataSchema>(Dir + "items_schema.tres");
        if (schema == null)
        {
            return;
        }
        string text = Read(Dir + "items_bad.csv");
        Expect(text.StartsWith("id,name,hp"), "items_bad.csv is readable");
        var csv = new Csv();
        ExpectEqual(csv.Parse(text, true), Error.Ok, "items_bad.csv parses with kept text");
        DataValidation result = csv.Validate(schema, Positions());
        Expect(!result.IsValid(), "items_bad.csv is not valid");
        ExpectEqual(result.GetIssueCount(), 5, "five issues in items_bad.csv");
        // Row 1 is a multi-line cell on lines 3 and 4, so the rows after it are one line further down.
        var expectedLines = new Dictionary<int, int> { { 0, 2 }, { 1, 3 }, { 2, 5 }, { 3, 6 }, { 4, 7 }, { 5, 8 } };
        bool linesOk = true;
        bool anyAfter = false;
        for (int i = 0; i < result.GetIssueCount(); i++)
        {
            int line = result.GetIssueLine(i);
            int row = result.GetIssueRow(i);
            linesOk = linesOk && line >= 1;
            if (expectedLines.TryGetValue(row, out int expected))
            {
                linesOk = linesOk && line == expected;
            }
            else
            {
                linesOk = false;
            }
            if (row == 2)
            {
                anyAfter = true;
                ExpectEqual(line, 5, "the row after the multi-line cell is on line 5");
            }
        }
        Expect(linesOk, "every issue has the expected line");
        Expect(anyAfter, "an issue is reported after the multi-line cell");
        if (result.GetIssueCount() > 0)
        {
            Expect(result.GetIssueMessage(0) != "", "issue 0 has a message");
        }
        DataValidation plain = csv.Validate(schema);
        if (plain.GetIssueCount() > 0)
        {
            ExpectEqual(plain.GetIssueLine(0), -1, "no line without keep_positions");
        }
    }

    private void CheckIssueCap()
    {
        DataSchema schema = GD.Load<DataSchema>(Dir + "items_schema.tres");
        if (schema == null)
        {
            return;
        }
        var builder = new StringBuilder("id,name,hp\n");
        for (int i = 0; i < 500; i++)
        {
            builder.Append(i).Append(",Item ").Append(i).Append(",500\n");
        }
        var csv = new Csv();
        ExpectEqual(csv.Parse(builder.ToString(), true), Error.Ok, "generated CSV parses");
        DataValidation result = csv.Validate(schema, Positions());
        ExpectEqual(result.GetIssueCount(), 200, "max_issues caps the issues at 200");
        bool allLines = true;
        for (int i = 0; i < result.GetIssueCount(); i++)
        {
            allLines = allLines && result.GetIssueLine(i) >= 1;
        }
        Expect(allLines, "every capped issue has a line");
    }

    private void CheckJsonLines()
    {
        var schema = new DataSchema();
        var id = new DataField();
        id.Name = "id";
        id.Type = Variant.Type.Int;
        id.Required = true;
        id.Unique = true;
        schema.AddField(id);
        var name = new DataField();
        name.Name = "name";
        name.Type = Variant.Type.String;
        schema.AddField(name);
        var hp = new DataField();
        hp.Name = "hp";
        hp.Type = Variant.Type.Int;
        hp.SetRange(0, 100);
        schema.AddField(hp);
        const int count = 3000;
        var builder = new StringBuilder("[\n");
        for (int i = 0; i < count; i++)
        {
            builder.Append("{\"id\": ").Append(i).Append(", \"name\": \"Item ").Append(i).Append("\", \"hp\": ").Append(i % 3 == 0 ? 500 : 10).Append('}');
            builder.Append(i < count - 1 ? ",\n" : "\n");
        }
        builder.Append("]\n");
        string text = builder.ToString();
        Variant data = Json.ParseString(text);
        Expect(data.VariantType == Variant.Type.Array, "generated JSON parses to an Array");
        if (data.VariantType != Variant.Type.Array)
        {
            return;
        }
        var options = new DataValidationOptions();
        options.MaxIssues = 5000;
        ulong start = Time.GetTicksMsec();
        DataValidation result = schema.Validate(data, options);
        result.ResolveLines(text);
        ulong elapsed = Time.GetTicksMsec() - start;
        GD.Print($"  INFO  {count} JSON records, {result.GetIssueCount()} issues validated and resolved in {elapsed} ms on {OS.GetName()}");
        ExpectEqual(result.GetIssueCount(), 1000, "1000 issues in the JSON records");
        bool linesOk = true;
        for (int i = 0; i < result.GetIssueCount(); i++)
        {
            linesOk = linesOk && result.GetIssueLine(i) == result.GetIssueRow(i) + 2;
        }
        Expect(linesOk, "ResolveLines() gives line = row + 2");
        Expect(elapsed < 2000, $"validate and ResolveLines() finish in under 2000 ms ({elapsed} ms)");
    }

    private void CheckProjectSetting()
    {
        if (!ProjectSettings.HasSetting("filesystem/data/schema_files"))
        {
            GD.Print("  SKIP  filesystem/data/schema_files: skipped (editor only)");
            return;
        }
        Variant value = ProjectSettings.GetSetting("filesystem/data/schema_files");
        Expect(value.VariantType == Variant.Type.PackedStringArray, "filesystem/data/schema_files is a PackedStringArray");
    }
}
