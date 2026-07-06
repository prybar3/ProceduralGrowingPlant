using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProceduralGrowingPlant.Editor
{
    /// <summary>
    /// 程序海洋植物验收样例工具。
    /// 用途：一键创建最小样例资源、手绘风格 Sprite 素材和场景预览对象，确保 Spline 与 L-System 都能在 Unity 中生成并播放生长。
    /// 使用方式：执行 Tools/程序生长植物 下的菜单；工具会复用已有样例资源，只覆盖 ProceduralGrowingPlant/Samples 下由本工具创建的样例数据。
    /// </summary>
    public static class MarineSampleUtility
    {
        private const string SampleFolder = "Assets/ProceduralGrowingPlant/Samples";
        private const string HandSpriteFolder = SampleFolder + "/HandDrawnSprites";

        private const string SpeciesPath = SampleFolder + "/PGP_AcceptanceSpeciesSO.asset";
        private const string RulePath = SampleFolder + "/PGP_AcceptanceGrowthRuleSO.asset";
        private const string PackPath = SampleFolder + "/PGP_AcceptanceSpritePackSO.asset";

        private const string SplineSpeciesPath = SampleFolder + "/PGP_SplineKelpSpeciesSO.asset";
        private const string SplineRulePath = SampleFolder + "/PGP_SplineKelpGrowthRuleSO.asset";
        private const string CoralSpeciesPath = SampleFolder + "/PGP_LSystemCoralSpeciesSO.asset";
        private const string CoralRulePath = SampleFolder + "/PGP_LSystemCoralGrowthRuleSO.asset";
        private const string HandPackPath = SampleFolder + "/PGP_HandDrawnSpritePackSO.asset";

        #region 菜单

        [MenuItem("Tools/程序生长植物/创建验收样例并播放生长")]
        public static void CreateAcceptanceSample()
        {
            EnsureFolder(SampleFolder);

            MarineGrowthRuleSO rule = LoadOrCreate<MarineGrowthRuleSO>(RulePath);
            MarineSpritePackSO pack = LoadOrCreate<MarineSpritePackSO>(PackPath);
            MarineSpeciesSO species = LoadOrCreate<MarineSpeciesSO>(SpeciesPath);

            ConfigureSplineRule(rule, true);
            ConfigureHandDrawnPack(pack);
            ConfigureSpecies(species, rule, pack, "pgp_acceptance_seaweed", "验收程序海草", MarinePlantKind.Seaweed, new Color(0.24f, 0.92f, 0.58f, 1f), 24681357);
            AssetDatabase.SaveAssets();

            CreatePreviewObject(species, "PGP_验收程序海草", Vector3.zero);
        }

        [MenuItem("Tools/Procedural Growing Plant/Create Acceptance Sample And Play Growth")]
        private static void CreateAcceptanceSampleEnglish()
        {
            CreateAcceptanceSample();
        }

        [MenuItem("Tools/程序生长植物/验证 LSystem 珊瑚和 Spline 长海草")]
        public static void CreateLSystemAndSplineValidation()
        {
            EnsureFolder(SampleFolder);

            MarineSpritePackSO pack = LoadOrCreate<MarineSpritePackSO>(HandPackPath);
            MarineGrowthRuleSO splineRule = LoadOrCreate<MarineGrowthRuleSO>(SplineRulePath);
            MarineSpeciesSO splineSpecies = LoadOrCreate<MarineSpeciesSO>(SplineSpeciesPath);
            MarineGrowthRuleSO coralRule = LoadOrCreate<MarineGrowthRuleSO>(CoralRulePath);
            MarineSpeciesSO coralSpecies = LoadOrCreate<MarineSpeciesSO>(CoralSpeciesPath);

            ConfigureHandDrawnPack(pack);
            ConfigureSplineRule(splineRule, false);
            ConfigureLSystemRule(coralRule);
            ConfigureSpecies(splineSpecies, splineRule, pack, "pgp_spline_long_kelp", "Spline长海草", MarinePlantKind.Seaweed, new Color(0.18f, 0.78f, 0.52f, 1f), 13579);
            ConfigureSpecies(coralSpecies, coralRule, pack, "pgp_lsystem_red_coral", "LSystem红珊瑚", MarinePlantKind.Coral, new Color(1f, 0.34f, 0.28f, 1f), 97531);
            AssetDatabase.SaveAssets();

            CreatePreviewObject(splineSpecies, "PGP_Spline长海草验证", new Vector3(-2.2f, 0f, 0f));
            CreatePreviewObject(coralSpecies, "PGP_LSystem珊瑚验证", new Vector3(2.2f, 0f, 0f));
        }

        [MenuItem("Tools/Procedural Growing Plant/Validate LSystem Coral And Spline Kelp")]
        private static void CreateLSystemAndSplineValidationEnglish()
        {
            CreateLSystemAndSplineValidation();
        }

        #endregion

        #region 配置

        private static void ConfigureSplineRule(MarineGrowthRuleSO rule, bool compact)
        {
            Undo.RecordObject(rule, "配置程序植物 Spline 验收规则");
            rule.generationMode = MarineGenerationMode.Spline;
            rule.globalSizeScale = 1f;
            rule.splineKind = MarineSplineKind.CatmullRom;
            rule.stemCount = compact ? 7 : 11;
            rule.segmentCount = compact ? 9 : 28;
            rule.rootSpread = compact ? new MarineFloatRange(-0.45f, 0.45f) : new MarineFloatRange(-0.75f, 0.75f);
            rule.length = compact ? new MarineFloatRange(1.15f, 2.1f) : new MarineFloatRange(4.6f, 6.8f);
            rule.width = compact ? new MarineFloatRange(0.08f, 0.15f) : new MarineFloatRange(0.055f, 0.11f);
            rule.leanAngle = compact ? new MarineFloatRange(-16f, 16f) : new MarineFloatRange(-24f, 24f);
            rule.colorVariance = 0.16f;
            rule.splineCurvature = compact ? 0.35f : 0.62f;
            rule.splineNoise = compact ? 0.18f : 0.46f;
            rule.splineWave = compact ? 0.12f : 0.32f;
            rule.splineGravity = compact ? 0.04f : 0.12f;
            rule.branchChance = compact ? 0.55f : 0.22f;
            rule.maxBranchesPerStem = compact ? 3 : 2;
            rule.branchAngle = new MarineFloatRange(28f, 55f);
            rule.branchLengthScale = compact ? new MarineFloatRange(0.28f, 0.5f) : new MarineFloatRange(0.16f, 0.32f);
            rule.leafChance = compact ? 0.65f : 0.45f;
            rule.leafScale = compact ? new MarineFloatRange(0.45f, 0.8f) : new MarineFloatRange(0.35f, 0.58f);
            rule.growSpeed = compact ? 1.2f : 1.6f;
            rule.swayAmplitude = compact ? 0.08f : 0.22f;
            rule.swaySpeed = compact ? 1.7f : 1.35f;
            rule.heightPhase = compact ? 2.4f : 4.1f;
            EditorUtility.SetDirty(rule);
        }

        private static void ConfigureLSystemRule(MarineGrowthRuleSO rule)
        {
            Undo.RecordObject(rule, "配置程序植物 L-System 验收规则");
            rule.generationMode = MarineGenerationMode.LSystem;
            rule.globalSizeScale = 1f;
            rule.stemCount = 1;
            rule.segmentCount = 1;
            rule.rootSpread = new MarineFloatRange(0f, 0f);
            rule.length = new MarineFloatRange(1f, 1f);
            rule.width = new MarineFloatRange(0.08f, 0.14f);
            rule.leanAngle = new MarineFloatRange(-4f, 4f);
            rule.colorVariance = 0.1f;
            rule.branchChance = 0f;
            rule.maxBranchesPerStem = 0;
            rule.leafChance = 0.15f;
            rule.leafScale = new MarineFloatRange(0.55f, 0.9f);
            rule.lSystemAxiom = "F";
            rule.lSystemIterations = 4;
            rule.lSystemMaxSymbols = 1600;
            rule.lSystemRules.Clear();
            rule.lSystemRules.Add(new MarineLSystemRule("F", "FF[+F][-F]F[+L]"));
            rule.lSystemStepLength = new MarineFloatRange(0.18f, 0.26f);
            rule.lSystemTurnAngle = new MarineFloatRange(20f, 34f);
            rule.lSystemAngleJitter = 7f;
            rule.lSystemLengthFalloff = 0.78f;
            rule.lSystemWidthFalloff = 0.72f;
            rule.growSpeed = 1.35f;
            rule.swayAmplitude = 0.025f;
            rule.swaySpeed = 0.85f;
            rule.heightPhase = 1.6f;
            EditorUtility.SetDirty(rule);
        }

        private static void ConfigureSpecies(MarineSpeciesSO species, MarineGrowthRuleSO rule, MarineSpritePackSO pack, string id, string displayName, MarinePlantKind kind, Color color, int seed)
        {
            Undo.RecordObject(species, "配置程序植物验收物种");
            species.speciesId = id;
            species.displayName = displayName;
            species.kind = kind;
            species.baseColor = color;
            species.defaultSeed = seed;
            species.randomSeedOnGenerate = false;
            species.growthRule = rule;
            species.spritePack = pack;
            EditorUtility.SetDirty(species);
        }

        private static void ConfigureHandDrawnPack(MarineSpritePackSO pack)
        {
            Undo.RecordObject(pack, "配置程序植物手绘素材包");
            EnsureFolder(HandSpriteFolder);

            pack.pieces.Clear();
            AddPiece(pack, EnsureHandSprite("PGP_HandStem", MarinePieceKind.Stem), MarinePieceKind.Stem, 80f, new Vector2(1f, 1f), MarinePieceSizeMode.SocketMatch, true, true, Color.white);
            AddPiece(pack, EnsureHandSprite("PGP_HandStemAlt", MarinePieceKind.Stem), MarinePieceKind.Stem, 35f, new Vector2(0.85f, 1.08f), MarinePieceSizeMode.SocketMatch, true, true, new Color(0.92f, 1f, 0.95f, 1f));
            AddPiece(pack, EnsureHandSprite("PGP_HandTip", MarinePieceKind.Tip), MarinePieceKind.Tip, 60f, new Vector2(1.1f, 0.8f), MarinePieceSizeMode.SocketMatch, true, true, Color.white);
            AddPiece(pack, EnsureHandSprite("PGP_HandFork", MarinePieceKind.Fork), MarinePieceKind.Fork, 75f, new Vector2(0.95f, 0.95f), MarinePieceSizeMode.SocketMatch, true, true, Color.white);
            AddPiece(pack, EnsureHandSprite("PGP_HandLeaf", MarinePieceKind.Leaf), MarinePieceKind.Leaf, 70f, new Vector2(0.9f, 0.7f), MarinePieceSizeMode.NativeSize, false, true, new Color(0.85f, 1f, 0.9f, 1f));
            AddPiece(pack, EnsureHandSprite("PGP_HandDecor", MarinePieceKind.Decor), MarinePieceKind.Decor, 45f, new Vector2(0.8f, 0.8f), MarinePieceSizeMode.NativeSize, false, false, new Color(1f, 0.78f, 0.52f, 1f));
            EditorUtility.SetDirty(pack);
        }

        private static void AddPiece(MarineSpritePackSO pack, Sprite sprite, MarinePieceKind kind, float weight, Vector2 scale, MarinePieceSizeMode sizeMode, bool stretch, bool useSpeciesColor, Color tint)
        {
            pack.pieces.Add(new MarinePiece
            {
                sprite = sprite,
                kind = kind,
                weight = weight,
                defaultScale = scale,
                sizeMode = sizeMode,
                allowFlip = true,
                allowStretch = stretch,
                useSpeciesColor = useSpeciesColor,
                colorTint = tint,
                bottomSocket = Vector2.zero,
                topSocket = Vector2.up,
            });
        }

        #endregion

        #region 素材

        private static Sprite EnsureHandSprite(string fileName, MarinePieceKind kind)
        {
            string path = $"{HandSpriteFolder}/{fileName}.png";
            WriteHandSprite(path, kind);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool changed = importer.textureType != TextureImporterType.Sprite
                    || importer.spriteImportMode != SpriteImportMode.Single
                    || Mathf.Abs(importer.spritePixelsPerUnit - 64f) > 0.01f
                    || !importer.alphaIsTransparency
                    || importer.filterMode != FilterMode.Bilinear;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64f;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            return sprite;
        }

        private static void WriteHandSprite(string path, MarinePieceKind kind)
        {
            int width = kind == MarinePieceKind.Leaf || kind == MarinePieceKind.Decor ? 64 : 40;
            int height = kind == MarinePieceKind.Decor ? 64 : 96;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width * 2f - 1f;
                    float wobble = Mathf.Sin(v * 18f + (int)kind * 1.7f) * 0.06f + Mathf.Sin(v * 41f) * 0.025f;
                    float radius = GetSpriteRadius(kind, v);
                    float distance = Mathf.Abs(u - wobble);
                    float alpha = Mathf.Clamp01((radius - distance) * 18f);
                    float stroke = Mathf.Clamp01((radius + 0.035f - distance) * 24f) * (1f - alpha);
                    Color fill = new Color(1f, 1f, 1f, alpha);
                    Color outline = new Color(0.38f, 0.28f, 0.22f, Mathf.Clamp01(stroke * 0.55f));
                    pixels[y * width + x] = alpha > 0.001f || outline.a > 0.001f
                        ? AlphaBlend(outline, fill)
                        : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// 按透明度合成手绘填充色和描边色。
        /// 输入：底层描边色和上层填充色，两个颜色都使用直通 Alpha。
        /// 输出：合成后的直通 Alpha 颜色。
        /// 边界情况：两个颜色都透明时返回 Color.clear，避免 PNG 透明区域残留深色 RGB。
        /// </summary>
        private static Color AlphaBlend(Color bottom, Color top)
        {
            float alpha = top.a + bottom.a * (1f - top.a);
            if (alpha <= 0.001f)
            {
                return Color.clear;
            }

            float r = (top.r * top.a + bottom.r * bottom.a * (1f - top.a)) / alpha;
            float g = (top.g * top.a + bottom.g * bottom.a * (1f - top.a)) / alpha;
            float b = (top.b * top.a + bottom.b * bottom.a * (1f - top.a)) / alpha;
            return new Color(r, g, b, alpha);
        }

        private static float GetSpriteRadius(MarinePieceKind kind, float v)
        {
            if (kind == MarinePieceKind.Tip)
            {
                return Mathf.Lerp(0.28f, 0.02f, v);
            }

            if (kind == MarinePieceKind.Leaf)
            {
                return Mathf.Sin(v * Mathf.PI) * 0.34f;
            }

            if (kind == MarinePieceKind.Decor)
            {
                return Mathf.Sin(v * Mathf.PI) * 0.22f + 0.05f;
            }

            if (kind == MarinePieceKind.Fork)
            {
                return Mathf.Lerp(0.18f, 0.1f, v) + Mathf.Sin(v * Mathf.PI) * 0.08f;
            }

            return Mathf.Lerp(0.16f, 0.1f, v) + Mathf.Sin(v * Mathf.PI) * 0.04f;
        }

        #endregion

        #region 资源

        private static void CreatePreviewObject(MarineSpeciesSO species, string objectName, Vector3 position)
        {
            GameObject root = new GameObject(GameObjectUtility.GetUniqueNameForSibling(null, objectName));
            Undo.RegisterCreatedObjectUndo(root, "创建程序植物验收样例");
            root.transform.position = position;

            MarinePlantPreview preview = Undo.AddComponent<MarinePlantPreview>(root);
            preview.species = species;
            preview.seed = species.defaultSeed;
            preview.growth01 = 0f;
            preview.autoRefresh = false;
            preview.Generate();
            preview.StartGrowth();

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorGUIUtility.PingObject(root);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        #endregion
    }
}
