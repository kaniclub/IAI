// ----------------------------
// 円形範囲へカマ効果とモンスターダメージを適用する。
// 発動時点で固定した対象を、ディレイ後にまとめて処理する。
// ----------------------------
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Tools;
using Object = StardewValley.Object;

namespace Iaigiri;

internal sealed class ScytheStrikeResolver
{
    private readonly ModConfig config;

    // ----------------------------
    // 現在設定を保持してインスタンスを初期化する。
    // ----------------------------
    public ScytheStrikeResolver(ModConfig config)
    {
        this.config = config;
    }

    // ----------------------------
    // 右クリックした瞬間の対象範囲とモンスター一覧を固定する。
    // ----------------------------
    public StrikeSnapshot Capture(Farmer who, MeleeWeapon scythe, Vector2 centerPixels, int radiusTiles)
    {
        GameLocation location = who.currentLocation;
        List<Vector2> tiles = CircleTileHelper.EnumerateTilesInCircle(centerPixels, radiusTiles).ToList();
        List<MonsterHitSnapshot> monsters = this.config.EnableMonsterDamage
            ? this.CaptureMonsters(location, who, scythe, centerPixels, radiusTiles)
            : new List<MonsterHitSnapshot>();

        StrikeDrawData drawData = this.GetDrawData(scythe);
        return new StrikeSnapshot(location, scythe, tiles, monsters, drawData);
    }

    // ----------------------------
    // 固定済みの対象へ実際の効果を適用する。
    // ----------------------------
    public void Apply(StrikeSnapshot snapshot)
    {
        foreach (Vector2 tile in snapshot.Tiles)
            this.ApplyWeaponToTile(snapshot.Location, snapshot.Scythe, tile);

        foreach (MonsterHitSnapshot monster in snapshot.Monsters)
        {
            if (monster.Target.currentLocation != snapshot.Location || !monster.Target.IsMonster || monster.Target.Health <= 0)
                continue;

            int actualDamage = monster.Target.takeDamage(monster.Damage, monster.XTrajectory, monster.YTrajectory, isBomb: false, monster.AddedPrecision, monster.Who);
            this.ShowDamageNumber(snapshot.Location, monster.Target, actualDamage);
        }
    }

    // ----------------------------
    // 指定タイルへ現在武器のツール処理を流す。
    // ----------------------------
    private void ApplyWeaponToTile(GameLocation location, MeleeWeapon weapon, Vector2 tile)
    {
        if (tile.X < 0 || tile.Y < 0)
            return;

        if (location.terrainFeatures.TryGetValue(tile, out var feature) && feature.performToolAction(weapon, 0, tile))
            location.terrainFeatures.Remove(tile);

        if (location.objects.TryGetValue(tile, out Object? obj) && obj.performToolAction(weapon))
        {
            obj.performRemoveAction();
            location.objects.Remove(tile);
        }

        location.performToolAction(weapon, (int)tile.X, (int)tile.Y);
    }

    // ----------------------------
    // 円内にいるモンスターのダメージ内容を固定する。
    // ----------------------------
    private List<MonsterHitSnapshot> CaptureMonsters(GameLocation location, Farmer who, MeleeWeapon scythe, Vector2 centerPixels, int radiusTiles)
    {
        List<MonsterHitSnapshot> results = new();
        float radiusPixels = radiusTiles * 64f;
        int minDamage = Math.Max(1, (int)Math.Ceiling(scythe.minDamage.Value * this.config.MonsterDamageMultiplier));
        int maxDamage = Math.Max(minDamage, (int)Math.Ceiling(scythe.maxDamage.Value * this.config.MonsterDamageMultiplier));
        double addedPrecision = Math.Max(0d, scythe.addedPrecision.Value / 10d);

        for (int i = location.characters.Count - 1; i >= 0; i--)
        {
            if (location.characters[i] is not Monster monster || !monster.IsMonster || monster.Health <= 0)
                continue;

            Rectangle monsterBox = monster.GetBoundingBox();
            Vector2 monsterCenter = new(monsterBox.Center.X, monsterBox.Center.Y);
            if (Vector2.DistanceSquared(centerPixels, monsterCenter) > radiusPixels * radiusPixels)
                continue;

            int damage = Game1.random.Next(minDamage, maxDamage + 1);
            Vector2 trajectory = Utility.getAwayFromPositionTrajectory(monsterBox, centerPixels);
            results.Add(new MonsterHitSnapshot(monster, who, damage, (int)trajectory.X, (int)trajectory.Y, addedPrecision));
        }

        return results;
    }

    // ----------------------------
    // 居合ポーズ描画用のスプライト情報を先に確定する。
    // ----------------------------
    private StrikeDrawData GetDrawData(MeleeWeapon scythe)
    {
        var itemData = ItemRegistry.GetDataOrErrorItem(scythe.GetDrawnItemId());
        Texture2D texture = itemData.GetTexture() ?? Tool.weaponsTexture;
        Rectangle sourceRect = itemData.GetSourceRect();
        return new StrikeDrawData(texture, sourceRect);
    }

    // ----------------------------
    // モンスターへ与えたダメージ数値を表示する。
    // ----------------------------
    private void ShowDamageNumber(GameLocation location, Monster monster, int actualDamage)
    {
        Rectangle monsterBox = monster.GetBoundingBox();
        if (actualDamage == -1)
        {
            string missText = Game1.content.LoadString("Strings\\StringsFromCSFiles:Attack_Miss");
            location.debris.Add(new Debris(missText, 1, new Vector2(monsterBox.Center.X, monsterBox.Center.Y), Color.LightGray, 1f, 0f));
            return;
        }

        if (actualDamage <= 0)
            return;

        location.removeDamageDebris(monster);
        location.debris.Add(new Debris(actualDamage, new Vector2(monsterBox.Center.X + 16, monsterBox.Center.Y), new Color(255, 130, 0), 1f, monster));
    }
}

internal sealed record StrikeSnapshot(GameLocation Location, MeleeWeapon Scythe, List<Vector2> Tiles, List<MonsterHitSnapshot> Monsters, StrikeDrawData DrawData);

internal sealed record StrikeDrawData(Texture2D Texture, Rectangle SourceRect);

internal sealed record MonsterHitSnapshot(Monster Target, Farmer Who, int Damage, int XTrajectory, int YTrajectory, double AddedPrecision);
