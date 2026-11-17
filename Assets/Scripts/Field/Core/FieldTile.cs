namespace SoccerIsland.Field
{
    /// <summary>
    /// タイルの種類。並びは Tools/PixelArt/generate_art.py の TILE_NAMES と同じ（tiles.png の並び）。
    /// </summary>
    public enum FieldTile
    {
        Grass, Grass2, Flowers, Path, Sand, Water, Water2, Shore,
        Rock, Tree, Pitch, PitchDark, LineH, LineV, Goal, Floor,
        RoofRed, RoofBlue, RoofGreen, RoofGray, RoofBrown, RoofWhite, Wall, WallWindow,
        Door, Fence, Sign, Lantern, Asphalt, RoadLine, Pier, StoneWall,
        CrossSign, Stall, Bush, DarkGrass, Bench, Well, Crop, Lamp,
        None = -1,
    }

    public static class FieldTiles
    {
        public const int SheetColumns = 8;
        public const int Size = 16;

        /// <summary>通れないタイル。</summary>
        public static bool IsSolid(FieldTile t)
        {
            switch (t)
            {
                case FieldTile.Water:
                case FieldTile.Water2:
                case FieldTile.Rock:
                case FieldTile.Tree:
                case FieldTile.RoofRed:
                case FieldTile.RoofBlue:
                case FieldTile.RoofGreen:
                case FieldTile.RoofGray:
                case FieldTile.RoofBrown:
                case FieldTile.RoofWhite:
                case FieldTile.Wall:
                case FieldTile.WallWindow:
                case FieldTile.Fence:
                case FieldTile.StoneWall:
                case FieldTile.CrossSign:
                case FieldTile.Stall:
                case FieldTile.Bush:
                case FieldTile.Well:
                case FieldTile.Goal:
                case FieldTile.Sign:
                case FieldTile.Lantern:
                case FieldTile.Lamp:
                    return true;
                default:
                    return false;
            }
        }
    }
}
