using System.Collections;
using UnityEngine;

/// <summary>
/// Simple main/landing page controller for the demo scene.
/// Shows a top-down island camera and menu UI until START is pressed, then starts the parachute drop.
/// Disables all enemies and wave spawner while on the menu.
/// </summary>
public class LandingPageController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera menuCamera;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AdvancedFpsController playerController;
    [SerializeField] private PlayerParachuteDrop parachuteDrop;
    [SerializeField] private GameObject landingPageRoot;
    [SerializeField] private GameObject gameplayHudRoot;
    [SerializeField] private RectTransform menuCursorDot;

    [Header("Menu Camera Motion")]
    [SerializeField] private bool animateMenuCamera = true;
    [SerializeField, Min(0f)] private float orbitRadius = 8f;
    [SerializeField, Min(0f)] private float orbitSpeed = 4f;
    [SerializeField, Min(0f)] private float bobAmount = 1.5f;
    [SerializeField, Min(0f)] private float bobSpeed = 0.45f;

    [Header("Start Transition")]
    [SerializeField, Min(0f)] private float startDelay = 0.15f;

    private Vector3 menuCameraStartPosition;
    private Quaternion menuCameraStartRotation;
    private FpsWeaponController[] playerWeapons;
    private PlayerGrenadeThrower[] grenadeThrowers;
    private EnemyChaseAttack[] allEnemies;
    private WaveSpawner waveSpawner;
    private bool gameStarted;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = FindObjectOfType<AdvancedFpsController>();
        }

        if (parachuteDrop == null && playerController != null)
        {
            parachuteDrop = playerController.GetComponent<PlayerParachuteDrop>();
        }

        if (playerCamera == null && playerController != null)
        {
            playerCamera = playerController.GetComponentInChildren<Camera>(true);
        }

        if (playerController != null)
        {
            playerWeapons = playerController.GetComponentsInChildren<FpsWeaponController>(true);
            grenadeThrowers = playerController.GetComponentsInChildren<PlayerGrenadeThrower>(true);
        }

        allEnemies = FindObjectsByType<EnemyChaseAttack>(FindObjectsSortMode.None);
        waveSpawner = FindObjectOfType<WaveSpawner>();

        if (menuCamera != null)
        {
            menuCameraStartPosition = menuCamera.transform.position;
            menuCameraStartRotation = menuCamera.transform.rotation;
        }

        ConfigureLandingPageRaycasts();
    }

    private void Start()
    {
        ShowLandingPage();
    }

    private void Update()
    {
        if (!gameStarted && animateMenuCamera && menuCamera != null)
        {
            AnimateMenuCamera();
        }

        if (!gameStarted)
        {
            ForceMenuCursorState();
            UpdateMenuCursorDot();
        }
    }

    public void StartGame()
    {
        if (gameStarted)
        {
            return;
        }

        gameStarted = true;
        StartCoroutine(StartGameRoutine());
    }

    public void ExitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void ShowLandingPage()
    {
        gameStarted = false;
        playerController?.SetMovementLocked(true);
        SetWeaponsEnabled(false);
        SetGrenadeThrowersEnabled(false);
        SetAllEnemiesEnabled(false);

        if (landingPageRoot != null)
        {
            landingPageRoot.SetActive(true);
        }

        if (gameplayHudRoot != null)
        {
            gameplayHudRoot.SetActive(false);
        }

        if (menuCamera != null)
        {
            menuCamera.gameObject.SetActive(true);
            menuCamera.enabled = true;
        }

        if (playerCamera != null)
        {
            playerCamera.enabled = false;
        }

        ForceMenuCursorState();
    }

    private IEnumerator StartGameRoutine()
    {
        if (landingPageRoot != null)
        {
            landingPageRoot.SetActive(false);
        }

        if (gameplayHudRoot != null)
        {
            gameplayHudRoot.SetActive(true);
        }

        yield return new WaitForSeconds(startDelay);

        if (menuCamera != null)
        {
            menuCamera.enabled = false;
            menuCamera.gameObject.SetActive(false);
        }

        if (playerCamera != null)
        {
            playerCamera.enabled = true;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SetWeaponsEnabled(true);
        SetGrenadeThrowersEnabled(true);
        SetAllEnemiesEnabled(true);

        if (parachuteDrop != null)
        {
            parachuteDrop.BeginDrop();
        }
        else
        {
            playerController?.SetMovementLocked(false);
        }
    }

    private void AnimateMenuCamera()
    {
        float time = Time.time;
        Vector3 orbit = new Vector3(
            Mathf.Sin(time * orbitSpeed * Mathf.Deg2Rad) * orbitRadius,
            Mathf.Sin(time * bobSpeed) * bobAmount,
            Mathf.Cos(time * orbitSpeed * Mathf.Deg2Rad) * orbitRadius
        );

        menuCamera.transform.position = menuCameraStartPosition + orbit;
        menuCamera.transform.rotation = menuCameraStartRotation;
    }

    private void SetWeaponsEnabled(bool enabled)
    {
        if (playerWeapons == null)
        {
            return;
        }

        for (int i = 0; i < playerWeapons.Length; i++)
        {
            if (playerWeapons[i] != null)
            {
                playerWeapons[i].enabled = enabled;
            }
        }
    }

    private void SetGrenadeThrowersEnabled(bool enabled)
    {
        if (grenadeThrowers == null)
        {
            return;
        }

        for (int i = 0; i < grenadeThrowers.Length; i++)
        {
            if (grenadeThrowers[i] != null)
            {
                grenadeThrowers[i].enabled = enabled;
            }
        }
    }

    private void SetAllEnemiesEnabled(bool enabled)
    {
        if (allEnemies != null)
        {
            for (int i = 0; i < allEnemies.Length; i++)
            {
                if (allEnemies[i] != null)
                {
                    allEnemies[i].enabled = enabled;
                }
            }
        }

        if (waveSpawner != null)
        {
            waveSpawner.enabled = enabled;
        }
    }

    private void ForceMenuCursorState()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void UpdateMenuCursorDot()
    {
        if (menuCursorDot == null)
        {
            return;
        }

        menuCursorDot.position = Input.mousePosition;
    }

    private void ConfigureLandingPageRaycasts()
    {
        if (landingPageRoot == null)
        {
            return;
        }

        UnityEngine.UI.Image backgroundImage = landingPageRoot.GetComponent<UnityEngine.UI.Image>();
        if (backgroundImage != null)
        {
            backgroundImage.raycastTarget = false;
        }
    }
}