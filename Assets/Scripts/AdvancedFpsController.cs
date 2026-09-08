using System.Collections;
using UnityEngine;

/// <summary>
/// Detailed FPS movement controller for a roguelike shooter / battle royale style character.
/// Attach this script to a Player GameObject that has a CharacterController component.
/// Optional: assign a child Camera to playerCamera for mouse look.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class AdvancedFpsController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Camera used for vertical look. If empty, the script tries to find a child Camera.")]
    [SerializeField] private Camera playerCamera;

    [Header("Look")]
    [SerializeField, Min(0.01f)] private float mouseSensitivity = 2.2f;
    [SerializeField, Range(40f, 89f)] private float maxLookAngle = 85f;
    [SerializeField] private bool lockCursorOnStart = true;
    [SerializeField] private bool allowEscapeToUnlockCursor = true;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 5.5f;
    [SerializeField, Min(0f)] private float sprintSpeed = 9f;
    [SerializeField, Min(0f)] private float crouchSpeed = 3f;
    [SerializeField, Min(0f)] private float slideSpeed = 13f;
    [SerializeField, Min(0f)] private float acceleration = 18f;
    [SerializeField, Min(0f)] private float airControl = 5f;

    [Header("Jump & Gravity")]
    [SerializeField, Min(0f)] private float jumpHeight = 1.35f;
    [SerializeField, Min(0f)] private float gravity = 24f;
    [SerializeField, Min(0f)] private float groundedStickForce = 4f;
    [SerializeField, Min(0f)] private float coyoteTime = 0.12f;
    [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;

    [Header("Crouch & Slide")]
    [SerializeField, Min(0.1f)] private float standingHeight = 1.8f;
    [SerializeField, Min(0.1f)] private float crouchingHeight = 1.0f;
    [SerializeField, Min(0.01f)] private float crouchTransitionSpeed = 12f;
    [SerializeField, Min(0.05f)] private float slideDuration = 0.75f;
    [SerializeField, Min(0.1f)] private float slideCooldown = 0.4f;
    [SerializeField, Min(0f)] private float minSlideStartSpeed = 6.5f;
    [SerializeField, Min(0f)] private float slideFriction = 10f;
    [SerializeField] private bool holdCrouchAfterSlide = true;
    [SerializeField] private bool blockStandUpWhenCeilingAbove = false;
    [SerializeField] private LayerMask ceilingCheckLayers = ~0;

    [Header("Key Bindings")]
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode slideKey = KeyCode.LeftControl;
    [SerializeField] private KeyCode crouchKey = KeyCode.C;
    [SerializeField] private bool alsoAcceptRightShiftForSprint = true;
    [SerializeField] private bool alsoAcceptRightControlForSlide = true;
    [SerializeField] private bool useSlideKeyAsCrouchHold = true;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField, Min(0.01f)] private float groundCheckDistance = 0.18f;

    [Header("Footstep Audio")]
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private AudioClip footstepClip;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.35f;
    [SerializeField, Min(0.05f)] private float walkStepInterval = 0.42f;
    [SerializeField, Min(0.05f)] private float sprintStepInterval = 0.30f;
    [SerializeField, Min(0.05f)] private float crouchStepInterval = 0.55f;
    [SerializeField, Min(0f)] private float footstepPitchRandomness = 0.08f;

    [Header("Damage Slow Effect")]
    [SerializeField, Min(0f)] private float damageSlowDuration = 0.25f;
    [SerializeField, Min(0f)] private float damageSlowMultiplier = 0.5f;
    [SerializeField] private bool enableDamageSlow = true;

    [Header("Debug")]
    [SerializeField] private bool logMovementState;

    public bool IsGrounded { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool IsSliding { get; private set; }
    public Vector3 CurrentVelocity => horizontalVelocity + Vector3.up * verticalVelocity;
    public bool MovementLocked { get; private set; }

    private CharacterController characterController;
    private Vector3 horizontalVelocity;
    private Vector3 slideDirection;
    private float verticalVelocity;
    private float cameraPitch;
    private float defaultCameraLocalY;
    private float lastGroundedTime;
    private float lastJumpPressedTime;
    private float slideTimer;
    private float lastSlideEndTime = -999f;
    private float nextFootstepTime;

    // Damage slow
    private Health playerHealth;
    private float originalWalkSpeed;
    private float originalSprintSpeed;
    private float originalCrouchSpeed;
    private Coroutine damageSlowRoutine;
    private float damageSlowTimer;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        standingHeight = Mathf.Max(standingHeight, crouchingHeight + 0.05f);
        characterController.height = standingHeight;
        characterController.center = new Vector3(0f, standingHeight * 0.5f, 0f);

        if (playerCamera != null)
        {
            defaultCameraLocalY = playerCamera.transform.localPosition.y;
        }

        // Cache original speed values
        originalWalkSpeed = walkSpeed;
        originalSprintSpeed = sprintSpeed;
        originalCrouchSpeed = crouchSpeed;

        // Subscribe to damage events
        playerHealth = GetComponent<Health>();
        if (playerHealth != null)
        {
            playerHealth.OnDamaged.AddListener(OnPlayerDamaged);
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDamaged.RemoveListener(OnPlayerDamaged);
        }
    }

    private void Start()
    {
        if (lockCursorOnStart)
        {
            LockCursor(true);
        }
    }

    private void Update()
    {
        HandleCursorToggle();
        HandleLook();

        if (MovementLocked)
        {
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            IsSprinting = false;
            IsCrouching = false;
            IsSliding = false;
            return;
        }

        UpdateGroundedState();
        CacheJumpInput();
        HandleStanceInput();
        HandleMovement();
        UpdateFootstepAudio();
        ApplyHeightAndCamera();

        if (logMovementState)
        {
            Debug.Log($"Velocity: {CurrentVelocity}, Grounded: {IsGrounded}, Sprinting: {IsSprinting}, Crouching: {IsCrouching}, Sliding: {IsSliding}");
        }
    }

    public void SetMovementLocked(bool locked)
    {
        MovementLocked = locked;

        if (!locked)
        {
            lastGroundedTime = Time.time;
            return;
        }

        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        IsSprinting = false;
        IsCrouching = false;
        IsSliding = false;
    }

    private void HandleCursorToggle()
    {
        if (!allowEscapeToUnlockCursor)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            LockCursor(Cursor.lockState != CursorLockMode.Locked);
        }
    }

    private void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        if (playerCamera == null)
        {
            return;
        }

        cameraPitch = Mathf.Clamp(cameraPitch - mouseY, -maxLookAngle, maxLookAngle);
        playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void UpdateGroundedState()
    {
        bool controllerGrounded = characterController.isGrounded;
        bool sphereGrounded = Physics.CheckSphere(GetGroundCheckPosition(), groundCheckDistance, groundLayers, QueryTriggerInteraction.Ignore);

        IsGrounded = controllerGrounded || sphereGrounded;
        if (IsGrounded)
        {
            lastGroundedTime = Time.time;

            if (verticalVelocity < 0f)
            {
                verticalVelocity = -groundedStickForce;
            }
        }
    }

    private Vector3 GetGroundCheckPosition()
    {
        return transform.position + characterController.center + Vector3.down * ((characterController.height * 0.5f) + 0.02f);
    }

    private void CacheJumpInput()
    {
        if (Input.GetKeyDown(jumpKey))
        {
            lastJumpPressedTime = Time.time;
        }
    }

    private void HandleStanceInput()
    {
        bool wantsSprint = IsSprintHeld();
        bool wantsCrouch = IsCrouchHeld();
        bool pressedSlide = WasSlidePressedThisFrame();

        if (pressedSlide && CanStartSlide())
        {
            StartSlide();
        }

        if (IsSliding)
        {
            IsCrouching = true;
            slideTimer -= Time.deltaTime;

            if (slideTimer <= 0f || horizontalVelocity.magnitude < crouchSpeed + 0.5f)
            {
                StopSlide();
            }
        }
        else
        {
            IsCrouching = wantsCrouch;

            if (!IsCrouching && !CanStandUp())
            {
                IsCrouching = true;
            }
        }

        // IsSprinting shows the button/state intent immediately when Shift is held.
        // Movement speed still only changes while there is movement input in HandleMovement().
        IsSprinting = wantsSprint && !IsCrouching && !IsSliding;
    }

    private bool IsSprintHeld()
    {
        return Input.GetKey(sprintKey)
            || Input.GetKey(KeyCode.LeftShift)
            || (alsoAcceptRightShiftForSprint && Input.GetKey(KeyCode.RightShift));
    }

    private bool IsCrouchHeld()
    {
        bool crouchHeld = Input.GetKey(crouchKey) || Input.GetKey(KeyCode.C);
        bool slideHeld = Input.GetKey(slideKey)
            || Input.GetKey(KeyCode.LeftControl)
            || (alsoAcceptRightControlForSlide && Input.GetKey(KeyCode.RightControl));
        return crouchHeld || (useSlideKeyAsCrouchHold && holdCrouchAfterSlide && slideHeld);
    }

    private bool WasSlidePressedThisFrame()
    {
        return Input.GetKeyDown(slideKey)
            || Input.GetKeyDown(KeyCode.LeftControl)
            || (alsoAcceptRightControlForSlide && Input.GetKeyDown(KeyCode.RightControl));
    }

    private bool CanStartSlide()
    {
        if (!IsGrounded || IsSliding || Time.time < lastSlideEndTime + slideCooldown)
        {
            return false;
        }

        float currentFlatSpeed = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z).magnitude;
        Vector2 moveInput = GetMoveInput();
        bool hasEnoughIntent = IsSprintHeld() || currentFlatSpeed >= minSlideStartSpeed || horizontalVelocity.magnitude >= minSlideStartSpeed;
        return hasEnoughIntent && moveInput.sqrMagnitude > 0.01f;
    }

    private void StartSlide()
    {
        IsSliding = true;
        IsCrouching = true;
        slideTimer = slideDuration;

        Vector3 inputDirection = GetWorldMoveDirection(GetMoveInput());
        slideDirection = inputDirection.sqrMagnitude > 0.01f ? inputDirection.normalized : transform.forward;
        horizontalVelocity = slideDirection * Mathf.Max(slideSpeed, horizontalVelocity.magnitude);
    }

    private void StopSlide()
    {
        IsSliding = false;
        lastSlideEndTime = Time.time;
    }

    private bool CanStandUp()
    {
        if (!blockStandUpWhenCeilingAbove)
        {
            return true;
        }

        float radius = Mathf.Max(0.05f, characterController.radius * 0.95f);
        Vector3 bottom = transform.position + Vector3.up * (crouchingHeight + radius);
        Vector3 top = transform.position + Vector3.up * (standingHeight - radius);

        Collider[] hits = Physics.OverlapCapsule(bottom, top, radius, ceilingCheckLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            if (hits[i].transform == transform || hits[i].transform.IsChildOf(transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void OnPlayerDamaged(DamageInfo damageInfo)
    {
        if (!enableDamageSlow)
        {
            return;
        }

        // Start or restart the damage slow coroutine
        if (damageSlowRoutine != null)
        {
            StopCoroutine(damageSlowRoutine);
        }

        damageSlowRoutine = StartCoroutine(DamageSlowRoutine());
    }

    private IEnumerator DamageSlowRoutine()
    {
        damageSlowTimer = damageSlowDuration;

        // Apply slow multiplier
        walkSpeed = originalWalkSpeed * damageSlowMultiplier;
        sprintSpeed = originalSprintSpeed * damageSlowMultiplier;
        crouchSpeed = originalCrouchSpeed * damageSlowMultiplier;

        // Wait for duration
        while (damageSlowTimer > 0f)
        {
            damageSlowTimer -= Time.deltaTime;
            yield return null;
        }

        // Restore original speeds
        walkSpeed = originalWalkSpeed;
        sprintSpeed = originalSprintSpeed;
        crouchSpeed = originalCrouchSpeed;

        damageSlowRoutine = null;
    }

    private void HandleMovement()
    {
        Vector2 moveInput = GetMoveInput();
        Vector3 desiredDirection = GetWorldMoveDirection(moveInput);

        if (IsSliding)
        {
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, slideDirection * crouchSpeed, slideFriction * Time.deltaTime);
        }
        else
        {
            float targetSpeed = IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
            Vector3 targetVelocity = desiredDirection * targetSpeed;
            float control = IsGrounded ? acceleration : airControl;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, control * Time.deltaTime);
        }

        bool canUseBufferedJump = Time.time <= lastJumpPressedTime + jumpBufferTime;
        bool canUseCoyoteJump = Time.time <= lastGroundedTime + coyoteTime;

        if (canUseBufferedJump && canUseCoyoteJump && !IsSliding)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * 2f * gravity);
            lastJumpPressedTime = -999f;
            lastGroundedTime = -999f;
            IsGrounded = false;
        }

        verticalVelocity -= gravity * Time.deltaTime;

        Vector3 finalVelocity = horizontalVelocity + Vector3.up * verticalVelocity;
        characterController.Move(finalVelocity * Time.deltaTime);
    }

    private void UpdateFootstepAudio()
    {
        if (footstepAudioSource == null || footstepClip == null || !IsGrounded || IsSliding)
        {
            return;
        }

        Vector3 flatVelocity = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z);
        if (flatVelocity.magnitude < 0.35f || GetMoveInput().sqrMagnitude < 0.01f)
        {
            return;
        }

        if (Time.time < nextFootstepTime)
        {
            return;
        }

        float interval = IsCrouching ? crouchStepInterval : IsSprinting ? sprintStepInterval : walkStepInterval;
        nextFootstepTime = Time.time + interval;
        footstepAudioSource.pitch = 1f + Random.Range(-footstepPitchRandomness, footstepPitchRandomness);
        footstepAudioSource.PlayOneShot(footstepClip, footstepVolume);
    }

    private Vector2 GetMoveInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector2 input = new Vector2(horizontal, vertical);
        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    private Vector3 GetWorldMoveDirection(Vector2 input)
    {
        Vector3 direction = transform.right * input.x + transform.forward * input.y;
        direction.y = 0f;
        return direction.sqrMagnitude > 1f ? direction.normalized : direction;
    }

    private void ApplyHeightAndCamera()
    {
        float targetHeight = IsCrouching || IsSliding ? crouchingHeight : standingHeight;
        characterController.height = Mathf.Lerp(characterController.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
        characterController.center = Vector3.up * (characterController.height * 0.5f);

        if (playerCamera == null)
        {
            return;
        }

        float targetCameraY = IsCrouching || IsSliding ? defaultCameraLocalY - (standingHeight - crouchingHeight) : defaultCameraLocalY;
        Vector3 cameraLocalPosition = playerCamera.transform.localPosition;
        cameraLocalPosition.y = Mathf.Lerp(cameraLocalPosition.y, targetCameraY, crouchTransitionSpeed * Time.deltaTime);
        playerCamera.transform.localPosition = cameraLocalPosition;
    }

    private void OnDrawGizmosSelected()
    {
        CharacterController controller = characterController != null ? characterController : GetComponent<CharacterController>();
        if (controller == null)
        {
            return;
        }

        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Vector3 groundCheckPosition = transform.position + controller.center + Vector3.down * ((controller.height * 0.5f) + 0.02f);
        Gizmos.DrawWireSphere(groundCheckPosition, groundCheckDistance);
    }
}