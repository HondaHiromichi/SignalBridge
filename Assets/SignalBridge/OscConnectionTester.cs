using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using SignalBridge.Osc;

// OSC 疎通確認 UI とコアモジュール (OscUdpSender / OscUdpListener) を仲介する MonoBehaviour.
// UI からの入力を OSC メッセージに変換して送信し, 受信内容と例外をログ表示する.
// 受信は別スレッドのため Update でキューを汲んでメインスレッドで反映する.
public class OscConnectionTester : MonoBehaviour
{
    #region 定数

    private const int MaxLogLines = 200;
    private const string TimestampFormat = "HH:mm:ss.fff";

    #endregion

    #region SerializeField

    [Header("送信設定")]
    [SerializeField] private InputField destinationIpField;
    [SerializeField] private InputField destinationPortField;
    [SerializeField] private InputField oscAddressField;
    [SerializeField] private InputField argTypeField;
    [SerializeField] private InputField argValueField;
    [SerializeField] private Button sendButton;

    [Header("受信設定")]
    [SerializeField] private Toggle listenToggle;
    [SerializeField] private InputField listenPortField;

    [Header("ログ表示")]
    [SerializeField] private Text logText;
    [SerializeField] private ScrollRect logScrollRect;

    #endregion

    #region フィールド

    private OscUdpSender sender;
    private OscUdpListener listener;

    // 受信スレッドで発生した例外メッセージをメインスレッドへ渡すためのキュー.
    private readonly ConcurrentQueue<string> listenErrors = new ConcurrentQueue<string>();

    private readonly List<string> logLines = new List<string>();

    #endregion

    #region ライフサイクル

    private void Awake()
    {
        sender = new OscUdpSender();
        listener = new OscUdpListener();
        listener.OnError += OnListenerError;
    }

    private void OnEnable()
    {
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(OnSendClicked);
        }
        if (listenToggle != null)
        {
            listenToggle.onValueChanged.AddListener(OnListenToggled);
        }

        WarnIfUnassigned();
    }

    private void Update()
    {
        // 受信メッセージをメインスレッドで取り出してログ反映する.
        while (listener != null && listener.TryDequeue(out OscMessage message))
        {
            AppendLog("RECV " + FormatMessage(message));
        }

        // 受信スレッドの例外もメインスレッドで反映する.
        while (listenErrors.TryDequeue(out string error))
        {
            AppendLog("ERROR(listen): " + error);
        }
    }

    private void OnDisable()
    {
        if (sendButton != null)
        {
            sendButton.onClick.RemoveListener(OnSendClicked);
        }
        if (listenToggle != null)
        {
            listenToggle.onValueChanged.RemoveListener(OnListenToggled);
        }
    }

    private void OnDestroy()
    {
        if (listener != null)
        {
            listener.OnError -= OnListenerError;
            listener.Dispose();
            listener = null;
        }
        if (sender != null)
        {
            sender.Dispose();
            sender = null;
        }
    }

    #endregion

    #region ハンドリング

    // Send ボタン押下: UI 入力から OSC メッセージを組み立てて送信する.
    private void OnSendClicked()
    {
        try
        {
            string host = destinationIpField != null ? destinationIpField.text : string.Empty;
            int port = ParsePort(destinationPortField != null ? destinationPortField.text : string.Empty);
            string address = oscAddressField != null ? oscAddressField.text : string.Empty;
            object[] args = ParseArguments();

            byte[] data = MinimalOsc.Encode(address, args);
            int sent = sender.Send(host, port, data);

            AppendLog($"SEND -> {host}:{port} {address} ({sent} bytes) hex={ToHex(data)}");
        }
        catch (Exception ex)
        {
            AppendLog("ERROR(send): " + ex.Message);
        }
    }

    // Listen トグル切り替え: 指定ポートで受信開始 / 停止する.
    private void OnListenToggled(bool isOn)
    {
        try
        {
            if (isOn)
            {
                int port = ParsePort(listenPortField != null ? listenPortField.text : string.Empty);
                listener.Start(port);
                AppendLog($"LISTEN start :{port}");
            }
            else
            {
                listener.Stop();
                AppendLog("LISTEN stop");
            }
        }
        catch (Exception ex)
        {
            AppendLog("ERROR(listen): " + ex.Message);
        }
    }

    // 受信スレッド上の例外通知. メインスレッドへ渡すためキューに積むだけにする.
    private void OnListenerError(Exception ex)
    {
        listenErrors.Enqueue(ex.Message);
    }

    #endregion

    #region Private メソッド

    // UI の型 (i/f/s) と値から OSC 引数配列を生成する. 型が空なら引数なし.
    private object[] ParseArguments()
    {
        string type = argTypeField != null ? argTypeField.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(type))
        {
            return Array.Empty<object>();
        }

        string value = argValueField != null ? argValueField.text : string.Empty;

        switch (type)
        {
            case "i":
                return new object[] { int.Parse(value, CultureInfo.InvariantCulture) };
            case "f":
                return new object[] { float.Parse(value, CultureInfo.InvariantCulture) };
            case "s":
                return new object[] { value };
            default:
                throw new FormatException("引数の型は i / f / s のいずれかを指定してください: " + type);
        }
    }

    private int ParsePort(string text)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
        {
            throw new FormatException("ポート番号が不正です: " + text);
        }
        return port;
    }

    private string FormatMessage(OscMessage message)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(message.Address);
        builder.Append(' ');
        builder.Append(message.TypeTags);

        if (message.Args.Length > 0)
        {
            builder.Append(" args=[");
            for (int i = 0; i < message.Args.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(Convert.ToString(message.Args[i], CultureInfo.InvariantCulture));
            }
            builder.Append(']');
        }

        return builder.ToString();
    }

    private string ToHex(byte[] data)
    {
        StringBuilder builder = new StringBuilder(data.Length * 2);
        foreach (byte b in data)
        {
            builder.AppendFormat("{0:x2}", b);
        }
        return builder.ToString();
    }

    // タイムスタンプ付きで 1 行ログを追加し, 末尾を表示する.
    private void AppendLog(string line)
    {
        string timestamped = $"[{DateTime.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture)}] {line}";
        logLines.Add(timestamped);

        // 古い行を間引いて表示量を抑える.
        if (logLines.Count > MaxLogLines)
        {
            logLines.RemoveRange(0, logLines.Count - MaxLogLines);
        }

        if (logText != null)
        {
            logText.text = string.Join("\n", logLines);
        }

        // 末尾 (最新行) へスクロールする.
        if (logScrollRect != null)
        {
            logScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    // Inspector 参照の未設定を起動時に警告する (致命ではないため LogWarning).
    private void WarnIfUnassigned()
    {
        if (destinationIpField == null || destinationPortField == null || oscAddressField == null
            || sendButton == null || listenToggle == null || listenPortField == null || logText == null)
        {
            Debug.LogWarning("OscConnectionTester: Inspector の UI 参照が未設定です. シーン構築後にワイヤリングしてください.");
        }
    }

    #endregion
}
