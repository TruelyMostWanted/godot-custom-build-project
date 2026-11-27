using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.TileMapPathGraphDebug;

/// <summary>
/// Full API test of Tile Map Path Graph Debug (TileMapPathGraph.DrawDebug(), the Updated signal and the values the
/// overlay draws). Map and options from tile-map-pathfinding/resources. Same tests as scripts/gd/Test.gd.
/// </summary>
public partial class Test : FeatureTest
{
    private const string Dir = "res://tile-map-pathfinding/resources/";
    private static readonly Vector2I Floor = new(0, 0);
    private static readonly Vector2I Wall = new(1, 0); // Custom data "blocked" = true.
    private static readonly Vector2I Mud = new(2, 0); // Custom data "cost" = 20.
    private static readonly Rect2I Map = new(0, 0, 7, 5);
    private static readonly Vector2I FloorCell = new(0, 0);
    private static readonly Vector2I WallCell = new(3, 0);
    private static readonly Vector2I MudCell = new(1, 2);
    private static readonly Rect2I Outside = new(100, 100, 4, 4);

    private TileMapPathGraph _graph;
    private Node2D _overlay;
    private int _draws;

    protected override async Task RunTests()
    {
        await TestShape("square", TileSet.TileShapeEnum.Square);
        await TestShape("stacked isometric", TileSet.TileShapeEnum.Isometric);
        await TestShape("hexagon", TileSet.TileShapeEnum.Hexagon);
    }

    private async Task TestShape(string label, TileSet.TileShapeEnum shape)
    {
        var ground = GD.Load<TileSet>(Dir + "ground.tres");
        var tileSet = (TileSet)ground.Duplicate(true);
        tileSet.TileShape = shape;
        var layer = new TileMapLayer { TileSet = tileSet };
        for (int y = Map.Position.Y; y < Map.End.Y; y++)
        {
            for (int x = Map.Position.X; x < Map.End.X; x++)
            {
                layer.SetCell(new Vector2I(x, y), 0, Floor);
            }
        }
        layer.SetCell(WallCell, 0, Wall);
        layer.SetCell(MudCell, 0, Mud);
        AddChild(layer);
        var options = GD.Load<TileMapPathGraphOptions>(Dir + "path_graph_options.tres");
        _graph = TileMapPathGraph.Create(layer, options);
        Expect(_graph.IsCellBlocked(WallCell), $"{label}: a wall cell is drawn as blocked");
        Expect(!_graph.IsCellBlocked(FloorCell), $"{label}: a floor cell is not drawn as blocked");
        ExpectEqual(_graph.GetCellCost(MudCell), 20.0f, $"{label}: the mud cell is the most expensive (fully shaded) cell");

        // The recipe of the guide: a Node2D child of the layer draws in _Draw() and redraws on Updated.
        _overlay = new Node2D();
        _overlay.Draw += OnOverlayDraw;
        _graph.Updated += _overlay.QueueRedraw;
        _draws = 0;
        layer.AddChild(_overlay);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(_draws > 0, $"{label}: DrawDebug() runs in _Draw() (all cells, a rect, own colors, a rect outside)");
        int before = _draws;
        layer.SetCell(FloorCell, 0, Wall);
        _graph.UpdateCells([FloorCell]);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(_draws > before, $"{label}: Updated redraws the overlay");
        Expect(_graph.IsCellBlocked(FloorCell), $"{label}: the overlay draws the updated cell as blocked");
        _graph.Updated -= _overlay.QueueRedraw;
        layer.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void OnOverlayDraw()
    {
        Rid canvasItem = _overlay.GetCanvasItem();
        _graph.DrawDebug(canvasItem);
        _graph.DrawDebug(canvasItem, new Rect2I(2, 1, 3, 3), Colors.Red, Colors.Blue);
        _graph.DrawDebug(canvasItem, Outside); // Outside the region: draws nothing.
        _draws++;
    }
}
