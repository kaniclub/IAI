// ----------------------------
// 円形範囲に含まれるタイル一覧を求める。
// 居合の地形効果と範囲表示で共通利用する。
// ----------------------------
using Microsoft.Xna.Framework;

namespace Iaigiri;

internal static class CircleTileHelper
{
    // ----------------------------
    // 中心座標と半径タイル数から円内タイルを列挙する。
    // ----------------------------
    public static IEnumerable<Vector2> EnumerateTilesInCircle(Vector2 centerPixels, int radiusTiles)
    {
        Vector2 centerTiles = centerPixels / 64f;
        int minX = (int)Math.Floor(centerTiles.X - radiusTiles);
        int maxX = (int)Math.Ceiling(centerTiles.X + radiusTiles);
        int minY = (int)Math.Floor(centerTiles.Y - radiusTiles);
        int maxY = (int)Math.Ceiling(centerTiles.Y + radiusTiles);
        float radiusSquared = radiusTiles * radiusTiles;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 tileCenter = new(x + 0.5f, y + 0.5f);
                if (Vector2.DistanceSquared(centerTiles, tileCenter) <= radiusSquared)
                    yield return new Vector2(x, y);
            }
        }
    }
}
