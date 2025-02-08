// Cubic weight function based on Catmull-Rom spline
float CubicWeight(float x)
{
	x = abs(x);
	if (x < 1.0)
	{
		return 1.5 * x * x * x - 2.5 * x * x + 1.0;
	}
	else if (x < 2.0)
	{
		return -0.5 * x * x * x + 2.5 * x * x - 4.0 * x + 2.0;
	}
	return 0.0;
}

// Bicubic sampling function generated from chat gpt lel
float4 SampleBicubic(Texture3D tex, SamplerState test, float3 uv, float lod, float texSize)
{
    // Map uv to texture space and calculate base and fractional coordinates
	float3 texel = uv * texSize - 0.5;
	float3 base = floor(texel);
	float3 frac = texel - base;

	float4 result = float4(0.0, 0.0, 0.0, 0.0);

    // Loop through a 4x4 grid of texels
	for (int j = -1; j <= 2; j++)
	{
		for (int i = -1; i <= 2; i++)
		{
			for (int u = -1; u <= 2; u++)
			{
				float3 offset = float3(i, j, u);
				float3 sampleUV = (base + offset + 1.0) / texSize;
				float4 texelColor = tex.SampleLevel(test, sampleUV, lod);

				// Compute cubic weights for x and y
				float weightX = CubicWeight(frac.x - i);
				float weightY = CubicWeight(frac.y - j);
				float weightZ = CubicWeight(frac.z - u);

				// Accumulate weighted color
				result += texelColor * weightX * weightY * weightZ;
			}
		}
	}

	return result;
}

float4 SampleBicubic(Texture2D tex, SamplerState test, float2 uv, float lod, float texSize)
{
    // Map uv to texture space and calculate base and fractional coordinates
	float2 texel = uv * texSize - 0.5;
	float2 base = floor(texel);
	float2 frac = texel - base;

	float4 result = float4(0.0, 0.0, 0.0, 0.0);

    // Loop through a 4x4 grid of texels
	for (int j = -1; j <= 2; j++)
	{
		for (int i = -1; i <= 2; i++)
		{
			float2 offset = float2(i, j);
			float2 sampleUV = (base + offset + 1.0) / texSize;
			float4 texelColor = tex.SampleLevel(test, sampleUV, lod);

			// Compute cubic weights for x and y
			float weightX = CubicWeight(frac.x - i);
			float weightY = CubicWeight(frac.y - j);

			// Accumulate weighted color
			result += texelColor * weightX * weightY;
		}
	}

	return result;
}

float4 SampleBicubic(Texture1D tex, SamplerState test, float uv, float lod, float texSize)
{
    // Map uv to texture space and calculate base and fractional coordinates
	float texel = uv * texSize - 0.5;
	float base = floor(texel);
	float frac = texel - base;

	float4 result = float4(0.0, 0.0, 0.0, 0.0);

    // Loop through a 4x4 grid of texels
	for (int i = -1; i <= 2; i++)
	{
		float offset = float(i);
		float sampleUV = (base + offset + 1.0) / texSize;
		float4 texelColor = tex.SampleLevel(test, sampleUV, lod);

		// Compute cubic weights for x and y
		float weightX = CubicWeight(frac.x - i);

		// Accumulate weighted color
		result += texelColor * weightX;
	}

	return result;
}

// https://gist.github.com/supertask/702439b84a341e5f45c79358135c9df6
float Remap(float v, float minOld, float maxOld, float minNew, float maxNew)
{
	return minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld);
}

float2 Remap(float2 v, float2 minOld, float2 maxOld, float2 minNew, float2 maxNew)
{
	return minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld);
}

float3 Remap(float3 v, float3 minOld, float3 maxOld, float3 minNew, float3 maxNew)
{
	return minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld);
}

float4 Remap(float4 v, float4 minOld, float4 maxOld, float4 minNew, float4 maxNew)
{
	return minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld);
}

float4 SampleBounded(Texture3D tex, SamplerState test, float3 uv, float lod, float texSize)
{
	if (any(uv < 0.0) || any(uv >= 1.0))
	{
		const float aaa = -10000;
		return float4(aaa, aaa, aaa, aaa);
	}
	
	//return tex[uint3(uv * texSize + 1.0/texSize)];
	return tex.SampleLevel(test, uv + (0.5 / texSize), lod);
}

float4 SampleBounded(Texture2D tex, SamplerState test, float2 uv, float lod, float texSize)
{
	if (any(uv < 0.0) || any(uv >= 1.0))
	{
		const float aaa = -10000;
		return float4(aaa, aaa, aaa, aaa);
	}
	
	//return tex[uint3(uv * texSize + 1.0/texSize)];
	return tex.SampleLevel(test, uv + (0.5 / texSize), lod);
	//return tex.SampleLevel(test, uv, lod);
}