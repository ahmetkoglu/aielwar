using UnityEngine;

/// <summary>
/// Optional hitbox helper for body-part damage multipliers.
/// Put this on child colliders such as Head, Body, Arm, Leg.
/// The damageableRoot should point to the object with Health/IDamageable.
/// </summary>
public class DamageHitbox : MonoBehaviour
{
    [SerializeField] private MonoBehaviour damageableRoot;
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;
    [SerializeField] private bool criticalHitbox;

    public float DamageMultiplier => damageMultiplier;
    public bool CriticalHitbox => criticalHitbox;

    public bool TryGetDamageable(out IDamageable damageable)
    {
        damageable = null;

        if (damageableRoot != null && damageableRoot is IDamageable assignedDamageable)
        {
            damageable = assignedDamageable;
            return true;
        }

        damageable = FindDamageableInParents(transform);
        return damageable != null;
    }

    private static IDamageable FindDamageableInParents(Transform startTransform)
    {
        Transform current = startTransform;

        while (current != null)
        {
            MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IDamageable foundDamageable)
                {
                    return foundDamageable;
                }
            }

            current = current.parent;
        }

        return null;
    }
}