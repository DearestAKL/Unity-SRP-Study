Shader "Custom RP/Unlit"
{
	Properties
	{
		_BaseMap("Texture",2D)="white"{}
		_BaseColor("Color",Color)=(1.0,1.0,1.0,1.0)
		_Cutoff("Alpha Cutoff",Range(0.0,1.0)) = 0.5
		[Toggle(_CLIPPING)] _Clipping("Alpha Clipping",Float) = 0
		
		[Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Scr Blend",Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst Blend",Float) = 0
		[Enum(Off,0,On,1)]_ZWrite("Z Write",Float) = 1
	}

	SubShader
	{

		Pass
		{
			Blend [_SrcBlend] [_DstBlend]//��� = _SrcBlend*shader�����������ɫ+_DstBlend*��Ļ������ɫ;
			//One 1
			//Zero 0
			//SrcColor shader������rgbֵ
			//SrcAlpha shader�����alphaֵ
			//DrcColor ֡��������Դrgbֵ����Ļ������ɫ��
			//DrcAlpha ֡��������Դalphaֵ����Ļ������ɫ��alphaֵ��
			//OneMinusSrcColor Ҳ����1-SrcColor
			//OneMinusSrcAlpha Ҳ����1-SrcAlpha
			//OneMinusDstColor Ҳ����1-DstColor
			//OneMinusDstAlpha Ҳ����1-DstAlpha

			ZWrite[_ZWrite]

			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _CLIPPING
			#pragma multi_compile_instancing
			#pragma vertex UnlitPassVertex
			#pragma fragment UnlitPassFragment
			#include "UnlitPass.hlsl"
			ENDHLSL
		}

	}
}