using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.DataSchemas;

/// <summary>
/// Full API test of Data Schemas: every method, property and constant of DataSchema, DataField, DataValidation and
/// DataValidationOptions, Validate() of every text format, line numbers and big data. Same tests as scripts/gd/Test.gd.
/// items.csv has a header, valid rows, invalid rows and one quoted cell over two lines. Rows are 0-based data rows
/// (header excluded), lines are 1-based.
/// </summary>
public partial class Test : FeatureTest
{
    private const string Res = "res://data-schemas/resources/";
    private const string Units = "res://csv/resources/units.csv";
    private const string UnitsSchema = "res://csv/resources/units_schema.tres";
    private const int ManyRows = 100000;
    private const int JsonRecords = 3000;
    private const int JsonMs = 2000;
    private const int IssueCap = 200;

    protected override Task RunTests()
    {
        TestDefaults();
        TestConstants();
        TestFieldSetters();
        TestEnumValues();
        TestSchemaResource();
        TestCsvFile();
        TestCsvPositions();
        TestCsvColumns();
        TestDictionary();
        TestOptions();
        TestJson();
        TestIni();
        TestXml();
        TestNestedAndYaml();
        TestAddIssue();
        TestFilePatterns();
        TestIssueCap();
        TestJsonLines();
        TestManyRows();
        return Task.CompletedTask;
    }

    /// <summary>id (int, unique, required), name (not empty), rarity (enum), hp (0..100, default 10), code (pattern): items_schema.tres in code.</summary>
    private static DataSchema ItemSchema()
    {
        var shape = new Dictionary
        {
            ["id"] = (int)Variant.Type.Int, ["name"] = (int)Variant.Type.String, ["rarity"] = (int)Variant.Type.String,
            ["hp"] = (int)Variant.Type.Int, ["code"] = (int)Variant.Type.String,
        };
        DataSchema schema = DataSchema.CreateFromDictionary(shape);
        foreach (DataField field in schema.Fields)
        {
            field.Required = field.Name == "id";
        }
        schema.GetField("id").Unique = true;
        schema.GetField("name").AllowEmpty = false;
        schema.GetField("rarity").SetEnumValues(["common", "rare"]);
        schema.GetField("hp").SetRange(0, 100);
        schema.GetField("hp").DefaultValue = 10;
        schema.GetField("code").Pattern = "[A-Z]{2}[0-9]+";
        return schema;
    }

    private static Dictionary Shape(params (string Name, Variant Value)[] fields)
    {
        var shape = new Dictionary();
        foreach ((string name, Variant value) in fields)
        {
            shape[name] = value;
        }
        return shape;
    }

    private static bool HasIssue(DataValidation result, DataValidation.IssueKind kind, string field, int row = -1)
    {
        return Enumerable.Range(0, result.GetIssueCount())
            .Any(i => result.GetIssueKind(i) == kind && result.GetIssueField(i) == field && result.GetIssueRow(i) == row);
    }

    private static string[] Paths(DataValidation result)
    {
        return Enumerable.Range(0, result.GetIssueCount()).Select(result.GetIssuePath).ToArray();
    }

