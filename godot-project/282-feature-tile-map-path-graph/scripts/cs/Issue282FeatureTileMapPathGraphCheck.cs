using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue282FeatureTileMapPathGraph;

/// <summary>
/// C# checks of 282-feature-tile-map-path-graph (TileMapPathGraph, TileMapPathGraphOptions).
/// Check the same things as scripts/gd/check.gd.
/// </summary>
public partial class Issue282FeatureTileMapPathGraphCheck : FeatureCheckCs
{
    private static readonly Vector2I Floor = new(0, 0);
    private static readonly Vector2I Wall = new(1, 0); // Custom data "blocked" = true.
    private static readonly Vector2I Mud = new(2, 0); // Custom data "cost" = 20.
    // Negative and positive cells. Every side step changes x by at most 1, so the column x = 0 splits the map.
    private static readonly Rect2I Map = new(-4, -3, 9, 7);
    private static readonly Vector2I From = new(-4, 0);
    private static readonly Vector2I To = new(4, 0);
    private static readonly Vector2I Gap = ColumnGraph.Gap;
    private static readonly Vector2I Gap2 = ColumnGraph.Gap2;

    protected override string CheckName => "282-feature-tile-map-path-graph";

    protected override Task RunChecks()
    {
        CheckApi();
        CheckPointIds();
        CheckMap("square", TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, false, TileMapPathGraph.Backend.Grid);
        CheckMap("diamond isometric", TileSet.TileShapeEnum.Isometric, TileSet.TileLayoutEnum.DiamondRight, false, TileMapPathGraph.Backend.Grid);
        CheckMap("stacked isometric", TileSet.TileShapeEnum.Isometric, TileSet.TileLayoutEnum.Stacked, false, TileMapPathGraph.Backend.Graph);
        CheckMap("half-offset square", TileSet.TileShapeEnum.HalfOffsetSquare, TileSet.TileLayoutEnum.Stacked, false, TileMapPathGraph.Backend.Graph);
        CheckMap("hexagon", TileSet.TileShapeEnum.Hexagon, TileSet.TileLayoutEnum.Stacked, false, TileMapPathGraph.Backend.Graph);
        CheckMap("square, swapped and flipped", TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, true, TileMapPathGraph.Backend.Grid);
        CheckMap("hexagon, swapped and flipped", TileSet.TileShapeEnum.Hexagon, TileSet.TileLayoutEnum.Stacked, true, TileMapPathGraph.Backend.Graph);
        CheckCorners();
        CheckEmptyCells();
        CheckVirtuals();
        return Task.CompletedTask;
    }

