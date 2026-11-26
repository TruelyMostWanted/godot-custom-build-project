using System.Text;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue277FeatureDataSchema;

/// <summary>
/// C# checks of 277-feature-data-schema. Check the same things as scripts/gd/check.gd.
/// items.csv has a header, valid rows, invalid rows and one quoted multi-line cell. items_schema.tres is a DataSchema made as
/// an inspector resource. Rows are 0-based data rows (header excluded), lines are 1-based.
/// </summary>
public partial class Issue277FeatureDataSchemaCheck : FeatureCheckCs
{
    private const string Res = "res://277-feature-data-schema/resources/";
    private const int PositionNone = -1;

    protected override string CheckName => "277-feature-data-schema";

    protected override Task RunChecks()
    {
        CheckApi();
        CheckSchemaResource();
        CheckCsvFile();
        CheckCsvParsePositions();
        CheckDictionaryAndOptions();
        CheckJson();
        CheckIni();
        CheckXml();
        CheckNestedAndYaml();
        CheckTypedCsv();
        CheckManyRows();
        return Task.CompletedTask;
    }

    private static long T(Variant.Type type) => (long)type;

    private static DataField MakeField(string name, Variant.Type type)
    {
        var field = new DataField();
        field.Name = name;
        field.Type = type;
        return field;
    }

    /// <summary>id (int, unique, required), name (not empty), rarity (enum), hp (0..100, default 10), code (pattern).</summary>
    private static DataSchema ItemSchema()
    {
        var schema = new DataSchema();
        DataField id = MakeField("id", Variant.Type.Int);
        id.Required = true;
        id.Unique = true;
        schema.AddField(id);
        DataField name = MakeField("name", Variant.Type.String);
        name.AllowEmpty = false;
        schema.AddField(name);
        DataField rarity = MakeField("rarity", Variant.Type.String);
        rarity.SetEnumValues(new[] { "common", "rare" });
        schema.AddField(rarity);
        DataField hp = MakeField("hp", Variant.Type.Int);
        hp.SetRange(0, 100);
        hp.DefaultValue = 10;
        schema.AddField(hp);
        DataField code = MakeField("code", Variant.Type.String);
        code.Pattern = "[A-Z]{2}[0-9]+";
        schema.AddField(code);
        return schema;
    }

    private static bool HasIssue(DataValidation validation, DataValidation.IssueKind kind, string field, int row = PositionNone)
    {
        for (int i = 0; i < validation.GetIssueCount(); i++)
        {
            if (validation.GetIssueKind(i) == kind && (string)validation.GetIssueField(i) == field && validation.GetIssueRow(i) == row)
            {
                return true;
            }
        }
        return false;
    }

    private void CheckApi()
    {
        foreach (string cls in new[] { "DataField", "DataSchema", "DataValidationOptions" })
        {
            Expect(ClassDB.ClassExists(cls) && ClassDB.IsParentClass(cls, "Resource"), $"{cls} is a Resource");
        }
        Expect(ClassDB.ClassExists("DataValidation") && ClassDB.IsParentClass("DataValidation", "RefCounted"), "DataValidation is RefCounted");
        foreach (string method in new[] { "set_range", "set_enum_values" })
        {
            Expect(ClassDB.ClassHasMethod("DataField", method), $"DataField.{method}()");
        }
        foreach (string method in new[] { "get_field", "find_field", "add_field", "remove_field", "validate", "convert_value", "create_from_dictionary" })
        {
            Expect(ClassDB.ClassHasMethod("DataSchema", method), $"DataSchema.{method}()");
        }
        foreach (string method in new[] { "is_valid", "get_issue_count", "get_issue_kind", "get_issue_message", "get_issue_field", "get_issue_path", "get_issue_row", "get_issue_column", "get_issue_line", "get_invalid_rows", "add_issue", "resolve_lines" })
        {
            Expect(ClassDB.ClassHasMethod("DataValidation", method), $"DataValidation.{method}()");
        }
        Expect(ClassDB.ClassHasMethod("TextDataFormat", "validate"), "TextDataFormat.validate()");
        foreach (string method in new[] { "get_typed_row", "append_typed_row", "get_typed_column" })
        {
            Expect(ClassDB.ClassHasMethod("CSV", method), $"CSV.{method}()");
        }
        foreach (string method in new[] { "get_fields", "get_extra_fields", "get_extra_field_template", "get_file_patterns" })
        {
            Expect(ClassDB.ClassHasMethod("DataSchema", method), $"DataSchema.{method}() (property fields)");
        }
        foreach (string method in new[] { "is_keep_positions", "get_max_issues", "is_stop_on_first" })
        {
            Expect(ClassDB.ClassHasMethod("DataValidationOptions", method), $"DataValidationOptions.{method}() (property)");
        }
        var field = new DataField();
        ExpectEqual(field.Type, Variant.Type.String, "DataField default type is String");
        Expect(!field.Required && !field.Unique && field.AllowEmpty, "DataField defaults");
        ExpectEqual(new DataSchema().ExtraFields, DataSchema.ExtraFieldsEnum.Reject, "extra_fields defaults to reject");
    }

