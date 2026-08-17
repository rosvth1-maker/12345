using System.Buffers.Binary;
using System.Text;
using TsmServer.Protocol;

namespace TsmServer.Data;

public class DatReader
{
    private readonly byte[] _data;
    private int _offset;

    public DatReader(byte[] data)
    {
        _data = data;
        _offset = 0;
    }

    public int Remaining => _data.Length - _offset;
    public int Position => _offset;

    public byte ReadByte() => _data[_offset++];
    public ushort ReadUInt16LE() { var v = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(_offset)); _offset += 2; return v; }
    public short ReadInt16LE() { var v = BinaryPrimitives.ReadInt16LittleEndian(_data.AsSpan(_offset)); _offset += 2; return v; }
    public uint ReadUInt32LE() { var v = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(_offset)); _offset += 4; return v; }
    public int ReadInt32LE() { var v = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(_offset)); _offset += 4; return v; }
    public byte[] ReadBytes(int count) { var b = new byte[count]; Array.Copy(_data, _offset, b, 0, count); _offset += count; return b; }
    public string ReadString(int length, Encoding? encoding = null)
    {
        encoding ??= GameCharsets.Default;
        var slice = ReadBytes(length);
        int nullIdx = Array.IndexOf(slice, (byte)0);
        if (nullIdx >= 0) Array.Resize(ref slice, nullIdx);
        return encoding.GetString(slice);
    }
}
