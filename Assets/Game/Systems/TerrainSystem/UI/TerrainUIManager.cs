using Assets.Game.Systems.TerrainSystem.Biomes;
using Assets.Game.Systems.TerrainSystem.Facade;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine;

namespace Assets.Game.Systems.TerrainSystem.UI
{
    public class TerrainUIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TerrainFacade terrainFacade;
        [SerializeField] private BiomeManager biomeManager;

        [Header("UI Elements")]
        [SerializeField] private TMP_Dropdown biomeDropdown;
        [SerializeField] private Button generateButton;
        [SerializeField] private Button randomBiomeButton;
        [SerializeField] private Slider widthSlider;
        [SerializeField] private Slider heightSlider;
        [SerializeField] private TMP_InputField seedInputField;
        [SerializeField] private Toggle randomizeSeedToggle;
        [SerializeField] private TextMeshProUGUI biomeInfoText;
        [SerializeField] private GameObject loadingPanel;

        private void Awake()
        {
            // Find required components if not assigned
            if (terrainFacade == null)
                terrainFacade = FindObjectOfType<TerrainFacade>();

            if (biomeManager == null)
                biomeManager = FindObjectOfType<BiomeManager>();

            // Subscribe to events
            if (terrainFacade != null)
            {
                terrainFacade.OnTerrainGenerationStarted += OnTerrainGenerationStarted;
                terrainFacade.OnTerrainGenerationCompleted += OnTerrainGenerationCompleted;
            }

            if (biomeManager != null)
            {
                biomeManager.OnBiomeChanged += OnBiomeChanged;
            }
        }

        private void Start()
        {
            SetupUI();
            InitializeUIValues();
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (terrainFacade != null)
            {
                terrainFacade.OnTerrainGenerationStarted -= OnTerrainGenerationStarted;
                terrainFacade.OnTerrainGenerationCompleted -= OnTerrainGenerationCompleted;
            }

            if (biomeManager != null)
            {
                biomeManager.OnBiomeChanged -= OnBiomeChanged;
            }
        }

        /// <summary>
        /// Sets up UI elements and attaches event listeners
        /// </summary>
        private void SetupUI()
        {
            // Initialize dropdown with available biomes
            if (biomeDropdown != null)
            {
                PopulateBiomeDropdown();
                biomeDropdown.onValueChanged.AddListener(OnBiomeDropdownChanged);
            }

            // Set up button click events
            if (generateButton != null)
                generateButton.onClick.AddListener(OnGenerateButtonClicked);

            if (randomBiomeButton != null)
                randomBiomeButton.onClick.AddListener(OnRandomBiomeButtonClicked);

            // Set up sliders
            if (widthSlider != null)
                widthSlider.onValueChanged.AddListener(OnTerrainParametersChanged);

            if (heightSlider != null)
                heightSlider.onValueChanged.AddListener(OnTerrainParametersChanged);

            // Set up seed input
            if (seedInputField != null)
                seedInputField.onEndEdit.AddListener(OnSeedInputChanged);

            // Set up randomize toggle
            if (randomizeSeedToggle != null)
                randomizeSeedToggle.onValueChanged.AddListener(OnRandomizeToggleChanged);
        }

        /// <summary>
        /// Initializes UI elements with current values
        /// </summary>
        private void InitializeUIValues()
        {
            // Select current biome in dropdown
            if (biomeDropdown != null && biomeManager != null)
            {
                BiomeConfig currentBiome = biomeManager.GetCurrentBiome();
                if (currentBiome != null)
                {
                    int biomeIndex = biomeManager.GetAvailableBiomes().IndexOf(currentBiome);
                    if (biomeIndex >= 0)
                    {
                        biomeDropdown.value = biomeIndex;
                    }
                }
            }

            // Initialize terrain parameter UI
            if (terrainFacade != null)
            {
                // These values would be exposed in TerrainFacade
                if (widthSlider != null)
                    widthSlider.value = 256; // Default value

                if (heightSlider != null)
                    heightSlider.value = 256; // Default value

                if (seedInputField != null)
                    seedInputField.text = "0"; // Default value

                if (randomizeSeedToggle != null)
                    randomizeSeedToggle.isOn = true; // Default value
            }

            // Update biome info text
            UpdateBiomeInfoText();

            // Hide loading panel initially
            if (loadingPanel != null)
                loadingPanel.SetActive(false);
        }

