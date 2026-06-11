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

    // UI の型タグ列 (例 "iif") と値 (空白区切り) から OSC 引数配列を生成する. 型タグが空なら引数なし.
    // 対応型: i(int) h(int64) f(float) d(double) s(string) b(blob=hex) T(true) F(false).
    // T/F は型タグのみで値を消費しない. 文字列(s)は空白を含められない (1 トークン = 1 値).
    private object[] ParseArguments()
    {
        string tags = argTypeField != null ? argTypeField.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(tags))
        {
            return Array.Empty<object>();
        }

        string valueText = argValueField != null ? argValueField.text.Trim() : string.Empty;
        string[] tokens = valueText.Length == 0
            ? Array.Empty<string>()
            : valueText.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

        List<object> args = new List<object>(tags.Length);
        int tokenIndex = 0;

        foreach (char tag in tags)
        {
            switch (tag)
            {
                case 'T':
                    args.Add(true);
                    break;
                case 'F':
                    args.Add(false);
                    break;
                case 'i':
                    args.Add(int.Parse(NextToken(tokens, ref tokenIndex, tag), CultureInfo.InvariantCulture));
                    break;
                case 'h':
                    args.Add(long.Parse(NextToken(tokens, ref tokenIndex, tag), CultureInfo.InvariantCulture));
                    break;
                case 'f':
                    args.Add(float.Parse(NextToken(tokens, ref tokenIndex, tag), CultureInfo.InvariantCulture));
                    break;
                case 'd':
                    args.Add(double.Parse(NextToken(tokens, ref tokenIndex, tag), CultureInfo.InvariantCulture));
                    break;
                case 's':
                    args.Add(NextToken(tokens, ref tokenIndex, tag));
                    break;
                case 'b':
                    args.Add(HexToBytes(NextToken(tokens, ref tokenIndex, tag)));
                    break;
                default:
                    throw new FormatException("未対応の型タグです (使用可能: i/h/f/d/s/b/T/F): " + tag);
            }
        }

        if (tokenIndex < tokens.Length)
        {
            throw new FormatException(
                $"値の数が型タグより多いです (型タグ '{tags}' が必要とする値: {tokenIndex} 個, 入力値: {tokens.Length} 個).");
        }

        return args.ToArray();
    }

    // 値トークンを 1 つ取り出す. 不足していれば例外.
    private static string NextToken(string[] tokens, ref int index, char tag)
    {
        if (index >= tokens.Length)
        {
            throw new FormatException($"型タグ '{tag}' に対応する値が足りません. 値を空白区切りで指定してください.");
        }
        return tokens[index++];
    }

    // hex 文字列 (例 "DEADBEEF") を byte[] に変換する (blob 引数用).
    private static byte[] HexToBytes(string hex)
    {
        hex = hex.Replace(" ", string.Empty);
        if (hex.Length % 2 != 0)
        {
            throw new FormatException("blob(b) の hex は偶数桁で指定してください: " + hex);
        }

        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
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

        // 送信元 (受信時のみ). 未知仕様の解析に使う.
        if (!string.IsNullOrEmpty(message.Source))
        {
            builder.Append("from ").Append(message.Source).Append(' ');
        }

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
                builder.Append(FormatArg(message.Args[i]));
            }
            builder.Append(']');
        }

        // 未知の型タグ / データ不足で途中停止した場合の注記.
        if (!message.IsComplete)
        {
            builder.Append(" [未デコード: 未知の型タグ or データ不足]");
        }

        // 生バイト列 (受信時のみ). 仕様不明の内容も hex で確認できるようにする.
        if (message.Raw != null)
        {
            builder.Append(" hex=").Append(ToHex(message.Raw));
        }

        return builder.ToString();
    }

    // 引数 1 個を表示用文字列にする. blob(byte[]) は 0x + hex で表す.
    private string FormatArg(object arg)
    {
        if (arg is byte[] blob)
        {
            return "0x" + ToHex(blob);
        }
        return Convert.ToString(arg, CultureInfo.InvariantCulture);
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
