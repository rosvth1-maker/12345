using TsmServer.Protocol;
using Xunit;

namespace TsmServer.Tests;

public class FrameCodecTests
{
    [Fact]
    public void Encode_And_Decode_Frame_Should_Roundtrip_Successfully()
    {
        byte[] payload = new byte[] { 1, 2, 3, 4, 5 };
        byte[] frame = FrameCodec.EncodeFrame(mainKind: 20, subKind: 1, payload);

        ReadOnlySpan<byte> buffer = frame;
        bool success = FrameCodec.TryDecodeFrame(ref buffer, out var decodedPacket);

        Assert.True(success);
        Assert.NotNull(decodedPacket);
        Assert.Equal(3 + payload.Length, decodedPacket.Length);
        Assert.Equal(20, decodedPacket[0]); // mainKind
        Assert.Equal(1, decodedPacket[1]);  // subKind
        Assert.Equal(0, decodedPacket[2]);  // compress
        Assert.Equal(payload, decodedPacket[3..]);
    }
}
