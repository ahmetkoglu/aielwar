using UnityEngine;
using TMPro;

/// <summary>
/// Handles a battle-royale style player drop: start high in the sky, open a visible parachute with a key,
/// steer while falling, show a lightweight wind streak effect, and land softly before returning control.
/// Call BeginDrop() from a UI Start button.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerParachuteDrop : MonoBehaviour
{
    private enum DropState
    {
        Idle,
        Freefall,
        Parachuting,
        Landing
    }

    [Header("References")]
    [SerializeField] private AdvancedFpsController fpsController;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private GameObject parachutePrefab;
    [SerializeField] private GameObject startUiToHide;

    [Header("Start")]
    [SerializeField] private bool beginDropOnStart;
    [SerializeField, Min(5f)] private float dropHeight = 85f;
    [SerializeField] private Vector3 horizontalDropOffset = Vector3.zero;

    [Header("Parachute Prompt UI")]
    [SerializeField] private TextMeshProUGUI parachutePromptText;
    [SerializeField] private string promptMessage = "PARAŞÜT AÇMAK İÇİN\n[F] BASINIZ";
    [SerializeField, Min(5f)] private float promptShowHeight = 60f;

    [Header("Input")]
    [SerializeField] private KeyCode openParachuteKey = KeyCode.F;
    [SerializeField] private bool allowJumpKeyToOpenParachute = true;

    [Header("Freefall")]
    [SerializeField, Min(1f)] private float freefallDescentSpeed = 28f;
    [SerializeField, Min(0f)] private float freefallSteerSpeed = 9f;
    [SerializeField, Min(0f)] private float freefallAcceleration = 7f;
    [SerializeField, Min(0f)] private float autoOpenHeight = 18f;

    [Header("Parachute Flight")]
    [SerializeField, Min(0.5f)] private float parachuteDescentSpeed = 5.5f;
    [SerializeField, Min(0.5f)] private float landingDescentSpeed = 1.75f;
    [SerializeField, Min(0f)] private float parachuteSteerSpeed = 7f;
    [SerializeField, Min(0f)] private float parachuteAcceleration = 4f;
    [SerializeField, Min(0f)] private float windDriftStrength = 1.35f;
    [SerializeField] private Vector3 windDirection = new Vector3(1f, 0f, 0.35f);
    [SerializeField, Min(0.5f)] private float softLandingHeight = 5f;
    [SerializeField, Min(0.05f)] private float groundCheckDistance = 2.1f;
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Parachute Visual")]
    [SerializeField] private Vector3 parachuteLocalPosition = new Vector3(0f, 2.85f, -0.35f);
    [SerializeField] private Vector3 parachuteLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 parachuteLocalScale = new Vector3(0.45f, 0.45f, 0.45f);
    [SerializeField, Min(0f)] private float parachuteSwayAngle = 9f;
    [SerializeField, Min(0.01f)] private float parachuteSwaySpeed = 1.7f;
    [SerializeField, Min(0.01f)] private float parachuteOpenScaleTime = 0.35f;

    [Header("Wind Streak Effect")]
    [SerializeField] private bool createWindEffect = true;
    [SerializeField, Min(0f)] private float freefallWindEmission = 115f;
    [SerializeField, Min(0f)] private float parachuteWindEmission = 35f;

    [Header("Audio")]
    [SerializeField] private AudioSource windAudioSource;
    [SerializeField] private AudioSource parachuteAudioSource;
    [SerializeField] private AudioClip freefallWindLoop;
    [SerializeField] private AudioClip parachuteOpenClip;
    [SerializeField, Range(0f, 1f)] private float freefallWindVolume = 0.42f;
    [SerializeField, Range(0f, 1f)] private float parachuteWindVolume = 0.18f;
    [SerializeField, Range(0f, 1f)] private float parachuteOpenVolume = 0.75f;

    private CharacterController characterController;
    private GameObject parachuteInstance;
    private ParticleSystem windParticles;
    private DropState state = DropState.Idle;
    private Vector3 horizontalVelocity;
    private float parachuteOpenTime;
    private float swaySeed;

    public bool IsDropping => state != DropState.Idle;
    public bool IsParachuteOpen => state == DropState.Parachuting || state == DropState.Landing;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (fpsController == null)
        {
            fpsController = GetComponent<AdvancedFpsController>();
        }

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        swaySeed = Random.Range(0f, 100f);
    }

    private void Start()
    {
        if (createWindEffect)
        {
            CreateWindEffectIfNeeded();
            SetWindEmission(0f);
        }

        if (beginDropOnStart)
        {
            BeginDrop();
        }
    }

    private void Update()
    {
        if (state == DropState.Idle)
        {
            return;
        }

        if (state == DropState.Freefall && WasOpenParachutePressed())
        {
            OpenParachute();
        }

        UpdateDropMovement();
        UpdateParachuteVisual();
        UpdateWindEffect();
    }

    public void BeginDrop()
    {
        Vector3 startPosition = transform.position + horizontalDropOffset + Vector3.up * dropHeight;
        characterController.enabled = false;
        transform.position = startPosition;
        characterController.enabled = true;

        horizontalVelocity = Vector3.zero;
        state = DropState.Freefall;
        fpsController?.SetMovementLocked(true);
        HideParachute();
        SetWindEmission(freefallWindEmission);
        StartWindAudio(freefallWindVolume);
        CreatePromptTextIfNeeded();
        SetPromptVisible(false);

        if (startUiToHide != null)
        {
            startUiToHide.SetActive(false);
        }
    }

    public void OpenParachute()
    {
        if (state != DropState.Freefall)
        {
            return;
        }

        state = DropState.Parachuting;
        parachuteOpenTime = Time.time;
        SpawnParachuteIfNeeded();
        SetWindEmission(parachuteWindEmission);
        SetWindAudioVolume(parachuteWindVolume);
        PlayParachuteOpenSound();
        SetPromptVisible(false);
    }

    private void UpdateDropMovement()
    {
        float groundDistance = GetGroundDistance();
        if (state == DropState.Freefall && autoOpenHeight > 0f && groundDistance <= autoOpenHeight)
        {
            OpenParachute();
        }

        if (state == DropState.Parachuting && groundDistance <= softLandingHeight)
        {
            state = DropState.Landing;
        }

        Vector2 moveInput = GetMoveInput();
        Vector3 desiredDirection = GetWorldDirection(moveInput);
        float targetSteerSpeed = state == DropState.Freefall ? freefallSteerSpeed : parachuteSteerSpeed;
        float acceleration = state == DropState.Freefall ? freefallAcceleration : parachuteAcceleration;

        Vector3 targetHorizontalVelocity = desiredDirection * targetSteerSpeed;
        if (IsParachuteOpen && windDirection.sqrMagnitude > 0.01f)
        {
            targetHorizontalVelocity += windDirection.normalized * windDriftStrength;
        }

        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetHorizontalVelocity, acceleration * Time.deltaTime);

        float descentSpeed = freefallDescentSpeed;
        if (state == DropState.Parachuting)
        {
            descentSpeed = parachuteDescentSpeed;
        }
        else if (state == DropState.Landing)
        {
            float landingT = Mathf.Clamp01(groundDistance / Mathf.Max(0.01f, softLandingHeight));
            descentSpeed = Mathf.Lerp(landingDescentSpeed, parachuteDescentSpeed, landingT);
        }

        Vector3 velocity = horizontalVelocity + Vector3.down * descentSpeed;
        CollisionFlags flags = characterController.Move(velocity * Time.deltaTime);

        UpdatePromptText(groundDistance);

        if ((flags & CollisionFlags.Below) != 0 || groundDistance <= 0.25f)
        {
            FinishLanding();
        }
    }

    private void FinishLanding()
    {
        if (state == DropState.Idle)
        {
            return;
        }

        state = DropState.Idle;
        horizontalVelocity = Vector3.zero;
        fpsController?.SetMovementLocked(false);
        SetWindEmission(0f);
        StopWindAudio();

        if (parachuteInstance != null)
        {
            Destroy(parachuteInstance, 0.65f);
            parachuteInstance = null;
        }

        SetPromptVisible(false);
    }

    private bool WasOpenParachutePressed()
    {
        return Input.GetKeyDown(openParachuteKey) || (allowJumpKeyToOpenParachute && Input.GetKeyDown(KeyCode.Space));
    }

    private Vector2 GetMoveInput()
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    private Vector3 GetWorldDirection(Vector2 input)
    {
        Transform reference = playerCamera != null ? playerCamera.transform : transform;
        Vector3 forward = reference.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = reference.right;
        right.y = 0f;
        right.Normalize();

        Vector3 direction = right * input.x + forward * input.y;
        return direction.sqrMagnitude > 1f ? direction.normalized : direction;
    }

    private float GetGroundDistance()
    {
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1000f, groundLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.distance;
        }

        return Mathf.Infinity;
    }

    private void SpawnParachuteIfNeeded()
    {
        if (parachuteInstance != null || parachutePrefab == null)
        {
            if (parachuteInstance != null)
            {
                parachuteInstance.SetActive(true);
            }
            return;
        }

        parachuteInstance = Instantiate(parachutePrefab, transform);
        parachuteInstance.transform.localPosition = parachuteLocalPosition;
        parachuteInstance.transform.localRotation = Quaternion.Euler(parachuteLocalEuler);
        parachuteInstance.transform.localScale = Vector3.zero;
    }

    private void HideParachute()
    {
        if (parachuteInstance != null)
        {
            parachuteInstance.SetActive(false);
        }
    }

    private void UpdateParachuteVisual()
    {
        if (parachuteInstance == null || !IsParachuteOpen)
        {
            return;
        }

        float openT = Mathf.Clamp01((Time.time - parachuteOpenTime) / parachuteOpenScaleTime);
        openT = Mathf.SmoothStep(0f, 1f, openT);

        float swayTime = (Time.time + swaySeed) * parachuteSwaySpeed;
        float pitchSway = Mathf.Sin(swayTime * 0.7f) * parachuteSwayAngle * 0.35f;
        float rollSway = Mathf.Sin(swayTime) * parachuteSwayAngle;
        float steerRoll = Mathf.Clamp(-horizontalVelocity.x, -1f, 1f) * parachuteSwayAngle * 0.45f;

        parachuteInstance.transform.localPosition = parachuteLocalPosition + new Vector3(
            Mathf.Sin(swayTime * 0.55f) * 0.08f,
            Mathf.Sin(swayTime * 0.8f) * 0.05f,
            0f
        );
        parachuteInstance.transform.localRotation = Quaternion.Euler(parachuteLocalEuler + new Vector3(pitchSway, 0f, rollSway + steerRoll));
        parachuteInstance.transform.localScale = parachuteLocalScale * openT;
    }

    private void CreateWindEffectIfNeeded()
    {
        if (windParticles != null || playerCamera == null)
        {
            return;
        }

        GameObject windObject = new GameObject("Parachute Wind Streaks");
        windObject.transform.SetParent(playerCamera.transform, false);
        windObject.transform.localPosition = new Vector3(0f, 0f, 0.65f);
        windObject.transform.localRotation = Quaternion.identity;

        windParticles = windObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = windParticles.main;
        main.loop = true;
        main.startLifetime = 0.35f;
        main.startSpeed = 8f;
        main.startSize = 0.025f;
        main.startColor = new Color(1f, 1f, 1f, 0.32f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = windParticles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = windParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(3.2f, 1.8f, 0.05f);

        ParticleSystem.VelocityOverLifetimeModule velocity = windParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.z = new ParticleSystem.MinMaxCurve(-13f, -7f);

        ParticleSystemRenderer renderer = windParticles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 4f;
        renderer.velocityScale = 0.45f;

        windParticles.Play(true);
    }

    private void UpdateWindEffect()
    {
        if (!createWindEffect)
        {
            return;
        }

        CreateWindEffectIfNeeded();

        if (state == DropState.Freefall)
        {
            SetWindEmission(freefallWindEmission);
        }
        else if (IsParachuteOpen)
        {
            SetWindEmission(parachuteWindEmission);
        }
    }

    private void SetWindEmission(float rate)
    {
        if (windParticles == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = windParticles.emission;
        emission.rateOverTime = rate;
    }

    private void StartWindAudio(float volume)
    {
        if (windAudioSource == null || freefallWindLoop == null)
        {
            return;
        }

        windAudioSource.clip = freefallWindLoop;
        windAudioSource.loop = true;
        windAudioSource.spatialBlend = 0f;
        windAudioSource.volume = volume;
        if (!windAudioSource.isPlaying)
        {
            windAudioSource.Play();
        }
    }

    private void SetWindAudioVolume(float volume)
    {
        if (windAudioSource != null)
        {
            windAudioSource.volume = volume;
        }
    }

    private void StopWindAudio()
    {
        if (windAudioSource != null)
        {
            windAudioSource.Stop();
        }
    }

    private void PlayParachuteOpenSound()
    {
        if (parachuteAudioSource != null && parachuteOpenClip != null)
        {
            parachuteAudioSource.PlayOneShot(parachuteOpenClip, parachuteOpenVolume);
        }
    }

    private void CreatePromptTextIfNeeded()
    {
        if (parachutePromptText != null)
        {
            return;
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        GameObject promptObj = new GameObject("Parachute Prompt");
        promptObj.transform.SetParent(canvas.transform, false);

        RectTransform rect = promptObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(400f, 120f);

        parachutePromptText = promptObj.AddComponent<TextMeshProUGUI>();
        parachutePromptText.text = promptMessage;
        parachutePromptText.color = Color.white;
        parachutePromptText.alignment = TextAlignmentOptions.Center;
        parachutePromptText.fontSize = 28f;
        parachutePromptText.fontStyle = FontStyles.Bold;
        parachutePromptText.enableAutoSizing = false;
    }

    private void UpdatePromptText(float groundDistance)
    {
        if (parachutePromptText == null || state != DropState.Freefall)
        {
            return;
        }

        bool shouldShow = groundDistance <= promptShowHeight && groundDistance > autoOpenHeight;
        SetPromptVisible(shouldShow);
    }

    private void SetPromptVisible(bool visible)
    {
        if (parachutePromptText != null)
        {
            parachutePromptText.gameObject.SetActive(visible);
        }
    }
}
