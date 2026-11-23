using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue50FeatureTextureAtlas;

/// <summary>C# checks of 50-feature-texture-atlas. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue50FeatureTextureAtlasCheck : FeatureCheckCs
{
    private const string Dir = "res://50-feature-texture-atlas/resources/";
    private const string SheetPath = Dir + "sheet.png";
    private const string UserDir = "user://atlas50_cs/";
    private const string Prefix = "atlas50cs://";

    protected override string CheckName => "50-feature-texture-atlas";

    private static bool SameRegions(TextureAtlas a, TextureAtlas b)
    {
        if (!a.GetRegionNames().SequenceEqual(b.GetRegionNames()))
        {
            return false;
        }
        foreach (string name in a.GetRegionNames())
        {
            if (a.GetRegion(name) != b.GetRegion(name) || a.GetRegionMargin(name) != b.GetRegionMargin(name))
            {
                return false;
            }
        }
        return true;
    }

    protected override async Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(UserDir);
        Expect(ClassDB.ClassExists("TextureAtlas"), "TextureAtlas is a registered class");

        // Imported PNG (CompressedTexture2D) and the own JSON layout.
        var sheet = GD.Load<Texture2D>(SheetPath);
        Expect(sheet is CompressedTexture2D, "the imported PNG is a CompressedTexture2D");
        var atlas = new TextureAtlas();
        Expect(atlas.LoadFromFile(Dir + "icons.atlas.json") == Error.Ok, "LoadFromFile reads the JSON layout");
        Expect(atlas.Texture == sheet, "TexturePath loads the imported PNG");
        Expect(atlas.GetRegionNames().SequenceEqual(["coin", "sword", "walk_0", "walk_1", "walk_2", "walk_10"]), "regions keep the file order");
        Expect(atlas.GetRegion("sword") == new Rect2I(16, 0, 24, 48), "region rectangle from JSON");
        Godot.Collections.Dictionary<StringName, Rect2I> regions = atlas.Regions;
        Expect(regions.Count == 6 && regions["coin"] == new Rect2I(0, 0, 16, 16), "Regions is a typed dictionary");

        // AtlasTextures reference the atlas texture and stay in sync.
        AtlasTexture coin = atlas.GetTexture("coin");
        Expect(coin != null && coin.Atlas == sheet, "GetTexture returns an AtlasTexture of the atlas texture");
        Expect(atlas.GetTexture("coin") == coin, "GetTexture returns the same texture again");
        atlas.SetRegion("coin", new Rect2I(0, 0, 8, 8));
        Expect(coin.Region == new Rect2(0, 0, 8, 8), "changing a region updates the handed-out texture");
        atlas.SetRegion("coin", new Rect2I(0, 0, 16, 16));
        Expect(atlas.SetRegion("outside", new Rect2I(120, 60, 16, 16)) == Error.InvalidParameter, "a region outside the texture is rejected");
        Expect(!atlas.HasRegion("outside"), "the rejected region is not added");

        // Drawing in Sprite2D and TextureRect: regions of different sizes.
        var sprite = new Sprite2D { Texture = atlas.GetTexture("sword") };
        AddChild(sprite);
        var rect = new TextureRect { Texture = atlas.GetTexture("coin") };
        AddChild(rect);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(sprite.GetRect().Size == new Vector2(24, 48), "Sprite2D draws the 24x48 region");
        Expect(rect.GetCombinedMinimumSize() == new Vector2(16, 16), "TextureRect uses the 16x16 region");
        Image image = atlas.GetTexture("coin").GetImage();
        if (image != null)
        {
            Expect(image.GetSize() == new Vector2I(16, 16) && image.GetPixel(0, 0).IsEqualApprox(new Color(1, 1, 0)), "the coin region shows the yellow pixels");
        }
        else
        {
            GD.Print("  INFO  GetImage() of the imported texture is null with this renderer; pixel check skipped");
        }

        // Changing the texture updates every handed-out texture.
        Image sheetImage = sheet.GetImage() ?? Image.CreateEmpty(128, 64, false, Image.Format.Rgba8);
        var imageTexture = ImageTexture.CreateFromImage(sheetImage);
        atlas.Texture = imageTexture;
        Expect(coin.Atlas == imageTexture && ((AtlasTexture)sprite.Texture).Atlas == imageTexture, "changing Texture updates the handed-out textures");
        Expect(atlas.TexturePath == "", "setting Texture from code clears TexturePath");

        // GradientTexture2D as texture.
        var gradient = new GradientTexture2D { Width = 64, Height = 64 };
        var gradientAtlas = new TextureAtlas { Texture = gradient };
        Expect(gradientAtlas.SetRegion("half", new Rect2I(0, 0, 32, 64)) == Error.Ok, "GradientTexture2D works as texture");
        Expect(gradientAtlas.GetTexture("half").Atlas == gradient, "the region references the GradientTexture2D");

        // Without TexturePath, the uid:// path of the texture is written and read back.
        var uidAtlas = new TextureAtlas { Texture = sheet };
        uidAtlas.SetRegion("a", new Rect2I(0, 0, 1, 1));
        string uidJson = uidAtlas.SaveAsJson();
        Expect(uidJson.Contains("\"TexturePath\": \"uid://"), "the uid:// path of the texture is written");
        var uidCopy = new TextureAtlas();
        Expect(uidCopy.LoadFromJson(uidJson) == Error.Ok && uidCopy.Texture == sheet, "a uid:// TexturePath is read");

        // CreateSpriteFrames and AnimatedSprite2D.
        atlas.Texture = sheet;
        atlas.TexturePath = SheetPath; // Written instead of the uid:// path of the imported PNG.
        SpriteFrames frames = atlas.CreateSpriteFrames("walk_", 30.0);
        Expect(frames.GetAnimationNames().SequenceEqual(["walk"]), "one animation per prefix");
        Expect(frames.GetFrameCount("walk") == 4, "four walk frames");
        Expect(frames.GetFrameTexture("walk", 2) == atlas.GetTexture("walk_2") && frames.GetFrameTexture("walk", 3) == atlas.GetTexture("walk_10"), "frames in numeric order");
        var animated = new AnimatedSprite2D { SpriteFrames = frames };
        AddChild(animated);
        animated.Play("walk");
        // Wait until the first frame change (at most 2 s): headless frames can be slow right after start.
        for (int i = 0; i < 20 && animated.Frame == 0; i++)
        {
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        }
        Expect(animated.IsPlaying() && animated.Animation == "walk", "AnimatedSprite2D plays the animation");
        Expect(animated.Frame > 0, $"the animation advances (frame {animated.Frame})");

        // JSON round-trip, order kept, with a texture path behind a path prefix (#3).
        ResourceSaver.Save(ImageTexture.CreateFromImage(Image.CreateEmpty(128, 64, false, Image.Format.Rgba8)), UserDir + "sheet.res");
        Expect(PathPrefixServer.RegisterPrefixFolder(Prefix, UserDir) == Error.Ok, "register a path prefix for the texture");
        string json = atlas.SaveAsJson().Replace(SheetPath, Prefix + "sheet.res");
        var prefixed = new TextureAtlas();
        Expect(prefixed.LoadFromJson(json) == Error.Ok, "LoadFromJson with a prefixed TexturePath");
        Expect(prefixed.Texture != null && prefixed.Texture.GetSize() == new Vector2(128, 64), "the texture is loaded through the prefix");
        Expect(prefixed.TexturePath == Prefix + "sheet.res", "TexturePath keeps the prefixed path");
        Expect(SameRegions(atlas, prefixed), "JSON keeps every region and the order");
        Expect(prefixed.SaveAsJson() == json, "JSON round-trips without loss");
        PathPrefixServer.UnregisterPrefix(Prefix);

        // CSV and INI round-trip through files, .tres through ResourceSaver.
        atlas.SetRegionMargin("sword", new Rect2I(2, 2, 4, 4));
        foreach (string extension in new[] { "json", "csv", "ini" })
        {
            string path = UserDir + "icons." + extension;
            Expect(atlas.SaveToFile(path) == Error.Ok, $"SaveToFile(.{extension})");
            var copy = new TextureAtlas();
            Expect(copy.LoadFromFile(path) == Error.Ok && SameRegions(atlas, copy) && copy.Texture == sheet, $"{extension.ToUpperInvariant()} round-trips without loss");
        }
        Expect(ResourceSaver.Save(atlas, UserDir + "icons.atlas.tres") == Error.Ok, "ResourceSaver saves a .tres");
        var tres = ResourceLoader.Load<TextureAtlas>(UserDir + "icons.atlas.tres", null, ResourceLoader.CacheMode.Ignore);
        Expect(tres != null && SameRegions(atlas, tres) && tres.Texture == sheet, ".tres round-trips without loss");

        // TexturePacker JSON: Hash with a trimmed frame, Array with a rotated frame.
        var packed = new TextureAtlas();
        Expect(packed.LoadFromFile(Dir + "texturepacker_hash.json") == Error.Ok, "TexturePacker Hash imports");
        Expect(packed.Texture == sheet, "meta.image is relative to the JSON file");
        Expect(packed.GetRegionNames().SequenceEqual(["items_coin", "items_sword"]), "file names become region names");
        Expect(packed.GetTexture("items_sword").GetSize() == new Vector2(32, 64), "a trimmed frame keeps its original size");
        Expect(packed.GetRegionMargin("items_sword") == new Rect2I(4, 8, 8, 16), "the trim offset becomes the margin");
        var rotated = new TextureAtlas();
        Expect(rotated.LoadFromFile(Dir + "texturepacker_array.json") == Error.InvalidData, "a rotated frame makes the Array import fail");
        Expect(rotated.GetRegionCount() == 0, "a failed import changes nothing");
        string arrayText = FileAccess.GetFileAsString(Dir + "texturepacker_array.json").Replace("\"rotated\": true", "\"rotated\": false");
        Expect(rotated.ImportTexturePackerJson(arrayText, Dir) == Error.Ok && rotated.GetRegionCount() == 3, "TexturePacker Array imports");

        sprite.QueueFree();
        rect.QueueFree();
        animated.QueueFree();
    }
}