    private void CheckSchemaResource()
    {
        var schema = GD.Load<DataSchema>(Res + "items_schema.tres");
        Expect(schema != null, "items_schema.tres loads as a DataSchema");
        if (schema == null)
        {
            return;
        }
        ExpectEqual(schema.Fields.Count, 5, "schema resource has five fields");
        ExpectEqual(schema.FindField("hp"), 3, "FindField() of hp");
        ExpectEqual(schema.FindField("missing"), -1, "FindField() of a missing field");
        DataField id = schema.GetField("id");
        Expect(id != null && id.Required && id.Unique && id.Type == Variant.Type.Int, "id field: int, required, unique");
        DataField hp = schema.GetField("hp");
        Expect(hp != null && hp.Hint == PropertyHint.Range && hp.HintString == "0,100,1" && (long)hp.DefaultValue == 10, "hp field: range and default");
        DataField rarity = schema.GetField("rarity");
        Expect(rarity != null && rarity.Hint == PropertyHint.Enum && rarity.HintString == "common,rare", "rarity field: enum");
        DataField name = schema.GetField("name");
        Expect(name != null && !name.AllowEmpty, "name field: not empty");
        ExpectEqual((long)schema.ConvertValue("hp", "42"), 42L, "ConvertValue() to int");
        ExpectEqual((string)schema.ConvertValue("name", "abc"), "abc", "ConvertValue() to String");
    }

    private void CheckCsvFile()
    {
        var schema = GD.Load<DataSchema>(Res + "items_schema.tres");
        var csv = GD.Load<Csv>(Res + "items.csv");
        Expect(csv != null, "GD.Load<Csv>() of items.csv");
        if (csv == null || schema == null)
        {
            return;
        }
        ExpectEqual(csv.GetRowCount(), 4, "items.csv has four data rows");
        ExpectEqual(csv.GetCell(1, 1).Replace("\r", ""), "Big\nAxe", "multi-line quoted cell");
        DataValidation result = csv.Validate(schema);
        Expect(!result.IsValid(), "items.csv is not valid");
        Expect(HasIssue(result, DataValidation.IssueKind.DuplicateKey, "id", 2), "duplicate id in row 2");
        Expect(HasIssue(result, DataValidation.IssueKind.EmptyRequired, "name", 2), "empty name in row 2");
        Expect(HasIssue(result, DataValidation.IssueKind.NotInEnum, "rarity", 2), "rarity epic in row 2");
        Expect(HasIssue(result, DataValidation.IssueKind.TypeMismatch, "hp", 2), "hp x in row 2");
        Expect(HasIssue(result, DataValidation.IssueKind.PatternMismatch, "code", 2), "code ab in row 2");
        Expect(HasIssue(result, DataValidation.IssueKind.OutOfRange, "hp", 3), "hp 500 in row 3");
        ExpectEqual(result.GetIssueCount(), 6, "six issues in items.csv");
        int[] invalid = result.GetInvalidRows();
        Expect(invalid.Length == 2 && invalid[0] == 2 && invalid[1] == 3, "invalid rows of items.csv");
        for (int i = 0; i < result.GetIssueCount(); i++)
        {
            Expect(result.GetIssueMessage(i) != "", $"issue {i} has a message");
            ExpectEqual(result.GetIssueLine(i), PositionNone, $"no line without keep_positions (issue {i})");
        }
    }

