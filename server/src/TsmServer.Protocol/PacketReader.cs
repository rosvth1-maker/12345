using System.Buffers.Binary;
using System.Text;

namespace TsmServer.Protocol;

public ref struct PacketReader
{
    private ReadOnlySpan<byte> _span;
    private int _offset;

    public PacketReader(ReadOnlySpan<byte> span)
    {
        _span = span;
        _offset = 0;
    }

    public int Remaining => _span.Length - _offset;
    public int Position => _offset;

    public byte ReadByte() => _span[_offset++];
    public sbyte ReadSByte() => (sbyte)_span[_offset++];

    public ushort ReadUInt16LE()
    {
        ushort val = BinaryPrimitives.ReadUInt16LittleEndian(_span[_offset..]);
        _offset += 2;
        return val;
    }

    public short ReadInt16LE()
    {
        short val = BinaryPrimitives.ReadInt16LittleEndian(_span[_offset..]);
        _offset += 2;
        return val;
    }

    public uint ReadUInt32LE()
    {
        uint val = BinaryPrimitives.ReadUInt32LittleEndian(_span[_offset..]);
        _offset += 4;
        return val;
    }

    public int ReadInt32LE()
    {
        int val = BinaryPrimitives.ReadInt32LittleEndian(_span[_offset..]);
        _offset += 4;
        return val;
    }

    public long ReadInt64LE()
    {
        long val = BinaryPrimitives.ReadInt64LittleEndian(_span[_offset..]);
        _offset += 8;
        return val;
    }

    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        var slice = _span.Slice(_offset, count);
        _offset += count;
        return slice;
    }

    public string ReadString(int length, Encoding? encoding = null)
    {
        encoding ??= GameCharsets.Default;
        var slice = ReadBytes(length);
        int nullIdx = slice.IndexOf((byte)0);
        if (nullIdx >= 0) slice = slice[..nullIdx];
        return encoding.GetString(slice);
    }

    public string ReadNullTerminatedString(Encoding? encoding = null)
    {
        encoding ??= GameCharsets.Default;
        int start = _offset;
        while (_offset < _span.Length && _span[_offset] != 0)
        {
            _offset++;
        }
        int len = _offset - start;
        if (_offset < _span.Length) _offset++; // skip null
        return encoding.GetString(_span.Slice(start, len));
    }
}
