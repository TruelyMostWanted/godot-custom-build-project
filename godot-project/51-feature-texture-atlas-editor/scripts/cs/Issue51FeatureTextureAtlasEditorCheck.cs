using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue51FeatureTextureAtlasEditor;

/// <summary>
/// C# checks of 51-feature-texture-atlas-editor. Check the same things as scripts/gd/check.gd.
/// The editor itself is checked by hand (see the issue). These checks cover the runtime API the editor uses
/// (auto slice, grid, generated names) and the files the editor writes (.tres, JSON, CSV, INI, SpriteFrames).
/// </summary>
public partial class Issue51FeatureTextureAtlasEditorCheck : FeatureCheckCs
{
    private const string UserDir = "user://atlas51_cs/";

    protected override string CheckName => "51-feature-texture-atlas-editor";

    /// <summary>50 sprites of mixed sizes in 5 rows of 10 cells (24x24), like a hand-made sheet.</summary>
    private static Image MakeSheet(List<Rect2I> expected)
    {
        Image image = Image.CreateEmpty(240, 120, false, Image.Format.Rgba8);
        for (int i = 0; i < 50; i++)
        {
            var rect = new Rect2I((i % 10) * 24 + 1, (i / 10) * 24 + 1, 4 + (i % 5) * 4, 6 + (i % 3) * 7);
            image.FillRect(rect, new Color(0.2f + 0.015f * i, 0.5f, 1.0f, 1.0f));
            expected.Add(rect);
        }
        return image;
    }

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
        var expected = new List<Rect2I>();
        Image image = MakeSheet(expected);

        // Auto slice: 50 sprites of mixed sizes become 50 correct regions, in rows from left to right.
        Godot.Collections.Array<Rect2I> found = TextureAtlas.DetectSpriteRects(image);
        Expect(found.Count == 50, $"DetectSpriteRects finds 50 sprites (found {found.Count})");
        Expect(found.Count == 50 && found.Select((rect, i) => rect == expected[i]).All(match => match), "every detected rectangle matches its sprite, in row order");
        Expect(TextureAtlas.DetectSpriteRects(image, 0.99f).Count == 50, "opaque sprites are found with a high alpha threshold too");

        // The same through a PNG file (as an editor user would paint it).
        string pngPath = UserDir + "sheet50.png";
        Expect(image.SavePng(pngPath) == Error.Ok, "the sheet is saved as PNG");
        ImageTexture sheet = ImageTexture.CreateFromImage(Image.LoadFromFile(pngPath));
        var atlas = new TextureAtlas { Texture = sheet };
        string[] names = atlas.AutoSlice("enemy_");
        Expect(names.Length == 50 && names[0] == "enemy_0" && names[49] == "enemy_49", "AutoSlice adds 50 regions named enemy_0 ... enemy_49");
        Expect(atlas.GetRegion("enemy_17") == expected[17], "AutoSlice region enemy_17 has the sprite rectangle");
        Expect(atlas.AutoSlice("enemy_").Length == 0, "AutoSlice again adds nothing");

        // Grid and generated names ("Add from Grid" in the editor).
        Godot.Collections.Array<Rect2I> cells = TextureAtlas.MakeGridRects(new Vector2I(240, 120), new Vector2I(24, 24));
        Expect(cells.Count == 50 && cells[11] == new Rect2I(24, 24, 24, 24), "MakeGridRects returns 10x5 cells, row by row");
        var gridAtlas = new TextureAtlas { Texture = sheet };
        string[] gridNames = gridAtlas.AddRegions(cells, "cell_");
        Expect(gridNames.Length == 50 && gridAtlas.GetRegion("cell_11") == new Rect2I(24, 24, 24, 24), "AddRegions adds the grid cells with generated names");
        Expect(gridAtlas.MakeUniqueRegionName("cell_") == "cell_50", "MakeUniqueRegionName returns the next free name");
        int count = gridAtlas.GetRegionCount();
        var invalid = new Godot.Collections.Array<Rect2I> { new Rect2I(0, 0, 8, 8), new Rect2I(230, 0, 32, 32) };
        Expect(gridAtlas.AddRegions(invalid, "cell_").Length == 0 && gridAtlas.GetRegionCount() == count, "AddRegions is all-or-nothing");

        // Files the editor writes: the atlas as .tres (with order), JSON, CSV and INI exports.
        atlas.RenameRegion("enemy_0", "boss");
        atlas.SetRegionMargin("boss", new Rect2I(1, 2, 3, 4));
        string tresPath = UserDir + "enemies.atlas.tres";
        Expect(ResourceSaver.Save(atlas, tresPath) == Error.Ok, "the atlas is saved as .tres");
        var reloaded = ResourceLoader.Load<TextureAtlas>(tresPath, "", ResourceLoader.CacheMode.Ignore);
        Expect(reloaded != null && SameRegions(atlas, reloaded), ".tres keeps 50 regions, their order and the margin");
        foreach (string extension in new[] { "json", "csv", "ini" })
        {
            string path = UserDir + "enemies." + extension;
            atlas.TexturePath = pngPath;
            Expect(atlas.SaveToFile(path) == Error.Ok, "export as " + extension);
            // The editor imports into an atlas that has the sheet already; the same path keeps that texture.
            var imported = new TextureAtlas { Texture = sheet };
            imported.TexturePath = pngPath;
            Expect(imported.LoadFromFile(path) == Error.Ok && SameRegions(atlas, imported), "import of the " + extension + " export gives the same regions");
        }

        // "Create SpriteFrames" from selected regions: a copy with only the selected ones.
        var selected = new TextureAtlas { Texture = sheet };
        foreach (string name in new[] { "enemy_12", "enemy_10", "enemy_11" })
        {
            selected.SetRegion(name, atlas.GetRegion(name));
        }
        SpriteFrames frames = selected.CreateSpriteFrames();
        Expect(frames.HasAnimation("enemy") && frames.GetFrameCount("enemy") == 3, "SpriteFrames from 3 selected regions has 3 frames");
        var first = frames.GetFrameTexture("enemy", 0) as AtlasTexture;
        Expect(first != null && first.Region == new Rect2(atlas.GetRegion("enemy_10").Position, atlas.GetRegion("enemy_10").Size), "frames are sorted by number");
        string framesPath = UserDir + "enemy_frames.tres";
        Expect(ResourceSaver.Save(frames, framesPath) == Error.Ok, "the SpriteFrames are saved as .tres");
        var framesReloaded = ResourceLoader.Load<SpriteFrames>(framesPath, "", ResourceLoader.CacheMode.Ignore);
        Expect(framesReloaded != null && framesReloaded.GetFrameCount("enemy") == 3, "the saved SpriteFrames load with 3 frames");

        // "Assign to Selected Nodes": a Sprite2D shows the region.
        var sprite = new Sprite2D { Texture = atlas.GetTexture("enemy_17") };
        AddChild(sprite);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(sprite.GetRect().Size == new Vector2(expected[17].Size.X, expected[17].Size.Y), "a Sprite2D with the region texture has the sprite size");
        sprite.QueueFree();
    }
}
