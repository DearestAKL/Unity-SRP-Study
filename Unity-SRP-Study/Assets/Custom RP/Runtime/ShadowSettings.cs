using UnityEngine;

[System.Serializable]
public class ShadowSettings
{
    [Min(0.001f)]
    public float maxDistance = 100f;

    [Range(0.001f, 1f)]
    public float distanceFade = 0.1f;

    /// <summary>
    /// 阴影贴图尺寸
    /// </summary>
    public enum MapSize
    { 
        _256 = 256,_512 = 512,_1024=1024,
        _2048 = 2048,_4096=4096,_8192 = 8192
    }

    /// <summary>
    /// PCF滤波模式
    /// </summary>
    public enum FilterMode
    {
        PCF2X2, PCF3X3, PCF5X5, PCF7X7
    }

    [System.Serializable]
    public struct Directional
    {
        /// <summary>
        /// 贴图尺寸
        /// </summary>
        public MapSize atlasSize;

        /// <summary>
        /// PCF滤波模式
        /// </summary>
        public FilterMode filter;

        /// <summary>
        /// 级联数量
        /// </summary>
        [Range(1, 4)]
        public int cascadeCount;

        /// <summary>
        /// 级联比率
        /// </summary>
        [Range(0f, 1f)]
        public float cascadeRatio1, cascadeRatio2, cascadeRatio3;

        /// <summary>
        /// 级联比率 向量
        /// </summary>
        public Vector3 CascadeRatios => new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);

        /// <summary>
        /// 级联透明度
        /// </summary>
        [Range(0.001f, 1f)]
        public float cascadeFade;

        /// <summary>
        /// 级联模式
        /// </summary>
        public enum CascadeBlendMode
        {
            Hard, Soft, Dither
        }

        /// <summary>
        /// 级联模式
        /// </summary>
        public CascadeBlendMode cascadeBlend;
    }

    public Directional directional = new Directional
    {
        atlasSize = MapSize._1024,
        filter = FilterMode.PCF2X2,
        cascadeCount = 4,
        cascadeRatio1 = 0.1f,
        cascadeRatio2 = 0.25f,
        cascadeRatio3 = 0.5f,
        cascadeFade = 0.1f,
        cascadeBlend = Directional.CascadeBlendMode.Hard
    };
}
