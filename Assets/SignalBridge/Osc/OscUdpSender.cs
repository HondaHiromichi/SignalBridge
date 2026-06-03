using System;
using System.Net.Sockets;

namespace SignalBridge.Osc
{
    // 生 UDP で OSC メッセージを送信する. UnityEngine 非依存.
    // 例外はそのまま呼び出し側へ伝播させ, ログ出力は利用側アプリの責務とする.
    public sealed class OscUdpSender : IDisposable
    {
        #region フィールド

        private readonly UdpClient client;
        private bool isDisposed;

        #endregion

        #region 初期化

        public OscUdpSender()
        {
            client = new UdpClient();
        }

        #endregion

        #region Public メソッド

        // 既に encode 済みのバイト列を送信し, 送信バイト数を返す.
        public int Send(string host, int port, byte[] data)
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(OscUdpSender));
            }
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return client.Send(data, data.Length, host, port);
        }

        // address と引数から OSC メッセージを生成して送信する.
        public int Send(string host, int port, string address, params object[] args)
        {
            byte[] data = MinimalOsc.Encode(address, args);
            return Send(host, port, data);
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
            client?.Dispose();
        }

        #endregion
    }
}
