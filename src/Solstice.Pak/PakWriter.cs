using System.Security.Cryptography;
using System.Text;

namespace Solstice.Pak;

/// <summary>
/// 写 UE pak（version 3 变体）。
///
/// <para>
/// 格式来自对真实游戏 pak 的实测解析（magic 0x5A6F12E1 / version 3）：
/// </para>
///
/// <code>
/// [条目 inline FPakEntry(53B)][条目数据] ... [index][footer(44B)]
///
/// footer: magic(0x5A6F12E1) version(3) indexOffset(8) indexSize(8) indexHash(20)
/// index : mountPoint(FString) entryCount(4) { name(FString) FPakEntry(53B) }*
/// FPakEntry: offset(8) size(8) uncompressedSize(8) method(4) hash(20) reserved(5)
/// </code>
///
/// <para>
/// inline 副本的 offset 字段恒为 0；索引里的 offset 指向该 inline 副本的位置。
/// 尾部 5 字节用途未确认，实测全 0，这里原样写 0。
/// </para>
/// </summary>
public static class PakWriter
{
    public const uint Magic = 0x5A6F12E1;
    public const int Version = 3;
    public const int EntrySize = 53;

    public static byte[] Build(string mountPoint, IReadOnlyList<PakEntry> entries)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        var hashes = new byte[entries.Count][];
        var offsets = new long[entries.Count];

        for (var i = 0; i < entries.Count; i++)
        {
            hashes[i] = Sha1(entries[i].Data);
            offsets[i] = ms.Position;

            // inline 副本：offset 写 0
            WriteEntry(w, 0, entries[i].Data.Length, entries[i].Data.Length, hashes[i]);
            w.Write(entries[i].Data);
        }

        var indexOffset = ms.Position;

        byte[] indexBytes;
        using (var idx = new MemoryStream())
        {
            using (var iw = new BinaryWriter(idx, Encoding.UTF8, leaveOpen: true))
            {
                WriteFString(iw, mountPoint);
                iw.Write(entries.Count);

                for (var i = 0; i < entries.Count; i++)
                {
                    WriteFString(iw, entries[i].Name);
                    WriteEntry(iw, offsets[i], entries[i].Data.Length, entries[i].Data.Length, hashes[i]);
                }
            }

            indexBytes = idx.ToArray();
        }

        w.Write(indexBytes);

        w.Write(Magic);
        w.Write(Version);
        w.Write(indexOffset);
        w.Write((long)indexBytes.Length);
        w.Write(Sha1(indexBytes));
        w.Flush();

        return ms.ToArray();
    }

    private static void WriteEntry(BinaryWriter w, long offset, long size, long uncompressed, byte[] hash)
    {
        w.Write(offset);
        w.Write(size);
        w.Write(uncompressed);
        w.Write(0);          // compressionMethod = none
        w.Write(hash);       // 20 字节
        w.Write(new byte[5]); // 尾部 5 字节，实测全 0
        // 8+8+8+4+20+5 = 53
    }

    private static void WriteFString(BinaryWriter w, string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            w.Write(0);
            return;
        }

        var bytes = Encoding.ASCII.GetBytes(s);
        w.Write(bytes.Length + 1); // 含结尾 0
        w.Write(bytes);
        w.Write((byte)0);
    }

    private static byte[] Sha1(byte[] data) => SHA1.HashData(data);

    /// <summary>条目在 pak 中占用的字节数（inline 前缀 + 数据）。</summary>
    public static int EntrySizeOf(byte[] data) => EntrySize + data.Length;
}
