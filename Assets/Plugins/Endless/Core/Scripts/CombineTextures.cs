using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace EndlessTerrain
{
    public class CombineTextures : MonoBehaviour
    {
        public string path;
        public int texSize;
        public Texture2D rTexture;
        public bool invertRTexture;
        public float rValue;
        public Texture2D gTexture;
        public bool invertGTexture;
        public float gValue;
        public Texture2D bTexture;
        public bool invertBTexture;
        public float bValue;
        public Texture2D aTexture;
        public bool invertATexture;
        public float aValue;

        public void CombineTexture()
        {
            Texture2D tex2d = new Texture2D(texSize, texSize, TextureFormat.RGBAFloat, true, true);

            for (int x = 0; x < texSize; x++)
            {
                for (int y = 0; y < texSize; y++)
                {
                    float r = rValue;
                    if (rTexture != null)
                    {
                        if (invertRTexture)
                        {
                            r = 1f - rTexture.GetPixel(x, y).r;
                        }
                        else
                        {
                            r = rTexture.GetPixel(x, y).r;
                        }
                    }

                    float g = gValue;
                    if (gTexture != null)
                    {
                        if (invertGTexture)
                        {
                            g = 1f - gTexture.GetPixel(x, y).r;
                        }
                        else
                        {
                            g = gTexture.GetPixel(x, y).r;
                        }
                    }


                    float b = bValue;
                    if (bTexture != null)
                    {
                        if (invertBTexture)
                        {
                            b = 1f - bTexture.GetPixel(x, y).r;
                        }
                        else
                        {
                            b = bTexture.GetPixel(x, y).r;
                        }
                    }


                    float a = aValue;
                    if (aTexture != null)
                    {
                        if (invertATexture)
                        {
                            a = 1f - aTexture.GetPixel(x, y).r;
                        }
                        else
                        {
                            a = aTexture.GetPixel(x, y).r;
                        }
                    }

                    tex2d.SetPixel(x, y, new Color(r, g, b, a));
                }
            }

            tex2d.Apply();

            File.WriteAllBytes(path + "/MaskMap.png", tex2d.EncodeToPNG());
        }


    }
}