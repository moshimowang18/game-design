using DG.Tweening;
using JN.Client.Manager;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace JN.Client.UI
{
    public class CreatePlayerPanelControllerData : UIPanelData
    {
    }

    /// <summary>
    /// 负责创建玩家相关的运行时逻辑。
    /// </summary>
    public class CreatePlayerPanelController : QFrameworkPanel<CreatePlayerPanelControllerData>
    {
        private static readonly string[] RandomSurnames =
        {
            "赵", "钱", "孙", "李", "周", "吴", "郑", "王",
            "冯", "陈", "褚", "卫", "蒋", "沈", "韩", "杨",
            "朱", "秦", "尤", "许", "何", "吕", "施", "张"
        };

        private static readonly string[] RandomGivenNames =
        {
            "子轩", "雨桐", "若景", "景然", "子墨", "若汐", "明轩", "沐阳",
            "星河", "清风", "云起", "晨曦", "青岚", "南风", "知夏", "听澜",
            "千雪", "安然", "书瑶", "亦凡", "嘉树", "一诺", "若云", "天佑"
        };

        [SerializeField] private Button btn_CreatePlayer;
        [SerializeField] private TMP_InputField input_PlayerName;
        [SerializeField] private Button btn_suiji;
        [SerializeField] private GameObject warningPrefab;

        /// <summary>
        /// 面板初始化时绑定控件和事件。
        /// </summary>
        protected override void OnPanelInit()
        {
            if (btn_CreatePlayer != null)
            {
                btn_CreatePlayer.onClick.AddListener(OnClickBtnCreatePlayer);
            }

            if (btn_suiji != null)
            {
                btn_suiji.onClick.AddListener(OnClickBtnRandomPlayerName);
            }
        }

        /// <summary>
        /// 面板打开时读取数据并刷新显示。
        /// </summary>
        /// <param name="data">数据。</param>
        protected override void OnPanelOpen(CreatePlayerPanelControllerData data)
        {
            if (input_PlayerName == null)
            {
                return;
            }

            var lastPlayerName = DataManager.Instance.PlayerData != null
                ? DataManager.Instance.PlayerData.playerName
                : string.Empty;
            input_PlayerName.text = lastPlayerName ?? string.Empty;
            input_PlayerName.MoveTextEnd(false);
        }

        /// <summary>
        /// 面板关闭时清理临时状态和监听。
        /// </summary>
        protected override void OnPanelClose()
        {
            if (btn_CreatePlayer != null)
            {
                btn_CreatePlayer.onClick.RemoveListener(OnClickBtnCreatePlayer);
            }

            if (btn_suiji != null)
            {
                btn_suiji.onClick.RemoveListener(OnClickBtnRandomPlayerName);
            }
        }

        /// <summary>
        /// 处理按钮创建玩家点击事件。
        /// </summary>
        private void OnClickBtnCreatePlayer()
        {
            var playerName = input_PlayerName != null ? input_PlayerName.text : string.Empty;
            if (string.IsNullOrWhiteSpace(playerName))
            {
                SpawnWarning("请输入玩家名字", transform);
                return;
            }

            if (!DataManager.Instance.LoginOrCreatePlayer(playerName))
            {
                SpawnWarning("创建玩家失败", transform);
                return;
            }

            EnterTown();
        }

        /// <summary>
        /// 使用本地玩家档案进入大地图。
        /// </summary>
        private void EnterTown()
        {
            StartCoroutine(GameManager.Instance.LoadSceneAsync("Town", () =>
            {
                CloseSelf();
                UIKit.OpenPanel<TownStatusBarPanelController>(UILevel.Common);

                if (DataManager.Instance.ShouldShowOpeningLoanWindow())
                {
                    UIKit.OpenPanel<LoanWindowController>(UILevel.PopUI);
                }
            }));
        }

        /// <summary>
        /// 处理按钮随机玩家名点击事件。
        /// </summary>
        private void OnClickBtnRandomPlayerName()
        {
            if (input_PlayerName == null)
            {
                return;
            }

            var surname = RandomSurnames[Random.Range(0, RandomSurnames.Length)];
            var givenName = RandomGivenNames[Random.Range(0, RandomGivenNames.Length)];
            input_PlayerName.text = surname + givenName;
            input_PlayerName.MoveTextEnd(false);
        }

        private void SpawnWarning(string text, Transform parent, GameObject obj = null, bool isRed = true)
        {
            var warning = Instantiate(warningPrefab, parent);
            warning.GetComponent<TMP_Text>().text = text;

            if (!isRed)
            {
                warning.GetComponent<TMP_Text>().color = Color.white;
            }

            var currentPos = warning.transform.position;
            if (obj == null)
            {
                warning.transform.position = new Vector3(currentPos.x + 25, currentPos.y + 45, currentPos.z);
            }
            else
            {
                var pos = Camera.main.WorldToScreenPoint(obj.transform.position);
                warning.transform.position = new Vector3(pos.x + 25, pos.y + 45, pos.z);
            }

            var newPos = warning.transform.position;

            warning.transform.DOMove(new Vector3(newPos.x + 45f, newPos.y + 65f, newPos.z), 1.5f).SetEase(Ease.InQuad);
            var canvas = warning.GetComponent<CanvasGroup>();
            canvas.DOFade(0f, 1f).SetDelay(.5f);
            Destroy(warning.gameObject, 2f);
        }
    }
}
