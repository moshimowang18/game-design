using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JN.Client.Model;

namespace JN.Client.UI
{
    /// <summary>
    /// 挂在3D小二GameObject上。仅在 EmployeeData.IsResting 时显示头顶 zzz 气泡 + 叫醒按钮。
    /// </summary>
    public class EmployeeRestBubble : MonoBehaviour
    {
        private EmployeeData _emp;
        private GameObject _canvasGO;
        private TextMeshProUGUI _txtZzz;
        private Button _btnWake;
        private float _zzzAnimTimer;

        public void Bind(EmployeeData emp)
        {
            _emp = emp;
            EnsureCanvas();
        }

        private void EnsureCanvas()
        {
            if (_canvasGO != null) return;

            _canvasGO = new GameObject("RestBubble_Canvas");
            _canvasGO.transform.SetParent(transform, false);
            _canvasGO.transform.localPosition = new Vector3(0, 2.2f, 0);

            var canvas = _canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            _canvasGO.AddComponent<CanvasScaler>();
            _canvasGO.AddComponent<GraphicRaycaster>();

            var canvasRT = _canvasGO.GetComponent<RectTransform>();
            canvasRT.sizeDelta = new Vector2(200, 80);
            canvasRT.localScale = Vector3.one * 0.01f;

            // zzz 文字
            var zzzGO = new GameObject("txt_Zzz");
            zzzGO.transform.SetParent(_canvasGO.transform, false);
            _txtZzz = zzzGO.AddComponent<TextMeshProUGUI>();
            _txtZzz.text = "💤 Zzz...";
            _txtZzz.fontSize = 32;
            _txtZzz.color = new Color(1f, 0.95f, 0.3f);
            _txtZzz.alignment = TextAlignmentOptions.Center;
            _txtZzz.fontStyle = FontStyles.Bold;
            var zzzRT = zzzGO.GetComponent<RectTransform>();
            zzzRT.anchorMin = new Vector2(0, 0.5f);
            zzzRT.anchorMax = new Vector2(1, 1f);
            zzzRT.offsetMin = Vector2.zero;
            zzzRT.offsetMax = Vector2.zero;

            // 叫醒按钮
            var btnGO = new GameObject("btn_Wake");
            btnGO.transform.SetParent(_canvasGO.transform, false);
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.95f, 0.55f, 0.2f, 0.95f);
            _btnWake = btnGO.AddComponent<Button>();
            _btnWake.onClick.AddListener(OnClickWake);

            var btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0, 0);
            btnRT.anchorMax = new Vector2(1, 0.5f);
            btnRT.offsetMin = Vector2.zero;
            btnRT.offsetMax = Vector2.zero;

            var labelGO = new GameObject("txt_Label");
            labelGO.transform.SetParent(btnGO.transform, false);
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = "叫醒";
            label.fontSize = 28;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            _canvasGO.SetActive(false);
        }

        private void Update()
        {
            if (_emp == null || _canvasGO == null) return;

            bool shouldShow = _emp.IsResting;
            if (_canvasGO.activeSelf != shouldShow)
                _canvasGO.SetActive(shouldShow);

            if (!shouldShow) return;

            var cam = Camera.main;
            if (cam != null)
            {
                _canvasGO.transform.rotation = Quaternion.LookRotation(
                    _canvasGO.transform.position - cam.transform.position);
            }

            _zzzAnimTimer += Time.deltaTime;
            if (_txtZzz != null)
            {
                var rt = _txtZzz.rectTransform;
                var pos = rt.anchoredPosition;
                pos.y = Mathf.Sin(_zzzAnimTimer * 2f) * 4f;
                rt.anchoredPosition = pos;
            }
        }

        private void OnClickWake()
        {
            if (_emp == null || !_emp.IsResting) return;
            _emp.IsResting = false;
            _emp.Stamina = Mathf.Max(_emp.Stamina, 1);
            _emp.KickedFromRest = true;
            Debug.Log($"[Employee] 玩家叫醒了 {_emp.Name}（体力={_emp.Stamina}, 下次30%犯错）");
        }

        private void OnDestroy()
        {
            if (_canvasGO != null) Destroy(_canvasGO);
        }
    }
}
