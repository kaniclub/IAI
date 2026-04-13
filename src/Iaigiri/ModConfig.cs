// ----------------------------
// Iaigiri の設定値を保持する。
// ----------------------------
namespace Iaigiri;

internal sealed class ModConfig
{
    public const int MinRadiusTiles = 1;
    public const int MaxRadiusTiles = 12;
    public const int MinDelayMilliseconds = 0;
    public const int MaxDelayMilliseconds = 2000;
    public const int MinCooldownMilliseconds = 0;
    public const int MaxCooldownMilliseconds = 10000;
    public const int MinEffectDurationMilliseconds = 1;
    public const int MaxEffectDurationMilliseconds = 2000;
    public const float MinMonsterDamageMultiplier = 0f;
    public const float MaxMonsterDamageMultiplier = 100f;
    public const int MinSoundVolumePercent = 0;
    public const int MaxSoundVolumePercent = 100;

    public bool ModEnabled { get; set; } = true;

    public bool ReplaceSwordsWithIaigiri { get; set; } = true;

    public int SwordRadiusTiles { get; set; } = 4;

    public int StandardScytheRadiusTiles { get; set; } = 2;

    public int GoldenScytheRadiusTiles { get; set; } = 3;

    public int IridiumScytheRadiusTiles { get; set; } = 4;

    public int StrikeDelayMilliseconds { get; set; } = 650;

    public int CooldownMilliseconds { get; set; } = 1200;

    public int EffectDurationMilliseconds { get; set; } = 110;

    public float MonsterDamageMultiplier { get; set; } = 2.0f;

    public bool EnableMonsterDamage { get; set; } = true;

    public bool EnableSoundEffects { get; set; } = true;

    public int SoundVolumePercent { get; set; } = 50;

    // ----------------------------
    // 設定値を有効範囲へ補正する。
    // ----------------------------
    public void Normalize()
    {
        this.SwordRadiusTiles = Math.Clamp(this.SwordRadiusTiles, MinRadiusTiles, MaxRadiusTiles);
        this.StandardScytheRadiusTiles = Math.Clamp(this.StandardScytheRadiusTiles, MinRadiusTiles, MaxRadiusTiles);
        this.GoldenScytheRadiusTiles = Math.Clamp(this.GoldenScytheRadiusTiles, MinRadiusTiles, MaxRadiusTiles);
        this.IridiumScytheRadiusTiles = Math.Clamp(this.IridiumScytheRadiusTiles, MinRadiusTiles, MaxRadiusTiles);
        this.StrikeDelayMilliseconds = Math.Clamp(this.StrikeDelayMilliseconds, MinDelayMilliseconds, MaxDelayMilliseconds);
        this.CooldownMilliseconds = Math.Clamp(this.CooldownMilliseconds, MinCooldownMilliseconds, MaxCooldownMilliseconds);
        this.EffectDurationMilliseconds = Math.Clamp(this.EffectDurationMilliseconds, MinEffectDurationMilliseconds, MaxEffectDurationMilliseconds);
        this.MonsterDamageMultiplier = Math.Clamp(this.MonsterDamageMultiplier, MinMonsterDamageMultiplier, MaxMonsterDamageMultiplier);
        this.SoundVolumePercent = Math.Clamp(this.SoundVolumePercent, MinSoundVolumePercent, MaxSoundVolumePercent);
    }
}