    private void CheckCsvParsePositions()
    {
        DataSchema schema = ItemSchema();
        string text = FileAccess.GetFileAsString(Res + "items.csv").Replace("\r\n", "\n");
        Expect(text.StartsWith("id,name"), "items.csv text is readable");
        var csv = new Csv();
        ExpectEqual(csv.Parse(text, true), Error.Ok, "Csv.Parse() keeping the text");
        var options = new DataValidationOptions();
        options.KeepPositions = true;
        DataValidation result = csv.Validate(schema, options);
        ExpectEqual(result.GetIssueCount(), 6, "six issues with positions");
        // Row 1 spans lines 3 and 4, so row 2 is on line 5 and row 3 on line 6.
        for (int i = 0; i < result.GetIssueCount(); i++)
        {
            int row = result.GetIssueRow(i);
            ExpectEqual(result.GetIssueLine(i), row == 2 ? 5 : 6, $"line of the issue in row {row}");
            if ((string)result.GetIssueField(i) == "hp")
            {
                ExpectEqual(result.GetIssueColumn(i), 3, "hp is column 3");
            }
        }

        // An unknown column and a missing required column.
        var extra = new Csv();
        extra.Parse("id,name,colour\n1,Sword,red\n");
        DataValidation extraResult = extra.Validate(schema);
        Expect(HasIssue(extraResult, DataValidation.IssueKind.UnknownField, "colour"), "unknown column colour");
        ExpectEqual(extraResult.GetIssueCount(), 1, "only the unknown column is reported");
        var missing = new Csv();
        missing.Parse("name\nSword\n");
        Expect(HasIssue(missing.Validate(schema), DataValidation.IssueKind.MissingField, "id"), "missing required column id");
        // A parse error is the only issue.
        var broken = new Csv();
        broken.Parse("id\n\"open\n");
        DataValidation brokenResult = broken.Validate(schema);
        ExpectEqual(brokenResult.GetIssueCount(), 1, "a parse error is the only issue");
        if (brokenResult.GetIssueCount() == 1)
        {
            ExpectEqual(brokenResult.GetIssueKind(0), DataValidation.IssueKind.ParseError, "parse error kind");
        }

        var valid = new Csv();
        valid.Parse("id,name,rarity,hp,code\n1,Sword,common,10,AB1\n2,Axe,rare,,CD2\n");
        Expect(valid.Validate(schema).IsValid(), "valid CSV is valid (empty hp is fine)");
    }

    private void CheckDictionaryAndOptions()
    {
        DataSchema schema = ItemSchema();
        var data = new Godot.Collections.Dictionary
        {
            ["name"] = "",
            ["rarity"] = "epic",
            ["hp"] = 150,
            ["code"] = "x1",
            ["colour"] = "red",
        };
        DataValidation result = schema.Validate(data);
        Expect(!result.IsValid(), "invalid dictionary");
        Expect(HasIssue(result, DataValidation.IssueKind.MissingField, "id"), "missing field id");
        Expect(HasIssue(result, DataValidation.IssueKind.EmptyRequired, "name"), "empty name");
        Expect(HasIssue(result, DataValidation.IssueKind.NotInEnum, "rarity"), "rarity not in enum");
        Expect(HasIssue(result, DataValidation.IssueKind.OutOfRange, "hp"), "hp out of range");
        Expect(HasIssue(result, DataValidation.IssueKind.PatternMismatch, "code"), "code pattern mismatch");
        Expect(HasIssue(result, DataValidation.IssueKind.UnknownField, "colour"), "unknown field colour");
        ExpectEqual(result.GetIssueCount(), 6, "six issues for the dictionary");

        data.Remove("colour");
        data["id"] = "abc";
        result = schema.Validate(data);
        Expect(HasIssue(result, DataValidation.IssueKind.TypeMismatch, "id"), "id is not an int");
        Expect(!HasIssue(result, DataValidation.IssueKind.UnknownField, "colour"), "colour is gone");

        schema.ExtraFields = DataSchema.ExtraFieldsEnum.Allow;
        data["colour"] = "red";
        Expect(!HasIssue(schema.Validate(data), DataValidation.IssueKind.UnknownField, "colour"), "extra_fields allow");

        // Options: stop_on_first and max_issues.
        var limited = new Godot.Collections.Dictionary { ["hp"] = 500, ["code"] = "?" };
        DataSchema strict = ItemSchema();
        var options = new DataValidationOptions();
        options.StopOnFirst = true;
        ExpectEqual(strict.Validate(limited, options).GetIssueCount(), 1, "stop_on_first gives one issue");
        options.StopOnFirst = false;
        options.MaxIssues = 2;
        ExpectEqual(strict.Validate(limited, options).GetIssueCount(), 2, "max_issues caps the issues");
        Expect(strict.Validate(limited).GetIssueCount() > 2, "without options there are more issues");
    }

