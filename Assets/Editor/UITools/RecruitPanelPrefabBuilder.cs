using JN.Client.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace JN.Client.EditorTools
{
    /// <summary>
    /// 在编辑器中重建招聘面板的静态节点，避免运行时通过代码创建 UI。
    /// </summary>
    public static class RecruitPanelPrefabBuilder
    {
        private const string RecruitPanelPath = "Assets/Res/Prefabs/UI/Runtime/RecruitPanel.prefab";
        private const string FrameSpritePath = "Assets/Res/Textures/UI/Panel/Recruit/frame.png";
        private const string ChefPortraitPath = "Assets/Res/Textures/UI/Panel/Recruit/1.9.png";
        private const string WaiterPortraitPath = "Assets/Res/Textures/UI/Panel/Recruit/xiaoer1.png";

        /// <summary>
        /// 提供手动重建入口，仅在明确需要时重建招聘列表基础结构。
        /// </summary>
        [MenuItem("JiangNan/UI/重建招聘面板")]
        public static void BuildRecruitPanelPrefab()
        {
            var root = CreateRoot();
            CreatePanel(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, RecruitPanelPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RecruitPanelPrefabBuilder] RecruitPanel.prefab 已重建为页签列表结构。");
        }

        /// <summary>
        /// 判断当前 prefab 是否缺少旧版招聘列表结构。
        /// 当前项目已允许美术自由调整 RecruitPanel，不再自动触发该判断。
        /// </summary>
        /// <returns>缺少结构时返回 true。</returns>
        private static bool NeedsRebuild()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RecruitPanelPath);
            if (prefab == null)
            {
                return true;
            }

            return prefab.transform.Find("Panel/group_Tabs/btn_Chef") == null
                   || prefab.transform.Find("Panel/group_Tabs/btn_Waiter") == null
                   || prefab.transform.Find("Panel/group_List/item_1/btn_Recruit") == null
                   || prefab.transform.Find("Panel/group_Single/btn_Confirm") == null;
        }

        /// <summary>
        /// 创建全屏遮罩根节点。
        /// </summary>
        /// <returns>招聘面板根节点。</returns>
        private static GameObject CreateRoot()
        {
            var root = new GameObject("RecruitPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(RuntimePrefabCoroutineHost));
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            var image = root.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);
            image.raycastTarget = true;
            return root;
        }

        /// <summary>
        /// 创建中央招聘面板以及所有静态子节点。
        /// </summary>
        /// <param name="root">根节点。</param>
        private static void CreatePanel(Transform root)
        {
            var panel = CreateImage("Panel", root, new Vector2(980f, 900f), new Vector2(0f, -40f), LoadSprite(FrameSpritePath), Color.white);
            panel.GetComponent<Image>().raycastTarget = false;

            CreateTitle(panel.transform);
            CreateCloseButton(panel.transform);
            CreateTabs(panel.transform);
            CreateList(panel.transform);
            CreateSingleGroup(panel.transform);
        }

        /// <summary>
        /// 创建标题区域。
        /// </summary>
        /// <param name="panel">面板节点。</param>
        private static void CreateTitle(Transform panel)
        {
            CreateText("txt_Title", panel, "招聘员工", new Vector2(500f, 80f), new Vector2(0f, 370f), 50f, new Color(0.95f, 0.86f, 0.58f, 1f));
        }

        /// <summary>
        /// 创建关闭按钮。
        /// </summary>
        /// <param name="panel">面板节点。</param>
        private static void CreateCloseButton(Transform panel)
        {
            var button = CreateButton("btn_Close", panel, "X", new Vector2(70f, 70f), new Vector2(420f, 360f));
            var text = button.transform.Find("txt_Label")?.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                text.fontSize = 32f;
            }
        }

        /// <summary>
        /// 创建厨师和小二页签。
        /// </summary>
        /// <param name="panel">面板节点。</param>
        private static void CreateTabs(Transform panel)
        {
            var tabs = CreateEmpty("group_Tabs", panel, new Vector2(760f, 90f), new Vector2(0f, 275f));
            CreateButton("btn_Chef", tabs.transform, "厨师 0/3", new Vector2(330f, 74f), new Vector2(-190f, 0f));
            CreateButton("btn_Waiter", tabs.transform, "小二 0/3", new Vector2(330f, 74f), new Vector2(190f, 0f));
        }

        /// <summary>
        /// 创建招聘列表的三个固定槽位。
        /// </summary>
        /// <param name="panel">面板节点。</param>
        private static void CreateList(Transform panel)
        {
            var list = CreateEmpty("group_List", panel, new Vector2(820f, 570f), new Vector2(0f, -30f));
            for (var index = 0; index < 3; index++)
            {
                CreateRecruitRow(list.transform, index + 1, new Vector2(0f, 190f - (index * 190f)));
            }
        }

        /// <summary>
        /// 创建单个候选人的招聘确认区域，用于掌柜等旧入口兼容。
        /// </summary>
        /// <param name="panel">面板节点。</param>
        private static void CreateSingleGroup(Transform panel)
        {
            var group = CreateEmpty("group_Single", panel, new Vector2(820f, 600f), new Vector2(0f, -45f));
            CreateImage("img_Portrait", group.transform, new Vector2(310f, 420f), new Vector2(-240f, 45f), LoadSprite(WaiterPortraitPath), Color.white);
            CreateText("txt_Name", group.transform, "掌\n柜", new Vector2(90f, 330f), new Vector2(-390f, -80f), 34f, new Color(0.08f, 0.05f, 0.02f, 1f));
            CreateText("txt_Role", group.transform, "人员类型：掌柜", new Vector2(430f, 72f), new Vector2(170f, 160f), 34f, new Color(0.12f, 0.07f, 0.03f, 1f));
            CreateText("txt_Cost", group.transform, "招募花费：0", new Vector2(430f, 72f), new Vector2(170f, 40f), 34f, new Color(0.18f, 0.1f, 0.03f, 1f));
            CreateButton("btn_Confirm", group.transform, "确认招募", new Vector2(360f, 92f), new Vector2(170f, -135f));
        }

        /// <summary>
        /// 创建一个招聘列表槽位。
        /// </summary>
        /// <param name="parent">列表父节点。</param>
        /// <param name="index">槽位序号。</param>
        /// <param name="position">槽位位置。</param>
        private static void CreateRecruitRow(Transform parent, int index, Vector2 position)
        {
            var row = CreateEmpty($"item_{index}", parent, new Vector2(800f, 170f), position);
            CreateImage("img_Bg", row.transform, new Vector2(800f, 165f), Vector2.zero, null, new Color(1f, 0.94f, 0.78f, 0.95f));
            CreateImage("img_Portrait", row.transform, new Vector2(116f, 132f), new Vector2(-310f, 0f), index == 1 ? LoadSprite(ChefPortraitPath) : LoadSprite(WaiterPortraitPath), Color.white);
            CreateText("txt_Name", row.transform, $"厨师{index}", new Vector2(180f, 54f), new Vector2(-165f, 34f), 30f, new Color(0.12f, 0.07f, 0.03f, 1f));
            CreateText("txt_Status", row.transform, "未招募", new Vector2(180f, 48f), new Vector2(-165f, -34f), 26f, new Color(0.5f, 0.18f, 0.08f, 1f));
            CreateText("txt_Cost", row.transform, "招聘价格：0", new Vector2(250f, 60f), new Vector2(80f, 0f), 28f, new Color(0.18f, 0.1f, 0.03f, 1f));
            CreateButton("btn_Recruit", row.transform, "0", new Vector2(170f, 74f), new Vector2(295f, 0f));
        }

        /// <summary>
        /// 创建空的矩形容器。
        /// </summary>
        /// <param name="name">节点名。</param>
        /// <param name="parent">父节点。</param>
        /// <param name="size">尺寸。</param>
        /// <param name="position">位置。</param>
        /// <returns>创建后的节点。</returns>
        private static GameObject CreateEmpty(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            SetupRect(obj.GetComponent<RectTransform>(), size, position);
            return obj;
        }

        /// <summary>
        /// 创建图片节点。
        /// </summary>
        /// <param name="name">节点名。</param>
        /// <param name="parent">父节点。</param>
        /// <param name="size">尺寸。</param>
        /// <param name="position">位置。</param>
        /// <param name="sprite">图片资源。</param>
        /// <param name="color">颜色。</param>
        /// <returns>创建后的节点。</returns>
        private static GameObject CreateImage(string name, Transform parent, Vector2 size, Vector2 position, Sprite sprite, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            SetupRect(obj.GetComponent<RectTransform>(), size, position);

            var image = obj.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = true;
            image.preserveAspect = sprite != null && name.Contains("Portrait");
            return obj;
        }

        /// <summary>
        /// 创建按钮节点。
        /// </summary>
        /// <param name="name">节点名。</param>
        /// <param name="parent">父节点。</param>
        /// <param name="label">按钮文本。</param>
        /// <param name="size">尺寸。</param>
        /// <param name="position">位置。</param>
        /// <returns>按钮节点。</returns>
        private static GameObject CreateButton(string name, Transform parent, string label, Vector2 size, Vector2 position)
        {
            var obj = CreateImage(name, parent, size, position, null, new Color(0.92f, 0.68f, 0.35f, 1f));
            var button = obj.AddComponent<Button>();
            button.targetGraphic = obj.GetComponent<Image>();
            CreateText("txt_Label", obj.transform, label, size, Vector2.zero, 28f, new Color(0.14f, 0.08f, 0.02f, 1f));
            return obj;
        }

        /// <summary>
        /// 创建 TextMeshProUGUI 文本。
        /// </summary>
        /// <param name="name">节点名。</param>
        /// <param name="parent">父节点。</param>
        /// <param name="content">文本内容。</param>
        /// <param name="size">尺寸。</param>
        /// <param name="position">位置。</param>
        /// <param name="fontSize">字号。</param>
        /// <param name="color">颜色。</param>
        /// <returns>文本节点。</returns>
        private static TextMeshProUGUI CreateText(string name, Transform parent, string content, Vector2 size, Vector2 position, float fontSize, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            SetupRect(obj.GetComponent<RectTransform>(), size, position);

            var text = obj.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// 设置 UI 节点的中心锚点矩形。
        /// </summary>
        /// <param name="rect">矩形组件。</param>
        /// <param name="size">尺寸。</param>
        /// <param name="position">位置。</param>
        private static void SetupRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// 加载图片资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>图片资源。</returns>
        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
