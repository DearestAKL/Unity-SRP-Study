using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    const string bufferName = "Shadows";

    CommandBuffer buffer = new CommandBuffer { name = bufferName };

    ScriptableRenderContext context;

    CullingResults cullingResults;

    ShadowSettings settings;

    const int maxShadowedDirectionalLightCount = 4;
    const int maxCascades = 4;

    int ShadowedDirectionalLightCount;

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
    }

    ShadowedDirectionalLight[] ShadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
    static int dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
    static int cascadeCountId = Shader.PropertyToID("_CascadeCount");
    static int cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
    //
    static int cascadeDataId= Shader.PropertyToID("_CascadeData");
    //static int shadowDistanceId = Shader.PropertyToID("_ShadowDistance");
    static int shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

    

    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades];

    /// <summary>
    /// 通用级联数据矢量数组
    /// </summary>

    static Vector4[] cascadeData= new Vector4[maxCascades];

    static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings)
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;

        ShadowedDirectionalLightCount = 0;
    }

    public void Render()
    {
        if(ShadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
    }

    /// <summary>
    /// 通过将阴影投射对象绘制到纹理来完成创建阴影贴图。
    /// </summary>
    void RenderDirectionalShadows()
    {
        //使用_DirectionalShadowAtlas来引用定向阴影图集。
        //从设置中检索整数形式的图集大小，然后以纹理标识符作为参数，
        //在命令缓冲区上调用GetTemporaryRT，再加上其宽度和高度的大小（以像素为单位）。
        int atlasSize = (int)settings.directional.atlasSize;
        //它声明具有正方形的渲染纹理，但默认情况下是普通的ARGB纹理。
        //我们需要一个阴影贴图，通过在调用中添加另外三个参数来指定阴影贴图。
        //首先是深度缓冲区的位数。我们希望它尽可能高，所以让我们使用32。
        //其次是filter 模式，为此我们使用默认的bilinear filtering。
        //第三是渲染纹理类型，必须RenderTextureFormat.ShadowMap。
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize,
            32,FilterMode.Bilinear,RenderTextureFormat.Shadowmap);


        //指示GPU渲染到该纹理而不是相机的Target。
        //这是通过在缓冲区上调用SetRenderTarget，标识渲染纹理以及如何加载和存储其数据来完成的。
        //我们不在乎它的初始状态，因为会立即清除它，因此我们将使用RenderBufferLoadAction.DontCare。
        //纹理的目的是包含阴影数据，因此我们需要使用RenderBufferStoreAction.Store作为第三个参数

        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);

        //完成后，我们可以像清除相机目标一样使用ClearRenderTarget，在这种情况下，只需关心深度缓冲区。
        //通过执行缓冲区来完成。如果你至少有一个阴影定向光处于活动状态，那么你会看到阴影图集的clear动作显示在帧调试器中
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        //使用的图块数量在RenderDirectionalShadows中成倍增加，这意味着我们最终可以得到总共16个图块，需要四分之一
        int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split,tileSize);
        }

        // 渲染级联后，将级联计数和球体发送到GPU
        buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
        buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);

        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);

        //buffer.SetGlobalFloat(shadowDistanceId, settings.maxDistance);
        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade,
            1f / (1f - f * f)));

        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);

        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m,Vector2 offset,int split)
    {
        if (SystemInfo.usesReversedZBuffer) 
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        // 为什么Z缓冲区要反转？
        // 最直观的是，0代表零深度，1代表最大深度。OpenGL就是这样做的。
        // 但是由于深度缓存器中精度的方式受到限制以及非线性存储的事实，我们通过反转来更好地利用这些位。
        // 其他图形API使用了反向方法。通常，我们不需要担心这个，除非我们明确使用Clip 空间。

        float scale = 1f / split;
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;

        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);

        return m;
    }

    Vector2 SetTileViewport(int index,int split,float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));

        return offset;
    }

    void RenderDirectionalShadows(int index, int split, int tileSize) 
    {
        ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);

        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;

        for (int i = 0; i < cascadeCount; i++)
        {
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex,//可见光指数
                i, cascadeCount, ratios, //控制阴影级联
                tileSize,//纹理尺寸
                0f,//靠近平面的阴影
                out Matrix4x4 viewMatrix,//视图矩阵
                out Matrix4x4 projectionMatrix,//投影矩阵
                out ShadowSplitData splitData
                );

            shadowSettings.splitData = splitData;

            if(index == 0) 
            {
                SetCasacdeData(i, splitData.cullingSphere, tileSize);
                //Vector4 cullingSphere = splitData.cullingSphere;
                //cullingSphere.w *= cullingSphere.w;
                //cascadeCullingSpheres[i] = cullingSphere;
            }

            int tileIndex = tileOffset + i;
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, SetTileViewport(tileIndex, split, tileSize), split);

            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);

            //第一个参数是全局深度偏差,第二个是坡度比例偏差
            //buffer.SetGlobalDepthBias(0, 3f);

            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);

            //buffer.SetGlobalDepthBias(0f, 0f);
        }
    }

    void SetCasacdeData(int index,Vector4 cullingSphere,float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere;
        cascadeData[index] = new Vector4(1f / cullingSphere.w, texelSize * 1.4142136f);
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    /// <summary>
    /// 在阴影图集中为灯光的阴影贴图保留空间，并存储渲染它们所需的信息。
    /// </summary>
    /// <param name="light"></param>
    /// <param name="visibleLightIndex"></param>
    /// <returns></returns>
    public Vector2 ReserveDirectionalShadows(Light light,int visibleLightIndex)
    {
        if(ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
            light.shadows != LightShadows.None && light.shadowStrength > 0f &&
            cullingResults.GetShadowCasterBounds(visibleLightIndex,out Bounds b))
        {
            ShadowedDirectionalLights[ShadowedDirectionalLightCount] = new ShadowedDirectionalLight
            {
                visibleLightIndex = visibleLightIndex
            };

            return new Vector2(light.shadowStrength, settings.directional.cascadeCount*ShadowedDirectionalLightCount++);
        }

        return Vector2.zero;
    }

    public void Cleanup()
    {
        if(ShadowedDirectionalLightCount > 0) 
        {
            buffer.ReleaseTemporaryRT(dirShadowAtlasId);
            ExecuteBuffer();
        }
    }
}