    private void CheckJson()
    {
        string text = FileAccess.GetFileAsString(Res + "items.json").Replace("\r\n", "\n");
        Variant data = Json.ParseString(text);
        Expect(data.VariantType == Variant.Type.Array, "items.json parses to an Array");
        if (data.VariantType != Variant.Type.Array)
        {
            return;
        }
        var stats = new Godot.Collections.Dictionary { ["atk"] = T(Variant.Type.Int) };
        var drop = new Godot.Collections.Dictionary { ["item"] = T(Variant.Type.String), ["count"] = T(Variant.Type.Int) };
        var shape = new Godot.Collections.Dictionary
        {
            ["id"] = T(Variant.Type.Int),
            ["name"] = T(Variant.Type.String),
            ["stats"] = stats,
            ["tags"] = new Godot.Collections.Array { T(Variant.Type.String) },
            ["drops"] = new Godot.Collections.Array { drop },
        };
        DataSchema schema = DataSchema.CreateFromDictionary(shape);
        ExpectEqual(schema.Fields.Count, 5, "CreateFromDictionary() makes five fields");
        Expect(schema.GetField("id").Required, "created fields are required");
        schema.GetField("id").Unique = true;
        DataValidation result = schema.Validate(data);
        Expect(HasIssue(result, DataValidation.IssueKind.DuplicateKey, "id", 1), "duplicate id in record 1");
        Expect(HasIssue(result, DataValidation.IssueKind.TypeMismatch, "atk", 1), "nested atk is not an int");
        Expect(HasIssue(result, DataValidation.IssueKind.TypeMismatch, "tags", 1), "tags element is not a string");
        Expect(HasIssue(result, DataValidation.IssueKind.TypeMismatch, "count", 1), "count in drops is not an int");
        ExpectEqual(result.GetIssueCount(), 4, "four issues in items.json");
        int[] invalid = result.GetInvalidRows();
        Expect(invalid.Length == 1 && invalid[0] == 1, "invalid JSON records");
        bool foundPath = false;
        for (int i = 0; i < result.GetIssueCount(); i++)
        {
            foundPath = foundPath || result.GetIssuePath(i) == "1/drops/0/count";
        }
        Expect(foundPath, "path of the nested count issue");
        result.ResolveLines(text);
        bool linesOk = true;
        for (int i = 0; i < result.GetIssueCount(); i++)
        {
            linesOk = linesOk && result.GetIssueLine(i) == 3;
        }
        Expect(linesOk, "ResolveLines() puts every issue on line 3");
        // The first record alone is valid.
        Godot.Collections.Array first = ((Godot.Collections.Array)data).Slice(0, 1);
        Expect(schema.Validate(first).IsValid(), "first record is valid");
    }

