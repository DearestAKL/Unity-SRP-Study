using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderPipeline : RenderPipeline
{
    CameraRenderer renderer = new CameraRenderer();

    bool useDynamicBatching, useGPUInstancing;

    public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing,bool useSRPBacher)
    {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        // 启用SRP批处理程序
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBacher;
        GraphicsSettings.lightsUseLinearIntensity = true;
    }
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (Camera camera in cameras)
        {
            renderer.Render(context, camera, useDynamicBatching, useGPUInstancing);
        }
    }
}
