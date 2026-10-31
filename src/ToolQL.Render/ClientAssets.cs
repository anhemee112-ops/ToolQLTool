using System.IO.Compression;
using SkiaSharp;

namespace ToolQL.Render;

/// <summary>
/// Tài nguyên đọc từ jar của client do chủ repo cung cấp (ngoài repo, không commit, không kèm gói phát hành).
/// Giấy phép tài nguyên chưa rõ nên việc phát hành kèm hay chỉ trỏ tới jar của người dùng còn để ngỏ.
/// </summary>
public sealed class ClientAssets : IDisposable
{
    private readonly ZipArchive _zip;

    private ClientAssets(ZipArchive zip) => _zip = zip;

    public static ClientAssets OpenJar(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Không thấy jar của client: " + path, path);
        return new ClientAssets(new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read));
    }

    /// <summary>PNG chuẩn của mục <paramref name="entry"/> (ví dụ <c>x1/t/9.png</c>), đã giải XOR nếu cần; null nếu jar không có.</summary>
    public byte[]? ReadPng(string entry)
    {
        var e = _zip.GetEntry(entry);
        if (e is null) return null;
        using var stream = e.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return ClientImageCodec.ToPng(memory.ToArray());
    }

    /// <summary>Bộ ô lát <paramref name="id"/> (<c>x1/t/&lt;id&gt;.png</c>); null nếu jar không có (bộ đó lấy từ server).</summary>
    public TileSet? LoadTileSet(int id) => ReadPng($"x1/t/{id}.png") is { } png ? TileSet.FromPng(png) : null;

    public void Dispose() => _zip.Dispose();
}
