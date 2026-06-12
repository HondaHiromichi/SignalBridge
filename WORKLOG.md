# SignalBridge 作業マップ (WORKLOG)

> 別マシンでの作業再開用ドキュメント. セッションをまたぐ作業状況・設計方針・次タスクを記録する.
> 最終更新: 2026-06-11 (dev ブランチ)

---

## 次のタスク: Phase 1 (実機 Flair 送信) — OSC 仕様確定待ち

> **Step B (自己ループバック) は 2026-06-04, Step C (Phase 0 プロセス間ループバック) は 2026-06-05 に
> いずれも Play で確認済み.** 下記「動作確認手順」「Step C 検証手順」は再確認用に残す.
> 次は Phase 1 (実機 Flair) だが, ムーブ起動の OSC 仕様が未確定 (メーカー確認待ち) のため保留中.

### 事前準備 (別マシン)
1. `git pull` で `origin/dev` を取得 (本作業は dev ブランチ. main ではない)
2. Unity **6000.4.9f1 (Unity 6.4)** でプロジェクトを開く
3. 初回オープン時, パッケージ解決やアセット移行で多少待つことがある (正常)
   - 注意: Unity MCP (Claude の操作支援) を使う場合のみ, そのマシンに `uv`/`uvx` が必要
     + Claude Code 再起動が要る. **動作確認自体は Unity エディタ単体で手動実行できる** (MCP 不要)

### 動作確認手順 (python 不要. Unity が自分宛に送って自分で受ける)
1. シーン `Assets/Scenes/OscDemo.unity` を開く (起動時に自動で開く設定済み)
2. **Play** ボタンを押す
3. UI の **`Listen` トグルを ON** -> ログに `LISTEN start :9000`
4. **`Send` ボタン**を押す (127.0.0.1:9000 へ `/ping` int 1 を送信)
5. 期待されるログ (2026-06-04 の実測値):
   - `SEND -> 127.0.0.1:9000 /ping (16 bytes) hex=2f70696e670000002c69000000000001`
   - `RECV /ping ,i args=[1]` <- 送った内容が受信・デコードされて返る
6. `Listen` を OFF にして Play 停止

-> これで encode -> UDP 送信 -> 別スレッド受信 -> decode -> メインスレッド反映 の一連が確認できる.
   うまくいかない場合はログの内容 (例外メッセージ等) を確認する.

### Step C 検証手順 (python-osc エコー, 2026-06-05 確認済み)
プロセス間ループバック. エコーサーバ `tools/osc_echo.py` (python-osc) が別プロセスとして待受け,
Unity の送信を解釈して送り返す. 同一 PC でポート衝突を避けるため送受で別ポートにする.
1. venv 準備 (初回のみ): `python -m venv tools/.venv` 後 `tools/requirements.txt` を pip install
2. エコーサーバ起動: `tools/.venv/Scripts/python tools/osc_echo.py` (既定: 9000 受信 / 9001 返送)
3. Unity Play -> 宛先 Port `9000` / 受信 Port `9001` / Listen ON -> Send
4. 期待: Unity 側 `RECV /ping ,i args=[1]`, サーバ側 `RECV /ping [1] -> echo to 127.0.0.1:9001`
-> Unity -> 別プロセス -> Unity の往復 + 第三者実装 (python-osc) が自作エンコードを解釈できることを確認.

### Phase 1 (実機 Flair) について
- ムーブ起動の OSC アドレス/引数が未確定 (メーカー確認待ち) のため保留. 情報が揃い次第,
  UI の 宛先 IP/Port/Address/引数 を実機値に差し替えるだけで接続できる設計.

---

## 進捗マップ

