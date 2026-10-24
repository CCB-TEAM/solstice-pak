using System.Security.Cryptography;
using System.Text;

namespace Solstice.Pak;

/// <summary>Reads back a pak produced by <see cref="PakWriter"/> (same v3 variant).</summary>
public sealed record PakArchive(string MountPoint, IReadOnlyList<PakEntry> Entries)
{
    public static PakArchive Read(byte[] pak)
    {
        const int FooterSize = 44;

        if (pak.Length < FooterSize)
        {
            throw new InvalidDataException("文件太小，不是 pak");
        }

        var footer = pak.Length - FooterSize;
        var magic = BitConverter.ToUInt32(pak, footer);
        if (magic != PakWriter.Magic)
        {
            throw new InvalidDataException($"magic 不匹配: 0x{magic:X8}");
        }

        var version = BitConverter.ToInt32(pak, footer + 4);
        var indexOffset = BitConverter.ToInt64(pak, footer + 8);
        var indexSize = BitConverter.ToInt64(pak, footer + 16);

        if (indexOffset + indexSize != footer)
        {
            throw new InvalidDataException("索引边界与 footer 不一致");
        }

        var index = new byte[indexSize];
        Array.Copy(pak, indexOffset, index, 0, indexSize);

        var expectedHash = new byte[20];
        Array.Copy(pak, footer + 24, expectedHash, 0, 20);
        if (!SHA1.HashData(index).AsSpan().SequenceEqual(expectedHash))
        {
            throw new InvalidDataException("索引 SHA-1 校验失败");
        }

        var pos = 0;
        var mount = ReadFString(index, ref pos);
        var count = BitConverter.ToInt32(index, pos);
        pos += 4;

        var entries = new List<PakEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var name = ReadFString(index, ref pos);

            var offset = BitConverter.ToInt64(index, pos);
            var size = BitConverter.ToInt64(index, pos + 8);
            var hash = new byte[20];
            Array.Copy(index, pos + 28, hash, 0, 20);
            pos += PakWriter.EntrySize;

            var data = new byte[size];
            Array.Copy(pak, offset + PakWriter.EntrySize, data, 0, size);

            if (!SHA1.HashData(data).AsSpan().SequenceEqual(hash))
            {
                throw new InvalidDataException($"条目 {name} 的数据 SHA-1 校验失败");
            }

            entries.Add(new PakEntry(name, data));
        }

        if (pos != indexSize)
        {
            throw new InvalidDataException($"索引未完全消费: {pos}/{indexSize}");
        }

        return new PakArchive(mount, entries);
    }

    private static string ReadFString(byte[] buffer, ref int pos)
    {
        var length = BitConverter.ToInt32(buffer, pos);
        pos += 4;

        if (length == 0)
        {
            return string.Empty;
        }

        if (length > 0)
        {
            var s = Encoding.ASCII.GetString(buffer, pos, length);
            pos += length;
            return s.TrimEnd('\0');
        }

        var chars = -length;
        var u = Encoding.Unicode.GetString(buffer, pos, chars * 2);
        pos += chars * 2;
        return u.TrimEnd('\0');
    }
}
