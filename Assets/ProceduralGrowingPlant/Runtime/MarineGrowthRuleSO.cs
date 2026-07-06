using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ProceduralGrowingPlant
{
    /// <summary>
    /// 海洋植物生成与生长规则。
    /// 用途：描述一株植物的茎数量、长度、弯曲、水流摆动和生长曲线，生成器只读取本配置。
    /// 使用方式：每个 MarineSpeciesSO 引用一个规则；修改参数后重新 Generate 即可得到新的确定结果。
    /// </summary>
    [CreateAssetMenu(fileName = "MarineGrowthRuleSO", menuName = "程序生长植物/生长规则 MarineGrowthRuleSO")]
    public sealed class MarineGrowthRuleSO : SerializedScriptableObject
    {
        #region 生成

        [Title("生成")]
        [LabelText("生成模式")]
        public MarineGenerationMode generationMode = MarineGenerationMode.Spline;

        [LabelText("整体尺寸倍率"), MinValue(0.001f)]
        [Tooltip("类似 Sprite 导入设置中的 Pixels Per Unit 对整体尺寸的影响。1 表示使用规则原始尺寸，2 表示整体放大 2 倍，0.5 表示整体缩小一半。")]
        public float globalSizeScale = 1f;

        [LabelText("茎数量"), MinValue(1)]
        public int stemCount = 5;

        [LabelText("每茎分段"), MinValue(1)]
        public int segmentCount = 8;

        [LabelText("根部水平散布")]
        public MarineFloatRange rootSpread = new MarineFloatRange(-0.4f, 0.4f);

        [LabelText("长度范围")]
        public MarineFloatRange length = new MarineFloatRange(0.8f, 1.8f);

        [LabelText("宽度范围")]
        public MarineFloatRange width = new MarineFloatRange(0.08f, 0.16f);

        [LabelText("倾斜角度")]
        public MarineFloatRange leanAngle = new MarineFloatRange(-18f, 18f);

        [LabelText("颜色随机幅度"), Range(0f, 1f)]
        public float colorVariance = 0.12f;

        #endregion

        #region 样条

        [Title("样条")]
        [ShowIf(nameof(generationMode), MarineGenerationMode.Spline)]
        [LabelText("样条类型")]
        public MarineSplineKind splineKind = MarineSplineKind.CatmullRom;

        [ShowIf(nameof(generationMode), MarineGenerationMode.Spline)]
        [LabelText("弯曲强度")]
        public float splineCurvature = 0.35f;

        [ShowIf(nameof(generationMode), MarineGenerationMode.Spline)]
        [LabelText("随机偏移")]
        public float splineNoise = 0.2f;

        [ShowIf(nameof(generationMode), MarineGenerationMode.Spline)]
        [LabelText("波浪强度")]
        public float splineWave = 0.12f;

        [ShowIf(nameof(generationMode), MarineGenerationMode.Spline)]
        [LabelText("重力下垂")]
        public float splineGravity = 0.05f;

        #endregion

        #region 分枝与叶片

        [Title("分枝与叶片")]
        [LabelText("分枝概率"), Range(0f, 1f)]
        public float branchChance = 0.35f;

        [LabelText("每茎最大分枝数"), MinValue(0)]
        public int maxBranchesPerStem = 2;

        [LabelText("分枝角度")]
        public MarineFloatRange branchAngle = new MarineFloatRange(28f, 52f);

        [LabelText("分枝长度倍率")]
        public MarineFloatRange branchLengthScale = new MarineFloatRange(0.28f, 0.55f);

        [LabelText("叶片概率"), Range(0f, 1f)]
        public float leafChance = 0.55f;

        [LabelText("叶片大小倍率")]
        public MarineFloatRange leafScale = new MarineFloatRange(0.35f, 0.75f);

        #endregion

        #region L-System

        [Title("L-System")]
        [ShowIf(nameof(generationMode), MarineGenerationMode.LSystem)]
        [LabelText("初始公理")]
        public string lSystemAxiom = "F";

        [ShowIf(nameof(generationMode), MarineGenerationMode.LSystem)]
        [LabelText("迭代次数"), MinValue(0)]
        public int lSystemIterations = 3;

        [ShowIf(nameof(generationMode), MarineGenerationMode.LSystem)]
        [LabelText("最大符号数"), MinValue(16)]
        public int lSystemMaxSymbols = 2048;

        [ShowIf(nameof(generationMode), MarineGenerationMode.LSystem)]
        [LabelText("替换规则")]
        public List<MarineLSystemRule> lSystemRules = new List<MarineLSystemRule>
        {
            new MarineLSystemRule("F", "F[+F]F[-F]F"),
        };

        [ShowIf(nameof(generationMode), MarineGenerationMode.LSystem)]
        [LabelText("步长")]
        public MarineFloatRange lSystemStepLength = new MarineFloatRange(0.22f, 0.36f);

        [ShowIf(nameof(generationMode), MarineGenerationMode.LSystem)]
        [LabelText("转向角度")]
        public MarineFloatRange lSystemTurnAngle = new MarineFloatRange(22f, 36f);

        [ShowIf(nameof(generationMode), MarineGenerationMode.LSystem)]
        [LabelText("角度扰动")]
        public float lSystemAngleJitter = 8f;

        [ShowIf(nameof(generationMode), MarineGenerationMode.LSystem)]
        [LabelText("分枝长度衰减"), Range(0.1f, 1f)]
        public float lSystemLengthFalloff = 0.82f;

        [ShowIf(nameof(generationMode), MarineGenerationMode.LSystem)]
        [LabelText("分枝宽度衰减"), Range(0.1f, 1f)]
        public float lSystemWidthFalloff = 0.72f;

        #endregion

        #region 生长

        [Title("生长")]
        [LabelText("生长速度"), MinValue(0.01f)]
        public float growSpeed = 0.25f;

        [LabelText("生长曲线")]
        public AnimationCurve growCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [LabelText("宽度曲线")]
        public AnimationCurve widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.35f);

        #endregion

        #region 海流

        [Title("海流")]
        [LabelText("摆动幅度")]
        public float swayAmplitude = 0.12f;

        [LabelText("摆动速度")]
        public float swaySpeed = 1.4f;

        [LabelText("高度相位")]
        public float heightPhase = 2.2f;

        #endregion

        #region 计算

        public float EvaluateGrowth(float growth01)
        {
            return growCurve == null ? Mathf.Clamp01(growth01) : Mathf.Clamp01(growCurve.Evaluate(Mathf.Clamp01(growth01)));
        }

        public float EvaluateWidth(float height01)
        {
            return widthCurve == null ? 1f : Mathf.Max(0.01f, widthCurve.Evaluate(Mathf.Clamp01(height01)));
        }

        #endregion
    }
}