    private void TestDefaults()
    {
        var field = new DataField();
        ExpectEqual((string)field.Name, "", "DataField.Name defaults to empty");
        ExpectEqual(field.Type, Variant.Type.String, "DataField.Type defaults to String");
        ExpectEqual(field.Hint, PropertyHint.None, "DataField.Hint defaults to none");
        ExpectEqual(field.HintString, "", "DataField.HintString defaults to empty");
        Expect(!field.Required, "DataField.Required defaults to false");
        Expect(!field.Unique, "DataField.Unique defaults to false");
        Expect(field.AllowEmpty, "DataField.AllowEmpty defaults to true");
        ExpectEqual(field.DefaultValue.VariantType, Variant.Type.Nil, "DataField.DefaultValue defaults to null");
        ExpectEqual(field.Description, "", "DataField.Description defaults to empty");
        ExpectEqual(field.Pattern, "", "DataField.Pattern defaults to empty");
        Expect(field.Schema == null, "DataField.Schema defaults to null");
        Expect(field.ElementSchema == null, "DataField.ElementSchema defaults to null");
        ExpectEqual(field.ElementType, Variant.Type.Nil, "DataField.ElementType defaults to Nil");
        var schema = new DataSchema();
        ExpectEqual(schema.Fields.Count, 0, "DataSchema.Fields defaults to empty");
        ExpectEqual(schema.ExtraFields, DataSchema.ExtraFieldsEnum.Reject, "DataSchema.ExtraFields defaults to Reject");
        Expect(schema.ExtraFieldTemplate == null, "DataSchema.ExtraFieldTemplate defaults to null");
        ExpectEqual(schema.FilePatterns.Length, 0, "DataSchema.FilePatterns defaults to empty");
        var options = new DataValidationOptions();
        ExpectEqual(options.MaxIssues, 1000, "DataValidationOptions.MaxIssues defaults to 1000");
        Expect(!options.StopOnFirst, "DataValidationOptions.StopOnFirst defaults to false");
        Expect(!options.KeepPositions, "DataValidationOptions.KeepPositions defaults to false");
        Expect(new DataValidation().IsValid(), "a new DataValidation is valid");
    }

    private void TestConstants()
    {
        ExpectEqual((int)DataSchema.ExtraFieldsEnum.Allow, 1, "ExtraFields.Allow");
        ExpectEqual((int)DataSchema.ExtraFieldsEnum.MatchTemplate, 2, "ExtraFields.MatchTemplate");
        ExpectEqual((int)DataSchema.ExtraFieldsEnum.Max, 3, "ExtraFields.Max");
        DataValidation.IssueKind[] kinds =
        [
            DataValidation.IssueKind.MissingField, DataValidation.IssueKind.UnknownField, DataValidation.IssueKind.TypeMismatch,
            DataValidation.IssueKind.OutOfRange, DataValidation.IssueKind.NotInEnum, DataValidation.IssueKind.PatternMismatch,
            DataValidation.IssueKind.EmptyRequired, DataValidation.IssueKind.DuplicateKey, DataValidation.IssueKind.ParseError,
            DataValidation.IssueKind.InvalidLocale, DataValidation.IssueKind.Max,
        ];
        Expect(kinds.Select(kind => (int)kind).SequenceEqual(Enumerable.Range(0, 11)), "IssueKind values are 0 to 9, Max is 10");
        ExpectEqual((int)DataValidation.PositionNone, -1, "PositionNone is -1");
    }

    private void TestFieldSetters()
    {
        var field = new DataField { Description = "Hit points." };
        ExpectEqual(field.Description, "Hit points.", "Description");
        field.SetRange(1, 200);
        ExpectEqual(field.Hint, PropertyHint.Range, "SetRange() sets the hint Range");
        ExpectEqual(field.HintString, "1.0,200.0,1.0", "SetRange() writes min, max and step");
        field.SetEnumValues(["warrior", "ranger"]);
        ExpectEqual(field.Hint, PropertyHint.Enum, "SetEnumValues() sets the hint Enum");
        ExpectEqual(field.HintString, "warrior,ranger", "SetEnumValues() writes the names");
    }

    private void TestEnumValues()
    {
        var quality = new DataField { Name = "quality", Type = Variant.Type.Int, Hint = PropertyHint.Enum, HintString = "Low,High:5" };
        var schema = new DataSchema();
        schema.AddField(quality);
        Expect(schema.Validate(new Dictionary { ["quality"] = 0 }).IsValid(), "an int enum \"Low,High:5\": Low is 0");
        Expect(schema.Validate(new Dictionary { ["quality"] = 5 }).IsValid(), "an int enum \"Low,High:5\": High is 5");
        Expect(!schema.Validate(new Dictionary { ["quality"] = 1 }).IsValid(), "an int enum \"Low,High:5\": 1 is not a value");
    }

