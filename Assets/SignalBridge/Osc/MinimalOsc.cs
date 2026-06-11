using System;
using System.Collections.Generic;
using System.Text;

namespace SignalBridge.Osc
{
    // OSC メッセージの encode / decode を行う static ヘルパ.
    // UnityEngine に依存しない純 C# 実装 (他プロジェクトへ移植しやすくするため).
    // 対応型タグ: i(int32) h(int64) f(float32) d(float64) s(string) b(blob) T(true) F(false).
    public static class MinimalOsc
    {
        #region 定数

        private const char TagInt = 'i';
        private const char TagInt64 = 'h';
        private const char TagFloat = 'f';
        private const char TagDouble = 'd';
        private const char TagString = 's';
        private const char TagBlob = 'b';
        private const char TagTrue = 'T';
        private const char TagFalse = 'F';

        #endregion

        #region Public メソッド

        // address と引数から OSC メッセージのバイト列を生成する.
        // 対応する C# 型: int / long / float / double / string / byte[] / bool.
        public static byte[] Encode(string address, params object[] args)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentException("address が空です.", nameof(address));
            }

            args = args ?? Array.Empty<object>();

            List<byte> buffer = new List<byte>();

            // 1. Address Pattern
            WriteOscString(buffer, address);

            // 2. Type Tag String (先頭は ',')
            StringBuilder tags = new StringBuilder();
            tags.Append(',');
            foreach (object arg in args)
            {
                tags.Append(TypeTagOf(arg));
            }
            WriteOscString(buffer, tags.ToString());

            // 3. Arguments (T/F は型タグのみでデータ部を持たない)
            foreach (object arg in args)
            {
                WriteArgument(buffer, arg);
            }

