// ----------------------------
// Iaigiri 実行中の状態を保持する。
// ----------------------------
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Iaigiri;

internal sealed class IaigiriState
{
    public ChargePhase Phase { get; set; } = ChargePhase.Idle;

    public int ReleasedRadiusTiles { get; set; }

    public int StrikeFacingDirection { get; set; } = 2;

    public Vector2 StrikeCenterPixels { get; set; }

    public float EffectElapsedMilliseconds { get; set; }

    public float StrikeElapsedMilliseconds { get; set; }

    public bool StrikeApplied { get; set; }

    public StrikeSnapshot? StrikeSnapshot { get; set; }

    public Texture2D? HeldScytheTexture { get; set; }

    public Rectangle HeldScytheSourceRect { get; set; }

    public float CooldownRemainingMilliseconds { get; set; }

    // ----------------------------
    // 発動中の状態を初期化する。
    // ----------------------------
    public void ResetStrike()
    {
        this.Phase = ChargePhase.Idle;
        this.ReleasedRadiusTiles = 0;
        this.StrikeFacingDirection = 2;
        this.StrikeCenterPixels = Vector2.Zero;
        this.EffectElapsedMilliseconds = 0f;
        this.StrikeElapsedMilliseconds = 0f;
        this.StrikeApplied = false;
        this.StrikeSnapshot = null;
        this.HeldScytheTexture = null;
        this.HeldScytheSourceRect = Rectangle.Empty;
    }

    // ----------------------------
    // 内部状態をすべて初期化する。
    // ----------------------------
    public void ResetAll()
    {
        this.CooldownRemainingMilliseconds = 0f;
        this.ResetStrike();
    }
}
