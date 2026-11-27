using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.TileMapPathfinding;

/// <summary>
/// Full API test of Tile Map Pathfinding (TileMapPathGraph, TileMapPathGraphOptions). Same tests as scripts/gd/Test.gd.
/// DrawDebug() is tested in tile-map-path-graph-debug.
/// </summary>
public partial class Test : FeatureTest
{
    private const string Dir = "res://tile-map-pathfinding/resources/";
    private static readonly Vector2I Floor = new(0, 0);
    private static readonly Vector2I Wall = new(1, 0); // Deep water: custom data "blocked" = true, terrain 2.
    private static readonly Vector2I Mud = new(2, 0); // Shallow water: custom data "cost" = 20, terrain 1.
    // Negative and positive cells. Every side step changes x by at most 1, so the column x = 0 splits the map.
    private static readonly Rect2I Map = new(-4, -3, 9, 7);
    private static readonly Vector2I From = new(-4, 0);
    private static readonly Vector2I To = new(4, 0);
    // Two gap cells in the wall column, one in an even and one in an odd row: odd rows of stacked isometric maps are
    // shifted half a cell, so a single gap cell cannot be passed with side neighbors only.
    private static readonly Vector2I Gap = new(0, 3);
    private static readonly Vector2I Gap2 = new(0, 2);
    private const long LowestId = 0;
    private const long HighestId = 4294967295;

    private TileSet _ground;
    private TileMapPathGraphOptions _options;
    private int _updates;

    protected override Task RunTests()
    {
        _ground = GD.Load<TileSet>(Dir + "ground.tres");
        _options = GD.Load<TileMapPathGraphOptions>(Dir + "path_graph_options.tres");
        TestOptionsDefaults();
        TestNewGraph();
        TestPointIds();
        TestMap("square", TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, false, TileMapPathGraph.Backend.Grid);
        TestMap("diamond isometric", TileSet.TileShapeEnum.Isometric, TileSet.TileLayoutEnum.DiamondRight, false, TileMapPathGraph.Backend.Grid);
        TestMap("stacked isometric", TileSet.TileShapeEnum.Isometric, TileSet.TileLayoutEnum.Stacked, false, TileMapPathGraph.Backend.Graph);
        TestMap("half-offset square", TileSet.TileShapeEnum.HalfOffsetSquare, TileSet.TileLayoutEnum.Stacked, false, TileMapPathGraph.Backend.Graph);
        TestMap("hexagon", TileSet.TileShapeEnum.Hexagon, TileSet.TileLayoutEnum.Stacked, false, TileMapPathGraph.Backend.Graph);
        TestMap("square, swapped and flipped", TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, true, TileMapPathGraph.Backend.Grid);
        TestMap("hexagon, swapped and flipped", TileSet.TileShapeEnum.Hexagon, TileSet.TileLayoutEnum.Stacked, true, TileMapPathGraph.Backend.Graph);
        TestCorners();
        TestEmptyCells();
        TestVirtuals();
        return Task.CompletedTask;
    }

