using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ProceduralGrowingPlant
{
    /// <summary>
    /// 海洋植物 Sprite 素材包。
    /// 用途：集中管理海草、珊瑚、尖端、分叉等 Sprite，并通过权重决定生成时的出现概率。
    /// 使用方式：创建资源后把美术素材拖入 Pieces；生成器会按 MarinePieceKind 和 Weight 选择素材。
    /// </summary>
    [CreateAssetMenu(fileName = "MarineSpritePackSO", menuName = "程序生长植物/素材包 MarineSpritePackSO")]
    public sealed class MarineSpritePackSO : SerializedScriptableObject
    {
        #region 配置

        [Title("素材列表")]
        [InfoBox("权重为 0 的素材不会被随机选中。没有匹配用途时，生成器会退回到任意可用素材。")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, ShowIndexLabels = true)]
        [LabelText("素材")]
        public List<MarinePiece> pieces = new List<MarinePiece>();

        #endregion

        #region 查询

        /// <summary>
        /// 按用途和权重挑选一个素材。
        /// 输入：稳定随机数和素材用途。
        /// 输出：命中的 MarinePiece；如果列表为空则返回 null。
        /// 边界情况：用途没有匹配素材时会从全部有效素材中回退选择，避免生成结果完全空白。
        /// </summary>
        public MarinePiece Pick(System.Random rng, MarinePieceKind kind)
        {
            MarinePiece picked = PickInternal(rng, kind, true);
            return picked ?? PickInternal(rng, kind, false);
        }

        private MarinePiece PickInternal(System.Random rng, MarinePieceKind kind, bool matchKind)
        {
            if (pieces == null || pieces.Count == 0)
            {
                return null;
            }

            float totalWeight = 0f;
            for (int i = 0; i < pieces.Count; i++)
            {
                MarinePiece piece = pieces[i];
                if (piece == null || piece.weight <= 0f)
                {
                    continue;
                }

                if (matchKind && piece.kind != kind)
                {
                    continue;
                }

                totalWeight += piece.weight;
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float roll = (float)rng.NextDouble() * totalWeight;
            for (int i = 0; i < pieces.Count; i++)
            {
                MarinePiece piece = pieces[i];
                if (piece == null || piece.weight <= 0f)
                {
                    continue;
                }

                if (matchKind && piece.kind != kind)
                {
                    continue;
                }

                roll -= piece.weight;
                if (roll <= 0f)
                {
                    return piece;
                }
            }

            return null;
        }

        #endregion
    }
}
