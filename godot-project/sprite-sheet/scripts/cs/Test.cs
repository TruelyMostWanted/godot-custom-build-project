using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.SpriteSheet;

using SpriteSheet = Godot.SpriteSheet;

/// <summary>Full API test of Sprite Sheet (SpriteSheet): every method and property. Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string Dir = "res://sprite-sheet/resources/";
    private const string SheetPath = Dir + "sheet.png";
    private const string UserDir = "user://sprite_sheet_cs/";
    private const string Prefix = "spritesheetcs://";
    private static readonly string[] FileOrder = ["coin", "sword", "walk_0", "walk_1", "walk_2", "walk_10"];
    private static readonly Rect2I Sword = new(16, 0, 24, 48);
    private static readonly Rect2I TrimmedSwordMargin = new(4, 8, 8, 16);

    private Texture2D _sheet;

    protected override async Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(UserDir);
        _sheet = GD.Load<Texture2D>(SheetPath);
        TestRegions();
        TestRegionNames();
        TestRegionTexture();
        await TestNodes();
        await TestSpriteFrames();
        TestTextFormats();
        TestFiles();
        TestTexturePacker();
        TestAutoSlice();
        TestGrid();
    }

    private void TestRegions()
    {
        var atlas = new SpriteSheet { Texture = _sheet };
        ExpectEqual(atlas.SetRegion("sword", Sword), Error.Ok, "SetRegion adds a region");
        ExpectEqual(atlas.GetRegion("sword"), Sword, "GetRegion returns the rectangle");
        Expect(atlas.HasRegion("sword"), "HasRegion finds the region");
        ExpectEqual(atlas.SetRegion("outside", new Rect2I(120, 60, 16, 16)), Error.InvalidParameter, "a region outside the texture is rejected");
        ExpectEqual(atlas.SetRegion("empty", new Rect2I(0, 0, 0, 4)), Error.InvalidParameter, "a region without width is rejected");
        Expect(!atlas.HasRegion("outside"), "a rejected region is not added");
        ExpectEqual(atlas.SetRegionMargin("sword", new Rect2I(2, 2, 4, 4)), Error.Ok, "SetRegionMargin sets a margin");
        ExpectEqual(atlas.GetRegionMargin("sword"), new Rect2I(2, 2, 4, 4), "GetRegionMargin returns the margin");
        Expect(atlas.Regions.Count == 1 && atlas.Regions["sword"] == Sword, "Regions holds every rectangle");
        Expect(atlas.Margins.Count == 1 && atlas.Margins["sword"] == new Rect2I(2, 2, 4, 4), "Margins holds every margin");
        ExpectEqual(atlas.SetRegion("coin", new Rect2I(0, 0, 16, 16)), Error.Ok, "a second region is added");
        ExpectEqual(atlas.GetRegionCount(), 2, "GetRegionCount counts the regions");
        atlas.RemoveRegion("coin");
        Expect(!atlas.HasRegion("coin"), "RemoveRegion removes a region");
        atlas.ClearRegions();
        ExpectEqual(atlas.GetRegionCount(), 0, "ClearRegions removes every region");
    }

    private void TestRegionNames()
    {
        var atlas = new SpriteSheet { Texture = _sheet };
        atlas.SetRegion("b", new Rect2I(0, 0, 1, 1));
        atlas.SetRegion("a", new Rect2I(0, 0, 1, 1));
        Expect(atlas.GetRegionNames().SequenceEqual(["b", "a"]), "regions keep the insertion order");
        Expect(atlas.RegionOrder.SequenceEqual(["b", "a"]), "RegionOrder is the insertion order");
        ExpectEqual(atlas.RenameRegion("b", "c"), Error.Ok, "RenameRegion renames");
        Expect(atlas.GetRegionNames().SequenceEqual(["c", "a"]), "RenameRegion keeps the position");
        ExpectEqual(atlas.RenameRegion("c", "a"), Error.AlreadyExists, "RenameRegion to a taken name fails");
        ExpectEqual(atlas.RenameRegion("missing", "d"), Error.DoesNotExist, "RenameRegion of a missing region fails");
        Expect(SpriteSheet.IsValidRegionName("hero_walk_01"), "a plain name is valid");
        Expect(!SpriteSheet.IsValidRegionName(""), "an empty name is invalid");
        Expect(!SpriteSheet.IsValidRegionName("a/b"), "a name with / is invalid");
        Expect(!SpriteSheet.IsValidRegionName(" a"), "a name with surrounding spaces is invalid");
        ExpectEqual(atlas.SetRegion("a:b", new Rect2I(0, 0, 1, 1)), Error.InvalidParameter, "SetRegion rejects an invalid name");
    }

    private void TestRegionTexture()
    {
        var atlas = new SpriteSheet { Texture = _sheet, TexturePath = SheetPath };
        atlas.SetRegion("coin", new Rect2I(0, 0, 16, 16));
        AtlasTexture coin = atlas.GetRegionTexture("coin");
        ExpectEqual(coin.Atlas, _sheet, "GetRegionTexture returns an AtlasTexture of the sheet");
        ExpectEqual(atlas.GetRegionTexture("coin"), coin, "GetRegionTexture returns the same texture again");
        atlas.SetRegion("coin", new Rect2I(0, 0, 8, 8));
        ExpectEqual(coin.Region, new Rect2(0, 0, 8, 8), "a moved region updates the handed-out texture");
        atlas.SetRegionMargin("coin", new Rect2I(1, 2, 3, 4));
        ExpectEqual(coin.Margin, new Rect2(1, 2, 3, 4), "the region margin is the AtlasTexture margin");
        atlas.FilterClip = true;
        Expect(coin.FilterClip, "FilterClip reaches every handed-out texture");
        var gradient = new GradientTexture2D();
        atlas.Texture = gradient;
        ExpectEqual<Texture2D>(coin.Atlas, gradient, "a new texture reaches every handed-out texture");
        ExpectEqual(atlas.TexturePath, "", "setting Texture from code clears TexturePath");
    }

    private async Task TestNodes()
    {
        var atlas = new SpriteSheet { Texture = _sheet };
        atlas.SetRegion("sword", Sword);
        atlas.SetRegion("coin", new Rect2I(0, 0, 16, 16));
        var sprite = new Sprite2D { Texture = atlas.GetRegionTexture("sword") };
        AddChild(sprite);
        var rect = new TextureRect { Texture = atlas.GetRegionTexture("coin") };
        AddChild(rect);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(sprite.GetRect().Size, new Vector2(24, 48), "Sprite2D draws the 24x48 region");
        ExpectEqual(rect.GetCombinedMinimumSize(), new Vector2(16, 16), "TextureRect uses the 16x16 region");
        sprite.QueueFree();
        rect.QueueFree();
    }

    private async Task TestSpriteFrames()
    {
        var atlas = new SpriteSheet();
        atlas.LoadFromFile(Dir + "icons.atlas.json");
        SpriteFrames frames = atlas.CreateSpriteFrames("walk_", 30.0);
        Expect(frames.GetAnimationNames().SequenceEqual(["walk"]), "one animation per prefix");
        ExpectEqual(frames.GetFrameCount("walk"), 4, "four walk frames");
        ExpectEqual(frames.GetAnimationSpeed("walk"), 30.0, "the animation runs at the given fps");
        ExpectEqual<Texture2D>(frames.GetFrameTexture("walk", 3), atlas.GetRegionTexture("walk_10"), "frames are sorted by number");
        SpriteFrames allFrames = atlas.CreateSpriteFrames();
        Expect(allFrames.GetAnimationNames().SequenceEqual(["coin", "sword", "walk"]), "an empty prefix makes one animation per name group");
        var animated = new AnimatedSprite2D { SpriteFrames = frames };
        AddChild(animated);
        animated.Play("walk");
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        Expect(animated.Frame > 0, $"AnimatedSprite2D plays the frames (frame {animated.Frame})");
        animated.QueueFree();
    }

    private void TestTextFormats()
    {
        var atlas = new SpriteSheet();
        ExpectEqual(atlas.LoadFromFile(Dir + "icons.atlas.json"), Error.Ok, "LoadFromFile reads the JSON layout");
        ExpectEqual(atlas.Texture, _sheet, "TexturePath loads the imported PNG");
        ExpectEqual(atlas.TexturePath, SheetPath, "TexturePath keeps the path of the file");
        Expect(atlas.GetRegionNames().SequenceEqual(FileOrder), "regions keep the file order");
        var uidAtlas = new SpriteSheet { Texture = _sheet };
        uidAtlas.SetRegion("a", new Rect2I(0, 0, 1, 1));
        string uidJson = uidAtlas.ToText(ResourceTextSerializer.Format.Json);
        Expect(uidJson.Contains("\"TexturePath\": \"uid://"), "without TexturePath the uid:// path of the texture is written");
        var uidCopy = new SpriteSheet();
        ExpectEqual(uidCopy.LoadFromText(uidJson, ResourceTextSerializer.Format.Json), Error.Ok, "LoadFromText reads a uid:// TexturePath");
        ExpectEqual(uidCopy.Texture, _sheet, "the uid:// TexturePath loads the sheet");
        var oneLine = new SerializerOptions { Indent = "" };
        Expect(!atlas.ToText(ResourceTextSerializer.Format.Json, oneLine).Contains('\n'), "ToText uses SerializerOptions.Indent");
        Image emptyImage = Image.CreateEmpty(128, 64, false, Image.Format.Rgba8);
        ResourceSaver.Save(ImageTexture.CreateFromImage(emptyImage), UserDir + "sheet.res");
        PathPrefixServer.RegisterPrefixFolder(Prefix, UserDir);
        string json = atlas.ToText(ResourceTextSerializer.Format.Json).Replace(SheetPath, Prefix + "sheet.res");
        var prefixed = new SpriteSheet();
        ExpectEqual(prefixed.LoadFromText(json, ResourceTextSerializer.Format.Json), Error.Ok, "LoadFromText reads a prefixed TexturePath");
        ExpectEqual(prefixed.Texture.GetSize(), new Vector2(128, 64), "the texture is loaded through the path prefix");
        ExpectEqual(prefixed.ToText(ResourceTextSerializer.Format.Json), json, "the prefixed path round-trips unchanged");
        PathPrefixServer.UnregisterPrefix(Prefix);
        string broken = json.Replace("\"Width\": 24", "\"Width\": 2400");
        ExpectEqual(prefixed.LoadFromText(broken, ResourceTextSerializer.Format.Json), Error.InvalidData, "a region outside the texture fails the load");
        Expect(SameRegions(atlas, prefixed), "a failed load leaves the regions unchanged");
    }

    private void TestFiles()
    {
        var atlas = new SpriteSheet();
        atlas.LoadFromFile(Dir + "icons.atlas.json");
        atlas.SetRegionMargin("sword", new Rect2I(2, 2, 4, 4));
        foreach (string extension in new[] { "json", "csv", "ini" })
        {
            string path = UserDir + "icons." + extension;
            ExpectEqual(atlas.SaveToFile(path), Error.Ok, $"SaveToFile writes .{extension}");
            var copy = new SpriteSheet();
            copy.LoadFromFile(path);
            Expect(SameRegions(atlas, copy), $"{extension.ToUpperInvariant()} keeps regions, order and margins");
        }
        ResourceSaver.Save(atlas, UserDir + "icons.atlas.tres");
        var tres = ResourceLoader.Load<SpriteSheet>(UserDir + "icons.atlas.tres", null, ResourceLoader.CacheMode.Ignore);
        Expect(SameRegions(atlas, tres), ".tres keeps regions, order and margins");
        ExpectEqual(tres.Texture, _sheet, ".tres keeps the texture");
    }

    private void TestTexturePacker()
    {
        ExpectEqual(SpriteSheet.TexturePackerNameToRegionName("hero/walk_01.png"), "hero_walk_01", "TexturePackerNameToRegionName drops the extension and replaces /");
        var packed = new SpriteSheet();
        ExpectEqual(packed.LoadFromFile(Dir + "texturepacker_hash.json"), Error.Ok, "LoadFromFile imports the TexturePacker Hash layout");
        ExpectEqual(packed.Texture, _sheet, "meta.image is relative to the JSON file");
        Expect(packed.GetRegionNames().SequenceEqual(["items_coin", "items_sword"]), "file names become region names");
        ExpectEqual(packed.GetRegionTexture("items_sword").GetSize(), new Vector2(32, 64), "a trimmed frame keeps its original size");
        ExpectEqual(packed.GetRegionMargin("items_sword"), TrimmedSwordMargin, "the trim offset becomes the margin");
        var rotated = new SpriteSheet();
        ExpectEqual(rotated.LoadFromFile(Dir + "texturepacker_array.json"), Error.InvalidData, "a rotated frame fails the Array import");
        ExpectEqual(rotated.GetRegionCount(), 0, "a failed import changes nothing");
        string arrayText = FileAccess.GetFileAsString(Dir + "texturepacker_array.json");
        string unrotated = arrayText.Replace("\"rotated\": true", "\"rotated\": false");
        ExpectEqual(rotated.ImportTexturePackerJson(unrotated, Dir), Error.Ok, "ImportTexturePackerJson imports the Array layout");
        ExpectEqual(rotated.GetRegionCount(), 3, "every Array frame becomes a region");
    }

    private void TestAutoSlice()
    {
        var expected = new Godot.Collections.Array<Rect2I>();
        Image image = Image.CreateEmpty(240, 120, false, Image.Format.Rgba8);
        for (int i = 0; i < 50; i++)
        {
            var rect = new Rect2I(i % 10 * 24 + 1, i / 10 * 24 + 1, 4 + i % 5 * 4, 6 + i % 3 * 7);
            image.FillRect(rect, new Color(0.2f + 0.015f * i, 0.5f, 1.0f));
            expected.Add(rect);
        }
        Godot.Collections.Array<Rect2I> found = SpriteSheet.DetectSpriteRects(image);
        Expect(found.SequenceEqual(expected), "DetectSpriteRects finds 50 sprites of mixed sizes, in row order");
        Godot.Collections.Array<Rect2I> opaque = SpriteSheet.DetectSpriteRects(image, 0.99f);
        ExpectEqual(opaque.Count, 50, "a high alpha threshold finds opaque sprites too");
        var atlas = new SpriteSheet { Texture = ImageTexture.CreateFromImage(image) };
        string[] names = atlas.AutoSlice("enemy_");
        ExpectEqual(names.Length, 50, "AutoSlice adds one region per sprite");
        ExpectEqual(names[49], "enemy_49", "AutoSlice numbers the names");
        ExpectEqual(atlas.GetRegion("enemy_17"), expected[17], "AutoSlice regions have the sprite rectangles");
        Expect(atlas.AutoSlice("enemy_").Length == 0, "AutoSlice again adds nothing");
    }

    private void TestGrid()
    {
        Godot.Collections.Array<Rect2I> cells = SpriteSheet.MakeGridRects(new Vector2I(128, 64), new Vector2I(32, 32), new Vector2I(2, 2), new Vector2I(1, 1));
        Expect(cells.SequenceEqual([new Rect2I(1, 1, 32, 32), new Rect2I(35, 1, 32, 32), new Rect2I(69, 1, 32, 32)]), "MakeGridRects keeps separation and offset inside the area");
        var atlas = new SpriteSheet { Texture = _sheet };
        string[] names = atlas.AddRegions(cells, "tile_");
        Expect(names.SequenceEqual(["tile_0", "tile_1", "tile_2"]), "AddRegions adds the cells with generated names");
        ExpectEqual(atlas.MakeUniqueRegionName("tile_"), "tile_3", "MakeUniqueRegionName returns the next free name");
        Godot.Collections.Array<Rect2I> tooBig = [new Rect2I(0, 0, 8, 8), new Rect2I(120, 0, 32, 32)];
        Expect(atlas.AddRegions(tooBig, "tile_").Length == 0, "AddRegions with one invalid rectangle adds nothing");
        ExpectEqual(atlas.GetRegionCount(), 3, "a failed AddRegions changes nothing");
    }

    private static bool SameRegions(SpriteSheet a, SpriteSheet b)
    {
        if (!a.GetRegionNames().SequenceEqual(b.GetRegionNames()))
        {
            return false;
        }
        foreach (string name in a.GetRegionNames())
        {
            if (a.GetRegion(name) != b.GetRegion(name))
            {
                return false;
            }
            if (a.GetRegionMargin(name) != b.GetRegionMargin(name))
            {
                return false;
            }
        }
        return true;
    }
}
