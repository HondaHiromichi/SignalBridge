# SignalBridge

Unity と外部システム (MRMC Flair 等) を信号で橋渡しする汎用連携プロジェクト.
現状のコアは **OSC 通信による疎通確認 (connectivity verification) デモ** です.

## 概要

Unity クライアントアプリから MRMC Flair へ信号 (OSC) を送り, Flair 側に事前定義した
ロボットアームのムーブ (プリセット) を起動する構成の疎通確認を行います.

```
[Unity クライアント] --(信号 / OSC)--> [Flair PC] --(EtherCAT 等)--> [ロボットアーム]
                                          ↑ 事前にムーブ (Job/Move) をロード済み
```

ゴールは「Unity のボタン -> Flair でプリセット起動 -> アームが動く」が通ること.
起動コマンドの実仕様 (OSC アドレス等) は未確定のため, **設定値 (IP / Port / OSC Address / 引数) を
すべて UI から可変** にして実装し, 仕様確定後に値を差し替えるだけで実機接続できる状態を目指します.

## 動作環境

| 項目 | 内容 |
|------|------|
| Unity | **6000.4.9f1 (Unity 6.4)** |
| Render Pipeline | Built-in |
| 対応 OS | Windows / macOS |
| 通信方式 | OSC (UDP) |

## セットアップ

1. Unity Hub に Unity `6000.4.9f1` をインストール.
2. このリポジトリをクローンし, Unity Hub から本フォルダをプロジェクトとして開く.
3. (UI 実装に必要) Package Manager で `com.unity.ugui` を追加.

```sh
git clone <repository-url> SignalBridge
```

> `Library/` などの生成物は `.gitignore` 済みのため, 初回オープン時に Unity が再生成します.
> Windows / Mac 間の改行コード差分は `.gitattributes` で正規化しています.

## 実装フェーズ

- **Phase 0** — ローカルループバック: Unity の OSC 送受信を Python (`python-osc`) エコーで検証 (Flair 不要).
- **Phase 1** — 実機 Flair へ送信し, ムーブ起動でアームが動けば本疎通 OK.
- **Phase 2 (任意)** — Flair の Data Output (OSC XYZ) を Unity で受信して返り路を確認.

### Phase 0 用 Python OSC エコーサーバ

```python
# pip install python-osc
from pythonosc.dispatcher import Dispatcher
from pythonosc.osc_server import BlockingOSCUDPServer

def handler(addr, *args):
    print(f"received: {addr} {args}")

disp = Dispatcher()
disp.set_default_handler(handler)
server = BlockingOSCUDPServer(("0.0.0.0", 7001), disp)
print("listening on udp/7001 ...")
server.serve_forever()
```

## コード構成

```
Assets/SignalBridge/
├─ Osc/                       OSC 送受信 (現状のコア)
│   ├─ OscConnectionTester.cs   UI 仲介 + ログ表示
│   ├─ OscUdpSender.cs          送信
│   ├─ OscUdpListener.cs        受信 (別スレッド + ConcurrentQueue)
│   └─ MinimalOsc.cs            OSC encode/decode ヘルパ
├─ Flair/                     (将来) Flair API 連携
└─ Watch/                     (将来) フォルダ監視
```

## ネットワーク注意点

- Flair PC の NIC が INtime 割り当ての場合は使用不可 -> Windows 側アダプタを使う.
- Unity マシンは Auxiliary 側ネットワーク (VLAN) 推奨. Flair PC とルーティングが通ること.
- ファイアウォールで宛先ポート (例 7001) の UDP を許可.
- 疎通前に `ping` で Flair PC へ到達できることを確認.

## 開発状況

- [x] Unity 6.4 プロジェクト作成
- [ ] OSC スクリプト群の実装 (`Assets/SignalBridge/Osc/`)
- [ ] Phase 0 (ローカルループバック検証)
- [ ] Phase 1 (実機 Flair 送信) — OSC アドレス仕様の確定待ち
