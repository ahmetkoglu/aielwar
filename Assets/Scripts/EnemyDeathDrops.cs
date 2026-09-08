using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Spawns XP orbs and optional loot prefabs when the attached Health dies.
/// Add manually to an enemy root with Health.
/// </summary>
[RequireComponent(typeof(Health))]
public class EnemyDeathDrops : MonoBehaviour
{
    [Header("Events")]
    [SerializeField] private bool notifyScoreManagerOnDeath = true;
    public UnityEvent OnKilled;

    [Header("XP Orb")]
    [SerializeField] private GameObject xpOrbPrefab;
    [SerializeField, Min(0)] private int xpAmount = 10;
    [SerializeField] private Vector3 xpSpawnOffset = new Vector3(0f, 0.35f, 0f);

    [Header("Loot Drops")]
    [SerializeField] private GameObject[] dropPrefabs;
    [SerializeField, Range(0f, 1f)] private float dropChance = 0.25f;
    [SerializeField] private Vector3 dropSpawnOffset = new Vector3(0f, 0.25f, 0f);
    [SerializeField, Min(0f)] private float randomDropRadius = 0.75f;

    [Header("Debug")]
    [SerializeField] private bool logDrops;

    private Health health;
    private bool hasDropped;

    private void Awake()
    {
        health = GetComponent<Health>();
        health.OnDied.AddListener(HandleDeath);
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied.RemoveListener(HandleDeath);
        }
    }

    private void HandleDeath(DamageInfo damageInfo)
    {
        if (hasDropped)
        {
            return;
        }

        hasDropped = true;
        SpawnXpOrb();
        TrySpawnLootDrop();

        if (notifyScoreManagerOnDeath)
        {
            OnKilled?.Invoke();
        }
    }

    private void SpawnXpOrb()
    {
        if (xpOrbPrefab == null || xpAmount <= 0)
        {
            return;
        }

        GameObject spawnedOrb = Instantiate(xpOrbPrefab, transform.position + xpSpawnOffset, Quaternion.identity);
        ExperienceOrb orb = spawnedOrb.GetComponent<ExperienceOrb>();
        if (orb != null)
        {
            orb.SetXpAmount(xpAmount);
        }

        if (logDrops)
        {
            Debug.Log($"{name} dropped XP orb worth {xpAmount}.");
        }
    }

    private void TrySpawnLootDrop()
    {
        if (dropPrefabs == null || dropPrefabs.Length == 0 || Random.value > dropChance)
        {
            return;
        }

        GameObject prefab = dropPrefabs[Random.Range(0, dropPrefabs.Length)];
        if (prefab == null)
        {
            return;
        }

        Vector2 randomCircle = Random.insideUnitCircle * randomDropRadius;
        Vector3 position = transform.position + dropSpawnOffset + new Vector3(randomCircle.x, 0f, randomCircle.y);
        Instantiate(prefab, position, Quaternion.identity);

        if (logDrops)
        {
            Debug.Log($"{name} dropped loot prefab {prefab.name}.");
        }
    }
}