    private static TileSet MakeTileSet(TileSet.TileShapeEnum shape, TileSet.TileLayoutEnum layout, bool swapped)
    {
        var tileSet = new TileSet { TileShape = shape, TileLayout = layout, TileSize = new Vector2I(16, 16), SwapXAndYAxis = swapped, FlipY = swapped };
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

    private static TileMapLayer MakeLayer(TileSet tileSet, Rect2I rect)
    {
        var layer = new TileMapLayer { TileSet = tileSet };
        for (int y = rect.Position.Y; y < rect.End.Y; y++)
        {
            for (int x = rect.Position.X; x < rect.End.X; x++)
            {
                layer.SetCell(new Vector2I(x, y), 0, Floor);
            }
        }
        return layer;
    }

    private static TileMapPathGraphOptions MakeOptions()
    {
        return new TileMapPathGraphOptions { BlockedDataLayer = "blocked", CostDataLayer = "cost" };
    }

    // A path is valid when it goes from `from` to `to`, every step goes to a side neighbor and no cell is blocked.
    private static bool IsValidPath(TileMapPathGraph graph, TileMapLayer layer, Array<Vector2I> path, Vector2I from, Vector2I to)
    {
        if (path.Count == 0 || path[0] != from || path[path.Count - 1] != to)
        {
            return false;
        }
        for (int i = 0; i < path.Count; i++)
        {
            if (graph.IsCellBlocked(path[i]))
            {
                return false;
            }
            if (i > 0 && !layer.GetSurroundingCells(path[i - 1]).Contains(path[i]))
            {
                return false;
            }
        }
        return true;
    }

    private void CheckApi()
    {
        foreach (string className in new[] { "TileMapPathGraph", "TileMapPathGraphOptions" })
        {
            Expect(ClassDB.ClassExists(className), $"{className} is registered");
        }
        var options = new TileMapPathGraphOptions();
        ExpectEqual(options.NeighborMode, TileMapPathGraphOptions.NeighborModeEnum.Sides, "neighbor_mode defaults to sides");
        ExpectEqual(options.DiagonalMode, AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles, "diagonal_mode defaults to no corner cutting");
        ExpectEqual(options.DefaultCost, 1.0f, "default_cost defaults to 1");
        Expect(options.EmptyCellsBlocked, "empty_cells_blocked defaults to true");
        Expect(options.CostDataLayer == "" && options.BlockedDataLayer == "", "no data layers by default");
        var graph = new TileMapPathGraph();
        Expect(graph.Options != null, "a new graph has default options");
        graph.Options = null;
        Expect(graph.Options != null, "setting null options restores the defaults");
        Expect(graph.GetLayer() == null, "a new graph has no layer");
        Expect(graph.GetCellPath(new Vector2I(), new Vector2I(1, 0)).Count == 0, "an unbuilt graph has no paths");
        Expect(graph.IsCellBlocked(new Vector2I()), "an unbuilt graph blocks every cell");
    }

    private void CheckPointIds()
    {
        ExpectEqual(TileMapPathGraph.CellToPointId(new Vector2I(-32768, -32768)), 0L, "the lowest cell has id 0");
        ExpectEqual(TileMapPathGraph.CellToPointId(new Vector2I(32767, 32767)), 4294967295L, "the highest cell has id 2^32 - 1");
        var astar = new AStar2D();
        foreach (Vector2I cell in new[] { new Vector2I(-1, -1), new Vector2I(-32768, 32767), new Vector2I(32767, -32768), new Vector2I(0, 0), new Vector2I(5, -7) })
        {
            long id = TileMapPathGraph.CellToPointId(cell);
            Expect(id >= 0, $"cell {cell} has a non-negative id");
            ExpectEqual(TileMapPathGraph.PointIdToCell(id), cell, $"id of {cell} converts back");
            astar.AddPoint(id, new Vector2(cell.X, cell.Y));
            Expect(astar.HasPoint(id), $"AStar2D accepts the id of {cell}");
        }
    }

    private void CheckMap(string label, TileSet.TileShapeEnum shape, TileSet.TileLayoutEnum layout, bool swapped, TileMapPathGraph.Backend backend)
    {
        TileSet tileSet = MakeTileSet(shape, layout, swapped);
        TileMapLayer layer = MakeLayer(tileSet, Map);
        TileMapPathGraph graph = TileMapPathGraph.Create(layer, MakeOptions());
        int updates = 0;
        graph.Updated += () => updates++;
        ExpectEqual(graph.GetBackend(), backend, $"{label}: backend");
        ExpectEqual(graph.GetAstarGrid() != null, backend == TileMapPathGraph.Backend.Grid, $"{label}: GetAstarGrid() only on the grid backend");
        ExpectEqual(graph.GetAstar() != null, backend == TileMapPathGraph.Backend.Graph, $"{label}: GetAstar() only on the graph backend");
        ExpectEqual(graph.GetRegion(), Map, $"{label}: the region is the used rect");
        Array<Vector2I> path = graph.GetCellPath(From, To);
        Expect(IsValidPath(graph, layer, path, From, To), $"{label}: path on the open map");
        Vector2[] points = graph.GetPointPath(From, To);
        Expect(points.Length == path.Count && points[0].IsEqualApprox(layer.MapToLocal(From)) && points[^1].IsEqualApprox(layer.MapToLocal(To)), $"{label}: point path in local positions");

        // A wall at x = 0 with one gap.
        var wall = new Array<Vector2I>();
        for (int y = Map.Position.Y; y < Map.End.Y; y++)
        {
            var cell = new Vector2I(0, y);
            if (cell != Gap && cell != Gap2)
            {
                layer.SetCell(cell, 0, Wall);
                wall.Add(cell);
            }
        }
        graph.UpdateCells(wall);
        ExpectEqual(updates, 1, $"{label}: UpdateCells() emits Updated");
        Expect(graph.IsCellBlocked(new Vector2I(0, 0)) && !graph.IsCellBlocked(Gap) && !graph.IsCellBlocked(Gap2), $"{label}: wall cells are blocked");
        path = graph.GetCellPath(From, To);
        Expect(IsValidPath(graph, layer, path, From, To) && (path.Contains(Gap) || path.Contains(Gap2)), $"{label}: the path goes through the gap");

        // Close the gap in the layer but update another cell: only the given cells are recomputed.
        layer.SetCell(Gap, 0, Wall);
        layer.SetCell(Gap2, 0, Wall);
        graph.UpdateCells(new Array<Vector2I> { From });
        Expect(graph.GetCellPath(From, To).Contains(Gap) || graph.GetCellPath(From, To).Contains(Gap2), $"{label}: UpdateCells() recomputes only the given cells");
        graph.UpdateCells(new Array<Vector2I> { Gap, Gap2 });
        Expect(graph.GetCellPath(From, To).Count == 0, $"{label}: closed wall, no path");
        Array<Vector2I> partial = graph.GetCellPath(From, To, true);
        Expect(partial.Count > 0 && partial[0] == From && partial[partial.Count - 1].X < 0, $"{label}: partial path ends before the wall");

        // Costs: mud everywhere at x = 0 except the gap.
        foreach (Vector2I cell in wall)
        {
            layer.SetCell(cell, 0, Mud);
        }
        layer.SetCell(Gap, 0, Floor);
        layer.SetCell(Gap2, 0, Floor);
        wall.Add(Gap);
        wall.Add(Gap2);
        graph.UpdateCells(wall);
        Expect(Mathf.IsEqualApprox(graph.GetCellCost(new Vector2I(0, 0)), 20.0f) && Mathf.IsEqualApprox(graph.GetCellCost(Gap), 1.0f) && Mathf.IsEqualApprox(graph.GetCellCost(Gap2), 1.0f), $"{label}: costs from the custom data layer");
        path = graph.GetCellPath(From, To);
        Expect(IsValidPath(graph, layer, path, From, To) && (path.Contains(Gap) || path.Contains(Gap2)), $"{label}: the cheap way goes through the gap");
        layer.Free();
    }

    private void CheckCorners()
    {
        TileMapLayer layer = MakeLayer(MakeTileSet(TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, false), new Rect2I(0, 0, 7, 7));
        TileMapPathGraphOptions options = MakeOptions();
        ExpectEqual(TileMapPathGraph.Create(layer, options).GetCellPath(new Vector2I(0, 0), new Vector2I(6, 6)).Count, 13, "sides only: 13 cells to the opposite corner");
        options.NeighborMode = TileMapPathGraphOptions.NeighborModeEnum.SidesAndCorners;
        TileMapPathGraph graph = TileMapPathGraph.Create(layer, options);
        ExpectEqual(graph.GetCellPath(new Vector2I(0, 0), new Vector2I(6, 6)).Count, 7, "sides and corners: 7 cells on the diagonal");
        // No corner cutting by default: a blocked side cell forbids the corner step past it.
        layer.SetCell(new Vector2I(1, 0), 0, Wall);
        graph.UpdateCells(new Array<Vector2I> { new Vector2I(1, 0) });
        ExpectEqual(graph.GetCellPath(new Vector2I(0, 0), new Vector2I(1, 1)).Count, 3, "no corner cutting past a blocked cell");
        layer.Free();
    }

    private void CheckEmptyCells()
    {
        TileMapLayer layer = MakeLayer(MakeTileSet(TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, false), new Rect2I(0, 0, 5, 1));
        layer.EraseCell(new Vector2I(2, 0));
        TileMapPathGraph graph = TileMapPathGraph.Create(layer);
        Expect(graph.IsCellBlocked(new Vector2I(2, 0)), "empty cells are blocked by default");
        Expect(graph.GetCellPath(new Vector2I(0, 0), new Vector2I(4, 0)).Count == 0, "no path through a hole");
        graph.Options.EmptyCellsBlocked = false;
        graph.Options.DefaultCost = 2.0f;
        ExpectEqual(graph.UpdateAll(), Error.Ok, "UpdateAll() returns Ok");
        Expect(!graph.IsCellBlocked(new Vector2I(2, 0)) && Mathf.IsEqualApprox(graph.GetCellCost(new Vector2I(2, 0)), 2.0f), "walkable empty cells cost DefaultCost");
        ExpectEqual(graph.GetCellPath(new Vector2I(0, 0), new Vector2I(4, 0)).Count, 5, "path through the hole");
        // Painting outside the region grows the graph.
        layer.SetCell(new Vector2I(5, 0), 0, Floor);
        graph.UpdateCells(new Array<Vector2I> { new Vector2I(5, 0) });
        ExpectEqual(graph.GetRegion(), new Rect2I(0, 0, 6, 1), "UpdateCells() grows the region");
        ExpectEqual(graph.GetCellPath(new Vector2I(0, 0), new Vector2I(5, 0)).Count, 6, "path to the new cell");
        layer.Free();
        Expect(graph.GetLayer() == null, "GetLayer() is null after the layer is freed");
    }

    private void CheckVirtuals()
    {
        TileMapLayer layer = MakeLayer(MakeTileSet(TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, false), Map);
        var graph = new ColumnGraph();
        graph.SetLayer(layer);
        ExpectEqual(graph.UpdateAll(), Error.Ok, "a C# subclass builds");
        Expect(graph.IsCellBlocked(new Vector2I(0, 0)) && !graph.IsCellBlocked(Gap) && !graph.IsCellBlocked(Gap2), "_IsCellBlocked() decides blocked cells");
        Expect(Mathf.IsEqualApprox(graph.GetCellCost(new Vector2I(1, 0)), 3.0f) && Mathf.IsEqualApprox(graph.GetCellCost(new Vector2I(1, 1)), 1.0f), "_GetCellCost() decides costs");
        Expect(graph.GetCellPath(From, To).Contains(Gap) || graph.GetCellPath(From, To).Contains(Gap2), "the path uses the gap left by _IsCellBlocked()");
        layer.Free();
    }
}
