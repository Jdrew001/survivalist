using Assets.Game.Managers;
using Assets.Game.Services.Interfaces;
using EndlessTerrain;
using System.Collections;
using UnityEngine;
using Zenject;

namespace Assets.Game.Controller
{
    public class PlayerSpawnController : MonoBehaviour
    {
        [Header("Player Settings")]
        [SerializeField] private float spawnHeight = 5f; // Extra height above terrain
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform playerSpawnPoint;

        [Header("Camera Settings")]
        [SerializeField] private float fadeInDuration = 1.5f;
        [SerializeField] private CanvasGroup fadeOverlay;

        [Header("Terrain Reference")]
        [SerializeField] private TerrainManager terrainManager;

        private GameObject player;
        private bool isPlayerSpawned = false;

        [Inject]
        private IPlayerService playerService;

        private void Start()
        {
            playerService.CameraCanMove = false;
            // Find references if not set
            if (terrainManager == null)
            {
                terrainManager = FindObjectOfType<TerrainManager>();
                if (terrainManager == null)
                {
                    Debug.LogError("TerrainManager not found! Cannot spawn player correctly.");
                    return;
                }
            }

            // Ensure we have a fade overlay
            if (fadeOverlay == null)
            {
                Debug.LogWarning("Fade overlay not assigned. Camera fade-in will be skipped.");
            }
            else
            {
                // Start with black screen
                fadeOverlay.alpha = 1f;
            }

            // Start terrain check routine
            StartCoroutine(WaitForTerrainAndSpawnPlayer());
        }

        private IEnumerator WaitForTerrainAndSpawnPlayer()
        {
            // Wait a short time for initial terrain generation
            yield return new WaitForSeconds(0.9f);

            // Get spawn position (using playerSpawnPoint if set, otherwise use origin)
            Vector3 spawnPosition = playerSpawnPoint != null
                ? playerSpawnPoint.position
                : new Vector3(0, 0, 0);

            // Get surface level at this position
            float surfaceLevel = terrainManager.GetSurfaceLevel(spawnPosition);

            // Set the spawn position above the surface
            spawnPosition.y = surfaceLevel + spawnHeight;

            // Spawn the player
            SpawnPlayer(spawnPosition);

            // Wait a frame to ensure player is properly initialized
            yield return null;

            // Start fade-in effect
            if (fadeOverlay != null)
            {
                yield return StartCoroutine(FadeIn());
            }
        }

        private void SpawnPlayer(Vector3 position)
        {
            // Use existing player in scene if no prefab is set
            if (playerPrefab == null)
            {
                player = GameObject.FindGameObjectWithTag("Player");
                if (player == null)
                {
                    Debug.LogError("Player prefab not set and no player found in scene!");
                    return;
                }

                // Move existing player to spawn position
                player.transform.position = position;
            }
            else
            {
                // Instantiate the player at the spawn position
                player = Instantiate(playerPrefab, position, Quaternion.identity);
            }

            // Set player reference in terrain manager if needed
            if (terrainManager.Player == null)
            {
                terrainManager.Player = player;
            }

            isPlayerSpawned = true;
            Debug.Log("Player spawned at: " + position);
        }

        private IEnumerator FadeIn()
        {
            float elapsedTime = 0f;

            // Gradually decrease the alpha value of the fade overlay
            while (elapsedTime < fadeInDuration)
            {
                fadeOverlay.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeInDuration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Ensure we end at completely transparent
            fadeOverlay.alpha = 0f;
            playerService.CameraCanMove = true;
        }
    }
}
