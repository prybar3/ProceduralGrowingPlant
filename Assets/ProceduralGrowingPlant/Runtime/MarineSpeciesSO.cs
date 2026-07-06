using Sirenix.OdinInspector;
using UnityEngine;

namespace ProceduralGrowingPlant
{
    /// <summary>
    /// 海洋程序植物物种配置。
    /// 用途：把物种名称、素材包、生长规则和默认 Seed 组合成一个可复用预设。
    /// 使用方式：美术为每个物种创建一个 MarineSpeciesSO，运行时或编辑器预览只引用该资源。
    /// </summary>
    [CreateAssetMenu(fileName = "MarineSpeciesSO", menuName = "程序生长植物/物种 MarineSpeciesSO")]
    public sealed class MarineSpeciesSO : SerializedScriptableObject
    {
        #region 基础信息

        [Title("基础信息")]
        [LabelText("物种 ID")]
        public string speciesId = "NewSpecies";

        [LabelText("显示名称")]
        public string displayName = "新海洋植物";

        [LabelText("植物类型")]
        public MarinePlantKind kind = MarinePlantKind.Seaweed;

        [LabelText("基础颜色")]
        public Color baseColor = Color.white;

        #endregion

        #region 规则引用

        [Title("规则引用")]
        [AssetsOnly, InlineEditor]
        [LabelText("素材包")]
        public MarineSpritePackSO spritePack;

        [AssetsOnly, InlineEditor]
        [LabelText("生长规则")]
        public MarineGrowthRuleSO growthRule;

        #endregion

        #region 随机

        [Title("随机")]
        [LabelText("默认 Seed")]
        public int defaultSeed = 12345;

        [LabelText("生成时随机 Seed")]
        public bool randomSeedOnGenerate;

        #endregion

        #region 校验

        /// <summary>
        /// 判断物种是否拥有最小可生成数据。
        /// 输入：无。
        /// 输出：素材包和规则都存在时返回 true。
        /// 边界情况：素材包可以没有 Sprite，生成器仍会生成空 SpriteRenderer 作为占位，方便后续补素材。
        /// </summary>
        public bool CanGenerate()
        {
            return growthRule != null && spritePack != null;
        }

        #endregion
    }
}
