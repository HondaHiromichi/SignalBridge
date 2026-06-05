#!/usr/bin/env python3
"""SignalBridge Phase 0 用 OSC エコーサーバ.

Unity から受信した OSC メッセージを別ポートへそのまま送り返す (echo).
Flair 抜きで Unity <-> 別プロセス間の UDP/OSC 疎通を検証するためのツール.
本番 (Phase 1) では不要になるが, 回帰確認や切り分け用に保持する.

依存: python-osc (tools/requirements.txt 参照. tools/.venv 推奨)

使い方 (デフォルト: 9000 で受け, 9001 へ返す):
    python osc_echo.py
    python osc_echo.py --listen-port 9000 --reply-host 127.0.0.1 --reply-port 9001

Unity 側 UI 設定:
    宛先 IP   = 127.0.0.1
    宛先 Port = listen-port (9000)   <- ここへ Unity が送る
    受信 Port = reply-port  (9001)   <- ここへ本サーバが返す. Listen を ON
送受で別ポートにするのは, 同一 PC でポート衝突を避けるため.
"""
import argparse
from datetime import datetime

from pythonosc.dispatcher import Dispatcher
from pythonosc.osc_server import BlockingOSCUDPServer
from pythonosc.udp_client import SimpleUDPClient


def main():
    parser = argparse.ArgumentParser(description="OSC echo server (SignalBridge Phase 0 loopback)")
    parser.add_argument("--listen-host", default="127.0.0.1", help="待ち受けホスト (既定 127.0.0.1)")
    parser.add_argument("--listen-port", type=int, default=9000, help="待ち受けポート (既定 9000)")
    parser.add_argument("--reply-host", default="127.0.0.1", help="返送先ホスト (既定 127.0.0.1)")
    parser.add_argument("--reply-port", type=int, default=9001, help="返送先ポート (既定 9001)")
    args = parser.parse_args()

    reply_client = SimpleUDPClient(args.reply_host, args.reply_port)

    def echo_handler(address, *osc_args):
        ts = datetime.now().strftime("%H:%M:%S.%f")[:-3]
        values = list(osc_args)
        print(f"[{ts}] RECV {address} {values} -> echo to {args.reply_host}:{args.reply_port}", flush=True)
        # 受け取った address と引数をそのまま送り返す.
        reply_client.send_message(address, values)

    dispatcher = Dispatcher()
    dispatcher.set_default_handler(echo_handler)

    server = BlockingOSCUDPServer((args.listen_host, args.listen_port), dispatcher)
    print(
        f"OSC echo server: listening on {args.listen_host}:{args.listen_port}, "
        f"echoing to {args.reply_host}:{args.reply_port}",
        flush=True,
    )
    print("Ctrl+C to stop.", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nstopped.", flush=True)


if __name__ == "__main__":
    main()
