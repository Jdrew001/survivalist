float inverseLerp_float(float a, float b, float value)
{
    return (value - a)/(b-a);
}

float3 blend_rnm(float3 n1, float3 n2)
{
    n1.z += 1;
    n2.xy = -n2.xy;

    return n1 * dot(n1, n2) / n1.z - n2;
}

float4 StochSample_float(Texture2DArray tex, SamplerState ss, float2 uv, int index)
{
	//skew the uv to create triangular grid
	float2 skewUV = mul(float2x2 (1.0, 0.0, -0.57735027, 1.15470054), uv * 3.464);

	//vertices on the triangular grid
	int2 vertID = int2(floor(skewUV));

	//barycentric coordinates of uv position
	float3 temp = float3(frac(skewUV), 0);
	temp.z = 1.0 - temp.x - temp.y;
	
	//each vertex on the grid gets an according weight value
	int2 vertA, vertB, vertC;
	float weightA, weightB, weightC;

	//determine which triangle we're in
	if (temp.z > 0.0)
	{
		weightA = temp.z;
		weightB = temp.y;
		weightC = temp.x;
		vertA = vertID;
		vertB = vertID + int2(0, 1);
		vertC = vertID + int2(1, 0);
	}
	else
	{
		weightA = -temp.z;
		weightB = 1.0 - temp.y;
		weightC = 1.0 - temp.x;
		vertA = vertID + int2(1, 1);
		vertB = vertID + int2(1, 0);
		vertC = vertID + int2(0, 1);
	}	

	//get derivatives to avoid triangular artifacts
	float2 dx = ddx(uv);
	float2 dy = ddy(uv);

	//offset uvs using magic numbers
	float2 randomA = uv + frac(sin(fmod(float2(dot(vertA, float2(127.1, 311.7)), dot(vertA, float2(269.5, 183.3))), 3.14159)) * 43758.5453);
	float2 randomB = uv + frac(sin(fmod(float2(dot(vertB, float2(127.1, 311.7)), dot(vertB, float2(269.5, 183.3))), 3.14159)) * 43758.5453);
	float2 randomC = uv + frac(sin(fmod(float2(dot(vertC, float2(127.1, 311.7)), dot(vertC, float2(269.5, 183.3))), 3.14159)) * 43758.5453);
	
	//get texture samples
	float4 sampleA = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, ss, randomA, index, dx, dy);
	float4 sampleB = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, ss, randomB, index, dx, dy);
	float4 sampleC = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, ss, randomC, index, dx, dy);
	
	//blend samples with weights	
	return sampleA * weightA + sampleB * weightB + sampleC * weightC;
}

void layerTexture(int index, int scaleIndex, int scaleTextureIndex, float weight, int TextureCount, float3 absWorldPos, float3 worldNormal, float3 blendAxes, SamplerState samplerState, Texture2DArray Textures, Texture2DArray TextureScales, Texture2DArray MaskMaps, Texture2DArray Normals, inout float3 baseColor, inout float metallic, inout float occlusion, inout float detail, inout float smoothness, inout float3 normal)
{
	if(weight > 0.0001)
	{
		//Calculate scale for this texture using scale array
		float scale = SAMPLE_TEXTURE2D_ARRAY(TextureScales, samplerState, float2((scaleTextureIndex + 0.5) / TextureCount, 0.5 / TextureCount), scaleIndex).r * 100;
		float3 scaledWorldPos = absWorldPos / scale;

		//Calculate base color based on triplanar projection
		float4 xProjection = StochSample_float(Textures, samplerState, scaledWorldPos.yz, index) * blendAxes.x;
		float4 yProjection = StochSample_float(Textures, samplerState, scaledWorldPos.xz, index) * blendAxes.y;
		float4 zProjection = StochSample_float(Textures, samplerState, scaledWorldPos.xy, index) * blendAxes.z;
         
		baseColor += (xProjection + yProjection + zProjection) * weight;

		//Calculate mask values based on triplanar projection
		float4 maskX = StochSample_float(MaskMaps, samplerState, scaledWorldPos.yz, index) * blendAxes.x;
		float4 maskY = StochSample_float(MaskMaps, samplerState, scaledWorldPos.xz, index) * blendAxes.y;
		float4 maskZ = StochSample_float(MaskMaps, samplerState, scaledWorldPos.xy, index) * blendAxes.z;
		float4 mask = maskX + maskY + maskZ;
		 
		metallic += mask.r * weight;
		occlusion += mask.g * weight;  
		detail += mask.b * weight;
		smoothness += mask.a * weight; 

		//Calculate normals based on triplanar projection
		float3 tangentNormalX = UnpackNormal(StochSample_float(Normals, samplerState, scaledWorldPos.zy, index));
		float3 tangentNormalY = UnpackNormal(StochSample_float(Normals, samplerState, scaledWorldPos.xz, index));
		float3 tangentNormalZ = UnpackNormal(StochSample_float(Normals, samplerState, scaledWorldPos.xy, index));

		tangentNormalX = float3(tangentNormalX.xy + worldNormal.zy, abs(tangentNormalX.z) * worldNormal.x);
		tangentNormalY = float3(tangentNormalY.xy + worldNormal.xz, abs(tangentNormalY.z) * worldNormal.y);
		tangentNormalZ = float3(tangentNormalZ.xy + worldNormal.xy, abs(tangentNormalX.z) * worldNormal.z);

		float4 tangentNormal = float4(normalize(tangentNormalX.zyx * blendAxes.x + tangentNormalY.xzy * blendAxes.y + tangentNormalZ.xyz * blendAxes.z), 1);

		normal += tangentNormal.rgb * weight;
	}
}