    private void CheckIni()
    {
        var section = new Godot.Collections.Dictionary { ["hp"] = T(Variant.Type.Int), ["name"] = T(Variant.Type.String) };
        DataField everySection = MakeField("section", Variant.Type.Dictionary);
        everySection.Schema = DataSchema.CreateFromDictionary(section);
        var schema = new DataSchema();
        schema.ExtraFields = DataSchema.ExtraFieldsEnum.MatchTemplate;
        schema.ExtraFieldTemplate = everySection;

        var ini = GD.Load<Ini>(Res + "monsters.ini");
        Expect(ini != null, "GD.Load<Ini>() of monsters.ini");
        if (ini != null)
        {
            DataValidation result = ini.Validate(schema);
            Expect(HasIssue(result, DataValidation.IssueKind.MissingField, "name"), "orc has no name");
            Expect(HasIssue(result, DataValidation.IssueKind.TypeMismatch, "hp"), "orc hp is not an int");
            ExpectEqual(result.GetIssueCount(), 2, "two issues in monsters.ini");
            bool orcHp = false;
            bool orc = false;
            for (int i = 0; i < result.GetIssueCount(); i++)
            {
                orcHp = orcHp || result.GetIssuePath(i) == "orc/hp";
                orc = orc || result.GetIssuePath(i) == "orc";
            }
            Expect(orcHp && orc, "paths orc and orc/hp");
        }

        var parsed = new Ini();
        string iniText = FileAccess.GetFileAsString(Res + "monsters.ini").Replace("\r\n", "\n");
        ExpectEqual(parsed.Parse(iniText, true), Error.Ok, "Ini.Parse() keeping the text");
        var options = new DataValidationOptions();
        options.KeepPositions = true;
        DataValidation withLines = parsed.Validate(schema, options);
        ExpectEqual(withLines.GetIssueCount(), 2, "two issues with positions");
        for (int i = 0; i < withLines.GetIssueCount(); i++)
        {
            if (withLines.GetIssueKind(i) == DataValidation.IssueKind.TypeMismatch)
            {
                ExpectEqual(withLines.GetIssuePath(i), "orc/hp", "type mismatch path");
                ExpectEqual(withLines.GetIssueLine(i), 6, "type mismatch is on line 6");
            }
            else
            {
                ExpectEqual(withLines.GetIssuePath(i), "orc", "missing field path");
                ExpectEqual(withLines.GetIssueLine(i), 5, "missing field is on line 5");
            }
        }
        // Without a template the sections are unknown fields.
        DataValidation unknown = parsed.Validate(new DataSchema());
        Expect(HasIssue(unknown, DataValidation.IssueKind.UnknownField, "goblin") && HasIssue(unknown, DataValidation.IssueKind.UnknownField, "orc"), "sections are unknown fields without a template");
    }

    private void CheckXml()
    {
        var xml = new Xml();
        ExpectEqual(xml.Parse("<items><item id=\"1\"><name>Sword</name></item><item id=\"x\"/></items>"), Error.Ok, "Xml.Parse()");
        var item = new Godot.Collections.Dictionary { ["id"] = T(Variant.Type.Int) };
        DataSchema itemSchema = DataSchema.CreateFromDictionary(item);
        itemSchema.AddField(MakeField("name", Variant.Type.String));
        DataField items = MakeField("item", Variant.Type.Array);
        items.ElementSchema = itemSchema;
        var root = new DataSchema();
        root.AddField(items);
        DataValidation result = xml.Validate(root);
        ExpectEqual(result.GetIssueCount(), 1, "one XML issue");
        if (result.GetIssueCount() == 1)
        {
            ExpectEqual(result.GetIssueKind(0), DataValidation.IssueKind.TypeMismatch, "XML attribute is not an int");
            ExpectEqual(result.GetIssuePath(0), "item/1/id", "XML issue path");
        }
    }

    private static Godot.Collections.Dictionary NestedShape()
    {
        return new Godot.Collections.Dictionary
        {
            ["name"] = T(Variant.Type.String),
            ["pos"] = new Godot.Collections.Dictionary { ["x"] = T(Variant.Type.Int) },
            ["tags"] = new Godot.Collections.Array { T(Variant.Type.String) },
        };
    }

    private void CheckNestedAndYaml()
    {
        DataSchema schema = DataSchema.CreateFromDictionary(NestedShape());
        ExpectEqual(schema.Fields.Count, 3, "nested schema has three fields");
        ExpectEqual(schema.FindField("pos"), 1, "pos is the second field");
        ExpectEqual(schema.GetField("pos").Schema.GetField("x").Type, Variant.Type.Int, "nested field type");
        ExpectEqual(schema.GetField("tags").ElementType, Variant.Type.String, "array element type");
        var good = new Godot.Collections.Dictionary
        {
            ["name"] = "a",
            ["pos"] = new Godot.Collections.Dictionary { ["x"] = 1 },
            ["tags"] = new Godot.Collections.Array { "t" },
        };
        Expect(schema.Validate(good).IsValid(), "nested dictionary is valid");
        var badData = new Godot.Collections.Dictionary
        {
            ["name"] = "a",
            ["pos"] = new Godot.Collections.Dictionary { ["x"] = "no" },
            ["tags"] = new Godot.Collections.Array { "t", 2 },
        };
        DataValidation bad = schema.Validate(badData);
        Expect(HasIssue(bad, DataValidation.IssueKind.TypeMismatch, "x"), "nested x is not an int");
        Expect(HasIssue(bad, DataValidation.IssueKind.TypeMismatch, "tags"), "array element is not a string");
        schema.RemoveField(0);
        ExpectEqual(schema.FindField("name"), -1, "RemoveField()");

        var yaml = new Yaml();
        ExpectEqual(yaml.Parse("name: Hero\npos:\n  x: 1\ntags:\n  - a\n  - b\n"), Error.Ok, "Yaml.Parse() of a valid document");
        DataSchema full = DataSchema.CreateFromDictionary(NestedShape());
        Expect(yaml.Validate(full).IsValid(), "valid YAML document");
        var broken = new Yaml();
        broken.Parse("name: Hero\npos:\n  x: nope\ntags:\n  - a\n");
        DataValidation result = broken.Validate(full);
        Expect(!result.IsValid(), "invalid YAML document");
        Expect(HasIssue(result, DataValidation.IssueKind.TypeMismatch, "x"), "YAML nested x is not an int");
        var missing = new Yaml();
        missing.Parse("name: Hero\n");
        DataValidation missingResult = missing.Validate(full);
        Expect(HasIssue(missingResult, DataValidation.IssueKind.MissingField, "pos"), "YAML missing pos");
        Expect(HasIssue(missingResult, DataValidation.IssueKind.MissingField, "tags"), "YAML missing tags");
    }

