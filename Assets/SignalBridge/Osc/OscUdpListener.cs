using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SignalBridge.Osc
{
    // 指定ポートで OSC メッセージを受信する. 受信はブロッキングのため別スレッドで回し,
    // 受信結果は ConcurrentQueue に積む (UnityEngine 非依存).
    // 利用側は Update 等のメインスレッドで TryDequeue して取り出す.
    public sealed class OscUdpListener : IDisposable
    {
        #region フィールド

        private readonly ConcurrentQueue<OscMessage> received = new ConcurrentQueue<OscMessage>();
        private UdpClient client;
        private Thread thread;
        private volatile bool isListening;

        #endregion

        #region イベント

        // 受信スレッド上で発生した例外を通知する.
        // 注意: このコールバックは受信スレッドから呼ばれる. UnityEngine API を触る場合は
        // 利用側でメインスレッドへマーシャリングすること.
        public event Action<Exception> OnError;

        #endregion

        #region プロパティ

        // 受信待ち受け中かどうか.
        public bool IsListening => isListening;

        // 待ち受け中のポート (未待ち受け時は 0).
        public int Port { get; private set; }

        #endregion

        #region Public メソッド

        // 指定ポートで待ち受けを開始する.
        public void Start(int port)
        {
            if (isListening)
            {
                Stop();
            }

            client = new UdpClient(port);
            Port = port;
            isListening = true;

            thread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "OscUdpListener"
            };
            thread.Start();
        }

        // 待ち受けを停止する.
        public void Stop()
        {
            isListening = false;

            // Receive のブロッキングを解除するため client を閉じる.
            client?.Close();
            client = null;

            if (thread != null && thread.IsAlive)
            {
                thread.Join(500);
            }
            thread = null;
            Port = 0;
        }

        // 受信済みメッセージを 1 件取り出す. 取り出せた場合 true.
        public bool TryDequeue(out OscMessage message)
        {
            return received.TryDequeue(out message);
        }

        public void Dispose()
        {
            Stop();
        }

        #endregion

        #region Private メソッド

        // 受信スレッド本体. client が閉じられるまでブロッキング受信を繰り返す.
        private void ReceiveLoop()
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

            while (isListening)
            {
                try
                {
                    byte[] data = client.Receive(ref remote);
                    OscMessage message = MinimalOsc.Decode(data, data.Length);
                    received.Enqueue(message);
                }
                catch (Exception ex)
                {
                    // Stop による正常な切断時は通知しない.
                    if (!isListening)
                    {
                        break;
                    }
                    OnError?.Invoke(ex);
                }
            }
        }

        #endregion
    }
}
