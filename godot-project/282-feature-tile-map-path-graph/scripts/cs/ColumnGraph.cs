using Godot;

namespace TruelyInteractive.Issue282FeatureTileMapPathGraph;

/// <summary>Blocks the column x = 0 except the gap and makes row y = 0 more expensive, without custom data.</summary>
public partial class ColumnGraph : TileMapPathGraph
{
    // Two gap cells, one in an even and one in an odd row: odd rows of stacked isometric maps are shifted half a cell.
    public static readonly Vector2I Gap = new(0, 3);
    public static readonly Vector2I Gap2 = new(0, 2);

    public override bool _IsCellBlocked(Vector2I cell, TileData tileData)
    {
        return cell.X == 0 && cell != Gap && cell != Gap2;
    }

    public override float _GetCellCost(Vector2I cell, TileData tileData)
    {
        return cell.Y == 0 ? 3.0f : 1.0f;
    }
}
