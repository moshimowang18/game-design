using JN.Client;
using JN.Client.Manager;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JN.Client.UI
{
    public class LoanWindowControllerData : UIPanelData
    {
    }

    /// <summary>
    /// 负责贷款相关的运行时逻辑。
    /// </summary>
    public class LoanWindowController : QFrameworkPanel<LoanWindowControllerData>
    {
        [SerializeField] private Button btn_Loan;
        [SerializeField] private TextMeshProUGUI txt_LoanNum;

        /// <summary>
        /// 面板初始化时绑定控件和事件。
        /// </summary>
        protected override void OnPanelInit()
        {
            CacheReferences();
            btn_Loan?.onClick.AddListener(OnClickBtnLoan);
            RefreshLoanAmount();
        }

        /// <summary>
        /// 面板关闭时清理临时状态和监听。
        /// </summary>
        protected override void OnPanelClose()
        {
            btn_Loan?.onClick.RemoveListener(OnClickBtnLoan);
        }

        /// <summary>
        /// 处理按钮贷款点击事件。
        /// </summary>
        private void OnClickBtnLoan()
        {
            if (!DataManager.Instance.TryTakeLoan(out _))
            {
                return;
            }

            GameUIEffects.PlayCoinsFly(btn_Loan.transform, GOReferenceManager.Instance.GetCoinTransform());
            CloseSelf();
        }

        /// <summary>
        /// 缓存界面引用。
        /// </summary>
        private void CacheReferences()
        {
            if (txt_LoanNum == null)
            {
                txt_LoanNum = transform.Find("group_Main/@btn_Loan/txt_LoanNum")?.GetComponent<TextMeshProUGUI>();
            }
        }

        /// <summary>
        /// 刷新贷款金额。
        /// </summary>
        private void RefreshLoanAmount()
        {
            if (txt_LoanNum == null)
            {
                return;
            }

            txt_LoanNum.text = DataManager.Instance.GetNextLoanAmount().ToString();
        }
    }
}