    private void TestSchemaResource()
    {
        var schema = GD.Load<DataSchema>(Res + "items_schema.tres");
        ExpectEqual(schema.Fields.Count, 5, "items_schema.tres has five fields");
        ExpectEqual(schema.FindField("hp"), 3, "FindField()");
        ExpectEqual(schema.FindField("missing"), -1, "FindField() of a missing field");
        Expect(schema.GetField("missing") == null, "GetField() of a missing field is null");
        DataField idField = schema.GetField("id");
        Expect(idField.Required, "id is required");
        Expect(idField.Unique, "id is unique");
        DataField hp = schema.GetField("hp");
        ExpectEqual(hp.HintString, "0,100,1", "hp has the range 0 to 100");
        ExpectEqual((int)hp.DefaultValue, 10, "hp has the default 10");
        ExpectEqual(schema.GetField("rarity").HintString, "common,rare", "rarity is an enum");
        Expect(!schema.GetField("name").AllowEmpty, "name must not be empty");
        ExpectEqual((int)schema.ConvertValue("hp", "42"), 42, "ConvertValue() to int");
        ExpectEqual((string)schema.ConvertValue("name", "abc"), "abc", "ConvertValue() to String");
    }

    private void TestCsvFile()
    {
        var schema = GD.Load<DataSchema>(Res + "items_schema.tres");
        var csv = new Csv();
        ExpectEqual(csv.Load(Res + "items.csv"), Error.Ok, "items.csv loads");
        ExpectEqual(csv.GetRowCount(), 4, "items.csv has four data rows");
        DataValidation result = csv.Validate(schema);
        Expect(!result.IsValid(), "items.csv is not valid");
        Expect(HasIssue(result, DataValidation.IssueKind.DuplicateKey, "id", 2), "duplicate id in row 2");
        Expect(HasIssue(result, DataValidation.IssueKind.EmptyRequired, "name", 2), "empty name in row 2");
        Expect(HasIssue(result, DataValidation.IssueKind.NotInEnum, "rarity", 2), "rarity epic in row 2");
        Expect(HasIssue(result, DataValidation.IssueKind.TypeMismatch, "hp", 2), "hp x in row 2");
        Expect(HasIssue(result, DataValidation.IssueKind.PatternMismatch, "code", 2), "code ab in row 2");
        Expect(HasIssue(result, DataValidation.IssueKind.OutOfRange, "hp", 3), "hp 500 in row 3");
        ExpectEqual(result.GetIssueCount(), 6, "six issues in items.csv");
        Expect(result.GetInvalidRows().SequenceEqual([2, 3]), "GetInvalidRows()");
        ExpectEqual(result.GetIssueMessage(0), "\"id\": the value \"2\" is already used by entry 1.", "GetIssueMessage()");
        ExpectEqual(result.GetIssueLine(0), (int)DataValidation.PositionNone, "no line without KeepPositions");
    }

    private void TestCsvPositions()
    {
        string text = FileAccess.GetFileAsString(Res + "items.csv");
        var csv = new Csv();
        ExpectEqual(csv.Parse(text, true), Error.Ok, "Parse() with keepText");
        var options = new DataValidationOptions { KeepPositions = true };
        DataValidation result = csv.Validate(ItemSchema(), options);
        ExpectEqual(result.GetIssueCount(), 6, "six issues with positions");
        // Row 1 spans lines 3 and 4, so row 2 is on line 5 and row 3 on line 6.
        for (int i = 0; i < result.GetIssueCount(); i++)
        {
            int row = result.GetIssueRow(i);
            int line = result.GetIssueLine(i);
            ExpectEqual(line, row + 3, $"the issue of row {row} is on line {row + 3}");
        }
        ExpectEqual(result.GetIssueColumn(5), 3, "hp is column 3");

        var extra = new Csv();
        extra.Parse("id,name,colour\n1,Sword,red\n");
        DataValidation extraResult = extra.Validate(ItemSchema());
        ExpectEqual(extraResult.GetIssueCount(), 1, "an unknown column is the only issue");
        Expect(HasIssue(extraResult, DataValidation.IssueKind.UnknownField, "colour"), "unknown column colour");
        var missing = new Csv();
        missing.Parse("name\nSword\n");
        Expect(HasIssue(missing.Validate(ItemSchema()), DataValidation.IssueKind.MissingField, "id"), "missing required column id");
        var broken = new Csv();
        broken.Parse("id\n\"open\n");
        DataValidation brokenResult = broken.Validate(ItemSchema());
        ExpectEqual(brokenResult.GetIssueCount(), 1, "a parse error is the only issue");
        ExpectEqual(brokenResult.GetIssueKind(0), DataValidation.IssueKind.ParseError, "IssueKind.ParseError");
        var valid = new Csv();
        valid.Parse("id,name,rarity,hp,code\n1,Sword,common,10,AB1\n2,Axe,rare,,CD2\n");
        Expect(valid.Validate(ItemSchema()).IsValid(), "a valid CSV is valid (an empty hp is fine)");
    }