void Triplanar_float(float3 absWorldPos, Texture2D TextureIndices, Texture2DArray Textures, Texture2DArray TextureScales, SamplerState samplerState, float LayerCount, float BiomeCount, float TextureCount, float BlendDistance, float SteepnessBlendDistance, float SecondSteepnessBlendDistance, float3 worldNormal, float steepnessThreshold, float secondSteepnessThreshold, Texture2DArray MaskMaps, Texture2DArray Normals, Texture2DArray StartHeights, float maxHeight, float vegetationNoise, Texture2DArray VegetationStartHeights, float biome, float vegetationBlend, float road, float roadStartHeight, float roadStartHeightBias, float roadHeight, float roadTextureBlend, Texture2DArray RoadTextures, Texture2DArray RoadMaskMaps, Texture2DArray RoadNormals, out float3 baseColor, out float metallic, out float occlusion, out float detail, out float smoothness, out float3 normal)
{
	//Calculate blend axes for triplanar shading
	float3 blendAxes = max(abs(worldNormal), 0);
	blendAxes /= (blendAxes.x + blendAxes.y + blendAxes.z).xxx;

	baseColor = float3(0, 0, 0);
	metallic = 0;
	occlusion = 0;
	detail = 0;
	smoothness = 0;
	normal = float3(0, 0, 0);

	//Calculate steepness values
    float steepness = 1 - (dot(normalize(worldNormal), float3(0, 1, 0)) * 0.5 + 0.5);
    int steepnessStep = max(0, sign(steepness - steepnessThreshold)); 
	int secondSteepnessStep = max(0, sign(steepness - secondSteepnessThreshold));

	int totalTextureCount = BiomeCount * LayerCount * TextureCount + BiomeCount * 2;
	int scaleTextureCount = max(BiomeCount, TextureCount); 

	float currentHeight = absWorldPos.y / maxHeight; 

	int baseLayerIndex = 999;	

	//Calculate the start height of the road
	float roadStartHeightCurrent = roadStartHeight + roadStartHeightBias;
	float roadPower = pow(saturate(inverseLerp_float(roadStartHeightCurrent - roadHeight, roadStartHeightCurrent, absWorldPos.y)), 1 / roadTextureBlend) * road;
	roadPower = absWorldPos.y > roadStartHeightCurrent ? 0 : roadPower;
	layerTexture(biome, BiomeCount * LayerCount + 2, biome, roadPower, scaleTextureCount, absWorldPos, worldNormal, blendAxes, samplerState, RoadTextures, TextureScales, RoadMaskMaps, RoadNormals, baseColor, metallic, occlusion, detail, smoothness, normal);

	//Calculate draw strength of terrain textures
	float terrainDrawStrength = 1 - roadPower;
	//Loop through each terrain layer
	UNITY_LOOP
	for(float i = 0; i < LayerCount; i++) 
	{
		//Calculate start and end heights for current layer
		float StartHeight = SAMPLE_TEXTURE2D_ARRAY(StartHeights, samplerState, float2((0.5 + i) / LayerCount, 0.5 / LayerCount), (int)biome).r;
		
		float EndHeight;
		//Make sure final layer end height is correctly calculated
		if(i == LayerCount - 1)
		{
			EndHeight = 1 + BlendDistance;
		}
		else
		{
			EndHeight = SAMPLE_TEXTURE2D_ARRAY(StartHeights, samplerState, float2((0.5 + i + 1) / LayerCount, 0.5 / LayerCount), (int)biome).r;
		}

		//Calculate start and end height draw strengths
		float startHeightDrawStrength = saturate(inverseLerp_float(StartHeight - BlendDistance, StartHeight + BlendDistance, currentHeight));
		float endHeightDrawStrength = saturate(inverseLerp_float(EndHeight + BlendDistance, EndHeight - BlendDistance, currentHeight));

		//Calculate layer draw strength based on start height, end height, and blend value
		float drawStrength = (startHeightDrawStrength - (1 - endHeightDrawStrength)) * terrainDrawStrength; 	

		//Calculate vegetation draw strengths based on vegetation values
		UNITY_LOOP
		for(int y = 0; y < TextureCount; y++)  
		{ 
			//Calculate vegetation start height draw strength
			float vegetationStartHeight;
			if(y == 0)
			{
				vegetationStartHeight = vegetationBlend * -2;
			}
			else
			{
				vegetationStartHeight = SAMPLE_TEXTURE2D_ARRAY(VegetationStartHeights, samplerState, float2((0.5 + y) / TextureCount, 0.5 / TextureCount), i + biome * LayerCount).r;
			}
			float vegetationStartHeightDrawStrength = saturate(inverseLerp_float(vegetationStartHeight - vegetationBlend, vegetationStartHeight + vegetationBlend, vegetationNoise));
			
			//Calculate vegetation end height draw strength
			float vegetationEndHeight;
			if(y == TextureCount - 1)
			{
				vegetationEndHeight = 1 + vegetationBlend;
			}
			else 
			{
				vegetationEndHeight = SAMPLE_TEXTURE2D_ARRAY(VegetationStartHeights, samplerState, float2((0.5 + y + 1) / TextureCount, 0.5 / TextureCount), i + biome * LayerCount).r;
			}
			float vegetationEndHeightDrawStrength = saturate(inverseLerp_float(vegetationEndHeight + vegetationBlend, vegetationEndHeight - vegetationBlend, vegetationNoise));

			//Calculate draw strength of this texture in the current layer
			float vegetationWeight = (vegetationStartHeightDrawStrength - (1 - vegetationEndHeightDrawStrength));
			float currentDrawStrength = drawStrength * vegetationWeight; 
			   
			//Layer current vegetation texture
			if(vegetationWeight > 0) 
			{
				int index = biome * TextureCount * LayerCount + biome * 2 + i * TextureCount + y;
				index = round(SAMPLE_TEXTURE2D(TextureIndices, samplerState, float2((0.5 + index) / totalTextureCount, 0.5 / totalTextureCount)).r * totalTextureCount);
				int scaleIndex = i + biome * LayerCount;

				layerTexture(index, scaleIndex, y, currentDrawStrength, scaleTextureCount, absWorldPos, worldNormal, blendAxes, samplerState, Textures, TextureScales, MaskMaps, Normals, baseColor, metallic, occlusion, detail, smoothness, normal);
			} 
		}
	}

	//Calculate draw strength of steepness texture
	float steepnessStartDrawStrength = saturate(inverseLerp_float(steepnessThreshold - SteepnessBlendDistance, steepnessThreshold + SteepnessBlendDistance, steepness));
	float steepnessEndDrawStrength = saturate(inverseLerp_float(secondSteepnessThreshold + SecondSteepnessBlendDistance, secondSteepnessThreshold - SecondSteepnessBlendDistance, steepness));
	float steepnessDrawStrength = saturate(steepnessStartDrawStrength - (1 - steepnessEndDrawStrength));
	
	//Modify terrain draw strength by the inverse of the steepness draw strength
	float flatPower = steepness < steepnessThreshold + SteepnessBlendDistance && steepness > steepnessThreshold - SteepnessBlendDistance ? 1 - steepnessDrawStrength : 1 - steepnessStep;
	baseColor *= flatPower; 
	metallic *= flatPower;
	occlusion *= flatPower;
	detail *= flatPower;
	smoothness *= flatPower;
	normal *= flatPower;

	//Calculate draw strength of second steepness texture
	float secondSteepnessDrawStrength = saturate(inverseLerp_float(secondSteepnessThreshold - SecondSteepnessBlendDistance, secondSteepnessThreshold + SecondSteepnessBlendDistance, steepness));
	int secondSteepnessIndex = (biome + 1) * TextureCount * LayerCount + (biome + 1) * 2 - 1;
	secondSteepnessIndex = round(SAMPLE_TEXTURE2D(TextureIndices, samplerState, float2((0.5 + secondSteepnessIndex) / totalTextureCount, 0.5 / totalTextureCount)).r * totalTextureCount);
	int secondSteepnessScaleIndex = BiomeCount * LayerCount + 1;	
	
	//Layer second steepness texture
	layerTexture(secondSteepnessIndex, secondSteepnessScaleIndex, biome, secondSteepnessDrawStrength, scaleTextureCount, absWorldPos, worldNormal, blendAxes, samplerState, Textures, TextureScales, MaskMaps, Normals, baseColor, metallic, occlusion, detail, smoothness, normal);
	
	int steepnessIndex = (biome + 1) * TextureCount * LayerCount + (biome + 1) * 2 - 2;
	steepnessIndex = round(SAMPLE_TEXTURE2D(TextureIndices, samplerState, float2((0.5 + steepnessIndex) / totalTextureCount, 0.5 / totalTextureCount)).r * totalTextureCount);
	int steepnessScaleIndex = BiomeCount * LayerCount; 
	 
	//Layer first steepness texture
	layerTexture(steepnessIndex, steepnessScaleIndex, biome, steepnessDrawStrength, scaleTextureCount, absWorldPos, worldNormal, blendAxes, samplerState, Textures, TextureScales, MaskMaps, Normals, baseColor, metallic, occlusion, detail, smoothness, normal);
}