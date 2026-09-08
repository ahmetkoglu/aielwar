using UnityEngine;
using UnityEngine.Events;
using TMPro;

[System.Serializable]
public class WeaponHitFeedbackEvent : UnityEvent<float, bool, bool, Vector3>
{
}

/// <summary>
/// Simple FPS weapon controller for firing, muzzle particles and recoil feel.
/// Attach this script to the weapon model under the FPS camera.
/// Assign muzzlePoint to the weapon barrel tip and muzzleFlash to your fire ParticleSystem.
/// </summary>
public class FpsWeaponController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Usually the FPS camera. Used for optional camera recoil.")]
    [SerializeField] private Transform cameraTransform;

    [Tooltip("Point at the barrel tip. Particle effect will be spawned/played here.")]
    [SerializeField] private Transform muzzlePoint;

    [Tooltip("Your muzzle/fire particle effect. Can be a child object or prefab.")]
    [SerializeField] private ParticleSystem muzzleFlash;

    [Tooltip("Optional: assign the whole FX prefab/object here if the ParticleSystem is on a child.")]
    [SerializeField] private GameObject muzzleFlashObject;

    [Tooltip("Optional URP-compatible material forced onto every ParticleSystemRenderer in the muzzle FX.")]
    [SerializeField] private Material muzzleFlashOverrideMaterial;

    [Tooltip("If the assigned muzzleFlash is a prefab/project asset, instantiate it at the muzzle point.")]
    [SerializeField] private bool instantiateMuzzleFlashPrefab = true;

    [Tooltip("How long spawned muzzle flash instances stay in the scene before being destroyed.")]
    [SerializeField, Min(0.01f)] private float spawnedMuzzleFlashLifetime = 2f;

    [Header("Fire")]
    [SerializeField] private KeyCode fireKey = KeyCode.Mouse0;
    [SerializeField, Min(0.01f)] private float fireRate = 9f;
    [SerializeField] private bool automaticFire = true;

    [Header("Fire Audio")]
    [SerializeField] private AudioSource fireAudioSource;
    [SerializeField] private AudioClip fireClip;
    [SerializeField, Range(0f, 1f)] private float fireVolume = 0.9f;
    [SerializeField, Min(0f)] private float firePitchRandomness = 0.035f;

    [Header("Ammo & Reload")]
    [SerializeField, Min(1)] private int magazineSize = 30;
    [SerializeField, Min(0)] private int startingReserveAmmo = 120;
    [SerializeField, Min(0.01f)] private float reloadDuration = 1.6f;
    [SerializeField] private KeyCode reloadKey = KeyCode.R;
    [SerializeField] private bool autoReloadWhenEmpty = true;
    [SerializeField] private bool consumeReserveAmmo = true;

    [Header("Reload Weapon Animation")]
    [SerializeField] private bool useReloadAnimation = true;
    [SerializeField, Min(0.01f)] private float reloadAnimationSmooth = 14f;
    [Tooltip("Main weapon offset while the player is changing the magazine.")]
    [SerializeField] private Vector3 reloadPositionOffset = new Vector3(0.11f, -0.22f, -0.12f);
    [SerializeField] private Vector3 reloadRotationOffset = new Vector3(22f, -18f, 16f);
    [Tooltip("Small punch near the magazine insert moment.")]
    [SerializeField] private Vector3 reloadInsertPositionKick = new Vector3(-0.025f, 0.035f, 0.035f);
    [SerializeField] private Vector3 reloadInsertRotationKick = new Vector3(-8f, 5f, -5f);

    [Header("Ammo UI")]
    [SerializeField] private TMP_Text ammoText;
    [SerializeField] private string ammoFormat = "{0} / {1}";
    [SerializeField] private string reloadingFormat = "Reloading... {0} / {1}";

    [Header("Hit Feedback")]
    [SerializeField] private HitFeedbackUi hitFeedbackUi;
    public WeaponHitFeedbackEvent OnDamageDealt;

    [Header("Ammo Events")]
    public UnityEvent OnReloadStarted;
    public UnityEvent OnReloadCompleted;
    public UnityEvent OnDryFire;

    [Header("Raycast Hit & Damage")]
    [SerializeField] private bool useRaycastHit = true;
    [SerializeField, Min(0f)] private float damage = 25f;
    [SerializeField, Min(0.1f)] private float range = 150f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private GameObject attacker;

    [Header("Impact FX")]
    [SerializeField] private GameObject damageableImpactFx;
    [SerializeField] private GameObject worldImpactFx;
    [SerializeField, Min(0.01f)] private float impactFxLifetime = 3f;
    [SerializeField] private bool alignImpactFxToHitNormal = true;

    [Header("Weapon Recoil")]
    [Tooltip("Local position kick applied to the weapon when firing.")]
    [SerializeField] private Vector3 weaponKickback = new Vector3(0f, 0.015f, -0.09f);

    [Tooltip("Local rotation kick applied to the weapon when firing. X is upward recoil.")]
    [SerializeField] private Vector3 weaponRotationKick = new Vector3(-7f, 2f, 0f);

    [SerializeField, Min(0.01f)] private float recoilSnapSpeed = 24f;
    [SerializeField, Min(0.01f)] private float recoilReturnSpeed = 12f;
    [SerializeField, Min(0f)] private float maxWeaponRecoilPosition = 0.18f;
    [SerializeField, Min(0f)] private float maxWeaponRecoilRotation = 16f;

    [Header("Camera Recoil")]
    [SerializeField] private bool useCameraRecoil = true;
    [SerializeField] private Vector2 cameraRecoilPerShot = new Vector2(1.5f, 0.55f);
    [SerializeField, Min(0.01f)] private float cameraRecoilReturnSpeed = 9f;
    [SerializeField, Min(0f)] private float maxCameraRecoilPitch = 8f;

    [Header("Movement Weapon Animation")]
    [Tooltip("Optional player movement controller. If empty, the script searches parent objects.")]
    [SerializeField] private AdvancedFpsController movementController;

    [SerializeField] private bool useMovementAnimation = true;
    [SerializeField, Min(0.01f)] private float movementAnimationSmooth = 10f;
    [SerializeField, Min(0.01f)] private float bobFrequency = 8f;
    [SerializeField, Min(0f)] private float walkBobAmount = 0.018f;
    [SerializeField, Min(0f)] private float sprintBobMultiplier = 1.65f;
    [SerializeField, Min(0f)] private float bobRotationAmount = 1.35f;
    [SerializeField, Min(0f)] private float strafeSwayAmount = 0.035f;
    [SerializeField, Min(0f)] private float strafeSwayRotation = 3.5f;

    [Header("Slide Weapon Pose")]
    [SerializeField] private Vector3 slidePositionOffset = new Vector3(0.08f, -0.11f, -0.06f);
    [SerializeField] private Vector3 slideRotationOffset = new Vector3(8f, -7f, 12f);

    [Header("Randomness")]
    [SerializeField, Min(0f)] private float horizontalRecoilRandomness = 1f;
    [SerializeField, Min(0f)] private float verticalRecoilRandomness = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool logShots;

    public bool IsFiring { get; private set; }
    public bool IsReloading { get; private set; }
    public int CurrentAmmoInMagazine { get; private set; }
    public int CurrentReserveAmmo { get; private set; }
    public float NextFireTime { get; private set; }

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Vector3 currentWeaponRecoilPosition;
    private Vector3 targetWeaponRecoilPosition;
    private Vector3 currentWeaponRecoilRotation;
    private Vector3 targetWeaponRecoilRotation;
    private float currentCameraRecoilPitch;
    private float targetCameraRecoilPitch;
    private float currentCameraRecoilYaw;
    private float targetCameraRecoilYaw;
    private Vector3 currentMovementPositionOffset;
    private Vector3 targetMovementPositionOffset;
    private Vector3 currentMovementRotationOffset;
    private Vector3 targetMovementRotationOffset;
    private Vector3 currentReloadPositionOffset;
    private Vector3 targetReloadPositionOffset;
    private Vector3 currentReloadRotationOffset;
    private Vector3 targetReloadRotationOffset;
    private float bobTimer;
    private float reloadEndTime;

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (movementController == null)
        {
            movementController = GetComponentInParent<AdvancedFpsController>();
        }

        if (hitFeedbackUi == null)
        {
            hitFeedbackUi = FindAnyObjectByType<HitFeedbackUi>();
        }

        CurrentAmmoInMagazine = magazineSize;
        CurrentReserveAmmo = startingReserveAmmo;
        UpdateAmmoText();
    }

    private void Update()
    {
        HandleReloadInput();
        UpdateReloadProgress();
        HandleFireInput();
        UpdateWeaponRecoil();
        UpdateCameraRecoil();
    }

    private void HandleReloadInput()
    {
        if (Input.GetKeyDown(reloadKey))
        {
            TryStartReload();
        }
    }

    private void UpdateReloadProgress()
    {
        if (!IsReloading)
        {
            return;
        }

        UpdateAmmoText();

        if (Time.time >= reloadEndTime)
        {
            CompleteReload();
        }
    }

    private void HandleFireInput()
    {
        IsFiring = automaticFire ? Input.GetKey(fireKey) : Input.GetKeyDown(fireKey);

        if (IsFiring && Time.time >= NextFireTime)
        {
            TryFire();
        }
    }

    private void TryFire()
    {
        if (IsReloading)
        {
            return;
        }

        if (CurrentAmmoInMagazine <= 0)
        {
            DryFire();

            if (autoReloadWhenEmpty)
            {
                TryStartReload();
            }

            return;
        }

        Fire();
    }

    private void Fire()
    {
        NextFireTime = Time.time + 1f / fireRate;
        CurrentAmmoInMagazine = Mathf.Max(0, CurrentAmmoInMagazine - 1);
        UpdateAmmoText();

        PlayFireSound();
        PlayMuzzleFlash();
        PerformRaycastHit();
        AddRecoil();

        if (logShots)
        {
            Debug.Log($"{name} fired at {Time.time:0.00}. MuzzleFlash: {(muzzleFlash != null ? muzzleFlash.name : "None")}, MuzzleFlashObject: {(muzzleFlashObject != null ? muzzleFlashObject.name : "None")}");
        }
    }

    private void DryFire()
    {
        NextFireTime = Time.time + 1f / fireRate;
        OnDryFire?.Invoke();

        if (logShots)
        {
            Debug.Log($"{name} dry fired. No ammo in magazine.");
        }
    }

    public bool TryStartReload()
    {
        if (IsReloading || CurrentAmmoInMagazine >= magazineSize)
        {
            return false;
        }

        if (consumeReserveAmmo && CurrentReserveAmmo <= 0)
        {
            return false;
        }

        IsReloading = true;
        reloadEndTime = Time.time + reloadDuration;
        OnReloadStarted?.Invoke();
        UpdateAmmoText();

        if (logShots)
        {
            Debug.Log($"{name} started reload.");
        }

        return true;
    }

    private void CompleteReload()
    {
        int neededAmmo = magazineSize - CurrentAmmoInMagazine;
        int ammoToLoad = consumeReserveAmmo ? Mathf.Min(neededAmmo, CurrentReserveAmmo) : neededAmmo;

        CurrentAmmoInMagazine += ammoToLoad;

        if (consumeReserveAmmo)
        {
            CurrentReserveAmmo -= ammoToLoad;
        }

        IsReloading = false;
        OnReloadCompleted?.Invoke();
        UpdateAmmoText();

        if (logShots)
        {
            Debug.Log($"{name} completed reload. Ammo: {CurrentAmmoInMagazine}/{CurrentReserveAmmo}");
        }
    }

    public void AddReserveAmmo(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        CurrentReserveAmmo += amount;
        UpdateAmmoText();
    }

    /// <summary>
    /// Fully refills the magazine and sets reserve ammo to a high value.
    /// Used by WaveSpawner when a wave is completed.
    /// </summary>
    public void RefillAmmo()
    {
        CurrentAmmoInMagazine = magazineSize;
        CurrentReserveAmmo = Mathf.Max(CurrentReserveAmmo, startingReserveAmmo * 2);
        
        if (IsReloading)
        {
            IsReloading = false;
        }
        
        UpdateAmmoText();
    }

    public void SetAmmoText(TMP_Text targetAmmoText)
    {
        ammoText = targetAmmoText;
        UpdateAmmoText();
    }

    private void UpdateAmmoText()
    {
        if (ammoText == null)
        {
            return;
        }

        string format = IsReloading ? reloadingFormat : ammoFormat;
        ammoText.text = string.Format(format, CurrentAmmoInMagazine, CurrentReserveAmmo);
    }

    private void PerformRaycastHit()
    {
        if (!useRaycastHit)
        {
            return;
        }

        Transform originTransform = cameraTransform != null ? cameraTransform : transform;
        Ray ray = new Ray(originTransform.position, originTransform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, range, hitLayers, triggerInteraction))
        {
            return;
        }

        bool hitDamageable = TryDamageHit(hit, ray.direction);
        SpawnImpactFx(hit, hitDamageable);

        if (logShots)
        {
            Debug.Log($"{name} hit {hit.collider.name} at {hit.point}. Damageable: {hitDamageable}");
        }
    }

    private bool TryDamageHit(RaycastHit hit, Vector3 shotDirection)
    {
        DamageHitbox hitbox = hit.collider.GetComponentInParent<DamageHitbox>();
        IDamageable damageable = null;
        float finalDamage = damage;
        bool isCritical = false;

        if (hitbox != null && hitbox.TryGetDamageable(out IDamageable hitboxDamageable))
        {
            damageable = hitboxDamageable;
            finalDamage *= hitbox.DamageMultiplier;
            isCritical = hitbox.CriticalHitbox;
        }
        else
        {
            damageable = FindDamageableInParents(hit.collider.transform);
        }

        if (damageable == null || !damageable.IsAlive)
        {
            return false;
        }

        DamageInfo damageInfo = new DamageInfo(
            finalDamage,
            attacker != null ? attacker : gameObject,
            gameObject,
            hit.point,
            hit.normal,
            shotDirection,
            hit.collider,
            isCritical
        );

        bool wasAlive = damageable.IsAlive;
        damageable.TakeDamage(damageInfo);
        TriggerDamageVisualFx(hit.collider.transform);
        bool killed = wasAlive && !damageable.IsAlive;

        if (hitFeedbackUi != null)
        {
            hitFeedbackUi.ShowHit(finalDamage, isCritical, killed, hit.point);
        }

        OnDamageDealt?.Invoke(finalDamage, isCritical, killed, hit.point);

        return true;
    }

    private void TriggerDamageVisualFx(Transform hitTransform)
    {
        if (hitTransform == null)
        {
            return;
        }

        CubeEnemyVisualFx visualFx = hitTransform.GetComponentInParent<CubeEnemyVisualFx>();
        if (visualFx != null)
        {
            visualFx.PlayDamageFx();
            return;
        }

        hitTransform.SendMessageUpwards("PlayDamageFx", SendMessageOptions.DontRequireReceiver);
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

    private void SpawnImpactFx(RaycastHit hit, bool hitDamageable)
    {
        GameObject prefab = hitDamageable ? damageableImpactFx : worldImpactFx;
        if (prefab == null)
        {
            return;
        }

        Quaternion rotation = alignImpactFxToHitNormal
            ? Quaternion.LookRotation(hit.normal)
            : Quaternion.identity;

        GameObject spawnedImpact = Instantiate(prefab, hit.point + hit.normal * 0.01f, rotation);
        Destroy(spawnedImpact, impactFxLifetime);
    }

    private void PlayMuzzleFlash()
    {
        if (muzzleFlash == null && muzzleFlashObject == null)
        {
            if (logShots)
            {
                Debug.LogWarning($"{name} fired, but no muzzle flash ParticleSystem or GameObject is assigned.");
            }

            return;
        }

        Transform spawnTransform = muzzlePoint != null ? muzzlePoint : transform;

        if (muzzleFlashObject != null)
        {
            bool objectIsSceneObject = muzzleFlashObject.scene.IsValid();
            if (!objectIsSceneObject && instantiateMuzzleFlashPrefab)
            {
                GameObject spawnedObject = Instantiate(muzzleFlashObject, spawnTransform.position, spawnTransform.rotation);
                ApplyOverrideMaterial(spawnedObject);
                PlayAllParticles(spawnedObject);
                Destroy(spawnedObject, spawnedMuzzleFlashLifetime);
                if (logShots)
                {
                    Debug.Log($"Spawned muzzle flash object prefab: {spawnedObject.name}");
                }
                return;
            }

            muzzleFlashObject.transform.SetPositionAndRotation(spawnTransform.position, spawnTransform.rotation);
            ApplyOverrideMaterial(muzzleFlashObject);
            if (!muzzleFlashObject.activeSelf)
            {
                muzzleFlashObject.SetActive(true);
            }

            PlayAllParticles(muzzleFlashObject);
            return;
        }

        // If muzzleFlash is a prefab/project asset, it is not part of a loaded scene.
        // In that case Play() will not show anything in the scene, so we instantiate it.
        bool muzzleFlashIsSceneObject = muzzleFlash.gameObject.scene.IsValid();
        if (!muzzleFlashIsSceneObject && instantiateMuzzleFlashPrefab)
        {
            ParticleSystem spawnedFlash = Instantiate(muzzleFlash, spawnTransform.position, spawnTransform.rotation);
            ApplyOverrideMaterial(spawnedFlash.gameObject);
            spawnedFlash.Play(true);
            Destroy(spawnedFlash.gameObject, spawnedMuzzleFlashLifetime);
            if (logShots)
            {
                Debug.Log($"Spawned muzzle flash particle prefab: {spawnedFlash.name}");
            }
            return;
        }

        muzzleFlash.transform.SetPositionAndRotation(spawnTransform.position, spawnTransform.rotation);
        ApplyOverrideMaterial(muzzleFlash.gameObject);

        if (!muzzleFlash.gameObject.activeSelf)
        {
            muzzleFlash.gameObject.SetActive(true);
        }

        muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        muzzleFlash.Play(true);
    }

    private void PlayFireSound()
    {
        if (fireAudioSource == null || fireClip == null)
        {
            return;
        }

        fireAudioSource.pitch = 1f + Random.Range(-firePitchRandomness, firePitchRandomness);
        fireAudioSource.PlayOneShot(fireClip, fireVolume);
    }

    private void ApplyOverrideMaterial(GameObject particleRoot)
    {
        if (muzzleFlashOverrideMaterial == null || particleRoot == null)
        {
            return;
        }

        ParticleSystemRenderer[] renderers = particleRoot.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material = muzzleFlashOverrideMaterial;
        }
    }

    private void PlayAllParticles(GameObject particleRoot)
    {
        ParticleSystem[] particles = particleRoot.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < particles.Length; i++)
        {
            if (!particles[i].gameObject.activeSelf)
            {
                particles[i].gameObject.SetActive(true);
            }

            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles[i].Play(true);
        }
    }

    private void AddRecoil()
    {
        float randomYaw = Random.Range(-horizontalRecoilRandomness, horizontalRecoilRandomness);
        float randomPitch = Random.Range(0f, verticalRecoilRandomness);

        targetWeaponRecoilPosition += weaponKickback;
        targetWeaponRecoilPosition = Vector3.ClampMagnitude(targetWeaponRecoilPosition, maxWeaponRecoilPosition);

        targetWeaponRecoilRotation += new Vector3(
            weaponRotationKick.x - randomPitch,
            weaponRotationKick.y + randomYaw,
            weaponRotationKick.z - randomYaw * 0.35f
        );
        targetWeaponRecoilRotation = Vector3.ClampMagnitude(targetWeaponRecoilRotation, maxWeaponRecoilRotation);

        if (useCameraRecoil)
        {
            targetCameraRecoilPitch = Mathf.Clamp(targetCameraRecoilPitch - cameraRecoilPerShot.x - randomPitch, -maxCameraRecoilPitch, 0f);
            targetCameraRecoilYaw += Random.Range(-cameraRecoilPerShot.y, cameraRecoilPerShot.y);
        }
    }

    private void UpdateWeaponRecoil()
    {
        UpdateMovementAnimationOffsets();
        UpdateReloadAnimationOffsets();

        targetWeaponRecoilPosition = Vector3.Lerp(targetWeaponRecoilPosition, Vector3.zero, recoilReturnSpeed * Time.deltaTime);
        targetWeaponRecoilRotation = Vector3.Lerp(targetWeaponRecoilRotation, Vector3.zero, recoilReturnSpeed * Time.deltaTime);

        currentWeaponRecoilPosition = Vector3.Lerp(currentWeaponRecoilPosition, targetWeaponRecoilPosition, recoilSnapSpeed * Time.deltaTime);
        currentWeaponRecoilRotation = Vector3.Lerp(currentWeaponRecoilRotation, targetWeaponRecoilRotation, recoilSnapSpeed * Time.deltaTime);

        transform.localPosition = initialLocalPosition + currentMovementPositionOffset + currentReloadPositionOffset + currentWeaponRecoilPosition;
        transform.localRotation = initialLocalRotation * Quaternion.Euler(currentMovementRotationOffset + currentReloadRotationOffset + currentWeaponRecoilRotation);
    }

    private void UpdateReloadAnimationOffsets()
    {
        if (!useReloadAnimation || !IsReloading)
        {
            targetReloadPositionOffset = Vector3.zero;
            targetReloadRotationOffset = Vector3.zero;
            SmoothReloadAnimationOffsets();
            return;
        }

        float reloadProgress = Mathf.Clamp01(1f - ((reloadEndTime - Time.time) / reloadDuration));
        float lowerWeight = GetReloadLowerWeight(reloadProgress);
        float insertWeight = GetReloadInsertWeight(reloadProgress);

        targetReloadPositionOffset = reloadPositionOffset * lowerWeight + reloadInsertPositionKick * insertWeight;
        targetReloadRotationOffset = reloadRotationOffset * lowerWeight + reloadInsertRotationKick * insertWeight;

        SmoothReloadAnimationOffsets();
    }

    private float GetReloadLowerWeight(float reloadProgress)
    {
        if (reloadProgress < 0.22f)
        {
            return Mathf.SmoothStep(0f, 1f, reloadProgress / 0.22f);
        }

        if (reloadProgress < 0.72f)
        {
            return 1f;
        }

        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, reloadProgress));
    }

    private float GetReloadInsertWeight(float reloadProgress)
    {
        float insertPhase = Mathf.InverseLerp(0.58f, 0.78f, reloadProgress);
        insertPhase = Mathf.Clamp01(insertPhase);
        return Mathf.Sin(insertPhase * Mathf.PI);
    }

    private void SmoothReloadAnimationOffsets()
    {
        currentReloadPositionOffset = Vector3.Lerp(
            currentReloadPositionOffset,
            targetReloadPositionOffset,
            reloadAnimationSmooth * Time.deltaTime
        );

        currentReloadRotationOffset = Vector3.Lerp(
            currentReloadRotationOffset,
            targetReloadRotationOffset,
            reloadAnimationSmooth * Time.deltaTime
        );
    }

    private void UpdateMovementAnimationOffsets()
    {
        if (!useMovementAnimation)
        {
            targetMovementPositionOffset = Vector3.zero;
            targetMovementRotationOffset = Vector3.zero;
            SmoothMovementAnimationOffsets();
            return;
        }

        Vector2 moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        moveInput = moveInput.sqrMagnitude > 1f ? moveInput.normalized : moveInput;

        bool isSliding = movementController != null && movementController.IsSliding;
        bool isSprinting = movementController != null && movementController.IsSprinting;
        bool isGrounded = movementController == null || movementController.IsGrounded;
        float horizontalSpeed = movementController != null
            ? new Vector3(movementController.CurrentVelocity.x, 0f, movementController.CurrentVelocity.z).magnitude
            : moveInput.magnitude;
        bool isMoving = isGrounded && horizontalSpeed > 0.1f && moveInput.sqrMagnitude > 0.01f;

        if (isSliding)
        {
            targetMovementPositionOffset = slidePositionOffset;
            targetMovementRotationOffset = slideRotationOffset;
            SmoothMovementAnimationOffsets();
            return;
        }

        if (!isMoving)
        {
            bobTimer = 0f;
            targetMovementPositionOffset = Vector3.zero;
            targetMovementRotationOffset = Vector3.zero;
            SmoothMovementAnimationOffsets();
            return;
        }

        float sprintMultiplier = isSprinting ? sprintBobMultiplier : 1f;
        bobTimer += Time.deltaTime * bobFrequency * sprintMultiplier;

        float bobSin = Mathf.Sin(bobTimer);
        float bobCos = Mathf.Cos(bobTimer * 2f);
        float bobAmount = walkBobAmount * sprintMultiplier;
        float lateralSway = -moveInput.x * strafeSwayAmount;

        targetMovementPositionOffset = new Vector3(
            lateralSway + bobSin * bobAmount * 0.35f,
            Mathf.Abs(bobCos) * bobAmount,
            0f
        );

        targetMovementRotationOffset = new Vector3(
            bobCos * bobRotationAmount * sprintMultiplier,
            -moveInput.x * strafeSwayRotation,
            -moveInput.x * strafeSwayRotation + bobSin * bobRotationAmount
        );

        SmoothMovementAnimationOffsets();
    }

    private void SmoothMovementAnimationOffsets()
    {
        currentMovementPositionOffset = Vector3.Lerp(
            currentMovementPositionOffset,
            targetMovementPositionOffset,
            movementAnimationSmooth * Time.deltaTime
        );

        currentMovementRotationOffset = Vector3.Lerp(
            currentMovementRotationOffset,
            targetMovementRotationOffset,
            movementAnimationSmooth * Time.deltaTime
        );
    }

    private void UpdateCameraRecoil()
    {
        if (!useCameraRecoil || cameraTransform == null)
        {
            return;
        }

        targetCameraRecoilPitch = Mathf.Lerp(targetCameraRecoilPitch, 0f, cameraRecoilReturnSpeed * Time.deltaTime);
        targetCameraRecoilYaw = Mathf.Lerp(targetCameraRecoilYaw, 0f, cameraRecoilReturnSpeed * Time.deltaTime);

        currentCameraRecoilPitch = Mathf.Lerp(currentCameraRecoilPitch, targetCameraRecoilPitch, recoilSnapSpeed * Time.deltaTime);
        currentCameraRecoilYaw = Mathf.Lerp(currentCameraRecoilYaw, targetCameraRecoilYaw, recoilSnapSpeed * Time.deltaTime);

        cameraTransform.localRotation *= Quaternion.Euler(currentCameraRecoilPitch * Time.deltaTime, currentCameraRecoilYaw * Time.deltaTime, 0f);
    }
}