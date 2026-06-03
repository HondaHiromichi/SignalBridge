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
    }
}
