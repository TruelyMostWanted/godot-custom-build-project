using Godot;

namespace TruelyInteractive.TileMapPathfinding;

/// <summary>The "Costs and walls from code" snippet of the guide: terrains of resources/ground.tres decide walls and costs.</summary>
public partial class RiverGraph : TileMapPathGraph
{
    public override bool _IsCellBlocked(Vector2I cell, TileData tileData)
    {
        return tileData == null || tileData.Terrain == 2; // Deep water.
    }

    public override float _GetCellCost(Vector2I cell, TileData tileData)
    {
        if (tileData != null && tileData.Terrain == 1)
        {
            return 2.0f; // Shallow water is slow.
        }
        return 1.0f;
    }
}