| Step | 内容 | 状態 |
|------|------|------|
| 環境 | Unity 6.4 (URP) / Unity MCP 導入 / dev ブランチ運用 / 不要パッケージ整理 | 完了 |
| A | 再利用 OSC コア (encode/decode + UDP 送受信) + EditMode テスト | 完了 (テスト 4 件 pass) |
| B | UI 仲介 (OscConnectionTester) + uGUI シーン (OscDemo) | 完了 (自己ループバック疎通を Play で確認. SEND/RECV 一致, 2026-06-04) |
| C | Phase 0 ループバック (python-osc エコー) | 完了 (tools/osc_echo.py で別プロセス疎通を確認. 両側ログ一致, 2026-06-05) |
| 汎用化1 | OSC テスター汎用化 第1弾 (拡張型 i/h/f/d/s/b/T/F, 寛容デコード, 送信元付与, 複数引数 UI) | 完了 (テスト 9 件 pass, `iif` `1 2 3.5` のエコーループバックを Play で確認, 2026-06-11) |
| 汎用化2a | プリセット保存/呼び出し (送信設定を PlayerPrefs に永続化, 右カラムに名前欄+Dropdown+Save/Load/Delete) | 完了 (Play で save/load/delete を確認, 2026-06-11) |
| 汎用化2b | raw hex 送信 (エンコーダを介さず hex 入力をそのままバイト列で送信) | 完了 (`/ping` int1 相当の hex を送り echo が OSC として解釈, 2026-06-11). これで汎用化は一区切り |
| UI刷新 | デザイン刷新 (Figma 案 B v2: ダーク=コンソール風, 左サイドバー操作 + 右大型ログ) を uGUI 実装 | 完了 (基準 1920x1080, アンカーでレスポンシブ, ログ色分け RECV緑/ERROR赤, Main Camera 背景を単色ダーク化, 2026-06-12). タブ/フィルタ/Clear/ステータスピルは見た目のみ (未配線) |
| Phase 1 | 実機 Flair 送信でアーム起動 | 未着手 (OSC 仕様未確定で保留) |

---

## 重要な設計方針・決定事項 (今回のセッションで確定)

### ブランチ運用
- 個人プロジェクトのため **`dev` を常用作業ブランチ**にし, 全変更を dev へコミットする.
  feature ブランチには小分けしない. `main` には直接コミットしない.

### 再利用可能な OSC モジュール設計 (重要)
- 通信機能は **他プロジェクトへ移植しやすいモジュール**として設計する, という要望が前提.
- `Assets/SignalBridge/Osc/` を **独立アセンブリ (asmdef)** 化し, `noEngineReferences: true` で
  **UnityEngine 非依存をコンパイル時に強制** (Debug.Log すら書けない). 他プロジェクトへは
  Osc/ フォルダごとコピーで導入できる.
- モジュール側のみ **namespace `SignalBridge.Osc`** を付与 (型名衝突回避. グローバル規約の
  「namespace 不使用」からの意図的な例外).
- **アプリ固有グルー (UI 仲介の MonoBehaviour) は Osc/ の外** (`Assets/SignalBridge/` 直下,
  Assembly-CSharp) に置く. これは UnityEngine/uGUI を使うため noEngineReferences の Osc/ には
  入れられない. namespace なし (グローバル規約どおり).

### OSC エンコード仕様 (自前実装)
- 外部アセットに依存せず `System.Net.Sockets.UdpClient` + 最小 OSC エンコードを自前実装.
- int32/float32 は **ビッグエンディアン 4 バイト**, 文字列は ASCII + null 終端 + 4 バイト境界
  パディング. 型タグは先頭 `,` + `i`/`f`/`s`.
- 受信はブロッキングのため別スレッドで回し, `ConcurrentQueue` に積んで `Update()` で
  メインスレッド反映 (Unity API はメインスレッド限定).
- (汎用化 第1弾, 2026-06-11) 型を拡張: `i`(int32)/`h`(int64)/`f`(float32)/`d`(float64)/
  `s`(string)/`b`(blob)/`T`/`F`(bool). 受信デコードを寛容化し, 未知の型タグ/データ不足でも
  例外を投げず, そこまでの引数 + `IsComplete=false` で返す (`OscMessage` に `Raw`/`Source`/
  `IsComplete` を追加, 受信時は送信元 `ip:port` を付与). UI は型タグ列 (例 `iif`) + 空白区切りの
  値で複数引数を送信可能. 仕様未確定の Flair 応答を落とさず観察するための汎用化.

### UI
- **legacy uGUI** (`UnityEngine.UI` の InputField/Text/Button/Toggle/ScrollRect) を使用
  (TMP 依存を増やさない).
- **新 Input System 環境** (`activeInputHandler: 1`) のため, EventSystem は
  **`InputSystemUIInputModule`** を使う (StandaloneInputModule だとクリックが効かない).

### シーンファイルの churn 対策 (重要)
- uGUI シーンは, ScrollRect/ContentSizeFitter/LayoutGroup の **駆動値が保存ごとに再計算・直列**
  され, マシン (解像度) や保存タイミングで非決定的に変わる "churn" が出やすい.
- 対策として OscDemo では以下を設定済み:
  - **CanvasScaler = Scale With Screen Size** (基準 800x600, Match 0.5). Constant Pixel Size だと
    Game ビュー解像度に直結し, 別マシンで開くたびに全 UI レイアウトが変わるため.
  - **ScrollRect の Scrollbar Visibility = AutoHide** (AutoHideAndExpandViewport ではない).
    Viewport を動的拡張するとスクロールバー矩形やつまみサイズが churn するため.
