#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

//表面
struct Surface
{
    float3 position;// 表面位置
    float3 normal;//法线
    float viewDirection;//观察方向
    float depth;//深度
    float3 color;//颜色
    float alpha;//透明度
    float metallic;//金属度
    float smoothness;//平滑度
};

#endif
