using Godot;

namespace TruelyInteractive.TileSetCoordinateAxes;

/// <summary>Minimal example of TileSet Coordinate Axes: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Setting the axes in code
        var tileSet = new TileSet
        {
            TileShape = TileSet.TileShapeEnum.Isometric,
            TileLayout = TileSet.TileLayoutEnum.DiamondRight,
            SwapXAndYAxis = true,
        };
        var layer = GetNode<TileMapLayer>("TileMapLayer");
        layer.TileSet = tileSet;
        Vector2I cell = layer.LocalToMap(layer.GetLocalMousePosition());
        GD.Print("Clicked cell: ", cell);

        // Changing the axes of a painted map
        TileSet.TileLayoutEnum oldLayout = tileSet.TileLayout;
        bool oldSwap = tileSet.SwapXAndYAxis;
        bool oldFlipX = tileSet.FlipX;
        bool oldFlipY = tileSet.FlipY;
        tileSet.FlipY = true;
        GetNode<TileMapLayer>("TileMapLayer").ConvertCellCoordinates(oldLayout, oldSwap, oldFlipX, oldFlipY);
        Vector2I stored = tileSet.ConvertMapCoords(new Vector2I(1, 2), oldLayout, oldSwap, oldFlipX, oldFlipY);
        GD.Print("Stored cell (1, 2) after FlipY: ", stored);

        // Layout coordinates (FlipY back off, so the values match the comments)
        tileSet.FlipY = false;
        tileSet.SwapXAndYAxis = true;
        tileSet.FlipX = true;
        GD.Print(tileSet.LayoutToMapCoords(new Vector2I(1, 2))); // (-2, 1)
        GD.Print(tileSet.MapToLayoutCoords(new Vector2I(-2, 1))); // (1, 2)
        GetTree().Quit();
    }
}
