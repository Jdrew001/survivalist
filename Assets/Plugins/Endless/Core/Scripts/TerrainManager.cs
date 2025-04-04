using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Collections;
using System.Diagnostics;
using Unity.Mathematics;
using UnityEngine.Rendering;
using Unity.Jobs;
using Unity.Burst;
using UnityEditor;
using System;
using System.IO;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace EndlessTerrain
{
    public class TerrainManager : MonoBehaviour
    {
        #region Exposed Settings
        //
        //
        //Generation Settings
        //

        private enum GenerationMode { Infinite, Bounded };
        [SerializeField]
        [Tooltip("The type of terrain being generated. Infinite terrain generates around player as the player moves. Bounded terrain generates around the terrain manager.")]
        private GenerationMode generationMode = GenerationMode.Infinite;
        [SerializeField]
        [Tooltip("The offset applied to calculating the center of the terrain. Applies to both infinite and bounded terrain.")]
        private Vector2 originOffset = Vector2.zero;
        public Vector2 OriginOffset { get { return originOffset; } set { originOffset = value; } }
        [Range(4, 1200)]
        [SerializeField]
        [Tooltip("The radius of terrain chunks to be generated. Total size of terrain in chunks is (chunkRadius * 2 - 1)^2.")]
        private int chunkRadius = 12;
        public int ChunkRadius { get { return chunkRadius; } set { chunkRadius = value; } }
        [SerializeField]
        [Tooltip("Whether to randomize seed at runtime.")]
        private bool randomizeSeed = false;
        public bool RandomizeSeed { get { return randomizeSeed; } set { randomizeSeed = value; } }
        [SerializeField]
        [Tooltip("The seed used to generate the terrain. Controls all aspects of the terrain.")]
        private int seed = 1;
        public int Seed { get { return seed; } set { seed = value; } }
        // Infinite only settings
        [SerializeField]
        [Range(30f, 180f)]
        [Tooltip("The angle in front of the camera used when calculating chunk priority. Chunks within this angle will have a higher priority than those outside.")]
        private float maxViewAngle = 60;
        public float MaxViewAngle { get { return maxViewAngle; } set { maxViewAngle = value; } }
        [SerializeField]
        [Tooltip("The additional priority added to chunks that are in view of player. Set this to zero if you want to disable view priority.")]
        private float viewGenerationFactor = 15;
        public float ViewGenerationFactor { get { return viewGenerationFactor; } set { viewGenerationFactor = value; } }
        [SerializeField]
        [Tooltip("The \"player\" gameobject that the terrain is generated around.")]
        private GameObject player;
        public GameObject Player { get { return player; } set { player = value; } }

        [SerializeField]
        [Tooltip("Whether to generate terrain at runtime. If false, terrain needs to be generated through the editor.")]
        private bool generateAtRuntime = true;

        //
        //
        //Biome settings
        //

        [SerializeField]
        [Tooltip("The noise values used to calculate temperature values of each chunk. (horizontal dimension of biome grid)")]
        private NoiseValues temperatureNoiseValues;
        public NoiseValues TemperatureNoiseValues { get { return temperatureNoiseValues; } set { temperatureNoiseValues = value; } }
        [SerializeField]
        [Tooltip("The noise values used to calculate moisture values of each chunk. (vertical dimension of biome grid)")]
        private NoiseValues moistureNoiseValues;
        public NoiseValues MoistureNoiseValues { get { return moistureNoiseValues; } set { moistureNoiseValues = value; } }
        [Range(100, 2000)]
        [SerializeField]
        [Tooltip("The length and width of the biome grid used to calculate biome weights from temperature and moisture noise.")]
        private int biomeGridSize = 500;
        public int BiomeGridSize { get { return biomeGridSize; } set { biomeGridSize = value; } }
        [SerializeField]
        [Tooltip("The amount of units on biome grid used to blend between separate biomes.")]
        private int biomeBlend = 25;
        public int BiomeBlend { get { return biomeBlend; } set { biomeBlend = value; } }
        [SerializeField]
        [Tooltip("Terrain biomes list. Used to calculate terrain shape, texture drawing, and object placement.")]
        private List<TerrainBiome> terrainBiomes = new List<TerrainBiome>();
        public List<TerrainBiome> TerrainBiomes { get { return terrainBiomes; } set { terrainBiomes = value; } }

#if UNITY_EDITOR
#pragma warning disable
        //Editor UI variables 
        [SerializeField]
        [HideInInspector]
        private int currentEditingBiome = -1;
#pragma warning restore
#endif

        //
        //
        //Noise Settings
        //

        //3D Noise settings only
        [SerializeField]
        [Tooltip("The size of each chunk in world units.")]
        private Vector3Int size3DExposed = new Vector3Int(32, 200, 32);
        [SerializeField]
        [HideInInspector]
        private Vector3Int size3D = new Vector3Int(33, 201, 33);
        public Vector3Int Size { get { return size3D; } set { size3D = value; } }
        [SerializeField]
        [Tooltip("Performance optimization used to decrease number of expensive gradient noise calculations. Instead of calculating gradient noise at each index, some indices are skipped and are instead calculate by interpolating between neighboring noise values. Can introduce artifacts at higher numbers.")]
        private int verticalSampleRate = 8;
        public int VerticalSampleRate { get { return verticalSampleRate; } set { verticalSampleRate = value; } }
        [SerializeField]
        [Tooltip("Performance optimization used to decrease number of expensive gradient noise calculations. Instead of calculating gradient noise at each index, some indices are skipped and are instead calculate by interpolating between neighboring noise values. Can introduce artifacts at higher numbers.")]
        private int horizontalSampleRate = 4;
        public int HorizontalSampleRate { get { return horizontalSampleRate; } set { horizontalSampleRate = value; } }

        [SerializeField]
        [Tooltip("Whether to enable ridged noise on terrain.")]
        private bool ridgedNoiseEnabled = false;
        public bool RidgedNoiseEnabled { get { return ridgedNoiseEnabled; } set { ridgedNoiseEnabled = value; } }
        [SerializeField]
        [Tooltip("The values used to apply ridged noise to terrain. Can be used for rivers, ravines, caves, etc.")]
        private List<RidgedNoiseValues> ridgedNoiseValues = new List<RidgedNoiseValues>();
        public List<RidgedNoiseValues> RidgedNoiseValues { get { return ridgedNoiseValues; } set { ridgedNoiseValues = value; } }

        //
        //
        //Mesh Settings
        //

        [SerializeField]
        [Tooltip("The prefab used when instantiating chunks. Should include a mesh renderer, mesh filter, and mesh collider.")]
        private GameObject chunkPrefab;
        public GameObject ChunkPrefab { get { return chunkPrefab; } set { chunkPrefab = value; } }
        //Physics Mesh Settings
        [SerializeField]
        [Tooltip("Minimum distance from player that a chunk needs to be in order to be valid for batched collider baking. Any chunk closer than this distance will have it's collider immediately baked.")]
        private float minColliderChunkDistance = 2f;
        public float MinColliderChunkDistance { get { return minColliderChunkDistance; } set { minColliderChunkDistance = value; } }
        [SerializeField]
        [Tooltip("Mininum number of physics colliders to bake at a time.")]
        private int colliderBatchSize = 10;
        public int ColliderBatchSize { get { return colliderBatchSize; } set { colliderBatchSize = value; } }
        //Marching Cubes only settings
        [SerializeField]
        [Range(-1f, 1f)]
        [Tooltip("The threshold between solid and air when generating mesh using noise values. Any value less than the threshold is solid, and any value higher is air.")]
        private float densityThreshold = 0f;
        public float DensityThreshold { get { return densityThreshold; } set { densityThreshold = value; } }

        //
        //
        //Texture Settings
        //

        [SerializeField]
        [Tooltip("The material used to render terrain meshes. Should only be a material with the provided terrain shader applied.")]
        private Material terrainMat;
        public Material TerrainMat { get { return terrainMat; } set { terrainMat = value; } }
        [Range(0.0001f, 1f)]
        [SerializeField]
        [Tooltip("How far texture layers should be blended, in terms of percentage of total height.")]
        private float textureBlendDistance = 0.02f;
        public float TextureBlendDistance { get { return textureBlendDistance; } set { textureBlendDistance = value; } }
        [Range(0.0001f, 1f)]
        [SerializeField]
        [Tooltip("How far steepness texture should be blended with regular terrain layer, in terms of percentage of total height.")]
        private float textureSteepnessBlendDistance = 0.04f;
        public float TextureSteepnessBlendDistance { get { return textureSteepnessBlendDistance; } set { textureSteepnessBlendDistance = value; } }
        [Range(0.0001f, 1f)]
        [SerializeField]
        [Tooltip("How far second steepness texture should be blended with steepness texture, in terms of percentage of total height.")]
        private float textureSecondSteepnessBlendDistance = 0.08f;
        public float TextureSecondSteepnessBlendDistance { get { return textureSecondSteepnessBlendDistance; } set { textureSecondSteepnessBlendDistance = value; } }
        [Range(0.0001f, 1f)]
        [SerializeField]
        [Tooltip("How far internal layer textures blend.")]
        private float vegetationBlendDistance = 0.05f;
        public float VegetationBlendDistance { get { return vegetationBlendDistance; } set { vegetationBlendDistance = value; } }
        //Internal variables
        private int layerCount;
        private int textureCountPerLayer;

        [SerializeField]
        [Tooltip("The width and length of terrain textures. All albedo maps should be this size, and all other maps should be half of this size.")]
        private int texSize = 2048;
        public int TexSize { get { return texSize; } set { texSize = value; } }
        [SerializeField]
        [Tooltip("The texture format used to create terrain texture arrays.")]
        private TextureFormat textureFormat = TextureFormat.RGBA32;
        public TextureFormat TextureFormat { get { return textureFormat; } set { textureFormat = value; } }
        [Range(0.0001f, 1f)]
        [SerializeField]
        [Tooltip("The steepness threshold used to determine when steepness texture is layered on top of base terrain layer. (0 is flat, 1 is straight down)")]
        private float steepnessThreshold = 0.08f;
        public float SteepnessThreshold { get { return steepnessThreshold; } set { steepnessThreshold = value; } }
        [Range(0.0001f, 1f)]
        [SerializeField]
        [Tooltip("The steepness threshold used to determine when second steepness texture is layered on top of first steepness texture. (0 is flat, 1 is straight down)")]
        private float secondSteepnessThreshold = 0.2f;
        public float SecondSteepnessThreshold { get { return secondSteepnessThreshold; } set { secondSteepnessThreshold = value; } }

        //
        //
        //Water Settings
        //

        [SerializeField]
        [Tooltip("Whether to enable water spawning in chunks.")]
        private bool waterEnabled = false;
        public bool WaterEnabled { get { return waterEnabled; } set { waterEnabled = value; } }
        [SerializeField]
        [Tooltip("The water prefab to be spawned with each chunk.")]
        private GameObject waterPrefab;
        public GameObject WaterPrefab { get { return waterPrefab; } set { waterPrefab = value; } }
        [SerializeField]
        [Tooltip("The height water objects are spawned at.")]
        private float waterLevel;
        public float WaterLevel { get { return waterLevel; } set { waterLevel = value; } }

        //
        //
        //Road Settings
        //

        [SerializeField]
        [Tooltip("Whether to enable generation of roads throughout terrain.")]
        private bool roadsEnabled = false;
        public bool RoadsEnabled { get { return roadsEnabled; } set { roadsEnabled = value; } }
        [SerializeField]
        [Tooltip("Ridged noise values used to generate roads throughout terrain.")]
        private List<RoadNoiseValues> roadRidgedNoisePasses;
        public List<RoadNoiseValues> RoadRidgedNoisePasses { get { return roadRidgedNoisePasses; } set { roadRidgedNoisePasses = value; } }
        [SerializeField]
        [Tooltip("How high above and below terrain is deformed around roads.")]
        private float roadHeight = 15f;
        public float RoadHeight { get { return roadHeight; } set { roadHeight = value; } }
        [Range(0f, 1f)]
        [SerializeField]
        [Tooltip("The road weight threshold needed to prevent object spawning.")]
        private float roadWeightObjectSpawnThreshold = 0.5f;
        public float RoadWeightObjectSpawnThreshold { get { return roadWeightObjectSpawnThreshold; } set { roadWeightObjectSpawnThreshold = value; } }
        [SerializeField]
        [Tooltip("How deep below road start height is road texture drawn.")]
        private float roadTextureHeight = 10f;
        public float RoadTextureHeight { get { return roadTextureHeight; } set { roadTextureHeight = value; } }
        [SerializeField]
        [Tooltip("The exponential power applied to road texture. Higher numbers will result in a higher strength.")]
        private float roadTextureStrength = 2f;
        public float RoadTextureStrength { get { return roadTextureStrength; } set { roadTextureStrength = value; } }
        [SerializeField]
        [Tooltip("Offset from road start height that texture is applied at.")]
        private float roadStartHeightTextureBias = 3f;
        public float RoadStartHeightTextureBias { get { return roadStartHeightTextureBias; } set { roadStartHeightTextureBias = value; } }
        public enum RoadElevationMode { Elevation, MinHeight, MaxHeight };
        [SerializeField]
        [Tooltip("How road height is determineded.")]
        private RoadElevationMode roadElevationMode = RoadElevationMode.Elevation;
        [SerializeField]
        [Tooltip("The minium height of terrain that can be deformed by roads.")]
        private float minRoadDeformHeight;
        public float MinRoadDeformHeight { get { return minRoadDeformHeight; } set { minRoadDeformHeight = value; } }
        [SerializeField]
        [Tooltip("The maximum height of terrain that can be deformed by roads.")]
        private float maxRoadDeformHeight;
        public float MaxRoadDeformHeight { get { return maxRoadDeformHeight; } set { maxRoadDeformHeight = value; } }
        [SerializeField]
        [Tooltip("The strength of terrain weight filling underneath roads.")]
        private float roadFillStrength = 1f;
        public float RoadFillStrength { get { return roadFillStrength; } set { roadFillStrength = value; } }
        [SerializeField]
        [Tooltip("The strength of terrain weight carving above roads.")]
        private float roadCarveStrength = 10f;
        public float RoadCarveStrength { get { return roadCarveStrength; } set { roadCarveStrength = value; } }

        //
        //
        //Structure Settings
        //

        [SerializeField]
        [Tooltip("Whether to enable generation of structures throughout terrain.")]
        private bool structuresEnabled = false;
        public bool StructuresEnabled { get { return structuresEnabled; } set { structuresEnabled = value; } }
        [SerializeField]
        [Tooltip("How many chunks around player to check for structures.")]
        private int structureCheckRadius = 50;
        public int StructureCheckRadius { get { return structureCheckRadius; } set { structureCheckRadius = value; } }
        [SerializeField]
        [Tooltip("How many chunks are skipped between each structure check. The higher the number, the less chunks are checked for structures.")]
        private int structureCheckChunkLength = 4;
        public int StructureCheckChunkLength { get { return structureCheckChunkLength; } set { structureCheckChunkLength = value; } }
        [SerializeField]
        [Tooltip("Multiplier applied to structure weight changes")]
        private float structureWeightChangeMultiplier = 4f;
        public float StructureWeightChangeMultiplier { get { return structureWeightChangeMultiplier; } set { structureWeightChangeMultiplier = value; } }
        [SerializeField]
        [Tooltip("The offset applied to the start height of paths between structures")]
        private float structurePathStartHeightOffset = 5f;
        public float StructurePathStartHeightOffset { get { return structurePathStartHeightOffset; } set { structurePathStartHeightOffset = value; } }

        //
        //
        //Debug Settings
        //

        [SerializeField]
        [Tooltip("Whether to enable the advanced inspector. Removes certain safety checks, allowing greater customability. Should only be enabled by users with a solid understanding of the terrain generation algorithms.")]
        private bool enableAdvancedInspector = false;

        #endregion

        #region Internal Variables
        //
        //
        //Internal Variables
        //

        private NativeArray<float> biomeGrid;
        private Dictionary<Vector2Int, TerrainChunk> chunks = new Dictionary<Vector2Int, TerrainChunk>();
        private NativeParallelHashSet<Vector2Int> chunkCoordHeap = new NativeParallelHashSet<Vector2Int>();
        private Dictionary<Vector2Int, TerrainChunk> colliderQueue = new Dictionary<Vector2Int, TerrainChunk>();

        private bool rockNoiseEnabled;
        private bool elevationNoiseEnabled;
        private bool voronoiNoiseEnabled;

        private NativeArray<Vector2> temperatureOctaveOffsets;
        private NativeArray<Vector2> moistureOctaveOffsets;
        private NativeArray<Vector2> vegetationOctaveOffsets;
        private NativeArray<Vector2> rockOctaveOffsets;
        private NativeArray<Vector2> elevationOctaveOffsets;
        private NativeArray<Vector3> voronoiOctaveOffsets;
        private NativeArray<Vector3> octaveOffsets;
        private NativeArray<Vector2> octaveOffsets2d;

        private NativeArray<NoiseValues> biomeVegetationValues;
        private NativeArray<NoiseValues> biomeRockValues;
        private NativeArray<NoiseValues> biomeElevationValues;
        private NativeArray<VoronoiNoiseValues> biomeVoronoiValues;
        private NativeArray<NoiseValues> biomeTerrainValues;

        private NativeArray<RidgedNoisePass> ridgedNoisePassesNative;
        private NativeArray<RidgedNoiseApplicationValues> ridgedNoiseApplicationValuesNative;
        private NativeArray<Vector2> ridgedNoiseOffsets;
        private NativeArray<RidgedNoisePass> roadPassesNative;
        private NativeArray<Vector2> roadNoiseOffsets;
        private NativeArray<float> roadMinHeights;
        private NativeArray<float> roadMaxHeights;

        private NativeArray<float> elevationCurveSamples;
        private int elevationCurveSampleCount = 100000;
        private NativeArray<float> floorWeightCurveSamples;
        private int floorWeightCurveSampleCount = 100000;
        private NativeArray<float> voronoiSteepnessSamples;
        private int voronoiSteepnessSampleCount = 50000;
        private NativeArray<float> ridgedNoiseHeightDistSamples;
        private int ridgedCurveSampleCount = 50000;

        //Compiled list of all terrain objects with unique index assigned to each
        private Dictionary<int, TerrainObjectInstance> compiledTerrainObjects =
            new Dictionary<int, TerrainObjectInstance>();
        //Compiled list of all terrain objects, converted to structs for use in jobs
        private NativeParallelHashMap<int, TerrainObjectInstanceStruct> compiledTerrainObjectStructs;

        //Compiled list of all terrain structures with unique index assigned to each
        private Dictionary<int, TerrainStructureInstance> compiledTerrainStructures =
            new Dictionary<int, TerrainStructureInstance>();
        //Compiled list of all terrain structure prefabs with unique index assigned to each
        private Dictionary<int, TerrainStructurePrefab> compiledTerrainStructurePrefabs =
            new Dictionary<int, TerrainStructurePrefab>();
        private List<NativeArray<TerrainStructurePrefabStruct>> terrainStructurePrefabStructs =
            new List<NativeArray<TerrainStructurePrefabStruct>>();

        private NativeParallelMultiHashMap<Vector2Int, PersistentWeightChange> persistentWeightChanges =
            new NativeParallelMultiHashMap<Vector2Int, PersistentWeightChange>();
        private NativeParallelHashMap<HashableIndex2, PersistentRoadChange> persistentRoadWeightChanges =
            new NativeParallelHashMap<HashableIndex2, PersistentRoadChange>();
        private NativeParallelMultiHashMap<Vector2Int, TerrainStructureTransform> persistentStructureTransforms =
            new NativeParallelMultiHashMap<Vector2Int, TerrainStructureTransform>();
        private NativeParallelMultiHashMap<Vector2Int, InfluenceBound> persistentInfluenceBounds =
            new NativeParallelMultiHashMap<Vector2Int, InfluenceBound>();

        private HashSet<Vector2Int> checkedStructureCoords = new HashSet<Vector2Int>();

        //Events
        public delegate void ChunkGeneratedEventHandler(object source, TerrainChunk args);
        public event ChunkGeneratedEventHandler ChunkGenerated;

        public delegate void TextureNoiseCalculatedEventHandler(object source, ref NativeArray<float> textureNoise,
            Vector2Int size, Vector2Int coords);
        public event TextureNoiseCalculatedEventHandler TextureNoiseCalculated;

        public delegate void ObjectNoiseCalculatedEventHandler(object source, ref NativeArray<float> objectNoise,
            Vector2Int size, Vector2Int coords);
        public event ObjectNoiseCalculatedEventHandler ObjectNoiseCalculated;

        public delegate void ElevationNoiseCalculatedEventHandler(object source, ref NativeArray<Vector2> elevationNoise,
            Vector2Int size, Vector2Int coords);
        public event ElevationNoiseCalculatedEventHandler ElevationNoiseCalculated;

        public delegate void RidgedNoiseCalculatedEventHandler(object source, ref NativeArray<float> ridgedNoise,
            Vector2Int size, int ridgedNoisePassCount, Vector2Int coords);
        public event RidgedNoiseCalculatedEventHandler RidgedNoiseCalculated;

        public delegate void TerrainNoiseCalculatedEventHandler(object source, ref NativeArray<float> terrainNoise,
            Vector3Int size, Vector2Int coords);
        public event TerrainNoiseCalculatedEventHandler TerrainNoiseCalculated;

        public delegate void RoadsCalculatedEventHandler(object source, ref NativeArray<float> roadWeights,
        ref NativeArray<float> roadStartHeights, Vector2Int size, Vector2Int coords);
        public event RoadsCalculatedEventHandler RoadsCalculated;

        public delegate void MeshCalculatedEventHandler(object source, ref NativeList<Vector3> verts,
            ref NativeList<int> trigs, Vector2Int coords);
        public event MeshCalculatedEventHandler MeshCalculated;

        public delegate void ObjectTransformsCalculatedEventHandler(object source,
            Dictionary<int, TerrainTransformCollection> transforms, Vector2Int coords);
        public event ObjectTransformsCalculatedEventHandler ObjectTransformsCalculated;

        public delegate void ChunkDespawnedEventHandler(object source, TerrainChunk chunk);
        public event ChunkDespawnedEventHandler ChunkDespawned;

        #endregion

        #region Initialization

        //
        //
        //Initialization
        //
        private void Start()
        {
            if (generateAtRuntime)
            {
                Initialize();
            }
        }

        private void Initialize()
        {
            InitializeCollections();

            InitializeNoise();

            GenerateBiomeGrid();

            PopulateObjectPools();

            TerrainMatSetup(ref terrainBiomes, biomeGridSize, ref biomeGrid);
        }

        private void Initialize(bool populateObjectPools, bool materialSetup)
        {
            InitializeCollections();

            InitializeNoise();

            GenerateBiomeGrid();

            if (populateObjectPools)
            {
                PopulateObjectPools();
            }
            if (materialSetup)
            {
                TerrainMatSetup(ref terrainBiomes, biomeGridSize, ref biomeGrid);
            }
        }

        private void InitializeCollections()
        {
            layerCount = 0;
            textureCountPerLayer = 0;

            float biomeBlendPercent = biomeBlend / (float)biomeGridSize;

            int objectIndex = 0;
            int structureIndex = 0;
            int structurePrefabIndex = 0;
            //Update biome/object/structure indices
            compiledTerrainObjectStructs =
                new NativeParallelHashMap<int, TerrainObjectInstanceStruct>(1000, Allocator.Persistent);

            for (int i = 0; i < terrainBiomes.Count; i++)
            {
                TerrainBiome biome = terrainBiomes[i];
                foreach (TerrainObjectInstance terrainObjectInstance in biome.TerrainObjects)
                {
                    terrainObjectInstance.biome = i;
                    terrainObjectInstance.index = objectIndex;
                    terrainObjectInstance.rockNoiseEnabled = biome.BiomeValues.rockNoiseEnabled;

                    compiledTerrainObjects.Add(objectIndex, terrainObjectInstance);
                    compiledTerrainObjectStructs.Add(objectIndex,
                        new TerrainObjectInstanceStruct(terrainObjectInstance));
                    objectIndex++;
                }

                foreach (TerrainStructureInstance terrainStructureInstance in biome.TerrainStructures)
                {
                    terrainStructureInstance.biome = i;
                    terrainStructureInstance.index = structureIndex;

                    compiledTerrainStructures.Add(structureIndex, terrainStructureInstance);
                    structureIndex++;

                    NativeArray<TerrainStructurePrefabStruct> terrainStructurePrefabs = new
                        NativeArray<TerrainStructurePrefabStruct>(terrainStructureInstance.terrainStructurePrefabs.Count,
                        Allocator.Persistent);
                    foreach (TerrainStructurePrefab terrainStructurePrefab in terrainStructureInstance.terrainStructurePrefabs)
                    {
                        terrainStructurePrefab.index = structurePrefabIndex;

                        compiledTerrainStructurePrefabs.Add(structurePrefabIndex, terrainStructurePrefab);
                        structurePrefabIndex++;

                        terrainStructurePrefabs[terrainStructureInstance.terrainStructurePrefabs.
                            IndexOf(terrainStructurePrefab)] = new TerrainStructurePrefabStruct(terrainStructurePrefab);
                    }
                    terrainStructurePrefabStructs.Add(terrainStructurePrefabs);
                }
                terrainBiomes[i].Index = i;

                //Adjust biome bound edges to prevent blurring
                foreach (BiomeBound bound in biome.Bounds)
                {
                    float temperatureMin = bound.TemperatureMinMax.x;
                    if (temperatureMin <= 0f)
                    {
                        temperatureMin = -biomeBlendPercent;
                    }
                    float temperatureMax = bound.TemperatureMinMax.y;
                    if (temperatureMax >= 1f)
                    {
                        temperatureMax = 1f + biomeBlendPercent;
                    }
                    bound.InternalTemperatureMinMax = new Vector2(temperatureMin, temperatureMax);

                    float moistureMin = bound.MoistureMinMax.x;
                    if (moistureMin <= 0f)
                    {
                        moistureMin = -biomeBlendPercent;
                    }
                    float moistureMax = bound.MoistureMinMax.y;
                    if (moistureMax >= 1f)
                    {
                        moistureMax = 1f + biomeBlendPercent;
                    }
                    bound.InternalMoistureMinMax = new Vector2(moistureMin, moistureMax);
                }

                //Calculate layer count and texture count based on maximum of all biomes' layers
                layerCount = Mathf.Max(layerCount, biome.TerrainLayers.Count);
                foreach (TextureLayer layer in biome.TerrainLayers)
                {
                    textureCountPerLayer = Mathf.Max(textureCountPerLayer, layer.textures.Count);
                }
            }

            persistentStructureTransforms =
                new NativeParallelMultiHashMap<Vector2Int, TerrainStructureTransform>(1000, Allocator.Persistent);
            persistentInfluenceBounds =
                new NativeParallelMultiHashMap<Vector2Int, InfluenceBound>(1000, Allocator.Persistent);
            persistentWeightChanges =
                new NativeParallelMultiHashMap<Vector2Int, PersistentWeightChange>(100000, Allocator.Persistent);
            persistentRoadWeightChanges =
                new NativeParallelHashMap<HashableIndex2, PersistentRoadChange>(1000, Allocator.Persistent);

            //Initialize misc. native containers
            chunkCoordHeap = new NativeParallelHashSet<Vector2Int>(10000, Allocator.Persistent);
            colliderQueue = new Dictionary<Vector2Int, TerrainChunk>();
            chunks = new Dictionary<Vector2Int, TerrainChunk>();

            //Misc. data validation
            size3D = size3DExposed + Vector3Int.one;

            if (randomizeSeed)
            {
                //Randomize seed
                seed = UnityEngine.Random.Range(-10000, 10000);
                //Ensure seed is not zero
                seed = seed == 0 ? 1 : seed;
            }
        }

        private void InitializeNoise()
        {
            System.Random prng = new System.Random(seed);

            //
            //
            //Calculate biome noise octave offsets
            //

            temperatureOctaveOffsets = new NativeArray<Vector2>(temperatureNoiseValues.octaves, Allocator.Persistent);
            moistureOctaveOffsets = new NativeArray<Vector2>(moistureNoiseValues.octaves, Allocator.Persistent);
            for (int i = 0; i < temperatureNoiseValues.octaves; i++)
            {
                temperatureOctaveOffsets[i] = new Vector2(prng.Next(-10000, 10000), prng.Next(-10000, 10000));
            }
            for (int i = 0; i < moistureNoiseValues.octaves; i++)
            {
                moistureOctaveOffsets[i] = new Vector2(prng.Next(-10000, 10000), prng.Next(-10000, 10000));
            }


            //
            //
            //Calculate maximum octaves
            //

            int maxBiomeVegetationOctaves = 0;
            int maxBiomeRockOctaves = 0;
            int maxBiomeElevationOctaves = 0;
            int maxBiomeVoronoiOctaves = 0;
            int maxBiomeTerrainOctaves = 0;
            rockNoiseEnabled = false;
            elevationNoiseEnabled = false;
            voronoiNoiseEnabled = false;

            foreach (TerrainBiome biome in terrainBiomes)
            {
                maxBiomeVegetationOctaves = Mathf.Max(biome.BiomeValues.biomeVegetationValues.octaves, maxBiomeVegetationOctaves);
                maxBiomeRockOctaves = Mathf.Max(biome.BiomeValues.biomeRockValues.octaves, maxBiomeRockOctaves);
                maxBiomeElevationOctaves = Mathf.Max(biome.BiomeValues.biomeElevationValues.octaves, maxBiomeElevationOctaves);
                maxBiomeVoronoiOctaves = Mathf.Max(biome.BiomeValues.biomeVoronoiValues.voronoiOctaves, maxBiomeVoronoiOctaves);
                maxBiomeTerrainOctaves = Mathf.Max(biome.BiomeValues.biomeTerrainValues.octaves, maxBiomeTerrainOctaves);

                rockNoiseEnabled = biome.BiomeValues.rockNoiseEnabled ? true : rockNoiseEnabled;
                elevationNoiseEnabled = biome.BiomeValues.elevationNoiseEnabled ? true : elevationNoiseEnabled;
                voronoiNoiseEnabled = biome.BiomeValues.voronoiNoiseEnabled ? true : voronoiNoiseEnabled;
            }

            //
            //
            //Calculate octave offsets 
            //

            vegetationOctaveOffsets = new NativeArray<Vector2>(maxBiomeVegetationOctaves, Allocator.Persistent);
            rockOctaveOffsets = new NativeArray<Vector2>(maxBiomeRockOctaves, Allocator.Persistent);
            elevationOctaveOffsets = new NativeArray<Vector2>(maxBiomeElevationOctaves, Allocator.Persistent);
            voronoiOctaveOffsets = new NativeArray<Vector3>(maxBiomeVoronoiOctaves, Allocator.Persistent);
            octaveOffsets = new NativeArray<Vector3>(maxBiomeTerrainOctaves, Allocator.Persistent);


            for (int i = 0; i < maxBiomeVegetationOctaves; i++)
            {
                vegetationOctaveOffsets[i] = new Vector2(prng.Next(-10000, 10000), prng.Next(-10000, 10000));
            }
            for (int i = 0; i < maxBiomeRockOctaves; i++)
            {
                rockOctaveOffsets[i] = new Vector2(prng.Next(-10000, 10000), prng.Next(-10000, 10000));
            }
            for (int i = 0; i < maxBiomeElevationOctaves; i++)
            {
                elevationOctaveOffsets[i] = new Vector2(prng.Next(-10000, 10000), prng.Next(-10000, 10000));
            }
            for (int i = 0; i < maxBiomeVoronoiOctaves; i++)
            {
                voronoiOctaveOffsets[i] = new Vector3(prng.Next(-10000, 10000), prng.Next(-10000, 10000), prng.Next(-10000, 10000));
            }
            for (int i = 0; i < maxBiomeTerrainOctaves; i++)
            {
                octaveOffsets[i] = new Vector3(prng.Next(-10000, 10000), prng.Next(-10000, 10000), prng.Next(-10000, 10000));
            }

            //
            //
            //Compile noise values from each biome
            //

            int biomeCount = terrainBiomes.Count;
            biomeVegetationValues = new NativeArray<NoiseValues>(biomeCount, Allocator.Persistent);
            biomeElevationValues = new NativeArray<NoiseValues>(biomeCount, Allocator.Persistent);
            biomeTerrainValues = new NativeArray<NoiseValues>(biomeCount, Allocator.Persistent);
            biomeRockValues = new NativeArray<NoiseValues>(biomeCount, Allocator.Persistent);
            biomeVoronoiValues = new NativeArray<VoronoiNoiseValues>(biomeCount, Allocator.Persistent);
            for (int i = 0; i < biomeCount; i++)
            {
                TerrainBiome biome = terrainBiomes[i];
                BiomeValues values = biome.BiomeValues;
                values.biomeVegetationValues.enabled = true;
                biomeVegetationValues[i] = values.biomeVegetationValues;
                values.biomeElevationValues.enabled = values.elevationNoiseEnabled;
                biomeElevationValues[i] = values.biomeElevationValues;
                values.biomeTerrainValues.enabled = true;
                biomeTerrainValues[i] = values.biomeTerrainValues;
                values.biomeRockValues.enabled = values.rockNoiseEnabled;
                biomeRockValues[i] = values.biomeRockValues;
                values.biomeVoronoiValues.enabled = values.voronoiNoiseEnabled;
                biomeVoronoiValues[i] = values.biomeVoronoiValues;
            }


            //
            //
            //Calculate elevation and floor curve samples
            //

            elevationCurveSamples =
                new NativeArray<float>(elevationCurveSampleCount * biomeCount, Allocator.Persistent);
            floorWeightCurveSamples =
                new NativeArray<float>(floorWeightCurveSampleCount * biomeCount, Allocator.Persistent);
            for (int i = 0; i < biomeCount; i++)
            {
                //Calculate elevation noise curve samples from this biome and append to array
                TerrainBiome biome = terrainBiomes[i];

                for (int l = 0; l < elevationCurveSampleCount; l++)
                {
                    int passIndex = i * elevationCurveSampleCount;
                    elevationCurveSamples[l + passIndex] =
                        biome.BiomeValues.elevationCurve.Evaluate(l / (float)elevationCurveSampleCount);
                }
                for (int l = 0; l < floorWeightCurveSampleCount; l++)
                {
                    int passIndex = i * floorWeightCurveSampleCount;
                    floorWeightCurveSamples[l + passIndex] =
                        biome.BiomeValues.floorWeightCurve.Evaluate(l / (float)floorWeightCurveSampleCount);
                }
            }


            //
            //
            //Convert ridged noise/road noise pass lists to native array 
            //

            ridgedNoisePassesNative = new NativeArray<RidgedNoisePass>(ridgedNoiseValues.Count, Allocator.Persistent);
            for (int i = 0; i < ridgedNoiseValues.Count; i++)
            {
                ridgedNoisePassesNative[i] = ridgedNoiseValues[i].ridgedNoisePass;
            }

            ridgedNoiseApplicationValuesNative =
                new NativeArray<RidgedNoiseApplicationValues>(ridgedNoiseValues.Count, Allocator.Persistent);
            for (int i = 0; i < ridgedNoiseValues.Count; i++)
            {
                ridgedNoiseApplicationValuesNative[i] = ridgedNoiseValues[i].ridgedNoiseApplicationValues;
            }


            roadPassesNative = new NativeArray<RidgedNoisePass>(roadRidgedNoisePasses.Count, Allocator.Persistent);
            for (int i = 0; i < roadRidgedNoisePasses.Count; i++)
            {
                roadPassesNative[i] = roadRidgedNoisePasses[i].ridgedNoisePass;
            }
            roadMinHeights = new NativeArray<float>(roadRidgedNoisePasses.Count, Allocator.Persistent);
            roadMaxHeights = new NativeArray<float>(roadRidgedNoisePasses.Count, Allocator.Persistent);
            for (int i = 0; i < roadRidgedNoisePasses.Count; i++)
            {
                roadMinHeights[i] = roadRidgedNoisePasses[i].minHeight;
                roadMaxHeights[i] = roadRidgedNoisePasses[i].maxHeight;
            }

            //
            //
            //Calculate ridged noise/road noise offsets
            //

            ridgedNoiseOffsets = new NativeArray<Vector2>(ridgedNoiseValues.Count, Allocator.Persistent);
            for (int i = 0; i < ridgedNoiseValues.Count; i++)
            {
                ridgedNoiseOffsets[i] = new Vector2(prng.Next(-10000, 10000), prng.Next(-10000, 10000));
            }

            roadNoiseOffsets = new NativeArray<Vector2>(roadRidgedNoisePasses.Count, Allocator.Persistent);
            for (int i = 0; i < roadRidgedNoisePasses.Count; i++)
            {
                roadNoiseOffsets[i] = new Vector2(prng.Next(-10000, 10000), prng.Next(-10000, 10000));
            }

            //
            //
            //Compile ridged noise height dist curve samples from all passes into one array to avoid nested array restriction
            //

            ridgedNoiseHeightDistSamples =
                new NativeArray<float>(ridgedNoiseValues.Count * ridgedCurveSampleCount, Allocator.Persistent);
            for (int i = 0; i < ridgedNoiseValues.Count; i++)
            {
                int passIndex = i * ridgedCurveSampleCount;
                for (int l = 0; l < ridgedCurveSampleCount; l++)
                {
                    ridgedNoiseHeightDistSamples[l + passIndex] =
                        ridgedNoiseValues[i].ridgedNoiseHeightDistCurve.Evaluate(l / (float)ridgedCurveSampleCount);
                }
            }

            //
            //
            //Compile voronoi steepness curve samples from all passes into one array
            //

            voronoiSteepnessSamples = new NativeArray<float>(voronoiSteepnessSampleCount * biomeCount, Allocator.Persistent);
            for (int i = 0; i < biomeCount; i++)
            {
                TerrainBiome biome = terrainBiomes[i];
                int passIndex = i * voronoiSteepnessSampleCount;
                for (int l = 0; l < voronoiSteepnessSampleCount; l++)
                {
                    voronoiSteepnessSamples[l + passIndex] =
                        biome.BiomeValues.voronoiSteepnessCurve.Evaluate(l / (float)voronoiSteepnessSampleCount);
                }
            }
        }

        private void PopulateObjectPools()
        {
            foreach (TerrainBiome biome in terrainBiomes)
            {
                foreach (TerrainObjectInstance terrainObject in biome.TerrainObjects)
                {
                    if (!terrainObject.combineMesh)
                    {
                        if (ObjectPoolManager.PoolExists(terrainObject.prefab.name) == false)
                        {
                            ObjectPoolManager.CreatePool(terrainObject.prefab, 1000);
                        }
                        else
                        {
                            ObjectPool pool = ObjectPoolManager.pools[terrainObject.prefab.name];
                            pool.DeactivateObjects(pool.activePool.ToList());
                        }
                    }
                }
                foreach (TerrainStructureInstance terrainStructureInstance in biome.TerrainStructures)
                {
                    foreach (TerrainStructurePrefab terrainStructurePrefab in
                        terrainStructureInstance.terrainStructurePrefabs)
                    {
                        if (ObjectPoolManager.PoolExists(terrainStructurePrefab.prefab.name) == false)
                        {
                            ObjectPoolManager.CreatePool(terrainStructurePrefab.prefab, 1000);
                        }
                        else
                        {
                            ObjectPool pool = ObjectPoolManager.pools[terrainStructurePrefab.prefab.name];
                            pool.DeactivateObjects(pool.activePool.ToList());
                        }
                    }
                }
            }
        }

        private void GenerateBiomeGrid()
        {
            float biomeBlendPercent = biomeBlend / (float)biomeGridSize;

            //Biome indices based on temperature and moisture values
            biomeGrid =
                new NativeArray<float>((biomeGridSize + 1) * (biomeGridSize + 1) * terrainBiomes.Count, Allocator.Persistent);

            for (int x = 0; x <= biomeGridSize; x++)
            {
                for (int y = 0; y <= biomeGridSize; y++)
                {
                    float currentX = x / (float)biomeGridSize;
                    float currentY = y / (float)biomeGridSize;

                    float[] biomeWeights = new float[terrainBiomes.Count];

                    //Calculate correct biome
                    for (int i = 0; i < terrainBiomes.Count; i++)
                    {
                        TerrainBiome biome = terrainBiomes[i];

                        float highestWeight = 0f;
                        foreach (BiomeBound bound in biome.Bounds)
                        {
                            Vector2 tempMinMax = bound.InternalTemperatureMinMax;
                            Vector2 moistureMinMax = bound.InternalMoistureMinMax;
                            float leftWeight = Mathf.InverseLerp(tempMinMax.x - biomeBlendPercent,
                                tempMinMax.x + biomeBlendPercent, currentX);
                            float rightWeight = Mathf.InverseLerp(tempMinMax.y + biomeBlendPercent,
                                tempMinMax.y - biomeBlendPercent, currentX);
                            float bottomWeight = Mathf.InverseLerp(moistureMinMax.x - biomeBlendPercent,
                                moistureMinMax.x + biomeBlendPercent, currentY);
                            float topWeight = Mathf.InverseLerp(moistureMinMax.y + biomeBlendPercent,
                                moistureMinMax.y - biomeBlendPercent, currentY);

                            float weight = Mathf.Min(leftWeight, Mathf.Min(rightWeight, Mathf.Min(bottomWeight, topWeight)));
                            highestWeight = Mathf.Max(weight, highestWeight);
                        }

                        biomeWeights[i] = highestWeight;
                    }

                    //Calculate weighted average sum
                    float sum = 0.00001f;
                    foreach (float weight in biomeWeights)
                    {
                        sum += weight;
                    }
                    //Catch any missing biomes
                    if (sum == 0.00001f)
                    {
                        biomeWeights[0] = 1f;
                        sum = 1f;
                    }

                    //Apply weighted averages
                    for (int i = 0; i < biomeWeights.Length; i++)
                    {
                        biomeWeights[i] = biomeWeights[i] / sum;
                        biomeGrid[(x * (biomeGridSize + 1) + y) * terrainBiomes.Count + i] = biomeWeights[i];
                    }
                }
            }
        }

        private void TerrainMatSetup(ref List<TerrainBiome> terrainBiomes, int biomeGridSize,
            ref NativeArray<float> biomeGrid)
        {
            int biomeCount = terrainBiomes.Count;

            terrainMat.SetFloat("_TextureCount", textureCountPerLayer);
            terrainMat.SetFloat("_LayerCount", layerCount);
            terrainMat.SetFloat("_BiomeCount", biomeCount);
            terrainMat.SetFloat("_MaxHeight", size3D.y);
            terrainMat.SetFloat("_SteepnessThreshold", steepnessThreshold);
            terrainMat.SetFloat("_SecondSteepnessThreshold", secondSteepnessThreshold);
            terrainMat.SetFloat("_BlendDistance", textureBlendDistance);
            terrainMat.SetFloat("_SteepnessBlendDistance", textureSteepnessBlendDistance);
            terrainMat.SetFloat("_SecondSteepnessBlendDistance", textureSecondSteepnessBlendDistance);
            terrainMat.SetFloat("_VegetationBlend", vegetationBlendDistance);
            terrainMat.SetInt("_BiomeGridSize", biomeGridSize);
            terrainMat.SetFloat("_RoadHeight", roadTextureHeight);
            terrainMat.SetFloat("_RoadStartHeightBias", roadStartHeightTextureBias);
            terrainMat.SetFloat("_RoadTextureBlend", roadTextureStrength);

            //Create biome grid texture to pass to shader
            Texture2D biomeGridTexture = new Texture2D(biomeGridSize, biomeGridSize,
                textureFormat, false, true);
            for (int x = 0; x < biomeGridSize + 1; x++)
            {
                for (int y = 0; y < biomeGridSize + 1; y++)
                {
                    float highestWeight = 0f;
                    int highestIndex = 0;
                    for (int i = 0; i < biomeCount; i++)
                    {
                        float weight = biomeGrid[(x * (biomeGridSize + 1) + y) * biomeCount + i];
                        if (weight > highestWeight)
                        {
                            highestWeight = weight;
                            highestIndex = i;
                        }
                    }

                    biomeGridTexture.SetPixel(x, y,
                        new Color(Mathf.InverseLerp(0, biomeCount, highestIndex), 0, 0));
                }
            }
            biomeGridTexture.Apply();
            terrainMat.SetTexture("_BiomeGrid", biomeGridTexture);

            //Create texture index lookup texture so textures can be used multiple times without having to exist in textures array twice
            int totalTextureCount = biomeCount * layerCount * textureCountPerLayer + biomeCount * 2;
            int textureNum = 0;
            int textureIndex = 0;
            Texture2D textureIndices = new Texture2D(totalTextureCount, totalTextureCount, TextureFormat.RHalf, false, true);
            List<Texture2D> textureList = new List<Texture2D>();
            for (int i = 0; i < biomeCount; i++)
            {
                //Get all albedo, mask, normal, and displacement maps
                TerrainBiome biome = terrainBiomes[i];
                for (int x = 0; x < layerCount; x++)
                {
                    int layerIndex = Mathf.Clamp(x, 0, biome.TerrainLayers.Count - 1);

                    for (int y = 0; y < textureCountPerLayer; y++)
                    {
                        int index = Mathf.Clamp(y, 0, biome.TerrainLayers[layerIndex].textures.Count - 1);

                        textureNum += AddTextureIndex(biome.TerrainLayers[layerIndex].textures[index].textureSet,
                            textureList, textureIndices, textureIndex, textureNum);
                        textureIndex++;
                    }
                }

                //Get all steepness textures
                textureNum += AddTextureIndex(biome.SteepnessTexture, textureList,
                    textureIndices, textureIndex, textureNum);
                textureIndex++;
                textureNum += AddTextureIndex(biome.SecondSteepnessTexture, textureList,
                    textureIndices, textureIndex, textureNum);
                textureIndex++;
            }
            textureIndices.Apply();

            //Assemble base color, mask map, normal, and displacement maps 
            Texture2D[] textures = new Texture2D[textureNum];
            Texture2D[] maskMaps = new Texture2D[textureNum];
            Texture2D[] normals = new Texture2D[textureNum];
            int textureIterator = 0;
            for (int i = 0; i < biomeCount; i++)
            {
                TerrainBiome biome = terrainBiomes[i];
                for (int x = 0; x < layerCount; x++)
                {
                    int layerIndex = Mathf.Clamp(x, 0, biome.TerrainLayers.Count - 1);
                    for (int y = 0; y < textureCountPerLayer; y++)
                    {
                        int index = Mathf.Clamp(y, 0, biome.TerrainLayers[layerIndex].textures.Count - 1);
                        Texture2D baseColor = biome.TerrainLayers[layerIndex].textures[index].textureSet.baseColor;
                        Texture2D maskMap = biome.TerrainLayers[layerIndex].textures[index].textureSet.maskMap;
                        Texture2D normalMap = biome.TerrainLayers[layerIndex].textures[index].textureSet.normalMap;

                        textureIterator += AddTextureSet(baseColor, maskMap, normalMap, textureIterator,
                            textures, maskMaps, normals);
                    }
                }

                textureIterator += AddTextureSet(biome.SteepnessTexture.baseColor, biome.SteepnessTexture.maskMap,
                    biome.SteepnessTexture.normalMap, textureIterator, textures, maskMaps, normals);
                textureIterator += AddTextureSet(biome.SecondSteepnessTexture.baseColor, biome.SecondSteepnessTexture.maskMap,
                    biome.SecondSteepnessTexture.normalMap, textureIterator, textures, maskMaps, normals);
            }

            Texture2D[] startHeightsArr = new Texture2D[biomeCount];
            Texture2D[] vegetationNoiseHeights = new Texture2D[layerCount * biomeCount];
            Texture2D[] textureScales = new Texture2D[biomeCount * layerCount + 3];
            Texture2D[] roadTextures = new Texture2D[biomeCount];
            Texture2D[] roadNormals = new Texture2D[biomeCount];
            Texture2D[] roadMaskMaps = new Texture2D[biomeCount];

            int scaleTextureWidth = Mathf.Max(biomeCount, textureCountPerLayer);

            //Steepness scales
            textureScales[biomeCount * layerCount] =
                new Texture2D(scaleTextureWidth, scaleTextureWidth, textureFormat, false, true);

            //Second steepness scales
            textureScales[biomeCount * layerCount + 1] =
                new Texture2D(scaleTextureWidth, scaleTextureWidth, textureFormat, false, true);

            //Road scales
            textureScales[biomeCount * layerCount + 2] =
                new Texture2D(scaleTextureWidth, scaleTextureWidth, textureFormat, false, true);
            for (int i = 0; i < biomeCount; i++)
            {
                startHeightsArr[i] = new Texture2D(layerCount, layerCount, TextureFormat.RHalf, false, true);
                TerrainBiome biome = terrainBiomes[i];

                for (int x = 0; x < layerCount; x++)
                {
                    int layerIndex = Mathf.Clamp(x, 0, biome.TerrainLayers.Count - 1);

                    textureScales[i * layerCount + x] = new Texture2D(scaleTextureWidth, scaleTextureWidth,
                        TextureFormat.RHalf, false, true);

                    startHeightsArr[i].SetPixel(x, 0,
                        new Color(biome.TerrainLayers[layerIndex].startHeight, 0f, 0f));
                    vegetationNoiseHeights[i * layerCount + x] = new Texture2D(textureCountPerLayer,
                        textureCountPerLayer, TextureFormat.RHalf, false, true);

                    for (int l = 0; l < textureCountPerLayer; l++)
                    {
                        int index = Mathf.Clamp(l, 0, biome.TerrainLayers[layerIndex].textures.Count - 1);

                        float vegetationStartHeight;
                        if (index != l)
                        {
                            vegetationStartHeight = 1f + vegetationBlendDistance * 2f;
                        }
                        else
                        {
                            vegetationStartHeight = biome.TerrainLayers[layerIndex].textures[index].vegetationStartHeight;
                        }

                        vegetationNoiseHeights[i * layerCount + x].SetPixel(l, 0, new Color(vegetationStartHeight, 0, 0));

                        textureScales[i * layerCount + x].SetPixel(l, 0,
                            new Color(biome.TerrainLayers[layerIndex].textures[index].textureSet.textureScale / 100f, 0, 0));
                    }

                    vegetationNoiseHeights[i * layerCount + x].Apply();
                }

                CheckNullTexture(biome.RoadTexture);
                roadTextures[i] = biome.RoadTexture.baseColor;
                roadNormals[i] = biome.RoadTexture.normalMap;
                roadMaskMaps[i] = biome.RoadTexture.maskMap;

                textureScales[biomeCount * layerCount].SetPixel(i, 0,
                    new Color(biome.SteepnessTexture.textureScale / 100f, 0, 0));
                textureScales[biomeCount * layerCount + 1].SetPixel(i, 0,
                    new Color(biome.SecondSteepnessTexture.textureScale / 100f, 0, 0));
                textureScales[biomeCount * layerCount + 2].SetPixel(i, 0,
                    new Color(biome.RoadTexture.textureScale / 100f, 0, 0));
                startHeightsArr[i].Apply();
            }
            textureScales[biomeCount * layerCount].Apply();
            textureScales[biomeCount * layerCount + 1].Apply();
            textureScales[biomeCount * layerCount + 2].Apply();

            terrainMat.SetTexture("_StartHeights", GenerateTextureArray(startHeightsArr, layerCount,
                TextureFormat.RHalf, false, true));
            terrainMat.SetTexture("_VegetationStartHeights", GenerateTextureArray(vegetationNoiseHeights,
                textureCountPerLayer, textureFormat, false, true));
            terrainMat.SetTexture("_TextureScales", GenerateTextureArray(textureScales, textureCountPerLayer,
                TextureFormat.RHalf, true, true));
            terrainMat.SetTexture("_TextureIndices", textureIndices);
            terrainMat.SetTexture("_Textures", GenerateTextureArray(textures, texSize,
                textureFormat, true, false));
            terrainMat.SetTexture("_MaskMaps", GenerateTextureArray(maskMaps, texSize / 2,
                textureFormat, true, true));
            terrainMat.SetTexture("_Normals", GenerateTextureArray(normals, texSize / 2,
                textureFormat, true, true));
            terrainMat.SetTexture("_RoadTextures", GenerateTextureArray(roadTextures, texSize,
                textureFormat, true, false));
            terrainMat.SetTexture("_RoadMaskMaps", GenerateTextureArray(roadMaskMaps, texSize / 2,
                textureFormat, true, true));
            terrainMat.SetTexture("_RoadNormals", GenerateTextureArray(roadNormals, texSize / 2,
                textureFormat, true, true));
        }
        #endregion

        #region Generation Loop
        //
        //
        //Generation Loop
        //
        private void Update()
        {
            if (generateAtRuntime)
            {
                Vector3 origin = (generationMode == GenerationMode.Infinite ? player.transform.position : gameObject.transform.position) +
                    new Vector3(originOffset.x, 0f, originOffset.y);
                Vector2Int originCoords = new Vector2Int((int)(origin.x / (size3D.x - 1)), (int)(origin.z / (size3D.z - 1)));

                //
                //
                //Culling
                //

                foreach (KeyValuePair<Vector2Int, TerrainChunk> chunk in chunks.ToArray())
                {
                    //Despawn out-of-bounds chunks
                    if (chunk.Key.x < originCoords.x - chunkRadius || chunk.Key.x > originCoords.x + chunkRadius ||
                        chunk.Key.y < originCoords.y - chunkRadius || chunk.Key.y > originCoords.y + chunkRadius)
                    {
                        DespawnChunk(chunk.Value);
                    }
                    //Generate chunk objects
                    else
                    {
                        float chunkDist = Vector2Int.Distance(chunk.Key, originCoords);
                        //Re-calculate chunk object LOD's
                        foreach (KeyValuePair<int, TerrainTransformCollection> collection in chunk.Value.TerrainObjectTransforms)
                        {
                            if (chunkDist > compiledTerrainObjects[collection.Key].cullDistance && collection.Value.spawned)
                            {
                                DespawnObjects(collection);
                            }
                            else if (chunkDist < compiledTerrainObjects[collection.Key].cullDistance && !collection.Value.spawned)
                            {
                                SpawnObjects(collection, chunk.Value.ChunkObject.transform);
                            }
                        }
                        foreach (KeyValuePair<int, TerrainStructureCollection> collection in chunk.Value.TerrainStructureTransforms)
                        {
                            if (chunkDist > compiledTerrainStructurePrefabs[collection.Key].cullDistance &&
                                collection.Value.spawned)
                            {
                                DespawnObjects(collection);
                            }
                            else if (chunkDist < compiledTerrainStructurePrefabs[collection.Key].cullDistance &&
                                !collection.Value.spawned)
                            {
                                SpawnObjects(collection, chunk.Value.ChunkObject.transform);
                            }
                        }
                    }
                }

                //
                //
                //Structure Generation
                //

                if (structuresEnabled)
                {
                    Vector2Int structureOrigin = originCoords / structureCheckChunkLength * structureCheckChunkLength;
                    //Check chunks around player for structures
                    Dictionary<Vector2Int, TerrainStructureInstance> structureChunks =
                        new Dictionary<Vector2Int, TerrainStructureInstance>();
                    for (int x = -structureCheckRadius; x < structureCheckRadius; x++)
                    {
                        for (int y = -structureCheckRadius; y < structureCheckRadius; y++)
                        {
                            Vector2Int coords = new Vector2Int(x * structureCheckChunkLength,
                                y * structureCheckChunkLength) + structureOrigin;

                            if (checkedStructureCoords.Contains(coords) == false)
                            {
                                //Check whether this chunk should have a structure
                                uint coordSeed = (uint)(TerrainManagerUtility.GetSeed(coords) + seed);
                                Unity.Mathematics.Random rand = new Unity.Mathematics.Random(coordSeed);

                                int biome = GetStrongestBiome(coords);
                                float roadSample = GetRoadSample(coords);
                                float elevationSample = GetElevationSample(coords).x;

                                //Determine which structure should be generated, if any fit this chunk  
                                foreach (TerrainStructureInstance structure in
                                    terrainBiomes[biome].TerrainStructures)
                                {
                                    if ((!roadsEnabled || roadSample >= structure.minRoadWeight) &&
                                        (!terrainBiomes[biome].BiomeValues.elevationNoiseEnabled ||
                                        elevationSample >= structure.minElevation &&
                                        elevationSample <= structure.maxElevation) &&
                                        rand.NextFloat() <= structure.spawnChance)
                                    {
                                        structureChunks.Add(coords, structure);
                                    }
                                }
                                checkedStructureCoords.Add(coords);
                            }
                        }
                    }

                    //Generate structure transforms, noise changes, and road changes
                    foreach (KeyValuePair<Vector2Int, TerrainStructureInstance> structure in structureChunks)
                    {
                        GenerateStructure(structure.Key, new TerrainStructureInstanceStruct(structure.Value));
                    }
                }

                //
                //
                //Calculate chunk priorities
                //

                //Compile chunk coordinates in order of generation priority
                NativeList<Vector2Int> chunksToGenerate = new NativeList<Vector2Int>(Allocator.TempJob);
                ChunkGenerationPriorityJob priorityJob = new ChunkGenerationPriorityJob(ref chunksToGenerate, ref chunkCoordHeap,
                    chunkRadius, originCoords, player.transform.forward, maxViewAngle, viewGenerationFactor);
                JobHandle priorityJobHandle = priorityJob.Schedule();
                priorityJobHandle.Complete();

                //
                //
                //Generate chunk
                //

                //Generate chunk with highest priority
                if (chunksToGenerate.Length > 0)
                {
                    Vector2Int currentCoords = chunksToGenerate[0];
                    if (chunks.ContainsKey(currentCoords) == false)
                    {
                        SpawnChunk(currentCoords);
                    }
                }
                chunksToGenerate.Dispose();

                //
                //
                //Collider Baking
                //

                if (colliderQueue.Count > 0)
                {
                    //Check whether any colliders are too close to player and need to be generated immediately
                    foreach (KeyValuePair<Vector2Int, TerrainChunk> chunk in colliderQueue.ToArray())
                    {
                        if (Vector2Int.Distance(chunk.Key, originCoords) <= minColliderChunkDistance)
                        {
                            chunk.Value.ChunkObject.GetComponent<MeshCollider>().sharedMesh = chunk.Value.TerrainMesh;
                            colliderQueue.Remove(chunk.Key);
                        }
                    }

                    //Determine whether colldier queue should be batch-baked
                    if (colliderQueue.Count >= colliderBatchSize)
                    {
                        //Compile mesh ids for meshes without colliders
                        NativeArray<int> meshIds = new NativeArray<int>(colliderQueue.Count, Allocator.TempJob);
                        int i = 0;
                        foreach (KeyValuePair<Vector2Int, TerrainChunk> chunk in colliderQueue)
                        {
                            meshIds[i] = chunk.Value.ChunkObject.GetComponent<MeshFilter>().mesh.GetInstanceID();
                            i++;
                        }

                        //Compute multiple collider meshes on separate threads
                        BakeTerrainCollider bakeTerrainCollider = new BakeTerrainCollider(meshIds);
                        JobHandle bakeTerrainColliderHandle = bakeTerrainCollider.Schedule(meshIds.Length, 1);
                        bakeTerrainColliderHandle.Complete();

                        //Apply collider to chunk objects
                        foreach (KeyValuePair<Vector2Int, TerrainChunk> chunk in colliderQueue)
                        {
                            chunk.Value.ChunkObject.GetComponent<MeshCollider>().sharedMesh =
                                chunk.Value.ChunkObject.GetComponent<MeshFilter>().mesh;
                        }

                        colliderQueue.Clear();
                    }
                }
            }
        }

        private void GenerateStructure(Vector2Int coord, TerrainStructureInstanceStruct structure)
        {
            NativeList<TerrainStructureTransform> structureTransforms =
                new NativeList<TerrainStructureTransform>(100, Allocator.TempJob);
            GenerateStructureJob generateStructureJob = new GenerateStructureJob(coord, size3D,
                structure, terrainStructurePrefabStructs[structure.index], ref structureTransforms);
            JobHandle generateStructureJobHandle = generateStructureJob.Schedule();
            generateStructureJobHandle.Complete();
            generateStructureJob.prefabCounts.Dispose();
            generateStructureJob.positions.Dispose();

            for (int i = 0; i < structureTransforms.Length; i++)
            {
                TerrainStructureTransform transform = structureTransforms[i];
                TerrainStructurePrefab prefab = compiledTerrainStructurePrefabs[transform.GetIndex()];
                //Get correct elevation for this transform
                Vector3 position = transform.GetPos();
                if (prefab.heightMode == TerrainStructurePrefab.HeightMode.Elevation)
                {
                    float surfaceLevel = Mathf.Clamp(Mathf.Round(GetSurfaceLevel(position)),
                        prefab.minimumSpawnHeight, prefab.maximumSpawnHeight);
                    position = new Vector3(position.x, surfaceLevel, position.z);
                }
                else if (prefab.heightMode == TerrainStructurePrefab.HeightMode.Height)
                {
                    position = new Vector3(position.x, Mathf.Round(prefab.spawnHeight), position.z);
                }
                transform.SetPos(position);
                persistentStructureTransforms.Add(TerrainManagerUtility.GetCoords(position, size3D), transform);

                Vector3Int influenceBound = prefab.influenceBounds;
                ApplyInfluenceBounds applyInfluenceBounds = new ApplyInfluenceBounds(size3D, position, influenceBound,
                    ref persistentWeightChanges, ref persistentRoadWeightChanges, ref persistentInfluenceBounds);
                JobHandle applyInfluenceBoundsJobHandle = applyInfluenceBounds.Schedule();
                applyInfluenceBoundsJobHandle.Complete();

                structureTransforms[i] = transform;
            }

            //Calculate structure connection changes
            if (structure.connectionMode == TerrainStructureInstance.ConnectionMode.Nearest)
            {
                HashSet<TerrainStructureTransform> connectedTransforms = new HashSet<TerrainStructureTransform>();
                for (int i = 0; i < structureTransforms.Length; i++)
                {
                    if (i < structureTransforms.Length - 1)
                    {
                        TerrainStructureTransform transform = structureTransforms[i];
                        Vector3 closestPosition = Vector3.zero;
                        float closestDistance = float.MaxValue;
                        foreach (TerrainStructureTransform neighborTransform in structureTransforms)
                        {
                            if (connectedTransforms.Contains(neighborTransform) == false &&
                                transform.GetPos() != neighborTransform.GetPos())
                            {
                                float distance =
                                    Vector3.Distance(transform.GetPos(), neighborTransform.GetPos());
                                if (distance < closestDistance)
                                {
                                    closestDistance = distance;
                                    closestPosition = neighborTransform.GetPos();
                                }
                            }
                        }
                        ConnectStructures connectStructures = new ConnectStructures(size3D, transform.GetPos(),
                            closestPosition, ref persistentRoadWeightChanges, structurePathStartHeightOffset);
                        JobHandle connectStructuresJobHandle = connectStructures.Schedule();
                        connectStructuresJobHandle.Complete();

                        connectedTransforms.Add(transform);
                    }
                }
            }
            structureTransforms.Dispose();
        }

        #endregion

        #region Chunk Generation
        //
        //
        //Chunk Generation
        //

        private void SpawnChunk(Vector2Int currentCoords)
        {
            //Generate chunk data
            TerrainChunk terrainChunk = GenerateChunk(currentCoords);
            //Create chunk gameobject   
            terrainChunk.ChunkObject = CreateTerrainObject(terrainChunk);
            //Apply texture changes to material property block for this chunk
            ApplyTextures(terrainChunk);
            //Spawn water
            if (waterEnabled)
            {
                GameObject waterObject = Instantiate(waterPrefab);
                waterObject.transform.position = terrainChunk.ChunkObject.transform.position + new Vector3(0f, waterLevel, 0f);
                terrainChunk.WaterObject = waterObject;
            }

            chunks.Add(currentCoords, terrainChunk);
            chunkCoordHeap.Add(currentCoords);

            OnChunkGenerated(terrainChunk);
        }

        private TerrainChunk GenerateChunk(Vector2Int currentCoords)
        {
            Vector2Int size = new Vector2Int(size3D.x, size3D.z);

            Vector2Int chunkOffset = new Vector2Int(currentCoords.x * size.x - currentCoords.x,
                currentCoords.y * size.y - currentCoords.y);

            //
            //
            //Calculate biome weights of this chunk
            //

            //Create job to calculate temperature noise values
            NativeArray<float> tempNoise = new NativeArray<float>(size.x * size.y, Allocator.Persistent);
            CalculateBiomeNoise(ref tempNoise, temperatureNoiseValues, ref temperatureOctaveOffsets,
                chunkOffset, size);
            //Create job to calculate moisture noise values
            NativeArray<float> moistureNoise = new NativeArray<float>(size.x * size.y, Allocator.Persistent);
            CalculateBiomeNoise(ref moistureNoise, moistureNoiseValues, ref moistureOctaveOffsets,
                chunkOffset, size);

            //Calculate interpolated biome noise
            NativeArray<float> biomeWeights = new NativeArray<float>(size.x * size.y * terrainBiomes.Count, Allocator.TempJob);
            NativeArray<int> biomeArr = new NativeArray<int>(size.x * size.y, Allocator.Persistent);
            CalculateBiomeWeights calculateBiomeWeights = new CalculateBiomeWeights(ref tempNoise, ref moistureNoise,
                ref biomeGrid, ref biomeWeights, ref biomeArr, biomeGridSize, terrainBiomes.Count);
            JobHandle calculateBiomeWeightsHandle = calculateBiomeWeights.Schedule(size.x * size.y, size.x);
            calculateBiomeWeightsHandle.Complete();

            //
            //
            //Calculate optional vegetation/rock noise values
            //

            //Create job to calculate vegetation noise values
            NativeArray<float> vegetationNoise = new NativeArray<float>(size.x * size.y, Allocator.Persistent);
            CalculateNoise(ref vegetationNoise, ref biomeVegetationValues, ref biomeWeights,
                ref vegetationOctaveOffsets, chunkOffset, size);
            OnTextureNoiseCalculated(ref vegetationNoise, size, currentCoords);

            //Create job to calculate rock noise values
            NativeArray<float> rockNoise = new NativeArray<float>(size.x * size.y, Allocator.Persistent);
            if (rockNoiseEnabled)
            {
                CalculateNoise(ref rockNoise, ref biomeRockValues, ref biomeWeights,
                    ref rockOctaveOffsets, chunkOffset, size);
                OnObjectNoiseCalculated(ref rockNoise, size, currentCoords);
            }

            NativeArray<float> weights = new NativeArray<float>(size3D.x * size3D.y * size3D.z, Allocator.Persistent);
            NativeArray<Vector2> elevationNoise = new NativeArray<Vector2>(size.x * size.y, Allocator.TempJob);

            //
            //
            //Calculate optional elevation noise values
            //

            if (elevationNoiseEnabled)
            {
                CalculateElevationNoise(ref elevationNoise, ref biomeWeights, chunkOffset, size3D);
                OnElevationNoiseCalculated(ref elevationNoise, size, currentCoords);
            }


            //
            //
            //Calculate optional ridged noise values
            //

            NativeArray<float> ridgedNoise = new NativeArray<float>(size.x * size.y * ridgedNoiseValues.Count,
                Allocator.TempJob);
            if (ridgedNoiseEnabled)
            {
                CalculateRidgedNoise(ref ridgedNoise, chunkOffset, size, ref ridgedNoisePassesNative,
                    ref ridgedNoiseOffsets);
                OnRidgedNoiseCalculated(ref ridgedNoise, size, ridgedNoisePassesNative.Length, currentCoords);
            }

            //
            //
            //Calculate optional Voronoi noise values
            //

            NativeArray<float> voronoiNoise = new NativeArray<float>(size3D.x * size3D.y * size3D.z, Allocator.TempJob);
            if (voronoiNoiseEnabled)
            {
                Vector2Int extendedSize = new Vector2Int(size.x + 2, size.y + 2);
                Vector2Int extendedChunkOffset = chunkOffset - Vector2Int.one;

                //Create job to calculate temperature noise values
                NativeArray<float> extendedTempNoise =
                    new NativeArray<float>(extendedSize.x * extendedSize.y, Allocator.Persistent);
                CalculateBiomeNoise(ref extendedTempNoise, temperatureNoiseValues, ref temperatureOctaveOffsets,
                    extendedChunkOffset, extendedSize);
                //Create job to calculate moisture noise values
                NativeArray<float> extendedMoistureNoise =
                    new NativeArray<float>(extendedSize.x * extendedSize.y, Allocator.Persistent);
                CalculateBiomeNoise(ref extendedMoistureNoise, moistureNoiseValues, ref moistureOctaveOffsets,
                    extendedChunkOffset, extendedSize);

                //Calculate interpolated biome noise
                NativeArray<float> extendedBiomeWeights =
                    new NativeArray<float>(extendedSize.x * extendedSize.y * terrainBiomes.Count, Allocator.TempJob);
                NativeArray<int> extendedBiomeArr =
                    new NativeArray<int>(extendedSize.x * extendedSize.y, Allocator.Persistent);
                CalculateBiomeWeights calculateExtendedBiomeWeights =
                    new CalculateBiomeWeights(ref extendedTempNoise, ref extendedMoistureNoise,
                    ref biomeGrid, ref extendedBiomeWeights, ref extendedBiomeArr, biomeGridSize, terrainBiomes.Count);
                JobHandle calculateExtendedBiomeWeightsHandle =
                    calculateExtendedBiomeWeights.Schedule(extendedSize.x * extendedSize.y, extendedSize.x);
                calculateExtendedBiomeWeightsHandle.Complete();
                extendedTempNoise.Dispose();
                extendedMoistureNoise.Dispose();

                NativeArray<Vector2> extendedElevationNoise =
                    new NativeArray<Vector2>(extendedSize.x * extendedSize.y, Allocator.TempJob);
                CalculateElevationNoise(ref extendedElevationNoise, ref extendedBiomeWeights,
                    extendedChunkOffset, new Vector3Int(extendedSize.x, size3D.y, extendedSize.y));
                extendedBiomeWeights.Dispose();
                extendedBiomeArr.Dispose();

                ////Create job to calculate steepness differences at each point
                NativeArray<float> steepnessWeights = new NativeArray<float>(size3D.x * size3D.z, Allocator.TempJob);
                SteepnessWeightHeap steepnessWeightHeap = new SteepnessWeightHeap(size3D, voronoiSteepnessSampleCount,
                    ref voronoiSteepnessSamples, ref extendedElevationNoise, ref steepnessWeights);
                JobHandle steepnessWeightHandle = steepnessWeightHeap.Schedule(size3D.x * size3D.z, size3D.x);
                steepnessWeightHandle.Complete();

                CalculateVoronoiNoise(ref voronoiNoise, ref steepnessWeights, ref elevationNoise,
                    chunkOffset, size3D, ref biomeWeights);
            }

            //
            //
            //Sample and fill 3d noise weights
            //

            CalculateTerrainNoise(ref weights, ref biomeWeights, ref ridgedNoise, ref elevationNoise, ref voronoiNoise,
                verticalSampleRate, horizontalSampleRate, chunkOffset, size3D);
            biomeWeights.Dispose();
            OnTerrainNoiseCalculated(ref weights, size3D, currentCoords);

            //
            //
            //Calculate road weights and apply to terrain
            //

            NativeArray<float> roadWeights = new NativeArray<float>(size.x * size.y * Mathf.Max(roadPassesNative.Length, 1), Allocator.Persistent);
            NativeArray<float> roadStartHeights = new NativeArray<float>(size.x * size.y, Allocator.Persistent);
            if (roadsEnabled)
            {
                NativeArray<float> roadNoise = new NativeArray<float>(size.x * size.y, Allocator.Persistent);
                CalculateRidgedNoise(ref roadNoise, chunkOffset, size, ref roadPassesNative, ref roadNoiseOffsets);

                CompileRoadData(size, ref roadNoise, ref roadWeights, ref roadStartHeights, ref elevationNoise);
                OnRoadsCalculated(ref roadWeights, ref roadStartHeights, size, currentCoords);

                ConformMeshToRoadWeights(ref weights, ref roadWeights, ref roadStartHeights, size3D);
            }
            elevationNoise.Dispose();

            if (structuresEnabled)
            {
                if (roadsEnabled)
                {
                    CompileStructureRoadData(size, currentCoords, ref roadWeights, ref roadStartHeights);
                }

                ConformMeshToStructures(size3D, currentCoords, ref weights);
            }

            //
            //
            //Compute vertices and trigs using marching cubes algorithm
            //

            NativeList<Vector3> verts = new NativeList<Vector3>(Allocator.TempJob);
            NativeList<int> trigs = new NativeList<int>(Allocator.TempJob);
            NativeList<Vector2> uvs = new NativeList<Vector2>(Allocator.TempJob);

            //Calculate mesh data using marching cubes algorithm
            CalculateMeshData(weights, ref verts, ref trigs, ref uvs,
                densityThreshold, size3D);
            OnMeshCalculated(ref verts, ref trigs, currentCoords);

            //
            //
            //Create terrain mesh using advanced mesh API
            //

            Mesh terrainMesh = GenerateTerrainMesh(ref verts, ref trigs, ref uvs);

            //
            //
            //Create chunk object/gameobject
            //

            TerrainChunk chunk = new TerrainChunk(currentCoords, ref weights,
                new NativeArray<Vector3>(terrainMesh.normals, Allocator.Persistent), verts.ToArray(Allocator.Persistent),
                ref tempNoise, ref moistureNoise, ref biomeArr, ref vegetationNoise, ref rockNoise, ref roadWeights,
                ref roadStartHeights, terrainMesh, size3D);
            colliderQueue.Add(currentCoords, chunk);
            verts.Dispose();

            //
            //
            //Calculate terrain object transforms (trees, grass, rocks) based on vertex data
            //

            Vector3 pos = new Vector3(chunk.ChunkCoords.x * size.x - chunk.ChunkCoords.x, 0f,
                chunk.ChunkCoords.y * size.y - chunk.ChunkCoords.y);

            GetObjectTransforms(chunk, ref terrainBiomes, pos);

            return chunk;
        }


        private GameObject CreateTerrainObject(TerrainChunk chunk)
        {
            GameObject chunkObject = Instantiate(chunkPrefab);
            chunkObject.name = "Terrain_" + chunk.ChunkCoords.x + "_" + chunk.ChunkCoords.y;

            MeshFilter meshFilter;
            if (chunkObject.GetComponent<MeshFilter>() == null)
            {
                meshFilter = chunkObject.AddComponent<MeshFilter>();
            }
            else
            {
                meshFilter = chunkObject.GetComponent<MeshFilter>();
            }
            meshFilter.mesh = chunk.TerrainMesh;

            if (chunkObject.GetComponent<MeshRenderer>() == null)
            {
                chunkObject.AddComponent<MeshRenderer>();
            }

            MeshCollider meshCollider;
            if (chunkObject.GetComponent<MeshCollider>() == null)
            {
                meshCollider = chunkObject.AddComponent<MeshCollider>();
            }
            else
            {
                meshCollider = chunkObject.GetComponent<MeshCollider>();
            }
            meshCollider.cookingOptions = MeshColliderCookingOptions.None;

            Vector3 worldPos = new Vector3(chunk.ChunkCoords.x * size3D.x - chunk.ChunkCoords.x, 0f,
                chunk.ChunkCoords.y * size3D.z - chunk.ChunkCoords.y);
            chunkObject.transform.position = worldPos;

            return chunkObject;
        }

        #endregion

        #region Noise
        //
        //
        //Noise
        //
        private void CalculateNoise(ref NativeArray<float> noise, ref NativeArray<NoiseValues> noiseValues,
            ref NativeArray<float> biomeWeights, ref NativeArray<Vector2> octaveOffsets,
            Vector2 chunkOffset, Vector2Int size)
        {
            NoiseHeap noiseStack = new NoiseHeap(ref noise, ref noiseValues,
                ref biomeWeights, chunkOffset, size, ref octaveOffsets);
            JobHandle noiseHandle = noiseStack.Schedule(size.x * size.y, size.x);
            noiseHandle.Complete();
        }

        private void CalculateBiomeNoise(ref NativeArray<float> noise, NoiseValues noiseValues,
            ref NativeArray<Vector2> octaveOffsets, Vector2 chunkOffset, Vector2Int size)
        {
            BiomeNoiseHeap noiseStack = new BiomeNoiseHeap(ref noise, noiseValues,
                chunkOffset, size, ref octaveOffsets);
            JobHandle noiseHandle = noiseStack.Schedule(size.x * size.y, size.x);
            noiseHandle.Complete();
        }

        private void CalculateElevationNoise(ref NativeArray<Vector2> elevationNoise,
            ref NativeArray<float> biomeWeights, Vector2 chunkOffset, Vector3Int size)
        {
            //Create job to calculate elevation weights
            ElevationNoiseHeap elevationNoiseHeap = new ElevationNoiseHeap(ref elevationNoise,
                ref biomeElevationValues, ref biomeWeights, chunkOffset, size, ref elevationOctaveOffsets,
                elevationCurveSampleCount, floorWeightCurveSampleCount, ref elevationCurveSamples,
                ref floorWeightCurveSamples);
            JobHandle elevationNoiseHandle = elevationNoiseHeap.Schedule(size.x * size.z, size.x);
            elevationNoiseHandle.Complete();
        }

        private void CalculateRidgedNoise(ref NativeArray<float> ridgedNoise, Vector2 chunkOffset, Vector2Int size,
            ref NativeArray<RidgedNoisePass> ridgedNoisePasses, ref NativeArray<Vector2> offsets)
        {
            RidgedNoiseHeap ridgedNoiseHeap = new RidgedNoiseHeap(ref ridgedNoise, chunkOffset, size,
                ref ridgedNoisePasses, ref offsets);
            JobHandle ridgedNoisehandle = ridgedNoiseHeap.Schedule(size.x * size.y, size.x);
            ridgedNoisehandle.Complete();
        }

        private void CalculateVoronoiNoise(ref NativeArray<float> voronoiNoise, ref NativeArray<float> steepnessWeights,
            ref NativeArray<Vector2> elevationNoise, Vector2Int chunkOffset,
            Vector3Int size, ref NativeArray<float> biomeWeights)
        {
            VoronoiNoiseHeap voronoiNoiseHeap = new VoronoiNoiseHeap(chunkOffset, size, ref biomeWeights,
                ref biomeVoronoiValues, ref voronoiOctaveOffsets, ref voronoiNoise, ref steepnessWeights,
                ref elevationNoise);
            JobHandle voronoiNoiseHandle = voronoiNoiseHeap.Schedule(size.x * size.y * size.z, size.x * size.y);
            voronoiNoiseHandle.Complete();
        }

        private void CalculateTerrainNoise(ref NativeArray<float> finalWeights, ref NativeArray<float> biomeWeights,
            ref NativeArray<float> ridgedNoiseWeights, ref NativeArray<Vector2> elevationNoise,
            ref NativeArray<float> voronoiWeights, int verticalSampleRate, int horizontalSampleRate,
            Vector2 chunkOffset, Vector3Int size)
        {
            NativeArray<float> weights = new NativeArray<float>(size.x * size.y * size.z
                , Allocator.TempJob);

            //Create job to calculate 3d noise values
            TerrainNoiseHeap terrainHeapJob = new TerrainNoiseHeap(ref weights, ref biomeTerrainValues, ref biomeWeights,
                chunkOffset, size, verticalSampleRate, horizontalSampleRate, ref octaveOffsets);
            JobHandle stackHandle = terrainHeapJob.Schedule(size.x * size.y * size.z, size.x * size.z);
            stackHandle.Complete();

            TerrainNoiseFill terrainFillJob = new TerrainNoiseFill(verticalSampleRate, horizontalSampleRate, size,
                ref weights, ref finalWeights, ridgedNoiseEnabled, ref ridgedNoiseApplicationValuesNative,
                ref ridgedNoiseWeights, ref ridgedNoiseHeightDistSamples, elevationNoiseEnabled, ref elevationNoise,
                ridgedCurveSampleCount, voronoiNoiseEnabled, ref voronoiWeights);
            JobHandle fillHandle = terrainFillJob.Schedule(size.x * size.y * size.z, size.x * size.z);
            fillHandle.Complete();
        }

        #endregion

        #region Mesh Generation
        //
        //
        //Mesh Generation
        //

        private void CalculateMeshData(NativeArray<float> weights, ref NativeList<Vector3> verts,
            ref NativeList<int> trigs, ref NativeList<Vector2> uvs, float densityThreshold, Vector3Int size)
        {
            NativeQueue<TerrainMeshTrig> meshTrigs = new NativeQueue<TerrainMeshTrig>(Allocator.TempJob);
            //Create job to convert 3d noise values into vertices using marching cubes algorithm
            March march = new March(size, densityThreshold, ref weights, meshTrigs.AsParallelWriter());
            JobHandle marchHandle = march.Schedule(size.x * size.y * size.z, size.x * size.z);
            marchHandle.Complete();

            //Create job to filter repeated vertices out of heap created with march jobs
            //This code isn't multi-threaded but is still run in a job to take advantage of the burst compiler
            //Pass method parameter arrays
            NativeParallelHashMap<Vector3, int> validatedVertices =
                new NativeParallelHashMap<Vector3, int>(1024, Allocator.TempJob);
            VertexFilter vertexFilter = new VertexFilter(size, ref meshTrigs, ref verts, ref trigs, ref uvs,
                ref validatedVertices);
            JobHandle filterHandle = vertexFilter.Schedule();
            filterHandle.Complete();
            meshTrigs.Dispose();
            validatedVertices.Dispose();
        }

        private void CalculateMeshData(NativeArray<float> heightMap, ref NativeList<Vector3> verts,
            ref NativeList<int> trigs, ref NativeList<Vector2> uvs, int maxHeight, Vector2Int size)
        {
            CalculateHeightMapMesh calculateHeightMapMesh = new CalculateHeightMapMesh(ref heightMap, ref verts, ref uvs,
                ref trigs, size, maxHeight);
            JobHandle calculateHeightMapMeshHandle = calculateHeightMapMesh.Schedule();
            calculateHeightMapMeshHandle.Complete();
        }

        private Mesh GenerateTerrainMesh(ref NativeList<Vector3> verts, ref NativeList<int> trigs,
            ref NativeList<Vector2> uvs)
        {
            Mesh terrainMesh = new Mesh();

            var vertLayout = new[]
            {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
        };

            //Create new mesh using advanced mesh API to save on performance
            int vertCount = verts.Length;
            terrainMesh.SetVertexBufferParams(vertCount, vertLayout);
            terrainMesh.SetVertexBufferData(verts.AsArray(), 0, 0, vertCount, 0,
                MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds |
                MeshUpdateFlags.DontNotifyMeshUsers);

            int trigCount = trigs.Length;
            terrainMesh.SetIndexBufferParams(trigCount, IndexFormat.UInt32);
            terrainMesh.SetIndexBufferData(trigs.AsArray(), 0, 0, trigCount,
                MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds |
                MeshUpdateFlags.DontNotifyMeshUsers);

            terrainMesh.SetSubMesh(0, new SubMeshDescriptor(0, trigCount));

            terrainMesh.SetUVs(0, uvs.AsArray());

            Vector3Int size = new Vector3Int(size3D.x - 1, size3D.y - 1, size3D.z - 1);
            Bounds bounds = new Bounds(new Vector3(size.x / 2f, size.y / 2f, size.z / 2f), size);
            terrainMesh.bounds = bounds;

            terrainMesh.RecalculateNormals();

            trigs.Dispose();
            uvs.Dispose();

            return terrainMesh;
        }
        #endregion

        #region Shader
        //
        //
        //Shader
        //
        private void ApplyTextures(TerrainChunk chunk)
        {
            Renderer renderer = chunk.ChunkObject.GetComponent<MeshRenderer>();
            renderer.material = terrainMat;
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);

            int lengthX = chunk.Size.x;
            int lengthZ = chunk.Size.z;

            NativeArray<half> biomeWeightsPixels = new NativeArray<half>(lengthX * lengthZ * 2, Allocator.TempJob);
            NativeArray<half> vegetationNoisePixels = new NativeArray<half>(lengthX * lengthZ, Allocator.TempJob);
            NativeArray<half> roadWeightsPixels = new NativeArray<half>(lengthX * lengthZ, Allocator.TempJob);
            NativeArray<half> roadStartHeightsPixels = new NativeArray<half>(lengthX * lengthZ, Allocator.TempJob);

            ApplyTexturesJob applyTexturesJob = new ApplyTexturesJob(chunk.TempNoise, chunk.MoistureNoise,
                chunk.VegetationNoise, chunk.RoadWeights, chunk.RoadStartHeights, ref biomeWeightsPixels,
                ref vegetationNoisePixels, ref roadWeightsPixels, ref roadStartHeightsPixels, chunk.Size);
            JobHandle applyTexturesHandle = applyTexturesJob.Schedule(lengthX * lengthZ, lengthX);
            applyTexturesHandle.Complete();

            Texture2D biomeWeightsTexture = new Texture2D(lengthX, lengthZ, TextureFormat.RGHalf, 1, true);
            biomeWeightsTexture.SetPixelData(biomeWeightsPixels, 0, 0);
            biomeWeightsTexture.Apply(false);

            Texture2D vegetationNoiseTexture = new Texture2D(lengthX, lengthZ, TextureFormat.RHalf, 1, true);
            vegetationNoiseTexture.SetPixelData(vegetationNoisePixels, 0, 0);
            vegetationNoiseTexture.Apply(false);

            Texture2D roadWeightsTexture = new Texture2D(lengthX, lengthZ, TextureFormat.RHalf, 1, true);
            roadWeightsTexture.SetPixelData(roadWeightsPixels, 0, 0);
            roadWeightsTexture.Apply(false);

            Texture2D roadStartHeightsTexture = new Texture2D(lengthX, lengthZ, TextureFormat.RHalf, 1, true);
            roadStartHeightsTexture.SetPixelData(roadStartHeightsPixels, 0, 0);
            roadStartHeightsTexture.Apply(false);

            propertyBlock.SetTexture("_BiomeWeights", biomeWeightsTexture);
            propertyBlock.SetTexture("_VegetationNoise", vegetationNoiseTexture);
            propertyBlock.SetTexture("_RoadWeights", roadWeightsTexture);
            propertyBlock.SetTexture("_RoadStartHeights", roadStartHeightsTexture);

            biomeWeightsPixels.Dispose();
            vegetationNoisePixels.Dispose();
            roadWeightsPixels.Dispose();
            roadStartHeightsPixels.Dispose();

            renderer.SetPropertyBlock(propertyBlock);
        }

        private Texture2DArray GenerateTextureArray(Texture2D[] textures, int texSize, TextureFormat format, bool mipmaps, bool linear)
        {
            Texture2DArray textureArray = new Texture2DArray(texSize, texSize, textures.Length, format, mipmaps, linear);
            for (int i = 0; i < textures.Length; i++)
            {
                textures[i].Apply();
                textureArray.SetPixels(textures[i].GetPixels(), i);
            }
            textureArray.Apply();
            return textureArray;
        }

        private int AddTextureIndex(TextureSet textureSet, List<Texture2D> compiledTextures,
            Texture2D textureIndices, int textureIndex, int textureNum)
        {
            CheckNullTexture(textureSet);

            float maxTextures = terrainBiomes.Count * layerCount * textureCountPerLayer + terrainBiomes.Count * 2;
            if (compiledTextures.Contains(textureSet.baseColor) == false)
            {
                compiledTextures.Add(textureSet.baseColor);
                textureIndices.SetPixel(textureIndex, 0, new Color(textureNum / maxTextures, 0, 0));
                return 1;
            }
            else
            {
                textureIndices.SetPixel(textureIndex, 0, new Color(compiledTextures.IndexOf(textureSet.baseColor) /
                    maxTextures, 0, 0));
                return 0;
            }
        }

        private int AddTextureSet(Texture2D baseColor, Texture2D maskMap, Texture2D normalMap,
            int textureIterator, Texture2D[] textures, Texture2D[] maskMaps, Texture2D[] normals)
        {
            if (textures.Contains(baseColor) == false)
            {
                textures[textureIterator] = baseColor;
                maskMaps[textureIterator] = maskMap;
                normals[textureIterator] = normalMap;
                return 1;
            }
            return 0;
        }

        private void CheckNullTexture(TextureSet textureSet)
        {
            if (textureSet.baseColor == null)
            {
                Texture2D emptyTex = new Texture2D(texSize, texSize);
                Texture2D emptyTexHalf = new Texture2D(texSize / 2, texSize / 2);

                textureSet.baseColor = emptyTex;
                textureSet.maskMap = emptyTexHalf;
                textureSet.normalMap = emptyTexHalf;
            }
        }

        #endregion

        #region Object Placement
        //
        //
        //Object Placement
        //
        private void GetObjectTransforms(TerrainChunk chunk, ref List<TerrainBiome> terrainBiomes, Vector3 position)
        {
            //Compile data from each vertex used to generate object transforms 
            NativeArray<VertexData> vertexData = new NativeArray<VertexData>(chunk.Verts.Length, Allocator.TempJob);
            CompileVertexData compileVertexData = new CompileVertexData(chunk.Verts, chunk.Size, position, chunk.Normals,
                chunk.VegetationNoise, chunk.RockNoise, chunk.BiomeArr, chunk.RoadWeights, chunk.RoadStartHeights,
                roadHeight, ref persistentInfluenceBounds, ref vertexData);
            JobHandle compileVertexDataHandle = compileVertexData.Schedule(chunk.Verts.Length, chunk.Verts.Length / 32);
            compileVertexDataHandle.Complete();

            List<TerrainObjectInstance> compiledTerrainObjects = new List<TerrainObjectInstance>();
            HashSet<int> biomeList = new HashSet<int>();
            foreach (int biome in chunk.BiomeArr)
            {
                if (biomeList.Contains(biome) == false)
                {
                    compiledTerrainObjects.AddRange(terrainBiomes[biome].TerrainObjects);
                    biomeList.Add(biome);
                }
            }

            NativeList<JobHandle> getObjectTransformsHandleList = new NativeList<JobHandle>(Allocator.Temp);
            Dictionary<TerrainObjectInstance, NativeList<TerrainObjectTransform>> getObjectTransformsList =
                new Dictionary<TerrainObjectInstance, NativeList<TerrainObjectTransform>>();
            foreach (TerrainObjectInstance terrainObjectInstance in compiledTerrainObjects)
            {
                NativeList<TerrainObjectTransform> terrainObjectTransforms =
                    new NativeList<TerrainObjectTransform>(Allocator.Persistent);
                GetObjectTransforms getObjectTransforms =
                    new GetObjectTransforms(compiledTerrainObjectStructs[terrainObjectInstance.index],
                    ref terrainObjectTransforms, ref vertexData, roadWeightObjectSpawnThreshold, roadsEnabled,
                    structuresEnabled, (uint)(TerrainManagerUtility.GetSeed(chunk.ChunkCoords) +
                    compiledTerrainObjects.IndexOf(terrainObjectInstance)));
                getObjectTransformsList.Add(terrainObjectInstance, terrainObjectTransforms);
                JobHandle getObjectTransformHandle = getObjectTransforms.Schedule();
                getObjectTransformsHandleList.Add(getObjectTransformHandle);
            }
            JobHandle.CompleteAll(getObjectTransformsHandleList);

            //Compile chunk object collections
            Dictionary<int, TerrainTransformCollection> objectCollections = new Dictionary<int, TerrainTransformCollection>();
            foreach (KeyValuePair<TerrainObjectInstance, NativeList<TerrainObjectTransform>> objectTransform
                in getObjectTransformsList)
            {
                if (objectTransform.Value.Length > 0)
                {
                    TerrainObjectInstance instance = objectTransform.Key;
                    if (objectCollections.ContainsKey(instance.index))
                    {
                        int totalTransformCount = objectCollections[instance.index].transforms.Length +
                            objectTransform.Value.Length;
                        NativeArray<TerrainObjectTransform> transformsConcat = new NativeArray<TerrainObjectTransform>
                            (totalTransformCount, Allocator.Persistent);
                        for (int i = 0; i < objectCollections[instance.index].transforms.Length; i++)
                        {
                            transformsConcat[i] = objectCollections[instance.index].transforms[i];
                        }
                        for (int i = 0; i < objectTransform.Value.Length; i++)
                        {
                            transformsConcat[i + objectCollections[instance.index].transforms.Length] =
                                objectTransform.Value[i];
                        }
                        objectCollections[instance.index].transforms.Dispose();
                        objectCollections[instance.index].transforms = transformsConcat;
                    }
                    else
                    {
                        objectCollections.Add(instance.index,
                            new TerrainTransformCollection(objectTransform.Value.ToArray(Allocator.Persistent), instance.combineMesh));
                    }
                }
                objectTransform.Value.Dispose();
            }
            OnObjectTransformsCalculated(objectCollections, chunk.ChunkCoords);
            chunk.TerrainObjectTransforms = objectCollections;

            //Compile chunk structure collections
            Dictionary<int, TerrainStructureCollection> structureCollections = new Dictionary<int, TerrainStructureCollection>();
            foreach (TerrainStructureTransform transform in persistentStructureTransforms.GetValuesForKey(chunk.ChunkCoords))
            {
                TerrainStructurePrefab instance = compiledTerrainStructurePrefabs[transform.GetIndex()];
                if (structureCollections.ContainsKey(instance.index))
                {
                    NativeArray<TerrainStructureTransform> transformsConcat = new NativeArray<TerrainStructureTransform>
                        (structureCollections[instance.index].transforms.Length + 1, Allocator.Persistent);
                    for (int i = 0; i < structureCollections[instance.index].transforms.Length; i++)
                    {
                        transformsConcat[i] = structureCollections[instance.index].transforms[i];
                    }
                    transformsConcat[structureCollections[instance.index].transforms.Length] = transform;
                    structureCollections[instance.index].transforms.Dispose();
                    structureCollections[instance.index].transforms = transformsConcat;
                }
                else
                {
                    NativeArray<TerrainStructureTransform> transformsArr =
                        new NativeArray<TerrainStructureTransform>(1, Allocator.Persistent);
                    transformsArr[0] = transform;
                    structureCollections.Add(instance.index,
                        new TerrainStructureCollection(transformsArr));
                }
            }
            chunk.TerrainStructureTransforms = structureCollections;

            getObjectTransformsHandleList.Dispose();
            vertexData.Dispose();
        }

        #endregion

        #region Road Functions

        private void CompileRoadData(Vector2Int size, ref NativeArray<float> roadNoise,
            ref NativeArray<float> roadWeights, ref NativeArray<float> roadStartHeights,
            ref NativeArray<Vector2> elevationNoise)
        {
            CompileRoadData compileRoadDataJob = new CompileRoadData(roadElevationMode, ref roadNoise, ref roadWeights,
                ref roadStartHeights, ref elevationNoise, ref roadMaxHeights, ref roadMinHeights);
            JobHandle compileRoadDataJobHandle = compileRoadDataJob.Schedule(size.x * size.y, size.x);
            compileRoadDataJobHandle.Complete();
        }

        private void ConformMeshToRoadWeights(ref NativeArray<float> weights, ref NativeArray<float> roadWeights,
            ref NativeArray<float> roadStartHeights, Vector3Int size)
        {
            ConformMeshToRoadWeights conformMeshToRoadWeightsJob = new ConformMeshToRoadWeights(size, roadHeight,
                minRoadDeformHeight, maxRoadDeformHeight, roadFillStrength, roadCarveStrength, ref roadWeights,
                ref roadStartHeights, ref weights);
            JobHandle conformMeshToRoadWeightsJobHandle = conformMeshToRoadWeightsJob.Schedule(size.x * size.z, size.x);
            conformMeshToRoadWeightsJobHandle.Complete();
        }

        #endregion

        #region Structure Functions

        private void CompileStructureRoadData(Vector2Int size, Vector2Int chunkCoords,
            ref NativeArray<float> roadWeights, ref NativeArray<float> roadStartHeights)
        {
            CompileRoadStructureData compileRoadStructureData = new CompileRoadStructureData(size, chunkCoords,
                ref roadWeights, ref roadStartHeights, ref persistentRoadWeightChanges);
            JobHandle compileRoadStructureDataJobHandle = compileRoadStructureData.Schedule(size.x * size.y, size.x);
            compileRoadStructureDataJobHandle.Complete();
        }

        private void ConformMeshToStructures(Vector3Int size, Vector2Int chunkCoords, ref NativeArray<float> weights)
        {
            ConformMeshToStructures conformMeshToStructures = new ConformMeshToStructures(size, chunkCoords,
                ref weights, ref persistentWeightChanges, structureWeightChangeMultiplier);
            JobHandle conformMeshToStructuresJobHandle = conformMeshToStructures.Schedule();
            conformMeshToStructuresJobHandle.Complete();
        }

        #endregion

        #region Culling and LOD
        //
        //
        //Culling and LOD
        //

        private void SpawnObjects(KeyValuePair<int, TerrainTransformCollection> collection, Transform chunkTransform)
        {
            //If terrain object is marked as a combined mesh, create mesh using transforms
            if (collection.Value.combined)
            {
                Mesh objectMesh;
                if (collection.Value.combinedMesh != null)
                {
                    objectMesh = collection.Value.combinedMesh;
                }
                else
                {
                    Mesh originMesh = compiledTerrainObjects[collection.Key].prefab.GetComponent<MeshFilter>().sharedMesh;
                    Mesh.MeshDataArray meshDataArr = Mesh.AcquireReadOnlyMeshData(originMesh);
                    Mesh.MeshData meshData = meshDataArr[0];
                    SubMeshDescriptor subMesh = meshData.GetSubMesh(0);

                    int totalVertCount = meshData.vertexCount * collection.Value.transforms.Length;
                    int totalTrigCount = subMesh.indexCount * collection.Value.transforms.Length;

                    NativeArray<Vector3> originVertices = new NativeArray<Vector3>(meshData.vertexCount, Allocator.TempJob);
                    meshData.GetVertices(originVertices);
                    NativeArray<int> originIndices = new NativeArray<int>(subMesh.indexCount, Allocator.TempJob);
                    meshData.GetIndices(originIndices, 0);
                    NativeArray<Vector3> originNormals = new NativeArray<Vector3>(meshData.vertexCount, Allocator.TempJob);
                    meshData.GetNormals(originNormals);
                    NativeArray<Vector4> originTangents = new NativeArray<Vector4>(meshData.vertexCount, Allocator.TempJob);
                    meshData.GetTangents(originTangents);
                    NativeArray<Vector2> originUvs1 = new NativeArray<Vector2>(meshData.vertexCount, Allocator.TempJob);
                    meshData.GetUVs(0, originUvs1);
                    NativeArray<Vector2> originUvs2 = new NativeArray<Vector2>(meshData.vertexCount, Allocator.TempJob);
                    meshData.GetUVs(1, originUvs2);

                    Mesh.MeshDataArray finalMeshDataArr = Mesh.AllocateWritableMeshData(1);
                    Mesh.MeshData finalMeshData = finalMeshDataArr[0];

                    finalMeshData.SetVertexBufferParams(totalVertCount,
                        new VertexAttributeDescriptor(VertexAttribute.Position,
                        meshData.GetVertexAttributeFormat(VertexAttribute.Position), 3),
                        new VertexAttributeDescriptor(VertexAttribute.Normal,
                        meshData.GetVertexAttributeFormat(VertexAttribute.Normal), 3),
                        new VertexAttributeDescriptor(VertexAttribute.Tangent,
                        meshData.GetVertexAttributeFormat(VertexAttribute.Tangent), 4),
                        new VertexAttributeDescriptor(VertexAttribute.TexCoord0,
                        meshData.GetVertexAttributeFormat(VertexAttribute.TexCoord0), 2),
                        new VertexAttributeDescriptor(VertexAttribute.TexCoord1,
                        meshData.GetVertexAttributeFormat(VertexAttribute.TexCoord1), 2));

                    finalMeshData.SetIndexBufferParams(totalTrigCount, IndexFormat.UInt32);

                    CreateObjectMesh createObjectMesh = new CreateObjectMesh(meshData.vertexCount, subMesh.indexCount,
                        originVertices, originIndices, originNormals, originTangents, originUvs1, originUvs2,
                        collection.Value.transforms, chunkTransform.position, finalMeshData.GetVertexData<MeshVertexData>(),
                        finalMeshData.GetIndexData<uint>());
                    JobHandle handle = createObjectMesh.Schedule(collection.Value.transforms.Length,
                        collection.Value.transforms.Length / 25);
                    handle.Complete();

                    finalMeshData.subMeshCount = 1;
                    SubMeshDescriptor subMeshDescriptor = new SubMeshDescriptor();
                    subMeshDescriptor.indexCount = totalTrigCount;
                    subMeshDescriptor.vertexCount = totalVertCount;
                    finalMeshData.SetSubMesh(0, subMeshDescriptor, MeshUpdateFlags.DontValidateIndices |
                        MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);

                    meshDataArr.Dispose();

                    objectMesh = new Mesh();
                    Mesh.ApplyAndDisposeWritableMeshData(finalMeshDataArr, objectMesh, MeshUpdateFlags.DontValidateIndices |
                        MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);
                    objectMesh.RecalculateBounds();
                }

                GameObject combinedMeshObject = new GameObject();
                combinedMeshObject.transform.position = chunkTransform.position;
                MeshFilter meshFilter = combinedMeshObject.AddComponent<MeshFilter>();
                meshFilter.mesh = objectMesh;
                MeshRenderer meshRenderer = combinedMeshObject.AddComponent<MeshRenderer>();
                meshRenderer.material =
                    compiledTerrainObjects[collection.Key].prefab.GetComponent<MeshRenderer>().sharedMaterial;
                meshRenderer.shadowCastingMode =
                    compiledTerrainObjects[collection.Key].prefab.GetComponent<MeshRenderer>().shadowCastingMode;

                collection.Value.spawnedObjects.Add(combinedMeshObject);
                collection.Value.combinedMesh = objectMesh;
            }
            //Pull object from object pull or instantiate one if none are available
            else
            {
                foreach (TerrainObjectTransform transform in collection.Value.transforms)
                {
                    if (ObjectPoolManager.PoolExists(compiledTerrainObjects[collection.Key].prefab.name))
                    {
                        GameObject terrainObjectInstance =
                            ObjectPoolManager.pools[compiledTerrainObjects[collection.Key].prefab.name].ActivateObject();
                        terrainObjectInstance.transform.position = transform.GetPos();
                        terrainObjectInstance.transform.rotation = transform.GetRot();
                        terrainObjectInstance.transform.localScale = transform.GetScale();
                        collection.Value.spawnedObjects.Add(terrainObjectInstance);
                    }
                    else
                    {
                        GameObject terrainObjectInstance =
                            Instantiate(compiledTerrainObjects[collection.Key].prefab, chunkTransform);
                        terrainObjectInstance.transform.position = transform.GetPos();
                        terrainObjectInstance.transform.rotation = transform.GetRot();
                        terrainObjectInstance.transform.localScale = transform.GetScale();
                        collection.Value.spawnedObjects.Add(terrainObjectInstance);
                    }
                }
            }
            collection.Value.spawned = true;
        }

        private void SpawnObjects(KeyValuePair<int, TerrainStructureCollection> collection, Transform chunkTransform)
        {
            //Pull object from object pool or instantiate one if none are available
            foreach (TerrainStructureTransform transform in collection.Value.transforms)
            {
                if (ObjectPoolManager.PoolExists(compiledTerrainStructurePrefabs[collection.Key].prefab.name))
                {
                    GameObject terrainObjectInstance = ObjectPoolManager.pools[compiledTerrainStructurePrefabs[
                        collection.Key].prefab.name].ActivateObject();
                    terrainObjectInstance.transform.position = transform.GetPos();
                    terrainObjectInstance.transform.rotation = transform.GetRot();
                    terrainObjectInstance.transform.localScale = transform.GetScale();
                    collection.Value.spawnedObjects.Add(terrainObjectInstance);
                }
                else
                {
                    GameObject terrainObjectInstance =
                        Instantiate(compiledTerrainStructurePrefabs[collection.Key].prefab, chunkTransform);
                    terrainObjectInstance.transform.position = transform.GetPos();
                    terrainObjectInstance.transform.rotation = transform.GetRot();
                    terrainObjectInstance.transform.localScale = transform.GetScale();
                    collection.Value.spawnedObjects.Add(terrainObjectInstance);
                }
            }
            collection.Value.spawned = true;
        }

        private void DespawnObjects(TerrainChunk chunk)
        {
            foreach (KeyValuePair<int, TerrainTransformCollection> collection in chunk.TerrainObjectTransforms)
            {
                DespawnObjects(collection);
            }
            foreach (KeyValuePair<int, TerrainStructureCollection> collection in chunk.TerrainStructureTransforms)
            {
                DespawnObjects(collection);
            }
        }

        private void DespawnObjects(KeyValuePair<int, TerrainTransformCollection> collection)
        {
            if (collection.Value.combined)
            {
                foreach (GameObject spawnedObject in collection.Value.spawnedObjects.ToArray())
                {
                    Destroy(spawnedObject);
                }
            }
            else
            {
                foreach (GameObject spawnedObject in collection.Value.spawnedObjects.ToArray())
                {
                    ObjectPoolManager.pools[compiledTerrainObjects[collection.Key].prefab.name].DeactivateObject(spawnedObject);
                }
            }
            collection.Value.spawnedObjects.Clear();
            collection.Value.spawned = false;
        }
        private void DespawnObjects(KeyValuePair<int, TerrainStructureCollection> collection)
        {
            foreach (GameObject spawnedObject in collection.Value.spawnedObjects.ToArray())
            {
                ObjectPoolManager.pools[compiledTerrainStructurePrefabs[collection.Key].prefab.name].DeactivateObject(spawnedObject);
            }
            collection.Value.spawnedObjects.Clear();
            collection.Value.spawned = false;
        }
        private void DespawnChunk(TerrainChunk chunk)
        {
            Destroy(chunk.ChunkObject);

            DespawnObjects(chunk);
            if (chunk.WaterObject != null)
            {
                Destroy(chunk.WaterObject);
            }

            chunk.Dispose();

            chunks.Remove(chunk.ChunkCoords);
            chunkCoordHeap.Remove(chunk.ChunkCoords);
            if (colliderQueue.ContainsKey(chunk.ChunkCoords))
            {
                colliderQueue.Remove(chunk.ChunkCoords);
            }

            OnChunkDespawned(chunk);
        }
        #endregion

        #region Utility Functions
        public TerrainChunk GetChunk(Vector3 position)
        {
            TerrainChunk chunk = null;
            Vector2Int coords = TerrainManagerUtility.GetCoords(position, size3D);
            if (chunks.ContainsKey(coords))
            {
                chunk = chunks[coords];
            }
            return chunk;
        }

        public TerrainChunk GetChunk(Vector2Int coords)
        {
            TerrainChunk chunk = null;
            if (chunks.ContainsKey(coords))
            {
                chunk = chunks[coords];
            }
            return chunk;
        }

        public void RespawnChunk(Vector2Int coords, bool immediate)
        {
            if (chunks.ContainsKey(coords))
            {
                TerrainChunk chunk = chunks[coords];
                DespawnChunk(chunk);

                if (immediate)
                {
                    SpawnChunk(chunk.ChunkCoords);
                }
            }
        }

        public void RespawnChunk(TerrainChunk chunk, bool immediate)
        {
            if (chunk != null)
            {
                DespawnChunk(chunk);

                if (immediate)
                {
                    SpawnChunk(chunk.ChunkCoords);
                }
            }
        }

        public float[] GetBiomeSample(Vector2Int coords)
        {
            Vector3Int index = new Vector3Int(size3D.x / 2, 0, size3D.z / 2);
            Vector3 pos = TerrainManagerUtility.TerrainToWorld(index, coords, size3D);

            NativeArray<float> tempNoise = new NativeArray<float>(1, Allocator.TempJob);
            CalculateBiomeNoise(ref tempNoise, temperatureNoiseValues, ref temperatureOctaveOffsets,
                new Vector2(pos.x, pos.y), Vector2Int.one);

            NativeArray<float> moistureNoise = new NativeArray<float>(1, Allocator.TempJob);
            CalculateBiomeNoise(ref moistureNoise, moistureNoiseValues, ref moistureOctaveOffsets,
                new Vector2(pos.x, pos.y), Vector2Int.one);

            NativeArray<float> biomeWeights = new NativeArray<float>(terrainBiomes.Count, Allocator.TempJob);
            NativeArray<int> biomeArr = new NativeArray<int>(1, Allocator.TempJob);
            CalculateBiomeWeights calculateBiomeWeights = new CalculateBiomeWeights(ref tempNoise, ref moistureNoise,
                ref biomeGrid, ref biomeWeights, ref biomeArr, biomeGridSize, terrainBiomes.Count);
            JobHandle calculateBiomeWeightsHandle = calculateBiomeWeights.Schedule(1, 1);
            calculateBiomeWeightsHandle.Complete();

            tempNoise.Dispose();
            moistureNoise.Dispose();
            biomeArr.Dispose();

            return biomeWeights.ToArray();
        }

        public float[] GetBiomeSample(Vector3 position)
        {
            NativeArray<float> tempNoise = new NativeArray<float>(1, Allocator.TempJob);
            CalculateBiomeNoise(ref tempNoise, temperatureNoiseValues, ref temperatureOctaveOffsets,
                new Vector2(position.x, position.y), Vector2Int.one);

            NativeArray<float> moistureNoise = new NativeArray<float>(1, Allocator.TempJob);
            CalculateBiomeNoise(ref moistureNoise, moistureNoiseValues, ref moistureOctaveOffsets,
                new Vector2(position.x, position.y), Vector2Int.one);

            NativeArray<float> biomeWeights = new NativeArray<float>(terrainBiomes.Count, Allocator.TempJob);
            NativeArray<int> biomeArr = new NativeArray<int>(1, Allocator.TempJob);
            CalculateBiomeWeights calculateBiomeWeights = new CalculateBiomeWeights(ref tempNoise, ref moistureNoise,
                ref biomeGrid, ref biomeWeights, ref biomeArr, biomeGridSize, terrainBiomes.Count);
            JobHandle calculateBiomeWeightsHandle = calculateBiomeWeights.Schedule(1, 1);
            calculateBiomeWeightsHandle.Complete();

            tempNoise.Dispose();
            moistureNoise.Dispose();
            biomeArr.Dispose();

            return biomeWeights.ToArray();
        }

        public int GetStrongestBiome(Vector2Int coords)
        {
            float[] biomeWeights = GetBiomeSample(coords);
            int strongestBiome = 0;
            float strongestBiomeWeight = 0f;
            for (int i = 0; i < biomeWeights.Length; i++)
            {
                float biomeWeight = biomeWeights[i];
                if (biomeWeight > strongestBiomeWeight)
                {
                    strongestBiome = i;
                    strongestBiomeWeight = biomeWeight;
                }
            }

            return strongestBiome;
        }

        public int GetStrongestBiome(Vector3 position)
        {
            float[] biomeWeights = GetBiomeSample(position);
            int strongestBiome = 0;
            float strongestBiomeWeight = 0f;
            for (int i = 0; i < biomeWeights.Length; i++)
            {
                float biomeWeight = biomeWeights[i];
                if (biomeWeight > strongestBiomeWeight)
                {
                    strongestBiome = i;
                    strongestBiomeWeight = biomeWeight;
                }
            }

            return strongestBiome;
        }

        public float GetVegetationSample(Vector2Int coords)
        {
            Vector3Int index = new Vector3Int(size3D.x / 2, 0, size3D.z / 2);
            Vector3 pos = TerrainManagerUtility.TerrainToWorld(index, coords, size3D);
            NativeArray<float> biomeWeights = new NativeArray<float>(GetBiomeSample(pos), Allocator.TempJob);
            NativeArray<float> vegetationArr = new NativeArray<float>(1, Allocator.TempJob);
            CalculateNoise(ref vegetationArr, ref biomeVegetationValues, ref biomeWeights, ref vegetationOctaveOffsets,
                new Vector2(pos.x, pos.z), Vector2Int.one);
            float vegetation = vegetationArr[0];
            vegetationArr.Dispose();
            biomeWeights.Dispose();
            return vegetation;
        }

        public float GetVegetationSample(Vector3 position)
        {
            NativeArray<float> biomeWeights = new NativeArray<float>(GetBiomeSample(position), Allocator.TempJob);
            NativeArray<float> vegetationArr = new NativeArray<float>(1, Allocator.TempJob);
            CalculateNoise(ref vegetationArr, ref biomeVegetationValues, ref biomeWeights, ref vegetationOctaveOffsets,
                new Vector2(position.x, position.z), Vector2Int.one);
            float vegetation = vegetationArr[0];
            vegetationArr.Dispose();
            biomeWeights.Dispose();
            return vegetation;
        }

        public float GetRockSample(Vector2Int coords)
        {
            Vector3Int index = new Vector3Int(size3D.x / 2, 0, size3D.z / 2);
            Vector3 pos = TerrainManagerUtility.TerrainToWorld(index, coords, size3D);
            NativeArray<float> biomeWeights = new NativeArray<float>(GetBiomeSample(pos), Allocator.TempJob);
            NativeArray<float> rockArr = new NativeArray<float>(1, Allocator.TempJob);
            CalculateNoise(ref rockArr, ref biomeRockValues, ref biomeWeights, ref rockOctaveOffsets,
                new Vector2(pos.x, pos.z), Vector2Int.one);
            float rock = rockArr[0];
            rockArr.Dispose();
            biomeWeights.Dispose();
            return rock;
        }

        public float GetRockSample(Vector3 position)
        {
            NativeArray<float> biomeWeights = new NativeArray<float>(GetBiomeSample(position), Allocator.TempJob);
            NativeArray<float> rockArr = new NativeArray<float>(1, Allocator.TempJob);
            CalculateNoise(ref rockArr, ref biomeRockValues, ref biomeWeights, ref rockOctaveOffsets,
                new Vector2(position.x, position.z), Vector2Int.one);
            float rock = rockArr[0];
            rockArr.Dispose();
            biomeWeights.Dispose();
            return rock;
        }

        public Vector2 GetElevationSample(Vector2Int coords)
        {
            Vector3Int index = new Vector3Int(size3D.x / 2, 0, size3D.z / 2);
            Vector3 pos = TerrainManagerUtility.TerrainToWorld(index, coords, size3D);
            NativeArray<float> biomeWeights = new NativeArray<float>(GetBiomeSample(pos), Allocator.TempJob);
            NativeArray<Vector2> elevationNoise = new NativeArray<Vector2>(1, Allocator.TempJob);
            CalculateElevationNoise(ref elevationNoise, ref biomeWeights, new Vector2(pos.x, pos.z),
                new Vector3Int(1, size3D.y, 1));
            Vector2 elevation = elevationNoise[0];
            elevationNoise.Dispose();
            biomeWeights.Dispose();
            return elevation;
        }

        public Vector2 GetElevationSample(Vector3 position)
        {
            NativeArray<float> biomeWeights = new NativeArray<float>(GetBiomeSample(position), Allocator.TempJob);
            NativeArray<Vector2> elevationNoise = new NativeArray<Vector2>(1, Allocator.TempJob);
            CalculateElevationNoise(ref elevationNoise, ref biomeWeights, new Vector2(position.x, position.z),
                new Vector3Int(1, size3D.y, 1));
            Vector2 elevation = elevationNoise[0];
            elevationNoise.Dispose();
            biomeWeights.Dispose();
            return elevation;
        }

        public float[] GetRidgedNoiseSample(Vector2Int coords)
        {
            Vector3Int index = new Vector3Int(size3D.x / 2, 0, size3D.z / 2);
            Vector3 pos = TerrainManagerUtility.TerrainToWorld(index, coords, size3D);
            NativeArray<float> ridgedNoiseArr = new NativeArray<float>(ridgedNoisePassesNative.Length, Allocator.TempJob);
            CalculateRidgedNoise(ref ridgedNoiseArr, new Vector2(pos.x, pos.z), Vector2Int.one,
                ref ridgedNoisePassesNative, ref ridgedNoiseOffsets);
            return ridgedNoiseArr.ToArray();
        }

        public float[] GetRidgedNoiseSample(Vector3 position)
        {
            NativeArray<float> ridgedNoiseArr = new NativeArray<float>(ridgedNoisePassesNative.Length, Allocator.TempJob);
            CalculateRidgedNoise(ref ridgedNoiseArr, new Vector2(position.x, position.z), Vector2Int.one,
                ref ridgedNoisePassesNative, ref ridgedNoiseOffsets);
            return ridgedNoiseArr.ToArray();
        }

        public float GetRoadSample(Vector2Int coords)
        {
            Vector3Int index = new Vector3Int(size3D.x / 2, 0, size3D.z / 2);
            Vector3 pos = TerrainManagerUtility.TerrainToWorld(index, coords, size3D);
            NativeArray<float> roadWeightArr = new NativeArray<float>(1, Allocator.TempJob);
            CalculateRidgedNoise(ref roadWeightArr, new Vector2(pos.x, pos.z), Vector2Int.one, ref roadPassesNative,
                ref roadNoiseOffsets);
            float roadWeight = roadWeightArr[0];
            roadWeightArr.Dispose();
            return roadWeight;
        }

        public float GetRoadSample(Vector3 position)
        {
            NativeArray<float> roadWeightArr = new NativeArray<float>(1, Allocator.TempJob);
            CalculateRidgedNoise(ref roadWeightArr, new Vector2(position.x, position.z), Vector2Int.one,
                ref roadPassesNative, ref roadNoiseOffsets);
            float roadWeight = roadWeightArr[0];
            roadWeightArr.Dispose();
            return roadWeight;
        }

        public float GetSurfaceLevel(Vector3 position)
        {
            NativeArray<float> biomeWeights = new NativeArray<float>(GetBiomeSample(position), Allocator.TempJob);

            NativeArray<Vector2> elevationNoise = new NativeArray<Vector2>(1, Allocator.TempJob);
            if (elevationNoiseEnabled)
            {
                elevationNoise[0] = GetElevationSample(position);
            }

            NativeArray<float> ridgedNoiseWeights = new NativeArray<float>(ridgedNoisePassesNative.Length, Allocator.TempJob);
            if (ridgedNoiseEnabled)
            {
                ridgedNoiseWeights = new NativeArray<float>(GetRidgedNoiseSample(position), Allocator.TempJob);
            }

            NativeArray<float> weights = new NativeArray<float>(size3D.y, Allocator.TempJob);
            TerrainNoiseHeap terrainNoiseHeap = new TerrainNoiseHeap(ref weights, ref biomeTerrainValues, ref biomeWeights,
                new Vector2(position.x, position.z), new Vector3Int(1, size3D.y, 1), verticalSampleRate, 1, ref octaveOffsets);
            JobHandle terrainNoiseHeapHandle = terrainNoiseHeap.Schedule(size3D.y, size3D.y);
            terrainNoiseHeapHandle.Complete();

            NativeArray<float> finalWeights = new NativeArray<float>(size3D.y, Allocator.TempJob);
            NativeArray<float> voronoiWeights = new NativeArray<float>(size3D.y, Allocator.TempJob);
            TerrainNoiseFill terrainNoiseFill = new TerrainNoiseFill(verticalSampleRate, 1, new Vector3Int(1, size3D.y, 1),
                ref weights, ref finalWeights, ridgedNoiseEnabled, ref ridgedNoiseApplicationValuesNative,
                ref ridgedNoiseWeights, ref ridgedNoiseHeightDistSamples, elevationNoiseEnabled, ref elevationNoise,
                ridgedCurveSampleCount, false, ref voronoiWeights);
            JobHandle terrainNoiseFillHandle = terrainNoiseFill.Schedule(size3D.y, size3D.y);
            terrainNoiseFillHandle.Complete();
            elevationNoise.Dispose();

            float surfaceLevel = 0f;
            for (int i = size3D.y - 1; i >= 0; i--)
            {
                if (finalWeights[i] > densityThreshold)
                {
                    float interpolation =
                        Mathf.InverseLerp(finalWeights[i], finalWeights[Mathf.Clamp(i + 1, 0, size3D.y - 1)], densityThreshold);
                    surfaceLevel = Mathf.Lerp(i, i + 1, interpolation);
                    break;
                }
            }
            finalWeights.Dispose();
            return surfaceLevel;
        }

        protected virtual void OnChunkGenerated(TerrainChunk chunk)
        {
            if (ChunkGenerated != null)
            {
                ChunkGenerated(this, chunk);
            }
        }

        protected virtual void OnTextureNoiseCalculated(ref NativeArray<float> textureNoise,
            Vector2Int size, Vector2Int coords)
        {
            if (TextureNoiseCalculated != null)
            {
                TextureNoiseCalculated(this, ref textureNoise, size, coords);
            }
        }

        protected virtual void OnObjectNoiseCalculated(ref NativeArray<float> objectNoise,
            Vector2Int size, Vector2Int coords)
        {
            if (ObjectNoiseCalculated != null)
            {
                ObjectNoiseCalculated(this, ref objectNoise, size, coords);
            }
        }

        protected virtual void OnElevationNoiseCalculated(ref NativeArray<Vector2> elevationNoise,
            Vector2Int size, Vector2Int coords)
        {
            if (ElevationNoiseCalculated != null)
            {
                ElevationNoiseCalculated(this, ref elevationNoise, size, coords);
            }
        }

        protected virtual void OnRidgedNoiseCalculated(ref NativeArray<float> ridgedNoise, Vector2Int size,
            int ridgedNoisePassCount, Vector2Int coords)
        {
            if (RidgedNoiseCalculated != null)
            {
                RidgedNoiseCalculated(this, ref ridgedNoise, size, ridgedNoisePassCount, coords);
            }
        }

        protected virtual void OnTerrainNoiseCalculated(ref NativeArray<float> terrainNoise,
            Vector3Int size, Vector2Int coords)
        {
            if (TerrainNoiseCalculated != null)
            {
                TerrainNoiseCalculated(this, ref terrainNoise, size, coords);
            }
        }

        protected virtual void OnRoadsCalculated(ref NativeArray<float> roadWeights,
            ref NativeArray<float> roadStartHeights, Vector2Int size, Vector2Int coords)
        {
            if (RoadsCalculated != null)
            {
                RoadsCalculated(this, ref roadWeights, ref roadStartHeights, size, coords);
            }
        }

        protected virtual void OnMeshCalculated(ref NativeList<Vector3> verts, ref NativeList<int> trigs,
            Vector2Int coords)
        {
            if (MeshCalculated != null)
            {
                MeshCalculated(this, ref verts, ref trigs, coords);
            }
        }

        protected virtual void OnObjectTransformsCalculated(Dictionary<int, TerrainTransformCollection> transforms,
            Vector2Int coords)
        {
            if (ObjectTransformsCalculated != null)
            {
                ObjectTransformsCalculated(this, transforms, coords);
            }
        }

        protected virtual void OnChunkDespawned(TerrainChunk chunk)
        {
            if (ChunkDespawned != null)
            {
                ChunkDespawned(this, chunk);
            }
        }

        #endregion

        #region Misc Functions
        //
        //
        //Misc
        //

        private int RoundToExp(int v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;

            return v;
        }

        private void Dispose()
        {
            //Chunk disposing
            if (chunks.Count > 0)
            {
                foreach (TerrainChunk chunk in chunks.Values.ToArray())
                {
                    chunk.Dispose();
                }
            }
            chunks.Clear();
            colliderQueue.Clear();

            //Compiled object disposing
            compiledTerrainObjects.Clear();
            if (compiledTerrainObjectStructs.IsCreated)
            {
                compiledTerrainObjectStructs.Dispose();
            }
            compiledTerrainStructures.Clear();
            compiledTerrainStructurePrefabs.Clear();

            foreach (NativeArray<TerrainStructurePrefabStruct> prefabArr in terrainStructurePrefabStructs)
            {
                prefabArr.Dispose();
            }
            terrainStructurePrefabStructs.Clear();

            //Persistent change container disposing
            if (persistentWeightChanges.IsCreated)
            {
                persistentWeightChanges.Dispose();
            }

            if (persistentRoadWeightChanges.IsCreated)
            {
                persistentRoadWeightChanges.Dispose();
            }

            if (persistentStructureTransforms.IsCreated)
            {
                persistentStructureTransforms.Dispose();
            }

            if (persistentInfluenceBounds.IsCreated)
            {
                persistentInfluenceBounds.Dispose();
            }

            //Chunk coord heap disposing
            if (chunkCoordHeap.IsCreated)
            {
                chunkCoordHeap.Dispose();
            }

            //Biome grid disposing
            if (biomeGrid.IsCreated)
            {
                biomeGrid.Dispose();
            }

            //Noise native containers disposing
            if (temperatureOctaveOffsets.IsCreated)
            {
                temperatureOctaveOffsets.Dispose();
            }
            if (moistureOctaveOffsets.IsCreated)
            {
                moistureOctaveOffsets.Dispose();
            }
            if (vegetationOctaveOffsets.IsCreated)
            {
                vegetationOctaveOffsets.Dispose();
            }
            if (rockOctaveOffsets.IsCreated)
            {
                rockOctaveOffsets.Dispose();
            }
            if (elevationOctaveOffsets.IsCreated)
            {
                elevationOctaveOffsets.Dispose();
            }
            if (voronoiOctaveOffsets.IsCreated)
            {
                voronoiOctaveOffsets.Dispose();
            }
            if (octaveOffsets.IsCreated)
            {
                octaveOffsets.Dispose();
            }
            if (octaveOffsets2d.IsCreated)
            {
                octaveOffsets2d.Dispose();
            }

            if (biomeVegetationValues.IsCreated)
            {
                biomeVegetationValues.Dispose();
            }
            if (biomeElevationValues.IsCreated)
            {
                biomeElevationValues.Dispose();
            }
            if (biomeTerrainValues.IsCreated)
            {
                biomeTerrainValues.Dispose();
            }
            if (biomeRockValues.IsCreated)
            {
                biomeRockValues.Dispose();
            }
            if (biomeVoronoiValues.IsCreated)
            {
                biomeVoronoiValues.Dispose();
            }

            if (elevationCurveSamples.IsCreated)
            {
                elevationCurveSamples.Dispose();
            }
            if (floorWeightCurveSamples.IsCreated)
            {
                floorWeightCurveSamples.Dispose();
            }

            if (ridgedNoisePassesNative.IsCreated)
            {
                ridgedNoisePassesNative.Dispose();
            }
            if (ridgedNoiseApplicationValuesNative.IsCreated)
            {
                ridgedNoiseApplicationValuesNative.Dispose();
            }
            if (ridgedNoiseOffsets.IsCreated)
            {
                ridgedNoiseOffsets.Dispose();
            }
            if (roadPassesNative.IsCreated)
            {
                roadPassesNative.Dispose();
            }
            if (roadNoiseOffsets.IsCreated)
            {
                roadNoiseOffsets.Dispose();
            }
            if (roadMinHeights.IsCreated)
            {
                roadMinHeights.Dispose();
            }
            if (roadMaxHeights.IsCreated)
            {
                roadMaxHeights.Dispose();
            }

            if (ridgedNoiseHeightDistSamples.IsCreated)
            {
                ridgedNoiseHeightDistSamples.Dispose();
            }

            if (voronoiSteepnessSamples.IsCreated)
            {
                voronoiSteepnessSamples.Dispose();
            }
        }
        #endregion

        #region Editor Functions
#if UNITY_EDITOR

        public void StressTest()
        {
            Dispose();

            Stopwatch sw = new Stopwatch();

            Initialize(false, false);

            for (int i = 0; i < 1000; i++)
            {
                Vector2Int coords = new Vector2Int(UnityEngine.Random.Range(-1000, 1000), UnityEngine.Random.Range(-1000, 1000));
                sw.Start();
                TerrainChunk chunk = GenerateChunk(coords);
                EditorUtility.DisplayProgressBar("Stress Test", "Running Stress Test", i / 1000f);
                sw.Stop();
                colliderQueue.Clear();
                chunk.Dispose();
            }

            Dispose();

            UnityEngine.Debug.Log("Stress Test Completed. Generation of 1000 chunks took " + sw.Elapsed.TotalMilliseconds +
                "ms. Average chunk generation time: " + sw.Elapsed.TotalMilliseconds / 1000f + "ms. " +
                "Note: this number does not account for object culling, which can have a significant performance cost.");
        }

        public void OnApplicationQuit()
        {
            Dispose();
        }
#endif
        #endregion

        #region Data Validation
        //
        //
        //Data Validation
        //
        private void OnValidate()
        {
            //Mandatory editor data validation
            //Ensure seed is not zero
            seed = seed == 0 ? 1 : seed;

            //If only one biome exists make sure it fills entire biome grid
            if (terrainBiomes.Count == 1)
            {
                BiomeBound bound = new BiomeBound();
                bound.TemperatureMinMax = new Vector2(0f, 1f);
                bound.MoistureMinMax = new Vector2(0f, 1f);
                if (terrainBiomes[0].Bounds.Count == 0)
                {
                    terrainBiomes[0].Bounds.Add(bound);
                }
            }

            foreach (TerrainBiome biome in terrainBiomes)
            {
                foreach (TerrainStructureInstance structure in biome.TerrainStructures)
                {
                    foreach (TerrainStructurePrefab prefab in structure.terrainStructurePrefabs)
                    {
                        prefab.heightMode = biome.BiomeValues.elevationNoiseEnabled ? prefab.heightMode :
                            TerrainStructurePrefab.HeightMode.Height;
                    }
                }
            }

            //Optional safety checks
            if (!enableAdvancedInspector)
            {
                //Round sizes and sample rates to nearest power of 2
                int x = Mathf.Clamp(RoundToExp(size3DExposed.x), 16, 512);
                int y = Mathf.Clamp(RoundToExp(size3DExposed.y), 16, 512);
                size3DExposed = new Vector3Int(x, y, x);
                horizontalSampleRate = RoundToExp(horizontalSampleRate);
                verticalSampleRate = RoundToExp(verticalSampleRate);

                bool allTexturesReadable = true;
                string unReadableTexture = "Texture";

                bool allTexturesCorrectSize = true;
                string incorrectlySizedTexture = "Texture";

                //Check texture settings in biome layers
                foreach (TerrainBiome biome in terrainBiomes)
                {
                    int biomeLayerCount = biome.TerrainLayers.Count;

                    foreach (TextureLayer layer in biome.TerrainLayers)
                    {
                        foreach (TerrainLayerTextureSet textureSet in layer.textures)
                        {
                            if (textureSet.textureSet.CheckNull())
                            {
                                if (textureSet.textureSet.CheckReadable() == false)
                                {
                                    allTexturesReadable = false;
                                    unReadableTexture = textureSet.textureSet.baseColor.name;
                                }

                                if (textureSet.textureSet.CheckSize(texSize) == false)
                                {
                                    allTexturesCorrectSize = false;
                                    incorrectlySizedTexture = textureSet.textureSet.baseColor.name;
                                }
                            }
                        }

                        //Verify all textures are readable
                        if (biome.SteepnessTexture.CheckNull())
                        {
                            if (biome.SteepnessTexture.CheckReadable() == false)
                            {
                                allTexturesReadable = false;
                                unReadableTexture = biome.SteepnessTexture.baseColor.name;
                            }
                            if (biome.SteepnessTexture.CheckSize(texSize) == false)
                            {
                                allTexturesCorrectSize = false;
                                incorrectlySizedTexture = biome.SteepnessTexture.baseColor.name;
                            }
                        }
                        if (biome.SecondSteepnessTexture.CheckNull())
                        {
                            if (biome.SecondSteepnessTexture.CheckReadable() == false)
                            {
                                allTexturesReadable = false;
                                unReadableTexture = biome.SecondSteepnessTexture.baseColor.name;
                            }
                            if (biome.SecondSteepnessTexture.CheckSize(texSize) == false)
                            {
                                allTexturesCorrectSize = false;
                                incorrectlySizedTexture = biome.SecondSteepnessTexture.baseColor.name;
                            }
                        }
                        if (biome.RoadTexture.CheckNull())
                        {
                            if (biome.RoadTexture.CheckReadable() == false)
                            {
                                allTexturesReadable = false;
                                unReadableTexture = biome.RoadTexture.baseColor.name;
                            }
                            if (biome.RoadTexture.CheckSize(texSize) == false)
                            {
                                allTexturesCorrectSize = false;
                                incorrectlySizedTexture = biome.RoadTexture.baseColor.name;
                            }
                        }
                    }

                    if (allTexturesReadable == false)
                    {
                        UnityEngine.Debug.LogWarning("Not all terrain textures readable. Please fix. Incorrect texture set: " +
                            unReadableTexture);
                    }
                    if (allTexturesCorrectSize == false)
                    {
                        UnityEngine.Debug.LogWarning("Not all terrain textures are correct size. Albedo maps should be " + texSize +
                            "x" + texSize + " and all other maps (normal, mask, height) should be " +
                            texSize / 2 + "x" + texSize / 2 + ". Please fix. Incorrect texture set: " +
                            incorrectlySizedTexture);
                    }
                }
            }
        }
        #endregion
    }

    #region Noise Jobs

    [BurstCompile]
    public struct NoiseHeap : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        [WriteOnly]
        private NativeArray<float> noiseArr;

        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<NoiseValues> noiseValues;
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<float> biomeWeights;

        private Vector2 chunkOffset;
        private Vector2Int size;

        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<Vector2> octaveOffsets;

        public NoiseHeap(ref NativeArray<float> noiseArr,
            ref NativeArray<NoiseValues> noiseValues, ref NativeArray<float> biomeWeights,
            Vector2 chunkOffset, Vector2Int size, ref NativeArray<Vector2> octaveOffsets)
        {
            this.noiseArr = noiseArr;
            this.noiseValues = noiseValues;
            this.biomeWeights = biomeWeights;
            this.chunkOffset = chunkOffset;
            this.size = size;
            this.octaveOffsets = octaveOffsets;
        }

        public void Execute(int i)
        {
            int xIndex = i % size.y;
            int zIndex = i / size.y;
            float totalNoise = 0f;

            for (int j = 0; j < noiseValues.Length; j++)
            {
                NoiseValues values = noiseValues[j];
                float biomeWeight = biomeWeights[(zIndex * size.y + xIndex) * noiseValues.Length + j];
                if (biomeWeight > 0f && values.enabled)
                {
                    double noise = 0f;

                    double amplitude = 1f;
                    double frequency = 1f;

                    for (int l = 0; l < values.octaves; l++)
                    {
                        Vector2 octaveOffset = octaveOffsets[l];
                        double currentX = (xIndex + chunkOffset.x) / values.scale * frequency +
                            octaveOffset.x;
                        double currentZ = (zIndex + chunkOffset.y) / values.scale * frequency +
                            octaveOffset.y;
                        noise += Perlin.Noise(currentX, currentZ) * amplitude;

                        amplitude *= values.persistence;
                        frequency *= values.lacunarity;
                    }

                    totalNoise += Mathf.Clamp01((float)noise * 0.5f + 0.5f + values.bias) *
                        biomeWeight;
                }
            }

            noiseArr[zIndex * size.y + xIndex] = totalNoise;
        }
    }

    [BurstCompile]
    public struct BiomeNoiseHeap : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        [WriteOnly]
        private NativeArray<float> noiseArr;

        private NoiseValues noiseValues;

        private Vector2 chunkOffset;
        private Vector2Int size;

        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<Vector2> octaveOffsets;

        public BiomeNoiseHeap(ref NativeArray<float> noiseArr, NoiseValues noiseValues,
            Vector2 chunkOffset, Vector2Int size, ref NativeArray<Vector2> octaveOffsets)
        {
            this.noiseArr = noiseArr;
            this.noiseValues = noiseValues;
            this.chunkOffset = chunkOffset;
            this.size = size;
            this.octaveOffsets = octaveOffsets;
        }

        public void Execute(int i)
        {
            int xIndex = i % size.y;
            int zIndex = i / size.y;


            double noise = 0f;

            double amplitude = 1f;
            double frequency = 1f;

            for (int l = 0; l < noiseValues.octaves; l++)
            {
                Vector2 octaveOffset = octaveOffsets[l];
                double currentX = (xIndex + chunkOffset.x) / noiseValues.scale * frequency +
                    octaveOffset.x;
                double currentZ = (zIndex + chunkOffset.y) / noiseValues.scale * frequency +
                    octaveOffset.y;
                noise += Perlin.Noise(currentX, currentZ) * amplitude;

                amplitude *= noiseValues.persistence;
                frequency *= noiseValues.lacunarity;
            }

            noiseArr[zIndex * size.y + xIndex] = Mathf.Clamp01((float)noise * 0.5f + 0.5f + noiseValues.bias);
        }
    }

    [BurstCompile]
    public struct ElevationNoiseHeap : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        [WriteOnly]
        private NativeArray<Vector2> elevationNoise;

        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<NoiseValues> biomeElevationValues;
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<float> biomeWeights;

        private Vector2 chunkOffset;
        private Vector3Int size;

        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<Vector2> elevationOctaveOffsets;
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<float> elevationCurveSamples;
        private int elevationCurveSampleCount;
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<float> floorWeightCurveSamples;
        private int floorWeightCurveSampleCount;

        public ElevationNoiseHeap(ref NativeArray<Vector2> elevationNoise, ref NativeArray<NoiseValues> biomeElevationValues,
            ref NativeArray<float> biomeWeights, Vector2 chunkOffset, Vector3Int size,
            ref NativeArray<Vector2> elevationOctaveOffsets,
            int elevationCurveSampleCount, int floorWeightCurveSampleCount, ref NativeArray<float> elevationCurveSamples,
            ref NativeArray<float> floorWeightCurveSamples)
        {
            this.elevationNoise = elevationNoise;
            this.biomeElevationValues = biomeElevationValues;
            this.biomeWeights = biomeWeights;
            this.chunkOffset = chunkOffset;
            this.size = size;
            this.elevationOctaveOffsets = elevationOctaveOffsets;
            this.elevationCurveSampleCount = elevationCurveSampleCount;
            this.floorWeightCurveSampleCount = floorWeightCurveSampleCount;
            this.elevationCurveSamples = elevationCurveSamples;
            this.floorWeightCurveSamples = floorWeightCurveSamples;
        }

        public void Execute(int i)
        {
            int xIndex = i % size.z;
            int zIndex = i / size.z;
            float totalSurfaceLevels = 0f;
            float totalFloorWeights = 0f;

            for (int j = 0; j < biomeElevationValues.Length; j++)
            {
                NoiseValues elevationValues = biomeElevationValues[j];
                float biomeWeight = biomeWeights[(zIndex * size.z + xIndex) * biomeElevationValues.Length + j];
                if (biomeWeight > 0f && elevationValues.enabled)
                {
                    double elevationNoise = 0f;

                    double amplitude = 1f;
                    double frequency = 1f;

                    for (int l = 0; l < elevationValues.octaves; l++)
                    {
                        Vector2 elevationOctaveOffset = elevationOctaveOffsets[l];

                        double currentX = (xIndex + chunkOffset.x) / elevationValues.scale * frequency +
                            elevationOctaveOffset.x;
                        double currentZ = (zIndex + chunkOffset.y) / elevationValues.scale * frequency +
                            elevationOctaveOffset.y;
                        double noise = Perlin.Noise(currentX, currentZ);

                        elevationNoise += noise * amplitude;

                        amplitude *= elevationValues.persistence;
                        frequency *= elevationValues.lacunarity;
                    }

                    float elevationPercent = Mathf.Clamp01((float)elevationNoise * 0.5f + 0.5f + elevationValues.bias);

                    int elevationSampleIndex =
                        Mathf.Clamp(Mathf.RoundToInt(elevationPercent * elevationCurveSampleCount), 0, elevationCurveSampleCount - 1)
                        + j * elevationCurveSampleCount;
                    totalSurfaceLevels += elevationCurveSamples[elevationSampleIndex] * size.y * biomeWeight;

                    int floorWeightSampleIndex =
                        Mathf.Clamp(Mathf.RoundToInt(elevationPercent * floorWeightCurveSampleCount), 0, floorWeightCurveSampleCount - 1)
                        + j * floorWeightCurveSampleCount;
                    totalFloorWeights += floorWeightCurveSamples[floorWeightSampleIndex] * biomeWeight;
                }
            }

            elevationNoise[zIndex * size.z + xIndex] = new Vector2(totalSurfaceLevels, totalFloorWeights);
        }
    }

    [BurstCompile]
    public struct RidgedNoiseHeap : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        [WriteOnly]
        public NativeArray<float> ridgedNoiseWeights;

        private Vector2 chunkOffset;
        private Vector2Int size;
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<Vector2> ridgedNoiseOffsets;
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<RidgedNoisePass> ridgedNoisePasses;

        public RidgedNoiseHeap(ref NativeArray<float> ridgedNoiseWeights, Vector2 chunkOffset, Vector2Int size,
            ref NativeArray<RidgedNoisePass> ridgedNoisePasses, ref NativeArray<Vector2> ridgedNoiseOffsets)
        {
            this.ridgedNoiseWeights = ridgedNoiseWeights;
            this.chunkOffset = chunkOffset;
            this.size = size;
            this.ridgedNoisePasses = ridgedNoisePasses;
            this.ridgedNoiseOffsets = ridgedNoiseOffsets;
        }

        public void Execute(int i)
        {
            int xIndex = i % size.y;
            int zIndex = i / size.y;

            for (int j = 0; j < ridgedNoisePasses.Length; j++)
            {
                RidgedNoisePass ridgedNoisePass = ridgedNoisePasses[j];
                Vector2 offset = ridgedNoiseOffsets[j];

                double amplitude = 1.0f;
                double frequency = 1.0f;
                double noise = 0f;

                for (int l = 0; l < ridgedNoisePass.octaves; l++)
                {
                    double currentX = (xIndex + chunkOffset.x + offset.x) / ridgedNoisePass.noiseScale *
                        frequency;
                    double currentY = (zIndex + chunkOffset.y + offset.y) / ridgedNoisePass.noiseScale *
                        frequency;
                    noise += Perlin.Noise(currentX, currentY) * amplitude;

                    amplitude *= ridgedNoisePass.persistence;
                    frequency *= ridgedNoisePass.lacunarity;
                }

                float finalNoise = 1f - Mathf.Abs((float)noise);
                finalNoise = Mathf.Clamp01(finalNoise + ridgedNoisePass.bias);
                ridgedNoiseWeights[ridgedNoisePasses.Length * (zIndex * size.y + xIndex) + j] = finalNoise;
            }
        }
    }

    [BurstCompile]
    public struct VoronoiNoiseHeap : IJobParallelFor
    {
        private Vector2Int chunkOffset;
        private Vector3Int size;

        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<float> biomeWeights;
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<VoronoiNoiseValues> biomeVoronoiValues;

        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<Vector3> voronoiOctaveOffsets;
        [DeallocateOnJobCompletion]
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<float> steepnessWeights;
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<Vector2> elevationNoise;

        [WriteOnly]
        private NativeArray<float> voronoiWeights;

        public VoronoiNoiseHeap(Vector2Int chunkOffset, Vector3Int size,
            ref NativeArray<float> biomeWeights, ref NativeArray<VoronoiNoiseValues> biomeVoronoiValues,
            ref NativeArray<Vector3> voronoiOctaveOffsets, ref NativeArray<float> voronoiWeights,
            ref NativeArray<float> steepnessWeights, ref NativeArray<Vector2> elevationNoise)
        {
            this.chunkOffset = chunkOffset;
            this.size = size;
            this.biomeWeights = biomeWeights;
            this.biomeVoronoiValues = biomeVoronoiValues;
            this.voronoiOctaveOffsets = voronoiOctaveOffsets;
            this.steepnessWeights = steepnessWeights;
            this.elevationNoise = elevationNoise;

            this.voronoiWeights = voronoiWeights;
        }

        public void Execute(int i)
        {
            int xIndex = i % size.x;
            int zIndex = i / (size.y * size.x);
            int yIndex = i / size.x % size.y;

            //Calculate noise value using 3d index
            float totalWeight = 0f;

            float steepnessWeight = steepnessWeights[zIndex * size.z + xIndex];
            Vector2 elevation = elevationNoise[zIndex * size.z + xIndex];
            float elevationWeight = Mathf.Clamp((yIndex - elevation.x) *
                elevation.y, -2f, 2f);
            if (steepnessWeight > 0.001f && elevationWeight > -2f && elevationWeight < 2f)
            {
                for (int j = 0; j < biomeVoronoiValues.Length; j++)
                {
                    VoronoiNoiseValues voronoiValues = biomeVoronoiValues[j];
                    float biomeWeight = biomeWeights[(zIndex * size.z + xIndex) * biomeVoronoiValues.Length + j];
                    if (biomeWeight > 0f && voronoiValues.enabled)
                    {
                        float voronoiAmplitude = 1.0f;
                        float voronoiFrequency = 1.0f;

                        float totalVoronoiNoise = 0f;
                        for (int k = 0; k < voronoiValues.voronoiOctaves; k++)
                        {
                            float currentXVoronoi = (xIndex + chunkOffset.x) / voronoiValues.voronoiScale *
                                voronoiFrequency + voronoiOctaveOffsets[k].x;
                            float currentYVoronoi = yIndex / voronoiValues.voronoiScale *
                                voronoiFrequency + voronoiOctaveOffsets[k].y;
                            float currentZVoronoi = (zIndex + chunkOffset.y) / voronoiValues.voronoiScale *
                                voronoiFrequency + voronoiOctaveOffsets[k].z;
                            float2 voronoiNoiseWeight = noise.cellular(new float3(currentXVoronoi,
                                currentYVoronoi, currentZVoronoi)) * voronoiAmplitude;
                            totalVoronoiNoise += voronoiNoiseWeight.y - voronoiNoiseWeight.x;

                            voronoiAmplitude *= voronoiValues.voronoiPersistence;
                            voronoiFrequency *= voronoiValues.voronoiLacunarity;
                        }

                        float currentXApplicationVoronoi = (xIndex + chunkOffset.x) / voronoiValues.voronoiApplicationScale;
                        float currentYApplicationVoronoi = yIndex / voronoiValues.voronoiApplicationScale;
                        float currentZApplicationVoronoi = (zIndex + chunkOffset.y) / voronoiValues.voronoiApplicationScale;
                        float2 voronoiApplicationNoise = noise.cellular(new float3(currentXApplicationVoronoi,
                            currentYApplicationVoronoi, currentZApplicationVoronoi));
                        float totalVoronoiApplicationNoise = voronoiApplicationNoise.y - voronoiApplicationNoise.x;

                        totalWeight += totalVoronoiNoise * totalVoronoiApplicationNoise * biomeWeight * voronoiValues.voronoiPower;
                    }
                }

                totalWeight *= steepnessWeight;
            }

            voronoiWeights[i] = totalWeight;
        }
    }

    [BurstCompile]
    public struct SteepnessWeightHeap : IJobParallelFor
    {
        private Vector3Int size;
        private Vector3Int extendedSize;
        private int voronoiSteepnessSampleCount;

        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<float> voronoiSteepnessSamples;
        [NativeDisableParallelForRestriction]
        [DeallocateOnJobCompletion]
        [ReadOnly]
        private NativeArray<Vector2> elevationNoise;

        [NativeDisableParallelForRestriction]
        [WriteOnly]
        private NativeArray<float> steepnessWeights;


        public SteepnessWeightHeap(Vector3Int size, int voronoiSteepnessSampleCount,
            ref NativeArray<float> voronoiSteepnessSamples, ref NativeArray<Vector2> elevationNoise,
            ref NativeArray<float> steepnessWeights)
        {
            this.size = size;
            this.voronoiSteepnessSampleCount = voronoiSteepnessSampleCount;
            this.voronoiSteepnessSamples = voronoiSteepnessSamples;
            this.elevationNoise = elevationNoise;
            this.steepnessWeights = steepnessWeights;
            extendedSize = new Vector3Int(size.x + 2, size.y, size.z + 2);
        }

        public void Execute(int i)
        {
            int xIndex = i % size.z;
            int zIndex = i / size.z;

            float steepness = GetSteepness(xIndex, zIndex);
            float steepnessLeft = Mathf.Abs(steepness - GetSteepness(xIndex - 1, zIndex));
            float steepnessRight = Mathf.Abs(steepness - GetSteepness(xIndex + 1, zIndex));
            float steepnessUp = Mathf.Abs(steepness - GetSteepness(xIndex, zIndex + 1));
            float steepnessDown = Mathf.Abs(steepness - GetSteepness(xIndex, zIndex - 1));

            float steepnessWeight = (steepnessLeft + steepnessRight + steepnessUp + steepnessDown) / 4f;

            int steepnessIndex =
                Mathf.Clamp(Mathf.RoundToInt(steepnessWeight * voronoiSteepnessSampleCount), 0, voronoiSteepnessSampleCount - 1);
            steepnessWeight = voronoiSteepnessSamples[steepnessIndex];
            steepnessWeights[zIndex * size.z + xIndex] = steepnessWeight;
        }

        private float GetSteepness(int xIndex, int zIndex)
        {
            xIndex++;
            zIndex++;

            return elevationNoise[zIndex * extendedSize.z + xIndex].x;
        }
    }

    [BurstCompile]
    public struct TerrainNoiseHeap : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        [WriteOnly]
        private NativeArray<float> weights;

        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<NoiseValues> biomeTerrainValues;
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<float> biomeWeights;

        private Vector2 chunkOffset;
        private Vector3Int size;
        private int verticalSampleRate;
        private int horizontalSampleRate;

        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<Vector3> octaveOffsets;

        public TerrainNoiseHeap(ref NativeArray<float> weights, ref NativeArray<NoiseValues> biomeTerrainValues,
            ref NativeArray<float> biomeWeights, Vector2 chunkOffset, Vector3Int size, int verticalSampleRate,
            int horizontalSampleRate, ref NativeArray<Vector3> octaveOffsets)
        {
            this.weights = weights;
            this.biomeTerrainValues = biomeTerrainValues;
            this.biomeWeights = biomeWeights;
            this.chunkOffset = chunkOffset;
            this.size = size;
            this.verticalSampleRate = verticalSampleRate;
            this.horizontalSampleRate = horizontalSampleRate;
            this.octaveOffsets = octaveOffsets;
        }

        //Calculate noise value for specific index of array
        public void Execute(int i)
        {
            //Calculate 3d index
            int xIndex = i % size.x;
            int zIndex = i / (size.y * size.x);
            int yIndex = i / size.x % size.y;
            if (yIndex % verticalSampleRate == 0 && xIndex % horizontalSampleRate == 0 && zIndex % horizontalSampleRate == 0)
            {
                //Calculate noise value using 3d index
                float totalWeight = 0f;

                for (int j = 0; j < biomeTerrainValues.Length; j++)
                {
                    float biomeWeight = biomeWeights[(zIndex * size.z + xIndex) * biomeTerrainValues.Length + j];
                    if (biomeWeight > 0f)
                    {
                        NoiseValues terrainValues = biomeTerrainValues[j];
                        double weight = 0.0;
                        double amplitude = 1.0;
                        double frequency = 1.0;
                        //Apply octaves
                        for (int l = 0; l < terrainValues.octaves; l++)
                        {
                            Vector3 octaveOffset = octaveOffsets[l];
                            double currentX = (xIndex + chunkOffset.x) / terrainValues.scale * frequency + octaveOffset.x;
                            double currentY = yIndex / terrainValues.scale * frequency + octaveOffset.y;
                            double currentZ = (zIndex + chunkOffset.y) / terrainValues.scale * frequency + octaveOffset.z;
                            double noise = Perlin.Noise(currentX, currentY, currentZ);
                            weight += noise * amplitude;

                            amplitude *= terrainValues.persistence;
                            frequency *= terrainValues.lacunarity;
                        }

                        totalWeight += (float)weight * biomeWeight;
                    }
                }

                //Assign noise value to array
                weights[zIndex * size.x * size.y + yIndex * size.x + xIndex] = totalWeight;
            }
        }
    }

    [BurstCompile]
    public struct TerrainNoiseFill : IJobParallelFor
    {
        private Vector3Int size;
        private int verticalSampleRate;
        private int horizontalSampleRate;

        [ReadOnly]
        [DeallocateOnJobCompletion]
        private NativeArray<float> weights;
        [WriteOnly]
        private NativeArray<float> finalWeights;

        private bool ridgedNoiseEnabled;
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<RidgedNoiseApplicationValues> ridgedNoisePasses;
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        [DeallocateOnJobCompletion]
        private NativeArray<float> ridgedNoiseWeights;
        private int ridgedCurveSampleCount;
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<float> ridgedNoiseHeightDistSamples;

        private bool elevationNoiseEnabled;
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<Vector2> elevationNoise;

        private bool voronoiNoiseEnabled;
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        [DeallocateOnJobCompletion]
        private NativeArray<float> voronoiWeights;

        public TerrainNoiseFill(int verticalSampleRate, int horizontalSampleRate, Vector3Int size,
            ref NativeArray<float> weights, ref NativeArray<float> finalWeights,
            bool ridgedNoiseEnabled, ref NativeArray<RidgedNoiseApplicationValues> ridgedNoisePasses,
            ref NativeArray<float> ridgedNoiseWeights, ref NativeArray<float> ridgedNoiseHeightDistSamples,
            bool elevationNoiseEnabled, ref NativeArray<Vector2> elevationNoise,
            int ridgedCurveSampleCount, bool voronoiNoiseEnabled, ref NativeArray<float> voronoiWeights)
        {
            this.verticalSampleRate = verticalSampleRate;
            this.horizontalSampleRate = horizontalSampleRate;
            this.size = size;
            this.weights = weights;
            this.finalWeights = finalWeights;
            this.ridgedNoiseEnabled = ridgedNoiseEnabled;
            this.ridgedNoisePasses = ridgedNoisePasses;
            this.ridgedNoiseWeights = ridgedNoiseWeights;
            this.ridgedCurveSampleCount = ridgedCurveSampleCount;
            this.ridgedNoiseHeightDistSamples = ridgedNoiseHeightDistSamples;
            this.elevationNoiseEnabled = elevationNoiseEnabled;
            this.elevationNoise = elevationNoise;
            this.voronoiNoiseEnabled = voronoiNoiseEnabled;
            this.voronoiWeights = voronoiWeights;
        }

        public void Execute(int i)
        {
            //Calculate 3d index
            int xIndex = i % size.x;
            int yIndex = i / size.x % size.y;
            int zIndex = i / (size.y * size.x);

            float weight = 0f;

            //Bottom of mesh edge case
            if (yIndex == 0)
            {
                weight = 1.0f;
            }
            //Top of mesh edge case
            else if (yIndex == size.y - 1)
            {
                weight = -1.0f;
            }
            else
            {
                if (elevationNoiseEnabled)
                {
                    //Elevation value is essentially the "surface" of the terrain at this x and y index
                    Vector2 elevation = elevationNoise[zIndex * size.z + xIndex];
                    float elevationWeight = Mathf.Clamp((yIndex - elevation.x) *
                        elevation.y, -2f, 2f);
                    weight -= elevationWeight;
                }

                //Calculate ridged weight
                if (ridgedNoiseEnabled)
                {
                    for (int l = 0; l < ridgedNoisePasses.Length; l++)
                    {
                        RidgedNoiseApplicationValues ridgedNoiseApplicationValues = ridgedNoisePasses[l];
                        float ridgedWeight = ridgedNoiseWeights[ridgedNoisePasses.Length * (zIndex * size.z + xIndex) + l] *
                            ridgedNoiseApplicationValues.ridgedPower;
                        if (ridgedWeight > 0f)
                        {
                            int riverHeightDistFactorIndex =
                                Mathf.Clamp(Mathf.RoundToInt(ridgedWeight * ridgedCurveSampleCount), 0, ridgedCurveSampleCount - 1);
                            float height = ridgedNoiseApplicationValues.height *
                                ridgedNoiseHeightDistSamples[l * ridgedCurveSampleCount + riverHeightDistFactorIndex];
                            if (height > 0f)
                            {
                                float currentHeightLevel = Mathf.Abs(yIndex - ridgedNoiseApplicationValues.applyHeight);
                                float heightDistFactor = Mathf.Pow(1f - Mathf.Clamp01(currentHeightLevel / height),
                                    ridgedNoiseApplicationValues.heightBlendPower);

                                weight -= ridgedWeight * heightDistFactor;
                            }
                        }
                    }
                }

                if (weight > -2f && weight < 2f)
                {
                    if (yIndex % verticalSampleRate == 0 && xIndex % horizontalSampleRate == 0 &&
                        zIndex % horizontalSampleRate == 0)
                    {
                        weight += weights[i];
                    }
                    else
                    {
                        float xSamplePercent = xIndex % horizontalSampleRate / (float)horizontalSampleRate;
                        float zSamplePercent = zIndex % horizontalSampleRate / (float)horizontalSampleRate;
                        float ySamplePercent = yIndex % verticalSampleRate / (float)verticalSampleRate;

                        int bottomX = xIndex / horizontalSampleRate * horizontalSampleRate;
                        int bottomZ = zIndex / horizontalSampleRate * horizontalSampleRate;
                        int bottomY = yIndex / verticalSampleRate * verticalSampleRate;
                        int topX = Mathf.Clamp(bottomX + horizontalSampleRate, 0, (size.x - 1) / horizontalSampleRate * horizontalSampleRate);
                        int topZ = Mathf.Clamp(bottomZ + horizontalSampleRate, 0, (size.z - 1) / horizontalSampleRate * horizontalSampleRate);
                        int topY = Mathf.Clamp(bottomY + verticalSampleRate, 0, (size.y - 1) / verticalSampleRate * verticalSampleRate);

                        weight += TriLerp(weights[GetIndex(bottomX, bottomY, bottomZ)], weights[GetIndex(topX, bottomY, bottomZ)],
                            weights[GetIndex(bottomX, bottomY, topZ)], weights[GetIndex(topX, bottomY, topZ)],
                            weights[GetIndex(bottomX, topY, bottomZ)], weights[GetIndex(topX, topY, bottomZ)],
                            weights[GetIndex(bottomX, topY, topZ)], weights[GetIndex(topX, topY, topZ)], xSamplePercent,
                            zSamplePercent, ySamplePercent);
                    }
                }

                if (voronoiNoiseEnabled)
                {
                    weight += voronoiWeights[i];
                }
            }

            finalWeights[i] = weight;
        }

        private int GetIndex(int xIndex, int yIndex, int zIndex)
        {
            return zIndex * size.x * size.y + yIndex * size.x + xIndex;
        }

        private float TriLerp(float a, float b, float c, float d, float e, float f, float g, float h, float s,
            float t, float u)
        {
            if (a == b && b == c && c == d && d == e && e == f && f == g && g == h)
            {
                return a;
            }
            else
            {
                float abcd = BiLerp(a, b, c, d, s, t);
                float efgh = BiLerp(e, f, g, h, s, t);
                return Mathf.Lerp(abcd, efgh, u);
            }
        }

        public float BiLerp(float a, float b, float c, float d, float s, float t)
        {
            float abs = Mathf.Lerp(a, b, s);
            float cds = Mathf.Lerp(c, d, s);
            return Mathf.Lerp(abs, cds, t);
        }
    }
    #endregion

    #region Mesh Generation Jobs
    [BurstCompile]
    public struct March : IJobParallelFor
    {
        private Vector3Int size;
        private float densityThreshold;
        [ReadOnly]
        private NativeArray<float> weights;
        [WriteOnly]
        private NativeQueue<TerrainMeshTrig>.ParallelWriter meshTrigs;

        public March(Vector3Int size, float densityThreshold, ref NativeArray<float> weights,
            NativeQueue<TerrainMeshTrig>.ParallelWriter meshTrigs)
        {
            this.size = size;
            this.densityThreshold = densityThreshold;
            this.weights = weights;
            this.meshTrigs = meshTrigs;
        }

        public void Execute(int i)
        {
            //Calculate 3d index
            int yIndex = i / size.x % size.y;
            int xIndex = i % size.x;
            int zIndex = i / (size.y * size.x);
            if (xIndex < size.x - 1 && yIndex < size.y - 1 && zIndex < size.z - 1)
            {
                //Calculate index of correct marching cube preset
                int cubeIndex = 0;
                if (GetWeight(xIndex, yIndex + 1, zIndex) > densityThreshold)
                {
                    cubeIndex |= 1 << 0;
                }
                if (GetWeight(xIndex + 1, yIndex + 1, zIndex) > densityThreshold)
                {
                    cubeIndex |= 1 << 1;
                }
                if (GetWeight(xIndex + 1, yIndex, zIndex) > densityThreshold)
                {
                    cubeIndex |= 1 << 2;
                }
                if (GetWeight(xIndex, yIndex, zIndex) > densityThreshold)
                {
                    cubeIndex |= 1 << 3;
                }
                if (GetWeight(xIndex, yIndex + 1, zIndex + 1) > densityThreshold)
                {
                    cubeIndex |= 1 << 4;
                }
                if (GetWeight(xIndex + 1, yIndex + 1, zIndex + 1) > densityThreshold)
                {
                    cubeIndex |= 1 << 5;
                }
                if (GetWeight(xIndex + 1, yIndex, zIndex + 1) > densityThreshold)
                {
                    cubeIndex |= 1 << 6;
                }
                if (GetWeight(xIndex, yIndex, zIndex + 1) > densityThreshold)
                {
                    cubeIndex |= 1 << 7;
                }

                //Filter out cubes that completely solid
                if (cubeIndex != 255)
                {
                    //Loop through each edge of cube
                    TerrainMeshTrig meshTrig = new TerrainMeshTrig();
                    for (int l = 0; l < 16; l++)
                    {
                        int edgeIndex = MarchingCubesTable1D.Triangles[cubeIndex * 16 + l];
                        if (edgeIndex > -1)
                        {
                            int indexA = MarchingCubesTable1D.CornerPoints[edgeIndex * 2];
                            int indexB = MarchingCubesTable1D.CornerPoints[edgeIndex * 2 + 1];

                            Vector3Int cornerPositionA = GetCornerPosition(indexA) + new Vector3Int(xIndex, yIndex, zIndex);
                            Vector3Int cornerPositionB = GetCornerPosition(indexB) + new Vector3Int(xIndex, yIndex, zIndex);

                            //Calculate correct vertex position using densities
                            float cornerDensityA = GetWeight(cornerPositionA.x, cornerPositionA.y, cornerPositionA.z);
                            float cornerDensityB = GetWeight(cornerPositionB.x, cornerPositionB.y, cornerPositionB.z);
                            float densityPercent = cornerDensityA == cornerDensityB ? cornerDensityA :
                                Mathf.InverseLerp(cornerDensityA, cornerDensityB, densityThreshold);

                            //Interpolate between corner positions based on where
                            //density threshold is between the two corners
                            Vector3 vertexPos = Vector3.Lerp((Vector3)cornerPositionA, (Vector3)cornerPositionB, densityPercent);

                            //Assign vertices to correct position in trig
                            int trigIndex = l % 3;
                            if (trigIndex == 0)
                            {
                                meshTrig.vertex1 = vertexPos;
                            }
                            else if (trigIndex == 1)
                            {
                                meshTrig.vertex2 = vertexPos;
                            }
                            else if (trigIndex == 2)
                            {
                                meshTrig.vertex3 = vertexPos;
                                meshTrigs.Enqueue(meshTrig);
                                meshTrig = new TerrainMeshTrig();
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
        }

        private float GetWeight(int x, int y, int z)
        {
            return weights[z * size.x * size.y + y * size.x + x];
        }

        public Vector3Int GetCornerPosition(int cornerIndex)
        {
            if (cornerIndex == 0)
            {
                return new Vector3Int(0, 1, 0);
            }
            else if (cornerIndex == 1)
            {
                return new Vector3Int(1, 1, 0);
            }
            else if (cornerIndex == 2)
            {
                return new Vector3Int(1, 0, 0);
            }
            else if (cornerIndex == 3)
            {
                return Vector3Int.zero;
            }
            else if (cornerIndex == 4)
            {
                return new Vector3Int(0, 1, 1);
            }
            else if (cornerIndex == 5)
            {
                return new Vector3Int(1, 1, 1);
            }
            else if (cornerIndex == 6)
            {
                return new Vector3Int(1, 0, 1);
            }
            else
            {
                return new Vector3Int(0, 0, 1);
            }
        }
    }

    [BurstCompile]
    public struct VertexFilter : IJob
    {
        private Vector3Int size;

        [NativeDisableParallelForRestriction]
        [DeallocateOnJobCompletion]
        private NativeArray<TerrainMeshTrig> vertHeap;

        public NativeList<Vector3> verts;
        public NativeList<int> trigs;
        public NativeList<Vector2> uvs;

        [NativeDisableParallelForRestriction]
        public NativeParallelHashMap<Vector3, int> validatedVertices;

        public VertexFilter(Vector3Int size, ref NativeQueue<TerrainMeshTrig> meshTrigs, ref NativeList<Vector3> verts,
            ref NativeList<int> trigs, ref NativeList<Vector2> uvs, ref NativeParallelHashMap<Vector3, int> validatedVertices)
        {
            this.size = size;
            vertHeap = meshTrigs.ToArray(Allocator.TempJob);
            this.verts = verts;
            this.trigs = trigs;
            this.uvs = uvs;
            this.validatedVertices = validatedVertices;
        }

        public void Execute()
        {
            foreach (TerrainMeshTrig vert in vertHeap)
            {
                Filter(vert.vertex1);
                Filter(vert.vertex2);
                Filter(vert.vertex3);
            }
        }

        public void Filter(Vector3 vert)
        {
            //If vertex is not already present add vertex and triangle index
            if (validatedVertices.ContainsKey(vert) == false)
            {
                verts.Add(vert);

                int vertexIndex = verts.Length - 1;
                trigs.Add(vertexIndex);
                Vector2 uv = new Vector2(vert.x / size.x, vert.z / size.z);
                uvs.Add(uv);

                validatedVertices.Add(vert, vertexIndex);
            }
            //If vertex is already present, add triangle index
            else
            {
                trigs.Add(validatedVertices[vert]);
            }
        }
    }

    [BurstCompile]
    public struct CalculateHeightMapMesh : IJob
    {
        private NativeArray<float> heightMap;

        private NativeList<Vector3> verts;
        private NativeList<Vector2> uvs;
        private NativeList<int> trigs;

        private Vector2Int size;
        private float maxHeight;

        public CalculateHeightMapMesh(ref NativeArray<float> heightMap, ref NativeList<Vector3> verts, ref NativeList<Vector2> uvs,
            ref NativeList<int> trigs, Vector2Int size, float maxHeight)
        {
            this.heightMap = heightMap;
            this.verts = verts;
            this.uvs = uvs;
            this.trigs = trigs;

            this.size = size;
            this.maxHeight = maxHeight;
        }

        public void Execute()
        {
            int vertIndex = 0;

            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    verts.Add(new Vector3(x, heightMap[y * size.y + x] * maxHeight, y));
                    uvs.Add(new Vector2(x / (float)size.x, y / (float)size.y));
                    if (x < size.x - 1 && y < size.y - 1)
                    {
                        trigs.Add(vertIndex);
                        trigs.Add(vertIndex + size.x + 1);
                        trigs.Add(vertIndex + size.x);
                        trigs.Add(vertIndex + size.x + 1);
                        trigs.Add(vertIndex);
                        trigs.Add(vertIndex + 1);
                    }
                    vertIndex++;
                }
            }
        }
    }
    #endregion

    #region Object Placement Jobs
    [BurstCompile]
    struct CompileVertexData : IJobParallelFor
    {
        [ReadOnly]
        private NativeArray<Vector3> verts;
        private Vector3Int size;
        private Vector3 chunkPosition;
        private Vector2Int chunkCoords;
        [ReadOnly]
        private NativeArray<Vector3> normals;
        [ReadOnly]
        private NativeArray<float> vegetationNoise;
        [ReadOnly]
        private NativeArray<float> rockNoise;
        [ReadOnly]
        private NativeArray<int> biomeArr;
        [ReadOnly]
        private NativeArray<float> roadWeights;
        [ReadOnly]
        private NativeArray<float> roadStartHeights;
        private float roadHeight;
        [ReadOnly]
        [NativeDisableParallelForRestriction]
        private NativeParallelMultiHashMap<Vector2Int, InfluenceBound> influenceBounds;
        [WriteOnly]
        public NativeArray<VertexData> vertexData;

        public CompileVertexData(NativeArray<Vector3> verts, Vector3Int size, Vector3 chunkPosition,
            NativeArray<Vector3> normals, NativeArray<float> vegetationNoise, NativeArray<float> rockNoise,
            NativeArray<int> biomeArr, NativeArray<float> roadWeights, NativeArray<float> roadStartHeights,
            float roadHeight, ref NativeParallelMultiHashMap<Vector2Int, InfluenceBound> influenceBounds,
            ref NativeArray<VertexData> vertexData)
        {
            this.verts = verts;
            this.size = size;
            this.chunkPosition = chunkPosition;
            this.normals = normals;
            this.vegetationNoise = vegetationNoise;
            this.rockNoise = rockNoise;
            this.biomeArr = biomeArr;
            this.roadWeights = roadWeights;
            this.roadStartHeights = roadStartHeights;
            this.roadHeight = roadHeight;
            this.influenceBounds = influenceBounds;
            this.vertexData = vertexData;

            chunkCoords = TerrainManagerUtility.GetCoords(chunkPosition, size);
        }

        public void Execute(int i)
        {
            Vector3 vert = verts[i];

            Vector3 pos = vert + chunkPosition;
            Vector3 normal = normals[i];
            float height = pos.y;
            float steepness = 1 - (Vector3.Dot(Vector3.Normalize(normal), Vector3.up) * 0.5f + 0.5f);
            int xCoord = Mathf.FloorToInt(vert.x);
            int zCoord = Mathf.FloorToInt(vert.z);
            float vegetation = vegetationNoise[zCoord * size.z + xCoord];
            float rock = rockNoise[zCoord * size.z + xCoord];
            int biome = biomeArr[zCoord * size.z + xCoord];
            float road = roadWeights[zCoord * size.z + xCoord];
            float roadStartHeight = roadStartHeights[zCoord * size.z + xCoord];
            float vertRoadHeight = roadHeight * road;

            bool inInfluenceBound = false;
            foreach (InfluenceBound influenceBound in influenceBounds.GetValuesForKey(chunkCoords))
            {
                if (CheckInfluenceBound(pos, influenceBound.position, influenceBound.bounds))
                {
                    inInfluenceBound = true;
                    break;
                }
            }

            VertexData data = new VertexData(pos, normal, height, steepness, vegetation, rock, biome, road, roadStartHeight,
                vertRoadHeight, inInfluenceBound);
            vertexData[i] = data;
        }

        private bool CheckInfluenceBound(Vector3 pos, Vector3 center, Vector3 bound)
        {
            if (pos.x <= center.x + bound.x / 2f && pos.x >= center.x - bound.x / 2f && pos.y <= center.y + bound.y / 2f &&
                pos.y >= center.y - bound.y / 2f && pos.z <= center.z + bound.z / 2f && pos.z >= center.z - bound.z / 2f)
            {
                return true;
            }

            return false;
        }
    }

    [BurstCompile]
    struct GetObjectTransforms : IJob
    {
        [ReadOnly]
        public TerrainObjectInstanceStruct terrainObjectInstance;
        [ReadOnly]
        private NativeArray<VertexData> vertexData;
        private Unity.Mathematics.Random random;

        private float roadWeightObjectSpawnThreshold;

        private bool roadsEnabled;
        private bool structuresEnabled;

        public NativeList<TerrainObjectTransform> terrainObjectTransforms;

        public GetObjectTransforms(TerrainObjectInstanceStruct terrainObjectInstance,
            ref NativeList<TerrainObjectTransform> terrainObjectTransforms, ref NativeArray<VertexData> vertexData,
            float roadWeightObjectSpawnThreshold, bool roadsEnabled, bool structuresEnabled, uint seed)
        {
            this.terrainObjectInstance = terrainObjectInstance;
            this.vertexData = vertexData;
            this.roadWeightObjectSpawnThreshold = roadWeightObjectSpawnThreshold;
            this.roadsEnabled = roadsEnabled;
            this.structuresEnabled = structuresEnabled;
            //Cant have seed = 0
            random = new Unity.Mathematics.Random(seed == 0 ? 314159 : seed);

            this.terrainObjectTransforms = terrainObjectTransforms;
        }

        public void Execute()
        {
            float spawnChance = terrainObjectInstance.spawnChance / 100f;

            foreach (VertexData vertexData in vertexData)
            {
                if (vertexData.height >= terrainObjectInstance.minSpawnHeight && vertexData.height <= terrainObjectInstance.maxSpawnHeight
                    && random.NextFloat() <= spawnChance && vertexData.steepness <= terrainObjectInstance.maxSteepness &&
                    vertexData.steepness >= terrainObjectInstance.minSteepness && vertexData.vegetation <= terrainObjectInstance.maxVegetationNoise
                    && vertexData.vegetation >= terrainObjectInstance.minVegetationNoise && vertexData.biome == terrainObjectInstance.biome
                    && (!terrainObjectInstance.rockNoiseEnabled || (vertexData.rock >= terrainObjectInstance.minRockNoise
                    && vertexData.rock <= terrainObjectInstance.maxRockNoise)) && (!roadsEnabled || (vertexData.road <= roadWeightObjectSpawnThreshold ||
                    vertexData.height < vertexData.roadStartHeight - vertexData.roadHeight || vertexData.height > vertexData.roadStartHeight + vertexData.roadHeight))
                    && (!structuresEnabled || vertexData.insideInfluenceBound == false))
                {
                    Vector3 pos = vertexData.pos;

                    if (terrainObjectInstance.heightOffsetRelativeToSlope)
                    {
                        pos += vertexData.normal * terrainObjectInstance.heightOffset;
                    }
                    else
                    {
                        pos.y += terrainObjectInstance.heightOffset;
                    }

                    Vector3 scale = terrainObjectInstance.objectScale;
                    if (terrainObjectInstance.randomScale)
                    {
                        if (terrainObjectInstance.uniformScale)
                        {
                            float uniformScale = random.NextFloat(terrainObjectInstance.scale.x,
                                terrainObjectInstance.scale.y);
                            scale = new Vector3(uniformScale, uniformScale, uniformScale);
                        }
                        else
                        {
                            scale = new Vector3(random.NextFloat(terrainObjectInstance.scaleX.x, terrainObjectInstance.scaleX.y),
                                random.NextFloat(terrainObjectInstance.scaleY.x, terrainObjectInstance.scaleY.y),
                                random.NextFloat(terrainObjectInstance.scaleZ.x, terrainObjectInstance.scaleZ.y));
                        }
                    }

                    Quaternion rotation = GetRandomObjectRotation(vertexData.normal, terrainObjectInstance.slopeWeight);

                    TerrainObjectTransform transform = new TerrainObjectTransform(pos, rotation, scale);
                    terrainObjectTransforms.Add(transform);
                }
            }
        }

        private Quaternion GetRandomObjectRotation(Vector3 normal, float slopeWeight)
        {
            //Rotate object to fit against terrain
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, normal, slopeWeight));
            //Apply a random rotation to object
            Quaternion randomRot = Quaternion.Euler(0, random.NextFloat(0f, 360f), 0);

            //Combine quaternions 
            return rotation *= randomRot;
        }
    }

    [BurstCompile]
    struct CreateObjectMesh : IJobParallelFor
    {
        private int vertexCount;
        private int indexCount;
        [NativeDisableParallelForRestriction]
        [DeallocateOnJobCompletion]
        [ReadOnly]
        private NativeArray<Vector3> verts;
        [NativeDisableParallelForRestriction]
        [DeallocateOnJobCompletion]
        [ReadOnly]
        private NativeArray<int> indices;
        [NativeDisableParallelForRestriction]
        [DeallocateOnJobCompletion]
        [ReadOnly]
        private NativeArray<Vector3> normals;
        [NativeDisableParallelForRestriction]
        [DeallocateOnJobCompletion]
        [ReadOnly]
        private NativeArray<Vector4> tangents;
        [NativeDisableParallelForRestriction]
        [DeallocateOnJobCompletion]
        [ReadOnly]
        private NativeArray<Vector2> uvs1;
        [NativeDisableParallelForRestriction]
        [DeallocateOnJobCompletion]
        [ReadOnly]
        private NativeArray<Vector2> uvs2;

        [ReadOnly]
        private NativeArray<TerrainObjectTransform> transforms;
        private Vector3 chunkPosition;

        [NativeDisableParallelForRestriction]
        [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestriction]
        [WriteOnly]
        public NativeArray<MeshVertexData> finalVertexData;
        [NativeDisableParallelForRestriction]
        [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestriction]
        [WriteOnly]
        public NativeArray<uint> finalIndices;

        public CreateObjectMesh(int vertexCount, int indexCount, NativeArray<Vector3> verts, NativeArray<int> indices,
            NativeArray<Vector3> normals, NativeArray<Vector4> tangents, NativeArray<Vector2> uvs1, NativeArray<Vector2> uvs2,
            NativeArray<TerrainObjectTransform> transforms, Vector3 chunkPosition, NativeArray<MeshVertexData> finalVertexData,
            NativeArray<uint> finalIndices)
        {
            this.vertexCount = vertexCount;
            this.indexCount = indexCount;
            this.verts = verts;
            this.indices = indices;
            this.normals = normals;
            this.tangents = tangents;
            this.uvs1 = uvs1;
            this.uvs2 = uvs2;
            this.transforms = transforms;
            this.chunkPosition = chunkPosition;
            this.finalVertexData = finalVertexData;
            this.finalIndices = finalIndices;
        }

        public void Execute(int i)
        {
            TerrainObjectTransform transform = transforms[i];

            for (int l = 0; l < vertexCount; l++)
            {
                Vector3 vert = transform.GetRot() * verts[l];
                vert += transform.GetPos() - chunkPosition;
                MeshVertexData data = new MeshVertexData(vert, normals[l], tangents[l], uvs1[l], uvs2[l]);
                finalVertexData[i * vertexCount + l] = data;
            }

            for (int l = 0; l < indexCount; l++)
            {
                ushort currentTrig = (ushort)(indices[l] + i * vertexCount);
                finalIndices[i * indexCount + l] = currentTrig;
            }
        }
    }
    #endregion

    #region Misc Jobs
    [BurstCompile]
    struct ChunkGenerationPriorityJob : IJob
    {
        private NativeList<Vector2Int> chunksToGenerate;
        private NativeParallelHashSet<Vector2Int> chunkCoordHeap;

        private int chunkRadius;
        private Vector2Int originCoords;
        private Vector3 playerForwardDir;
        private float maxViewAngle;
        private float viewGenerationFactor;

        public ChunkGenerationPriorityJob(ref NativeList<Vector2Int> chunksToGenerate,
            ref NativeParallelHashSet<Vector2Int> chunkCoordHeap, int chunkRadius, Vector2Int originCoords,
            Vector3 playerForwardDir, float maxViewAngle, float viewGenerationFactor)
        {
            this.chunksToGenerate = chunksToGenerate;
            this.chunkCoordHeap = chunkCoordHeap;
            this.chunkRadius = chunkRadius;
            this.originCoords = originCoords;
            this.playerForwardDir = playerForwardDir;
            this.maxViewAngle = maxViewAngle;
            this.viewGenerationFactor = viewGenerationFactor;
        }

        public void Execute()
        {
            int minPriority = int.MaxValue;

            for (int i = 0; i < chunkRadius; i++)
            {
                for (int x = -i; x <= i; x++)
                {
                    for (int y = -i; y <= i; y++)
                    {
                        if (x == i || x == -i || y == i || y == -i)
                        {
                            Vector2Int coords = new Vector2Int(x, y) + originCoords;
                            if (chunkCoordHeap.Contains(coords) == false)
                            {
                                float distance = Vector2Int.Distance(Vector2Int.zero, new Vector2Int(x, y));
                                float viewDirAngle = Vector2.Angle(new Vector2(x, y).normalized,
                                    new Vector2(playerForwardDir.x, playerForwardDir.z));
                                float inView = viewDirAngle <= maxViewAngle ? 1f : 0f;
                                int priority = Mathf.Clamp(Mathf.RoundToInt(distance - inView * viewGenerationFactor), 0, int.MaxValue);

                                if (priority == minPriority)
                                {
                                    chunksToGenerate.Add(coords);
                                }
                                else if (priority < minPriority)
                                {
                                    chunksToGenerate.Clear();
                                    chunksToGenerate.Add(coords);
                                    minPriority = priority;
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    [BurstCompile]
    struct CalculateBiomeWeights : IJobParallelFor
    {
        [ReadOnly]
        [NativeDisableParallelForRestriction]
        private NativeArray<float> tempNoise;
        [ReadOnly]
        [NativeDisableParallelForRestriction]
        private NativeArray<float> moistureNoise;
        [ReadOnly]
        [NativeDisableParallelForRestriction]
        private NativeArray<float> biomeGrid;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        private NativeArray<float> biomeWeights;
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        private NativeArray<int> biomeArr;

        private int biomeGridSize;
        private int biomeCount;

        public CalculateBiomeWeights(ref NativeArray<float> tempNoise, ref NativeArray<float> moistureNoise,
            ref NativeArray<float> biomeGrid, ref NativeArray<float> biomeWeights, ref NativeArray<int> biomeArr,
            int biomeGridSize, int biomeCount)
        {
            this.tempNoise = tempNoise;
            this.moistureNoise = moistureNoise;
            this.biomeGrid = biomeGrid;

            this.biomeWeights = biomeWeights;
            this.biomeArr = biomeArr;

            this.biomeGridSize = biomeGridSize;
            this.biomeCount = biomeCount;
        }

        public void Execute(int i)
        {
            int currentX = (int)(tempNoise[i] * biomeGridSize);
            float remainderX = tempNoise[i] * biomeGridSize - currentX;
            int currentY = (int)(moistureNoise[i] * biomeGridSize);
            float remainderY = moistureNoise[i] * biomeGridSize - currentY;

            NativeArray<float> currentBiomeWeights = new NativeArray<float>(biomeCount, Allocator.Temp);
            for (int j = 0; j < biomeCount; j++)
            {
                float currentBiomeWeightsBottomLeft = biomeGrid[GetBiomeGridIndex(currentX, currentY) + j];
                float currentBiomeWeightsBottomRight = biomeGrid[GetBiomeGridIndex(currentX + 1, currentY) + j];
                float currentBiomeWeightsTopLeft = biomeGrid[GetBiomeGridIndex(currentX, currentY + 1) + j];
                float currentBiomeWeightsTopRight = biomeGrid[GetBiomeGridIndex(currentX + 1, currentY + 1) + j];

                currentBiomeWeights[j] = BiLerp(currentBiomeWeightsBottomLeft, currentBiomeWeightsBottomRight,
                    currentBiomeWeightsTopLeft, currentBiomeWeightsTopRight, remainderX, remainderY);
            }

            float highestWeight = 0f;
            int highestIndex = 0;
            for (int j = 0; j < biomeCount; j++)
            {
                float weight = currentBiomeWeights[j];
                biomeWeights[i * biomeCount + j] = weight;

                if (weight > highestWeight)
                {
                    highestWeight = weight;
                    highestIndex = j;
                }
            }
            currentBiomeWeights.Dispose();

            biomeArr[i] = highestIndex;
        }

        private int GetBiomeGridIndex(int x, int y)
        {
            return (Mathf.Clamp(x, 0, biomeGridSize) * (biomeGridSize + 1) + Mathf.Clamp(y, 0, biomeGridSize)) * biomeCount;
        }

        private float BiLerp(float a, float b, float c, float d, float s, float t)
        {
            float abs = Mathf.Lerp(a, b, s);
            float cds = Mathf.Lerp(c, d, s);
            return Mathf.Lerp(abs, cds, t);
        }
    }

    [BurstCompile]
    struct BakeTerrainCollider : IJobParallelFor
    {
        [DeallocateOnJobCompletion]
        private NativeArray<int> meshIDs;

        public BakeTerrainCollider(NativeArray<int> meshIDs)
        {
            this.meshIDs = meshIDs;
        }

        public void Execute(int i)
        {
            Physics.BakeMesh(meshIDs[i], false);
        }
    }

    [BurstCompile]
    struct ApplyTexturesJob : IJobParallelFor
    {
        private Vector3Int size;

        [ReadOnly]
        private NativeArray<float> tempNoise;
        [ReadOnly]
        private NativeArray<float> moistureNoise;
        [ReadOnly]
        private NativeArray<float> vegetationNoise;
        [ReadOnly]
        private NativeArray<float> roadWeights;
        [ReadOnly]
        private NativeArray<float> roadStartHeights;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        private NativeArray<half> biomeWeightPixelData;
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        private NativeArray<half> vegetationNoisePixelData;
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        private NativeArray<half> roadWeightsPixelData;
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        private NativeArray<half> roadStartHeightsPixelsData;

        public ApplyTexturesJob(NativeArray<float> tempNoise, NativeArray<float> moistureNoise,
            NativeArray<float> vegetationNoise, NativeArray<float> roadWeights, NativeArray<float> roadStartHeights,
            ref NativeArray<half> biomeWeightPixelData, ref NativeArray<half> vegetationNoisePixelData,
            ref NativeArray<half> roadWeightsPixelData, ref NativeArray<half> roadStartHeightsPixelsData,
            Vector3Int size)
        {
            this.tempNoise = tempNoise;
            this.moistureNoise = moistureNoise;
            this.vegetationNoise = vegetationNoise;
            this.roadWeights = roadWeights;
            this.roadStartHeights = roadStartHeights;
            this.biomeWeightPixelData = biomeWeightPixelData;
            this.vegetationNoisePixelData = vegetationNoisePixelData;
            this.roadWeightsPixelData = roadWeightsPixelData;
            this.roadStartHeightsPixelsData = roadStartHeightsPixelsData;
            this.size = size;
        }

        public void Execute(int i)
        {
            int x = i % size.z;
            int y = i / size.z;

            float temp = tempNoise[y * size.z + x];
            float moisture = moistureNoise[y * size.z + x];
            biomeWeightPixelData[i * 2] = (half)temp;
            biomeWeightPixelData[i * 2 + 1] = (half)moisture;
            vegetationNoisePixelData[i] = (half)Mathf.Clamp01(vegetationNoise[y * size.z + x]);
            roadWeightsPixelData[i] = (half)Mathf.Clamp01(roadWeights[y * size.z + x]);
            roadStartHeightsPixelsData[i] = (half)Mathf.Clamp01(roadStartHeights[y * size.z + x] / size.y);
        }
    }

    [BurstCompile]
    struct GenerateStructureJob : IJob
    {
        private Vector2Int coords;
        private Vector3Int size;

        private TerrainStructureInstanceStruct structure;
        private NativeArray<TerrainStructurePrefabStruct> structurePrefabs;

        private NativeList<TerrainStructureTransform> structureTransforms;

        public NativeParallelHashMap<int, int> prefabCounts;
        public NativeList<StructurePosition> positions;

        public GenerateStructureJob(Vector2Int coords, Vector3Int size,
            TerrainStructureInstanceStruct structure, NativeArray<TerrainStructurePrefabStruct> structurePrefabs,
            ref NativeList<TerrainStructureTransform> structureTransforms)
        {
            this.coords = coords;
            this.size = size;

            this.structure = structure;
            this.structurePrefabs = structurePrefabs;

            this.structureTransforms = structureTransforms;

            prefabCounts = new NativeParallelHashMap<int, int>(100, Allocator.TempJob);
            positions = new NativeList<StructurePosition>(100, Allocator.TempJob);
        }

        public void Execute()
        {
            uint seed = (uint)TerrainManagerUtility.GetSeed(coords);
            Unity.Mathematics.Random rand = new Unity.Mathematics.Random(seed);

            Vector3 chunkPos = TerrainManagerUtility.TerrainToWorld(Vector3Int.zero, coords, size);
            int spawnedPrefabs = rand.NextInt(structure.minSpawnedPrefabs, structure.maxSpawnedPrefabs + 1);
            for (int i = 0; i < spawnedPrefabs; i++)
            {
                TerrainStructurePrefabStruct terrainStructurePrefab = structurePrefabs[0];
                for (int l = 0; l < structurePrefabs.Length; l++)
                {
                    TerrainStructurePrefabStruct prefab = structurePrefabs[l];
                    if ((rand.NextFloat() <= prefab.spawnChance && (!prefabCounts.ContainsKey(prefab.index) ||
                        prefabCounts[prefab.index] < prefab.maxSpawned)) || l == structurePrefabs.Length - 1)
                    {
                        terrainStructurePrefab = prefab;
                        if (prefabCounts.ContainsKey(terrainStructurePrefab.index))
                        {
                            prefabCounts[terrainStructurePrefab.index]++;
                        }
                        else
                        {
                            prefabCounts.Add(terrainStructurePrefab.index, 1);
                        }
                        break;
                    }
                }

                Vector3 position = chunkPos + new Vector3(rand.NextFloat(-structure.structureRadius, structure.structureRadius),
                    0f, rand.NextFloat(-structure.structureRadius, structure.structureRadius));
                int sentinel = 0;
                while (CheckDist(position, terrainStructurePrefab.minNeighborDistance) == false && sentinel < 100)
                {
                    float x = chunkPos.x + rand.NextFloat(-structure.structureRadius, structure.structureRadius);
                    float y = chunkPos.z + rand.NextFloat(-structure.structureRadius, structure.structureRadius);

                    position = new Vector3(x, 0f, y);

                    sentinel++;
                }

                position = new Vector3(Mathf.Round(position.x), 0f, Mathf.Round(position.z));
                positions.Add(new StructurePosition(position, terrainStructurePrefab.minNeighborDistance));

                TerrainStructureTransform transform = new TerrainStructureTransform(position,
                    Vector3.zero, terrainStructurePrefab.scale, terrainStructurePrefab.influenceBounds,
                    terrainStructurePrefab.index);

                structureTransforms.Add(transform);
            }
        }

        private bool CheckDist(Vector3 position, float minDist)
        {
            bool checkDist = true;
            foreach (StructurePosition transform in positions)
            {
                if (Vector3.Distance(position, transform.position) < Mathf.Min(minDist, transform.minDist))
                {
                    checkDist = false;
                }
            }
            return checkDist;
        }
    }

    [BurstCompile]
    public struct ApplyInfluenceBounds : IJob
    {
        public Vector3Int size;
        public Vector3 position;
        public Vector3Int bounds;

        public NativeParallelMultiHashMap<Vector2Int, PersistentWeightChange> weightChanges;
        public NativeParallelHashMap<HashableIndex2, PersistentRoadChange> roadChanges;
        public NativeParallelMultiHashMap<Vector2Int, InfluenceBound> influenceBounds;

        private InfluenceBound influenceBound;
        private Vector3Int halfBounds;

        public ApplyInfluenceBounds(Vector3Int size, Vector3 position, Vector3Int bounds,
            ref NativeParallelMultiHashMap<Vector2Int, PersistentWeightChange> weightChanges,
            ref NativeParallelHashMap<HashableIndex2, PersistentRoadChange> roadChanges,
            ref NativeParallelMultiHashMap<Vector2Int, InfluenceBound> influenceBounds)
        {
            this.size = size;
            this.position = position;
            this.bounds = bounds;
            this.weightChanges = weightChanges;
            this.roadChanges = roadChanges;
            this.influenceBounds = influenceBounds;

            influenceBound = new InfluenceBound(position, bounds);
            halfBounds = new Vector3Int((int)(bounds.x / 2f), (int)(bounds.y / 2f), (int)(bounds.z / 2f));
        }

        public void Execute()
        {
            for (int x = -halfBounds.x; x < halfBounds.x; x++)
            {
                for (int z = -halfBounds.z; z < halfBounds.z; z++)
                {
                    Vector3 pos = position + new Vector3(x, 0f, z);
                    Vector2Int coord = TerrainManagerUtility.GetCoords(pos, size);
                    Vector3Int index = TerrainManagerUtility.WorldToTerrain(pos, size);

                    NativeList<InfluenceIndex> indices = GetIndices(coord, index);
                    float roadWeight = 1f - Mathf.Clamp01(Vector2.Distance(new Vector2(x, z),
                        Vector2.zero) / Mathf.Max(halfBounds.x, halfBounds.y));

                    foreach (InfluenceIndex influenceIndex in indices)
                    {
                        HashableIndex2 hash = new HashableIndex2(new
                            Vector2Int(influenceIndex.index.x, influenceIndex.index.z), influenceIndex.coord);
                        if (roadChanges.ContainsKey(hash))
                        {
                            roadChanges[hash] = roadChanges[hash].roadWeight > roadWeight ?
                                roadChanges[hash] : new PersistentRoadChange(roadWeight, position.y + 1f);
                        }
                        else
                        {
                            roadChanges.Add(hash,
                                new PersistentRoadChange(roadWeight, position.y + 1f));
                        }

                        //Add influence bounds to each relevant chunk
                        foreach (InfluenceBound bound in influenceBounds.GetValuesForKey(influenceIndex.coord))
                        {
                            if (bound.position != influenceBound.position)
                            {
                                influenceBounds.Add(influenceIndex.coord, influenceBound);
                            }
                        }
                    }

                    for (int y = -halfBounds.y; y < halfBounds.y; y++)
                    {
                        //Calculate weight changes
                        float weightChange = (1f - Mathf.Clamp01(Vector3.Distance(Vector3.zero,
                            new Vector3(x, y, z)) / Mathf.Max(halfBounds.x, halfBounds.z))) * (y >= 0f ? -1f : 1f);

                        foreach (InfluenceIndex influenceIndex in indices)
                        {
                            //Calculate road weight changes
                            PersistentWeightChange persistentWeightChange =
                                new PersistentWeightChange(influenceIndex.index.x, influenceIndex.index.y + y,
                                influenceIndex.index.z, weightChange);
                            weightChanges.Add(influenceIndex.coord, persistentWeightChange);
                        }
                    }
                    indices.Dispose();
                }
            }
        }

        private NativeList<InfluenceIndex> GetIndices(Vector2Int coords, Vector3Int index)
        {
            NativeList<InfluenceIndex> indices = new NativeList<InfluenceIndex>(Allocator.Temp);
            indices.Add(new InfluenceIndex(coords, index));
            if (index.x == 0)
            {
                indices.Add(new InfluenceIndex(new Vector2Int(coords.x - 1, coords.y),
                    new Vector3Int(size.x - 1, index.y, index.z)));
            }
            if (index.z == 0)
            {
                indices.Add(new InfluenceIndex(new Vector2Int(coords.x, coords.y - 1),
                    new Vector3Int(index.x, index.y, size.z - 1)));
            }
            if (index.x == size.x - 1)
            {
                indices.Add(new InfluenceIndex(new Vector2Int(coords.x + 1, coords.y),
                    new Vector3Int(0, index.y, index.z)));
            }
            if (index.z == size.z - 1)
            {
                indices.Add(new InfluenceIndex(new Vector2Int(coords.x, coords.y + 1),
                    new Vector3Int(index.x, index.y, 0)));
            }
            if (index.x == 0 && index.z == 0)
            {
                indices.Add(new InfluenceIndex(new Vector2Int(coords.x - 1, coords.y - 1),
                    new Vector3Int(size.x - 1, index.y, size.z - 1)));
            }
            if (index.x == size.x - 1 && index.z == size.z - 1)
            {
                indices.Add(new InfluenceIndex(new Vector2Int(coords.x + 1, coords.y + 1),
                    new Vector3Int(0, index.y, 0)));
            }
            if (index.x == 0 && index.z == size.z - 1)
            {
                indices.Add(new InfluenceIndex(new Vector2Int(coords.x - 1, coords.y + 1),
                    new Vector3Int(size.x - 1, index.y, 0)));
            }
            if (index.x == size.x - 1 && index.z == 0)
            {
                indices.Add(new InfluenceIndex(new Vector2Int(coords.x + 1, coords.y - 1),
                    new Vector3Int(0, index.y, size.z - 1)));
            }
            return indices;
        }
    }

    [BurstCompile]
    public struct ConnectStructures : IJob
    {
        private Vector3Int size;
        private Vector3 position;
        private Vector3 neighborPosition;

        public NativeParallelHashMap<HashableIndex2, PersistentRoadChange> roadChanges;

        private float structurePathStartHeightOffset;

        public ConnectStructures(Vector3Int size, Vector3 position, Vector3 neighborPosition,
            ref NativeParallelHashMap<HashableIndex2, PersistentRoadChange> roadChanges,
            float structurePathStartHeightOffset)
        {
            this.size = size;
            this.position = position;
            this.neighborPosition = neighborPosition;
            this.roadChanges = roadChanges;
            this.structurePathStartHeightOffset = structurePathStartHeightOffset;
        }

        public void Execute()
        {
            Vector2 pos = new Vector2(position.x, position.z);
            Vector2 neighborPos = new Vector2(neighborPosition.x, neighborPosition.z);
            int connectionIterations = Mathf.RoundToInt(Vector2.Distance(pos, neighborPos));
            Vector2 dir = (neighborPos - pos).normalized;
            int iterationRadius = 8;
            float maxDistance = Vector2.Distance(Vector2.zero, new Vector2(iterationRadius, iterationRadius));
            for (float i = 0; i <= connectionIterations; i += 0.5f)
            {
                Vector2 iterationPos = pos + dir * i;
                float roadHeight = Mathf.Lerp(position.y, neighborPosition.y, i / connectionIterations) +
                    structurePathStartHeightOffset;

                for (int x = -iterationRadius; x <= iterationRadius; x++)
                {
                    for (int z = -iterationRadius; z <= iterationRadius; z++)
                    {
                        Vector3 currentPos = new Vector3(iterationPos.x + x, roadHeight, iterationPos.y + z);
                        Vector3Int index = TerrainManagerUtility.WorldToTerrain(currentPos, size);
                        Vector2Int coord = TerrainManagerUtility.GetCoords(currentPos, size);
                        float roadWeight = 1f - Mathf.Clamp01(Vector2.Distance(Vector2.zero, new Vector2(x, z)) /
                            maxDistance);

                        NativeList<InfluenceIndex> influenceIndices = GetIndices(coord, index);
                        foreach (InfluenceIndex influenceIndex in influenceIndices)
                        {
                            HashableIndex2 hash = new HashableIndex2(new Vector2Int(influenceIndex.index.x,
                                influenceIndex.index.z), influenceIndex.coord);
                            if (roadChanges.ContainsKey(hash))
                            {
                                roadChanges[hash] = roadChanges[hash].roadWeight > roadWeight ?
                                    roadChanges[hash] : new PersistentRoadChange(roadWeight, roadHeight);
                            }
                            else
                            {
                                roadChanges.Add(hash,
                                    new PersistentRoadChange(roadWeight, roadHeight));
                            }
                        }
                    }
                }
            }
        }

        private NativeList<InfluenceIndex> GetIndices(Vector2Int coords, Vector3Int index)
        {
            NativeList<InfluenceIndex> indices = new NativeList<InfluenceIndex>(Allocator.Temp);
            indices.Add(new InfluenceIndex(coords, index));
            if (index.x == 0)
            {
                indices.Add(new InfluenceIndex(new Vector2Int(coords.x - 1, coords.y),
                    new Vector3Int(size.x - 1, index.y, index.z)));
            }
            if (index.z == 0)
            {
                indices.Add(new InfluenceIndex(new Vector2Int(coords.x, coords.y - 1),
                    new Vector3Int(index.x, index.y, size.z - 1)));
            }
            if (index.x == size.x - 1)
            {
                indices.Add(new InfluenceIndex(new Vector2Int(coords.x + 1, coords.y),
                    new Vector3Int(0, index.y, index.z)));
            }
            if (index.z == size.z - 1)
            {
                indices.Add(new InfluenceIndex(new Vector2Int(coords.x, coords.y + 1),
                    new Vector3Int(index.x, index.y, 0)));
            }
            if (index.x == 0 && index.z == 0)
            {
                indices.Add(new InfluenceIndex(new Vector2Int(coords.x - 1, coords.y - 1),
                    new Vector3Int(size.x - 1, index.y, size.z - 1)));
            }
            if (index.x == size.x - 1 && index.z == size.z - 1)
            {
                indices.Add(new InfluenceIndex(new Vector2Int(coords.x + 1, coords.y + 1),
                    new Vector3Int(0, index.y, 0)));
            }
            if (index.x == 0 && index.z == size.z - 1)
            {
                indices.Add(new InfluenceIndex(new Vector2Int(coords.x - 1, coords.y + 1),
                    new Vector3Int(size.x - 1, index.y, 0)));
            }
            if (index.x == size.x - 1 && index.z == 0)
            {
                indices.Add(new InfluenceIndex(new Vector2Int(coords.x + 1, coords.y - 1),
                    new Vector3Int(0, index.y, size.z - 1)));
            }
            return indices;
        }
    }

    [BurstCompile]
    public struct ConformMeshToRoadWeights : IJobParallelFor
    {
        private Vector3Int size;
        private float roadHeight;
        private float minRoadDeformHeight;
        private float maxRoadDeformHeight;
        private float roadFillStrength;
        private float roadCarveStrength;

        [ReadOnly]
        private NativeArray<float> roadWeights;
        [ReadOnly]
        private NativeArray<float> roadStartHeights;
        [NativeDisableParallelForRestriction]
        [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestriction]
        private NativeArray<float> weights;

        public ConformMeshToRoadWeights(Vector3Int size, float roadHeight, float minRoadDeformHeight,
            float maxRoadDeformHeight, float roadFillStrength, float roadCarveStrength,
            ref NativeArray<float> roadWeights, ref NativeArray<float> roadStartHeights, ref NativeArray<float> weights)
        {
            this.size = size;
            this.roadHeight = roadHeight;
            this.minRoadDeformHeight = minRoadDeformHeight;
            this.maxRoadDeformHeight = maxRoadDeformHeight;
            this.roadFillStrength = roadFillStrength;
            this.roadCarveStrength = roadCarveStrength;
            this.roadWeights = roadWeights;
            this.roadStartHeights = roadStartHeights;
            this.weights = weights;
        }

        public void Execute(int i)
        {
            int x = i % size.z;
            int z = i / size.z;

            float roadWeight = roadWeights[i];

            if (roadWeight > 0f)
            {
                float roadStartHeight = roadStartHeights[i];
                for (float y = roadStartHeight - roadHeight; y <= roadStartHeight + roadHeight; y++)
                {
                    if (y > 0f && y < size.y - 1)
                    {
                        int yIndex = Mathf.Clamp(Mathf.FloorToInt(y), 0, size.y - 1);
                        int yIndexUpper = Mathf.Clamp(yIndex + 1, 0, size.y - 1);

                        float lowerStrength = yIndexUpper - y;
                        float weight = weights[z * size.x * size.y + yIndex * size.x + x];

                        float upperStrength = y - yIndex;
                        float weightUpper = weights[z * size.x * size.y + yIndexUpper * size.x + x];

                        if (y < roadStartHeight + 1f)
                        {
                            float heightFactor = Mathf.Clamp01(Mathf.InverseLerp(roadStartHeight - roadHeight,
                                roadStartHeight, y));
                            float minDeformFactor = Mathf.Clamp01(1f - (minRoadDeformHeight - y));
                            float fillStrength = roadWeight * heightFactor * roadFillStrength * minDeformFactor;
                            weight += fillStrength * lowerStrength;
                            weightUpper += fillStrength * upperStrength;
                        }
                        if (y > roadStartHeight - 1f)
                        {
                            float heightFactor = Mathf.Clamp01(Mathf.InverseLerp(roadStartHeight + roadHeight,
                                roadStartHeight, y));
                            float maxDeformFactor = Mathf.Clamp01(1f - (y - maxRoadDeformHeight));
                            float carveStrength = roadWeight * heightFactor * roadCarveStrength * maxDeformFactor;
                            weight -= carveStrength * lowerStrength;
                            weightUpper -= carveStrength * upperStrength;
                        }

                        weights[z * size.x * size.y + yIndex * size.x + x] = yIndex == 0 ? 1f : weight;
                        weights[z * size.x * size.y + yIndexUpper * size.x + x] = weightUpper;
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct CompileRoadData : IJobParallelFor
    {
        private TerrainManager.RoadElevationMode roadElevationMode;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        [DeallocateOnJobCompletion]
        private NativeArray<float> roadNoise;
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        private NativeArray<float> roadWeights;
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        private NativeArray<float> roadStartHeights;
        [ReadOnly]
        private NativeArray<Vector2> elevationNoise;
        [ReadOnly]
        [NativeDisableParallelForRestriction]
        private NativeArray<float> roadMaxHeights;
        [ReadOnly]
        [NativeDisableParallelForRestriction]
        private NativeArray<float> roadMinHeights;

        public CompileRoadData(TerrainManager.RoadElevationMode roadElevationMode,
            ref NativeArray<float> roadNoise, ref NativeArray<float> roadWeights, ref NativeArray<float> roadStartHeights,
            ref NativeArray<Vector2> elevationNoise, ref NativeArray<float> roadMaxHeights, ref NativeArray<float> roadMinHeights)
        {
            this.roadElevationMode = roadElevationMode;
            this.roadNoise = roadNoise;
            this.roadWeights = roadWeights;
            this.roadStartHeights = roadStartHeights;
            this.elevationNoise = elevationNoise;
            this.roadMaxHeights = roadMaxHeights;
            this.roadMinHeights = roadMinHeights;
        }

        public void Execute(int i)
        {
            float roadWeight = 0f;
            float roadStartHeight = 0f;

            //Calculate road noise sum
            float noiseSum = 0.00001f;
            for (int l = 0; l < roadMaxHeights.Length; l++)
            {
                noiseSum += roadNoise[i * roadMaxHeights.Length + l];
            }

            //Calculate road weights and road start heights using weighted average
            for (int l = 0; l < roadMaxHeights.Length; l++)
            {
                float noise = roadNoise[i * roadMaxHeights.Length + l];
                float noiseWeight = noise / noiseSum;

                if (noiseWeight > 0f)
                {
                    roadWeight += noise * noiseWeight;

                    //Apply elevation to road start height
                    if (roadElevationMode == TerrainManager.RoadElevationMode.Elevation)
                    {
                        float startHeight = elevationNoise[i].x;
                        roadStartHeight += Mathf.Clamp(startHeight, roadMinHeights[l], roadMaxHeights[l]) * noiseWeight;
                    }
                    else if (roadElevationMode == TerrainManager.RoadElevationMode.MaxHeight)
                    {
                        roadStartHeight += roadMaxHeights[l] * noiseWeight;
                    }
                    else if (roadElevationMode == TerrainManager.RoadElevationMode.MinHeight)
                    {
                        roadStartHeight += roadMinHeights[l] * noiseWeight;
                    }
                }
            }

            roadWeights[i] = roadWeight;
            roadStartHeights[i] = roadStartHeight;
        }
    }

    [BurstCompile]
    public struct CompileRoadStructureData : IJobParallelFor
    {
        private Vector2Int size;
        private Vector2Int chunkCoords;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestriction]
        private NativeArray<float> roadWeights;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestriction]
        private NativeArray<float> roadStartHeights;

        [ReadOnly]
        private NativeParallelHashMap<HashableIndex2, PersistentRoadChange> persistentRoadWeightChanges;

        public CompileRoadStructureData(Vector2Int size, Vector2Int chunkCoords,
            ref NativeArray<float> roadWeights, ref NativeArray<float> roadStartHeights,
            ref NativeParallelHashMap<HashableIndex2, PersistentRoadChange> persistentRoadWeightChanges)
        {
            this.size = size;
            this.chunkCoords = chunkCoords;
            this.roadWeights = roadWeights;
            this.roadStartHeights = roadStartHeights;
            this.persistentRoadWeightChanges = persistentRoadWeightChanges;
        }

        public void Execute(int i)
        {
            int xIndex = i % size.y;
            int zIndex = i / size.y;

            float roadWeight = roadWeights[i];
            float roadStartHeight = roadStartHeights[i];

            HashableIndex2 hash = new HashableIndex2(new Vector2Int(xIndex, zIndex), chunkCoords);
            if (persistentRoadWeightChanges.ContainsKey(hash))
            {
                PersistentRoadChange roadChange = persistentRoadWeightChanges[hash];
                roadStartHeight = Mathf.Max(roadStartHeight, roadChange.startHeight);
                roadWeight = Mathf.Max(roadWeight, roadChange.roadWeight);
            }

            roadWeights[i] = roadWeight;
            roadStartHeights[i] = roadStartHeight;
        }
    }

    [BurstCompile]
    public struct ConformMeshToStructures : IJob
    {
        private Vector3Int size;
        private Vector2Int chunkCoords;

        private NativeArray<float> weights;

        private NativeParallelMultiHashMap<Vector2Int, PersistentWeightChange> persistentWeightChanges;

        private float structureWeightChangeMultiplier;

        public ConformMeshToStructures(Vector3Int size, Vector2Int chunkCoords, ref NativeArray<float> weights,
            ref NativeParallelMultiHashMap<Vector2Int, PersistentWeightChange> persistentWeightChanges,
            float structureWeightChangeMultiplier)
        {
            this.size = size;
            this.chunkCoords = chunkCoords;
            this.weights = weights;
            this.persistentWeightChanges = persistentWeightChanges;
            this.structureWeightChangeMultiplier = structureWeightChangeMultiplier;
        }

        public void Execute()
        {
            foreach (PersistentWeightChange persistentWeightChange in
                persistentWeightChanges.GetValuesForKey(chunkCoords))
            {
                int index = persistentWeightChange.zIndex * size.x * size.y + persistentWeightChange.yIndex *
                    size.x + persistentWeightChange.xIndex;
                weights[index] += persistentWeightChange.weight * structureWeightChangeMultiplier;
            }
        }
    }

    #endregion

    #region Misc Structs/Classes
    [System.Serializable]
    public class RidgedNoiseValues
    {
        //Has no purpose other than organizational
        public string name;

        public RidgedNoisePass ridgedNoisePass;
        public RidgedNoiseApplicationValues ridgedNoiseApplicationValues;

        public AnimationCurve ridgedNoiseHeightDistCurve;
    }

    [System.Serializable]
    public class RoadNoiseValues
    {
        //Has no purpose other than organizational
        public string name;
        public float minHeight;
        public float maxHeight;

        public RidgedNoisePass ridgedNoisePass;
    }

    [System.Serializable]
    public struct RidgedNoisePass
    {
        public float noiseScale;
        [Range(1, 12)]
        public int octaves;
        public float persistence;
        public float lacunarity;
        public float bias;

#if UNITY_EDITOR
        //Editor UI variables 
        [SerializeField]
        [HideInInspector]
        private bool isExpanded;
        [SerializeField]
        [HideInInspector]
        private bool previewExpanded;
#endif
    }

    [System.Serializable]
    public struct RidgedNoiseApplicationValues
    {
        public int applyHeight;
        public float height;
        public float heightBlendPower;
        public float ridgedPower;
    }

    public struct VertexData
    {
        public Vector3 pos;
        public Vector3 normal;
        public float height;
        public float steepness;
        public float vegetation;
        public float rock;
        public int biome;
        public float road;
        public float roadStartHeight;
        public float roadHeight;
        public bool insideInfluenceBound;

        public VertexData(Vector3 pos, Vector3 normal, float height, float steepness, float vegetation, float rock,
            int biome, float road, float roadStartHeight, float roadHeight, bool insideInfluenceBound)
        {
            this.pos = pos;
            this.normal = normal;
            this.height = height;
            this.steepness = steepness;
            this.vegetation = vegetation;
            this.rock = rock;
            this.biome = biome;
            this.road = road;
            this.roadStartHeight = roadStartHeight;
            this.roadHeight = roadHeight;
            this.insideInfluenceBound = insideInfluenceBound;
        }
    }

    public struct MeshVertexData
    {
        public Vector3 vertex;
        public Vector3 normal;
        public Vector4 tangent;
        public Vector2 uv1;
        public Vector2 uv2;

        public MeshVertexData(Vector3 vertex, Vector3 normal, Vector4 tangent, Vector2 uv1, Vector2 uv2)
        {
            this.vertex = vertex;
            this.normal = normal;
            this.tangent = tangent;
            this.uv1 = uv1;
            this.uv2 = uv2;
        }
    }

    public struct TerrainMeshVertexData
    {
        public Vector3 vertex;
        public Vector3 normal;
        public Vector2 uv1;

        public TerrainMeshVertexData(Vector3 vertex, Vector3 normal, Vector2 uv1)
        {
            this.vertex = vertex;
            this.normal = normal;
            this.uv1 = uv1;
        }
    }

    public struct TerrainMeshTrig
    {
        public Vector3 vertex1;
        public Vector3 vertex2;
        public Vector3 vertex3;
    }

    public struct PersistentRoadChange
    {
        public half roadWeight;
        public float startHeight;

        public PersistentRoadChange(float roadWeight, float startHeight)
        {
            this.roadWeight = (half)roadWeight;
            this.startHeight = startHeight;
        }
    }

    public struct StructurePosition
    {
        public Vector3 position;
        public float minDist;

        public StructurePosition(Vector3 position, float minDist)
        {
            this.position = position;
            this.minDist = minDist;
        }
    }

    public struct StructurePrefabCount
    {
        public int index;
        public int count;

        public StructurePrefabCount(int index)
        {
            this.index = index;
            count = 1;
        }
    }

    public struct InfluenceBound
    {
        public Vector3 position;
        public Vector3Int bounds;

        public InfluenceBound(Vector3 position, Vector3Int bounds)
        {
            this.position = position;
            this.bounds = bounds;
        }
    }

    public struct InfluenceIndex
    {
        public Vector2Int coord;
        public Vector3Int index;

        public InfluenceIndex(Vector2Int coord, Vector3Int index)
        {
            this.coord = coord;
            this.index = index;
        }
    }

    public struct PersistentWeightChange
    {
        public byte xIndex;
        public byte yIndex;
        public byte zIndex;
        public half weight;

        public PersistentWeightChange(int xIndex, int yIndex, int zIndex, float weight)
        {
            this.xIndex = (byte)xIndex;
            this.yIndex = (byte)yIndex;
            this.zIndex = (byte)zIndex;
            this.weight = (half)weight;
        }
    }

    public struct HashableIndex2 : IEquatable<HashableIndex2>
    {
        public byte xIndex;
        public byte yIndex;
        public int xCoord;
        public int yCoord;

        public HashableIndex2(Vector2Int index, Vector2Int coords)
        {
            xIndex = (byte)index.x;
            yIndex = (byte)index.y;
            xCoord = coords.x;
            yCoord = coords.y;
        }

        public bool Equals(HashableIndex2 obj)
        {
            return obj.xIndex == xIndex && obj.yIndex == yIndex && obj.xCoord == xCoord && obj.yCoord == yCoord;
        }

        public override int GetHashCode()
        {
            return (xIndex * 73856093) ^ (yIndex * 19349663) ^ (xCoord * 83492791) ^ (yCoord * 318211);
        }
    }

    #endregion

    public static class TerrainManagerUtility
    {
        //Calculate terrain chunk index from world position
        public static Vector3Int WorldToTerrain(Vector3 position, Vector3Int size)
        {
            int x = Mathf.RoundToInt(Mathf.Abs(position.x) % (size.x - 1));
            int y = Mathf.RoundToInt(position.y);
            int z = Mathf.RoundToInt(Mathf.Abs(position.z) % (size.z - 1));

            if (position.x < 0)
            {
                x = size.x - 1 - x;
            }
            if (position.z < 0)
            {
                z = size.z - 1 - z;
            }
            return new Vector3Int(x, y, z);
        }

        //Calculates world position from terrain chunk index
        public static Vector3 TerrainToWorld(Vector3Int index, Vector2Int coords, Vector3Int size)
        {
            float x = index.x + coords.x * (size.x - 1);
            float y = index.y;
            float z = index.z + coords.y * (size.z - 1);

            return new Vector3(x, y, z);
        }

        //Calculates terrain chunk coordinates from world position
        public static Vector2Int GetCoords(Vector3 position, Vector3Int size)
        {
            int x = (int)(position.x / (size.x - 1));
            int z = (int)(position.z / (size.z - 1));

            if (position.x < 0)
            {
                x--;
            }
            if (position.z < 0)
            {
                z--;
            }

            return new Vector2Int(x, z);
        }

        //Calculates unique seed for any given set of 2d chunk coordinates
        public static int GetSeed(Vector2Int coords)
        {
            int x;
            if (coords.x < 0)
            {
                x = coords.x * -2 - 1;
            }
            else
            {
                x = coords.x * 2;
            }

            int y;
            if (coords.y < 0)
            {
                y = coords.y * -2 - 1;
            }
            else
            {
                y = coords.y * 2;
            }

            int seed = ((x + y) * (x + y + 1) / 2 + y) * 1000;
            seed = seed == 0 ? 1 : seed;

            return seed;
        }
    }
}