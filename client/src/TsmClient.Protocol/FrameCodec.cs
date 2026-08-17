using TsmClient.Domain;

namespace TsmClient.Protocol;

public static class FrameCodec
{
    public const byte XorKey = 0xAD;
    public const byte Header0 = 0xC0;
    public const byte Header1 = 0x91;
    public const int HeaderSize = 6;
    public const int MinimumFrameSize = 9;

    public static byte[] Encode(byte mainKind, byte subKind, ReadOnlySpan<byte> payload, byte compress = 0, ushort flags = 0)
    {
        int length = HeaderSize + 3 + payload.Length;
        var frame = new byte[length];
        frame[0] = (byte)(Header0 ^ XorKey);
        frame[1] = (byte)(Header1 ^ XorKey);
        frame[2] = (byte)((length & 0xff) ^ XorKey);
        frame[3] = (byte)(((length >> 8) & 0xff) ^ XorKey);
        frame[4] = (byte)((flags & 0xff) ^ XorKey);
        frame[5] = (byte)(((flags >> 8) & 0xff) ^ XorKey);
        frame[6] = (byte)(mainKind ^ XorKey);
        frame[7] = (byte)(subKind ^ XorKey);
        frame[8] = (byte)(compress ^ XorKey);
        for (int i = 0; i < payload.Length; i++) frame[9 + i] = (byte)(payload[i] ^ XorKey);
        return frame;
    }

    public static bool TryDecode(List<byte> buffer, out GamePacket? packet)
    {
        packet = null;
        while (buffer.Count >= HeaderSize)
        {
            if ((buffer[0] ^ XorKey) != Header0 || (buffer[1] ^ XorKey) != Header1)
            {
                buffer.RemoveAt(0);
                continue;
            }

            int length = (buffer[2] ^ XorKey) | ((buffer[3] ^ XorKey) << 8);
            if (length < MinimumFrameSize)
            {
                buffer.RemoveAt(0);
                continue;
            }
            if (buffer.Count < length) return false;

            var decoded = new byte[length - HeaderSize];
            for (int i = 0; i < decoded.Length; i++) decoded[i] = (byte)(buffer[HeaderSize + i] ^ XorKey);
            buffer.RemoveRange(0, length);
            packet = new GamePacket(decoded[0], decoded[1], decoded[2], decoded[3..]);
            return true;
        }
        return false;
    }
}
