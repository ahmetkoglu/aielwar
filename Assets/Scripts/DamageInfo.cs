using UnityEngine;

/// <summary>
/// Immutable-ish data payload describing a single damage event.
/// Passed from weapons/explosions/traps to objects implementing IDamageable.
/// </summary>
public readonly struct DamageInfo
{
    public readonly float Amount;
    public readonly GameObject Attacker;
    public readonly GameObject Source;
    public readonly Vector3 HitPoint;
    public readonly Vector3 HitNormal;
    public readonly Vector3 Direction;
    public readonly Collider HitCollider;
    public readonly bool IsCritical;

    public DamageInfo(
        float amount,
        GameObject attacker,
        GameObject source,
        Vector3 hitPoint,
        Vector3 hitNormal,
        Vector3 direction,
        Collider hitCollider,
        bool isCritical = false)
    {
        Amount = amount;
        Attacker = attacker;
        Source = source;
        HitPoint = hitPoint;
        HitNormal = hitNormal;
        Direction = direction;
        HitCollider = hitCollider;
        IsCritical = isCritical;
    }
}