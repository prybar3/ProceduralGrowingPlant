using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ProceduralGrowingPlant
{
    /// <summary>
    /// 海洋程序植物预览与运行时生成组件。
    /// 用途：读取 MarineSpeciesSO，在当前 Transform 下生成可复现的 Sprite 分段，并支持生长进度和海流摆动。
    /// 使用方式：挂到空 GameObject，指定 Species，点击 Generate；运行时可用 ApplySaveData 还原存档进度。
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("MiniEco/ProceduralGrowingPlant/海洋程序植物预览")]
    public sealed class MarinePlantPreview : MonoBehaviour
    {
        private const string ChildPrefix = "PGP_Generated_";
        private const int FallbackSpriteSize = 16;

        private sealed class SegmentRuntime
        {
            public Transform transform;
            public SpriteRenderer renderer;
            public Vector2 start;
            public Vector2 end;
            public float width;
            public float phase;
            public int index;
            public int count;
            public int stemIndex;
            public float startGrowth;
            public bool flipped;
            public bool isBranch;
            public bool isLeaf;
            public bool allowStretch;
            public Vector2 pieceScale;
            public MarinePieceSizeMode sizeMode;
            public Vector2 spriteSize;
            public Vector2 bottomSocketLocal;
            public Vector2 topSocketLocal;
            public Color color;
        }

        private struct TurtleState
        {
            public Vector2 position;
            public float angle;
            public float stepLength;
            public float width;
            public int depth;
            public float phase;
        }

        #region 配置

        [Title("物种")]
        [AssetsOnly, InlineEditor]
        [LabelText("物种配置")]
        public MarineSpeciesSO species;

        [Title("预览")]
        [LabelText("固定 Seed")]
        public int seed = 12345;

        [LabelText("生长进度"), Range(0f, 1f)]
        public float growth01 = 1f;

        [LabelText("自动播放生长")]
        public bool playGrowth;

        [LabelText("编辑器自动刷新")]
        public bool autoRefresh = true;

        #endregion

        #region 运行状态

        [ShowInInspector, ReadOnly, LabelText("已播放时间")]
        private float elapsedSeconds;

        [ShowInInspector, ReadOnly, LabelText("生成分段数量")]
        public int GeneratedSegmentCount => segments.Count;

        [ShowInInspector, ReadOnly, LabelText("生长完成")]
        public bool IsGrowthComplete => species != null && species.growthRule != null && segments.Count > 0 && growth01 >= 1f;

        private static Sprite fallbackSprite;
        private readonly List<SegmentRuntime> segments = new List<SegmentRuntime>();

#if UNITY_EDITOR
        private bool pendingEditorRefresh;
#endif

        #endregion

        #region Unity 生命周期

        private void OnEnable()
        {
            if (segments.Count == 0 && transform.childCount > 0)
            {
                ClearGenerated();
            }

            Generate();
        }

        private void OnValidate()
        {
            if (!autoRefresh)
            {
                return;
            }

#if UNITY_EDITOR
            if (pendingEditorRefresh)
            {
                return;
            }

            pendingEditorRefresh = true;
            UnityEditor.EditorApplication.delayCall += GenerateAfterValidate;
            return;
#else
            Generate();
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// 在 OnValidate 后延迟刷新生成结果。
        /// 输入：无，由 UnityEditor.EditorApplication.delayCall 调用。
        /// 输出：无，会重新生成当前预览对象。
        /// 边界情况：组件已经被删除、自动刷新关闭或退出编辑器时直接跳过，避免 OnValidate 内销毁对象导致 Unity 报错。
        /// </summary>
        private void GenerateAfterValidate()
        {
            pendingEditorRefresh = false;
            if (this == null || !autoRefresh)
            {
                return;
            }

            Generate();
        }
#endif

        private void Update()
        {
            float deltaTime = Application.isPlaying ? Time.deltaTime : 0.016f;
            AdvanceGrowth(deltaTime);
        }

        /// <summary>
        /// 主动推进程序植物生长与海流摆动。
        /// 输入：deltaTime 表示本次推进的秒数，可由 Unity Update 传入，也可由编辑器验收脚本手动传入。
        /// 输出：无，会更新 growth01、playGrowth 和已生成分段的 Transform。
        /// 边界情况：未配置物种、未生成分段或 deltaTime 小于 0 时安全跳过，避免验收工具误调用破坏场景。
        /// </summary>
        public void AdvanceGrowth(float deltaTime)
        {
            if (species == null || species.growthRule == null || segments.Count == 0)
            {
                return;
            }

            deltaTime = Mathf.Max(0f, deltaTime);
            elapsedSeconds += deltaTime;

            if (playGrowth)
            {
                growth01 = Mathf.Clamp01(growth01 + deltaTime * species.growthRule.growSpeed);
                if (growth01 >= 1f)
                {
                    playGrowth = false;
                }
            }

            ApplySegments();
        }

        #endregion

        #region 生成

        [Button("生成预览", ButtonSizes.Medium)]
        [GUIColor(0.35f, 0.8f, 1f)]
        public void Generate()
        {
            ClearGenerated();

            if (species == null || !species.CanGenerate())
            {
                return;
            }

            int useSeed = species.randomSeedOnGenerate ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : seed;
            seed = useSeed;

            System.Random rng = new System.Random(useSeed);
            MarineGrowthRuleSO rule = species.growthRule;
            int stemCount = Mathf.Max(1, rule.stemCount);
            int segmentCount = Mathf.Max(1, rule.segmentCount);

            if (rule.generationMode == MarineGenerationMode.LSystem)
            {
                BuildLSystem(rng, rule);
            }
            else
            {
                for (int stemIndex = 0; stemIndex < stemCount; stemIndex++)
                {
                    BuildSplineStem(rng, rule, stemIndex, segmentCount);
                }
            }

            ApplySegments();
        }

        [Button("从头播放生长", ButtonSizes.Medium)]
        [GUIColor(0.35f, 1f, 0.55f)]
        public void StartGrowth()
        {
            growth01 = 0f;
            elapsedSeconds = 0f;
            playGrowth = true;
            if (segments.Count == 0)
            {
                Generate();
            }
            else
            {
                ApplySegments();
            }
        }

        [Button("清理生成内容", ButtonSizes.Small)]
        public void ClearGenerated()
        {
            segments.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (!child.name.StartsWith(ChildPrefix))
                {
                    continue;
                }

                DestroyGeneratedObject(child.gameObject);
            }
        }

        private void BuildSplineStem(System.Random rng, MarineGrowthRuleSO rule, int stemIndex, int segmentCount)
        {
            float rootX = rule.rootSpread.Evaluate(rng);
            float fullLength = Mathf.Max(0.01f, rule.length.Evaluate(rng));
            float fullWidth = Mathf.Max(0.001f, rule.width.Evaluate(rng));
            float lean = rule.leanAngle.Evaluate(rng);
            float phase = (float)rng.NextDouble() * Mathf.PI * 2f;
            float noiseA = ((float)rng.NextDouble() * 2f - 1f) * rule.splineNoise;
            float noiseB = ((float)rng.NextDouble() * 2f - 1f) * rule.splineNoise;

            for (int i = 0; i < segmentCount; i++)
            {
                MarinePiece piece = species.spritePack.Pick(rng, i == segmentCount - 1 ? MarinePieceKind.Tip : MarinePieceKind.Stem);
                float start01 = (float)i / segmentCount;
                float end01 = (float)(i + 1) / segmentCount;
                Vector2 start = EvaluateSplinePoint(rule, rootX, fullLength, lean, noiseA, noiseB, start01);
                Vector2 end = EvaluateSplinePoint(rule, rootX, fullLength, lean, noiseA, noiseB, end01);
                AddPathSegment(rng, piece, stemIndex, i, segmentCount, start, end, fullWidth, phase, start01, false, false);
            }

            BuildSplineBranches(rng, rule, stemIndex, segmentCount, rootX, fullLength, fullWidth, lean, phase, noiseA, noiseB);
        }

        private void BuildSplineBranches(System.Random rng, MarineGrowthRuleSO rule, int stemIndex, int segmentCount, float rootX, float fullLength, float fullWidth, float lean, float phase, float noiseA, float noiseB)
        {
            int branchCount = 0;
            for (int i = 2; i < segmentCount - 1; i++)
            {
                if (branchCount >= rule.maxBranchesPerStem || rng.NextDouble() > rule.branchChance)
                {
                    TryBuildSplineLeaf(rng, rule, stemIndex, i, segmentCount, rootX, fullLength, fullWidth, lean, phase, noiseA, noiseB);
                    continue;
                }

                branchCount++;
                float height01 = (i + 0.5f) / segmentCount;
                float direction = rng.NextDouble() > 0.5 ? 1f : -1f;
                Vector2 basePoint = EvaluateSplinePoint(rule, rootX, fullLength, lean, noiseA, noiseB, height01);
                Vector2 before = EvaluateSplinePoint(rule, rootX, fullLength, lean, noiseA, noiseB, Mathf.Clamp01(height01 - 0.02f));
                Vector2 after = EvaluateSplinePoint(rule, rootX, fullLength, lean, noiseA, noiseB, Mathf.Clamp01(height01 + 0.02f));
                float tangentAngle = VectorToAngle(after - before);
                float branchAngle = tangentAngle + direction * rule.branchAngle.Evaluate(rng);
                float branchLength = fullLength * rule.branchLengthScale.Evaluate(rng);
                float branchWidth = fullWidth * 0.65f;
                int branchSegments = Mathf.Max(2, Mathf.RoundToInt(segmentCount * 0.45f));

                for (int j = 0; j < branchSegments; j++)
                {
                    MarinePiece piece = species.spritePack.Pick(rng, j == branchSegments - 1 ? MarinePieceKind.Tip : MarinePieceKind.Fork);
                    float start01 = (float)j / branchSegments;
                    float end01 = (float)(j + 1) / branchSegments;
                    Vector2 start = BuildBranchPoint(basePoint, branchAngle, branchLength, direction, rule.splineWave, start01);
                    Vector2 end = BuildBranchPoint(basePoint, branchAngle, branchLength, direction, rule.splineWave, end01);
                    AddPathSegment(rng, piece, stemIndex, j, branchSegments, start, end, branchWidth, phase + branchCount, height01 + j * 0.04f, true, false);
                }
            }
        }

        private void TryBuildSplineLeaf(System.Random rng, MarineGrowthRuleSO rule, int stemIndex, int index, int segmentCount, float rootX, float fullLength, float fullWidth, float lean, float phase, float noiseA, float noiseB)
        {
            if (rng.NextDouble() > rule.leafChance)
            {
                return;
            }

            float height01 = (index + 0.5f) / segmentCount;
            float direction = rng.NextDouble() > 0.5 ? 1f : -1f;
            float leafLength = fullLength / segmentCount * rule.leafScale.Evaluate(rng);
            float leafWidth = fullWidth * 1.35f;
            Vector2 basePoint = EvaluateSplinePoint(rule, rootX, fullLength, lean, noiseA, noiseB, height01);
            Vector2 before = EvaluateSplinePoint(rule, rootX, fullLength, lean, noiseA, noiseB, Mathf.Clamp01(height01 - 0.02f));
            Vector2 after = EvaluateSplinePoint(rule, rootX, fullLength, lean, noiseA, noiseB, Mathf.Clamp01(height01 + 0.02f));
            float leafAngle = VectorToAngle(after - before) + direction * 72f;
            MarinePiece piece = species.spritePack.Pick(rng, MarinePieceKind.Leaf);
            AddPathSegment(rng, piece, stemIndex, 0, 1, basePoint, basePoint + DirectionFromAngle(leafAngle) * leafLength, leafWidth, phase + index, height01 + 0.08f, false, true);
        }

        private void BuildLSystem(System.Random rng, MarineGrowthRuleSO rule)
        {
            string symbols = BuildLSystemSymbols(rule);
            int drawCount = CountDrawableSymbols(symbols);
            if (drawCount <= 0)
            {
                return;
            }

            Stack<TurtleState> stack = new Stack<TurtleState>();
            TurtleState state = new TurtleState
            {
                position = Vector2.zero,
                angle = 90f + rule.leanAngle.Evaluate(rng),
                stepLength = Mathf.Max(0.01f, rule.lSystemStepLength.Evaluate(rng)),
                width = Mathf.Max(0.001f, rule.width.Evaluate(rng)),
                depth = 0,
                phase = (float)rng.NextDouble() * Mathf.PI * 2f,
            };

            int drawn = 0;
            for (int i = 0; i < symbols.Length; i++)
            {
                char symbol = symbols[i];
                if (symbol == 'F')
                {
                    Vector2 next = state.position + DirectionFromAngle(state.angle) * state.stepLength;
                    MarinePieceKind pieceKind = state.depth == 0 ? MarinePieceKind.Stem : MarinePieceKind.Fork;
                    MarinePiece piece = species.spritePack.Pick(rng, pieceKind);
                    AddPathSegment(rng, piece, state.depth, drawn, drawCount, state.position, next, state.width, state.phase, (float)drawn / drawCount, state.depth > 0, false);
                    state.position = next;
                    drawn++;
                }
                else if (symbol == 'L')
                {
                    float leafAngle = state.angle + (rng.NextDouble() > 0.5 ? 1f : -1f) * 70f;
                    float leafLength = state.stepLength * rule.leafScale.Evaluate(rng);
                    MarinePiece piece = species.spritePack.Pick(rng, MarinePieceKind.Decor);
                    AddPathSegment(rng, piece, state.depth, drawn, drawCount, state.position, state.position + DirectionFromAngle(leafAngle) * leafLength, state.width * 1.2f, state.phase, (float)drawn / drawCount, false, true);
                    drawn++;
                }
                else if (symbol == '+')
                {
                    state.angle += rule.lSystemTurnAngle.Evaluate(rng) + RandomSigned(rng, rule.lSystemAngleJitter);
                }
                else if (symbol == '-')
                {
                    state.angle -= rule.lSystemTurnAngle.Evaluate(rng) + RandomSigned(rng, rule.lSystemAngleJitter);
                }
                else if (symbol == '[')
                {
                    stack.Push(state);
                    state.depth++;
                    state.stepLength *= rule.lSystemLengthFalloff;
                    state.width *= rule.lSystemWidthFalloff;
                    state.phase += state.depth * 0.73f;
                }
                else if (symbol == ']' && stack.Count > 0)
                {
                    state = stack.Pop();
                }
            }
        }

        private void AddPathSegment(System.Random rng, MarinePiece piece, int stemIndex, int index, int count, Vector2 start, Vector2 end, float width, float phase, float startGrowth, bool isBranch, bool isLeaf)
        {
            GameObject child = new GameObject($"{ChildPrefix}{stemIndex}_{segments.Count}");
            child.transform.SetParent(transform, false);

            SpriteRenderer spriteRenderer = child.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = piece?.sprite != null ? piece.sprite : GetFallbackSprite();
            spriteRenderer.sortingOrder = stemIndex * 1000 + segments.Count;
            spriteRenderer.color = BuildColor(rng, piece);
            Sprite useSprite = spriteRenderer.sprite;

            segments.Add(new SegmentRuntime
            {
                transform = child.transform,
                renderer = spriteRenderer,
                start = start,
                end = end,
                width = width,
                phase = phase,
                index = index,
                count = count,
                stemIndex = stemIndex,
                startGrowth = Mathf.Clamp01(startGrowth),
                flipped = piece != null && piece.allowFlip && rng.NextDouble() > 0.5,
                isBranch = isBranch,
                isLeaf = isLeaf,
                allowStretch = piece == null || piece.allowStretch,
                pieceScale = piece != null ? piece.defaultScale : Vector2.one,
                sizeMode = piece != null ? piece.sizeMode : MarinePieceSizeMode.RuleDriven,
                spriteSize = GetSpriteSize(useSprite),
                bottomSocketLocal = GetSocketLocal(useSprite, piece != null ? piece.bottomSocket : Vector2.zero),
                topSocketLocal = GetSocketLocal(useSprite, piece != null ? piece.topSocket : Vector2.up),
                color = spriteRenderer.color,
            });
        }

        #endregion

        #region 更新

        private void ApplySegments()
        {
            MarineGrowthRuleSO rule = species.growthRule;
            float grown = rule.EvaluateGrowth(growth01);
            float globalScale = Mathf.Max(0.001f, rule.globalSizeScale);

            for (int i = 0; i < segments.Count; i++)
            {
                SegmentRuntime segment = segments[i];
                float height01 = Mathf.Clamp01((segment.index + 0.5f) / Mathf.Max(1f, segment.count));
                float visible01 = Mathf.Clamp01((grown - segment.startGrowth) * segment.count);
                bool visible = visible01 > 0.01f;

                segment.renderer.enabled = visible;
                if (!visible)
                {
                    continue;
                }

                float time = Application.isPlaying ? Time.time : elapsedSeconds;
                float wave = Mathf.Sin(time * rule.swaySpeed + segment.phase + height01 * rule.heightPhase);
                float sway = wave * rule.swayAmplitude * height01 * grown * globalScale;
                Vector2 scaledStart = segment.start * globalScale;
                Vector2 scaledEnd = segment.end * globalScale;
                Vector2 direction = scaledEnd - scaledStart;
                float segmentLength = Mathf.Max(0.001f, direction.magnitude);
                float angle = VectorToAngle(direction) - 90f;
                float width = segment.width * rule.EvaluateWidth(segment.isLeaf ? 0.75f : height01) * globalScale;
                Vector3 scale = CalculateSegmentScale(segment, width, segmentLength, visible01, globalScale);
                float socketX = segment.flipped ? -segment.bottomSocketLocal.x : segment.bottomSocketLocal.x;
                Vector3 bottomOffset = new Vector3(socketX * scale.x, segment.bottomSocketLocal.y * scale.y, 0f);
                Quaternion planarRotation = Quaternion.Euler(0f, 0f, angle - wave * 8f);
                Vector2 start = scaledStart + new Vector2(sway, 0f);
                Vector3 position = new Vector3(start.x, start.y, 0f) - planarRotation * bottomOffset;

                segment.transform.localPosition = position;
                segment.transform.localRotation = Quaternion.Euler(0f, segment.flipped ? 180f : 0f, angle - wave * 8f);
                segment.transform.localScale = scale;
                segment.renderer.color = segment.color;
            }
        }

        /// <summary>
        /// 根据素材尺寸模式计算单节最终缩放。
        /// 输入：缓存的素材尺寸、规则宽度、路径段长度、当前生长可见比例和整体尺寸倍率。
        /// 输出：可直接赋值给 Transform.localScale 的三维缩放。
        /// 边界情况：素材尺寸或 Socket 距离为 0 时使用安全最小值，避免除零导致对象消失或出现 NaN。
        /// </summary>
        private static Vector3 CalculateSegmentScale(SegmentRuntime segment, float ruleWidth, float segmentLength, float visible01, float globalScale)
        {
            Vector2 spriteSize = new Vector2(Mathf.Max(0.001f, segment.spriteSize.x), Mathf.Max(0.001f, segment.spriteSize.y));
            Vector2 pieceScale = new Vector2(Mathf.Max(0.001f, segment.pieceScale.x), Mathf.Max(0.001f, segment.pieceScale.y));
            MarinePieceSizeMode mode = segment.allowStretch ? segment.sizeMode : MarinePieceSizeMode.NativeSize;

            if (mode == MarinePieceSizeMode.NativeSize)
            {
                return new Vector3(pieceScale.x * globalScale, pieceScale.y * globalScale * visible01, 1f);
            }

            if (mode == MarinePieceSizeMode.SocketMatch)
            {
                float socketDistance = Mathf.Max(0.001f, Vector2.Distance(segment.bottomSocketLocal, segment.topSocketLocal));
                float uniformScale = segmentLength / socketDistance;
                return new Vector3(uniformScale * pieceScale.x, uniformScale * pieceScale.y * visible01, 1f);
            }

            return new Vector3(ruleWidth / spriteSize.x * pieceScale.x, segmentLength / spriteSize.y * pieceScale.y * visible01, 1f);
        }

        private Color BuildColor(System.Random rng, MarinePiece piece)
        {
            Color baseColor = piece != null && !piece.useSpeciesColor ? Color.white : species != null ? species.baseColor : Color.white;
            Color tint = piece != null ? piece.colorTint : Color.white;
            float variance = species != null && species.growthRule != null ? species.growthRule.colorVariance : 0f;
            float r = 1f + ((float)rng.NextDouble() * 2f - 1f) * variance;
            float g = 1f + ((float)rng.NextDouble() * 2f - 1f) * variance;
            float b = 1f + ((float)rng.NextDouble() * 2f - 1f) * variance;
            return new Color(
                Mathf.Clamp01(baseColor.r * tint.r * r),
                Mathf.Clamp01(baseColor.g * tint.g * g),
                Mathf.Clamp01(baseColor.b * tint.b * b),
                Mathf.Clamp01(baseColor.a * tint.a));
        }

        #endregion

        #region 存档

        /// <summary>
        /// 导出当前生长状态。
        /// 输入：无。
        /// 输出：包含物种 ID、Seed、生长进度和已播放时间的纯数据对象。
        /// 边界情况：没有物种时 speciesId 为空字符串，外部系统可据此跳过还原。
        /// </summary>
        public MarineGrowthSaveData CaptureSaveData()
        {
            return new MarineGrowthSaveData
            {
                speciesId = species != null ? species.speciesId : string.Empty,
                seed = seed,
                growth01 = Mathf.Clamp01(growth01),
                elapsedSeconds = elapsedSeconds,
            };
        }

        /// <summary>
        /// 应用外部存档数据。
        /// 输入：MarineGrowthSaveData，通常来自项目存档系统。
        /// 输出：无；组件会重新生成同 Seed 的植物并恢复生长时间。
        /// 边界情况：传入 null 时不做任何修改，避免读档流程因为空数据破坏当前预览。
        /// </summary>
        public void ApplySaveData(MarineGrowthSaveData data)
        {
            if (data == null)
            {
                return;
            }

            seed = data.seed;
            growth01 = Mathf.Clamp01(data.growth01);
            elapsedSeconds = Mathf.Max(0f, data.elapsedSeconds);
            Generate();
        }

        #endregion

        #region 工具

        /// <summary>
        /// 采样海草中心线上的一个点。
        /// 输入：规则、根部 X、完整长度、倾斜角和两段随机噪声。
        /// 输出：t 对应的本地坐标点，t=0 是根部，t=1 是顶端。
        /// 边界情况：t 会被限制在 0-1，避免外部参数导致曲线反向或越界。
        /// </summary>
        private static Vector2 EvaluateSplinePoint(MarineGrowthRuleSO rule, float rootX, float length, float lean, float noiseA, float noiseB, float t)
        {
            t = Mathf.Clamp01(t);
            Vector2 start = new Vector2(rootX, 0f);
            Vector2 end = start + new Vector2(Mathf.Sin(lean * Mathf.Deg2Rad) * length, length * (1f - rule.splineGravity * t));
            float wave = Mathf.Sin(t * Mathf.PI * 2f) * rule.splineWave;

            if (rule.splineKind == MarineSplineKind.Bezier)
            {
                Vector2 p1 = start + new Vector2(noiseA + wave, length * 0.35f);
                Vector2 p2 = start + new Vector2(noiseB - wave, length * 0.72f);
                return CubicBezier(start, p1, p2, end, t);
            }

            if (rule.splineKind == MarineSplineKind.Hermite)
            {
                Vector2 tangentA = new Vector2(noiseA + wave, length * rule.splineCurvature);
                Vector2 tangentB = new Vector2(noiseB - wave, length * rule.splineCurvature);
                return Hermite(start, end, tangentA, tangentB, t);
            }

            Vector2 c0 = start - Vector2.up * length * 0.25f;
            Vector2 c1 = start;
            Vector2 c2 = start + new Vector2(noiseA + wave, length * 0.58f);
            Vector2 c3 = end + new Vector2(noiseB - wave, length * 0.25f);
            return CatmullRom(c0, c1, c2, c3, t);
        }

        private static Vector2 BuildBranchPoint(Vector2 root, float angle, float length, float direction, float waveStrength, float t)
        {
            Vector2 forward = DirectionFromAngle(angle);
            Vector2 side = new Vector2(-forward.y, forward.x) * direction;
            float bend = Mathf.Sin(t * Mathf.PI) * waveStrength;
            return root + forward * length * t + side * bend;
        }

        /// <summary>
        /// 展开 L-System 字符串。
        /// 输入：规则 SO 中的公理、替换规则、迭代次数和最大符号数。
        /// 输出：用于海龟解释器的符号串。
        /// 边界情况：规则为空时保留原符号；超过最大符号数时截断，防止误配置造成编辑器卡死。
        /// </summary>
        private static string BuildLSystemSymbols(MarineGrowthRuleSO rule)
        {
            string current = string.IsNullOrEmpty(rule.lSystemAxiom) ? "F" : rule.lSystemAxiom;
            int maxSymbols = Mathf.Max(16, rule.lSystemMaxSymbols);

            for (int i = 0; i < rule.lSystemIterations; i++)
            {
                System.Text.StringBuilder next = new System.Text.StringBuilder(Mathf.Min(maxSymbols, current.Length * 4));
                for (int j = 0; j < current.Length; j++)
                {
                    string replacement = FindLSystemReplacement(rule, current[j]);
                    next.Append(replacement ?? current[j].ToString());
                    if (next.Length >= maxSymbols)
                    {
                        return next.ToString(0, maxSymbols);
                    }
                }

                current = next.ToString();
            }

            return current;
        }

        private static string FindLSystemReplacement(MarineGrowthRuleSO rule, char symbol)
        {
            if (rule.lSystemRules == null)
            {
                return null;
            }

            for (int i = 0; i < rule.lSystemRules.Count; i++)
            {
                MarineLSystemRule lRule = rule.lSystemRules[i];
                if (lRule == null || string.IsNullOrEmpty(lRule.from) || lRule.from[0] != symbol)
                {
                    continue;
                }

                return lRule.to;
            }

            return null;
        }

        private static int CountDrawableSymbols(string symbols)
        {
            int count = 0;
            for (int i = 0; i < symbols.Length; i++)
            {
                if (symbols[i] == 'F' || symbols[i] == 'L')
                {
                    count++;
                }
            }

            return count;
        }

        private static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }

        private static Vector2 Hermite(Vector2 p0, Vector2 p1, Vector2 m0, Vector2 m1, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * p0
                + (t3 - 2f * t2 + t) * m0
                + (-2f * t3 + 3f * t2) * p1
                + (t3 - t2) * m1;
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1)
                + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static Vector2 DirectionFromAngle(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static float VectorToAngle(Vector2 vector)
        {
            if (vector.sqrMagnitude <= 0.0001f)
            {
                return 90f;
            }

            return Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
        }

        private static float RandomSigned(System.Random rng, float range)
        {
            return ((float)rng.NextDouble() * 2f - 1f) * Mathf.Max(0f, range);
        }

        /// <summary>
        /// 读取 Sprite 在 Transform 缩放为 1 时的本地世界尺寸。
        /// 输入：Sprite，可为空。
        /// 输出：Sprite.bounds.size 的二维尺寸。
        /// 边界情况：Sprite 为空或尺寸异常时返回 Vector2.one，保证规则驱动模式仍能生成可见对象。
        /// </summary>
        private static Vector2 GetSpriteSize(Sprite sprite)
        {
            if (sprite == null)
            {
                return Vector2.one;
            }

            Vector3 size = sprite.bounds.size;
            return new Vector2(Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y));
        }

        /// <summary>
        /// 把归一化 Socket 转换为 Sprite 本地坐标。
        /// 输入：Sprite 和 Socket，Socket.x 是相对素材中心的水平偏移，Socket.y 是从底部到顶部的高度比例。
        /// 输出：考虑 Sprite pivot 后的本地坐标，可直接参与 Transform 对齐计算。
        /// 边界情况：Sprite 为空时使用一个 1x1 的虚拟素材范围，避免外部缺图时 Socket 对齐失败。
        /// </summary>
        private static Vector2 GetSocketLocal(Sprite sprite, Vector2 socket)
        {
            if (sprite == null)
            {
                return new Vector2(socket.x, Mathf.Clamp01(socket.y));
            }

            Bounds bounds = sprite.bounds;
            float x = bounds.center.x + socket.x * bounds.size.x;
            float y = Mathf.Lerp(bounds.min.y, bounds.max.y, Mathf.Clamp01(socket.y));
            return new Vector2(x, y);
        }

        private static Sprite GetFallbackSprite()
        {
            if (fallbackSprite != null)
            {
                return fallbackSprite;
            }

            Texture2D texture = new Texture2D(FallbackSpriteSize, FallbackSpriteSize, TextureFormat.RGBA32, false)
            {
                name = "PGP_FallbackSpriteTexture",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
            };

            Color[] pixels = new Color[FallbackSpriteSize * FallbackSpriteSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, FallbackSpriteSize, FallbackSpriteSize), new Vector2(0.5f, 0f), FallbackSpriteSize);
            fallbackSprite.name = "PGP_FallbackSprite";
            fallbackSprite.hideFlags = HideFlags.HideAndDontSave;
            return fallbackSprite;
        }

        private static void DestroyGeneratedObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        #endregion
    }
}
