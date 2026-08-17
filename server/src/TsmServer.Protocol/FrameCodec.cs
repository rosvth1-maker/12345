using System.Buffers.Binary;

namespace TsmServer.Protocol;

public static class FrameCodec
{
    public const byte XorKey = 0xAD;
    public const byte Header0 = 0xC0;
    public const byte Header1 = 0x91;
    public const int FrameHeaderSize = 6;
    public const int MinFrameSize = FrameHeaderSize + 3; // header(6) + main(1) + sub(1) + compress(1)

    public static bool TryDecodeFrame(ref ReadOnlySpan<byte> buffer, out byte[]? packet)
    {
        packet = null;
        if (buffer.Length < FrameHeaderSize)
            return false;

        byte h0 = (byte)(buffer[0] ^ XorKey);
        byte h1 = (byte)(buffer[1] ^ XorKey);
        byte lenLo = (byte)(buffer[2] ^ XorKey);
        byte lenHi = (byte)(buffer[3] ^ XorKey);
        int totalLength = lenLo | (lenHi << 8);

        if (h0 != Header0 || h1 != Header1 || totalLength < MinFrameSize)
        {
            // Invalid header, advance 1 byte to search next
            buffer = buffer[1..];
            return false;
        }

        if (buffer.Length < totalLength)
            return false; // Incomplete frame, wait for more data

        // Extract game packet (skipping header 4 bytes + flags 2 bytes = 6 bytes)
        int packetSize = totalLength - FrameHeaderSize;
        packet = new byte[packetSize];

        for (int i = 0; i < packetSize; i++)
        {
            packet[i] = (byte)(buffer[FrameHeaderSize + i] ^ XorKey);
        }

        buffer = buffer[totalLength..];
        return true;
    }

    public static byte[] EncodeFrame(int mainKind, int subKind, ReadOnlySpan<byte> payload, byte compress = 0, ushort flags = 0)
    {
        int totalLength = FrameHeaderSize + 3 + payload.Length;
        byte[] frame = new byte[totalLength];

        // 1. Header (2B)
        frame[0] = (byte)(Header0 ^ XorKey);
        frame[1] = (byte)(Header1 ^ XorKey);

        // 2. Total Length (2B LE)
        frame[2] = (byte)((totalLength & 0xFF) ^ XorKey);
        frame[3] = (byte)(((totalLength >> 8) & 0xFF) ^ XorKey);

        // 3. Flags (2B)
        frame[4] = (byte)((flags & 0xFF) ^ XorKey);
        frame[5] = (byte)(((flags >> 8) & 0xFF) ^ XorKey);

        // 4. Game Packet Header
        frame[6] = (byte)(mainKind ^ XorKey);
        frame[7] = (byte)(subKind ^ XorKey);
        frame[8] = (byte)(compress ^ XorKey);

        // 5. Payload
        for (int i = 0; i < payload.Length; i++)
        {
            frame[FrameHeaderSize + 3 + i] = (byte)(payload[i] ^ XorKey);
        }

        return frame;
    }
}
