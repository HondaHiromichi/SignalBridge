using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// セクション見出しのホバーで表示する共有ツールチップ. カーソルに追従し, 画面内に収まるようクランプする.
// TooltipTrigger から Show/Hide を呼ばれる. 1 シーンに 1 つ配置する想定 (static Instance で参照).
public class Tooltip : MonoBehaviour
{
    #region 定数

    private const float CursorOffsetX = 18f;
    private const float CursorOffsetY = -18f;
    private const float ScreenPadding = 12f;

    #endregion

    #region SerializeField

    [SerializeField] private RectTransform panel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text bodyText;
    [SerializeField] private Canvas rootCanvas;

    #endregion

    #region フィールド

    private static Tooltip instance;
    private RectTransform canvasRect;

    #endregion

    #region プロパティ

    public static Tooltip Instance => instance;

    #endregion

    #region ライフサイクル

    private void Awake()
    {
        instance = this;
        if (rootCanvas != null)
        {
            canvasRect = rootCanvas.GetComponent<RectTransform>();
        }
        Hide();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void LateUpdate()
    {
        if (panel != null && panel.gameObject.activeSelf)
        {
            FollowCursor();
        }
    }

    #endregion

    #region Public メソッド

    // 指定の見出し/本文を表示し, カーソル位置へ移動する.
    public void Show(string title, string body)
    {
        if (titleText != null)
        {
            titleText.text = title;
        }
        if (bodyText != null)
        {
            bodyText.text = body;
        }
        if (panel != null)
        {
            panel.gameObject.SetActive(true);
            // 文字量に応じてサイズを確定させてから位置を計算する.
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            FollowCursor();
        }
    }

    public void Hide()
    {
        if (panel != null)
        {
            panel.gameObject.SetActive(false);
        }
    }

    #endregion

    #region Private メソッド

    // カーソル位置 (右下) へツールチップを置き, 画面外へはみ出さないようクランプする.
    private void FollowCursor()
    {
        if (canvasRect == null)
        {
            return;
        }

        Vector2 screen = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
        {
            return;
        }

        Vector2 pos = local + new Vector2(CursorOffsetX, CursorOffsetY);

        // panel のピボットは (0, 1) 前提. 右と下にはみ出すならクランプする.
        Vector2 size = panel.rect.size;
        float halfW = canvasRect.rect.width * 0.5f;
        float halfH = canvasRect.rect.height * 0.5f;
        pos.x = Mathf.Clamp(pos.x, -halfW + ScreenPadding, halfW - size.x - ScreenPadding);
        pos.y = Mathf.Clamp(pos.y, -halfH + size.y + ScreenPadding, halfH - ScreenPadding);

        panel.anchoredPosition = pos;
    }

    #endregion
}