    private void CheckTypedCsv()
    {
        DataSchema schema = ItemSchema();
        var csv = new Csv();
        ExpectEqual(csv.Parse("id,name,hp\n1,Sword,20\n2,Axe,\n"), Error.Ok, "Csv.Parse() for typed access");
        Godot.Collections.Dictionary row = csv.GetTypedRow(1, schema);
        ExpectEqual((long)row["id"], 2L, "typed id is an int");
        ExpectEqual((string)row["name"], "Axe", "typed name");
        ExpectEqual((long)row["hp"], 10L, "empty cell gives the default value");
        ExpectEqual((long)csv.GetTypedRow(0, schema)["hp"], 20L, "typed hp");
        Variant ids = csv.GetTypedColumn(0, schema);
        Expect(ids.VariantType == Variant.Type.PackedInt64Array, "GetTypedColumn() of an int field is a PackedInt64Array");
        if (ids.VariantType == Variant.Type.PackedInt64Array)
        {
            long[] idArray = ids.AsInt64Array();
            Expect(idArray.Length == 2 && idArray[0] == 1 && idArray[1] == 2, "typed id column values");
        }
        Variant names = csv.GetTypedColumn(1, schema);
        Expect(names.VariantType == Variant.Type.PackedStringArray, "GetTypedColumn() of a String field is a PackedStringArray");

        var values = new Godot.Collections.Dictionary { ["id"] = 3, ["name"] = "Bow", ["hp"] = 30.0 };
        ExpectEqual(csv.AppendTypedRow(values, schema), Error.Ok, "AppendTypedRow()");
        string[] appended = csv.GetRow(2);
        Expect(appended.Length == 3 && appended[0] == "3" && appended[1] == "Bow" && appended[2] == "30", "appended row is written plainly");
        values["hp"] = "lots";
        ExpectEqual(csv.AppendTypedRow(values, schema), Error.InvalidData, "AppendTypedRow() of an invalid value");
        ExpectEqual(csv.GetRowCount(), 3, "the invalid row is not added");
    }

    private void CheckManyRows()
    {
        const int count = 100000;
        var builder = new StringBuilder("id,name,hp\n");
        for (int i = 0; i < count; i++)
        {
            builder.Append(i).Append(",Item ").Append(i).Append(',').Append(i % 100).Append('\n');
        }
        var csv = new Csv();
        ExpectEqual(csv.Parse(builder.ToString()), Error.Ok, $"parse of {count} rows");
        ExpectEqual(csv.GetRowCount(), count, "row count of the big CSV");
        DataSchema schema = GD.Load<DataSchema>(Res + "items_schema.tres") ?? ItemSchema();
        ulong start = Time.GetTicksMsec();
        DataValidation result = csv.Validate(schema);
        ulong elapsed = Time.GetTicksMsec() - start;
        GD.Print($"  INFO  {count} rows validated in {elapsed} ms on {OS.GetName()}");
        Expect(result.IsValid(), $"{count} valid rows validate ({result.GetIssueCount()} issues)");
    }
}
