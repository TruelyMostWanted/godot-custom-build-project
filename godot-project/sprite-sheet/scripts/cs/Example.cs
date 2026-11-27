using Godot;

namespace TruelyInteractive.SpriteSheet;

using SpriteSheet = Godot.SpriteSheet;

/// <summary>Minimal example of Sprite Sheet: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
/// <remarks>"Edit regions in the editor" needs the editor and is skipped.</remarks>
public partial class Example : Node
{
    private const string Dir = "res://sprite-sheet/resources/";

    public override void _Ready()
    {
        // Cut a texture into named regions
        var atlas = new SpriteSheet();
        atlas.Texture = GD.Load<Texture2D>(Dir + "sheet.png");
        atlas.SetRegion("coin", new Rect2I(0, 0, 16, 16));
        atlas.SetRegion("sword", new Rect2I(16, 0, 24, 48));
        atlas.RenameRegion("coin", "gold");
        ResourceSaver.Save(atlas, "user://icons.atlas.tres");
        var icon = new Sprite2D { Texture = atlas.GetRegionTexture("gold") };
        AddChild(icon);
        var sword = new TextureRect { Texture = atlas.GetRegionTexture("sword") };
        AddChild(sword);
        atlas.SetRegion("gold", new Rect2I(0, 16, 16, 16));
        var gold = (AtlasTexture)icon.Texture;
        GD.Print("Regions: ", string.Join(", ", atlas.GetRegionNames()), ", gold now at ", gold.Region);

        // Build animations from regions
        atlas.SetRegion("walk_0", new Rect2I(48, 0, 16, 32));
        atlas.SetRegion("walk_1", new Rect2I(64, 0, 16, 32));
        var hero = new AnimatedSprite2D();
        AddChild(hero);
        hero.SpriteFrames = atlas.CreateSpriteFrames("walk_", 12.0);
        hero.Play("walk");
        GD.Print("Walk frames: ", hero.SpriteFrames.GetFrameCount("walk"));

        // Load regions from JSON, CSV or INI
        var loaded = new SpriteSheet();
        if (loaded.LoadFromFile(Dir + "icons.atlas.json") != Error.Ok)
        {
            return;
        }
        loaded.SaveToFile("user://hero.atlas.csv");
        string ini = loaded.ToText(ResourceTextSerializer.Format.Ini);
        GD.Print("INI export:\n", ini);

        // Import a TexturePacker sheet
        var packed = new SpriteSheet();
        Error err = packed.LoadFromFile(Dir + "texturepacker_hash.json");
        var coin = new Sprite2D { Texture = packed.GetRegionTexture("items_coin") };
        AddChild(coin);
        GD.Print("TexturePacker import: ", err, ", regions ", string.Join(", ", packed.GetRegionNames()));

        // Slice a sheet automatically
        var sliced = new SpriteSheet { Texture = GD.Load<Texture2D>(Dir + "sheet.png") };
        string[] names = sliced.AutoSlice("enemy_");
        GD.Print(names.Length, " sprites: ", string.Join(", ", names));
        Image image = sliced.Texture.GetImage();
        Godot.Collections.Array<Rect2I> rects = SpriteSheet.DetectSpriteRects(image, 0.1f);
        GD.Print("Detected rectangles: ", rects.Count);
        var sheetSize = (Vector2I)sliced.Texture.GetSize();
        Godot.Collections.Array<Rect2I> cells = SpriteSheet.MakeGridRects(sheetSize, new Vector2I(32, 32), new Vector2I(2, 2), new Vector2I(1, 1));
        sliced.AddRegions(cells, "tile_");
        GD.Print("Next free name: ", sliced.MakeUniqueRegionName("tile_"));

        GetTree().Quit();
    }
}