    private void TestCsvColumns()
    {
        var units = new Csv();
        units.Load(Units);
        var schema = GD.Load<DataSchema>(UnitsSchema);
        DataValidation result = units.Validate(schema);
        Expect(result.GetInvalidRows().SequenceEqual([3]), "units.csv: only the ghost row is invalid");
        ExpectEqual(result.GetIssueColumn(0), 1, "class is column 1");
        ExpectEqual(result.GetIssueColumn(1), 2, "hp is column 2");
        ExpectEqual(result.GetIssueColumn(2), 3, "ranged is column 3");
        var reordered = new Csv();
        reordered.Parse("ranged,hp,class,name\nmaybe,60,ranger,archer\n");
        DataValidation reorderedResult = reordered.Validate(schema);
        ExpectEqual(reorderedResult.GetIssueColumn(0), 0, "columns map to fields by header name, not order");
    }

    private void TestDictionary()
    {
        DataSchema schema = ItemSchema();
        var data = new Dictionary { ["name"] = "", ["rarity"] = "epic", ["hp"] = 150, ["code"] = "x1", ["colour"] = "red" };
        DataValidation result = schema.Validate(data);
        Expect(HasIssue(result, DataValidation.IssueKind.MissingField, "id"), "missing field id");
        Expect(HasIssue(result, DataValidation.IssueKind.EmptyRequired, "name"), "empty name");
        Expect(HasIssue(result, DataValidation.IssueKind.NotInEnum, "rarity"), "rarity not in enum");
        Expect(HasIssue(result, DataValidation.IssueKind.OutOfRange, "hp"), "hp out of range");
        Expect(HasIssue(result, DataValidation.IssueKind.PatternMismatch, "code"), "code pattern mismatch");
        Expect(HasIssue(result, DataValidation.IssueKind.UnknownField, "colour"), "unknown field colour");
        ExpectEqual(result.GetIssueCount(), 6, "six issues for the Dictionary");
        data["id"] = "abc";
        DataValidation typed = schema.Validate(data);
        Expect(HasIssue(typed, DataValidation.IssueKind.TypeMismatch, "id"), "id is not an int");
        schema.ExtraFields = DataSchema.ExtraFieldsEnum.Allow;
        DataValidation allowed = schema.Validate(data);
        Expect(!HasIssue(allowed, DataValidation.IssueKind.UnknownField, "colour"), "ExtraFields.Allow accepts unknown fields");
    }

    private void TestOptions()
    {
        DataSchema schema = ItemSchema();
        var data = new Dictionary { ["hp"] = 500, ["code"] = "?" };
        var options = new DataValidationOptions { StopOnFirst = true };
        ExpectEqual(schema.Validate(data, options).GetIssueCount(), 1, "StopOnFirst gives one issue");
        options.StopOnFirst = false;
        options.MaxIssues = 2;
        ExpectEqual(schema.Validate(data, options).GetIssueCount(), 2, "MaxIssues caps the issues");
        ExpectEqual(schema.Validate(data).GetIssueCount(), 3, "without options all three issues");
        var records = new Array();
        for (int i = 0; i < 1500; i++)
        {
            records.Add(new Dictionary { ["id"] = "x" });
        }
        DataSchema ids = DataSchema.CreateFromDictionary(Shape(("id", (int)Variant.Type.Int)));
        ExpectEqual(ids.Validate(records).GetIssueCount(), 1000, "the default MaxIssues is 1000");
        options.MaxIssues = -1;
        ExpectEqual(ids.Validate(records, options).GetIssueCount(), 1500, "MaxIssues = -1 keeps all issues");
    }

