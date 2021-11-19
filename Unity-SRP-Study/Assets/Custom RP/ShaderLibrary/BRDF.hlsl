#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

struct BRDF
{
	float3 diffuse;
	float3 specular;
	float roughness;
};


//不同的表面，反射的方式不同，但通常金属会通过镜面反射反射所有光，
//并且漫反射为零。因此，我们将声明反射率等于金属表面属性。
//被反射的光不会扩散，因此我们应将扩散色的缩放比例减去GetBRDF中的反射率一倍

//实际上，一些光还会从电介质表面反射回来，从而使其具有亮点
//定义最小反射率
#define MIN_REFLECTIVITY 0.04

//传入金属度，获取1-反射率，也就是漫反射强度
float OneMinusReflectivity(float metallic)
{
	float range = 1.0 - MIN_REFLECTIVITY;
	return range - metallic*range;
}

BRDF GetBRDF(Surface surface,bool applyAlphaToDiffuse = false)
{
	BRDF brdf;

	//漫反射
	float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
	brdf.diffuse = surface.color * oneMinusReflectivity;

	if(applyAlphaToDiffuse)
	{
		//预乘alpha混合
		brdf.diffuse *= surface.alpha;
	}

	//以一种方式反射的光，不能全部以另一种方式反射。
	//这称为能量转换，意味着出射光的量不能超过入射光的量。
	//这表明镜面反射颜色应等于表面颜色减去漫反射颜色

	//金属会影响镜面反射的颜色而非金属不会影响镜面反射的颜色,
	//介电表面的镜面颜色应为白色,
	//这可以通过使用金属属性在最小反射率和表面颜色之间进行插值来实现

	//镜面反射
	brdf.specular = lerp(MIN_REFLECTIVITY,surface.color,surface.metallic);


	//感性粗糙度（调用api，传入感性平滑度）
	float perceptualRughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
	//感性粗糙度 转化为粗糙度
	brdf.roughness = PerceptualRoughnessToRoughness(perceptualRughness);

	return brdf;
}

float Square(float v)
{
	return v*v;
}

float SpecularStrength(Surface surface,BRDF brdf,Light light)
{
	float3 h = SafeNormalize(light.direction + surface.viewDirection);//半程向量（光线方向 点乘 视线方向）

	float nh2 = Square(saturate(dot(surface.normal,h)));
	float lh2 = Square(saturate(dot(light.direction,h)));
	float r2 = Square(brdf.roughness);
	float d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
	float normalization = brdf.roughness * 4.0+2.0;
	return r2/(d2*max(0.1,lh2)*normalization);
}

float DirectBRDF(Surface surface,BRDF brdf,Light light)
{
	return SpecularStrength(surface,brdf,light)*brdf.specular+brdf.diffuse;
}

#endif