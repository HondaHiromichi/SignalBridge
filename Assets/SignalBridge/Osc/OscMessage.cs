using System;

namespace SignalBridge.Osc
{
    // 受信した 1 件の OSC メッセージを表す不変データ.
    public readonly struct OscMessage
    {
        #region プロパティ

        // OSC アドレスパターン (例: "/ping").
        public string Address { get; }

        // 型タグ文字列 (先頭の ',' を含む. 例: ",ifs").
        public string TypeTags { get; }

        // デコードできた引数 (int/long/float/double/string/bool/byte[] が boxing された配列).
        // 未知の型タグやデータ不足で途中停止した場合, 型タグ数より少ないことがある.
        public object[] Args { get; }

        // 受信した生バイト列 (送信用 Encode 経由で生成した場合は null). 解析不能時の hex 表示に使う.
        public byte[] Raw { get; }

        // 送信元エンドポイント "ip:port" (受信時のみ設定. 送信/テストでは null).
        public string Source { get; }

        // 全引数を型タグどおりにデコードできたか. 未知の型タグ/データ不足で途中停止した場合は false.
        public bool IsComplete { get; }

        #endregion

        #region 初期化

        // 送信側やテストで使う簡易コンストラクタ (Raw/Source なし, 完全デコード扱い).
        public OscMessage(string address, string typeTags, object[] args)
            : this(address, typeTags, args, null, null, true)
        {
        }

        public OscMessage(string address, string typeTags, object[] args, byte[] raw, string source, bool isComplete)
        {
            Address = address;
            TypeTags = typeTags;
            Args = args ?? Array.Empty<object>();
            Raw = raw;
            Source = source;
            IsComplete = isComplete;
        }

        #endregion

        #region Public メソッド

        // 送信元エンドポイントを付与した複製を返す (受信側で利用).
        public OscMessage WithSource(string source)
        {
            return new OscMessage(Address, TypeTags, Args, Raw, source, IsComplete);
        }

        #endregion
    }
}
