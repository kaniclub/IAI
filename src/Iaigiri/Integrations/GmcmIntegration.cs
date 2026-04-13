// ----------------------------
// Generic Mod Config Menu に Iaigiri の設定項目を登録する。
// ----------------------------
using StardewModdingAPI;

namespace Iaigiri.Integrations;

internal sealed class GmcmIntegration
{
    private const string GmcmId = "spacechase0.GenericModConfigMenu";

    private readonly IModHelper helper;
    private readonly IManifest manifest;
    private readonly ITranslationHelper translation;
    private readonly Func<ModConfig> getConfig;
    private readonly Action resetAllSettings;
    private readonly Action saveAllSettings;

    // ----------------------------
    // 必要な初期値を設定してインスタンスを初期化する。
    // ----------------------------
    public GmcmIntegration(
        IModHelper helper,
        IManifest manifest,
        ITranslationHelper translation,
        Func<ModConfig> getConfig,
        Action resetAllSettings,
        Action saveAllSettings)
    {
        this.helper = helper;
        this.manifest = manifest;
        this.translation = translation;
        this.getConfig = getConfig;
        this.resetAllSettings = resetAllSettings;
        this.saveAllSettings = saveAllSettings;
    }

    // ----------------------------
    // 設定項目の登録または再登録を行う。
    // ----------------------------
    public void RegisterOrReload()
    {
        IGenericModConfigMenuApi? api = this.helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(GmcmId);
        if (api is null)
            return;

        api.Unregister(this.manifest);
        api.Register(this.manifest, this.resetAllSettings, this.saveAllSettings);

        this.AddGeneral(api);
        this.AddEffect(api);
        this.AddDamage(api);
    }

    // ----------------------------
    // 基本設定の項目を登録する。
    // ----------------------------
    private void AddGeneral(IGenericModConfigMenuApi api)
    {
        api.AddSectionTitle(this.manifest, () => this.T("gmcm.section.general.name"), () => this.T("gmcm.section.general.desc"));
        api.AddBoolOption(this.manifest, () => this.getConfig().ModEnabled, value => this.getConfig().ModEnabled = value, () => this.T("gmcm.enabled.name"), () => this.T("gmcm.enabled.desc"));
        api.AddBoolOption(this.manifest, () => this.getConfig().ReplaceSwordsWithIaigiri, value => this.getConfig().ReplaceSwordsWithIaigiri = value, () => this.T("gmcm.replace_swords.name"), () => this.T("gmcm.replace_swords.desc"));
        api.AddNumberOption(this.manifest, () => this.getConfig().SwordRadiusTiles, value => this.getConfig().SwordRadiusTiles = value, () => this.T("gmcm.sword_radius.name"), () => this.T("gmcm.sword_radius.desc"), ModConfig.MinRadiusTiles, ModConfig.MaxRadiusTiles, 1);
        api.AddNumberOption(this.manifest, () => this.getConfig().StandardScytheRadiusTiles, value => this.getConfig().StandardScytheRadiusTiles = value, () => this.T("gmcm.standard_radius.name"), () => this.T("gmcm.standard_radius.desc"), ModConfig.MinRadiusTiles, ModConfig.MaxRadiusTiles, 1);
        api.AddNumberOption(this.manifest, () => this.getConfig().GoldenScytheRadiusTiles, value => this.getConfig().GoldenScytheRadiusTiles = value, () => this.T("gmcm.golden_radius.name"), () => this.T("gmcm.golden_radius.desc"), ModConfig.MinRadiusTiles, ModConfig.MaxRadiusTiles, 1);
        api.AddNumberOption(this.manifest, () => this.getConfig().IridiumScytheRadiusTiles, value => this.getConfig().IridiumScytheRadiusTiles = value, () => this.T("gmcm.iridium_radius.name"), () => this.T("gmcm.iridium_radius.desc"), ModConfig.MinRadiusTiles, ModConfig.MaxRadiusTiles, 1);
        api.AddNumberOption(this.manifest, () => this.getConfig().StrikeDelayMilliseconds, value => this.getConfig().StrikeDelayMilliseconds = value, () => this.T("gmcm.delay.name"), () => this.T("gmcm.delay.desc"), ModConfig.MinDelayMilliseconds, ModConfig.MaxDelayMilliseconds, 10);
        api.AddNumberOption(this.manifest, () => this.getConfig().CooldownMilliseconds, value => this.getConfig().CooldownMilliseconds = value, () => this.T("gmcm.cooldown.name"), () => this.T("gmcm.cooldown.desc"), ModConfig.MinCooldownMilliseconds, ModConfig.MaxCooldownMilliseconds, 10);
    }

    // ----------------------------
    // エフェクトと SE の項目を登録する。
    // ----------------------------
    private void AddEffect(IGenericModConfigMenuApi api)
    {
        api.AddSectionTitle(this.manifest, () => this.T("gmcm.section.effect.name"), () => this.T("gmcm.section.effect.desc"));
        api.AddNumberOption(this.manifest, () => this.getConfig().EffectDurationMilliseconds, value => this.getConfig().EffectDurationMilliseconds = value, () => this.T("gmcm.effect_duration.name"), () => this.T("gmcm.effect_duration.desc"), ModConfig.MinEffectDurationMilliseconds, ModConfig.MaxEffectDurationMilliseconds, 10);
        api.AddBoolOption(this.manifest, () => this.getConfig().EnableSoundEffects, value => this.getConfig().EnableSoundEffects = value, () => this.T("gmcm.enable_sound.name"), () => this.T("gmcm.enable_sound.desc"));
        api.AddNumberOption(this.manifest, () => this.getConfig().SoundVolumePercent, value => this.getConfig().SoundVolumePercent = value, () => this.T("gmcm.sound_volume.name"), () => this.T("gmcm.sound_volume.desc"), ModConfig.MinSoundVolumePercent, ModConfig.MaxSoundVolumePercent, 5);
    }

    // ----------------------------
    // モンスターダメージ設定の項目を登録する。
    // ----------------------------
    private void AddDamage(IGenericModConfigMenuApi api)
    {
        api.AddSectionTitle(this.manifest, () => this.T("gmcm.section.damage.name"), () => this.T("gmcm.section.damage.desc"));
        api.AddBoolOption(this.manifest, () => this.getConfig().EnableMonsterDamage, value => this.getConfig().EnableMonsterDamage = value, () => this.T("gmcm.enable_monster_damage.name"), () => this.T("gmcm.enable_monster_damage.desc"));
        api.AddNumberOption(this.manifest, () => this.getConfig().MonsterDamageMultiplier, value => this.getConfig().MonsterDamageMultiplier = value, () => this.T("gmcm.monster_multiplier.name"), () => this.T("gmcm.monster_multiplier.desc"), ModConfig.MinMonsterDamageMultiplier, ModConfig.MaxMonsterDamageMultiplier, 0.1f);
    }

    // ----------------------------
    // 翻訳キーから現在の言語で使う表示文言を取得する。
    // ----------------------------
    private string T(string key)
    {
        return this.translation.Get(key).ToString();
    }
}
