# solstice-pak

最小的 UE `.pak` 读写库，针对 **magic `0x5A6F12E1` / version 3** 这一变体。

零依赖，只用到 BCL（`BinaryWriter` + `SHA1`）。可以跑在 .NET 10、Blazor WASM 里（浏览器内直接产出 pak）。

## 格式（实测反推）

```
[条目0 内嵌 FPakEntry (53B)][条目0 数据]
[条目1 内嵌 FPakEntry (53B)][条目1 数据]
...
[index]
[footer (44B)]

footer:
  magic         uint32  0x5A6F12E1
  version       int32   3
  indexOffset   int64
  indexSize     int64
  indexHash     byte[20]  index 的 SHA-1

index:
  mountPoint    FString    例如 "../../../Game/Content/"
  entryCount    int32
  entry*:
    name        FString    相对 mount point 的路径，正斜杠
    FPakEntry   53 字节

FPakEntry (53B):
  offset            int64   内嵌副本恒为 0；索引里指向该内嵌副本的位置
  size              int64
  uncompressedSize  int64
  compressionMethod int32   0 = 不压缩
  hash              byte[20] 数据 SHA-1
  reserved          byte[5]  用途未确认，实测全 0
```

### 两个容易踩的点

1. **数据不在 `offset` 处，而在 `offset + 53`** —— 每个条目前面都有一份内嵌的 `FPakEntry` 副本。
2. **FString 的长度包含结尾的 `\0`** —— `"abc"` 写成 `int32(4) + "abc\0"`。

## 用法

```csharp
using Solstice.Pak;

var entries = new[]
{
    new PakEntry("Blueprints/Cards/demo.uasset", File.ReadAllBytes("demo.uasset")),
    new PakEntry("Blueprints/Cards/demo.uexp",   File.ReadAllBytes("demo.uexp")),
};

var bytes = PakWriter.Build("../../../Game/Content/", entries);
File.WriteAllBytes("mod.pak", bytes);

// 读回来校验
var archive = PakArchive.Read(bytes);
foreach (var e in archive.Entries)
{
    Console.WriteLine($"{e.Name}  {e.Data.Length} 字节");
}
```

## 验证

```bash
dotnet run --project tests/Solstice.Pak.Verify
```

`PakArchive.Read` 会校验 footer 边界、索引 SHA-1、每个条目的数据 SHA-1，并检查索引是否被完全消费。

**这套实现曾对一份真实游戏 pak（66,520 字节 / 2 条目）做过逐字节复现**：用 `PakWriter.Build` 重建出的文件与原始文件完全一致。仓库里不含任何游戏资产，自检用的是合成数据。

## 不支持

- 压缩（`compressionMethod != 0`）—— 只写 store 模式
- 加密索引
- 签名（`.sig`）
- IoStore（`.utoc` / `.ucas`）
- 其它 pak version —— 只验证过 version 3

## 许可

MIT
