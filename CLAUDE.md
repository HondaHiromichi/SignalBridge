# SignalBridge — プロジェクト規約 / コンテキスト

> このファイルは Claude Code がプロジェクト作業時に参照する指示書です.
> コーディング規約の詳細はグローバル `~/.claude/CLAUDE.md` の
> 「Unity C# コーディング規約」「Unity 一般ルール」「Git ワークフロー」に従い,
> ここではプロジェクト固有の前提のみを記述します.

## プロジェクト概要

Unity と外部システム (MRMC Flair 等) を信号で橋渡しする汎用連携プロジェクト.
現状のコアは **OSC (Open Sound Control) 通信** による疎通確認 (connectivity verification) デモ.
将来的に Flair API 連携・フォルダ監視のモジュールを追加する前提で構成する.

```
[Unity クライアント] --(信号 / OSC)--> [Flair PC] --(EtherCAT 等)--> [ロボットアーム]
```

最終ゴール: 「Unity のボタン -> Flair でプリセット (Job/Move) 起動 -> アームが動く」が通ること.

## 環境

- Unity **6000.4.9f1 (Unity 6.4)** / URP (Universal Render Pipeline, `com.unity.render-pipelines.universal` 17.4.0)
- 対応 OS: **Windows / macOS 両方** (開発環境として両対応)
- バージョン管理: Git (改行コードは `.gitattributes` で正規化)

## 実装方針 (段階的に疎通確認)

- **Phase 0**: ローカルループバック. Unity の OSC 送受信を Python (`python-osc`) エコーで検証 (Flair 不要).
- **Phase 1**: 実機 Flair へ送信し, ムーブ起動でアームが動けば本疎通 OK.
- **Phase 2 (任意)**: Flair の Data Output (OSC XYZ) を Unity で受信し返り路を確認.

> 重要: ムーブ起動の OSC アドレス文字列・引数・受信側設定は **未確定** (購入元 / メーカー確認待ち).
> よって **送信先 IP / Port / OSC Address / 引数を全て UI から可変** にして実装し,
> 仕様が確定したら値を差し替えるだけで実機に繋がる状態を目指す.

## コード構成 (1 ファイル 1 MonoBehaviour, namespace なし)

namespace は使わず, 機能ごとのフォルダで分ける. 分離が必要なら asmdef をフォルダ単位で置く.

```
Assets/SignalBridge/
├─ Osc/                       現状のコア (OSC 送受信)
│   ├─ OscConnectionTester.cs   UI 仲介 + ログ表示 (ライフサイクル, ハンドリング)
│   ├─ OscUdpSender.cs          送信 (UdpClient.Send + OSC エンコード)
│   ├─ OscUdpListener.cs        受信 (別スレッド + ConcurrentQueue)
│   └─ MinimalOsc.cs            OSC encode/decode の static ヘルパ
├─ Flair/                     (将来) Flair API 連携
│   └─ FlairApiClient.cs
└─ Watch/                     (将来) フォルダ監視
    └─ FolderWatcher.cs
```

## 技術メモ

- 外部アセットに依存せず `System.Net.Sockets.UdpClient` で生 UDP + 最小 OSC エンコードを自前実装する.
- OSC は UDP ベース. 送信メッセージ構造:
  1. Address Pattern (ASCII, null 終端, 4 バイト境界まで null パディング)
  2. Type Tag String (先頭 `,` + 型 `i`/`f`/`s`, 同じく null 終端 + 4 バイトパディング)
  3. Arguments (int32 / float32 は **ビッグエンディアン** 4 バイト, string は OSC 文字列形式)
- `UdpClient` の受信はブロッキングのため別スレッドで回し, 受信データは `ConcurrentQueue` に積んで
  `Update()` で取り出して UI 反映する (Unity API はメインスレッド限定).

## UI (疎通確認に必要十分)

- InputField: 宛先 IP / 宛先 Port / OSC Address / 引数 (型 + 値)
- Button: Send / Toggle: Listen (受信ポート指定)
- ScrollView + Text: 送受信ログ (タイムスタンプ + 送信バイト数 (hex) / 受信内容 / 例外)
- uGUI を使用するため `com.unity.ugui` パッケージが必要 (未導入なら追加する).

## 参照資料 (リポジトリ外 / ローカル管理)

以下はリポジトリには含めないローカル資料. 各自のローカル環境を参照すること.

- 引き継ぎ資料: `HANDOFF_Unity-Flair-OSC-Demo.md`
- 確認質問リスト: `Flair連携_OSC_API_確認質問リスト.pdf`
- 元マニュアル: Flair v7.4 Operator's Manual / Flair Virtual Production Sync Box QSG (MRMC-2283)