    private void TestJson()
    {
        string text = FileAccess.GetFileAsString(Res + "items.json");
        Array data = Json.ParseString(text).AsGodotArray();
        Dictionary stats = Shape(("atk", (int)Variant.Type.Int));
        Dictionary drop = Shape(("item", (int)Variant.Type.String), ("count", (int)Variant.Type.Int));
        Dictionary shape = Shape(("id", (int)Variant.Type.Int), ("name", (int)Variant.Type.String), ("stats", stats),
            ("tags", new Array { (int)Variant.Type.String }), ("drops", new Array { drop }));
        DataSchema schema = DataSchema.CreateFromDictionary(shape);
        ExpectEqual(schema.Fields.Count, 5, "CreateFromDictionary() makes five fields");
        Expect(schema.GetField("id").Required, "CreateFromDictionary() fields are required");
        ExpectEqual(schema.GetField("stats").Type, Variant.Type.Dictionary, "a Dictionary value is a nested schema");
        ExpectEqual(schema.GetField("tags").ElementType, Variant.Type.String, "an Array of a type is a list");
        Expect(schema.GetField("drops").ElementSchema != null, "an Array of a Dictionary is a list of records");
        schema.GetField("id").Unique = true;
        DataValidation result = schema.Validate(data);
        Expect(HasIssue(result, DataValidation.IssueKind.DuplicateKey, "id", 1), "duplicate id in record 1");
        Expect(HasIssue(result, DataValidation.IssueKind.TypeMismatch, "atk", 1), "the nested atk is not an int");
        Expect(HasIssue(result, DataValidation.IssueKind.TypeMismatch, "tags", 1), "a tags element is not a String");
        Expect(HasIssue(result, DataValidation.IssueKind.TypeMismatch, "count", 1), "count in drops is not an int");
        ExpectEqual(result.GetIssueCount(), 4, "four issues in items.json");
        Expect(result.GetInvalidRows().SequenceEqual([1]), "the index of a JSON array is the row");
        Expect(Paths(result).Contains("1/drops/0/count"), "GetIssuePath() of the nested count");
        result.ResolveLines(text);
        for (int i = 0; i < result.GetIssueCount(); i++)
        {
            ExpectEqual(result.GetIssueLine(i), 3, $"ResolveLines(): issue {i} is on line 3");
        }
        Expect(schema.Validate(data.Slice(0, 1)).IsValid(), "the first record alone is valid");
    }

    private void TestIni()
    {
        var section = new DataField
        {
            Name = "section",
            Type = Variant.Type.Dictionary,
            Schema = DataSchema.CreateFromDictionary(Shape(("hp", (int)Variant.Type.Int), ("name", (int)Variant.Type.String))),
        };
        var schema = new DataSchema { ExtraFields = DataSchema.ExtraFieldsEnum.MatchTemplate, ExtraFieldTemplate = section };
        var monsters = GD.Load<Ini>(Res + "monsters.ini");
        DataValidation result = monsters.Validate(schema);
        ExpectEqual(result.GetIssueCount(), 2, "two issues in monsters.ini");
        Expect(Paths(result).SequenceEqual(["orc/hp", "orc"]), "the paths orc/hp (type) and orc (missing name)");

        string text = FileAccess.GetFileAsString(Res + "monsters.ini");
        var parsed = new Ini();
        parsed.Parse(text, true);
        var options = new DataValidationOptions { KeepPositions = true };
        DataValidation withLines = parsed.Validate(schema, options);
        ExpectEqual(withLines.GetIssueLine(0), 6, "the type mismatch of orc/hp is on line 6");
        ExpectEqual(withLines.GetIssueLine(1), 5, "the missing name of orc is on the header line 5");
        DataValidation unknown = parsed.Validate(new DataSchema());
        Expect(HasIssue(unknown, DataValidation.IssueKind.UnknownField, "goblin"), "without a template the sections are unknown fields");
    }

