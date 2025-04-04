using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace EndlessTerrain
{
    [CustomEditor(typeof(CombineTextures))]
    public class CombineTexturesButton : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            CombineTextures combine = (CombineTextures)target;
            if (GUILayout.Button("Combine Textures"))
            {
                combine.CombineTexture();
            }
        }
    }
}