        /// <summary>
        /// Populates the biome dropdown with available biomes
        /// </summary>
        private void PopulateBiomeDropdown()
        {
            if (biomeDropdown == null || biomeManager == null)
                return;

            List<BiomeConfig> biomes = biomeManager.GetAvailableBiomes();
            List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();

            foreach (BiomeConfig biome in biomes)
            {
                options.Add(new TMP_Dropdown.OptionData(biome.biomeName));
            }

            biomeDropdown.ClearOptions();
            biomeDropdown.AddOptions(options);
        }

        /// <summary>
        /// Updates the biome info text with current biome details
        /// </summary>
        private void UpdateBiomeInfoText()
        {
            if (biomeInfoText == null || biomeManager == null)
                return;

            BiomeConfig currentBiome = biomeManager.GetCurrentBiome();
            if (currentBiome != null)
            {
                biomeInfoText.text = $"<b>{currentBiome.biomeName}</b>\n{currentBiome.biomeDescription}";
            }
            else
            {
                biomeInfoText.text = "No biome selected";
            }
        }

        #region Event Handlers

        private void OnBiomeDropdownChanged(int index)
        {
            if (biomeManager == null || index < 0 || index >= biomeManager.GetAvailableBiomes().Count)
                return;

            // Set the selected biome
            biomeManager.SetBiome(biomeManager.GetAvailableBiomes()[index]);
        }

        private void OnRandomBiomeButtonClicked()
        {
            if (biomeManager != null)
            {
                biomeManager.SelectRandomBiome();
            }
        }

        private void OnGenerateButtonClicked()
        {
            if (terrainFacade != null)
            {
                // Get values from UI
                int width = Mathf.RoundToInt(widthSlider != null ? widthSlider.value : 256);
                int height = Mathf.RoundToInt(heightSlider != null ? heightSlider.value : 256);

                int seed = 0;
                if (seedInputField != null && int.TryParse(seedInputField.text, out int parsedSeed))
                {
                    seed = parsedSeed;
                }

                bool randomize = randomizeSeedToggle != null && randomizeSeedToggle.isOn;

                // Update terrain parameters
                terrainFacade.UpdateTerrainParameters(width, height, seed, randomize);

                // Generate terrain
                terrainFacade.GenerateTerrain();
            }
        }

        private void OnTerrainParametersChanged(float value)
        {
            // This is called when width or height sliders change
            // We don't need to do anything here since values are only applied when Generate is clicked
        }

        private void OnSeedInputChanged(string seedText)
        {
            // Validate seed input
            if (!int.TryParse(seedText, out _))
            {
                // Reset to 0 if invalid
                seedInputField.text = "0";
            }
        }

        private void OnRandomizeToggleChanged(bool isOn)
        {
            // Enable/disable seed input field based on toggle
            if (seedInputField != null)
            {
                seedInputField.interactable = !isOn;
            }
        }

        private void OnBiomeChanged(BiomeConfig newBiome)
        {
            // Update dropdown selection
            if (biomeDropdown != null && biomeManager != null)
            {
                int biomeIndex = biomeManager.GetAvailableBiomes().IndexOf(newBiome);
                if (biomeIndex >= 0 && biomeIndex != biomeDropdown.value)
                {
                    biomeDropdown.value = biomeIndex;
                }
            }

            // Update biome info text
            UpdateBiomeInfoText();
        }

        private void OnTerrainGenerationStarted()
        {
            // Show loading panel
            if (loadingPanel != null)
                loadingPanel.SetActive(true);

            // Disable UI during generation
            SetUIInteractable(false);
        }

        private void OnTerrainGenerationCompleted(Terrain terrain)
        {
            // Hide loading panel
            if (loadingPanel != null)
                loadingPanel.SetActive(false);

            // Re-enable UI
            SetUIInteractable(true);
        }

        #endregion

        /// <summary>
        /// Enables or disables all UI elements
        /// </summary>
        private void SetUIInteractable(bool interactable)
        {
            if (biomeDropdown != null) biomeDropdown.interactable = interactable;
            if (generateButton != null) generateButton.interactable = interactable;
            if (randomBiomeButton != null) randomBiomeButton.interactable = interactable;
            if (widthSlider != null) widthSlider.interactable = interactable;
            if (heightSlider != null) heightSlider.interactable = interactable;
            if (seedInputField != null) seedInputField.interactable = interactable;
            if (randomizeSeedToggle != null) randomizeSeedToggle.interactable = interactable;
        }
    }
}
