using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName ="Rendering/Custom Render Pipeline")]
public class CustomRenderPiineAsset : RenderPipelineAsset
{
    [SerializeField]
    bool useDynamicBatching, useGPUInstancing, useSRPBacher;

    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(useDynamicBatching, useGPUInstancing, useSRPBacher);
    }
}
