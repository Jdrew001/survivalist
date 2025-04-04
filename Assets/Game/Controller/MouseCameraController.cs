using UnityEngine;

public class MouseCameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float mouseSensitivity = 2.0f;
    [SerializeField] private Transform playerBody;
    [SerializeField] private bool lockCursor = true;
    [SerializeField] private float upperLookLimit = 80.0f;
    [SerializeField] private float lowerLookLimit = 80.0f;

    // Camera smoothing
    [SerializeField] private bool enableSmoothing = true;
    [SerializeField] private float smoothingSpeed = 10.0f;

    // Debug options
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // Internal variables
    private float xRotation = 0f;
    private float yRotation = 0f;

    void Start()
    {
        // Lock cursor
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Find player body if not assigned
        if (playerBody == null)
        {
            playerBody = transform.parent;

            if (playerBody != null && playerBody.parent != null)
            {
                // Likely a camera holder setup, get the actual player
                playerBody = playerBody.parent;
            }

            // If still null, try to find a character controller in the scene
            if (playerBody == null)
            {
                CharacterController controller = FindObjectOfType<CharacterController>();
                if (controller != null)
                {
                    playerBody = controller.transform;
                    if (showDebugLogs) Debug.Log("Found player body via CharacterController: " + playerBody.name);
                }
                else
                {
                    Debug.LogError("ImprovedMouseCameraController: No player body assigned and none could be found!");
                }
            }
        }

        // Initialize rotation values based on current orientation
        if (playerBody != null)
        {
            yRotation = playerBody.eulerAngles.y;
            if (showDebugLogs) Debug.Log("Starting yRotation: " + yRotation);
        }

        // Initialize camera pitch (x rotation)
        xRotation = transform.localEulerAngles.x;
        // Fix for initial value when starting with camera looking up
        if (xRotation > 180f)
        {
            xRotation -= 360f;
        }
        xRotation = Mathf.Clamp(xRotation, -upperLookLimit, lowerLookLimit);

        if (showDebugLogs) Debug.Log("Starting xRotation: " + xRotation);
    }

    void Update()
    {
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        if (showDebugLogs && (Mathf.Abs(mouseX) > 0.1f || Mathf.Abs(mouseY) > 0.1f))
        {
            Debug.Log($"Mouse input: X={mouseX}, Y={mouseY}");
        }

        // Update vertical rotation (pitch - looking up/down)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -upperLookLimit, lowerLookLimit);

        // Update horizontal rotation (yaw - looking left/right)
        yRotation += mouseX;

        // Apply rotations - with or without smoothing
        if (enableSmoothing)
        {
            // Smoothly apply vertical rotation to camera
            Quaternion targetCameraRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                targetCameraRotation,
                smoothingSpeed * Time.deltaTime
            );

            // Smoothly apply horizontal rotation to player body
            if (playerBody != null)
            {
                Quaternion targetBodyRotation = Quaternion.Euler(0f, yRotation, 0f);
                playerBody.rotation = Quaternion.Slerp(
                    playerBody.rotation,
                    targetBodyRotation,
                    smoothingSpeed * Time.deltaTime
                );
            }
        }
        else
        {
            // Direct application of rotations without smoothing
            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

            if (playerBody != null)
            {
                playerBody.rotation = Quaternion.Euler(0f, yRotation, 0f);
            }
        }
    }

    // Helper method to temporarily disable camera control (for menus, cutscenes, etc.)
    public void SetCameraControlEnabled(bool enabled)
    {
        this.enabled = enabled;

        if (enabled && lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // Reset rotations to specified values (useful for cutscenes, etc.)
    public void SetCameraRotation(float pitch, float yaw)
    {
        xRotation = Mathf.Clamp(pitch, -upperLookLimit, lowerLookLimit);
        yRotation = yaw;

        // Apply immediately
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        if (playerBody != null)
        {
            playerBody.rotation = Quaternion.Euler(0f, yRotation, 0f);
        }
    }
}