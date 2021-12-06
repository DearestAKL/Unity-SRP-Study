using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    //定义自定义渲染管道使用的状态和绘图命令
    ScriptableRenderContext context;

    Camera camera;

    const string bufferName = "Render Camera";
    //命令缓冲区
    CommandBuffer buffer = new CommandBuffer { name = bufferName };
    //剔除结果
    CullingResults cullingResults;

    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

    Lighting lighting = new Lighting();

    /// <summary>
    /// 渲染
    /// </summary>
    /// <param name="context">自定义渲染命令</param>
    /// <param name="camera">相机</param>
    /// <param name="useDynamicBatching">开启动态合批</param>
    /// <param name="useGPUInstancing">开启GPUInstancing</param>
    /// <param name="shadowSettings">阴影设置</param>
    public void Render(ScriptableRenderContext context,Camera camera,
        bool useDynamicBatching, bool useGPUInstancing, ShadowSettings shadowSettings)
    {
        this.context = context;
        this.camera = camera;

        //编辑器准备
        PrepareBuffer();
        PrepareForSceneWindow();

        if (!Cull(shadowSettings.maxDistance))
        {
            // 没有获取到剔除参数，则直接返回
            return;
        }

        //当我们在阴影图集之前设置常规摄影机时，最终会在渲染常规几何图形之前切换到阴影图集，
        //这不是我们想要的。我们应该在调用CameraRenderer.Render中的CameraRenderer.Setup之前渲染阴影，
        //这样常规渲染不会受到影响。
        //SetUp();

        // 添加采样开始命令
        buffer.BeginSample(SampleName);
        // 执行缓冲区，这里主要是清理缓冲区
        ExecuteBuffer();
        // 将灯光的属性应用于上下文
        lighting.SetUp(context, cullingResults, shadowSettings);
        // 添加采样结束命令
        buffer.EndSample(SampleName);

        // 将摄像机的属性应用于上下文
        SetUp();

        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);

        DrawUnsupportedShaders();

        DrawGizmos();

        lighting.Cleanup();

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

    /// <summary>
    /// 剔除
    /// </summary>
    /// <param name="maxShadowDistance">最大阴影距离</param>
    /// <returns></returns>
    bool Cull(float maxShadowDistance) 
    {
        // 获取剔除参数
        if(camera.TryGetCullingParameters(out ScriptableCullingParameters p)) 
        {
            //最大阴影距离和摄像机远剪切平面中的最小值
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }

}
