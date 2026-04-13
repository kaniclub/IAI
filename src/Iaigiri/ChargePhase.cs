// ----------------------------
// Iaigiri の進行段階を定義する。
// 右クリック待機中か、発動待ち中かだけを判定する。
// ----------------------------
namespace Iaigiri;

internal enum ChargePhase
{
    Idle,
    PendingStrike,
}