    // A copy of resources/ground.tres with another shape. swapped = SwapXAndYAxis and FlipY.
    private TileSet MakeTileSet(TileSet.TileShapeEnum shape, TileSet.TileLayoutEnum layout, bool swapped)
    {
        var tileSet = (TileSet)_ground.Duplicate(true);
        tileSet.TileShape = shape;
        tileSet.TileLayout = layout;
        tileSet.SwapXAndYAxis = swapped;
        tileSet.FlipY = swapped;
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

    // From From to To, every step to a side neighbor, no blocked cell.
    private static bool IsValidPath(TileMapPathGraph graph, TileMapLayer layer, Array<Vector2I> path)
    {
        if (path.Count == 0 || path[0] != From || path[^1] != To)
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

    private static bool PassesGap(Array<Vector2I> path)
    {
        return path.Contains(Gap) || path.Contains(Gap2);
    }

    private void TestOptionsDefaults()
    {
        var options = new TileMapPathGraphOptions();
        ExpectEqual(options.NeighborMode, TileMapPathGraphOptions.NeighborModeEnum.Sides, "NeighborMode defaults to Sides");
        ExpectEqual(options.DiagonalMode, AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles, "DiagonalMode defaults to no corner cutting");
        ExpectEqual(options.DefaultCost, 1.0f, "DefaultCost defaults to 1");
        Expect(options.EmptyCellsBlocked, "EmptyCellsBlocked defaults to true");
        ExpectEqual(options.CostDataLayer, "", "CostDataLayer defaults to empty");
        ExpectEqual(options.BlockedDataLayer, "", "BlockedDataLayer defaults to empty");
        ExpectEqual((int)TileMapPathGraphOptions.NeighborModeEnum.SidesAndCorners, 1, "NeighborModeEnum.SidesAndCorners is 1");
        ExpectEqual((int)TileMapPathGraphOptions.NeighborModeEnum.Max, 2, "NeighborModeEnum.Max is 2");
        ExpectEqual(_options.BlockedDataLayer, "blocked", "the sample options .tres loads");
    }

    private void TestNewGraph()
    {
        var graph = new TileMapPathGraph();
        Expect(graph.Options != null, "a new graph has default options");
        graph.Options = null;
        Expect(graph.Options != null, "setting Options to null restores the defaults");
        Expect(graph.GetLayer() == null, "a new graph has no layer");
        Expect(graph.GetCellPath(new Vector2I(), new Vector2I(1, 0)).Count == 0, "an unbuilt graph has no paths");
        Expect(graph.IsCellBlocked(new Vector2I()), "an unbuilt graph blocks every cell");
        ExpectEqual((int)TileMapPathGraph.Backend.Max, 2, "Backend.Max is 2");
    }

    private void TestPointIds()
    {
        ExpectEqual(TileMapPathGraph.CellToPointId(new Vector2I(-32768, -32768)), LowestId, "the lowest cell has id 0");
        ExpectEqual(TileMapPathGraph.CellToPointId(new Vector2I(32767, 32767)), HighestId, "the highest cell has id 2^32 - 1");
        var astar = new AStar2D();
        Vector2I[] cells = [new(-1, -1), new(-32768, 32767), new(32767, -32768), new(5, -7)];
        foreach (Vector2I cell in cells)
        {
            long id = TileMapPathGraph.CellToPointId(cell);
            ExpectEqual(TileMapPathGraph.PointIdToCell(id), cell, $"PointIdToCell() reverses CellToPointId() for {cell}");
            astar.AddPoint(id, new Vector2(cell.X, cell.Y));
            Expect(astar.HasPoint(id), $"AStar2D accepts the id of {cell}");
        }
    }

    private void TestMap(string label, TileSet.TileShapeEnum shape, TileSet.TileLayoutEnum layout, bool swapped, TileMapPathGraph.Backend backend)
    {
        TileMapLayer layer = MakeLayer(MakeTileSet(shape, layout, swapped), Map);
        TileMapPathGraph graph = TileMapPathGraph.Create(layer, _options);
        _updates = 0;
        graph.Updated += OnUpdated;
        ExpectEqual(graph.GetBackend(), backend, $"{label}: GetBackend()");
        ExpectEqual(graph.GetAstarGrid() != null, backend == TileMapPathGraph.Backend.Grid, $"{label}: GetAstarGrid() only on the grid backend");
        ExpectEqual(graph.GetAstar() != null, backend == TileMapPathGraph.Backend.Graph, $"{label}: GetAstar() only on the graph backend");
        ExpectEqual(graph.GetRegion(), Map, $"{label}: GetRegion() is the used rect");
        Expect(graph.GetLayer() == layer, $"{label}: GetLayer() is the layer");
        Array<Vector2I> path = graph.GetCellPath(From, To);
        Expect(IsValidPath(graph, layer, path), $"{label}: path on the open map");
        Vector2[] points = graph.GetPointPath(From, To);
        ExpectEqual(points.Length, path.Count, $"{label}: GetPointPath() has one point per cell");
        Expect(points[^1].IsEqualApprox(layer.MapToLocal(To)), $"{label}: GetPointPath() returns local positions");

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
        ExpectEqual(_updates, 1, $"{label}: UpdateCells() emits Updated");
        Expect(graph.IsCellBlocked(new Vector2I(0, 0)), $"{label}: wall cells are blocked");
        Expect(!graph.IsCellBlocked(Gap), $"{label}: the gap is walkable");
        path = graph.GetCellPath(From, To);
        Expect(IsValidPath(graph, layer, path), $"{label}: path with a wall");
        Expect(PassesGap(path), $"{label}: the path goes through the gap");

        layer.SetCell(Gap, 0, Wall);
        layer.SetCell(Gap2, 0, Wall);
        graph.UpdateCells([From]);
        Expect(PassesGap(graph.GetCellPath(From, To)), $"{label}: UpdateCells() recomputes only the given cells");
        graph.UpdateCells([Gap, Gap2]);
        Expect(graph.GetCellPath(From, To).Count == 0, $"{label}: closed wall, no path");
        Array<Vector2I> partial = graph.GetCellPath(From, To, true);
        Expect(partial[^1].X < 0, $"{label}: allowPartialPath ends before the wall");

        foreach (Vector2I cell in wall)
        {
            layer.SetCell(cell, 0, Mud);
        }
        layer.SetCell(Gap, 0, Floor);
        layer.SetCell(Gap2, 0, Floor);
        wall.Add(Gap);
        wall.Add(Gap2);
        graph.UpdateCells(wall);
        ExpectEqual(graph.GetCellCost(new Vector2I(0, 0)), 20.0f, $"{label}: cost from the custom data layer");
        ExpectEqual(graph.GetCellCost(Gap), 1.0f, $"{label}: unpainted cost is DefaultCost");
        Expect(PassesGap(graph.GetCellPath(From, To)), $"{label}: the cheap way goes through the gap");
        graph.Updated -= OnUpdated;
        layer.Free();
    }

    private void TestCorners()
    {
        TileMapLayer layer = MakeLayer(MakeTileSet(TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, false), new Rect2I(0, 0, 7, 7));
        var options = (TileMapPathGraphOptions)_options.Duplicate();
        TileMapPathGraph graph = TileMapPathGraph.Create(layer, options);
        ExpectEqual(graph.GetCellPath(new Vector2I(0, 0), new Vector2I(6, 6)).Count, 13, "sides only: 13 cells to the opposite corner");
        options.NeighborMode = TileMapPathGraphOptions.NeighborModeEnum.SidesAndCorners;
        graph.UpdateAll();
        ExpectEqual(graph.GetCellPath(new Vector2I(0, 0), new Vector2I(6, 6)).Count, 7, "sides and corners: 7 cells on the diagonal");
        layer.SetCell(new Vector2I(1, 0), 0, Wall);
        graph.UpdateCells([new Vector2I(1, 0)]);
        ExpectEqual(graph.GetCellPath(new Vector2I(0, 0), new Vector2I(1, 1)).Count, 3, "OnlyIfNoObstacles: no corner cutting past a wall");
        options.DiagonalMode = AStarGrid2D.DiagonalModeEnum.AtLeastOneWalkable;
        graph.UpdateAll();
        ExpectEqual(graph.GetCellPath(new Vector2I(0, 0), new Vector2I(1, 1)).Count, 2, "AtLeastOneWalkable: corner step past a wall");
        layer.Free();
    }

    private void TestEmptyCells()
    {
        TileMapLayer layer = MakeLayer(MakeTileSet(TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, false), new Rect2I(0, 0, 5, 1));
        layer.EraseCell(new Vector2I(2, 0));
        TileMapPathGraph graph = TileMapPathGraph.Create(layer);
        Expect(graph.IsCellBlocked(new Vector2I(2, 0)), "empty cells are blocked by default");
        Expect(graph.GetCellPath(new Vector2I(0, 0), new Vector2I(4, 0)).Count == 0, "no path through a hole");
        graph.Options.EmptyCellsBlocked = false;
        graph.Options.DefaultCost = 2.0f;
        ExpectEqual(graph.UpdateAll(), Error.Ok, "UpdateAll() returns Ok");
        Expect(!graph.IsCellBlocked(new Vector2I(2, 0)), "EmptyCellsBlocked = false: the hole is walkable");
        ExpectEqual(graph.GetCellCost(new Vector2I(2, 0)), 2.0f, "a walkable empty cell costs DefaultCost");
        ExpectEqual(graph.GetCellPath(new Vector2I(0, 0), new Vector2I(4, 0)).Count, 5, "path through the hole");
        layer.SetCell(new Vector2I(5, 0), 0, Floor);
        graph.UpdateCells([new Vector2I(5, 0)]);
        ExpectEqual(graph.GetRegion(), new Rect2I(0, 0, 6, 1), "UpdateCells() outside the region grows it");
        ExpectEqual(graph.GetCellPath(new Vector2I(0, 0), new Vector2I(5, 0)).Count, 6, "path to the new cell");
        layer.Free();
        Expect(graph.GetLayer() == null, "GetLayer() is null after the layer is freed");
    }

    private void TestVirtuals()
    {
        TileMapLayer layer = MakeLayer(MakeTileSet(TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, false), Map);
        layer.SetCell(new Vector2I(0, 0), 0, Wall);
        layer.SetCell(new Vector2I(1, 0), 0, Mud);
        layer.EraseCell(new Vector2I(2, 0));
        var graph = new RiverGraph();
        graph.SetLayer(layer);
        ExpectEqual(graph.UpdateAll(), Error.Ok, "a C# subclass builds");
        Expect(graph.IsCellBlocked(new Vector2I(0, 0)), "_IsCellBlocked(): deep water is blocked");
        Expect(graph.IsCellBlocked(new Vector2I(2, 0)), "_IsCellBlocked() gets null tileData for an empty cell");
        Expect(!graph.IsCellBlocked(new Vector2I(0, 1)), "_IsCellBlocked(): floor is walkable");
        ExpectEqual(graph.GetCellCost(new Vector2I(1, 0)), 2.0f, "_GetCellCost(): shallow water costs 2");
        ExpectEqual(graph.GetCellCost(new Vector2I(0, 1)), 1.0f, "_GetCellCost(): floor costs 1");
        layer.Free();
    }

    private void OnUpdated()
    {
        _updates++;
    }
}
