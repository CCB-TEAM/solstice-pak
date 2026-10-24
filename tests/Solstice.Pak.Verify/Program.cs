using System.Text;
using Solstice.Pak;

// 无第三方依赖的自检程序：写一个 pak 再读回来，逐项校验。
// 退出码 0 = 全部通过。

var failures = 0;

void Check(string name, bool ok, string? detail = null)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail is null ? "" : "  " + detail)}");
    if (!ok) { failures++; }
}

var payload = new[]
{
    new PakEntry("Blueprints/Cards/demo.uasset", Encoding.ASCII.GetBytes("UE-ASSET-PAYLOAD")),
    new PakEntry("Blueprints/Cards/demo.uexp", Encoding.ASCII.GetBytes("UE-EXPORT-PAYLOAD")),
    new PakEntry("Data/empty.bin", Array.Empty<byte>())
};

var pak = PakWriter.Build("../../../Game/Content/", payload);
Check("写出非空", pak.Length > 0, $"{pak.Length} 字节");

// footer
var footer = pak.Length - 44;
Check("magic", BitConverter.ToUInt32(pak, footer) == PakWriter.Magic, $"0x{BitConverter.ToUInt32(pak, footer):X8}");
Check("version", BitConverter.ToInt32(pak, footer + 4) == PakWriter.Version);

var indexOffset = BitConverter.ToInt64(pak, footer + 8);
var indexSize = BitConverter.ToInt64(pak, footer + 16);
Check("索引紧贴 footer", indexOffset + indexSize == footer, $"{indexOffset}+{indexSize}");

// 读回
var archive = PakArchive.Read(pak);
Check("挂载点", archive.MountPoint == "../../../Game/Content/", archive.MountPoint);
Check("条目数", archive.Entries.Count == payload.Length, $"{archive.Entries.Count}");

for (var i = 0; i < payload.Length; i++)
{
    var expected = payload[i];
    var actual = archive.Entries[i];
    Check($"条目{i} 名称", actual.Name == expected.Name, actual.Name);
    Check($"条目{i} 数据", actual.Data.AsSpan().SequenceEqual(expected.Data), $"{actual.Data.Length} 字节");
}

// 文件开头就是第一条目的内嵌 FPakEntry，其 offset 字段恒为 0
Check("首条目内嵌头 offset=0", BitConverter.ToInt64(pak, 0) == 0);
// 内嵌头的 size 字段 = 第一条目长度
Check("首条目内嵌头 size", BitConverter.ToInt64(pak, 8) == payload[0].Data.Length);

// 空数据条目
Check("空文件条目", archive.Entries[2].Data.Length == 0);

Console.WriteLine(failures == 0 ? "全部通过" : $"{failures} 项失败");
return failures == 0 ? 0 : 1;
