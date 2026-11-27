using Godot;

namespace TruelyInteractive.TileMapPathGraphDebug;

/// <summary>
/// Minimal example of Tile Map Path Graph Debug: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.
/// A child of the painted layer of the scene (map and options from tile-map-pathfinding/resources).
/// "In the TileMap editor" is editor-only and not part of this example.
/// </summary>
public partial class Example : Node2D
{
    private const string OptionsPath = "res://tile-map-pathfinding/resources/path_graph_options.tres";

    private TileMapPathGraph _graph;
    private Rect2I _shown;

    // Runtime overlay in 4 steps
    public override async void _Ready()
    {
        if (!OS.IsDebugBuild())
        {
            return;
        }
        _graph = TileMapPathGraph.Create(GetParent<TileMapLayer>(), GD.Load<TileMapPathGraphOptions>(OptionsPath));
        _graph.Updated += QueueRedraw;
        QueueRedraw();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print("Drawn cells: ", _shown);

        // Colors and shading
        _graph.DrawDebug(GetCanvasItem(), new Rect2I(), new Color(0.8f, 0, 0.8f, 0.6f), new Color(0, 0.6f, 1, 0.6f));
        GD.Print("Wall blocked: ", _graph.IsCellBlocked(new Vector2I(4, 0)));
        GD.Print("Cost of the shaded cell: ", _graph.GetCellCost(new Vector2I(2, 2)));
        _graph.Updated -= QueueRedraw;
        GetTree().Quit();
    }

    // Large maps: draw only the visible cells
    public override void _Process(double delta)
    {
        Rect2I cells = VisibleCells();
        if (cells != _shown)
        {
            _shown = cells;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        _graph?.DrawDebug(GetCanvasItem(), _shown);
    }

    private Rect2I VisibleCells()
    {
        var layer = GetParent<TileMapLayer>();
        Transform2D toLocal = GetGlobalTransformWithCanvas().AffineInverse();
        Rect2 screen = GetViewportRect();
        var rect = new Rect2I(layer.LocalToMap(toLocal * screen.Position), Vector2I.Zero);
        rect = rect.Expand(layer.LocalToMap(toLocal * screen.End));
        rect = rect.Expand(layer.LocalToMap(toLocal * new Vector2(screen.End.X, screen.Position.Y)));
        rect = rect.Expand(layer.LocalToMap(toLocal * new Vector2(screen.Position.X, screen.End.Y)));
        return rect.Grow(1);
    }
}
