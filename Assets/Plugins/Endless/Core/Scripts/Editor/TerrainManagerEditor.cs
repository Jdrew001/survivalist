using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using Unity.Collections;
using System.Text.RegularExpressions;
using System.Linq;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

namespace EndlessTerrain
{
    [CustomEditor(typeof(TerrainManager))]
    public class TerrainManagerEditor : Editor
    {
        TerrainManager terrainManager;
        SerializedObject t;

        GUIStyle l_scriptHeaderStyle;
        GUIStyle labelHeaderStyle;
        GUIStyle BoxPanel;
        GUIStyle BoxPanelBiome;

        Texture2D BoxPanelColor;
        Texture2D BoxPanelColorBiome;

        Texture2D biomeGridTexture;

        public void OnEnable()
        {
            terrainManager = (TerrainManager)target;
            t = new SerializedObject(terrainManager);
            BoxPanelColor = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
            BoxPanelColor.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.3f));
            BoxPanelColor.Apply();
            BoxPanelColorBiome = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
            BoxPanelColorBiome.SetPixel(0, 0, new Color(0.2f, 0.2f, 0.2f, 0.2f));
            BoxPanelColorBiome.Apply();
        }

        public override void OnInspectorGUI()
        {
            l_scriptHeaderStyle = l_scriptHeaderStyle != null ? l_scriptHeaderStyle : new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, fontSize = 16 };
            labelHeaderStyle = labelHeaderStyle != null ? labelHeaderStyle : new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 16 };
            BoxPanel = BoxPanel != null ? BoxPanel : new GUIStyle(GUI.skin.box) { normal = { background = BoxPanelColor } };
            BoxPanelBiome = BoxPanelBiome != null ? BoxPanelBiome :
                new GUIStyle(GUI.skin.box) { normal = { background = BoxPanelColorBiome } };

            t.Update();

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Changes to inspector values will not be reflected in playmode.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            GUILayout.Label("<b><i><size=24>Terrain Manager</size></i></b>", l_scriptHeaderStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.MaxHeight(6));

            //
            //
            //Generation Settings Inspector
            //

            EditorGUILayout.Space(25);
            GUILayout.Label("Generation Settings", labelHeaderStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.MaxHeight(6));
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(t.FindProperty("generationMode"));
            EditorGUILayout.PropertyField(t.FindProperty("generateAtRuntime"));
            EditorGUILayout.PropertyField(t.FindProperty("originOffset"));
            EditorGUILayout.PropertyField(t.FindProperty("chunkRadius"));
            if (t.FindProperty("generationMode").enumValueIndex == 0 &&
                t.FindProperty("chunkRadius").intValue * t.FindProperty("size3D").vector3IntValue.x > 2500)
            {
                EditorGUILayout.HelpBox("Warning: Very large terrain size. " +
                    "Terrains this large will have an extreme performance impact.", MessageType.Warning);
            }
            EditorGUILayout.PropertyField(t.FindProperty("randomizeSeed"));
            if (t.FindProperty("randomizeSeed").boolValue == false)
            {
                EditorGUILayout.PropertyField(t.FindProperty("seed"));
            }
            if (t.FindProperty("generationMode").enumValueIndex == 0)
            {
                if (t.FindProperty("enableAdvancedInspector").boolValue)
                {
                    EditorGUILayout.PropertyField(t.FindProperty("maxViewAngle"));
                    EditorGUILayout.PropertyField(t.FindProperty("viewGenerationFactor"));
                }
                EditorGUILayout.PropertyField(t.FindProperty("player"));
            }

            //
            //
            //Biome Settings Inspector
            //

            EditorGUILayout.Space(25);
            GUILayout.Label("Biome Settings", labelHeaderStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.MaxHeight(6));
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(BoxPanel);
            EditorGUILayout.PropertyField(t.FindProperty("temperatureNoiseValues"));
            EditorGUILayout.PropertyField(t.FindProperty("moistureNoiseValues"));
            EditorGUILayout.EndVertical();
            EditorGUILayout.PropertyField(t.FindProperty("biomeGridSize"));
            EditorGUILayout.IntSlider(t.FindProperty("biomeBlend"), 0, t.FindProperty("biomeGridSize").intValue);

            //
            //
            //Biome Editor Inspector
            //

            EditorGUILayout.Space(25);
            GUILayout.Label("Biome Editor", labelHeaderStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.MaxHeight(6));
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(BoxPanel);
            for (int i = 0; i < t.FindProperty("terrainBiomes").arraySize; i++)
            {
                TerrainBiome biome =
                    (TerrainBiome)GetTargetObjectOfProperty(t.FindProperty("terrainBiomes").GetArrayElementAtIndex(i));
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(biome.Name))
                {
                    t.FindProperty("currentEditingBiome").intValue = i;
                }
                if (GUILayout.Button(new GUIContent("X", "Delete"), GUILayout.Width(25f)))
                {
                    t.FindProperty("terrainBiomes").DeleteArrayElementAtIndex(i);
                    if (t.FindProperty("currentEditingBiome").intValue == i)
                    {
                        t.FindProperty("currentEditingBiome").intValue = -1;
                    }
                    else if (t.FindProperty("currentEditingBiome").intValue > i)
                    {
                        t.FindProperty("currentEditingBiome").intValue -= 1;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Create New Biome"))
            {
                t.FindProperty("terrainBiomes").InsertArrayElementAtIndex(t.FindProperty("terrainBiomes").arraySize);
                t.FindProperty("currentEditingBiome").intValue = t.FindProperty("terrainBiomes").arraySize - 1;
                t.FindProperty("terrainBiomes").GetArrayElementAtIndex(t.FindProperty("terrainBiomes").arraySize - 1).
                    FindPropertyRelative("noiseValuesExpanded").boolValue = true;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.MaxHeight(6));
            EditorGUILayout.Space(5);

            if (t.FindProperty("currentEditingBiome").intValue > -1)
            {
                EditorGUILayout.BeginVertical(BoxPanel);
                EditorGUILayout.PropertyField(t.FindProperty("terrainBiomes").GetArrayElementAtIndex(
                    t.FindProperty("currentEditingBiome").intValue));
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.MaxHeight(6));

            //
            //
            //Noise Settings Inspector
            //

            EditorGUILayout.Space(25);
            GUILayout.Label("Noise Settings", labelHeaderStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.MaxHeight(6));
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(t.FindProperty("size3DExposed"), new GUIContent("3D Size"));
            EditorGUILayout.IntSlider(t.FindProperty("verticalSampleRate"), 1, t.FindProperty("size3DExposed").vector3IntValue.y / 2);
            EditorGUILayout.IntSlider(t.FindProperty("horizontalSampleRate"), 1, t.FindProperty("size3DExposed").vector3IntValue.x / 2);
            EditorGUILayout.BeginVertical(BoxPanel);
            EditorGUILayout.PropertyField(t.FindProperty("ridgedNoiseEnabled"));
            if (t.FindProperty("ridgedNoiseEnabled").boolValue == true)
            {
                EditorGUILayout.PropertyField(t.FindProperty("ridgedNoiseValues"));
            }
            EditorGUILayout.EndVertical();

            //
            //
            //Mesh Settings Inspector
            //

            EditorGUILayout.Space(25);
            GUILayout.Label("Mesh Settings", labelHeaderStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.MaxHeight(6));
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(t.FindProperty("chunkPrefab"));
            if (t.FindProperty("terrainMat").propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUILayout.HelpBox("Warning: Terrain chunk prefab missing.", MessageType.Warning);
            }
            EditorGUILayout.PropertyField(t.FindProperty("densityThreshold"));


            //
            //
            //Texture Settings Inspector
            //

            EditorGUILayout.Space(25);
            GUILayout.Label("Texture Settings", labelHeaderStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.MaxHeight(6));
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(t.FindProperty("terrainMat"));
            if (t.FindProperty("terrainMat").propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUILayout.HelpBox("Warning: Terrain material missing.", MessageType.Warning);
            }
            EditorGUILayout.PropertyField(t.FindProperty("texSize"));
            EditorGUILayout.PropertyField(t.FindProperty("textureFormat"));
            EditorGUILayout.PropertyField(t.FindProperty("steepnessThreshold"));
            EditorGUILayout.PropertyField(t.FindProperty("secondSteepnessThreshold"));
            EditorGUILayout.PropertyField(t.FindProperty("textureBlendDistance"), new GUIContent("Layer Blend Distance"));
            EditorGUILayout.PropertyField(t.FindProperty("textureSteepnessBlendDistance"));
            EditorGUILayout.PropertyField(t.FindProperty("textureSecondSteepnessBlendDistance"));
            EditorGUILayout.PropertyField(t.FindProperty("vegetationBlendDistance"), new GUIContent("Texture Blend Distance"));
            if (t.FindProperty("roadsEnabled").boolValue)
            {
                EditorGUILayout.PropertyField(t.FindProperty("roadTextureHeight"));
                EditorGUILayout.PropertyField(t.FindProperty("roadStartHeightTextureBias"));
                EditorGUILayout.PropertyField(t.FindProperty("roadTextureStrength"));
            }

            //
            //
            //Water Settings Enabled
            //

            EditorGUILayout.Space(25);
            GUILayout.Label("Water Settings", labelHeaderStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.MaxHeight(6));
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(BoxPanel);
            EditorGUILayout.PropertyField(t.FindProperty("waterEnabled"));
            if (t.FindProperty("waterEnabled").boolValue)
            {
                EditorGUILayout.PropertyField(t.FindProperty("waterPrefab"));
                EditorGUILayout.PropertyField(t.FindProperty("waterLevel"));
            }
            EditorGUILayout.EndVertical();

            //
            //
            //Road Settings Enabled
            //

            EditorGUILayout.Space(25);
            GUILayout.Label("Road Settings", labelHeaderStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.MaxHeight(6));
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(BoxPanel);
            EditorGUILayout.PropertyField(t.FindProperty("roadsEnabled"));
            if (t.FindProperty("roadsEnabled").boolValue)
            {
                EditorGUILayout.PropertyField(t.FindProperty("roadRidgedNoisePasses"));
                EditorGUILayout.PropertyField(t.FindProperty("roadHeight"));
                EditorGUILayout.PropertyField(t.FindProperty("roadWeightObjectSpawnThreshold"));
                EditorGUILayout.PropertyField(t.FindProperty("roadElevationMode"));
                EditorGUILayout.PropertyField(t.FindProperty("minRoadDeformHeight"));
                EditorGUILayout.PropertyField(t.FindProperty("maxRoadDeformHeight"));
                EditorGUILayout.PropertyField(t.FindProperty("roadFillStrength"));
                EditorGUILayout.PropertyField(t.FindProperty("roadCarveStrength"));
            }
            EditorGUILayout.EndVertical();

            //
            //
            //Structure Settings Enabled
            //

            EditorGUILayout.Space(25);
            GUILayout.Label("Structure Settings", labelHeaderStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.MaxHeight(6));
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(BoxPanel);
            EditorGUILayout.PropertyField(t.FindProperty("structuresEnabled"));
            if (t.FindProperty("structuresEnabled").boolValue)
            {
                EditorGUILayout.PropertyField(t.FindProperty("structureCheckRadius"));
                EditorGUILayout.PropertyField(t.FindProperty("structureCheckChunkLength"));
                if (t.FindProperty("enableAdvancedInspector").boolValue)
                {
                    EditorGUILayout.PropertyField(t.FindProperty("structurePathStartHeightOffset"));
                }
                EditorGUILayout.PropertyField(t.FindProperty("structureWeightChangeMultiplier"));
            }
            EditorGUILayout.EndVertical();


            //
            //
            //Debug Settings Inspector
            //

            EditorGUILayout.Space(25);
            GUILayout.Label("Debug Settings", labelHeaderStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.MaxHeight(6));
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(t.FindProperty("enableAdvancedInspector"));

            //
            //
            //Editor Buttons
            //

            EditorGUILayout.Space(25);
            GUILayout.Label("Editor Functions", labelHeaderStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.MaxHeight(6));
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginVertical(BoxPanel);
            if (GUILayout.Button("Draw Biome Grid Texture"))
            {
                GenerateBiomeGridTexture();
            }
            if (biomeGridTexture != null)
            {
                EditorGUILayout.Space(25);
                GUILayout.Box(biomeGridTexture, GUILayout.Width(500f), GUILayout.Height(500f),
                    GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                EditorGUILayout.Space(25);
            }
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("Run Stress Test"))
            {
                terrainManager.StressTest();
                EditorUtility.ClearProgressBar();
            }

            t.ApplyModifiedProperties();
        }

        private void GenerateBiomeGridTexture()
        {
            int biomeGridSize = t.FindProperty("biomeGridSize").intValue;
            float biomeBlendPercent = t.FindProperty("biomeBlend").intValue / (float)biomeGridSize;

            TerrainBiome[] terrainBiomes = new TerrainBiome[t.FindProperty("terrainBiomes").arraySize];
            for (int i = 0; i < t.FindProperty("terrainBiomes").arraySize; i++)
            {
                terrainBiomes[i] =
                    (TerrainBiome)GetTargetObjectOfProperty(t.FindProperty("terrainBiomes").GetArrayElementAtIndex(i));

                //Adjust biome bound edges to prevent blurring
                foreach (BiomeBound bound in terrainBiomes[i].Bounds)
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
            }

            biomeGridTexture = new Texture2D(biomeGridSize, biomeGridSize);

            Color[] biomeColors = new Color[terrainBiomes.Length];
            for (int i = 0; i < terrainBiomes.Length; i++)
            {
                biomeColors[i] = new Color(UnityEngine.Random.Range(0f, 1f),
                    UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
            }

            for (int x = 0; x < biomeGridSize; x++)
            {
                for (int y = 0; y < biomeGridSize; y++)
                {
                    float currentX = x / (float)biomeGridSize;
                    float currentY = y / (float)biomeGridSize;

                    float[] colorWeights = new float[terrainBiomes.Length];
                    for (int i = 0; i < terrainBiomes.Length; i++)
                    {
                        TerrainBiome biome = terrainBiomes[i];

                        float highestWeight = 0f;
                        foreach (BiomeBound bound in biome.Bounds)
                        {
                            float leftWeight = Mathf.InverseLerp(bound.InternalTemperatureMinMax.x - biomeBlendPercent,
                                bound.InternalTemperatureMinMax.x + biomeBlendPercent, currentX);
                            float rightWeight = Mathf.InverseLerp(bound.InternalTemperatureMinMax.y + biomeBlendPercent,
                                bound.InternalTemperatureMinMax.y - biomeBlendPercent, currentX);
                            float bottomWeight = Mathf.InverseLerp(bound.InternalMoistureMinMax.x - biomeBlendPercent,
                                bound.InternalMoistureMinMax.x + biomeBlendPercent, currentY);
                            float topWeight = Mathf.InverseLerp(bound.InternalMoistureMinMax.y + biomeBlendPercent,
                                bound.InternalMoistureMinMax.y - biomeBlendPercent, currentY);

                            float weight = Mathf.Min(leftWeight, Mathf.Min(rightWeight, Mathf.Min(bottomWeight, topWeight)));
                            highestWeight = Mathf.Max(weight, highestWeight);
                        }
                        colorWeights[i] = highestWeight;
                    }

                    float sum = 0f;
                    foreach (float weight in colorWeights)
                    {
                        sum += weight;
                    }
                    Vector3 weightedColor = new Vector3();
                    for (int i = 0; i < colorWeights.Length; i++)
                    {
                        float sumWeight = colorWeights[i] / sum;
                        weightedColor += new Vector3(biomeColors[i].r * sumWeight, biomeColors[i].g * sumWeight,
                            biomeColors[i].b * sumWeight);
                    }

                    biomeGridTexture.SetPixel(x, y, new Color(weightedColor.x, weightedColor.y, weightedColor.z));
                }
            }

            biomeGridTexture.Apply();
        }

        public object GetTargetObjectOfProperty(SerializedProperty prop)
        {
            if (prop == null) return null;

            var path = prop.propertyPath.Replace(".Array.data[", "[");
            object obj = prop.serializedObject.targetObject;
            var elements = path.Split('.');
            foreach (var element in elements)
            {
                if (element.Contains("["))
                {
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else
                {
                    obj = GetValue_Imp(obj, element);
                }
            }
            return obj;
        }

        private static object GetValue_Imp(object source, string name)
        {
            if (source == null)
                return null;
            var type = source.GetType();

            while (type != null)
            {
                var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f != null)
                    return f.GetValue(source);

                var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null)
                    return p.GetValue(source, null);

                type = type.BaseType;
            }
            return null;
        }

        private static object GetValue_Imp(object source, string name, int index)
        {
            var enumerable = GetValue_Imp(source, name) as System.Collections.IEnumerable;
            if (enumerable == null) return null;
            var enm = enumerable.GetEnumerator();
            //while (index-- >= 0)
            //    enm.MoveNext();
            //return enm.Current;

            for (int i = 0; i <= index; i++)
            {
                if (!enm.MoveNext()) return null;
            }
            return enm.Current;
        }
    }

    [CustomPropertyDrawer(typeof(NoiseValues))]
    public class NoiseValuesPropertyDrawer : PropertyDrawer
    {
        bool foldout = false;
        bool noiseFoldout = false;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            TerrainManager t = property.serializedObject.targetObject as TerrainManager;

            EditorGUI.BeginProperty(position, label, property);

            foldout = EditorGUI.BeginFoldoutHeaderGroup(new Rect(position.x, position.y, position.width - 25f, 25),
                foldout, label);
            if (foldout)
            {
                EditorGUIUtility.labelWidth = Mathf.Min(100f, position.width - 35f);
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 25, position.width - 25f, 25),
                    property.FindPropertyRelative("scale"), new GUIContent("Scale"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 50, position.width - 25f, 25),
                    property.FindPropertyRelative("persistence"), new GUIContent("Persistence"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 75, position.width - 25f, 25),
                    property.FindPropertyRelative("lacunarity"), new GUIContent("Lacunarity"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 100, position.width - 25f, 25),
                    property.FindPropertyRelative("octaves"), new GUIContent("Octaves"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 125, position.width - 25f, 25),
                    property.FindPropertyRelative("bias"), new GUIContent("Bias"));
                EditorGUIUtility.labelWidth = 0f;
                noiseFoldout = EditorGUI.Foldout(new Rect(position.x + 25, position.y + 150, position.width - 25f, 25),
                    noiseFoldout, "Noise Preview", true);

                if (noiseFoldout && !Application.isPlaying)
                {
                    System.Random prng = new System.Random(0);
                    Texture2D noiseTexture = new Texture2D(250, 250);
                    NoiseValues noiseValues = (NoiseValues)GetTargetObjectOfProperty(property);
                    NativeArray<float> weights = new NativeArray<float>(250 * 250, Allocator.TempJob);
                    NativeArray<Vector2> octaveOffsets = new NativeArray<Vector2>(noiseValues.octaves, Allocator.TempJob);
                    for (int i = 0; i < noiseValues.octaves; i++)
                    {
                        octaveOffsets[i] = new Vector2(prng.Next(-10000, 10000), prng.Next(-10000, 10000));
                    }

                    BiomeNoiseHeap noiseStack = new BiomeNoiseHeap(ref weights, noiseValues,
                        Vector2.zero, new Vector2Int(250, 250), ref octaveOffsets);
                    JobHandle noiseHandle = noiseStack.Schedule(250 * 250, 250);
                    noiseHandle.Complete();

                    for (int i = 0; i < 250 * 250; i++)
                    {
                        noiseTexture.SetPixel(i % 250, i / 250, new Color(weights[i], weights[i], weights[i]));
                    }
                    noiseTexture.Apply();
                    weights.Dispose();
                    octaveOffsets.Dispose();

                    EditorGUI.DrawPreviewTexture(new Rect(position.x + 25, position.y + 175, Mathf.Min(250, position.width - 25f),
                        Mathf.Min(250, position.width - 25f)), noiseTexture);
                }
            }
            noiseFoldout = foldout ? noiseFoldout : false;
            EditorGUI.EndFoldoutHeaderGroup();
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (noiseFoldout)
            {
                return 430f;
            }
            if (foldout)
            {
                return 180f;
            }
            else
            {
                return 25f;
            }
        }

        public object GetTargetObjectOfProperty(SerializedProperty prop)
        {
            if (prop == null) return null;

            var path = prop.propertyPath.Replace(".Array.data[", "[");
            object obj = prop.serializedObject.targetObject;
            var elements = path.Split('.');
            foreach (var element in elements)
            {
                if (element.Contains("["))
                {
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else
                {
                    obj = GetValue_Imp(obj, element);
                }
            }
            return obj;
        }

        private static object GetValue_Imp(object source, string name)
        {
            if (source == null)
                return null;
            var type = source.GetType();

            while (type != null)
            {
                var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f != null)
                    return f.GetValue(source);

                var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null)
                    return p.GetValue(source, null);

                type = type.BaseType;
            }
            return null;
        }

        private static object GetValue_Imp(object source, string name, int index)
        {
            var enumerable = GetValue_Imp(source, name) as System.Collections.IEnumerable;
            if (enumerable == null) return null;
            var enm = enumerable.GetEnumerator();
            //while (index-- >= 0)
            //    enm.MoveNext();
            //return enm.Current;

            for (int i = 0; i <= index; i++)
            {
                if (!enm.MoveNext()) return null;
            }
            return enm.Current;
        }
    }

    [CustomPropertyDrawer(typeof(BiomeValues))]
    public class BiomeValuesPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Texture2D BoxPanelColor = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
            BoxPanelColor.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.25f));
            BoxPanelColor.Apply();
            GUIStyle BoxPanel = new GUIStyle(GUI.skin.box) { normal = { background = BoxPanelColor } };

            EditorGUI.BeginProperty(position, label, property);

            EditorGUIUtility.labelWidth = Mathf.Min(150f, position.width - 50f);

            float height = position.y + 25f;
            GUI.Box(new Rect(position.x, height, position.width - 10f,
                15f + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("biomeTerrainValues"))), "", BoxPanel);
            EditorGUI.PropertyField(new Rect(position.x + 25, height + 10f, position.width - 35f, 25),
                property.FindPropertyRelative("biomeTerrainValues"));
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("biomeTerrainValues")) + 30f;

            GUI.Box(new Rect(position.x, height - 5f, position.width - 10f,
            10f + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("biomeVegetationValues"))), "", BoxPanel);
            EditorGUI.PropertyField(new Rect(position.x + 25, height, position.width - 35f, 25),
                property.FindPropertyRelative("biomeVegetationValues"), new GUIContent("Texture Noise values"));
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("biomeVegetationValues")) + 25f;
            GUI.Box(new Rect(position.x, height - 5f, position.width - 10f,
                (property.FindPropertyRelative("rockNoiseEnabled").boolValue ? 30f : 10f) +
                EditorGUI.GetPropertyHeight(property.FindPropertyRelative("biomeRockValues"))), "", BoxPanel);
            EditorGUI.PropertyField(new Rect(position.x + 25, height, position.width - 35f, 25),
                property.FindPropertyRelative("rockNoiseEnabled"), new GUIContent("Object Noise Enabled"));
            height += 25f;
            if (property.FindPropertyRelative("rockNoiseEnabled").boolValue)
            {
                EditorGUI.PropertyField(new Rect(position.x + 25, height, position.width - 35f, 25),
                    property.FindPropertyRelative("biomeRockValues"), new GUIContent("Object Noise Values"));
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("biomeRockValues"));
            }
            height += 25f;

            GUI.Box(new Rect(position.x, height - 5f, position.width - 10f,
                (property.FindPropertyRelative("elevationNoiseEnabled").boolValue ? 125f : 10f) +
                EditorGUI.GetPropertyHeight(property.FindPropertyRelative("biomeElevationValues"))), "", BoxPanel);
            EditorGUI.PropertyField(new Rect(position.x + 25, height, position.width - 35f, 25),
                property.FindPropertyRelative("elevationNoiseEnabled"));
            height += 25f;
            if (property.FindPropertyRelative("elevationNoiseEnabled").boolValue)
            {
                EditorGUI.PropertyField(new Rect(position.x + 25, height, position.width - 35f, 25),
                    property.FindPropertyRelative("biomeElevationValues"));
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("biomeElevationValues")) + 25f;
                EditorGUI.PropertyField(new Rect(position.x + 25, height, position.width - 35f, 15),
                    property.FindPropertyRelative("elevationCurve"));
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("elevationCurve")) + 25f;
                EditorGUI.PropertyField(new Rect(position.x + 25, height, position.width - 35f, 15),
                    property.FindPropertyRelative("floorWeightCurve"));
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("floorWeightCurve")) + 25f;

                GUI.Box(new Rect(position.x, height - 5f, position.width - 10f,
                    (property.FindPropertyRelative("voronoiNoiseEnabled").boolValue ? 90f : 10f) +
                    EditorGUI.GetPropertyHeight(property.FindPropertyRelative("biomeVoronoiValues"))), "", BoxPanel);
                EditorGUI.PropertyField(new Rect(position.x + 25, height, position.width - 35f, 25),
                    property.FindPropertyRelative("voronoiNoiseEnabled"));
                height += 25f;
                if (property.FindPropertyRelative("voronoiNoiseEnabled").boolValue)
                {
                    EditorGUI.PropertyField(new Rect(position.x + 25, height, position.width - 35f, 25),
                        property.FindPropertyRelative("biomeVoronoiValues"));
                    height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("biomeVoronoiValues")) + 25f;
                    EditorGUI.PropertyField(new Rect(position.x + 25, height, position.width - 35f, 25),
                        property.FindPropertyRelative("voronoiSteepnessCurve"));
                }
            }

            EditorGUIUtility.labelWidth = 0f;

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = 30f;
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("biomeTerrainValues"));
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("biomeVegetationValues")) + 25f;
            if (property.FindPropertyRelative("rockNoiseEnabled").boolValue)
            {
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("biomeRockValues")) + 15f;
            }
            height += 80f;
            if (property.FindPropertyRelative("elevationNoiseEnabled").boolValue)
            {
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("biomeElevationValues")) + 25f;
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("elevationCurve")) + 25f;
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("floorWeightCurve")) + 25f;

                if (property.FindPropertyRelative("voronoiNoiseEnabled").boolValue)
                {
                    height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("biomeVoronoiValues")) + 25f;
                    height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("voronoiSteepnessCurve")) + 25f;
                }
                height += 15f;
            }
            return height;
        }

        public SerializedProperty FindParentProperty(SerializedProperty serializedProperty)
        {
            var propertyPaths = serializedProperty.propertyPath.Split('.');
            if (propertyPaths.Length <= 1)
            {
                return default;
            }

            var parentSerializedProperty = serializedProperty.serializedObject.FindProperty(propertyPaths.First());
            for (var index = 1; index < propertyPaths.Length - 1; index++)
            {
                if (propertyPaths[index] == "Array")
                {
                    if (index + 1 == propertyPaths.Length - 1)
                    {
                        // reached the end
                        break;
                    }
                    if (propertyPaths.Length > index + 1 && Regex.IsMatch(propertyPaths[index + 1], "^data\\[\\d+\\]$"))
                    {
                        var match = Regex.Match(propertyPaths[index + 1], "^data\\[(\\d+)\\]$");
                        var arrayIndex = int.Parse(match.Groups[1].Value);
                        parentSerializedProperty = parentSerializedProperty.GetArrayElementAtIndex(arrayIndex);
                        index++;
                    }
                }
                else
                {
                    parentSerializedProperty = parentSerializedProperty.FindPropertyRelative(propertyPaths[index]);
                }
            }

            return parentSerializedProperty;
        }
    }

    [CustomPropertyDrawer(typeof(TerrainBiome))]
    public class TerrainBiomePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            EditorGUIUtility.labelWidth = 50f;

            EditorGUI.PropertyField(new Rect(position.x, position.y, Mathf.Min(525f, position.width - 25f), 25),
                property.FindPropertyRelative("name"), new GUIContent("Name"));

            EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 35f, Mathf.Min(500f, position.width - 25f), 25),
                property.FindPropertyRelative("bounds"), new GUIContent("Bounds"));

            float boundsHeight = EditorGUI.GetPropertyHeight(property.FindPropertyRelative("bounds"));
            float height = 55f + boundsHeight;

            if (GUI.Button(new Rect(position.x, position.y + height,
                (position.width - 25f) / 3f, 25), "Noise Values"))
            {
                property.FindPropertyRelative("noiseValuesExpanded").boolValue = true;
                property.FindPropertyRelative("texturesExpanded").boolValue = false;
                property.FindPropertyRelative("objectsExpanded").boolValue = false;
            }
            if (GUI.Button(new Rect(position.x + ((position.width - 25f) / 3f),
                position.y + height, (position.width - 25f) / 3f, 25), "Textures"))
            {
                property.FindPropertyRelative("noiseValuesExpanded").boolValue = false;
                property.FindPropertyRelative("texturesExpanded").boolValue = true;
                property.FindPropertyRelative("objectsExpanded").boolValue = false;
            }
            if (GUI.Button(new Rect(position.x + (position.width - 25f) / 3f * 2f,
                position.y + height, (position.width - 25f) / 3f, 25), "Objects"))
            {
                property.FindPropertyRelative("noiseValuesExpanded").boolValue = false;
                property.FindPropertyRelative("texturesExpanded").boolValue = false;
                property.FindPropertyRelative("objectsExpanded").boolValue = true;
            }

            float subHeight = 120f + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("bounds"));
            if (property.FindPropertyRelative("noiseValuesExpanded").boolValue)
            {
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + subHeight - 20f, position.width - 25f, 25),
                    property.FindPropertyRelative("biomeValues"), new GUIContent("Biome Values"));
            }
            else if (property.FindPropertyRelative("texturesExpanded").boolValue)
            {
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + subHeight, position.width - 25f, 25),
                    property.FindPropertyRelative("terrainLayers"), new GUIContent("Texture Layers"));
                subHeight += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("terrainLayers")) + 10f;
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + subHeight, position.width - 25f, 25),
                    property.FindPropertyRelative("steepnessTexture"), new GUIContent("Steepness Texture"));
                subHeight += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("steepnessTexture")) + 10f;
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + subHeight, position.width - 25f, 25),
                    property.FindPropertyRelative("secondSteepnessTexture"), new GUIContent("Second Steepness Texture"));
                subHeight += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("secondSteepnessTexture")) + 10f;
                if (property.serializedObject.FindProperty("roadsEnabled").boolValue)
                {
                    EditorGUI.PropertyField(new Rect(position.x + 25, position.y + subHeight, position.width - 25f, 25),
                        property.FindPropertyRelative("roadTexture"), new GUIContent("Road Texture"));
                }
            }
            else if (property.FindPropertyRelative("objectsExpanded").boolValue)
            {
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + subHeight, position.width - 25f, 25),
                    property.FindPropertyRelative("terrainObjects"), new GUIContent("Terrain Objects"));

                if (property.serializedObject.FindProperty("structuresEnabled").boolValue)
                {
                    EditorGUI.PropertyField(new Rect(position.x + 25, position.y + subHeight +
                        EditorGUI.GetPropertyHeight(property.FindPropertyRelative("terrainObjects")) + 25,
                        position.width - 25f, 25), property.FindPropertyRelative("terrainStructures"),
                        new GUIContent("Terrain Structures"));
                }
            }

            EditorGUIUtility.labelWidth = 0f;

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = 50f;

            height += 110f + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("bounds"));

            if (property.FindPropertyRelative("noiseValuesExpanded").boolValue)
            {
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("biomeValues"));
            }
            else if (property.FindPropertyRelative("texturesExpanded").boolValue)
            {
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("terrainLayers"));
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("steepnessTexture"));
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("secondSteepnessTexture"));
                if (property.serializedObject.FindProperty("roadsEnabled").boolValue)
                {
                    height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("roadTexture")) + 20f;
                }
            }
            else if (property.FindPropertyRelative("objectsExpanded").boolValue)
            {
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("terrainObjects"));
                if (property.serializedObject.FindProperty("structuresEnabled").boolValue)
                {
                    height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("terrainStructures")) + 20f;
                }
            }

            return height;
        }
    }

    [CustomPropertyDrawer(typeof(TextureSet))]
    public class TextureSetPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUIUtility.labelWidth = 100f;

            Texture2D BoxPanelColor = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
            BoxPanelColor.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.4f));
            BoxPanelColor.Apply();
            GUIStyle BoxPanel = new GUIStyle(GUI.skin.button) { normal = { background = BoxPanelColor } };

            GUI.Box(new Rect(position.x - 25f, position.y, position.width + 28f,
                EditorGUI.GetPropertyHeight(property) + 30f), "", BoxPanel);

            property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, 100, 25),
                property.isExpanded, property.displayName, true);
            if (property.isExpanded)
            {
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 25, Mathf.Min(300f, position.width - 25f), 25f),
                    property.FindPropertyRelative("baseColor"), new GUIContent("Base Color"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 45, Mathf.Min(300f, position.width - 25f), 25f),
                    property.FindPropertyRelative("maskMap"), new GUIContent("Mask Map"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 65, Mathf.Min(300f, position.width - 25f), 25f),
                    property.FindPropertyRelative("normalMap"), new GUIContent("Normal Map"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 105, Mathf.Min(300f, position.width - 25f), 25f),
                    property.FindPropertyRelative("textureScale"), new GUIContent("Texture Scale"));
            }

            EditorGUIUtility.labelWidth = 0f;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return property.isExpanded ? 130f : 30f;
        }
    }

    [CustomPropertyDrawer(typeof(VoronoiNoiseValues))]
    public class VoronoiNoiseValuesPropertyDrawer : PropertyDrawer
    {
        bool foldout = false;
        bool noiseFoldout = false;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            TerrainManager t = property.serializedObject.targetObject as TerrainManager;

            EditorGUI.BeginProperty(position, label, property);

            foldout = EditorGUI.BeginFoldoutHeaderGroup(new Rect(position.x, position.y, 100, 25),
                foldout, property.displayName);
            if (foldout)
            {
                EditorGUIUtility.labelWidth = 100f;
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 25, 300, 25),
                    property.FindPropertyRelative("voronoiScale"), new GUIContent("Voronoi Scale"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 50, 300, 25),
                    property.FindPropertyRelative("voronoiPersistence"), new GUIContent("Voronoi Persistence"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 75, 300, 25),
                    property.FindPropertyRelative("voronoiLacunarity"), new GUIContent("Voronoi Lacunarity"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 100, 300, 25),
                    property.FindPropertyRelative("voronoiOctaves"), new GUIContent("Voronoi Octaves"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 125, 300, 25),
                    property.FindPropertyRelative("voronoiApplicationScale"), new GUIContent("Voronoi Application Scale"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 150, 300, 25),
                    property.FindPropertyRelative("bias"), new GUIContent("Voronoi Bias"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 175, 300, 25),
                    property.FindPropertyRelative("voronoiPower"), new GUIContent("Voronoi Power"));
                EditorGUIUtility.labelWidth = 0f;
                noiseFoldout = EditorGUI.Foldout(new Rect(position.x + 25, position.y + 200, 100, 25),
                    noiseFoldout, "Noise Preview", true);

                if (noiseFoldout && !Application.isPlaying)
                {
                    System.Random prng = new System.Random(0);
                    Texture2D noiseTexture = new Texture2D(250, 250);
                    VoronoiNoiseValues noiseValues = (VoronoiNoiseValues)GetTargetObjectOfProperty(property);
                    NativeArray<float> voronoiWeights = new NativeArray<float>(250 * 250, Allocator.TempJob);
                    NativeArray<Vector2> octaveOffsets = new NativeArray<Vector2>(Mathf.Clamp(noiseValues.voronoiOctaves, 1, 100), Allocator.TempJob);
                    for (int i = 0; i < noiseValues.voronoiOctaves; i++)
                    {
                        octaveOffsets[i] = new Vector2(prng.Next(-10000, 10000), prng.Next(-10000, 10000));
                    }

                    VoronoiNoiseHeapSample noiseStack = new VoronoiNoiseHeapSample(new Vector2Int(250, 250),
                        noiseValues, ref octaveOffsets, ref voronoiWeights);
                    JobHandle noiseHandle = noiseStack.Schedule(250 * 250, 250);
                    noiseHandle.Complete();

                    for (int i = 0; i < 250 * 250; i++)
                    {
                        noiseTexture.SetPixel(i % 250, i / 250, new Color(voronoiWeights[i], voronoiWeights[i], voronoiWeights[i]));
                    }
                    noiseTexture.Apply();
                    voronoiWeights.Dispose();
                    octaveOffsets.Dispose();

                    EditorGUI.DrawPreviewTexture(new Rect(position.x + 25, position.y + 225, 250, 250), noiseTexture);
                }
            }
            noiseFoldout = foldout ? noiseFoldout : false;
            EditorGUI.EndFoldoutHeaderGroup();
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (noiseFoldout)
            {
                return 475f;
            }
            if (foldout)
            {
                return 225f;
            }
            else
            {
                return 25f;
            }
        }

        public object GetTargetObjectOfProperty(SerializedProperty prop)
        {
            if (prop == null) return null;

            var path = prop.propertyPath.Replace(".Array.data[", "[");
            object obj = prop.serializedObject.targetObject;
            var elements = path.Split('.');
            foreach (var element in elements)
            {
                if (element.Contains("["))
                {
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else
                {
                    obj = GetValue_Imp(obj, element);
                }
            }
            return obj;
        }

        private static object GetValue_Imp(object source, string name)
        {
            if (source == null)
                return null;
            var type = source.GetType();

            while (type != null)
            {
                var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f != null)
                    return f.GetValue(source);

                var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null)
                    return p.GetValue(source, null);

                type = type.BaseType;
            }
            return null;
        }

        private static object GetValue_Imp(object source, string name, int index)
        {
            var enumerable = GetValue_Imp(source, name) as System.Collections.IEnumerable;
            if (enumerable == null) return null;
            var enm = enumerable.GetEnumerator();
            //while (index-- >= 0)
            //    enm.MoveNext();
            //return enm.Current;

            for (int i = 0; i <= index; i++)
            {
                if (!enm.MoveNext()) return null;
            }
            return enm.Current;
        }
    }

    [CustomPropertyDrawer(typeof(RidgedNoisePass))]
    public class RidgedNoisePassPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            TerrainManager t = property.serializedObject.targetObject as TerrainManager;

            EditorGUI.BeginProperty(position, label, property);

            property.FindPropertyRelative("isExpanded").boolValue = EditorGUI.BeginFoldoutHeaderGroup(new Rect(position.x, position.y, 100, 25),
                property.FindPropertyRelative("isExpanded").boolValue, property.displayName);
            if (property.FindPropertyRelative("isExpanded").boolValue)
            {
                EditorGUIUtility.labelWidth = 100f;
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 25, 300, 25),
                    property.FindPropertyRelative("noiseScale"), new GUIContent("Ridged Noise Scale"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 50, 300, 25),
                    property.FindPropertyRelative("octaves"), new GUIContent("Ridged Noise Octaves"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 75, 300, 25),
                    property.FindPropertyRelative("persistence"), new GUIContent("Ridged Noise Persistence"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 100, 300, 25),
                    property.FindPropertyRelative("lacunarity"), new GUIContent("Ridged Noise Lacunarity"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 125, 300, 25),
                    property.FindPropertyRelative("bias"), new GUIContent("Ridged Noise Bias"));
                EditorGUIUtility.labelWidth = 0f;
                property.FindPropertyRelative("previewExpanded").boolValue = EditorGUI.Foldout(new Rect(position.x + 25, position.y + 150, 100, 25),
                    property.FindPropertyRelative("previewExpanded").boolValue, "Noise Preview", true);

                if (property.FindPropertyRelative("previewExpanded").boolValue && !Application.isPlaying)
                {
                    System.Random prng = new System.Random(0);
                    Texture2D noiseTexture = new Texture2D(250, 250);
                    RidgedNoisePass pass = (RidgedNoisePass)GetTargetObjectOfProperty(property);
                    NativeArray<RidgedNoisePass> passes = new NativeArray<RidgedNoisePass>(1, Allocator.Persistent);
                    passes[0] = pass;
                    NativeArray<float> ridgedWeights = new NativeArray<float>(250 * 250, Allocator.TempJob);
                    NativeArray<Vector2> offsets = new NativeArray<Vector2>(1, Allocator.Persistent);
                    offsets[0] = new Vector2(prng.Next(-10000, 10000), prng.Next(-10000, 10000));

                    RidgedNoiseHeap noiseStack = new RidgedNoiseHeap(ref ridgedWeights, Vector2.zero, new Vector2Int(250, 250),
                        ref passes, ref offsets);
                    JobHandle noiseHandle = noiseStack.Schedule(250 * 250, 250);
                    noiseHandle.Complete();

                    for (int i = 0; i < 250 * 250; i++)
                    {
                        noiseTexture.SetPixel(i % 250, i / 250, new Color(ridgedWeights[i], ridgedWeights[i], ridgedWeights[i]));
                    }
                    noiseTexture.Apply();
                    ridgedWeights.Dispose();
                    offsets.Dispose();
                    passes.Dispose();

                    EditorGUI.DrawPreviewTexture(new Rect(position.x + 25, position.y + 175, 250, 250), noiseTexture);
                }
            }
            property.FindPropertyRelative("previewExpanded").boolValue = property.FindPropertyRelative("isExpanded").boolValue
                ? property.FindPropertyRelative("previewExpanded").boolValue : false;
            EditorGUI.EndFoldoutHeaderGroup();
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.FindPropertyRelative("previewExpanded").boolValue)
            {
                return 425f;
            }
            if (property.FindPropertyRelative("isExpanded").boolValue)
            {
                return 175f;
            }
            else
            {
                return 25f;
            }
        }

        public object GetTargetObjectOfProperty(SerializedProperty prop)
        {
            if (prop == null) return null;

            var path = prop.propertyPath.Replace(".Array.data[", "[");
            object obj = prop.serializedObject.targetObject;
            var elements = path.Split('.');
            foreach (var element in elements)
            {
                if (element.Contains("["))
                {
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else
                {
                    obj = GetValue_Imp(obj, element);
                }
            }
            return obj;
        }

        private static object GetValue_Imp(object source, string name)
        {
            if (source == null)
                return null;
            var type = source.GetType();

            while (type != null)
            {
                var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f != null)
                    return f.GetValue(source);

                var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null)
                    return p.GetValue(source, null);

                type = type.BaseType;
            }
            return null;
        }

        private static object GetValue_Imp(object source, string name, int index)
        {
            var enumerable = GetValue_Imp(source, name) as System.Collections.IEnumerable;
            if (enumerable == null) return null;
            var enm = enumerable.GetEnumerator();
            //while (index-- >= 0)
            //    enm.MoveNext();
            //return enm.Current;

            for (int i = 0; i <= index; i++)
            {
                if (!enm.MoveNext()) return null;
            }
            return enm.Current;
        }
    }

    [CustomPropertyDrawer(typeof(TerrainObjectInstance))]
    public class TerrainObjectInstanceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            TerrainManager t = property.serializedObject.targetObject as TerrainManager;
            SerializedProperty biome = FindParentProperty(FindParentProperty(property));

            EditorGUI.BeginProperty(position, label, property);
            EditorGUIUtility.labelWidth = 200f;

            property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width - 25f, 15f),
                property.isExpanded, property.displayName, true);
            if (property.isExpanded)
            {
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 15f, position.width - 25f, 25),
                    property.FindPropertyRelative("name"), new GUIContent("Name"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 35f, position.width - 25f, 25),
                    property.FindPropertyRelative("prefab"), new GUIContent("Prefab"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 55f, position.width - 25f, 25),
                    property.FindPropertyRelative("spawnChance"), new GUIContent("Spawn Chance"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 75f, position.width - 25f, 25),
                    property.FindPropertyRelative("cullDistance"), new GUIContent("Cull Distance"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 95f, position.width - 25f, 25),
                    property.FindPropertyRelative("minSpawnHeight"), new GUIContent("Min Spawn Height"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 115f, position.width - 25f, 25),
                    property.FindPropertyRelative("maxSpawnHeight"), new GUIContent("Max Spawn Height"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 135f, position.width - 25f, 25),
                    property.FindPropertyRelative("minVegetationNoise"), new GUIContent("Min Texture Noise"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + 155f, position.width - 25f, 25),
                    property.FindPropertyRelative("maxVegetationNoise"), new GUIContent("Max Texture Noise"));

                float height = 155f;
                if (biome.FindPropertyRelative("biomeValues").FindPropertyRelative("rockNoiseEnabled").boolValue)
                {
                    EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height + 20f, position.width - 25f, 25),
                        property.FindPropertyRelative("minRockNoise"), new GUIContent("Min Object Noise"));
                    EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height + 40f, position.width - 25f, 25),
                        property.FindPropertyRelative("maxRockNoise"), new GUIContent("Max Object Noise"));
                    height += 40f;
                }
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height + 20f, position.width - 25f, 25),
                    property.FindPropertyRelative("minSteepness"), new GUIContent("Min Steepness"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height + 40f, position.width - 25f, 25),
                    property.FindPropertyRelative("maxSteepness"), new GUIContent("Max Steepness"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height + 60f, position.width - 25f, 25),
                    property.FindPropertyRelative("slopeWeight"), new GUIContent("Slope Weight"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height + 80f, position.width - 25f, 25),
                    property.FindPropertyRelative("heightOffset"), new GUIContent("Height Offset"));
                height += 80f;
                if (property.FindPropertyRelative("heightOffset").floatValue != 0f)
                {
                    EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height + 20f, position.width - 25f, 25),
                        property.FindPropertyRelative("heightOffsetRelativeToSlope"),
                        new GUIContent("Height Offset Relative To Slope"));
                    height += 20f;
                }
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height + 20f, position.width - 25f, 25),
                    property.FindPropertyRelative("randomScale"), new GUIContent("Random Scale"));
                if (property.FindPropertyRelative("randomScale").boolValue)
                {
                    EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height + 40f, position.width - 25f, 25),
                        property.FindPropertyRelative("uniformScale"), new GUIContent("Uniform Scale"));
                    if (property.FindPropertyRelative("uniformScale").boolValue)
                    {
                        EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height + 60f, position.width - 25f, 25),
                            property.FindPropertyRelative("scale"), new GUIContent("Scale Min Max"));
                    }
                    else
                    {
                        EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height + 60f, position.width - 25f, 25),
                            property.FindPropertyRelative("scaleX"), new GUIContent("Scale X Min Max"));
                        EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height + 80f, position.width - 25f, 25),
                            property.FindPropertyRelative("scaleY"), new GUIContent("Scale Y Min Max"));
                        EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height + 100f, position.width - 25f, 25),
                            property.FindPropertyRelative("scaleZ"), new GUIContent("Scale Z Min Max"));
                    }
                }
            }
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty biome = FindParentProperty(FindParentProperty(property));
            float height = 20f;
            if (property.isExpanded)
            {
                height += 200f;
                if (biome.FindPropertyRelative("biomeValues").FindPropertyRelative("rockNoiseEnabled").boolValue)
                {
                    height += 40f;
                }
                height += 60f;
                if (property.FindPropertyRelative("heightOffset").floatValue != 0f)
                {
                    height += 20f;
                }
                height += 20f;
                if (property.FindPropertyRelative("randomScale").boolValue)
                {
                    if (property.FindPropertyRelative("uniformScale").boolValue)
                    {
                        height += 20f;
                    }
                    else
                    {
                        height += 60f;
                    }
                }
            }
            return height;
        }

        public object GetTargetObjectOfProperty(SerializedProperty prop)
        {
            if (prop == null) return null;

            var path = prop.propertyPath.Replace(".Array.data[", "[");
            object obj = prop.serializedObject.targetObject;
            var elements = path.Split('.');
            foreach (var element in elements)
            {
                if (element.Contains("["))
                {
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else
                {
                    obj = GetValue_Imp(obj, element);
                }
            }
            return obj;
        }

        private static object GetValue_Imp(object source, string name)
        {
            if (source == null)
                return null;
            var type = source.GetType();

            while (type != null)
            {
                var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f != null)
                    return f.GetValue(source);

                var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null)
                    return p.GetValue(source, null);

                type = type.BaseType;
            }
            return null;
        }

        private static object GetValue_Imp(object source, string name, int index)
        {
            var enumerable = GetValue_Imp(source, name) as System.Collections.IEnumerable;
            if (enumerable == null) return null;
            var enm = enumerable.GetEnumerator();
            //while (index-- >= 0)
            //    enm.MoveNext();
            //return enm.Current;

            for (int i = 0; i <= index; i++)
            {
                if (!enm.MoveNext()) return null;
            }
            return enm.Current;
        }

        public SerializedProperty FindParentProperty(SerializedProperty serializedProperty)
        {
            var propertyPaths = serializedProperty.propertyPath.Split('.');
            if (propertyPaths.Length <= 1)
            {
                return default;
            }

            var parentSerializedProperty = serializedProperty.serializedObject.FindProperty(propertyPaths.First());
            for (var index = 1; index < propertyPaths.Length - 1; index++)
            {
                if (propertyPaths[index] == "Array")
                {
                    if (index + 1 == propertyPaths.Length - 1)
                    {
                        // reached the end
                        break;
                    }
                    if (propertyPaths.Length > index + 1 && Regex.IsMatch(propertyPaths[index + 1], "^data\\[\\d+\\]$"))
                    {
                        var match = Regex.Match(propertyPaths[index + 1], "^data\\[(\\d+)\\]$");
                        var arrayIndex = int.Parse(match.Groups[1].Value);
                        parentSerializedProperty = parentSerializedProperty.GetArrayElementAtIndex(arrayIndex);
                        index++;
                    }
                }
                else
                {
                    parentSerializedProperty = parentSerializedProperty.FindPropertyRelative(propertyPaths[index]);
                }
            }

            return parentSerializedProperty;
        }
    }

    [CustomPropertyDrawer(typeof(TerrainStructureInstance))]
    public class TerrainStructureInstanceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty biome = FindParentProperty(FindParentProperty(property));

            EditorGUI.BeginProperty(position, label, property);
            EditorGUIUtility.labelWidth = 200f;

            property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, 100, 25),
                property.isExpanded, property.displayName, true);
            if (property.isExpanded)
            {
                float height = 20F;
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                    property.FindPropertyRelative("name"), new GUIContent("Name"));
                height += 20f;
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                    property.FindPropertyRelative("spawnChance"), new GUIContent("Spawn Chance"));
                height += 20f;
                if (property.serializedObject.FindProperty("roadsEnabled").boolValue)
                {
                    EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                        property.FindPropertyRelative("minRoadWeight"), new GUIContent("Min Road Weight"));
                    height += 20f;
                }
                if (biome.FindPropertyRelative("biomeValues").FindPropertyRelative("elevationNoiseEnabled").boolValue)
                {
                    EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                        property.FindPropertyRelative("minElevation"), new GUIContent("Min Elevation"));
                    height += 20f;
                    EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                        property.FindPropertyRelative("maxElevation"), new GUIContent("Max Elevation"));
                    height += 20f;
                }
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                    property.FindPropertyRelative("minSpawnedPrefabs"), new GUIContent("Min Spawned Prefabs"));
                height += 20f;
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                    property.FindPropertyRelative("maxSpawnedPrefabs"), new GUIContent("Max Spawned Prefabs"));
                height += 20f;
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                    property.FindPropertyRelative("structureRadius"), new GUIContent("Structure Radius"));
                height += 20f;
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                    property.FindPropertyRelative("terrainStructurePrefabs"), new GUIContent("Structure Prefabs"));
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("terrainStructurePrefabs"));
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                    property.FindPropertyRelative("connectionMode"), new GUIContent("Connection Mode"));
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty biome = FindParentProperty(FindParentProperty(property));

            if (property.isExpanded)
            {
                float height = 40f;
                if (property.serializedObject.FindProperty("roadsEnabled").boolValue)
                {
                    height += 20f;
                }
                if (biome.FindPropertyRelative("biomeValues").FindPropertyRelative("elevationNoiseEnabled").boolValue)
                {
                    height += 40f;
                }
                height += 60f;
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("terrainStructurePrefabs"));
                height += 40f;
                return height;
            }
            else
            {
                return 20f;
            }
        }

        public SerializedProperty FindParentProperty(SerializedProperty serializedProperty)
        {
            var propertyPaths = serializedProperty.propertyPath.Split('.');
            if (propertyPaths.Length <= 1)
            {
                return default;
            }

            var parentSerializedProperty = serializedProperty.serializedObject.FindProperty(propertyPaths.First());
            for (var index = 1; index < propertyPaths.Length - 1; index++)
            {
                if (propertyPaths[index] == "Array")
                {
                    if (index + 1 == propertyPaths.Length - 1)
                    {
                        // reached the end
                        break;
                    }
                    if (propertyPaths.Length > index + 1 && Regex.IsMatch(propertyPaths[index + 1], "^data\\[\\d+\\]$"))
                    {
                        var match = Regex.Match(propertyPaths[index + 1], "^data\\[(\\d+)\\]$");
                        var arrayIndex = int.Parse(match.Groups[1].Value);
                        parentSerializedProperty = parentSerializedProperty.GetArrayElementAtIndex(arrayIndex);
                        index++;
                    }
                }
                else
                {
                    parentSerializedProperty = parentSerializedProperty.FindPropertyRelative(propertyPaths[index]);
                }
            }

            return parentSerializedProperty;
        }
    }

    [CustomPropertyDrawer(typeof(TerrainStructurePrefab))]
    public class TerrainStructurePrefabDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, 100, 25),
                property.isExpanded, property.displayName, true);
            if (property.isExpanded)
            {
                float height = 20f;
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                    property.FindPropertyRelative("prefab"), new GUIContent("Prefab"));
                height += 20f;
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                    property.FindPropertyRelative("spawnChance"), new GUIContent("Spawn Chance"));
                height += 20f;
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                    property.FindPropertyRelative("cullDistance"), new GUIContent("Cull Distance"));
                height += 20f;
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                    property.FindPropertyRelative("influenceBounds"), new GUIContent("Influence Bounds"));
                height += 20f;
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                    property.FindPropertyRelative("minNeighborDistance"), new GUIContent("Min Neighbor Distance"));
                height += 20f;
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                    property.FindPropertyRelative("maxSpawned"), new GUIContent("Max Spawned"));
                height += 20f;
                EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                    property.FindPropertyRelative("heightMode"), new GUIContent("Height Mode"));
                height += 20f;
                if (property.FindPropertyRelative("heightMode").enumValueIndex == 0)
                {
                    EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                        property.FindPropertyRelative("minimumSpawnHeight"), new GUIContent("Minimum Spawn Height"));
                    height += 20f;
                    EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                        property.FindPropertyRelative("maximumSpawnHeight"), new GUIContent("Maximum Spawn Height"));
                }
                else if (property.FindPropertyRelative("heightMode").enumValueIndex == 1)
                {
                    EditorGUI.PropertyField(new Rect(position.x + 25, position.y + height, position.width - 25f, 25),
                        property.FindPropertyRelative("spawnHeight"), new GUIContent("Spawn Height"));
                }
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.isExpanded)
            {
                float height = 160f;
                if (property.FindPropertyRelative("heightMode").enumValueIndex == 0)
                {
                    height += 40f;
                }
                else if (property.FindPropertyRelative("heightMode").enumValueIndex == 1)
                {
                    height += 20f;
                }
                return height;
            }
            else
            {
                return 20f;
            }
        }
    }


    [BurstCompile]
    public struct VoronoiNoiseHeapSample : IJobParallelFor
    {
        private Vector2Int size;

        private VoronoiNoiseValues voronoiValues;

        [NativeDisableParallelForRestriction]
        [ReadOnly]
        private NativeArray<Vector2> voronoiOctaveOffsets;

        [WriteOnly]
        private NativeArray<float> voronoiWeights;

        public VoronoiNoiseHeapSample(Vector2Int size, VoronoiNoiseValues voronoiValues,
            ref NativeArray<Vector2> voronoiOctaveOffsets, ref NativeArray<float> voronoiWeights)
        {
            this.size = size;
            this.voronoiValues = voronoiValues;
            this.voronoiOctaveOffsets = voronoiOctaveOffsets;

            this.voronoiWeights = voronoiWeights;
        }

        public void Execute(int i)
        {
            int xIndex = i % size.y;
            int zIndex = i / size.y;

            float voronoiAmplitude = 1.0f;
            float voronoiFrequency = 1.0f;

            float totalVoronoiNoise = 0f;
            for (int k = 0; k < voronoiValues.voronoiOctaves; k++)
            {
                float currentXVoronoi = xIndex / voronoiValues.voronoiScale *
                    voronoiFrequency + voronoiOctaveOffsets[k].x;
                float currentZVoronoi = zIndex / voronoiValues.voronoiScale *
                    voronoiFrequency + voronoiOctaveOffsets[k].y;
                float2 voronoiNoiseWeight = noise.cellular(new float2(currentXVoronoi, currentZVoronoi)) * voronoiAmplitude;
                totalVoronoiNoise += voronoiNoiseWeight.y - voronoiNoiseWeight.x;

                voronoiAmplitude *= voronoiValues.voronoiPersistence;
                voronoiFrequency *= voronoiValues.voronoiLacunarity;
            }

            float currentXApplicationVoronoi = xIndex / voronoiValues.voronoiApplicationScale;
            float currentZApplicationVoronoi = zIndex / voronoiValues.voronoiApplicationScale;
            float2 voronoiApplicationNoise = noise.cellular(new float2(currentXApplicationVoronoi, currentZApplicationVoronoi));
            float totalVoronoiApplicationNoise = voronoiApplicationNoise.y - voronoiApplicationNoise.x;

            totalVoronoiNoise += totalVoronoiNoise * totalVoronoiApplicationNoise;

            voronoiWeights[i] = totalVoronoiNoise;
        }
    }
}