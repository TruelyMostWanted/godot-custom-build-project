using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue283FeatureTileMapPathGraphDebug;

/// <summary>
/// C# checks of 283-feature-tile-map-path-graph-debug (TileMapPathGraph.DrawDebug()).
/// Check the same things as scripts/gd/check.gd.
/// </summary>
public partial class Issue283FeatureTileMapPathGraphDebugCheck : FeatureCheckCs
{
    private static readonly Vector2I Floor = new(0, 0);
    private static readonly Vector2I Wall = new(1, 0); // Custom data "blocked" = true.
    private static readonly Vector2I Mud = new(2, 0); // Custom data "cost" = 20.
    // Every side step changes x by at most 1, so the wall in column 3 splits the map except at the gap.
    private static readonly Rect2I Map = new(0, 0, 7, 5);
    private static readonly Vector2I From = new(0, 2);
    private static readonly Vector2I To = new(6, 2);
    private static readonly Vector2I Gap = new(3, 4);
    private static readonly Vector2I Gap2 = new(3, 3); // Even and odd row: odd rows of stacked isometric maps are shifted half a cell.
    private static readonly Vector2I MudCell = new(1, 2);

    protected override string CheckName => "283-feature-tile-map-path-graph-debug";

    protected override async Task RunChecks()
    {
        Expect(ClassDB.ClassHasMethod("TileMapPathGraph", "draw_debug"), "TileMapPathGraph has draw_debug()");
        await CheckShape("square", TileSet.TileShapeEnum.Square);
        await CheckShape("stacked isometric", TileSet.TileShapeEnum.Isometric);
        await CheckShape("hexagon", TileSet.TileShapeEnum.Hexagon);
    }

    private static TileSet MakeTileSet(TileSet.TileShapeEnum shape)
    {
        var tileSet = new TileSet { TileShape = shape, TileSize = new Vector2I(16, 16) };
        tileSet.AddCustomDataLayer();
        tileSet.SetCustomDataLayerName(0, "blocked");
        tileSet.SetCustomDataLayerType(0, Variant.Type.Bool);
        tileSet.AddCustomDataLayer();
        tileSet.SetCustomDataLayerName(1, "cost");
        tileSet.SetCustomDataLayerType(1, Variant.Type.Float);
        var atlas = new TileSetAtlasSource
        {
            Texture = ImageTexture.CreateFromImage(Image.CreateEmpty(48, 16, false, Image.Format.Rgba8)),
            TextureRegionSize = new Vector2I(16, 16),
        };
        tileSet.AddSource(atlas, 0);
        foreach (Vector2I tile in new[] { Floor, Wall, Mud })
        {
            atlas.CreateTile(tile);
        }
        atlas.GetTileData(Wall, 0).SetCustomData("blocked", true);
        atlas.GetTileData(Mud, 0).SetCustomData("cost", 20.0);
        return tileSet;
    }

    private async Task CheckShape(string label, TileSet.TileShapeEnum shape)
    {
        var layer = new TileMapLayer { TileSet = MakeTileSet(shape) };
        for (int y = Map.Position.Y; y < Map.End.Y; y++)
        {
            for (int x = Map.Position.X; x < Map.End.X; x++)
            {
                var cell = new Vector2I(x, y);
                layer.SetCell(cell, 0, x == 3 && cell != Gap && cell != Gap2 ? Wall : Floor);
            }
        }
        layer.SetCell(MudCell, 0, Mud);
        AddChild(layer);

        var options = new TileMapPathGraphOptions { BlockedDataLayer = "blocked", CostDataLayer = "cost" };
        TileMapPathGraph graph = TileMapPathGraph.Create(layer, options);

        // The overlay draws what the pathfinder uses: no path cell is drawn as blocked,
        // the wall is, and the mud cell is the most expensive (fully shaded) cell.
        var path = graph.GetCellPath(From, To);
        Expect(path.Count > 0 && (path.Contains(Gap) || path.Contains(Gap2)), label + ": the path goes through the gap");
        Expect(!path.Any(cell => graph.IsCellBlocked(cell)), label + ": no path cell is drawn as blocked");
        Expect(graph.IsCellBlocked(new Vector2I(3, 0)) && !graph.IsCellBlocked(Gap) && !graph.IsCellBlocked(Gap2), label + ": wall cells are drawn as blocked");
        Expect(Mathf.IsEqualApprox((float)graph.GetCellCost(MudCell), 20.0f) && Mathf.IsEqualApprox((float)graph.GetCellCost(new Vector2I(0, 0)), 1.0f), label + ": mud is shaded, floor is not");
        Expect(!path.Contains(MudCell), label + ": the path avoids the shaded cell");

        // The documented recipe: a Node2D child of the layer draws the graph in _Draw() and redraws on Updated.
        var overlay = new Node2D();
        int draws = 0;
        overlay.Draw += () =>
        {
            graph.DrawDebug(overlay.GetCanvasItem());
            graph.DrawDebug(overlay.GetCanvasItem(), new Rect2I(2, 1, 3, 3), Colors.Red, Colors.Blue);
            draws++;
        };
        graph.Updated += overlay.QueueRedraw;
        layer.AddChild(overlay);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(draws >= 1, label + ": DrawDebug() runs in _Draw()");
        int before = draws;
        layer.SetCell(Gap, 0, Wall);
        layer.SetCell(Gap2, 0, Wall);
        graph.UpdateCells(new Godot.Collections.Array<Vector2I> { Gap, Gap2 });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(draws > before, label + ": Updated redraws the overlay");
        Expect(graph.GetCellPath(From, To).Count == 0, label + ": closed wall, no path");

        // A canvas item of its own, without a node.
        Rid canvasItem = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(canvasItem, layer.GetCanvasItem());
        graph.DrawDebug(canvasItem, new Rect2I(100, 100, 4, 4)); // Outside the region: nothing drawn, no error.
        graph.DrawDebug(canvasItem);
        RenderingServer.FreeRid(canvasItem);

        graph.Updated -= overlay.QueueRedraw;
        layer.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
