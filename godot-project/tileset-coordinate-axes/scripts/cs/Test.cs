using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.TileSetCoordinateAxes;

/// <summary>
/// Full API test of TileSet Coordinate Axes (TileSet: SwapXAndYAxis, FlipX, FlipY, LayoutToMapCoords, MapToLayoutCoords,
/// ConvertMapCoords; TileMapLayer.ConvertCellCoordinates). Same tests as scripts/gd/Test.gd.
/// </summary>
public partial class Test : FeatureTest
{
    private const string Dir = "res://tileset-coordinate-axes/";
    // The eight combinations, by index: bit 0 is FlipX, bit 1 FlipY, bit 2 SwapXAndYAxis
    // (the values of the former coordinate_axes property). Expected LayoutToMapCoords((1, 2)) for each.
    private static readonly Vector2I[] Expected12 =
    [
        new(1, 2), new(-1, 2), new(1, -2), new(-1, -2),
        new(2, 1), new(-2, 1), new(2, -1), new(-2, -1),
    ];
    private const int FlipY = 2;
    private const int Swap = 4;
    private const int SwapFlipX = 5;

    protected override Task RunTests()
    {
        TestDefaults();
        TestCombinations();
        TestLegacyValue();
        TestDirections();
        TestRoundTrip();
        TestConvertMapCoords();
        TestConvertCellCoordinates();
        TestExampleScene();
        return Task.CompletedTask;
    }

    private static TileSet MakeTileSet(TileSet.TileShapeEnum shape, TileSet.TileLayoutEnum layout, int index)
    {
        return new TileSet
        {
            TileShape = shape,
            TileLayout = layout,
            SwapXAndYAxis = (index & 4) != 0,
            FlipX = (index & 1) != 0,
            FlipY = (index & 2) != 0,
            TileSize = new Vector2I(32, 16),
        };
    }

    private void TestDefaults()
    {
        var plain = new TileSet();
        ExpectEqual((plain.SwapXAndYAxis, plain.FlipX, plain.FlipY), (false, false, false), "the coordinate system defaults to swap, flip X, flip Y off");
        Expect(plain.Get("coordinate_axes").VariantType == Variant.Type.Nil, "the old coordinate_axes property is not listed");
        string group = "";
        string groupOfSwap = "";
        foreach (Godot.Collections.Dictionary property in plain.GetPropertyList())
        {
            string name = property["name"].AsString();
            long usage = property["usage"].AsInt64();
            if ((usage & (long)PropertyUsageFlags.Group) != 0)
            {
                group = name;
            }
            else if (name == "swap_x_and_y_axis")
            {
                groupOfSwap = group;
            }
        }
        ExpectEqual(groupOfSwap, "Coordinate System", "swap_x_and_y_axis is in the inspector group Coordinate System");
    }

    private void TestCombinations()
    {
        for (int index = 0; index < 8; index++)
        {
            TileSet tileSet = MakeTileSet(TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, index);
            Vector2I mapCoords = tileSet.LayoutToMapCoords(new Vector2I(1, 2));
            ExpectEqual(mapCoords, Expected12[index], $"combination {index}: LayoutToMapCoords((1, 2))");
            Vector2I layoutCoords = tileSet.MapToLayoutCoords(Expected12[index]);
            ExpectEqual(layoutCoords, new Vector2I(1, 2), $"combination {index}: MapToLayoutCoords() reverses it");
        }
    }

    private void TestLegacyValue()
    {
        var tileSet = GD.Load<TileSet>(Dir + "resources/legacy_coordinate_axes.tres");
        ExpectEqual((tileSet.SwapXAndYAxis, tileSet.FlipX, tileSet.FlipY), (true, false, true), "a .tres with coordinate_axes = 6 loads as swap and flip Y");
        for (int index = 0; index < 8; index++)
        {
            var fresh = new TileSet();
            fresh.Set("coordinate_axes", index);
            var actual = (fresh.SwapXAndYAxis, fresh.FlipX, fresh.FlipY);
            var expected = ((index & 4) != 0, (index & 1) != 0, (index & 2) != 0);
            ExpectEqual(actual, expected, $"coordinate_axes = {index} sets the three bools");
        }
    }

