using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ProceduralGrowingPlant
{
    /// <summary>
    /// 海洋程序植物的大类。
    /// 用途：只作为编辑器筛选和生成规则分组使用，不直接绑定任何项目玩法系统。
    /// 使用方式：在 MarineSpeciesSO 中选择当前物种类型，后续编辑器可以按类型显示不同预设。
    /// </summary>
    public enum MarinePlantKind
    {
        [LabelText("海草")]
        Seaweed,

        [LabelText("珊瑚")]
        Coral,

        [LabelText("海绵")]
        Sponge,

        [LabelText("生物植物")]
        BioPlant,
    }

    /// <summary>
    /// 单张素材在程序拼接中的用途标签。
    /// 用途：生成时按用途从素材包中挑选 Sprite，避免海草叶片误用到尖端或分叉素材。
    /// 使用方式：在 MarineSpritePackSO 的素材列表中为每个条目指定用途和权重。
    /// </summary>
    public enum MarinePieceKind
    {
        [LabelText("茎/主体")]
        Stem,

        [LabelText("叶片")]
        Leaf,

        [LabelText("尖端")]
        Tip,

        [LabelText("分叉")]
        Fork,

        [LabelText("装饰")]
        Decor,
    }

    /// <summary>
    /// 程序植物的骨架生成方式。
    /// 用途：让同一个 MarinePlantPreview 可以按规则切换为样条海草或 L-System 珊瑚，而不需要新增平行组件。
    /// 使用方式：在 MarineGrowthRuleSO 中选择模式；Spline 适合长条海草，LSystem 适合珊瑚和树状分枝。
    /// </summary>
    public enum MarineGenerationMode
    {
        [LabelText("样条 Spline")]
        Spline,

        [LabelText("L-System 分枝")]
        LSystem,
    }

    /// <summary>
    /// 样条采样算法。
    /// 用途：控制长海草中心线的曲线类型，方便美术在不同柔顺程度之间切换。
    /// 使用方式：Spline 模式下修改该枚举，然后重新 Generate 查看形态变化。
    /// </summary>
    public enum MarineSplineKind
    {
        [LabelText("Bezier")]
        Bezier,

        [LabelText("CatmullRom")]
        CatmullRom,

        [LabelText("Hermite")]
        Hermite,
    }

    /// <summary>
    /// 单张素材参与分段生成时的尺寸换算模式。
    /// 用途：决定生成器如何把路径段长度、规则宽度和 Sprite 原始世界尺寸组合成最终 Transform 缩放。
    /// 使用方式：茎、分叉和尖端通常使用 Socket 匹配；叶片和装饰通常使用原始尺寸；需要规则强控粗细时使用规则驱动。
    /// </summary>
    public enum MarinePieceSizeMode
    {
        [LabelText("Socket 匹配")]
        SocketMatch,

        [LabelText("原始尺寸")]
        NativeSize,

        [LabelText("规则驱动")]
        RuleDriven,
    }

    /// <summary>
    /// 可复现的浮点随机范围。
    /// 用途：把长度、宽度、角度等随机参数统一收束到数据配置里，避免在生成逻辑中硬编码。
    /// 使用方式：在规则 SO 中配置 Min 和 Max，生成时传入 System.Random 得到确定结果。
    /// </summary>
    [Serializable]
    public struct MarineFloatRange
    {
        [HorizontalGroup("Range"), LabelText("最小值")]
        public float min;

        [HorizontalGroup("Range"), LabelText("最大值")]
        public float max;

        public MarineFloatRange(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        public float Evaluate(System.Random rng)
        {
            float safeMin = Mathf.Min(min, max);
            float safeMax = Mathf.Max(min, max);
            return Mathf.Lerp(safeMin, safeMax, (float)rng.NextDouble());
        }
    }

    /// <summary>
    /// 单个 Sprite 素材的拼接配置。
    /// 用途：记录权重、缩放、Socket 偏移和标签，生成器只读取这些数据来组合植物外观。
    /// 使用方式：美术把 Sprite 拖入素材包，设置用途与权重；Socket 用于后续分叉、尖端和叶片拼接扩展。
    /// </summary>
    [Serializable]
    public sealed class MarinePiece
    {
        [PreviewField(64, ObjectFieldAlignment.Left)]
        [HorizontalGroup("Piece", 72), LabelText("图片素材")]
        public Sprite sprite;

        [VerticalGroup("Piece/Info"), LabelText("用途")]
        public MarinePieceKind kind = MarinePieceKind.Stem;

        [VerticalGroup("Piece/Info"), LabelText("权重"), MinValue(0f)]
        public float weight = 1f;

        [VerticalGroup("Piece/Info"), LabelText("默认缩放")]
        public Vector2 defaultScale = Vector2.one;

        [LabelText("尺寸模式")]
        public MarinePieceSizeMode sizeMode = MarinePieceSizeMode.SocketMatch;

        [LabelText("允许翻转")]
        public bool allowFlip = true;

        [LabelText("允许拉伸")]
        public bool allowStretch = true;

        [LabelText("使用物种颜色")]
        public bool useSpeciesColor = true;

        [LabelText("素材颜色叠加")]
        public Color colorTint = Color.white;

        [LabelText("底部 Socket")]
        [InfoBox("Socket 使用归一化素材坐标：x 是相对中心的水平偏移，y 是从底部到顶部的高度比例。默认 (0,0) 表示底部中心。")]
        public Vector2 bottomSocket;

        [LabelText("顶部 Socket")]
        [InfoBox("默认 (0,1) 表示顶部中心。Socket 匹配模式会用底部到顶部 Socket 的距离匹配路径段长度。")]
        public Vector2 topSocket = Vector2.up;

        [LabelText("标签")]
        public List<string> tags = new List<string>();
    }

    /// <summary>
    /// L-System 的单条替换规则。
    /// 用途：把一个符号替换成下一代字符串，用少量数据生成复杂珊瑚分枝。
    /// 使用方式：例如 From=F、To=F[+F]F[-F]F；生成器会按迭代次数连续替换。
    /// </summary>
    [Serializable]
    public sealed class MarineLSystemRule
    {
        [LabelText("原符号")]
        public string from = "F";

        [LabelText("替换为")]
        public string to = "F[+F]F[-F]F";

        public MarineLSystemRule()
        {
        }

        public MarineLSystemRule(string from, string to)
        {
            this.from = from;
            this.to = to;
        }
    }

    /// <summary>
    /// 程序植物运行时存档数据。
    /// 用途：保存固定 Seed、生长进度和已播放时间，读档后可以复现同一株植物的当前状态。
    /// 使用方式：外部存档系统只需要保存这个纯数据结构，再调用 MarinePlantPreview.ApplySaveData 还原。
    /// </summary>
    [Serializable]
    public sealed class MarineGrowthSaveData
    {
        [LabelText("物种 ID")]
        public string speciesId;

        [LabelText("固定随机种子")]
        public int seed;

        [LabelText("生长进度")]
        [Range(0f, 1f)]
        public float growth01;

        [LabelText("已播放时间")]
        public float elapsedSeconds;
    }
}
