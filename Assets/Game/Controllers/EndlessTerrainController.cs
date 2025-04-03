using Assets.Game.Systems.TerrainSystem.Biomes;
using Assets.Game.Systems.TerrainSystem.TerrainChunk;
using UnityEngine;

namespace Assets.Game.Systems.TerrainSystem
{
    public class EndlessTerrainController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EndlessTerrainManager terrainManager;
        [SerializeField] private BiomeManager biomeManager;
        [SerializeField] private Transform cameraTransform;

        [Header("Camera Settings")]
        [SerializeField] private float moveSpeed = 40f;
        [SerializeField] private float fastMoveSpeed = 100f;
        [SerializeField] private float rotationSpeed = 120f;
        [SerializeField] private float elevationSpeed = 30f;
        [SerializeField] private float minHeight = 10f;
        [SerializeField] private float maxHeight = 500f;

        [Header("Settings")]
        [SerializeField] private KeyCode regenerateKey = KeyCode.F5;
        [SerializeField] private KeyCode randomBiomeKey = KeyCode.B;
        [SerializeField] private KeyCode increaseViewDistanceKey = KeyCode.Equals; // '+' key
        [SerializeField] private KeyCode decreaseViewDistanceKey = KeyCode.Minus;
        [SerializeField] private KeyCode randomSeedKey = KeyCode.R;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private int viewDistance = 3;
        [SerializeField] private int seed = 0;

        private bool isFastMoving = false;
        private Vector2Int currentChunkCoord;
        private Vector3 lastPosition;
        private float distanceTraveled = 0f;

