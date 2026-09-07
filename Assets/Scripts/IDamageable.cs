/// <summary>
/// Implement this on anything that can receive damage.
/// </summary>
public interface IDamageable
{
    bool IsAlive { get; }
    void TakeDamage(DamageInfo damageInfo);
}