using Godot;

namespace TruelyInteractive.TileMapPathfinding;

/// <summary>
/// Minimal example of Tile Map Pathfinding: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.
/// Steps 1 to 4 (custom data layers, painting) are editor work: resources/ground.tres and the painted Ground layer.
/// </summary>
public partial class Example : Node
{
    private TileMapLayer _ground;
    private TileMapPathGraph _graph;

    public override void _Ready()
    {
        // Grid pathfinding in 6 steps
        _ground = GetNode<TileMapLayer>("Ground");
        var options = new TileMapPathGraphOptions { BlockedDataLayer = "blocked", CostDataLayer = "cost" };
        _graph = TileMapPathGraph.Create(_ground, options);
        GD.Print("Path around the wall: ", string.Join(", ", Walk(new Vector2I(0, 2), new Vector2I(7, 2))));

        // Update the graph when cells change
        OpenDoor(new Vector2I(4, 2));
        GD.Print("Path through the door: ", string.Join(", ", Walk(new Vector2I(0, 2), new Vector2I(7, 2))));

        // Diagonal steps and corner cutting
        options.NeighborMode = TileMapPathGraphOptions.NeighborModeEnum.SidesAndCorners;
        options.DiagonalMode = AStarGrid2D.DiagonalModeEnum.AtLeastOneWalkable;
        _graph.UpdateAll();
        GD.Print("Path with corner steps: ", string.Join(", ", Walk(new Vector2I(0, 0), new Vector2I(7, 4))));

        // Costs and walls from code (RiverGraph.cs)
        var riverGraph = new RiverGraph();
        riverGraph.SetLayer(_ground);
        riverGraph.UpdateAll();
        GD.Print("Cost of shallow water: ", riverGraph.GetCellCost(new Vector2I(2, 2)));

        // Large maps
        _graph.GetAstarGrid().JumpingEnabled = true;
        Godot.Collections.Array<Vector2I> partial = _graph.GetCellPath(new Vector2I(0, 0), new Vector2I(4, 0), true);
        GD.Print("Partial path to deep water: ", partial);
        GetTree().Quit();
    }

    public Vector2[] Walk(Vector2I from, Vector2I to)
    {
        return _graph.GetPointPath(from, to); // Local positions of the layer.
    }

    public void OpenDoor(Vector2I cell)
    {
        _ground.SetCell(cell, 0, new Vector2I(0, 0)); // A floor tile.
        _graph.UpdateCells([cell]);
    }
}
