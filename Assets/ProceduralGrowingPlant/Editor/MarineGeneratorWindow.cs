using ProceduralGrowingPlant;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProceduralGrowingPlant.Editor
{
    /// <summary>
    /// 海洋程序植物生成窗口。
    /// 用途：提供基于 UI Toolkit 的创作入口，让美术选择物种、编辑数据、生成场景预览并导出 Prefab。
    /// 使用方式：通过 Tools/程序生长植物/海洋植物生成器 打开；左侧选择或创建资源，中间生成预览，右侧直接编辑 SO 和预览组件。
    /// </summary>
    public sealed class MarineGeneratorWindow : EditorWindow
    {
        #region 字段
        private MarineSpeciesSO species;
        private Transform parent;
        private MarinePlantPreview preview;
        private int seed = 12345;
        private float growth01 = 1f;

        private ObjectField speciesField;
        private ObjectField parentField;
        private IntegerField seedField;
        private Slider growthSlider;
        private Label statusLabel;
        private Label previewLabel;
        private Button generateButton;
        private Button refreshButton;
        private Button exportButton;
        private Button selectPreviewButton;
        private VisualElement speciesInspector;
        private VisualElement previewInspector;

        #endregion

        #region 窗口

        [MenuItem("工具/程序生长植物/海洋植物生成器")]
        public static void OpenWindow()
        {
            MarineGeneratorWindow m = GetWindow<MarineGeneratorWindow>("海洋植物生成器");
            m.titleContent = new GUIContent("海洋植物生成器");
        }

        private void OnEnable()
        {
            Selection.selectionChanged += HandleSelectionChanged;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= HandleSelectionChanged;
        }

        public void CreateGUI()
        {

            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            DrawHeader();

            VisualElement body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.width = Length.Percent(100);
            body.style.marginTop = 8;
            root.Add(body);


            DrawResourcePanel(body);
            // DrawPreviewPanel(body);
            DrawInspectorPanel(body);

            statusLabel = new Label();
            statusLabel.style.marginTop = 6;
            root.Add(statusLabel);

            HandleSelectionChanged();
            RefreshControls();

        }

        private void DrawHeader()
        {
            Label title = new Label("程序海洋植物生成器");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 16;
            rootVisualElement.Add(title);
        }

        private void DrawResourcePanel(VisualElement body)
        {
            VisualElement leftColumn = new VisualElement();
            leftColumn.style.flexGrow = 1;
            leftColumn.style.flexBasis = 0;
            leftColumn.style.minWidth = 300;
            leftColumn.style.flexShrink = 1;
            leftColumn.style.marginRight = 12;
            body.Add(leftColumn);

            VisualElement panel = CreatePanel(0);
            leftColumn.Add(panel);

            panel.Add(CreateTitle("资源"));

            speciesField = new ObjectField("物种配置")
            {
                objectType = typeof(MarineSpeciesSO),
                allowSceneObjects = false,
            };
            speciesField.RegisterValueChangedCallback(evt =>
            {
                species = evt.newValue as MarineSpeciesSO;
                if (species != null)
                {
                    seed = species.defaultSeed;
                }

                RefreshControls();
            });
            panel.Add(speciesField);

            parentField = new ObjectField("父节点")
            {
                objectType = typeof(Transform),
                allowSceneObjects = true,
            };
            parentField.RegisterValueChangedCallback(evt =>
            {
                parent = evt.newValue as Transform;
            });
            panel.Add(parentField);

            VisualElement assetRow = CreateRow();
            panel.Add(assetRow);
            assetRow.Add(new Button(CreateSpeciesAsset) { text = "新建物种" });
            assetRow.Add(new Button(CreateRuleAsset) { text = "新建规则" });
            assetRow.Add(new Button(CreatePackAsset) { text = "新建素材包" });

            Button sampleButton = new Button(CreateSampleAndRefresh) { text = "创建验收样例" };
            sampleButton.style.marginTop = 8;
            panel.Add(sampleButton);

            Button validationButton = new Button(CreateDualValidationAndRefresh) { text = "验证 LSystem + Spline" };
            validationButton.style.marginTop = 4;
            panel.Add(validationButton);

            VisualElement previewPanel = CreatePanel(0);
            previewPanel.style.marginTop = 14;
            leftColumn.Add(previewPanel);
            DrawPreviewPanel(previewPanel);
        }

        private void DrawPreviewPanel(VisualElement body)
        {
            body.Add(CreateTitle("生成"));

            seedField = new IntegerField("固定 Seed");
            seedField.RegisterValueChangedCallback(evt =>
            {
                seed = evt.newValue;
            });
            body.Add(seedField);

            growthSlider = new Slider("生长进度", 0f, 1f);
            growthSlider.RegisterValueChangedCallback(evt =>
            {
                growth01 = Mathf.Clamp01(evt.newValue);
                if (preview != null)
                {
                    Undo.RecordObject(preview, "修改程序植物生长进度");
                    preview.growth01 = growth01;
                    preview.Generate();
                    EditorUtility.SetDirty(preview);
                }
            });
            body.Add(growthSlider);

            VisualElement buttonRow = CreateRow();
            body.Add(buttonRow);

            generateButton = new Button(GeneratePreview) { text = "生成预览" };
            refreshButton = new Button(RefreshSelected) { text = "刷新选中" };
            selectPreviewButton = new Button(SelectPreview) { text = "定位预览" };
            exportButton = new Button(ExportPrefab) { text = "导出 Prefab" };
            buttonRow.Add(generateButton);
            buttonRow.Add(refreshButton);
            buttonRow.Add(selectPreviewButton);
            buttonRow.Add(exportButton);

            previewLabel = new Label();
            previewLabel.style.marginTop = 8;
            body.Add(previewLabel);
        }

        private void DrawInspectorPanel(VisualElement body)
        {
            VisualElement panel = CreatePanel(0);
            panel.style.flexGrow = 1;
            panel.style.flexBasis = 0;
            panel.style.minWidth = 360;
            body.Add(panel);

            panel.Add(CreateTitle("检查器"));

            ScrollView scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            panel.Add(scrollView);

            speciesInspector = new VisualElement();
            previewInspector = new VisualElement();
            scrollView.Add(CreateTitle("物种"));
            scrollView.Add(speciesInspector);
            scrollView.Add(CreateTitle("预览组件"));
            scrollView.Add(previewInspector);
        }

        #endregion

        #region 操作

        private void GeneratePreview()
        {
            if (species == null)
            {
                SetStatus("请先选择物种配置。", MessageType.Warning);
                return;
            }

            GameObject root = new GameObject(species != null ? $"PGP_{species.displayName}" : "PGP_MarinePlant");
            Undo.RegisterCreatedObjectUndo(root, "生成程序海洋植物预览");
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            preview = Undo.AddComponent<MarinePlantPreview>(root);
            preview.species = species;
            preview.seed = seed;
            preview.growth01 = growth01;
            preview.Generate();
            EditorUtility.SetDirty(preview);
            Selection.activeGameObject = root;
            SetStatus("已生成场景预览。", MessageType.Info);
            RefreshControls();
        }

        private void RefreshSelected()
        {
            MarinePlantPreview selected = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<MarinePlantPreview>()
                : null;

            if (selected == null)
            {
                GeneratePreview();
                return;
            }

            Undo.RecordObject(selected, "刷新程序海洋植物预览");
            preview = selected;
            preview.species = species;
            preview.seed = seed;
            preview.growth01 = growth01;
            preview.Generate();
            EditorUtility.SetDirty(preview);
            SetStatus("已刷新选中的预览组件。", MessageType.Info);
            RefreshControls();
        }

        private void SelectPreview()
        {
            if (preview == null)
            {
                SetStatus("当前没有可定位的预览对象。", MessageType.Warning);
                return;
            }

            Selection.activeObject = preview.gameObject;
            EditorGUIUtility.PingObject(preview.gameObject);
        }

        private void ExportPrefab()
        {
            if (preview == null)
            {
                SetStatus("请先生成或选择一个预览对象。", MessageType.Warning);
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "导出程序植物 Prefab",
                preview.name,
                "prefab",
                "选择 Prefab 保存位置");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            PrefabUtility.SaveAsPrefabAssetAndConnect(preview.gameObject, path, InteractionMode.UserAction);
            AssetDatabase.SaveAssets();
            SetStatus($"已导出 Prefab：{path}", MessageType.Info);
        }

        private void CreateSpeciesAsset()
        {
            MarineSpeciesSO asset = CreateAsset<MarineSpeciesSO>("新建物种配置", "MarineSpeciesSO.asset");
            if (asset == null)
            {
                return;
            }

            asset.defaultSeed = seed;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            species = asset;
            RefreshControls();
        }

        private void CreateRuleAsset()
        {
            MarineGrowthRuleSO asset = CreateAsset<MarineGrowthRuleSO>("新建生长规则", "MarineGrowthRuleSO.asset");
            if (asset == null)
            {
                return;
            }

            if (species != null)
            {
                Undo.RecordObject(species, "绑定程序植物生长规则");
                species.growthRule = asset;
                EditorUtility.SetDirty(species);
                AssetDatabase.SaveAssets();
            }

            RefreshControls();
        }

        private void CreatePackAsset()
        {
            MarineSpritePackSO asset = CreateAsset<MarineSpritePackSO>("新建素材包", "MarineSpritePackSO.asset");
            if (asset == null)
            {
                return;
            }

            if (species != null)
            {
                Undo.RecordObject(species, "绑定程序植物素材包");
                species.spritePack = asset;
                EditorUtility.SetDirty(species);
                AssetDatabase.SaveAssets();
            }

            RefreshControls();
        }

        private void CreateSampleAndRefresh()
        {
            MarineSampleUtility.CreateAcceptanceSample();
            HandleSelectionChanged();
            SetStatus("已创建验收样例并开始播放生长。", MessageType.Info);
        }

        private void CreateDualValidationAndRefresh()
        {
            MarineSampleUtility.CreateLSystemAndSplineValidation();
            HandleSelectionChanged();
            SetStatus("已创建 LSystem 珊瑚和 Spline 长海草验证样例。", MessageType.Info);
        }

        private T CreateAsset<T>(string title, string fileName) where T : ScriptableObject
        {
            string path = EditorUtility.SaveFilePanelInProject(title, fileName, "asset", "选择资源保存位置");
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            T asset = CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            SetStatus($"已创建资源：{path}", MessageType.Info);
            return asset;
        }

        #endregion

        #region 刷新

        private void HandleSelectionChanged()
        {
            MarinePlantPreview selected = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<MarinePlantPreview>()
                : null;

            if (selected != null)
            {
                preview = selected;
                species = selected.species;
                seed = selected.seed;
                growth01 = selected.growth01;
            }

            RefreshControls();
        }

        private void RefreshControls()
        {
            if (speciesField == null)
            {
                return;
            }

            speciesField.SetValueWithoutNotify(species);
            parentField.SetValueWithoutNotify(parent);
            seedField.SetValueWithoutNotify(seed);
            growthSlider.SetValueWithoutNotify(growth01);

            bool hasSpecies = species != null;
            bool hasPreview = preview != null;
            generateButton.SetEnabled(hasSpecies);
            refreshButton.SetEnabled(hasSpecies);
            selectPreviewButton.SetEnabled(hasPreview);
            exportButton.SetEnabled(hasPreview);

            previewLabel.text = hasPreview
                ? $"当前预览：{preview.name}"
                : "当前预览：未选择";

            RebuildInspectors();
        }

        private void RebuildInspectors()
        {
            speciesInspector.Clear();
            previewInspector.Clear();

            if (species != null)
            {
                speciesInspector.Add(new InspectorElement(species));
            }
            else
            {
                speciesInspector.Add(new Label("未选择物种资源。"));
            }

            if (preview != null)
            {
                previewInspector.Add(new InspectorElement(preview));
            }
            else
            {
                previewInspector.Add(new Label("未选择预览组件。"));
            }
        }

        private void SetStatus(string message, MessageType type)
        {
            if (statusLabel == null)
            {
                return;
            }

            statusLabel.text = message;
            statusLabel.style.color = type == MessageType.Warning
                ? new Color(1f, 0.72f, 0.3f)
                : new Color(0.72f, 0.9f, 1f);
        }

        #endregion

        #region 样式

        private static VisualElement CreatePanel(float width)
        {
            VisualElement panel = new VisualElement();
            panel.style.paddingLeft = 10;
            panel.style.paddingRight = 10;
            panel.style.paddingTop = 10;
            panel.style.paddingBottom = 10;
            panel.style.backgroundColor = new Color(0.17f, 0.17f, 0.17f);
            panel.style.borderLeftWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderTopWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderTopLeftRadius = 2;
            panel.style.borderTopRightRadius = 2;
            panel.style.borderBottomLeftRadius = 2;
            panel.style.borderBottomRightRadius = 2;
            // panel.style.borderLeftColor = new Color(0.34f, 0.34f, 0.34f);
            // panel.style.borderRightColor = new Color(0.34f, 0.34f, 0.34f);
            // panel.style.borderTopColor = new Color(0.46f, 0.46f, 0.46f);
            // panel.style.borderBottomColor = new Color(0.11f, 0.11f, 0.11f);
            panel.style.flexShrink = 0;

            if (width > 0f)
            {
                panel.style.width = width;
            }

            return panel;
        }

        private static Label CreateTitle(string text)
        {
            Label label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 12;
            label.style.color = new Color(0.88f, 0.88f, 0.88f);
            label.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            label.style.paddingLeft = 6;
            label.style.paddingRight = 6;
            label.style.paddingTop = 3;
            label.style.paddingBottom = 3;
            label.style.marginLeft = -4;
            label.style.marginRight = -4;
            label.style.marginTop = -4;
            label.style.marginBottom = 8;
            label.style.borderBottomWidth = 1;
            label.style.borderBottomColor = new Color(0.08f, 0.08f, 0.08f);
            return label;
        }

        private static VisualElement CreateRow()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 8;
            row.style.marginBottom = 4;
            return row;
        }

        #endregion
    }
}
