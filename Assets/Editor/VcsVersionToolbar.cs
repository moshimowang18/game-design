using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AkiFramework.Editor
{
    /// <summary>
    /// 在 Unity 顶部工具栏显示版本信息。
    /// 默认插入到 Play、Pause、Step 左侧。
    /// </summary>
    [InitializeOnLoad]
    public static class VcsVersionToolbar
    {
        private const string ContainerName = "AniiTools-VcsVersion";
        private const string LeftZoneName = "ToolbarZoneLeftAlign";
        private const string ToolbarTypeName = "UnityEditor.Toolbar";
        private const string RootFieldName = "m_Root";

        private static readonly Color LatestColor = new(0f, 1f, 0f);
        private static readonly Color OutdatedColor = new(1f, 0.62f, 0.2f);
        private static readonly Color UnknownColor = new(0.7f, 0.7f, 0.7f);

        private static ScriptableObject _toolbar;
        private static Label _versionLabel;
        private static double _nextRefreshTime;
        private static string _cachedText = "版本: --";
        private static string _cachedTooltip = "版本: --";
        private static Color _cachedColor = UnknownColor;

        static VcsVersionToolbar()
        {
            RefreshCache();
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            if (_toolbar == null)
            {
                TryAttachToToolbar();
            }

            if (EditorApplication.timeSinceStartup < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = EditorApplication.timeSinceStartup + 10d;
            RefreshVersionLabel();
        }

        private static void TryAttachToToolbar()
        {
            var toolbarType = typeof(UnityEditor.Editor).Assembly.GetType(ToolbarTypeName);
            if (toolbarType == null)
            {
                return;
            }

            var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
            if (toolbars == null || toolbars.Length == 0)
            {
                return;
            }

            _toolbar = toolbars[0] as ScriptableObject;
            if (_toolbar == null)
            {
                return;
            }

            var rootField = toolbarType.GetField(RootFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            var root = rootField?.GetValue(_toolbar) as VisualElement;
            if (root == null)
            {
                _toolbar = null;
                return;
            }

            var leftZone = root.Q(LeftZoneName);
            if (leftZone == null)
            {
                _toolbar = null;
                return;
            }

            var exists = leftZone.Q(ContainerName);
            if (exists != null)
            {
                _versionLabel = exists.Q<Label>();
                ApplyLabelState();
                return;
            }

            var container = new VisualElement
            {
                name = ContainerName
            };
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginLeft = 8;
            container.style.marginRight = 8;
            container.style.unityTextAlign = TextAnchor.MiddleLeft;

            _versionLabel = new Label();
            _versionLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            _versionLabel.style.color = LatestColor;
            _versionLabel.style.fontSize = 12;
            _versionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _versionLabel.style.paddingLeft = 6;
            _versionLabel.style.paddingRight = 6;
            _versionLabel.style.borderLeftWidth = 1;
            _versionLabel.style.borderRightWidth = 1;
            _versionLabel.style.borderLeftColor = new Color(1f, 1f, 1f, 0.12f);
            _versionLabel.style.borderRightColor = new Color(1f, 1f, 1f, 0.12f);
            _versionLabel.pickingMode = PickingMode.Ignore;

            container.Add(_versionLabel);
            leftZone.Add(container);
            ApplyLabelState();
        }

        private static void RefreshVersionLabel()
        {
            RefreshCache();
            ApplyLabelState();
        }

        private static void RefreshCache()
        {
            var projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
            var info = ProjectInfoProvider.GetVcsVersionInfo(projectRoot);
            _cachedText = info.DisplayText;
            _cachedTooltip = info.Tooltip;
            _cachedColor = ResolveColor(info);
        }

        private static void ApplyLabelState()
        {
            if (_versionLabel == null)
            {
                return;
            }

            _versionLabel.text = _cachedText;
            _versionLabel.tooltip = _cachedTooltip;
            _versionLabel.style.color = _cachedColor;
        }

        private static Color ResolveColor(VcsVersionInfo info)
        {
            if (info == null || info == VcsVersionInfo.Unknown)
            {
                return UnknownColor;
            }

            return info.IsBehindLatest ? OutdatedColor : LatestColor;
        }
    }
}
