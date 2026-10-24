namespace Solstice.Pak;

/// <summary>One file inside a pak.</summary>
/// <param name="Name">Path relative to the mount point, using forward slashes.</param>
/// <param name="Data">Raw file bytes (stored, not compressed).</param>
public sealed record PakEntry(string Name, byte[] Data);