    private void TestDirections()
    {
        var layer = new TileMapLayer();
        layer.TileSet = MakeTileSet(TileSet.TileShapeEnum.Isometric, TileSet.TileLayoutEnum.DiamondRight, Swap);
        Vector2 origin = layer.MapToLocal(new Vector2I(0, 0));
        Vector2 plusX = layer.MapToLocal(new Vector2I(1, 0)) - origin;
        Vector2 plusY = layer.MapToLocal(new Vector2I(0, 1)) - origin;
        ExpectEqual(plusX.Sign(), new Vector2(1, 1), "isometric with swap: +X goes down right");
        ExpectEqual(plusY.Sign(), new Vector2(1, -1), "isometric with swap: +Y goes up right");
        Vector2I bottomRight = layer.GetNeighborCell(new Vector2I(2, 3), TileSet.CellNeighbor.BottomRightSide);
        ExpectEqual(bottomRight, new Vector2I(3, 3), "isometric with swap: the bottom right neighbor is +X");

        layer.TileSet = MakeTileSet(TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, FlipY);
        Vector2 stepY = layer.MapToLocal(new Vector2I(0, 1)) - layer.MapToLocal(new Vector2I(0, 0));
        Expect(stepY.Y < 0, "square with flip Y: +Y goes up");
        Vector2I top = layer.GetNeighborCell(new Vector2I(0, 0), TileSet.CellNeighbor.TopSide);
        ExpectEqual(top, new Vector2I(0, 1), "square with flip Y: the top neighbor is +Y");
        layer.Free();
    }

    private void TestRoundTrip()
    {
        var layer = new TileMapLayer();
        int failures = 0;
        for (int index = 0; index < 8; index++)
        {
            layer.TileSet = MakeTileSet(TileSet.TileShapeEnum.Hexagon, TileSet.TileLayoutEnum.Stacked, index);
            for (int x = -3; x <= 3; x++)
            {
                for (int y = -3; y <= 3; y++)
                {
                    var cell = new Vector2I(x, y);
                    Vector2I back = layer.LocalToMap(layer.MapToLocal(cell));
                    if (back != cell)
                    {
                        failures++;
                    }
                }
            }
        }
        ExpectEqual(failures, 0, "hexagon: LocalToMap(MapToLocal(cell)) == cell for every combination");
        layer.Free();
    }

    private void TestConvertMapCoords()
    {
        TileSet tileSet = MakeTileSet(TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, SwapFlipX);
        Vector2I fromDefault = tileSet.ConvertMapCoords(new Vector2I(1, 2), TileSet.TileLayoutEnum.Stacked);
        ExpectEqual(fromDefault, new Vector2I(-2, 1), "ConvertMapCoords() from the default axes");
        Vector2I fromSame = tileSet.ConvertMapCoords(new Vector2I(-2, 1), TileSet.TileLayoutEnum.Stacked, true, true, false);
        ExpectEqual(fromSame, new Vector2I(-2, 1), "ConvertMapCoords() from the same axes keeps the cell");
    }

    private void TestConvertCellCoordinates()
    {
        TileSet tileSet = MakeTileSet(TileSet.TileShapeEnum.Isometric, TileSet.TileLayoutEnum.Stacked, 0);
        var layer = new TileMapLayer();
        layer.TileSet = tileSet;
        Vector2I[] cells = [new(0, 0), new(1, 0), new(1, 1), new(2, 3)];
        var positions = new Dictionary<Vector2I, Vector2>();
        foreach (Vector2I cell in cells)
        {
            layer.SetCell(cell, 0, new Vector2I(cell.X, 0));
            positions[cell] = layer.MapToLocal(cell);
        }
        tileSet.TileLayout = TileSet.TileLayoutEnum.DiamondRight;
        tileSet.SwapXAndYAxis = true;
        layer.ConvertCellCoordinates(TileSet.TileLayoutEnum.Stacked);
        int moved = 0;
        foreach (Vector2I cell in cells)
        {
            Vector2I newCell = layer.LocalToMap(positions[cell]);
            if (layer.GetCellAtlasCoords(newCell) != new Vector2I(cell.X, 0))
            {
                moved++;
            }
        }
        ExpectEqual(moved, 0, "ConvertCellCoordinates() keeps every cell at its place");

        tileSet.TileLayout = TileSet.TileLayoutEnum.Stacked;
        tileSet.SwapXAndYAxis = false;
        layer.ConvertCellCoordinates(TileSet.TileLayoutEnum.DiamondRight, true, false, false);
        int lost = 0;
        foreach (Vector2I cell in cells)
        {
            if (layer.GetCellAtlasCoords(cell) != new Vector2I(cell.X, 0))
            {
                lost++;
            }
        }
        ExpectEqual(lost, 0, "converting back restores every cell");
        ExpectEqual(layer.GetUsedCells().Count, cells.Length, "converting keeps the number of cells");
        layer.Free();
    }

    private void TestExampleScene()
    {
        var scene = GD.Load<PackedScene>(Dir + "scenes/isometric_axes_demo.tscn");
        var layer = scene.Instantiate<TileMapLayer>();
        Expect(layer.TileSet.SwapXAndYAxis, "the demo TileSet swaps X and Y");
        ExpectEqual(layer.GetUsedCells().Count, 9, "the demo map has 9 cells");
        layer.Free();
    }
}
