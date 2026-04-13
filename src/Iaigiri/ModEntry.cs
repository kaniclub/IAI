// ----------------------------
// SMAPI から呼ばれる Iaigiri の起点処理をまとめる。
// ----------------------------
using System.Diagnostics.CodeAnalysis;
using Iaigiri.Integrations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace Iaigiri;

public sealed class ModEntry : Mod
{
    private const float UpFacingScytheAngleDegrees = -65f;
    private const float RightFacingScytheAngleDegrees = -45f;
    private const float DownFacingScytheAngleDegrees = 170f;
    private const float LeftFacingScytheAngleDegrees = 20f;
    private const float ReleaseAnimationDurationMilliseconds = 480f;
    private const int SweepEffectSegmentCount = 56;
    private const float SweepEffectThickness = 8f;
    private const string DrawSoundPath = "assets/audio/battou.ogg";
    private const string StrikeSoundPath = "assets/audio/zangeki4.ogg";

    private readonly IaigiriState state = new();
    private ModConfig config = new();
    private OggSoundPlayer soundPlayer = null!;
    private ScytheStrikeResolver strikeResolver = null!;
    private GmcmIntegration gmcmIntegration = null!;
    private bool updateTickedHooked;
    private bool renderedWorldHooked;
    private bool renderedHudHooked;

    // ----------------------------
    // MOD 読み込み時の初期化を行う。
    // ----------------------------
    public override void Entry(IModHelper helper)
    {
        this.config = helper.ReadConfig<ModConfig>();
        this.config.Normalize();
        this.soundPlayer = new OggSoundPlayer(this.Monitor, this.Helper.DirectoryPath);
        this.strikeResolver = new ScytheStrikeResolver(this.config);
        this.gmcmIntegration = new GmcmIntegration(helper, this.ModManifest, helper.Translation, () => this.config, this.ResetConfig, this.SaveConfig);

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        helper.Events.Player.Warped += this.OnWarped;
    }

