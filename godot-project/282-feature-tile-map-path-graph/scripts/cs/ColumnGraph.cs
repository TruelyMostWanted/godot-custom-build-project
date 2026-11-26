using Godot;

namespace TruelyInteractive.Issue282FeatureTileMapPathGraph;

/// <summary>Blocks the column x = 0 except the gap and makes row y = 0 more expensive, without custom data.</summary>
public partial class ColumnGraph : TileMapPathGraph
{
    public static readonly Vector2I Gap = new(0, 3);

    public override bool _IsCellBlocked(Vector2I cell, TileData tileData)
    {
        return cell.X == 0 && cell != Gap;
    }

    public override float _GetCellCost(Vector2I cell, TileData tileData)
    {
        return cell.Y == 0 ? 3.0f : 1.0f;
    }
}