    private void TestXml()
    {
        var xml = new Xml();
        xml.Parse("<items><item id=\"1\"><name>Sword</name></item><item id=\"x\"/></items>");
        DataSchema item = DataSchema.CreateFromDictionary(Shape(("id", (int)Variant.Type.Int)));
        item.AddField(new DataField { Name = "name" });
        var items = new DataField { Name = "item", Type = Variant.Type.Array, ElementSchema = item };
        var root = new DataSchema();
        root.AddField(items);
        DataValidation result = xml.Validate(root);
        ExpectEqual(result.GetIssueCount(), 1, "one XML issue");
        ExpectEqual(result.GetIssueKind(0), DataValidation.IssueKind.TypeMismatch, "an attribute that is not an int");
        ExpectEqual(result.GetIssuePath(0), "item/1/id", "repeated children are an Array: path item/1/id");
    }

    private void TestNestedAndYaml()
    {
        Dictionary shape = Shape(("name", (int)Variant.Type.String), ("pos", Shape(("x", (int)Variant.Type.Int))), ("tags", new Array { (int)Variant.Type.String }));
        DataSchema schema = DataSchema.CreateFromDictionary(shape);
        ExpectEqual(schema.GetField("pos").Schema.GetField("x").Type, Variant.Type.Int, "a nested field type");
        var good = new Dictionary { ["name"] = "a", ["pos"] = new Dictionary { ["x"] = 1 }, ["tags"] = new Array { "t" } };
        Expect(schema.Validate(good).IsValid(), "a valid nested Dictionary");
        var badData = new Dictionary { ["name"] = "a", ["pos"] = new Dictionary { ["x"] = "no" }, ["tags"] = new Array { "t", 2 } };
        DataValidation bad = schema.Validate(badData);
        Expect(HasIssue(bad, DataValidation.IssueKind.TypeMismatch, "x"), "the nested x is not an int");
        Expect(HasIssue(bad, DataValidation.IssueKind.TypeMismatch, "tags"), "an array element is not a String");
        schema.RemoveField(0);
        ExpectEqual(schema.FindField("name"), -1, "RemoveField()");

        DataSchema full = DataSchema.CreateFromDictionary(shape);
        var yaml = new Yaml();
        yaml.Parse("name: Hero\npos:\n  x: 1\ntags:\n  - a\n  - b\n");
        Expect(yaml.Validate(full).IsValid(), "a valid YAML document");
        yaml.Parse("name: Hero\npos:\n  x: nope\ntags:\n  - a\n");
        Expect(HasIssue(yaml.Validate(full), DataValidation.IssueKind.TypeMismatch, "x"), "YAML: the nested x is not an int");
        yaml.Parse("name: Hero\n");
        DataValidation missing = yaml.Validate(full);
        Expect(HasIssue(missing, DataValidation.IssueKind.MissingField, "pos"), "YAML: missing pos");
        Expect(HasIssue(missing, DataValidation.IssueKind.MissingField, "tags"), "YAML: missing tags");
        var toml = new Toml();
        toml.Parse("name = \"Hero\"\ntags = [\"a\"]\n[pos]\nx = 1\n");
        Expect(toml.Validate(full).IsValid(), "a valid TOML document");
    }

