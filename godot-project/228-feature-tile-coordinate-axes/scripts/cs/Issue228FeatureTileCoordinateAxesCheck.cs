using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue228FeatureTileCoordinateAxes;

/// <summary>
/// C# checks of 228-feature-tile-coordinate-axes (coordinate system as three bools since engine #261).
/// Check the same things as scripts/gd/check.gd.
/// </summary>
public partial class Issue228FeatureTileCoordinateAxesCheck : FeatureCheckCs
{
    private const string Dir = "res://228-feature-tile-coordinate-axes/";

    // The eight combinations, by index: bit 0 is FlipX, bit 1 FlipY, bit 2 SwapXAndYAxis
    // (the values of the former coordinate_axes property). Expected LayoutToMapCoords(new Vector2I(1, 2)) for each.
    private static readonly Vector2I[] Expected12 =
    {
        new(1, 2), new(-1, 2), new(1, -2), new(-1, -2),
        new(2, 1), new(-2, 1), new(2, -1), new(-2, -1),
    };

    protected override string CheckName => "228-feature-tile-coordinate-axes";

    protected override Task RunChecks()
    {
        CheckApi();
        CheckAllCombinations();
        CheckLegacyValue();
        CheckIsometricDiagonal();
        CheckSquareYUp();
        CheckRoundTrip();
        CheckConvertCells();
        CheckExampleScene();
        return Task.CompletedTask;
    }

    private static TileSet MakeTileSet(TileSet.TileShapeEnum shape, TileSet.TileLayoutEnum layout, bool swap, bool flipX, bool flipY)
    {
        return new TileSet { TileShape = shape, TileLayout = layout, SwapXAndYAxis = swap, FlipX = flipX, FlipY = flipY, TileSize = new Vector2I(32, 16) };
    }

    private static TileSet MakeTileSet(TileSet.TileShapeEnum shape, TileSet.TileLayoutEnum layout, int index)
    {
        return MakeTileSet(shape, layout, (index & 4) != 0, (index & 1) != 0, (index & 2) != 0);
    }

    private void CheckApi()
    {
        var plain = new TileSet();
        Expect(!plain.SwapXAndYAxis && !plain.FlipX && !plain.FlipY, "the coordinate system defaults to false, false, false");
        Expect(plain.Get("coordinate_axes").VariantType == Variant.Type.Nil, "the old coordinate_axes property is gone");
        bool grouped = false;
        bool inGroup = false;
        foreach (Godot.Collections.Dictionary property in plain.GetPropertyList())
        {
            string name = property["name"].AsString();
            if ((property["usage"].AsInt64() & (long)PropertyUsageFlags.Group) != 0)
            {
                inGroup = name == "Coordinate System";
            }
            else if (name == "swap_x_and_y_axis")
            {
                grouped = inGroup;
            }
        }
        Expect(grouped, "swap_x_and_y_axis is in the inspector group Coordinate System");
        TileSet tileSet = MakeTileSet(TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, true, true, false);
        ExpectEqual(tileSet.LayoutToMapCoords(new Vector2I(1, 2)), new Vector2I(-2, 1), "swap, then flip X");
        ExpectEqual(tileSet.MapToLayoutCoords(new Vector2I(-2, 1)), new Vector2I(1, 2), "map_to_layout_coords() reverses it");
    }

    private void CheckAllCombinations()
    {
        for (int index = 0; index < 8; index++)
        {
            TileSet tileSet = MakeTileSet(TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, index);
            ExpectEqual(tileSet.LayoutToMapCoords(new Vector2I(1, 2)), Expected12[index], $"combination {index}: layout (1, 2)");
            ExpectEqual(tileSet.MapToLayoutCoords(Expected12[index]), new Vector2I(1, 2), $"combination {index}: reverse");
        }
    }

    private void CheckLegacyValue()
    {
        var tileSet = GD.Load<TileSet>(Dir + "resources/legacy_coordinate_axes.tres");
        Expect(tileSet != null, "a TileSet saved with coordinate_axes loads");
        if (tileSet == null)
        {
            return;
        }
        Expect(tileSet.SwapXAndYAxis && !tileSet.FlipX && tileSet.FlipY, "coordinate_axes = 6 loads as swap and flip Y");
        for (int index = 0; index < 8; index++)
        {
            var fresh = new TileSet();
            fresh.Set("coordinate_axes", index);
            bool ok = fresh.SwapXAndYAxis == ((index & 4) != 0) && fresh.FlipX == ((index & 1) != 0) && fresh.FlipY == ((index & 2) != 0);
            Expect(ok, $"coordinate_axes = {index} maps to the three bools");
        }
    }