        private void Start()
        {
            // Find references if not set
            if (terrainManager == null)
            {
                terrainManager = FindObjectOfType<EndlessTerrainManager>();
            }

            if (biomeManager == null)
            {
                biomeManager = FindObjectOfType<BiomeManager>();
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            lastPosition = cameraTransform != null ? cameraTransform.position : Vector3.zero;
            currentChunkCoord = WorldToChunkCoord(lastPosition);
        }

        private void Update()
        {
            if (cameraTransform == null) return;

            // Handle camera movement
            HandleCameraMovement();

            // Handle key inputs
            HandleKeyInputs();

            // Update metrics
            UpdateMovementMetrics();

            // Show debug GUI if enabled
            if (showDebugInfo)
            {
                UpdateDebugInfo();
            }
        }

        private void HandleCameraMovement()
        {
            // Check for sprint key
            isFastMoving = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float currentMoveSpeed = isFastMoving ? fastMoveSpeed : moveSpeed;

            // Get input axes
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            float upDown = 0;

            // Handle elevation changes
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space))
            {
                upDown = 1;
            }
            else if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl))
            {
                upDown = -1;
            }

            // Calculate movement vector
            Vector3 forward = cameraTransform.forward;
            forward.y = 0; // Keep movement on a horizontal plane
            forward.Normalize();

            Vector3 right = cameraTransform.right;
            right.y = 0; // Keep movement on a horizontal plane
            right.Normalize();

            Vector3 moveDirection = (forward * vertical + right * horizontal).normalized;
            Vector3 movement = moveDirection * currentMoveSpeed * Time.deltaTime;

            // Apply elevation change
            movement.y = upDown * elevationSpeed * Time.deltaTime;

            // Apply movement
            cameraTransform.position += movement;

            // Clamp height
            Vector3 pos = cameraTransform.position;
            pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);
            cameraTransform.position = pos;

            // Handle rotation with mouse if right mouse button is held
            if (Input.GetMouseButton(1))
            {
                float rotX = Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
                float rotY = Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;

                // Rotate the camera
                cameraTransform.rotation *= Quaternion.Euler(-rotY, rotX, 0);

                // Clamp vertical rotation to prevent over-rotation
                Vector3 angles = cameraTransform.eulerAngles;
                angles.z = 0; // No roll

                // Clamp x rotation between -80 and 80 degrees
                if (angles.x > 180)
                    angles.x = Mathf.Max(angles.x, 360 - 80);
                else
                    angles.x = Mathf.Min(angles.x, 80);

                cameraTransform.eulerAngles = angles;
            }
        }

        private void HandleKeyInputs()
        {
            // Regenerate terrain
            if (Input.GetKeyDown(regenerateKey) && terrainManager != null)
            {
                terrainManager.RegenerateVisibleTerrain();
            }

            // Random biome
            if (Input.GetKeyDown(randomBiomeKey) && biomeManager != null)
            {
                biomeManager.SelectRandomBiome();
            }

            // Change view distance
            if (Input.GetKeyDown(increaseViewDistanceKey) && terrainManager != null)
            {
                viewDistance++;
                terrainManager.SetViewDistance(viewDistance);
            }

            if (Input.GetKeyDown(decreaseViewDistanceKey) && terrainManager != null)
            {
                viewDistance = Mathf.Max(1, viewDistance - 1);
                terrainManager.SetViewDistance(viewDistance);
            }

            // Randomize seed
            if (Input.GetKeyDown(randomSeedKey) && terrainManager != null)
            {
                seed = Random.Range(0, 100000);
                terrainManager.UpdateSeed(seed);
            }
        }

        private void UpdateMovementMetrics()
        {
            if (cameraTransform == null) return;

            // Calculate distance traveled
            distanceTraveled += Vector3.Distance(lastPosition, cameraTransform.position);
            lastPosition = cameraTransform.position;

            // Check if chunk changed
            Vector2Int newChunkCoord = WorldToChunkCoord(cameraTransform.position);
            if (newChunkCoord != currentChunkCoord)
            {
                currentChunkCoord = newChunkCoord;
            }
        }

        private void UpdateDebugInfo()
        {
            // This will be displayed in OnGUI
        }

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.alignment = TextAnchor.UpperLeft;
            style.normal.textColor = Color.white;

            // Create background box
            GUI.Box(new Rect(10, 10, 260, 160), "Endless Terrain Debug", style);

            // Display current position and chunk
            string posText = $"Position: {cameraTransform.position.x:F1}, {cameraTransform.position.y:F1}, {cameraTransform.position.z:F1}";
            string chunkText = $"Chunk: {currentChunkCoord.x}, {currentChunkCoord.y}";
            string speedText = $"Speed: {(isFastMoving ? fastMoveSpeed : moveSpeed)} units/sec";
            string distanceText = $"Distance Traveled: {distanceTraveled:F1} units";
            string biomeText = "";

            if (biomeManager != null && biomeManager.GetCurrentBiome() != null)
            {
                biomeText = $"Biome: {biomeManager.GetCurrentBiome().biomeName}";
            }

            string viewDistText = $"View Distance: {viewDistance} chunks";
            string seedText = $"Seed: {seed}";

            // Controls help
            string controlsText = "WASD: Move | E/Q: Up/Down | RMB: Look | SHIFT: Sprint";
            string controlsText2 = "F5: Regenerate | B: Random Biome | +/-: View Dist | R: Random Seed";

            // Display all text
            GUI.Label(new Rect(20, 35, 240, 20), posText, style);
            GUI.Label(new Rect(20, 55, 240, 20), chunkText, style);
            GUI.Label(new Rect(20, 75, 240, 20), speedText, style);
            GUI.Label(new Rect(20, 95, 240, 20), distanceText, style);
            GUI.Label(new Rect(20, 115, 240, 20), biomeText, style);
            GUI.Label(new Rect(20, 135, 240, 20), viewDistText + " | " + seedText, style);

            // Controls at bottom of screen
            GUI.Box(new Rect(10, Screen.height - 60, Screen.width - 20, 50), "", style);
            GUI.Label(new Rect(20, Screen.height - 55, Screen.width - 40, 20), controlsText, style);
            GUI.Label(new Rect(20, Screen.height - 35, Screen.width - 40, 20), controlsText2, style);
        }

        private Vector2Int WorldToChunkCoord(Vector3 worldPosition)
        {
            // Assuming standard chunk size of 256
            int chunkSize = 256;
            int x = Mathf.FloorToInt(worldPosition.x / chunkSize);
            int z = Mathf.FloorToInt(worldPosition.z / chunkSize);
            return new Vector2Int(x, z);
        }
    }
}