- 上記で「開くだけ」では差分ゼロ, 再保存しても駆動値が安定 (固定点に収束) することを確認済み.

### コミット運用
- Unity が自動生成・移行するファイル (URP アセットのバージョン移行, SceneTemplateSettings 等)
  は, 機能の成果物とは **別コミット**に分離する (履歴を読みやすく保つため).
- コミットタイトルは英語のみ (Add:/Fix:/Update:/Refactor:), 本文は日本語.

---

## ファイル構成 (現状)

```
Assets/
├─ Scenes/
│   └─ OscDemo.unity            疎通確認シーン (Canvas + uGUI + 配線済み. ビルド/起動シーン)
└─ SignalBridge/
    ├─ OscConnectionTester.cs   UI 仲介 MonoBehaviour (Assembly-CSharp, namespace なし)
    ├─ OscPreset.cs             送信設定プリセット 1 件分のデータ ([Serializable])
    ├─ OscPresetStore.cs        プリセットを PlayerPrefs に JSON 保存/読出する static ヘルパ
    └─ Osc/                     再利用コア (asmdef, noEngineReferences, namespace SignalBridge.Osc)
        ├─ SignalBridge.Osc.asmdef
        ├─ MinimalOsc.cs        encode/decode の static ヘルパ
        ├─ OscMessage.cs        受信メッセージの不変データ
        ├─ OscUdpSender.cs      UDP 送信 (plain class, IDisposable)
        ├─ OscUdpListener.cs    別スレッド受信 + ConcurrentQueue, TryDequeue/OnError
        └─ Tests/
            ├─ SignalBridge.Osc.Tests.asmdef
            └─ MinimalOscTests.cs   EditMode テスト 9 件
```

> シーンは `Assets/Scenes/OscDemo.unity` に配置 (`Assets/Scenes/` は OscDemo のみ).
> デフォルトの SampleScene は削除済み. アプリ層のコードと再利用コアは `Assets/SignalBridge/` 配下.

検証ツール (リポジトリ直下 `tools/`, Unity 外):
```
tools/
├─ osc_echo.py        Phase 0 用 OSC エコーサーバ (python-osc)
├─ requirements.txt   依存 (python-osc)
└─ .venv/             仮想環境 (gitignore. requirements.txt から再現)
```

OscDemo シーンの UI -> OscConnectionTester の SerializeField は配線済み
(IP/Port/Address/引数型/引数値/Send/Listen/受信 Port/ログ Text/ログ ScrollRect,
 + プリセット: 名前欄/Dropdown/Save/Load/Delete, + Raw 送信: hex 欄/Send Raw を右カラムに追加・配線済み).
デフォルト値: IP 127.0.0.1 / 送受信 Port とも 9000 / Address /ping / 引数 int 1.

---

## コミット履歴 (dev ブランチ. このセッション分)

```
Add: Initialize Unity 6.4 (URP) project ...   (前回)
Add: Integrate MCP for Unity (CoplayDev) package
Remove: Unused Unity Ads and In-App Purchasing packages
Update: Migrate URP project settings to material version 10
Fix: Correct render pipeline to URP in project CLAUDE.md
Add: Reusable OSC core module (encode/decode, UDP send/receive)   <- Step A
Add: OSC connection tester UI scene and mediator                 <- Step B
Update: Migrate URP pipeline assets to v13 and add scene template settings
Refactor: Relocate OscDemo scene to Assets/Scenes and fix uGUI scene churn
Add: OSC echo server (tools/osc_echo.py) for Phase 0 loopback verification   <- Step C
Docs: Fix stale README (URP render pipeline, code structure)
Update: Generalize OSC tester (extended types, lenient decode, multi-arg UI)   <- 汎用化 第1弾
Add: OSC send-config presets (save/load/delete via PlayerPrefs)              <- 汎用化 第2弾 (a)
Add: Raw hex OSC send (bypass encoder, send arbitrary bytes)                 <- 汎用化 第2弾 (b)
Update: Redesign OscDemo UI (dark console: sidebar + large log)              <- UI 刷新 (Figma 案 B v2)
```

---

## 未確定・要確認事項
- Flair のムーブ起動 OSC アドレス文字列・引数・受信側設定は **未確定** (購入元/メーカー確認待ち).
  確定したら UI の値を差し替えるだけで実機接続できる設計にしてある.
- 参照資料 (リポジトリ外, ローカル管理): `HANDOFF_Unity-Flair-OSC-Demo.md`,
  `Flair連携_OSC_API_確認質問リスト.pdf`, Flair v7.4 Operator's Manual ほか.
