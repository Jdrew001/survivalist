using UnityEngine;
using System.Collections;
using Assets.Game.Inventory;
using Assets.Game.Inventory.UI;

public class RealisticPlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerCamera;

    [Header("Movement Settings")]
    [SerializeField] private float maxSpeed = 6f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float deceleration = 10f;
    [SerializeField] private float turnSpeed = 8f;
    [SerializeField] private float sprintMultiplier = 1.5f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float jumpCooldown = 0.1f;
    [SerializeField] private int maxAirJumps = 0;
    [SerializeField] private float gravity = 20f;
    [SerializeField] private float airControl = 0.3f;
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float fallMultiplier = 1.5f;

    [Header("Crouch Settings")]
    [SerializeField] private float crouchHeight = 0.5f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float crouchTransitionSpeed = 10f;
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float headCheckDistance = 0.1f;
    [Tooltip("Crouch momentum jump bonus")]
    [SerializeField] private float crouchJumpBoost = 1.5f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private float slopeLimit = 45f;

    [Header("Sound & Feedback")]
    [SerializeField] private float footstepInterval = 0.5f;
    [SerializeField] private AudioClip[] footstepSounds;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip landSound;

    // Internal variables
    private CharacterController controller;
    private Transform cameraTransform;
    private Transform cameraHolder;
    private AudioSource audioSource;
    private Vector3 moveDirection = Vector3.zero;
    private Vector3 velocity = Vector3.zero;
    private float verticalVelocity = 0f;
    private float lastGroundedTime = 0f;
    private float lastJumpPressedTime = 0f;
    private float nextJumpTime = 0f;
    private float footstepTimer = 0f;
    private float currentHeight;
    private float targetHeight;
    private int airJumpsRemaining;
    private bool isJumping = false;
    private bool wasGroundedLastFrame = false;
    private bool isCrouching = false;
    private bool isSprinting = false;
    private Vector3 slopeNormal;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // Use directly assigned camera if available
        if (playerCamera != null)
        {
            cameraTransform = playerCamera;
            Debug.Log("Using directly assigned camera reference.");
        }
        // Otherwise try to find camera using standard methods
        else if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
            Debug.Log("Using Camera.main reference.");
        }
        else
        {
            // Try to find any camera in the scene if Main Camera tag isn't set
            Camera cam = FindObjectOfType<Camera>();
            if (cam != null)
            {
                cameraTransform = cam.transform;
                Debug.LogWarning("Main Camera not found. Using first camera found in scene.");
            }
            else
            {
                Debug.LogError("No camera found in the scene! Player movement will not work correctly.");
            }
        }

        audioSource = GetComponent<AudioSource>();

        // If no audio source, add one
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // 3D sound
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.maxDistance = 20f;
        }

        // Find or create camera holder - only if we found a camera
        if (cameraTransform != null)
        {
            if (cameraTransform.parent != null && cameraTransform.parent.parent == transform)
            {
                cameraHolder = cameraTransform.parent;
                Debug.Log("Camera holder found: " + cameraHolder.name);
            }
            else
            {
                Debug.LogWarning("Camera setup not optimal for crouch. Modify camera relationship hierarchy if needed.");
            }
        }

        // Set initial heights
        currentHeight = standingHeight;
        targetHeight = standingHeight;

        // Set controller height
        controller.height = standingHeight;

        // Lock cursor for first-person control
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Get input
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        bool jumpPressed = Input.GetButtonDown("Jump");
        bool crouchHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
        isSprinting = Input.GetKey(KeyCode.LeftShift) && !isCrouching;

        // Check if grounded and update timer
        bool isGrounded = IsGrounded(out slopeNormal);
        if (isGrounded)
        {
            lastGroundedTime = Time.time;

            // Reset vertical velocity when grounded
            if (verticalVelocity < 0)
                verticalVelocity = -2f; // Small value to ensure grounding

            // Reset jump state and air jumps
            if (!isJumping)
                verticalVelocity = -2f;

            isJumping = false;
            airJumpsRemaining = maxAirJumps;

            // Play landing sound if we just landed
            if (!wasGroundedLastFrame && landSound != null && velocity.magnitude > 3f)
            {
                audioSource.PlayOneShot(landSound);
            }
        }

        wasGroundedLastFrame = isGrounded;

        // Process crouch input
        UpdateCrouch(crouchHeld, isGrounded);

        // Process jump input
        if (jumpPressed)
        {
            lastJumpPressedTime = Time.time;
        }

        // Handle jumping
        HandleJump(isGrounded);

        // Apply gravity with fall multiplier for more responsive falling
        if (verticalVelocity < 0)
        {
            verticalVelocity -= gravity * fallMultiplier * Time.deltaTime;
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        // Calculate movement direction relative to camera
        Vector3 inputDirection = new Vector3(horizontal, 0, vertical).normalized;
        Vector3 targetMoveDirection;

        // Make sure we have a valid camera reference
        if (cameraTransform != null)
        {
            Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward).normalized;

            targetMoveDirection = (cameraForward * inputDirection.z + cameraRight * inputDirection.x).normalized;
        }
        else
        {
            // Fallback to world directions if camera isn't available
            targetMoveDirection = new Vector3(inputDirection.x, 0, inputDirection.z).normalized;
            Debug.LogWarning("Camera not found, using world directions for movement");
        }

        // Calculate target speed based on crouch and sprint state
        float targetSpeed = maxSpeed;
        if (isCrouching)
        {
            targetSpeed = crouchSpeed;
        }
        else if (isSprinting && inputDirection.z > 0.5f) // Only sprint when moving forward
        {
            targetSpeed = maxSpeed * sprintMultiplier;
        }

        // Apply acceleration or deceleration based on input
        float currentControlModifier = isGrounded ? 1f : airControl;

        if (inputDirection.magnitude > 0.1f)
        {
            // Apply acceleration
            velocity = Vector3.Lerp(velocity, targetMoveDirection * targetSpeed, acceleration * currentControlModifier * Time.deltaTime);

            // Handle footstep sounds when moving on ground
            if (isGrounded && footstepSounds.Length > 0)
            {
                UpdateFootsteps(velocity.magnitude / maxSpeed);
            }
        }
        else
        {
            // Apply deceleration
            velocity = Vector3.Lerp(velocity, Vector3.zero, deceleration * currentControlModifier * Time.deltaTime);
        }

        // REMOVED: No longer handle rotation in movement script
        // Let MouseCameraController handle all rotation exclusively

        // Apply slope movement if on a slope
        if (isGrounded && Vector3.Angle(Vector3.up, slopeNormal) <= slopeLimit)
        {
            ApplySlopeMovement();
        }

        // Combine horizontal velocity with vertical velocity
        Vector3 finalVelocity = velocity;
        finalVelocity.y = verticalVelocity;

        // Move the character
        controller.Move(finalVelocity * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.I))
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OpenContainer(
                    InventoryManager.Instance.playerInventory);
            }
        }

        // Update camera position based on crouch
        UpdateCameraPosition();
    }

    public void SetMovementEnabled(bool enabled)
    {
        this.enabled = enabled;
    }

    private void HandleJump(bool isGrounded)
    {
        // Check coyote time and jump buffer for jump
        bool canJump = Time.time - lastGroundedTime <= coyoteTime && Time.time > nextJumpTime;
        bool shouldJump = Time.time - lastJumpPressedTime <= jumpBufferTime;

        if (canJump && shouldJump)
        {
            // Calculate jump force, add boost if crouched
            float actualJumpForce = jumpForce;
            if (isCrouching)
            {
                actualJumpForce *= crouchJumpBoost;

                // Stand up when jump-crouching if there's room
                if (CanStandUp())
                {
                    isCrouching = false;
                    targetHeight = standingHeight;
                }
            }

            verticalVelocity = actualJumpForce;
            isJumping = true;
            nextJumpTime = Time.time + jumpCooldown;
            lastJumpPressedTime = 0; // Reset to prevent double jumping

            // Play jump sound
            if (jumpSound != null)
            {
                audioSource.PlayOneShot(jumpSound);
            }
        }
        // Handle air jumps (double jump, triple jump, etc.)
        else if (!isGrounded && shouldJump && airJumpsRemaining > 0 && Time.time > nextJumpTime)
        {
            verticalVelocity = jumpForce * 0.8f; // Slightly reduced force for air jumps
            airJumpsRemaining--;
            nextJumpTime = Time.time + jumpCooldown;
            lastJumpPressedTime = 0;

            if (jumpSound != null)
            {
                audioSource.PlayOneShot(jumpSound, 0.7f);
            }
        }
    }

    private void UpdateCrouch(bool crouchInput, bool isGrounded)
    {
        if (crouchInput)
        {
            // Start crouching if not already
            if (!isCrouching)
            {
                isCrouching = true;
                targetHeight = crouchHeight;
            }
        }
        else if (isCrouching && CanStandUp())
        {
            // Stand up if there's room
            isCrouching = false;
            targetHeight = standingHeight;
        }

        // Smoothly adjust character controller height
        currentHeight = Mathf.Lerp(currentHeight, targetHeight, crouchTransitionSpeed * Time.deltaTime);
        controller.height = currentHeight;

        // Adjust center based on height
        controller.center = new Vector3(0, currentHeight / 2f, 0);
    }

    private bool CanStandUp()
    {
        // Check if there's enough room to stand up
        Ray ray = new Ray(transform.position + new Vector3(0, crouchHeight, 0), Vector3.up);
        return !Physics.Raycast(ray, standingHeight - crouchHeight + headCheckDistance, ~LayerMask.GetMask("Player"));
    }

    private void UpdateCameraPosition()
    {
        // Make sure both the camera holder and camera transform exist
        if (cameraHolder != null && cameraTransform != null)
        {
            // Calculate the camera offset based on crouch state
            float targetCameraY = currentHeight - 0.5f; // Head is 0.5 units from top
            Vector3 currentCameraPos = cameraHolder.localPosition;
            Vector3 targetCameraPos = new Vector3(currentCameraPos.x, targetCameraY, currentCameraPos.z);

            // Smoothly move camera position
            cameraHolder.localPosition = Vector3.Lerp(currentCameraPos, targetCameraPos, crouchTransitionSpeed * Time.deltaTime);
        }
        else if (cameraTransform != null && cameraTransform.parent == transform)
        {
            // Direct child camera fallback (not ideal but functional)
            float targetCameraY = currentHeight - 0.5f;
            Vector3 currentCameraPos = cameraTransform.localPosition;
            Vector3 targetCameraPos = new Vector3(currentCameraPos.x, targetCameraY, currentCameraPos.z);

            // Smoothly move camera position
            cameraTransform.localPosition = Vector3.Lerp(currentCameraPos, targetCameraPos, crouchTransitionSpeed * Time.deltaTime);
        }
    }

    private bool IsGrounded(out Vector3 normal)
    {
        normal = Vector3.up;
        if (verticalVelocity > 0.1f)
        {
            return false;
        }

        // Use a slightly larger radius than the character controller
        float radius = controller.radius + 0.05f;
        Vector3 spherePosition = transform.position + Vector3.up * (radius - 0.1f);

        // Check for ground with a larger sphere
        if (Physics.SphereCast(spherePosition, radius, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayers))
        {
            normal = hit.normal;
            return true;
        }

        return false;
    }

    private void ApplySlopeMovement()
    {
        // Project movement onto the slope
        Vector3 slopeMovement = Vector3.ProjectOnPlane(velocity, slopeNormal);
        velocity = slopeMovement;
    }

    private void UpdateFootsteps(float speedRatio)
    {
        // Update footstep timer based on movement speed
        footstepTimer -= Time.deltaTime * speedRatio;

        if (footstepTimer <= 0)
        {
            // Play random footstep sound
            if (footstepSounds.Length > 0)
            {
                AudioClip footstep = footstepSounds[Random.Range(0, footstepSounds.Length)];
                audioSource.PlayOneShot(footstep, Mathf.Clamp01(speedRatio * 0.6f));
            }

            // Reset timer with slight variation
            float intervalVariation = Random.Range(-0.1f, 0.1f);
            footstepTimer = footstepInterval + intervalVariation;

            // Make footsteps faster when sprinting
            if (isSprinting)
            {
                footstepTimer *= 0.7f;
            }
            // Make footsteps slower when crouching
            else if (isCrouching)
            {
                footstepTimer *= 1.5f;
            }
        }
    }
}