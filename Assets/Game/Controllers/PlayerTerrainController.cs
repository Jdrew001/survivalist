using UnityEngine;

namespace Assets.Game.Controllers
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerTerrainController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 6f;
        [SerializeField] private float runSpeed = 12f;
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float gravity = 30f;
        [SerializeField] private float lookSensitivity = 2f;
        [SerializeField] private float maxLookAngle = 80f;

        [Header("Ground Check")]
        [SerializeField] private float groundCheckDistance = 0.2f;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private bool snapToGround = true;
        [SerializeField] private float groundSnapForce = 20f;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        private CharacterController characterController;
        private Vector3 moveDirection = Vector3.zero;
        private float verticalLookRotation = 0f;
        private bool isGrounded = false;
        private float currentSpeed;

        private void Start()
        {
            // Get references
            characterController = GetComponent<CharacterController>();

            if (cameraTransform == null)
            {
                // Try to find the camera
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    cameraTransform = mainCamera.transform;
                }
                else
                {
                    Debug.LogError("No camera assigned to PlayerTerrainController!");
                }
            }

            // Lock cursor for FPS controls
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Initialize speed
            currentSpeed = walkSpeed;
        }

        private void Update()
        {
            // Handle mouse looking
            HandleMouseLook();

            // Check if we're grounded
            CheckGroundStatus();

            // Handle player movement
            HandleMovement();

            // Handle input for cursor lock toggle
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ToggleCursorLock();
            }
        }

        private void HandleMouseLook()
        {
            // Only process mouse look if cursor is locked
            if (Cursor.lockState != CursorLockMode.Locked) return;

            // Get mouse input
            float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

            // Rotate player based on mouse X movement
            transform.Rotate(Vector3.up * mouseX);

            // Rotate camera based on mouse Y movement
            verticalLookRotation -= mouseY;
            verticalLookRotation = Mathf.Clamp(verticalLookRotation, -maxLookAngle, maxLookAngle);

            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(verticalLookRotation, 0f, 0f);
            }
        }

        private void CheckGroundStatus()
        {
            // Cast a ray downward to check for ground
            Ray ray = new Ray(transform.position + (Vector3.up * 0.1f), Vector3.down);
            isGrounded = Physics.Raycast(ray, groundCheckDistance + 0.1f, groundMask);
        }

        private void HandleMovement()
        {
            // Set speed based on whether we're running
            currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

            // Calculate horizontal movement
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            // Get forward and right directions relative to the player (ignoring camera rotation)
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            // Calculate move direction in player space
            Vector3 horizontalMovement = (forward * vertical + right * horizontal).normalized;

            // Apply gravity if we're in the air
            if (isGrounded)
            {
                moveDirection.y = -gravity * Time.deltaTime; // Small downward force when grounded

                // Jump
                if (Input.GetButtonDown("Jump"))
                {
                    moveDirection.y = jumpForce;
                }

                // Snap to ground if enabled
                if (snapToGround)
                {
                    Ray ray = new Ray(transform.position + (Vector3.up * 0.1f), Vector3.down);
                    if (Physics.Raycast(ray, out RaycastHit hit, groundCheckDistance + 0.2f, groundMask))
                    {
                        // Apply downward force to keep player on ground
                        moveDirection.y = -groundSnapForce;

                        // Adjust player to match ground normal slightly (for slopes)
                        Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation * transform.rotation, 0.1f);
                    }
                }
            }
            else
            {
                // Apply gravity
                moveDirection.y -= gravity * Time.deltaTime;
            }

            // Set horizontal movement
            moveDirection.x = horizontalMovement.x * currentSpeed;
            moveDirection.z = horizontalMovement.z * currentSpeed;

            // Move the character controller
            characterController.Move(moveDirection * Time.deltaTime);

            // Detect falling off terrain (reset if below certain height)
            if (transform.position.y < -50f)
            {
                transform.position = new Vector3(transform.position.x, 100f, transform.position.z);
                moveDirection = Vector3.zero;
            }
        }

        private void ToggleCursorLock()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public void TeleportTo(Vector3 position)
        {
            // Safely teleport the player to a new position
            characterController.enabled = false;
            transform.position = position;
            characterController.enabled = true;
            moveDirection = Vector3.zero;
        }

        public void TeleportToSafeHeight(Vector2 position2D)
        {
            // Find a safe height to teleport the player to
            RaycastHit hit;
            Vector3 startPos = new Vector3(position2D.x, 1000f, position2D.y);

            if (Physics.Raycast(startPos, Vector3.down, out hit, 2000f, groundMask))
            {
                // Teleport to slightly above ground
                TeleportTo(hit.point + Vector3.up * 2f);
            }
            else
            {
                // If no ground found, teleport to fixed height
                TeleportTo(new Vector3(position2D.x, 100f, position2D.y));
            }
        }
    }
}