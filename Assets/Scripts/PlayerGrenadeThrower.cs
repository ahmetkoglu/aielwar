using UnityEngine;

/// <summary>
/// Hold G to pull a grenade into the hand and hide the weapon. Release G to throw it toward the crosshair.
/// Assign grenadePrefab in the Inspector when your prefab is ready.
/// </summary>
public class PlayerGrenadeThrower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private GameObject grenadePrefab;
    [SerializeField] private Transform handPoint;
    [SerializeField] private GameObject explosionFxPrefab;
    [SerializeField] private HitFeedbackUi hitFeedbackUi;

    [Header("Audio")]
    [SerializeField] private AudioClip explosionClip;
    [SerializeField, Range(0f, 1f)] private float explosionVolume = 0.95f;

    [Header("Input")]
    [SerializeField] private KeyCode throwKey = KeyCode.G;
    [SerializeField, Min(0f)] private float throwCooldown = 0.8f;

    [Header("Hold & Throw Feel")]
    [SerializeField, Min(0.01f)] private float equipAnimationSpeed = 13f;
    [SerializeField, Min(0f)] private float throwVelocity = 19f;
    [SerializeField, Min(0f)] private float spinTorque = 10f;
    [SerializeField] private Vector3 fallbackHandLocalPosition = new Vector3(0.38f, -0.28f, 0.62f);
    [SerializeField] private Vector3 grenadeHiddenLocalPosition = new Vector3(0.12f, -0.58f, 0.18f);
    [SerializeField] private Vector3 grenadeReadyLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 grenadeHiddenEuler = new Vector3(35f, 45f, -20f);
    [SerializeField] private Vector3 grenadeReadyEuler = new Vector3(-18f, 25f, 8f);
    [SerializeField] private Vector3 handPreviewScale = Vector3.one;

    [Header("Weapon Hide While Holding")]
    [SerializeField] private bool hideWeaponsWhileHoldingGrenade = true;
    [SerializeField] private FpsWeaponController[] weaponsToHide;

    [Header("Weapon Switcher Integration")]
    [SerializeField] private WeaponSwitcher weaponSwitcher;

    [Header("Grenade Stats")]
    [SerializeField, Min(0.05f)] private float fuseTime = 2.4f;
    [SerializeField, Min(0f)] private float explosionDamage = 65f;
    [SerializeField, Min(0.1f)] private float explosionRadius = 5f;
    [SerializeField, Min(0f)] private float explosionForce = 650f;

    private GameObject previewGrenade;
    private float nextThrowTime;
    private bool isHoldingGrenade;

    public bool IsHoldingGrenade => isHoldingGrenade;

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (weaponsToHide == null || weaponsToHide.Length == 0)
        {
            weaponsToHide = GetComponentsInChildren<FpsWeaponController>(true);
        }

        if (weaponSwitcher == null)
        {
            weaponSwitcher = GetComponent<WeaponSwitcher>();
        }

        if ((handPoint == null || IsWeaponTransform(handPoint)) && playerCamera != null)
        {
            GameObject handObject = new GameObject("Grenade Hand Point");
            handObject.transform.SetParent(playerCamera.transform, false);
            handObject.transform.localPosition = fallbackHandLocalPosition;
            handObject.transform.localRotation = Quaternion.identity;
            handPoint = handObject.transform;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(throwKey))
        {
            BeginHoldGrenade();
        }

        if (isHoldingGrenade)
        {
            UpdateHeldGrenadeAnimation();
        }

        if (Input.GetKeyUp(throwKey))
        {
            ReleaseGrenade();
        }
    }

    public void BeginHoldGrenade()
    {
        if (isHoldingGrenade || Time.time < nextThrowTime || grenadePrefab == null || playerCamera == null || handPoint == null)
        {
            return;
        }

        isHoldingGrenade = true;
        SetWeaponsVisible(false);
        ShowPreviewGrenade();
    }

    public void ReleaseGrenade()
    {
        if (!isHoldingGrenade)
        {
            return;
        }

        SpawnThrownGrenade();
        HidePreviewGrenade();
        RestoreLastWeapon();
        isHoldingGrenade = false;
        nextThrowTime = Time.time + throwCooldown;
    }

    private void ShowPreviewGrenade()
    {
        if (previewGrenade != null)
        {
            Destroy(previewGrenade);
        }

        previewGrenade = Instantiate(grenadePrefab, handPoint.position, handPoint.rotation, handPoint);
        previewGrenade.transform.localPosition = grenadeHiddenLocalPosition;
        previewGrenade.transform.localRotation = Quaternion.Euler(grenadeHiddenEuler);
        previewGrenade.transform.localScale = handPreviewScale;

        Rigidbody body = previewGrenade.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
        }

        Collider[] colliders = previewGrenade.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        GrenadeProjectile projectile = previewGrenade.GetComponent<GrenadeProjectile>();
        if (projectile != null)
        {
            projectile.enabled = false;
        }
    }

    private void UpdateHeldGrenadeAnimation()
    {
        if (previewGrenade == null)
        {
            return;
        }

        float lerp = equipAnimationSpeed * Time.deltaTime;
        previewGrenade.transform.localPosition = Vector3.Lerp(previewGrenade.transform.localPosition, grenadeReadyLocalPosition, lerp);
        previewGrenade.transform.localRotation = Quaternion.Slerp(previewGrenade.transform.localRotation, Quaternion.Euler(grenadeReadyEuler), lerp);
    }

    private void HidePreviewGrenade()
    {
        if (previewGrenade != null)
        {
            Destroy(previewGrenade);
            previewGrenade = null;
        }
    }

    private void SpawnThrownGrenade()
    {
        Vector3 spawnPosition = handPoint != null ? handPoint.position : playerCamera.transform.position + playerCamera.transform.forward * 0.65f;
        Quaternion spawnRotation = Quaternion.LookRotation(playerCamera.transform.forward, Vector3.up);
        GameObject grenade = Instantiate(grenadePrefab, spawnPosition, spawnRotation);

        Collider[] colliders = grenade.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = true;
        }

        Rigidbody body = grenade.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = grenade.AddComponent<Rigidbody>();
        }

        body.isKinematic = false;
        body.useGravity = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        GrenadeProjectile projectile = grenade.GetComponent<GrenadeProjectile>();
        if (projectile == null)
        {
            projectile = grenade.AddComponent<GrenadeProjectile>();
        }

        projectile.enabled = true;
        projectile.Initialize(gameObject, fuseTime, explosionDamage, explosionRadius, explosionForce, explosionFxPrefab);
        projectile.SetExplosionAudio(explosionClip, explosionVolume);
        projectile.SetHitFeedbackUi(hitFeedbackUi);

        body.linearVelocity = playerCamera.transform.forward.normalized * throwVelocity;
        body.AddTorque(Random.insideUnitSphere * spinTorque, ForceMode.VelocityChange);
    }

    private void SetWeaponsVisible(bool visible)
    {
        if (!hideWeaponsWhileHoldingGrenade)
        {
            return;
        }

        if (visible)
        {
            RestoreLastWeapon();
        }
        else
        {
            HideAllWeapons();
        }
    }

    private void HideAllWeapons()
    {
        if (weaponSwitcher != null)
        {
            weaponSwitcher.HideAllWeapons();
            return;
        }

        if (weaponsToHide == null)
        {
            return;
        }

        for (int i = 0; i < weaponsToHide.Length; i++)
        {
            if (weaponsToHide[i] != null)
            {
                weaponsToHide[i].gameObject.SetActive(false);
            }
        }
    }

    private void RestoreLastWeapon()
    {
        if (weaponSwitcher != null)
        {
            weaponSwitcher.RestoreLastWeapon();
            return;
        }

        if (weaponsToHide == null)
        {
            return;
        }

        for (int i = 0; i < weaponsToHide.Length; i++)
        {
            if (weaponsToHide[i] != null)
            {
                weaponsToHide[i].gameObject.SetActive(true);
            }
        }
    }

    private bool IsWeaponTransform(Transform candidate)
    {
        if (candidate == null || weaponsToHide == null)
        {
            return false;
        }

        for (int i = 0; i < weaponsToHide.Length; i++)
        {
            if (weaponsToHide[i] != null && candidate == weaponsToHide[i].transform)
            {
                return true;
            }
        }

        return false;
    }

    private void OnDisable()
    {
        if (isHoldingGrenade)
        {
            HidePreviewGrenade();
            SetWeaponsVisible(true);
            isHoldingGrenade = false;
        }
    }
}