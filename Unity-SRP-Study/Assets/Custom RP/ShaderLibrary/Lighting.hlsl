#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

//入射光
float3 IncomingLight(Surface surface,Light light)
{
	return saturate(dot(surface.normal,light.direction))*light.color;
}

//入射光 * BRDF(双向反射)
float3 GetLighting(Surface surface,BRDF brdf,Light light)
{
	return IncomingLight(surface,light)*DirectBRDF(surface,brdf,light);
}

//多光源叠加
float3 GetLighting(Surface surface,BRDF brdf)
{
	float3 color = 0.0;
	for(int i =0;i<GetDirectionalLightCount();i++)
	{
		color += GetLighting(surface,brdf,GetDirectionalLight(i));
	}
	return color;
	//return GetLighting(surface,GetDirectionalLight());
}


#endif