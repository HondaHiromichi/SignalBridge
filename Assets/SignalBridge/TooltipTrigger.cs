using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// セクション見出し等に付与し, 一定時間ホバーし続けたら共有 Tooltip に解説を表示する.
// 対象は raycastTarget が ON の Graphic (Text 等) であること.
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    #region 定数

    // この秒数だけ範囲内に留まってから表示する (即時表示でのチラつきを防ぐ).
    private const float HoverDelaySeconds = 0.5f;

    #endregion

    #region SerializeField

    [SerializeField] private string title;
    [SerializeField] [TextArea(2, 6)] private string body;

    #endregion

    #region フィールド

    private Coroutine pendingShow;

    #endregion

    #region ライフサイクル

    private void OnDisable()
    {
        CancelPending();
        if (Tooltip.Instance != null)
        {
            Tooltip.Instance.Hide();
        }
    }

    #endregion

    #region ハンドリング

    public void OnPointerEnter(PointerEventData eventData)
    {
        CancelPending();
        pendingShow = StartCoroutine(ShowAfterDelay());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CancelPending();
        if (Tooltip.Instance != null)
        {
            Tooltip.Instance.Hide();
        }
    }

    #endregion

    #region Private メソッド

    // 遅延後にまだホバー継続中なら表示する (途中で離れたら OnPointerExit がコルーチンを止める).
    private IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSecondsRealtime(HoverDelaySeconds);
        pendingShow = null;
        if (Tooltip.Instance != null)
        {
            Tooltip.Instance.Show(title, body);
        }
    }

    private void CancelPending()
    {
        if (pendingShow != null)
        {
            StopCoroutine(pendingShow);
            pendingShow = null;
        }
    }

    #endregion
}
