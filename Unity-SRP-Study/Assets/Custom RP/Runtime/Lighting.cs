using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting 
{
    const string bufferName = "Lighting";
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    //static int dirLightColorId = Shader.PropertyToID("_DirectionalLightColor");
    //static int dirLightDirectionalId = Shader.PropertyToID("_DirectionalLightDirection");

    const int maxDirLightCount = 4;

    static int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
    static int dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
    static int dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");
    static int dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

    static Vector4[] dirLightColors = new Vector4[maxDirLightCount];
    static Vector4[] dirLightDirections = new Vector4[maxDirLightCount];
    static Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];

    CullingResults cullingResults;

    Shadows shadows = new Shadows();

    public void SetUp(ScriptableRenderContext context,CullingResults cullingResults,ShadowSettings shadowSettings) 
    {
        this.cullingResults = cullingResults;

        buffer.BeginSample(bufferName);
        // 将阴影的属性应用于上下文
        shadows.Setup(context, cullingResults, shadowSettings);

        // 将所有灯光的属性应用于上下文
        SetupLights();

        // 渲染阴影
        shadows.Render();

        buffer.EndSample(bufferName);

        // 执行缓冲区，根据上述属性执行
        context.ExecuteCommandBuffer(buffer);
        // 清理
        buffer.Clear();
    }

    void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
    {
        //Light light = RenderSettings.sun;
        //buffer.SetGlobalVector(dirLightColorId, light.color.linear * light.intensity);
        //buffer.SetGlobalVector(dirLightDirectionalId, -light.transform.forward);

        dirLightColors[index] = visibleLight.finalColor;

        //向前矢量 取反
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);

        //保留阴影
        dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light, index);
    }

    void SetupLights()
    {
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        int dirLightCount = 0;
        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];
            if (visibleLight.lightType == LightType.Directional)
            {
                SetupDirectionalLight(dirLightCount++, ref visibleLight);
                if (dirLightCount >= maxDirLightCount)
                {
                    break;
                }
            }
        }

        // 设置全局变量
        buffer.SetGlobalInt(dirLightCountId, visibleLights.Length);
        buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
        buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
        buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
    }

    public void Cleanup()
    {
        shadows.Cleanup();
    }
}