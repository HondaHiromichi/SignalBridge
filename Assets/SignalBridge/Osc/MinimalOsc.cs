using System;
using System.Collections.Generic;
using System.Text;

namespace SignalBridge.Osc
{
    // OSC メッセージの encode / decode を行う static ヘルパ.
    // UnityEngine に依存しない純 C# 実装 (他プロジェクトへ移植しやすくするため).
    public static class MinimalOsc
    {
        #region 定数

        private const char TagInt = 'i';
        private const char TagFloat = 'f';
        private const char TagString = 's';

        #endregion

        #region Public メソッド

        // address と引数 (int / float / string) から OSC メッセージのバイト列を生成する.
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

            // 3. Arguments
            foreach (object arg in args)
            {
                WriteArgument(buffer, arg);
            }

            return buffer.ToArray();
        }

        // 受信した OSC バイト列を OscMessage へ復元する.
        public static OscMessage Decode(byte[] data, int length)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            int position = 0;
            string address = ReadOscString(data, length, ref position);
            string tags = ReadOscString(data, length, ref position);

            // 先頭の ',' を除いた型タグ分だけ引数を読む.
            int argCount = tags.Length > 0 && tags[0] == ',' ? tags.Length - 1 : 0;
            object[] args = new object[argCount];
            for (int i = 0; i < argCount; i++)
            {
                char tag = tags[i + 1];
                args[i] = ReadArgument(data, length, ref position, tag);
            }

            return new OscMessage(address, tags, args);
        }

        #endregion

        #region Private メソッド

        private static char TypeTagOf(object arg)
        {
            switch (arg)
            {
                case int _:
                    return TagInt;
                case float _:
                    return TagFloat;
                case string _:
                    return TagString;
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
                case float f:
                    WriteBigEndian(buffer, BitConverter.GetBytes(f));
                    break;
                case string s:
                    WriteOscString(buffer, s);
                    break;
                default:
                    throw new NotSupportedException("未対応の OSC 引数型です: " + (arg?.GetType().Name ?? "null"));
            }
        }

        // 4 バイトをビッグエンディアンで書き込む (実行環境がリトルエンディアンなら反転する).
        private static void WriteBigEndian(List<byte> buffer, byte[] fourBytes)
        {
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(fourBytes);
            }
            buffer.AddRange(fourBytes);
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

        private static object ReadArgument(byte[] data, int length, ref int position, char tag)
        {
            switch (tag)
            {
                case TagInt:
                    return ReadInt32(data, ref position);
                case TagFloat:
                    return ReadFloat(data, ref position);
                case TagString:
                    return ReadOscString(data, length, ref position);
                default:
                    throw new NotSupportedException("未対応の OSC 型タグです: " + tag);
            }
        }

        private static int ReadInt32(byte[] data, ref int position)
        {
            byte[] slice = ReadBigEndian(data, ref position);
            return BitConverter.ToInt32(slice, 0);
        }

        private static float ReadFloat(byte[] data, ref int position)
        {
            byte[] slice = ReadBigEndian(data, ref position);
            return BitConverter.ToSingle(slice, 0);
        }

        // 4 バイトを読み出し, ビッグエンディアンとして解釈できるよう必要なら反転する.
        private static byte[] ReadBigEndian(byte[] data, ref int position)
        {
            byte[] slice = new byte[4];
            Array.Copy(data, position, slice, 0, 4);
            position += 4;
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(slice);
            }
            return slice;
        }

        #endregion
    }
}
