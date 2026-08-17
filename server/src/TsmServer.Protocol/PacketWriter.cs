using System.Buffers.Binary;
using System.Text;

namespace TsmServer.Protocol;

public class PacketWriter
{
    private readonly MemoryStream _ms = new();

    public PacketWriter WriteByte(byte val)
    {
        _ms.WriteByte(val);
        return this;
    }

    public PacketWriter WriteUInt16LE(ushort val)
    {
        Span<byte> buf = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, val);
        _ms.Write(buf);
        return this;
    }

    public PacketWriter WriteInt16LE(short val)
    {
        Span<byte> buf = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(buf, val);
        _ms.Write(buf);
        return this;
    }

    public PacketWriter WriteUInt32LE(uint val)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, val);
        _ms.Write(buf);
        return this;
    }

    public PacketWriter WriteInt32LE(int val)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, val);
        _ms.Write(buf);
        return this;
    }

    public PacketWriter WriteInt64LE(long val)
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buf, val);
        _ms.Write(buf);
        return this;
    }

    public PacketWriter WriteDoubleLE(double val)
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleLittleEndian(buf, val);
        _ms.Write(buf);
        return this;
    }

    public PacketWriter WriteBytes(ReadOnlySpan<byte> bytes)
    {
        _ms.Write(bytes);
        return this;
    }

    public PacketWriter WriteString(string str, int fixedLength, Encoding? encoding = null)
    {
        encoding ??= GameCharsets.Default;
        byte[] bytes = encoding.GetBytes(str);
        byte[] buf = new byte[fixedLength];
        Array.Copy(bytes, buf, Math.Min(bytes.Length, fixedLength));
        _ms.Write(buf, 0, fixedLength);
        return this;
    }

    public PacketWriter WriteNullTerminatedString(string str, Encoding? encoding = null)
    {
        encoding ??= GameCharsets.Default;
        byte[] bytes = encoding.GetBytes(str);
        _ms.Write(bytes, 0, bytes.Length);
        _ms.WriteByte(0);
        return this;
    }

    public byte[] ToArray() => _ms.ToArray();
}
