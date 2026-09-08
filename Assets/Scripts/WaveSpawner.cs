using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TMPro;

/// <summary>
/// Spawns enemies in waves on NavMesh. Each wave's enemy count is configurable from the Inspector.
/// Enemies spawn on NavMesh surfaces with Y <= 2 and away from the player.
/// Shows "Wave X" text at wave start.
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    [System.Serializable]
    public class Wave
    {
        [Header("Wave Settings")]
        public string waveName = "Wave 1";
        [Min(1)] public int enemyCount = 5;
        [Min(0.1f)] public float spawnInterval = 1.5f;
        public GameObject enemyPrefab;

    }

    [Header("Wave List")]
    [SerializeField] private List<Wave> waves = new List<Wave>();
    [SerializeField] private bool loopWaves = false;

    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private string playerTag = "Player";

    [Header("Spawn Settings")]
    [SerializeField] private float spawnZoneMinX = -60f;
    [SerializeField] private float spawnZoneMaxX = 40f;
    [SerializeField] private float spawnZoneMinZ = -90f;
    [SerializeField] private float spawnZoneMaxZ = 20f;
    [SerializeField] private float maxSpawnHeight = 2f;
    [SerializeField] private int maxSpawnAttempts = 30;
    [SerializeField] private LayerMask navMeshArea = -1;

    [Header("Wave Start Text")]
    [SerializeField] private TMP_Text waveTextPrefab;
    [SerializeField] private string wavePrefix = "WAVE ";
    [SerializeField] private Color waveTextColor = new Color(1f, 0.5f, 0f, 1f);
    [SerializeField, Min(0.5f)] private float waveTextDuration = 1.5f;
    [SerializeField] private float waveTextFontSize = 48f;
    [SerializeField] private Vector2 waveTextStartOffset = new Vector2(0f, -50f);
    [SerializeField] private Vector2 waveTextDrift = new Vector2(0f, -30f);

    [Header("Wave Complete Text")]
    [SerializeField] private TMP_Text allWavesCompletePrefab;
    [SerializeField] private string allWavesCompleteMessage = "ALL WAVES COMPLETE!";
    [SerializeField] private Color allWavesCompleteColor = Color.green;
    [SerializeField, Min(0.5f)] private float allWavesCompleteDuration = 2f;

    [Header("Debug")]
    [SerializeField] private bool logSpawns;

    public int CurrentWaveIndex { get; private set; } = -1;
    public int EnemiesRemainingInWave { get; private set; }
    public bool IsSpawning { get; private set; }
    public bool AllWavesComplete { get; private set; }

    private Coroutine waveRoutine;
    private int totalSpawnedInWave;
    private FpsWeaponController[] cachedWeapons;


    private void Awake()
    {
        if (playerTarget == null)
        {
            FindPlayerByTag();
        }

        if (waveTextPrefab != null)
        {
            waveTextPrefab.gameObject.SetActive(false);
        }

        if (allWavesCompletePrefab != null)
        {
            allWavesCompletePrefab.gameObject.SetActive(false);
        }

        // Cache weapons for refill
        cachedWeapons = FindObjectsByType<FpsWeaponController>(FindObjectsSortMode.None);
    }

    private void Start()
    {
        StartNextWave();
    }

    public void StartNextWave()
    {
        if (waveRoutine != null)
        {
            StopCoroutine(waveRoutine);
        }

        waveRoutine = StartCoroutine(WaveRoutine());
    }

    private IEnumerator WaveRoutine()
    {
        // Determine next wave index
        int nextWaveIndex = CurrentWaveIndex + 1;

        // Check if we've completed all waves
        if (nextWaveIndex >= waves.Count)
        {
            if (loopWaves)
            {
                nextWaveIndex = 0;
            }
            else
            {
                AllWavesComplete = true;
                ShowAllWavesComplete();
                yield break;
            }
        }

        CurrentWaveIndex = nextWaveIndex;
        Wave currentWave = waves[CurrentWaveIndex];

        if (currentWave == null || currentWave.enemyPrefab == null)
        {
            Debug.LogError($"{nameof(WaveSpawner)}: Wave {CurrentWaveIndex} has no enemy prefab assigned!", this);
            yield break;
        }

        IsSpawning = true;
        totalSpawnedInWave = 0;
        EnemiesRemainingInWave = currentWave.enemyCount;

        // Show wave start text
        ShowWaveText(CurrentWaveIndex + 1);

        if (logSpawns)
        {
            Debug.Log($"{nameof(WaveSpawner)}: Starting {currentWave.waveName} with {currentWave.enemyCount} enemies.");
        }

        // Spawn enemies one by one
        while (totalSpawnedInWave < currentWave.enemyCount)
        {
            if (playerTarget == null)
            {
                FindPlayerByTag();
            }

            Vector3 spawnPosition = FindValidSpawnPosition(currentWave);
            if (spawnPosition != Vector3.zero)
            {
                GameObject enemy = Instantiate(currentWave.enemyPrefab, spawnPosition, Quaternion.identity);
                totalSpawnedInWave++;

                // Set player target on the enemy's chase AI
                EnemyChaseAttack chase = enemy.GetComponent<EnemyChaseAttack>();
                if (chase != null && playerTarget != null)
                {
                    chase.SetTarget(playerTarget);
                }

                // Link events for score and wave tracking
                EnemyDeathDrops deathDrops = enemy.GetComponent<EnemyDeathDrops>();
                if (deathDrops != null)
                {
                    // Score tracking
                    ScoreManager scoreManager = FindObjectOfType<ScoreManager>();
                    if (scoreManager != null)
                    {
                        deathDrops.OnKilled.RemoveListener(scoreManager.RegisterEnemyKill);
                        deathDrops.OnKilled.AddListener(scoreManager.RegisterEnemyKill);
                    }

                    // Wave completion tracking
                    deathDrops.OnKilled.RemoveListener(OnEnemyKilled);
                    deathDrops.OnKilled.AddListener(OnEnemyKilled);
                }

                if (logSpawns)
                {
                    Debug.Log($"{nameof(WaveSpawner)}: Spawned enemy {totalSpawnedInWave}/{currentWave.enemyCount} at {spawnPosition}");
                }
            }

            yield return new WaitForSeconds(currentWave.spawnInterval);
        }

        IsSpawning = false;

        if (logSpawns)
        {
            Debug.Log($"{nameof(WaveSpawner)}: {currentWave.waveName} spawn complete. Waiting for all enemies to die...");
        }

        // Wait until all enemies in this wave have been killed
        while (EnemiesRemainingInWave > 0)
        {
            yield return null;
        }

        if (logSpawns)
        {
            Debug.Log($"{nameof(WaveSpawner)}: All enemies in {currentWave.waveName} killed!");
        }

        // Refill ammo for all weapons
        RefillAllWeapons();

        // Auto-start next wave after a short delay
        yield return new WaitForSeconds(2f);
        waveRoutine = StartCoroutine(WaveRoutine());
    }

    private Vector3 FindValidSpawnPosition(Wave wave)
    {
        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            // Random point within the fixed spawn zone (X: -60..40, Z: -90..20)
            float randomX = Random.Range(spawnZoneMinX, spawnZoneMaxX);
            float randomZ = Random.Range(spawnZoneMinZ, spawnZoneMaxZ);
            Vector3 randomPoint = new Vector3(randomX, 0f, randomZ);

            // Sample on NavMesh
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 5f, navMeshArea))
            {
                // Check Y <= maxSpawnHeight
                if (hit.position.y <= maxSpawnHeight)
                {
                    return hit.position;
                }
            }
        }

        // Fallback: try with larger sample radius
        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            float randomX = Random.Range(spawnZoneMinX, spawnZoneMaxX);
            float randomZ = Random.Range(spawnZoneMinZ, spawnZoneMaxZ);
            Vector3 randomPoint = new Vector3(randomX, 0f, randomZ);

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 15f, navMeshArea))
            {
                if (hit.position.y <= maxSpawnHeight)
                {
                    return hit.position;
                }
            }
        }

        return Vector3.zero;
    }

    private void ShowWaveText(int waveNumber)
    {
        if (waveTextPrefab == null)
        {
            return;
        }

        TMP_Text text = Instantiate(waveTextPrefab, waveTextPrefab.transform.parent);
        text.gameObject.SetActive(true);
        text.text = $"{wavePrefix}{waveNumber}";
        text.color = waveTextColor;
        text.fontSize = waveTextFontSize;
        text.rectTransform.anchoredPosition = waveTextStartOffset;
        text.rectTransform.localScale = Vector3.one * 1.5f;

        StartCoroutine(WaveTextAnimation(text));
    }

    private IEnumerator WaveTextAnimation(TMP_Text text)
    {
        Vector2 start = waveTextStartOffset;
        Vector2 end = start + waveTextDrift;
        float elapsed = 0f;

        while (elapsed < waveTextDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / waveTextDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            text.rectTransform.anchoredPosition = Vector2.Lerp(start, end, eased);
            text.rectTransform.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one, t * 2f);

            Color faded = waveTextColor;
            faded.a = Mathf.Lerp(1f, 0f, t);
            text.color = faded;

            yield return null;
        }

        Destroy(text.gameObject);
    }

    private void ShowAllWavesComplete()
    {
        if (allWavesCompletePrefab == null)
        {
            return;
        }

        TMP_Text text = Instantiate(allWavesCompletePrefab, allWavesCompletePrefab.transform.parent);
        text.gameObject.SetActive(true);
        text.text = allWavesCompleteMessage;
        text.color = allWavesCompleteColor;
        text.rectTransform.anchoredPosition = Vector2.zero;
        text.rectTransform.localScale = Vector3.one * 2f;

        StartCoroutine(AllWavesCompleteAnimation(text));
    }

    private IEnumerator AllWavesCompleteAnimation(TMP_Text text)
    {
        float elapsed = 0f;
        while (elapsed < allWavesCompleteDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / allWavesCompleteDuration);

            text.rectTransform.localScale = Vector3.Lerp(Vector3.one * 2f, Vector3.one, t);

            Color faded = allWavesCompleteColor;
            faded.a = Mathf.Lerp(1f, 0f, t);
            text.color = faded;

            yield return null;
        }

        Destroy(text.gameObject);
    }

    private void FindPlayerByTag()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
        {
            playerTarget = player.transform;
        }
    }

    /// <summary>
    /// Call this when an enemy dies to track remaining enemies.
    /// </summary>
    public void OnEnemyKilled()
    {
        EnemiesRemainingInWave = Mathf.Max(0, EnemiesRemainingInWave - 1);
    }

    /// <summary>
    /// Refills ammo to full for all FpsWeaponControllers in the scene.
    /// </summary>
    private void RefillAllWeapons()
    {
        if (cachedWeapons == null || cachedWeapons.Length == 0)
        {
            cachedWeapons = FindObjectsByType<FpsWeaponController>(FindObjectsSortMode.None);
        }

        for (int i = 0; i < cachedWeapons.Length; i++)
        {
            if (cachedWeapons[i] != null)
            {
                cachedWeapons[i].RefillAmmo();
                
                if (logSpawns)
                {
                    Debug.Log($"Refilled ammo for {cachedWeapons[i].name}");
                }
            }
        }
    }
}