            return buffer.ToArray();
        }

        // 受信した OSC バイト列を OscMessage へ復元する.
        // 未知の型タグやデータ不足に遭遇しても例外を投げず, そこまでの引数 + IsComplete=false で返す
        // (未知仕様の受信内容を落とさず観察するため).
        public static OscMessage Decode(byte[] data, int length)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            byte[] raw = new byte[length];
            Array.Copy(data, raw, length);

            int position = 0;
            string address = ReadOscString(data, length, ref position);
            string tags = ReadOscString(data, length, ref position);

            // 先頭の ',' を除いた型タグ分だけ引数を読む.
            int argCount = tags.Length > 0 && tags[0] == ',' ? tags.Length - 1 : 0;
            List<object> args = new List<object>(argCount);
            bool isComplete = true;

            for (int i = 0; i < argCount; i++)
            {
                char tag = tags[i + 1];
                if (!TryReadArgument(data, length, ref position, tag, out object value))
                {
                    // 未知の型タグはサイズが不明で以降を安全に読めないため, 途中停止する.
                    isComplete = false;
                    break;
                }
                args.Add(value);
            }

            return new OscMessage(address, tags, args.ToArray(), raw, null, isComplete);
        }

        #endregion

        #region Private メソッド (encode)

        private static char TypeTagOf(object arg)
        {
            switch (arg)
            {
                case int _:
                    return TagInt;
                case long _:
                    return TagInt64;
                case float _:
                    return TagFloat;
                case double _:
                    return TagDouble;
                case string _:
                    return TagString;
                case byte[] _:
                    return TagBlob;
                case bool b:
                    return b ? TagTrue : TagFalse;
                default:
                    throw new NotSupportedException("未対応の OSC 引数型です: " + (arg?.GetType().Name ?? "null"));
            }
        }

        private static void WriteArgument(List<byte> buffer, object arg)
        {
            switch (arg)
            {
                case int i:
                    WriteBigEndian(buffer, BitConverter.GetBytes(i));
                    break;
                case long h:
                    WriteBigEndian(buffer, BitConverter.GetBytes(h));
                    break;
                case float f:
                    WriteBigEndian(buffer, BitConverter.GetBytes(f));
                    break;
                case double d:
                    WriteBigEndian(buffer, BitConverter.GetBytes(d));
                    break;
                case string s:
                    WriteOscString(buffer, s);
                    break;
                case byte[] blob:
                    WriteBlob(buffer, blob);
                    break;
                case bool _:
                    // T / F は型タグのみ. データ部なし.
                    break;
                default:
                    throw new NotSupportedException("未対応の OSC 引数型です: " + (arg?.GetType().Name ?? "null"));
            }
        }

        // バイト列をビッグエンディアンで書き込む (実行環境がリトルエンディアンなら反転する). 4/8 バイト共用.
        private static void WriteBigEndian(List<byte> buffer, byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            buffer.AddRange(bytes);
        }

        // OSC blob を書き込む (int32 サイズ + データ + 4 バイト境界パディング).
        private static void WriteBlob(List<byte> buffer, byte[] blob)
        {
            WriteBigEndian(buffer, BitConverter.GetBytes(blob.Length));
            buffer.AddRange(blob);

            // blob は null 終端不要. 4 の倍数でなければ 0 埋めする.
            int padding = (4 - (blob.Length % 4)) % 4;
            for (int i = 0; i < padding; i++)
            {
                buffer.Add(0);
            }
        }

        // OSC 文字列を書き込む (ASCII + null 終端 + 4 バイト境界まで null パディング).
        private static void WriteOscString(List<byte> buffer, string value)
        {
            byte[] ascii = Encoding.ASCII.GetBytes(value);
            buffer.AddRange(ascii);

            // 最低 1 個の null 終端を含め, 全長が 4 の倍数になるまで null を足す.
            int padding = 4 - (ascii.Length % 4);
            for (int i = 0; i < padding; i++)
            {
                buffer.Add(0);
            }
        }

        #endregion

        #region Private メソッド (decode)

        // 1 引数を読み出す. 未知の型タグやデータ不足の場合は false を返す (例外を投げない).
        private static bool TryReadArgument(byte[] data, int length, ref int position, char tag, out object value)
        {
            value = null;
            switch (tag)
            {
                case TagInt:
                    if (!HasBytes(position, 4, length)) return false;
                    value = BitConverter.ToInt32(ReadBigEndian(data, ref position, 4), 0);
                    return true;
                case TagFloat:
                    if (!HasBytes(position, 4, length)) return false;
                    value = BitConverter.ToSingle(ReadBigEndian(data, ref position, 4), 0);
                    return true;
                case TagInt64:
                    if (!HasBytes(position, 8, length)) return false;
                    value = BitConverter.ToInt64(ReadBigEndian(data, ref position, 8), 0);
                    return true;
                case TagDouble:
                    if (!HasBytes(position, 8, length)) return false;
                    value = BitConverter.ToDouble(ReadBigEndian(data, ref position, 8), 0);
                    return true;
                case TagString:
                    value = ReadOscString(data, length, ref position);
                    return true;
                case TagBlob:
                    return TryReadBlob(data, length, ref position, out value);
                case TagTrue:
                    value = true;
                    return true;
                case TagFalse:
                    value = false;
                    return true;
                default:
                    // 未知の型タグ.
                    return false;
            }
        }

        private static bool TryReadBlob(byte[] data, int length, ref int position, out object value)
        {
            value = null;
            if (!HasBytes(position, 4, length))
            {
                return false;
            }

            int size = BitConverter.ToInt32(ReadBigEndian(data, ref position, 4), 0);
            if (size < 0 || !HasBytes(position, size, length))
            {
                return false;
            }

            byte[] blob = new byte[size];
            Array.Copy(data, position, blob, 0, size);
            position += size;

            // 4 バイト境界パディングを読み飛ばす.
            position += (4 - (size % 4)) % 4;

            value = blob;
            return true;
        }

        // position から count バイトを読み出し, ビッグエンディアンとして解釈できるよう必要なら反転する.
        private static byte[] ReadBigEndian(byte[] data, ref int position, int count)
        {
            byte[] slice = new byte[count];
            Array.Copy(data, position, slice, 0, count);
            position += count;
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(slice);
            }
            return slice;
        }

        private static string ReadOscString(byte[] data, int length, ref int position)
        {
            int start = position;
            while (position < length && data[position] != 0)
            {
                position++;
            }

            string value = Encoding.ASCII.GetString(data, start, position - start);

            // null 終端を含めて 4 バイト境界まで読み進める.
            int consumed = position - start;
            int padding = 4 - (consumed % 4);
            position += padding;

            return value;
        }

        // position から need バイトを読めるか (length を超えないか).
        private static bool HasBytes(int position, int need, int length)
        {
            return length - position >= need;
        }

        #endregion
    }
}
