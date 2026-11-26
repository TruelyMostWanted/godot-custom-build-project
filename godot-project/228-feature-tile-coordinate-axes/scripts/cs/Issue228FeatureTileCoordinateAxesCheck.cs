using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue228FeatureTileCoordinateAxes;

/// <summary>C# checks of 228-feature-tile-coordinate-axes. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue228FeatureTileCoordinateAxesCheck : FeatureCheckCs
{
    private const string Dir = "res://228-feature-tile-coordinate-axes/";

    protected override string CheckName => "228-feature-tile-coordinate-axes";

    protected override Task RunChecks()
    {
        CheckApi();
        CheckIsometricDiagonal();
        CheckSquareYUp();
        CheckRoundTrip();
        CheckConvertCells();
        CheckExampleScene();
        return Task.CompletedTask;
    }

    private static TileSet MakeTileSet(TileSet.TileShapeEnum shape, TileSet.TileLayoutEnum layout, TileSet.CoordinateAxesEnum axes)
    {
        return new TileSet { TileShape = shape, TileLayout = layout, CoordinateAxes = axes, TileSize = new Vector2I(32, 16) };
    }

    private void CheckApi()
    {
        ExpectEqual(new TileSet().CoordinateAxes, TileSet.CoordinateAxesEnum.Default, "coordinate_axes defaults to DEFAULT");
        ExpectEqual((long)TileSet.CoordinateAxesEnum.Max, 8L, "eight coordinate axes");
        TileSet tileSet = MakeTileSet(TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, TileSet.CoordinateAxesEnum.SwapFlipX);
        ExpectEqual(tileSet.LayoutToMapCoords(new Vector2I(1, 2)), new Vector2I(-2, 1), "swap, then flip X");
        ExpectEqual(tileSet.MapToLayoutCoords(new Vector2I(-2, 1)), new Vector2I(1, 2), "map_to_layout_coords() reverses it");
    }

    private void CheckIsometricDiagonal()
    {
        var layer = new TileMapLayer { TileSet = MakeTileSet(TileSet.TileShapeEnum.Isometric, TileSet.TileLayoutEnum.DiamondRight, TileSet.CoordinateAxesEnum.Swap) };
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
        var layer = new TileMapLayer { TileSet = MakeTileSet(TileSet.TileShapeEnum.Square, TileSet.TileLayoutEnum.Stacked, TileSet.CoordinateAxesEnum.FlipY) };
        Expect(layer.MapToLocal(new Vector2I(0, 1)).Y < layer.MapToLocal(new Vector2I(0, 0)).Y, "square: +Y goes up");
        ExpectEqual(layer.GetNeighborCell(new Vector2I(0, 0), TileSet.CellNeighbor.TopSide), new Vector2I(0, 1), "the top neighbor is +Y");
        layer.Free();
    }

    private void CheckRoundTrip()
    {
        bool ok = true;
        var layer = new TileMapLayer();
        for (int axes = 0; axes < (int)TileSet.CoordinateAxesEnum.Max; axes++)
        {
            layer.TileSet = MakeTileSet(TileSet.TileShapeEnum.Hexagon, TileSet.TileLayoutEnum.Stacked, (TileSet.CoordinateAxesEnum)axes);
            for (int x = -3; x <= 3; x++)
            {
                for (int y = -3; y <= 3; y++)
                {
                    var cell = new Vector2I(x, y);
                    ok = ok && layer.LocalToMap(layer.MapToLocal(cell)) == cell;
                }
            }
        }
        Expect(ok, "hexagon: local_to_map(map_to_local(c)) == c for every axes");
        layer.Free();
    }

    private void CheckConvertCells()
    {
        TileSet tileSet = MakeTileSet(TileSet.TileShapeEnum.Isometric, TileSet.TileLayoutEnum.Stacked, TileSet.CoordinateAxesEnum.Default);
        var layer = new TileMapLayer { TileSet = tileSet };
        Vector2I[] cells = { new(0, 0), new(1, 0), new(1, 1), new(2, 3) };
        var positions = new Dictionary<Vector2I, Vector2>();
        foreach (Vector2I cell in cells)
        {
            layer.SetCell(cell, 0, new Vector2I(cell.X, 0));
            positions[cell] = layer.MapToLocal(cell);
        }
        tileSet.TileLayout = TileSet.TileLayoutEnum.DiamondRight;
        tileSet.CoordinateAxes = TileSet.CoordinateAxesEnum.Swap;
        layer.ConvertCellCoordinates(TileSet.TileLayoutEnum.Stacked, TileSet.CoordinateAxesEnum.Default);
        bool ok = layer.GetUsedCells().Count == cells.Length;
        foreach (Vector2I cell in cells)
        {
            Vector2I newCell = layer.LocalToMap(positions[cell]);
            ok = ok && layer.GetCellAtlasCoords(newCell) == new Vector2I(cell.X, 0);
        }
        Expect(ok, "convert_cell_coordinates() keeps every cell at its place");
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
        ExpectEqual(layer.TileSet.CoordinateAxes, TileSet.CoordinateAxesEnum.Swap, "the example TileSet uses SWAP");
        ExpectEqual(layer.GetUsedCells().Count, 9, "the example map has 9 cells");
        layer.Free();
    }
}