    private void CheckIsometricDiagonal()
    {
        var layer = new TileMapLayer { TileSet = MakeTileSet(TileSet.TileShapeEnum.Isometric, TileSet.TileLayoutEnum.DiamondRight, true, false, false) };
        Vector2 origin = layer.MapToLocal(new Vector2I(0, 0));
        Vector2 plusX = layer.MapToLocal(new Vector2I(1, 0)) - origin;
        Vector2 plusY = layer.MapToLocal(new Vector2I(0, 1)) - origin;
        Expect(plusX.X > 0 && plusX.Y > 0, "isometric diagonal: +X goes down right");
        Expect(plusY.X > 0 && plusY.Y < 0, "isometric diagonal: +Y goes up right");
        ExpectEqual(layer.GetNeighborCell(new Vector2I(2, 3), TileSet.CellNeighbor.BottomRightSide), new Vector2I(3, 3), "the bottom right neighbor is +X");
        layer.Free();
    }

    private void CheckSquareYUp()
    {
        var layer = new TileMapLayer { TileSet = MakeTileSet(TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, false, false, true) };
        Expect(layer.MapToLocal(new Vector2I(0, 1)).Y < layer.MapToLocal(new Vector2I(0, 0)).Y, "square: +Y goes up");
        ExpectEqual(layer.GetNeighborCell(new Vector2I(0, 0), TileSet.CellNeighbor.TopSide), new Vector2I(0, 1), "the top neighbor is +Y");
        layer.Free();
    }

    private void CheckRoundTrip()
    {
        bool ok = true;
        var layer = new TileMapLayer();
        for (int index = 0; index < 8; index++)
        {
            layer.TileSet = MakeTileSet(TileSet.TileShapeEnum.Hexagon, TileSet.TileLayoutEnum.Stacked, index);
            for (int x = -3; x <= 3; x++)
            {
                for (int y = -3; y <= 3; y++)
                {
                    var cell = new Vector2I(x, y);
                    ok = ok && layer.LocalToMap(layer.MapToLocal(cell)) == cell;
                }
            }
        }
        Expect(ok, "hexagon: local_to_map(map_to_local(c)) == c for every combination");
        layer.Free();
    }

    private void CheckConvertCells()
    {
        TileSet tileSet = MakeTileSet(TileSet.TileShapeEnum.Isometric, TileSet.TileLayoutEnum.Stacked, false, false, false);
        var layer = new TileMapLayer { TileSet = tileSet };
        Vector2I[] cells = { new(0, 0), new(1, 0), new(1, 1), new(2, 3) };
        var positions = new Dictionary<Vector2I, Vector2>();
        foreach (Vector2I cell in cells)
        {
            layer.SetCell(cell, 0, new Vector2I(cell.X, 0));
            positions[cell] = layer.MapToLocal(cell);
        }
        tileSet.TileLayout = TileSet.TileLayoutEnum.DiamondRight;
        tileSet.SwapXAndYAxis = true;
        layer.ConvertCellCoordinates(TileSet.TileLayoutEnum.Stacked);
        bool ok = layer.GetUsedCells().Count == cells.Length;
        foreach (Vector2I cell in cells)
        {
            Vector2I newCell = layer.LocalToMap(positions[cell]);
            ok = ok && layer.GetCellAtlasCoords(newCell) == new Vector2I(cell.X, 0);
        }
        Expect(ok, "convert_cell_coordinates() keeps every cell at its place");

        // And back, from the settings the cells were converted to.
        tileSet.TileLayout = TileSet.TileLayoutEnum.Stacked;
        tileSet.SwapXAndYAxis = false;
        layer.ConvertCellCoordinates(TileSet.TileLayoutEnum.DiamondRight, true, false, false);
        ok = layer.GetUsedCells().Count == cells.Length;
        foreach (Vector2I cell in cells)
        {
            ok = ok && layer.GetCellAtlasCoords(cell) == new Vector2I(cell.X, 0);
        }
        Expect(ok, "converting back restores the cells");
        layer.Free();
    }

    private void CheckExampleScene()
    {
        var scene = GD.Load<PackedScene>(Dir + "packed-scenes/isometric_axes_demo.tscn");
        Expect(scene != null, "the example scene loads");
        if (scene == null)
        {
            return;
        }
        var layer = scene.Instantiate<TileMapLayer>();
        TileSet tileSet = layer.TileSet;
        Expect(tileSet.SwapXAndYAxis && !tileSet.FlipX && !tileSet.FlipY, "the example TileSet swaps X and Y");
        ExpectEqual(layer.GetUsedCells().Count, 9, "the example map has 9 cells");
        layer.Free();
    }
}
