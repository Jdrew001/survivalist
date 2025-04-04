using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

namespace EndlessTerrain
{
    [System.Serializable]
    public class TerrainBiome
    {
        [Header("Biome Settings")]
        private int index;
        public int Index
        {
            get { return index; }
            set { index = value; }
        }

        //Has no purpose other than organizational
        [SerializeField]
        [Tooltip("The name of this biome. Has no purpose outside of the editor.")]
        private string name;
        public string Name
        {
            get { return name; }
        }

        [SerializeField]
        [Tooltip("The list of biome bounds used to determine where this biome is placed in the biome grid.")]
        private List<BiomeBound> bounds = new List<BiomeBound>();
        public List<BiomeBound> Bounds
        {
            get { return bounds; }
        }

        [SerializeField]
        [Tooltip("The noise values used to generate chunks of this biome.")]
        private BiomeValues biomeValues;
        public BiomeValues BiomeValues
        {
            get { return biomeValues; }
            set { biomeValues = value; }
        }

        [SerializeField]
        [Tooltip("The texture layers used to render chunks of this biome.")]
        private List<TextureLayer> terrainLayers = new List<TextureLayer>();
        public List<TextureLayer> TerrainLayers
        {
            get { return terrainLayers; }
        }

        [SerializeField]
        [Tooltip("The steepness texture set of this biome.")]
        private TextureSet steepnessTexture;
        public TextureSet SteepnessTexture
        {
            get { return steepnessTexture; }
        }

        [SerializeField]
        [Tooltip("The second steepness texture set of this biome.")]
        private TextureSet secondSteepnessTexture;
        public TextureSet SecondSteepnessTexture
        {
            get { return secondSteepnessTexture; }
        }

        [SerializeField]
        [Tooltip("The road texture set of this biome.")]
        private TextureSet roadTexture;
        public TextureSet RoadTexture
        {
            get { return roadTexture; }
        }

        [SerializeField]
        [Tooltip("The list of terrain objects placed on chunks of this biome.")]
        private List<TerrainObjectInstance> terrainObjects = new List<TerrainObjectInstance>();
        public List<TerrainObjectInstance> TerrainObjects
        {
            get { return terrainObjects; }
        }

        [SerializeField]
        [Tooltip("The list of terrain structures placed on structures generated in chunks of this biome.")]
        private List<TerrainStructureInstance> terrainStructures = new List<TerrainStructureInstance>();
        public List<TerrainStructureInstance> TerrainStructures
        {
            get { return terrainStructures; }
        }

#if UNITY_EDITOR
#pragma warning disable
        //Editor UI variables 
        [SerializeField]
        [HideInInspector]
        private bool noiseValuesExpanded = true;
        [SerializeField]
        [HideInInspector]
        private bool texturesExpanded = false;
        [SerializeField]
        [HideInInspector]
        private bool objectsExpanded = false;
#pragma warning restore
#endif
    }

    [System.Serializable]
    public class BiomeValues
    {
        public NoiseValues biomeVegetationValues;
        public bool rockNoiseEnabled;
        public NoiseValues biomeRockValues;
        public bool elevationNoiseEnabled;
        public NoiseValues biomeElevationValues;
        [Tooltip("Maps elevation noise to elevation of terrain.")]
        public AnimationCurve elevationCurve;
        [Tooltip("Describes how much difference in elevation affects terrain.")]
        public AnimationCurve floorWeightCurve;
        public bool voronoiNoiseEnabled;
        public VoronoiNoiseValues biomeVoronoiValues;
        [Tooltip("Maps voronoi noise strength to steepness (difference in elevation values between neighboring cells).")]
        public AnimationCurve voronoiSteepnessCurve;
        public NoiseValues biomeTerrainValues;
    }

    [System.Serializable]
    public struct NoiseValues
    {
        public float scale;
        public float persistence;
        public float lacunarity;
        public int octaves;
        [Range(-2f, 2f)]
        public float bias;
        [HideInInspector]
        public bool enabled;
    }

    [System.Serializable]
    public struct VoronoiNoiseValues
    {
        public float voronoiScale;
        public int voronoiOctaves;
        public float voronoiPersistence;
        public float voronoiLacunarity;
        public float voronoiApplicationScale;
        public float bias;
        public float voronoiPower;
        [HideInInspector]
        public bool enabled;
    }

    [System.Serializable]
    public class BiomeBound
    {
        [SerializeField]
        [Tooltip("The temperature value of this bound")]
        private Vector2 temperatureMinMax;
        public Vector2 TemperatureMinMax
        {
            get { return temperatureMinMax; }
            set { temperatureMinMax = value; }
        }

        private Vector2 internalTemperatureMinMax;
        public Vector2 InternalTemperatureMinMax
        {
            get { return internalTemperatureMinMax; }
            set { internalTemperatureMinMax = value; }
        }

        [SerializeField]
        [Tooltip("The moisture value of this bound.")]
        private Vector2 moistureMinMax;
        public Vector2 MoistureMinMax
        {
            get { return moistureMinMax; }
            set { moistureMinMax = value; }
        }

        private Vector2 internalMoistureMinMax;
        public Vector2 InternalMoistureMinMax
        {
            get { return internalMoistureMinMax; }
            set { internalMoistureMinMax = value; }
        }
    }
}