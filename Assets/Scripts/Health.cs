using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Generic health component for enemies, props, destructibles, etc.
/// Add this manually to any object that should be damageable.
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private bool destroyOnDeath;
    [SerializeField, Min(0f)] private float destroyDelay = 0f;

    [Header("Events")]
    public UnityEvent<DamageInfo> OnDamaged;
    public UnityEvent<DamageInfo> OnDied;
    public UnityEvent OnHealed;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0f;

    private bool hasDied;

    private void Awake()
    {
        OnDamaged ??= new UnityEvent<DamageInfo>();
        OnDied ??= new UnityEvent<DamageInfo>();
        OnHealed ??= new UnityEvent();
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (!IsAlive || damageInfo.Amount <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damageInfo.Amount);
        OnDamaged?.Invoke(damageInfo);

        if (CurrentHealth <= 0f && !hasDied)
        {
            Die(damageInfo);
        }
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || !IsAlive)
        {
            return;
        }

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealed?.Invoke();
    }

    public void ResetHealth()
    {
        hasDied = false;
        CurrentHealth = maxHealth;
    }

    private void Die(DamageInfo damageInfo)
    {
        hasDied = true;
        OnDied?.Invoke(damageInfo);

        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
    }
}