    // ----------------------------
    // 起動直後に設定値を保存し、GMCM を登録する。
    // ----------------------------
    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.SaveConfig();
        this.gmcmIntegration.RegisterOrReload();
    }

    // ----------------------------
    // 右クリック時は先にバニラの右クリック処理を通し、
    // 余った入力だけ Iaigiri へ使う。
    // ----------------------------
    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!this.CanHandleIaigiriInput(e.Button, out Farmer? player))
            return;

        if (!this.TryGetEligibleWeapon(player, out MeleeWeapon? weapon))
            return;

        bool vanillaConsumed = this.TryConsumeVanillaRightClickAction(player, weapon);
        this.Helper.Input.Suppress(e.Button);
        if (vanillaConsumed)
            return;

        if (!IsRuntimeAllowed(player) || this.state.Phase != ChargePhase.Idle || this.state.CooldownRemainingMilliseconds > 0f)
            return;

        int radiusTiles = this.GetRadiusTiles(weapon);
        if (radiusTiles <= 0)
            return;

        this.StartIaigiri(player, weapon, radiusTiles);
    }

    // ----------------------------
    // 毎 tick ごとの状態遷移とクールタイム減算を処理する。
    // ----------------------------
    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.player is null)
        {
            this.UpdateRuntimeHooks();
            return;
        }

        float elapsedMs = (float)Game1.currentGameTime.ElapsedGameTime.TotalMilliseconds;
        if (this.state.CooldownRemainingMilliseconds > 0f)
            this.state.CooldownRemainingMilliseconds = Math.Max(0f, this.state.CooldownRemainingMilliseconds - elapsedMs);

        if (this.state.Phase == ChargePhase.PendingStrike)
        {
            Farmer player = Game1.player;
            if (!this.config.ModEnabled || !IsRuntimeAllowed(player))
                this.CancelStrike(player);
            else
                this.UpdatePendingStrike(player, elapsedMs);
        }

        this.UpdateRuntimeHooks();
    }

    // ----------------------------
    // タイトルへ戻ったときに内部状態を破棄する。
    // ----------------------------
    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        this.state.ResetAll();
        this.UpdateRuntimeHooks();
    }

    // ----------------------------
    // ワープ時に不整合を避けるため発動中だけ解除する。
    // ----------------------------
    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (!e.IsLocalPlayer)
            return;

        this.CancelStrike(e.Player);
        this.UpdateRuntimeHooks();
    }

    // ----------------------------
    // 白い円弧エフェクトと、居合中の武器表示を描画する。
    // ----------------------------
    private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        if (!Context.IsWorldReady || this.state.Phase != ChargePhase.PendingStrike)
            return;

        float effectDuration = Math.Max(1f, this.config.EffectDurationMilliseconds);
        float progress = Math.Clamp(this.state.EffectElapsedMilliseconds / effectDuration, 0f, 1f);
        float alpha = 1f - progress;
        float radiusPixels = this.state.ReleasedRadiusTiles * 64f;
        float startAngle = -MathF.PI / 4f;
        float endAngle = startAngle + MathHelper.TwoPi * progress;
        Color color = Color.White * alpha;

        for (int i = 0; i < SweepEffectSegmentCount; i++)
        {
            float t0 = i / (float)SweepEffectSegmentCount;
            float t1 = (i + 1) / (float)SweepEffectSegmentCount;
            float a0 = startAngle + (endAngle - startAngle) * t0;
            float a1 = startAngle + (endAngle - startAngle) * t1;
            if (a1 <= a0)
                continue;

            Vector2 p0 = this.state.StrikeCenterPixels + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radiusPixels;
            Vector2 p1 = this.state.StrikeCenterPixels + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radiusPixels;
            this.DrawLine(p0, p1, color, SweepEffectThickness);
        }

        if (Game1.player is not null && this.state.StrikeSnapshot is not null)
            this.DrawHeldScythe(Game1.player, this.state.StrikeSnapshot.Scythe);
    }

    // ----------------------------
    // 武器の特殊攻撃と同じ見た目で、
    // ツールバーのアイコンへクールタイムを重ね描きする。
    // ----------------------------
    private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.activeClickableMenu is not null || this.state.CooldownRemainingMilliseconds <= 0f || Game1.player is null)
            return;

        if (!this.TryGetEligibleWeapon(Game1.player, out _))
            return;

        Toolbar? toolbar = this.TryGetToolbar();
        if (toolbar is null)
            return;

        int index = Game1.player.CurrentToolIndex;
        if (index < 0 || index >= toolbar.buttons.Count)
            return;

        float cooldownLevel = Math.Clamp(this.state.CooldownRemainingMilliseconds / Math.Max(1f, this.config.CooldownMilliseconds), 0f, 1f);
        if (cooldownLevel <= 0f)
            return;

        Rectangle bounds = toolbar.buttons[index].bounds;
        Rectangle overlay = new(bounds.X, bounds.Y + (64 - (int)(cooldownLevel * 64f)), 64, (int)(cooldownLevel * 64f));
        Game1.spriteBatch.Draw(Game1.staminaRect, overlay, Color.Red * 0.66f);
    }

    // ----------------------------
    // 入力・状況・ UI 状態を見て、Iaigiri 入力を処理してよいか判定する。
    // ----------------------------
    private bool CanHandleIaigiriInput(SButton button, [NotNullWhen(true)] out Farmer? player)
    {
        player = Game1.player;
        return Context.IsWorldReady
            && player is not null
            && this.config.ModEnabled
            && button == SButton.MouseRight
            && Game1.activeClickableMenu is null;
    }

    // ----------------------------
    // 武器特性以外の右クリック行動だけを、バニラ本体にそのまま処理させる。
    // ----------------------------
    private bool TryConsumeVanillaRightClickAction(Farmer player, MeleeWeapon scythe)
    {
        if (player.UsingTool || Game1.fadeToBlack)
            return true;

        Tool? originalTool = player.CurrentTool;
        try
        {
            player.CurrentTool = null;
            return !Game1.pressActionButton(Game1.GetKeyboardState(), Game1.input.GetMouseState(), Game1.input.GetGamePadState());
        }
        finally
        {
            player.CurrentTool = originalTool ?? scythe;
        }
    }

    // ----------------------------
    // 右クリック時点の範囲と対象を固定して居合を開始する。
    // ----------------------------
    private void StartIaigiri(Farmer player, MeleeWeapon scythe, int radiusTiles)
    {
        this.state.ResetStrike();
        this.state.Phase = ChargePhase.PendingStrike;
        this.state.ReleasedRadiusTiles = radiusTiles;
        this.state.StrikeFacingDirection = player.FacingDirection;
        this.state.StrikeCenterPixels = Utility.PointToVector2(player.StandingPixel);
        this.state.StrikeSnapshot = this.strikeResolver.Capture(player, scythe, this.state.StrikeCenterPixels, radiusTiles);
        this.state.HeldScytheTexture = this.state.StrikeSnapshot.DrawData.Texture;
        this.state.HeldScytheSourceRect = this.state.StrikeSnapshot.DrawData.SourceRect;
        this.state.CooldownRemainingMilliseconds = this.config.CooldownMilliseconds;

        this.ApplyStrikePose(player, scythe);
        this.LockPlayerDuringPendingStrike(player);
        this.UpdateRuntimeHooks();
        this.PlayDrawSound();

        if (this.config.StrikeDelayMilliseconds <= 0)
            this.ApplyStrikeIfNeeded();
    }

    // ----------------------------
    // 抜刀演出のあとで固定済みの効果を適用する。
    // ----------------------------
    private void UpdatePendingStrike(Farmer player, float elapsedMs)
    {
        if (this.state.StrikeSnapshot is null)
        {
            this.CancelStrike(player);
            return;
        }

        this.state.EffectElapsedMilliseconds += elapsedMs;
        this.state.StrikeElapsedMilliseconds += elapsedMs;
        this.ApplyStrikePose(player, this.state.StrikeSnapshot.Scythe);
        this.LockPlayerDuringPendingStrike(player);

        if (this.state.StrikeElapsedMilliseconds >= this.config.StrikeDelayMilliseconds)
            this.ApplyStrikeIfNeeded();

        float finishAfterMilliseconds = Math.Max(this.config.StrikeDelayMilliseconds, ReleaseAnimationDurationMilliseconds);
        if (this.state.StrikeElapsedMilliseconds >= finishAfterMilliseconds)
            this.FinishStrike(player);
    }

    // ----------------------------
    // 発動待ち中のプレイヤー姿勢を固定する。
    // ----------------------------
    private void ApplyStrikePose(Farmer player, MeleeWeapon scythe)
    {
        player.Halt();
        player.running = false;
        player.CanMove = false;
        player.UsingTool = false;
        player.canReleaseTool = false;
        player.stopJittering();
        player.completelyStopAnimatingOrDoingAction();
        player.faceDirection(this.state.StrikeFacingDirection);
        player.FacingDirection = this.state.StrikeFacingDirection;
        player.FarmerSprite.PauseForSingleAnimation = true;
        player.FarmerSprite.StopAnimation();
        player.FarmerSprite.CurrentToolIndex = this.GetScytheToolSpriteIndex(scythe);

        switch (this.state.StrikeFacingDirection)
        {
            case 0:
                player.FarmerSprite.setCurrentFrame(252);
                break;
            case 1:
                player.FarmerSprite.setCurrentFrame(243);
                break;
            case 2:
                player.FarmerSprite.setCurrentFrame(234);
                break;
            default:
                player.FarmerSprite.setCurrentFrame(259);
                break;
        }
    }

    // ----------------------------
    // 発動待ち中の移動入力と慣性を完全に止める。
    // ----------------------------
    private void LockPlayerDuringPendingStrike(Farmer player)
    {
        float finishAfterMilliseconds = Math.Max(this.config.StrikeDelayMilliseconds, ReleaseAnimationDurationMilliseconds);
        int remainingMilliseconds = Math.Max(1, (int)MathF.Ceiling(finishAfterMilliseconds - this.state.StrikeElapsedMilliseconds));

        player.CanMove = false;
        player.freezePause = Math.Max(player.freezePause, remainingMilliseconds);
        player.Halt();
        player.movementDirections.Clear();
        player.xVelocity = 0f;
        player.yVelocity = 0f;
    }

    // ----------------------------
    // ディレイ後の本効果をまだ未適用なら一度だけ実行する。
    // ----------------------------
    private void ApplyStrikeIfNeeded()
    {
        if (this.state.StrikeApplied || this.state.StrikeSnapshot is null)
            return;

        this.state.StrikeApplied = true;
        this.PlayStrikeSound();
        this.strikeResolver.Apply(this.state.StrikeSnapshot);
    }

    // ----------------------------
    // 発動後の後始末を行う。
    // ----------------------------
    private void FinishStrike(Farmer player)
    {
        this.ResetPlayerState(player);
        this.state.ResetStrike();
        this.UpdateRuntimeHooks();
    }

    // ----------------------------
    // 不正状態になったときに安全に解除する。
    // ----------------------------
    private void CancelStrike(Farmer player)
    {
        if (this.state.Phase == ChargePhase.Idle)
            return;

        this.ResetPlayerState(player);
        this.state.ResetStrike();
        this.UpdateRuntimeHooks();
    }

    // ----------------------------
    // 発動終了後にプレイヤーの操作状態を元へ戻す。
    // ----------------------------
    private void ResetPlayerState(Farmer player)
    {
        player.stopJittering();
        player.UsingTool = false;
        player.canReleaseTool = false;
        player.freezePause = 0;
        player.xVelocity = 0f;
        player.yVelocity = 0f;
        player.movementDirections.Clear();
        player.completelyStopAnimatingOrDoingAction();
        player.forceCanMove();
    }

    // ----------------------------
    // 設定に従って抜刀 SE を再生する。
    // ----------------------------
    private void PlayDrawSound()
    {
        if (!this.config.EnableSoundEffects)
            return;

        this.soundPlayer.Play(DrawSoundPath, this.GetDrawSoundTargetDurationMilliseconds(), this.GetSoundVolume());
    }

    // ----------------------------
    // 設定に従って斬撃 SE を再生する。
    // ----------------------------
    private void PlayStrikeSound()
    {
        if (!this.config.EnableSoundEffects)
            return;

        this.soundPlayer.Play(StrikeSoundPath, this.GetSoundVolume());
    }

    // ----------------------------
    // 設定から 0 から 1 の音量へ変換する。
    // ----------------------------
    private float GetSoundVolume()
    {
        return Math.Clamp(this.config.SoundVolumePercent / 100f, 0f, 1f);
    }

    // ----------------------------
    // 抜刀 SE を効果発動ディレイ時間で鳴り終わる長さへ合わせる。
    // ----------------------------
    private int? GetDrawSoundTargetDurationMilliseconds()
    {
        return this.config.StrikeDelayMilliseconds > 0 ? this.config.StrikeDelayMilliseconds : null;
    }

    // ----------------------------
    // 現在選択中の Iaigiri 対象武器を取得する。
    // カマは常に対象にし、設定有効時だけ剣も対象にする。
    // ----------------------------
    private bool TryGetEligibleWeapon(Farmer player, [NotNullWhen(true)] out MeleeWeapon? weapon)
    {
        weapon = player.CurrentTool as MeleeWeapon;
        if (weapon is null)
            return false;

        return weapon.isScythe() || (this.config.ReplaceSwordsWithIaigiri && this.IsEligibleSword(weapon));
    }

    // ----------------------------
    // 現在の武器に対応する居合半径を返す。
    // 剣は剣用半径、カマは種類ごとの半径を使う。
    // ----------------------------
    private int GetRadiusTiles(MeleeWeapon weapon)
    {
        if (!weapon.isScythe())
            return this.config.SwordRadiusTiles;

        return weapon.ItemId switch
        {
            "53" => this.config.GoldenScytheRadiusTiles,
            "66" => this.config.IridiumScytheRadiusTiles,
            _ => this.config.StandardScytheRadiusTiles,
        };
    }

    // ----------------------------
    // Iaigiri へ置き換える対象の剣かどうかを判定する。
    // ----------------------------
    private bool IsEligibleSword(MeleeWeapon weapon)
    {
        return weapon.type.Value == 3 && !weapon.isScythe();
    }

    // ----------------------------
    // 現在の武器用スプライト番号を返す。
    // ----------------------------
    private int GetScytheToolSpriteIndex(MeleeWeapon scythe)
    {
        if (scythe.CurrentParentTileIndex > 0)
            return scythe.CurrentParentTileIndex;

        if (scythe.IndexOfMenuItemView > 0)
            return scythe.IndexOfMenuItemView;

        return scythe.InitialParentTileIndex;
    }

    // ----------------------------
    // 現在の実行状況で Iaigiri を継続してよいか判定する。
    // ----------------------------
    private static bool IsRuntimeAllowed(Farmer player)
    {
        return player.IsLocalPlayer
            && Game1.activeClickableMenu is null
            && Game1.currentMinigame is null
            && Game1.CurrentEvent is null
            && !Game1.dialogueUp
            && player.currentLocation is not null;
    }

    // ----------------------------
    // 居合ポーズ中の手元に、現在装備している武器を方向ごとの固定角度で重ねる。
    // ----------------------------
    private void DrawHeldScythe(Farmer player, MeleeWeapon scythe)
    {
        Texture2D? texture = this.state.HeldScytheTexture;
        if (texture is null)
            return;

        Rectangle sourceRect = this.state.HeldScytheSourceRect;
        Vector2 playerPosition = player.getLocalPosition(Game1.viewport) + player.jitter + player.armOffset;

        float baseSortLayer = player.getDrawLayer();
        FarmerRenderer.FarmerSpriteLayers weaponLayer = player.FacingDirection switch
        {
            0 => FarmerRenderer.FarmerSpriteLayers.ToolUp,
            2 => FarmerRenderer.FarmerSpriteLayers.ToolDown,
            _ => FarmerRenderer.FarmerSpriteLayers.TOOL_IN_USE_SIDE,
        };
        float sortLayer = FarmerRenderer.GetLayerDepth(baseSortLayer, weaponLayer);

        Vector2 drawPosition;
        float rotation;
        Vector2 origin;
        SpriteEffects effects;

        switch (this.state.StrikeFacingDirection)
        {
            case 0:
                drawPosition = new Vector2(playerPosition.X + 56f, playerPosition.Y - 44f);
                rotation = ToRadians(UpFacingScytheAngleDegrees);
                origin = new Vector2(1f, 15f);
                effects = SpriteEffects.None;
                break;
            case 1:
                drawPosition = new Vector2(playerPosition.X + 56f, playerPosition.Y - 4f);
                rotation = ToRadians(RightFacingScytheAngleDegrees);
                origin = new Vector2(1f, 15f);
                effects = SpriteEffects.None;
                break;
            case 2:
                drawPosition = new Vector2(playerPosition.X + 12f, playerPosition.Y + 4f);
                rotation = ToRadians(DownFacingScytheAngleDegrees);
                origin = new Vector2(1f, 15f);
                effects = SpriteEffects.None;
                break;
            default:
                drawPosition = new Vector2(playerPosition.X + 8f, playerPosition.Y - 4f);
                rotation = ToRadians(LeftFacingScytheAngleDegrees);
                origin = new Vector2(15f, 15f);
                effects = SpriteEffects.FlipHorizontally;
                break;
        }

        Game1.spriteBatch.Draw(texture, drawPosition, sourceRect, Color.White, rotation, origin, 4f, effects, sortLayer);
    }

    // ----------------------------
    // 白線 1 本を描画する。
    // ----------------------------
    private void DrawLine(Vector2 worldStart, Vector2 worldEnd, Color color, float thickness)
    {
        Vector2 localStart = Game1.GlobalToLocal(worldStart);
        Vector2 localEnd = Game1.GlobalToLocal(worldEnd);
        Vector2 edge = localEnd - localStart;
        float length = edge.Length();
        if (length <= 0.001f)
            return;

        Game1.spriteBatch.Draw(
            Game1.staminaRect,
            localStart,
            null,
            color,
            MathF.Atan2(edge.Y, edge.X),
            Vector2.Zero,
            new Vector2(length, thickness),
            SpriteEffects.None,
            1f);
    }


    // ----------------------------
    // 表示中のツールバーを余計な列挙なしで取得する。
    // ----------------------------
    private Toolbar? TryGetToolbar()
    {
        for (int i = 0; i < Game1.onScreenMenus.Count; i++)
        {
            if (Game1.onScreenMenus[i] is Toolbar toolbar)
                return toolbar;
        }

        return null;
    }

    // ----------------------------
    // 実行中の状態に合わせて重いイベントだけを購読する。
    // ----------------------------
    private void UpdateRuntimeHooks()
    {
        bool needTick = Context.IsWorldReady && (this.state.Phase == ChargePhase.PendingStrike || this.state.CooldownRemainingMilliseconds > 0f);
        bool needWorldRender = Context.IsWorldReady && this.state.Phase == ChargePhase.PendingStrike;
        bool needHudRender = Context.IsWorldReady && this.state.CooldownRemainingMilliseconds > 0f;

        this.SetUpdateTickedHook(needTick);
        this.SetRenderedWorldHook(needWorldRender);
        this.SetRenderedHudHook(needHudRender);
    }

    // ----------------------------
    // UpdateTicked の購読状態を切り替える。
    // ----------------------------
    private void SetUpdateTickedHook(bool enabled)
    {
        if (enabled == this.updateTickedHooked)
            return;

        if (enabled)
            this.Helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        else
            this.Helper.Events.GameLoop.UpdateTicked -= this.OnUpdateTicked;

        this.updateTickedHooked = enabled;
    }

    // ----------------------------
    // RenderedWorld の購読状態を切り替える。
    // ----------------------------
    private void SetRenderedWorldHook(bool enabled)
    {
        if (enabled == this.renderedWorldHooked)
            return;

        if (enabled)
            this.Helper.Events.Display.RenderedWorld += this.OnRenderedWorld;
        else
            this.Helper.Events.Display.RenderedWorld -= this.OnRenderedWorld;

        this.renderedWorldHooked = enabled;
    }

    // ----------------------------
    // RenderedHud の購読状態を切り替える。
    // ----------------------------
    private void SetRenderedHudHook(bool enabled)
    {
        if (enabled == this.renderedHudHooked)
            return;

        if (enabled)
            this.Helper.Events.Display.RenderedHud += this.OnRenderedHud;
        else
            this.Helper.Events.Display.RenderedHud -= this.OnRenderedHud;

        this.renderedHudHooked = enabled;
    }

    // ----------------------------
    // 度数法をラジアンへ変換する。
    // ----------------------------
    private static float ToRadians(float degrees)
    {
        return MathF.PI / 180f * degrees;
    }

    // ----------------------------
    // 現在設定を保存し、変更内容を関連サービスへ反映する。
    // ----------------------------
    private void SaveConfig()
    {
        this.config.Normalize();
        this.Helper.WriteConfig(this.config);
        this.strikeResolver = new ScytheStrikeResolver(this.config);
        this.UpdateRuntimeHooks();
    }

    // ----------------------------
    // config を既定値へ戻す。
    // ----------------------------
    private void ResetConfig()
    {
        this.config = new ModConfig();
        this.config.Normalize();
        this.strikeResolver = new ScytheStrikeResolver(this.config);
        this.UpdateRuntimeHooks();
    }
}