    private void TestAddIssue()
    {
        var result = new DataValidation();
        result.AddIssue(DataValidation.IssueKind.NotInEnum, "\"gem\" is no item.", "drop", "3/drop", 3);
        result.AddIssue(DataValidation.IssueKind.NotInEnum, "\"ore\" is no item.", "drop", "1/drop", 1, 2, 7);
        result.AddIssue(DataValidation.IssueKind.NotInEnum, "\"gem\" is no item.", "drop", "3/drop", 3);
        Expect(!result.IsValid(), "AddIssue() makes the result invalid");
        ExpectEqual(result.GetIssueCount(), 3, "AddIssue() adds an issue");
        ExpectEqual(result.GetIssueMessage(1), "\"ore\" is no item.", "AddIssue(): message");
        ExpectEqual((string)result.GetIssueField(1), "drop", "AddIssue(): field");
        ExpectEqual(result.GetIssuePath(1), "1/drop", "AddIssue(): path");
        ExpectEqual(result.GetIssueRow(1), 1, "AddIssue(): row");
        ExpectEqual(result.GetIssueColumn(1), 2, "AddIssue(): column");
        ExpectEqual(result.GetIssueLine(1), 7, "AddIssue(): line");
        ExpectEqual(result.GetIssueColumn(0), (int)DataValidation.PositionNone, "AddIssue() without a column");
        Expect(result.GetInvalidRows().SequenceEqual([1, 3]), "GetInvalidRows() lists each row once, sorted");
    }

    private void TestFilePatterns()
    {
        var schema = GD.Load<DataSchema>(Res + "items_schema.tres");
        Expect(schema.FilePatterns.SequenceEqual(["res://data-schemas/resources/items.csv"]), "FilePatterns");
        var units = GD.Load<DataSchema>(UnitsSchema);
        Expect(Units.Match(units.FilePatterns[0]), "the pattern of units_schema.tres matches units.csv");
        Expect(!"res://other/units.csv".Match(units.FilePatterns[0]), "the pattern does not match another path");
        ExpectEqual(ProjectSettings.GetSetting("filesystem/data/schema_files").VariantType, Variant.Type.PackedStringArray, "the project setting filesystem/data/schema_files");
    }

    private void TestIssueCap()
    {
        var lines = new System.Collections.Generic.List<string> { "id,name,hp" };
        for (int i = 0; i < 500; i++)
        {
            lines.Add($"{i},Item {i},500");
        }
        var csv = new Csv();
        csv.Parse(string.Join("\n", lines) + "\n", true);
        var options = new DataValidationOptions { KeepPositions = true, MaxIssues = IssueCap };
        DataValidation result = csv.Validate(ItemSchema(), options);
        ExpectEqual(result.GetIssueCount(), IssueCap, "MaxIssues caps the issues of 500 rows at 200");
        ExpectEqual(result.GetIssueLine(IssueCap - 1), IssueCap + 1, "the capped issues have their lines");
    }

    private void TestJsonLines()
    {
        DataSchema schema = ItemSchema();
        var records = new string[JsonRecords];
        for (int i = 0; i < JsonRecords; i++)
        {
            int hp = i % 3 == 0 ? 500 : 10;
            records[i] = $"{{\"id\": {i}, \"name\": \"Item {i}\", \"hp\": {hp}}}";
        }
        string text = "[\n" + string.Join(",\n", records) + "\n]\n";
        Variant data = Json.ParseString(text);
        var options = new DataValidationOptions { MaxIssues = -1 };
        ulong start = Time.GetTicksMsec();
        DataValidation result = schema.Validate(data, options);
        result.ResolveLines(text);
        ulong elapsed = Time.GetTicksMsec() - start;
        ExpectEqual(result.GetIssueCount(), JsonRecords / 3, "1000 issues in 3000 JSON records");
        ExpectEqual(result.GetIssueLine(999), result.GetIssueRow(999) + 2, "ResolveLines(): line = row + 2");
        Expect(elapsed < JsonMs, $"Validate() and ResolveLines() of 3000 records under {JsonMs} ms ({elapsed} ms)");
    }

    private void TestManyRows()
    {
        var lines = new string[ManyRows + 1];
        lines[0] = "id,name,hp";
        for (int i = 0; i < ManyRows; i++)
        {
            lines[i + 1] = $"{i},Item {i},{i % 100}";
        }
        var csv = new Csv();
        csv.Parse(string.Join("\n", lines) + "\n");
        ulong start = Time.GetTicksMsec();
        DataValidation result = csv.Validate(ItemSchema());
        GD.Print($"  INFO  {ManyRows} rows validated in {Time.GetTicksMsec() - start} ms");
        Expect(result.IsValid(), "100 000 valid rows validate");
    }
}
