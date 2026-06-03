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

        // 引数 (int / float / string が boxing された配列).
        public object[] Args { get; }

        #endregion

        #region 初期化

        public OscMessage(string address, string typeTags, object[] args)
        {
            Address = address;
            TypeTags = typeTags;
            Args = args ?? Array.Empty<object>();
        }

        #endregion
    }
}
