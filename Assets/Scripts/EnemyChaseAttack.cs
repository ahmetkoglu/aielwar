using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// FPS-style enemy AI: patrols, detects the player, keeps combat distance,
/// strafes, investigates last seen position, and damages with raycast shooting.
/// Add manually to an enemy root with Health. NavMeshAgent is optional but recommended.
/// </summary>
[RequireComponent(typeof(Health))]
public class EnemyChaseAttack : MonoBehaviour
{
    private enum EnemyState
    {
        Patrol,
        Investigate,
        Combat,
        Dead
    }

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private bool findTargetByTagOnStart = true;

    [Header("Patrol")]
    [Tooltip("Optional patrol points. If empty, enemy patrols forward/backward from spawn position.")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField, Min(0f)] private float autoPatrolForwardDistance = 8f;
    [SerializeField, Min(0f)] private float autoPatrolBackwardDistance = 8f;
    [SerializeField, Min(0.1f)] private float patrolPointReachDistance = 0.75f;
    [SerializeField, Min(0f)] private float waitAtPatrolPoint = 0.75f;

    [Header("Detection")]
    [SerializeField, Min(0f)] private float detectionRadius = 80f;
    [SerializeField, Range(1f, 360f)] private float fieldOfViewAngle = 360f;
    [SerializeField, Min(0f)] private float loseTargetDelay = 4f;
    [SerializeField] private LayerMask lineOfSightLayers = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 1.55f, 0f);
    [SerializeField] private Vector3 targetAimOffset = new Vector3(0f, 1.25f, 0f);

    [Header("Movement")]
    [SerializeField] private bool useNavMeshAgentIfAvailable = true;
    [SerializeField, Min(0f)] private float patrolSpeed = 2.2f;
    [SerializeField, Min(0f)] private float combatMoveSpeed = 3.8f;
    [SerializeField, Min(0f)] private float rotationSpeed = 12f;
    [SerializeField, Min(0f)] private float preferredCombatDistance = 12f;
    [SerializeField, Min(0f)] private float tooCloseDistance = 5f;
    [SerializeField, Min(0f)] private float stoppingDistance = 1.2f;
    [SerializeField, Min(0f)] private float strafeDistance = 4f;
    [SerializeField, Min(0.1f)] private float strafeChangeInterval = 1.5f;

    [Header("Raycast Weapon")]
    [SerializeField] private Transform firePoint;
    [SerializeField, Min(0f)] private float attackDamage = 8f;
    [SerializeField, Min(0.01f)] private float fireRate = 2f;
    [SerializeField, Min(0.1f)] private float weaponRange = 40f;
    [SerializeField, Min(0f)] private float spreadAngle = 2.5f;
    [SerializeField] private LayerMask hitLayers = ~0;

    [Header("Enemy Fire FX")]
    [SerializeField] private GameObject muzzleFxPrefab;
    [SerializeField] private GameObject damageableImpactFx;
    [SerializeField] private GameObject worldImpactFx;
    [SerializeField, Min(0.01f)] private float fxLifetime = 2f;
    [SerializeField] private MonoBehaviour cubeVisualFx;

    [Header("Enemy Fire Audio")]
    [SerializeField] private AudioSource fireAudioSource;
    [SerializeField] private AudioClip fireClip;
    [SerializeField, Range(0f, 1f)] private float fireVolume = 0.75f;
    [SerializeField, Min(0f)] private float firePitchRandomness = 0.04f;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;
    [SerializeField] private bool logShots;
    [SerializeField] private bool drawGizmos = true;

    public Transform Target => target;
    public bool CanSeeTarget { get; private set; }

    private Health health;
    private NavMeshAgent agent;
    private EnemyState currentState;
    private Vector3 spawnPosition;
    private Vector3 spawnForward;
    private Vector3 lastSeenTargetPosition;
    private Vector3 currentDestination;
    private int currentPatrolIndex;
    private int autoPatrolIndex;
    private int strafeDirection = 1;
    private float nextShotTime;
    private float lastTimeSawTarget;
    private float nextStrafeChangeTime;
    private float waitUntilTime;

    private void Awake()
    {
        health = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
        spawnPosition = transform.position;
        spawnForward = transform.forward;

        if (agent != null)
        {
            agent.stoppingDistance = stoppingDistance;
            agent.speed = patrolSpeed;
            agent.acceleration = 25f;
            agent.angularSpeed = 720f;
        }

        SetupFireAudioSourceIfNeeded();

        ChangeState(EnemyState.Patrol);
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDamaged.AddListener(OnDamagedByTarget);
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDamaged.RemoveListener(OnDamagedByTarget);
        }
    }

    private void OnDamagedByTarget(DamageInfo damageInfo)
    {
        if (currentState == EnemyState.Dead || target == null)
        {
            return;
        }

        // Immediately enter combat state when damaged
        // Set the attacker as the target if we don't have one
        if (target == null && damageInfo.Attacker != null)
        {
            target = damageInfo.Attacker.transform;
        }

        lastSeenTargetPosition = target.position;
        lastTimeSawTarget = Time.time;
        CanSeeTarget = HasLineOfSight(GetEyePosition(), target.position + targetAimOffset, target);

        if (currentState != EnemyState.Combat)
        {
            ChangeState(EnemyState.Combat);
        }
    }

    private void Start()
    {
        AcquireTargetByTagIfNeeded();
        ChooseNextPatrolDestination();
    }

    private void Update()
    {
        if (health != null && !health.IsAlive)
        {
            ChangeState(EnemyState.Dead);
        }

        if (currentState == EnemyState.Dead)
        {
            StopMoving();
            return;
        }

        AcquireTargetByTagIfNeeded();
        UpdateTargetVisibility();

        switch (currentState)
        {
            case EnemyState.Patrol:
                UpdatePatrol();
                break;
            case EnemyState.Investigate:
                UpdateInvestigate();
                break;
            case EnemyState.Combat:
                UpdateCombat();
                break;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void AcquireTargetByTagIfNeeded()
    {
        if (target != null || !findTargetByTagOnStart || string.IsNullOrWhiteSpace(targetTag))
        {
            return;
        }

        GameObject targetObject = GameObject.FindGameObjectWithTag(targetTag);
        if (targetObject != null)
        {
            target = targetObject.transform;
        }
    }

    private void UpdateTargetVisibility()
    {
        if (target == null)
        {
            return;
        }

        if (currentState == EnemyState.Dead)
        {
            return;
        }

        Vector3 origin = GetEyePosition();
        Vector3 aimPoint = target.position + targetAimOffset;
        Vector3 toTarget = aimPoint - origin;
        float distance = toTarget.magnitude;

        if (distance > detectionRadius)
        {
            CanSeeTarget = false;
            LoseTargetIfNeeded();
            return;
        }

        // No field of view angle check - enemy sees in all directions (360°)
        // But still requires line of sight
        if (!HasLineOfSight(origin, aimPoint, target))
        {
            CanSeeTarget = false;
            LoseTargetIfNeeded();
            return;
        }

        CanSeeTarget = true;
        lastSeenTargetPosition = target.position;
        lastTimeSawTarget = Time.time;

        if (currentState != EnemyState.Combat)
        {
            ChangeState(EnemyState.Combat);
        }
    }

    private void LoseTargetIfNeeded()
    {
        if (currentState == EnemyState.Combat && Time.time > lastTimeSawTarget + loseTargetDelay)
        {
            ChangeState(EnemyState.Investigate);
            SetDestination(lastSeenTargetPosition, combatMoveSpeed, stoppingDistance);
        }
    }

    private void UpdatePatrol()
    {
        if (Time.time < waitUntilTime)
        {
            StopMoving();
            return;
        }

        if (ReachedDestination())
        {
            waitUntilTime = Time.time + waitAtPatrolPoint;
            ChooseNextPatrolDestination();
        }

        MoveToCurrentDestination(patrolSpeed);
    }

    private void UpdateInvestigate()
    {
        MoveToCurrentDestination(combatMoveSpeed);

        if (ReachedDestination())
        {
            ChangeState(EnemyState.Patrol);
            ChooseNextPatrolDestination();
        }
    }

    private void UpdateCombat()
    {
        if (target == null)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        FacePosition(target.position + targetAimOffset);

        if (!CanSeeTarget && Time.time > lastTimeSawTarget + loseTargetDelay)
        {
            ChangeState(EnemyState.Investigate);
            SetDestination(lastSeenTargetPosition, combatMoveSpeed, stoppingDistance);
            return;
        }

        UpdateCombatMovement();

        if (CanSeeTarget)
        {
            TryShoot();
        }
    }

    private void UpdateCombatMovement()
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (toTarget.sqrMagnitude <= 0.001f)
        {
            return;
        }

        if (Time.time >= nextStrafeChangeTime)
        {
            strafeDirection = Random.value > 0.5f ? 1 : -1;
            nextStrafeChangeTime = Time.time + strafeChangeInterval;
        }

        Vector3 destination;

        if (distance < tooCloseDistance)
        {
            destination = transform.position - toTarget.normalized * strafeDistance;
        }
        else if (distance > preferredCombatDistance + 2f)
        {
            destination = target.position - toTarget.normalized * preferredCombatDistance;
        }
        else
        {
            Vector3 strafe = Vector3.Cross(Vector3.up, toTarget.normalized) * strafeDirection;
            destination = transform.position + strafe * strafeDistance;
        }

        SetDestination(destination, combatMoveSpeed, stoppingDistance);
        MoveToCurrentDestination(combatMoveSpeed);
    }

    private void TryShoot()
    {
        if (Time.time < nextShotTime)
        {
            return;
        }

        nextShotTime = Time.time + 1f / fireRate;

        Transform shotTransform = firePoint != null ? firePoint : transform;
        Vector3 origin = firePoint != null ? firePoint.position : GetEyePosition();
        Vector3 direction = GetSpreadDirection((target.position + targetAimOffset - origin).normalized);

        if (cubeVisualFx != null)
        {
            cubeVisualFx.SendMessage("PlayFireFx", SendMessageOptions.DontRequireReceiver);
        }

        PlayFireSound(origin);
        SpawnFx(muzzleFxPrefab, origin, shotTransform.rotation);

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, weaponRange, hitLayers, triggerInteraction))
        {
            return;
        }

        bool hitDamageable = TryDamageHit(hit, direction);
        Quaternion impactRotation = Quaternion.LookRotation(hit.normal);
        SpawnFx(hitDamageable ? damageableImpactFx : worldImpactFx, hit.point + hit.normal * 0.01f, impactRotation);

        if (logShots)
        {
            Debug.Log($"{name} shot {hit.collider.name}. Damageable: {hitDamageable}");
        }
    }

    private Vector3 GetSpreadDirection(Vector3 baseDirection)
    {
        if (spreadAngle <= 0f)
        {
            return baseDirection;
        }

        Quaternion spread = Quaternion.Euler(Random.Range(-spreadAngle, spreadAngle), Random.Range(-spreadAngle, spreadAngle), 0f);
        return spread * baseDirection;
    }

    private bool TryDamageHit(RaycastHit hit, Vector3 shotDirection)
    {
        IDamageable damageable = FindDamageableInParents(hit.collider.transform);
        if (damageable == null || !damageable.IsAlive)
        {
            return false;
        }

        DamageInfo damageInfo = new DamageInfo(attackDamage, gameObject, gameObject, hit.point, hit.normal, shotDirection, hit.collider, false);
        damageable.TakeDamage(damageInfo);
        return true;
    }

    private void SetupFireAudioSourceIfNeeded()
    {
        if (fireAudioSource == null)
        {
            fireAudioSource = GetComponent<AudioSource>();
        }

        if (fireAudioSource == null)
        {
            fireAudioSource = gameObject.AddComponent<AudioSource>();
        }

        fireAudioSource.playOnAwake = false;
        fireAudioSource.loop = false;
        fireAudioSource.spatialBlend = 1f;
        fireAudioSource.rolloffMode = AudioRolloffMode.Linear;
        fireAudioSource.minDistance = 2.5f;
        fireAudioSource.maxDistance = 45f;
        fireAudioSource.dopplerLevel = 0.15f;
    }

    private void PlayFireSound(Vector3 origin)
    {
        if (fireAudioSource == null || fireClip == null)
        {
            return;
        }

        fireAudioSource.transform.position = origin;
        fireAudioSource.pitch = 1f + Random.Range(-firePitchRandomness, firePitchRandomness);
        fireAudioSource.PlayOneShot(fireClip, fireVolume);
    }

    private void ChooseNextPatrolDestination()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                Transform patrolPoint = patrolPoints[currentPatrolIndex];
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;

                if (patrolPoint != null)
                {
                    SetDestination(patrolPoint.position, patrolSpeed, stoppingDistance);
                    return;
                }
            }
        }

        Vector3 forwardPoint = spawnPosition + spawnForward * autoPatrolForwardDistance;
        Vector3 backwardPoint = spawnPosition - spawnForward * autoPatrolBackwardDistance;
        Vector3 destination = autoPatrolIndex == 0 ? forwardPoint : backwardPoint;
        autoPatrolIndex = 1 - autoPatrolIndex;
        SetDestination(destination, patrolSpeed, stoppingDistance);
    }

    private void SetDestination(Vector3 destination, float speed, float stopDistance)
    {
        currentDestination = destination;

        if (agent != null && useNavMeshAgentIfAvailable && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = speed;
            agent.stoppingDistance = stopDistance;
            agent.SetDestination(destination);
        }
    }

    private void MoveToCurrentDestination(float speed)
    {
        if (agent != null && useNavMeshAgentIfAvailable && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            return;
        }

        Vector3 toDestination = currentDestination - transform.position;
        toDestination.y = 0f;

        if (toDestination.magnitude <= patrolPointReachDistance)
        {
            return;
        }

        transform.position += toDestination.normalized * speed * Time.deltaTime;
        FacePosition(currentDestination);
    }

    private bool ReachedDestination()
    {
        if (agent != null && useNavMeshAgentIfAvailable && agent.isOnNavMesh)
        {
            return !agent.pathPending && agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, patrolPointReachDistance);
        }

        Vector3 flatDelta = currentDestination - transform.position;
        flatDelta.y = 0f;
        return flatDelta.magnitude <= patrolPointReachDistance;
    }

    private void StopMoving()
    {
        if (agent != null && useNavMeshAgentIfAvailable && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
    }

    private void FacePosition(Vector3 position)
    {
        Vector3 direction = position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private bool HasLineOfSight(Vector3 origin, Vector3 destination, Transform expectedTarget)
    {
        Vector3 direction = destination - origin;

        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, direction.magnitude, lineOfSightLayers, triggerInteraction);
        if (hits == null || hits.Length == 0)
        {
            return true;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null || hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                continue;
            }

            return hitTransform == expectedTarget || hitTransform.IsChildOf(expectedTarget);
        }

        return true;
    }

    private Vector3 GetEyePosition()
    {
        return transform.position + eyeOffset;
    }

    private void SpawnFx(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject spawned = Instantiate(prefab, position, rotation);
        Destroy(spawned, fxLifetime);
    }

    private void ChangeState(EnemyState newState)
    {
        if (currentState == newState)
        {
            return;
        }

        currentState = newState;

        if (logStateChanges)
        {
            Debug.Log($"{name} changed state to {currentState}");
        }
    }

    private static IDamageable FindDamageableInParents(Transform startTransform)
    {
        Transform current = startTransform;

        while (current != null)
        {
            MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IDamageable damageable)
                {
                    return damageable;
                }
            }

            current = current.parent;
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, tooCloseDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, preferredCombatDistance);

        Vector3 basePosition = Application.isPlaying ? spawnPosition : transform.position;
        Vector3 baseForward = Application.isPlaying ? spawnForward : transform.forward;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(basePosition - baseForward * autoPatrolBackwardDistance, basePosition + baseForward * autoPatrolForwardDistance);
    }
}