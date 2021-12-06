#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	int _CascadeCount;
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4 _CascadeData[MAX_CASCADE_COUNT]; 
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT*MAX_CASCADE_COUNT];
	//float _ShadowDistance;
	float4 _ShadowDistanceFade;
CBUFFER_END

struct DirectionalShadowData
{
	float strength;//强度
	int tileOffset;//偏移
};

struct ShadowData
{
	int cascadeIndex;
	float strength;
};

float FadedShadowStrength(float distance,float scale,float fade)
{
	return saturate((1.0 - distance*scale)*fade);	
}

ShadowData GetShadowData(Surface surfaceWS)
{
	ShadowData data;
	//data.strength = surfaceWS.depth < _ShadowDistance?1.0:0.0;
	data.strength = FadedShadowStrength(surfaceWS.depth,_ShadowDistanceFade.x,_ShadowDistanceFade.y);
	int i;
	for(i = 0;i<_CascadeCount;i++)
	{
		float4 sphere = _CascadeCullingSpheres[i];
		float diatanceSqr = DistanceSquared(surfaceWS.position,sphere.xyz);
		if(diatanceSqr < sphere.w)
		{
			if(i == _CascadeCount - 1)
			{
				data.strength *= FadedShadowStrength(diatanceSqr,_CascadeData[i].x,_ShadowDistanceFade.z);	
			}
			break;
		}
	}

	if(i == _CascadeCount)
	{
		data.strength = 0.0;
	}

	data.cascadeIndex = i;
	return data;
}

float SampleDirectionalShadowAtlas(float3 positionSTS)
{
	//SAMPLE_TEXTURE2D_SHADOW宏对阴影图集进行采样，
	//并向其传递 图集，阴影采样器 以及 阴影纹理空间中的位置（这是一个对应的参数）
	return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas,SHADOW_SAMPLER,positionSTS);
}

// 得到阴影的衰减
float GetDirectionalShadowAttenuation(DirectionalShadowData directional,ShadowData global,Surface surfaceWS)
{
	if(directional.strength <= 0.0)
	{
		return 1.0;
	}

	// 
	float3 normalBias = surfaceWS.normal * _CascadeData[global.cascadeIndex].y;

	//阴影纹理空间中的位置
	float3 positionSTS = mul(_DirectionalShadowMatrices[directional.tileOffset],float4(surfaceWS.position+normalBias,1.0)).xyz;

	// 对阴影图集采样
	float shadow = SampleDirectionalShadowAtlas(positionSTS);

	//最终衰减通过强度和采样衰减之间的线性插值获得
	return lerp(1.0,shadow,directional.strength);
}


#endif