//阴影采样相关库
#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
// 如果使用得是PCF 3X3
#if defined(_DIRECTIONAL_PCF3)
// 需要4个滤波样本
#define DIRECTIONAL_FILTER_SAMPLES 4
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
#define DIRECTIONAL_FILTER_SAMPLES 9
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
#define DIRECTIONAL_FILTER_SAMPLES 16
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

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
	float4 _ShadowAtlasSize;
CBUFFER_END

struct DirectionalShadowData
{
	float strength;//强度
	int tileOffset;//偏移
	float normalBias;//法线偏差
};

struct ShadowData
{
	// 级联
	int cascadeIndex;
	// 强度
	float strength;
	// 混合级联
	float cascadeBlend;
};

float FadedShadowStrength(float distance,float scale,float fade)
{
	return saturate((1.0 - distance*scale)*fade);	
}

ShadowData GetShadowData(Surface surfaceWS)
{
	ShadowData data;
	data.cascadeBlend = 1.0;

	//data.strength = surfaceWS.depth < _ShadowDistance?1.0:0.0;
	data.strength = FadedShadowStrength(surfaceWS.depth,_ShadowDistanceFade.x,_ShadowDistanceFade.y);
	int i;
	for(i = 0;i<_CascadeCount;i++)
	{
		float4 sphere = _CascadeCullingSpheres[i];
		float diatanceSqr = DistanceSquared(surfaceWS.position,sphere.xyz);
		if(diatanceSqr < sphere.w)
		{
			// 计算级联阴影的过渡强度
			float fade = FadedShadowStrength(diatanceSqr,_CascadeData[i].x,_ShadowDistanceFade.z);

			// 如果对象处于最后一个级联范围中
			if(i == _CascadeCount - 1)
			{
				data.strength *= fade;	
			}
			else
			{
				data.strength = fade;	
			}
			break;
		}
	}

	if(i == _CascadeCount)
	{
		data.strength = 0.0;
	}
#if defined(_CASCADE_BLEND_DITHER)
	else if(data.cascadeBlend < surfaceWS.dither)
	{
		i +=1;
	}
#endif
#if !defined(_CASCADE_BLEND_SOFT)
	data.cascadeBlend = 1.0;
#endif
	data.cascadeIndex = i;
	return data;
}

float SampleDirectionalShadowAtlas(float3 positionSTS)
{
	//SAMPLE_TEXTURE2D_SHADOW宏对阴影图集进行采样，
	//并向其传递 图集，阴影采样器 以及 阴影纹理空间中的位置（这是一个对应的参数）
	return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas,SHADOW_SAMPLER,positionSTS);
}

float FilterDirectionalShadow(float3 positionSTS)
{
	#if defined(DIRECTIONAL_FILTER_SETUP)
		//样本权重
		float weights[DIRECTIONAL_FILTER_SAMPLES];
		//样本位置
		float2 positions[DIRECTIONAL_FILTER_SAMPLES];
		float4 size = _ShadowAtlasSize.yyxx;
		DIRECTIONAL_FILTER_SETUP(size,positionSTS.xy,weights,positions);
		float shadow = 0;
		for(int i = 0;i < DIRECTIONAL_FILTER_SAMPLES;i++)
		{
			//遍历所有样本得到权重和
			shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i].xy,positionSTS.z));
		}
		return shadow;
	#else
		return SampleDirectionalShadowAtlas(positionSTS);
	#endif
}

// 得到阴影的衰减
float GetDirectionalShadowAttenuation(DirectionalShadowData directional,ShadowData global,Surface surfaceWS)
{
	// 如果不接受阴影，阴影衰减为1
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif

	if(directional.strength <= 0.0)
	{
		return 1.0;
	}

	// 计算法线偏差
	// float3 normalBias = surfaceWS.normal * _CascadeData[global.cascadeIndex].y;
	float3 normalBias = surfaceWS.normal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);

	// 通过加上法线偏移后的表面顶点位置,得到阴影纹理空间中的新位置
	float3 positionSTS = mul(_DirectionalShadowMatrices[directional.tileOffset],float4(surfaceWS.position+normalBias,1.0)).xyz;
	// 对阴影图集采样
	float shadow = FilterDirectionalShadow(positionSTS);

	// 如果级联混合小于1，代表在级联层级过渡区域中，必须从下一级联中采样并在两个值之间插值
	if(global.cascadeBlend < 1.0)
	{
		normalBias = surfaceWS.normal * (directional.normalBias * _CascadeData[global.cascadeIndex+1].y);
		positionSTS = mul(_DirectionalShadowMatrices[directional.tileOffset],float4(surfaceWS.position+normalBias,1.0)).xyz;
		shadow = lerp(FilterDirectionalShadow(positionSTS),shadow,global.cascadeBlend);
	}

	// 最终衰减通过强度和采样衰减之间的线性插值获得
	return lerp(1.0,shadow,directional.strength);
}
#endif