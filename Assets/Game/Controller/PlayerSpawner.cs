using EndlessTerrain;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Game.Controller
{
    public class PlayerSpawner : MonoBehaviour
    {
        [Header("Player References")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private float spawnHeight = 5f; // Extra height above the terrain

        [Header("Terrain Reference")]
        [SerializeField] private TerrainManager terrainManager;

        [Header("Pickup Item")]
        [SerializeField] private GameObject pickupItemPrefab;
        [SerializeField] private float itemDistance = 3f; // Distance in front of player
        [SerializeField] private Vector3 itemScale = new Vector3(0.3f, 0.3f, 0.3f); // Small scale for the item

        private GameObject player;

        void Start()
        {
            if (terrainManager == null)
            {
                terrainManager = FindObjectOfType<TerrainManager>();
                if (terrainManager == null)
                {
                    Debug.LogError("TerrainManager not found! Cannot spawn player correctly.");
                    return;
                }
            }

            // Subscribe to the ChunkGenerated event to wait for terrain to generate
           // terrainManager.ChunkGenerated += OnChunkGenerated;

            // Start a routine to check for terrain generation
            StartCoroutine(WaitForTerrain());
        }

        private IEnumerator WaitForTerrain()
        {
            // Wait a short time for initial terrain generation
            yield return new WaitForSeconds(0.5f);

            // Get spawn position at the center of the world
            Vector3 spawnPosition = new Vector3(0, 0, 0);

            // Get surface level at this position
            float surfaceLevel = terrainManager.GetSurfaceLevel(spawnPosition);

            Debug.Log($"Surface level at {spawnPosition}: {surfaceLevel}");

            // Set the spawn position above the surface
            spawnPosition.y = surfaceLevel + spawnHeight;

            // Spawn the player
            SpawnPlayer(spawnPosition);

            //// Spawn the pickup item
            SpawnPickupItem();
        }

        private void SpawnPlayer(Vector3 position)
        {
            // If the player prefab isn't set, try to find a player in the scene
            if (playerPrefab == null)
            {
                player = GameObject.FindGameObjectWithTag("Player");
                if (player == null)
                {
                    Debug.LogError("Player prefab not set and no player found in scene!");
                    return;
                }

                // Move existing player to the spawn position
                player.transform.position = position;
            }
            else
            {
                // Instantiate the player at the spawn position
                player = Instantiate(playerPrefab, position, Quaternion.identity);
            }

            // Set the player reference in the terrain manager
            if (terrainManager.Player == null)
            {
                terrainManager.Player = player;
            }

            Debug.Log("Player spawned at: " + position);
        }

        private void SpawnPickupItem()
        {
            if (pickupItemPrefab == null)
            {
                pickupItemPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);

                // Add a rigidbody to the item
                if (pickupItemPrefab.GetComponent<Rigidbody>() == null)
                {
                    pickupItemPrefab.AddComponent<Rigidbody>();
                }
            }

            if (player != null)
            {
                // Spawn the item in front of the player on the ground
                Vector3 playerForward = player.transform.forward;
                playerForward.y = 0; // Make sure it's on the same height plane
                playerForward.Normalize();

                Vector3 itemPosition = player.transform.position + playerForward * itemDistance;

                // Find ground level at item position
                float groundLevel = terrainManager.GetSurfaceLevel(itemPosition);
                itemPosition.y = groundLevel + 0.2f; // Slightly above ground to prevent clipping

                // Instantiate the item
                GameObject item = Instantiate(pickupItemPrefab, itemPosition, Quaternion.identity);
                item.name = "PickupItem";
                item.transform.localScale = itemScale;

                // Add component to make it interactable
                if (item.GetComponent<Assets.Game.Inventory.Helpers.WorldItem>() == null)
                {
                    Assets.Game.Inventory.Helpers.WorldItem worldItem = item.AddComponent<Assets.Game.Inventory.Helpers.WorldItem>();
                    worldItem.itemId = "stick1"; // Reference to the item ID from your ItemDatabase
                }

                Debug.Log("Pickup item spawned at: " + itemPosition);
            }
            else
            {
                Debug.LogError("Cannot spawn pickup item - player not found!");
            }
        }
    }
}
