using NUnit.Framework;
using SignalBridge.Osc;

namespace SignalBridge.Osc.Tests
{
    // MinimalOsc の encode / decode を検証する EditMode テスト.
    public class MinimalOscTests
    {
        #region encode

        [Test]
        public void Encode_IntArgument_ProducesBigEndianBytes()
        {
            // "/ping" + int 1 の既知バイト列を検証する (ビッグエンディアン + 4 バイトパディング).
            byte[] expected =
            {
                0x2F, 0x70, 0x69, 0x6E, 0x67, 0x00, 0x00, 0x00, // "/ping" + null パディング
                0x2C, 0x69, 0x00, 0x00,                         // ",i" + null パディング
                0x00, 0x00, 0x00, 0x01                          // int 1 (big-endian)
            };

            byte[] actual = MinimalOsc.Encode("/ping", 1);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Encode_AlwaysPadsToFourByteBoundary()
        {
            byte[] bytes = MinimalOsc.Encode("/a", 1, 2.0f, "hello");
            Assert.AreEqual(0, bytes.Length % 4);
        }

        #endregion

        #region round-trip

        [Test]
        public void EncodeDecode_MixedArguments_RoundTrips()
        {
            byte[] bytes = MinimalOsc.Encode("/move", 42, 3.5f, "go");
            OscMessage message = MinimalOsc.Decode(bytes, bytes.Length);

            Assert.AreEqual("/move", message.Address);
            Assert.AreEqual(",ifs", message.TypeTags);
            Assert.AreEqual(3, message.Args.Length);
            Assert.AreEqual(42, message.Args[0]);
            Assert.AreEqual(3.5f, message.Args[1]);
            Assert.AreEqual("go", message.Args[2]);
        }

        [Test]
        public void EncodeDecode_NoArguments_RoundTrips()
        {
            byte[] bytes = MinimalOsc.Encode("/trigger");
            OscMessage message = MinimalOsc.Decode(bytes, bytes.Length);

            Assert.AreEqual("/trigger", message.Address);
            Assert.AreEqual(",", message.TypeTags);
            Assert.AreEqual(0, message.Args.Length);
        }

        #endregion

        #region 拡張型 (h/d/T/F/b)

        [Test]
        public void EncodeDecode_Int64AndDouble_RoundTrips()
        {
            byte[] bytes = MinimalOsc.Encode("/v", 9000000000L, 1.25d);
            OscMessage message = MinimalOsc.Decode(bytes, bytes.Length);

            Assert.AreEqual(",hd", message.TypeTags);
            Assert.AreEqual(9000000000L, message.Args[0]);
            Assert.AreEqual(1.25d, message.Args[1]);
        }

        [Test]
        public void EncodeDecode_Booleans_RoundTrips()
        {
            // T/F は型タグのみでデータ部を持たない.
            byte[] bytes = MinimalOsc.Encode("/flags", true, false);
            OscMessage message = MinimalOsc.Decode(bytes, bytes.Length);

            Assert.AreEqual(",TF", message.TypeTags);
            Assert.AreEqual(2, message.Args.Length);
            Assert.AreEqual(true, message.Args[0]);
            Assert.AreEqual(false, message.Args[1]);
        }

        [Test]
        public void EncodeDecode_Blob_RoundTrips()
        {
            byte[] payload = { 0xDE, 0xAD, 0xBE, 0xEF, 0x01 };
            byte[] bytes = MinimalOsc.Encode("/raw", payload);
            OscMessage message = MinimalOsc.Decode(bytes, bytes.Length);

            Assert.AreEqual(",b", message.TypeTags);
            Assert.AreEqual(payload, (byte[])message.Args[0]);
        }

        #endregion

        #region 寛容デコード (未知型タグ / データ不足)

        [Test]
        public void Decode_UnknownTypeTag_StopsAndMarksIncomplete()
        {
            // "/x" + ",iZ" (Z は未対応型) + int 7. 'i' まで読めて 'Z' で停止するはず.
            byte[] bytes =
            {
                0x2F, 0x78, 0x00, 0x00,       // "/x"
                0x2C, 0x69, 0x5A, 0x00,       // ",iZ"
                0x00, 0x00, 0x00, 0x07        // int 7
            };

            OscMessage message = MinimalOsc.Decode(bytes, bytes.Length);

            Assert.AreEqual("/x", message.Address);
            Assert.AreEqual(",iZ", message.TypeTags);
            Assert.IsFalse(message.IsComplete);
            Assert.AreEqual(1, message.Args.Length);
            Assert.AreEqual(7, message.Args[0]);
        }

        [Test]
        public void Decode_TruncatedData_StopsWithoutThrowing()
        {
            // ",ii" だが int を 1 個分しかデータがない (2 個目は不足).
            byte[] bytes =
            {
                0x2F, 0x78, 0x00, 0x00,       // "/x"
                0x2C, 0x69, 0x69, 0x00,       // ",ii"
                0x00, 0x00, 0x00, 0x05        // int 5 (2 個目のデータなし)
            };

            OscMessage message = MinimalOsc.Decode(bytes, bytes.Length);

            Assert.IsFalse(message.IsComplete);
            Assert.AreEqual(1, message.Args.Length);
            Assert.AreEqual(5, message.Args[0]);
        }

        #endregion
    }
}
