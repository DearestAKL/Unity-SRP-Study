using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    ScriptableRenderContext context;

    Camera camera;

    const string bufferName = "Render Camera";
    //命令缓冲区
    CommandBuffer buffer = new CommandBuffer { name = bufferName };

    CullingResults cullingResults;

    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

    Lighting lighting = new Lighting();

    public void Render(ScriptableRenderContext context,Camera camera,
        bool useDynamicBatching, bool useGPUInstancing)
    {
        this.context = context;
        this.camera = camera;

        PrepareBuffer();
        PrepareForSceneWindow();

        if (!Cull())
        {
            return;
        }


        SetUp();

        lighting.SetUp(context, cullingResults);

        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);

        DrawUnsupportedShaders();

        DrawGizmos();

        //仅仅这样并没有使天空盒渲染出来。
        //这是因为我们向上下文发出的命令都是缓冲的。
        //必须通过在上下文上调用Submit来提交排队的工作才会执行。
        //再写一个单独的Submit方法，该方法在DrawVisibleGeometry学之后调用。
        Submit();
    }

    /// <summary>
    /// 将摄像机的属性应用于上下文
    /// </summary>

    private void SetUp()
    {
        // 没有这步的话,相机的方向将不会影响天窗盒的渲染方式
        // 为了正确渲染天空盒以及整个场景，我们必须设置视图投影矩阵。
        // 此转换矩阵将摄像机的位置和方向（视图矩阵）与摄像机的透视或正投影（投影矩阵）结合在一起。
        // 在着色器中称为unity_MatrixVP，这是绘制几何图形时使用的着色器属性之一
        context.SetupCameraProperties(camera);

        CameraClearFlags flags = camera.clearFlags;

        buffer.BeginSample(SampleName);

        //清除渲染目标
        buffer.ClearRenderTarget(flags<=CameraClearFlags.Depth, flags == CameraClearFlags.Color, 
            flags == CameraClearFlags.Color?camera.backgroundColor.linear:Color.clear);

        ExecuteBuffer();
    }

    /// <summary>
    /// 提交排队
    /// </summary>
    private void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();

        context.Submit();
    }

    /// <summary>
    /// 执行缓冲区
    /// </summary>
    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    /// <summary>
    /// 渲染可见的物体
    /// </summary>
    /// <param name="useDynamicBatching">使用动态怕批处理</param>
    /// <param name="useGPUInstancing">使用GUP实例化</param>
    private void DrawVisibleGeometry(bool useDynamicBatching,bool useGPUInstancing)
    {
        //绘制 不透明物体
        var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
        {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing
        };
        drawingSettings.SetShaderPassName(1, litShaderTagId);

        var filteringSetting = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSetting);

        //渲染天空盒
        context.DrawSkybox(camera);

        //绘制 透明物体
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSetting.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSetting);
    }
    bool Cull() 
    {
        if(camera.TryGetCullingParameters(out ScriptableCullingParameters p)) 
        {
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }

}
