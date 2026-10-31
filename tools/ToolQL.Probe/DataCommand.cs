using ToolQL.Protocol;
using ToolQL.Protocol.Resources;

namespace ToolQL.Probe;

/// <summary>
/// `probe data &lt;tệp&gt;`: đọc một gói <c>-87</c> (NRdata) đã lưu trong kho (<c>%LOCALAPPDATA%\ToolQL\cache\data-0.v&lt;N&gt;.bin</c>)
/// bằng <see cref="NrData"/>, <see cref="ImageTable"/>, <see cref="PartTable"/> và in thống kê. KHÔNG nối mạng.
/// "Byte thừa" khác 0 nghĩa là bộ đọc chưa khớp dữ liệu thật.
/// </summary>
public static class DataCommand
{
    public static int Run(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine("Không thấy tệp: " + path);
            return 1;
        }

        byte[] payload = File.ReadAllBytes(path);
        NrData data;
        try
        {
            data = NrData.Parse(payload);
        }
        catch (InvalidDataException ex)
        {
            Console.WriteLine("Không tách được khối: " + ex.Message);
            return 3;
        }

        Console.WriteLine($"Tệp: {path} ({payload.Length} byte)");
        Console.WriteLine($"Phiên bản dữ liệu: {data.Version}; byte thừa sau 6 khối: {data.ExtraBytes}");
        Console.WriteLine($"Độ dài khối: dart {data.Dart.Length}, arrow {data.Arrow.Length}, effect {data.Effect.Length}, " +
                          $"image {data.Image.Length}, part {data.Part.Length}, skill {data.Skill.Length}");

        int exit = data.ExtraBytes == 0 ? 0 : 3;

        try
        {
            var images = ImageTable.Parse(data.Image);
            Console.WriteLine($"NR_image: {images.Sprites.Length} sprite, byte thừa {images.ExtraBytes}");
            var atlases = images.Sprites.GroupBy(s => s.Atlas).OrderBy(g => g.Key)
                .Select(g => $"{g.Key}:{g.Count()}");
            Console.WriteLine("  số sprite theo atlas: " + string.Join(" ", atlases));
            var big = images.Sprites.OrderByDescending(s => s.Width * s.Height).First();
            Console.WriteLine($"  sprite lớn nhất: {big.Width}x{big.Height} (atlas {big.Atlas}); " +
                              $"có kích thước <= 0: {images.Sprites.Count(s => s.Width <= 0 || s.Height <= 0)}");
            Console.WriteLine("  kích thước tối thiểu mỗi atlas (x+rộng, y+cao lớn nhất): " + string.Join("  ", images.Sprites.GroupBy(s => s.Atlas).OrderBy(g => g.Key).Select(g => $"{g.Key}: {g.Max(s => s.X + s.Width)}x{g.Max(s => s.Y + s.Height)}")));
            if (images.ExtraBytes != 0) exit = 3;
        }
        catch (InvalidDataException ex)
        {
            Console.WriteLine("NR_image: LỖI " + ex.Message);
            exit = 3;
        }

        try
        {
            var parts = PartTable.Parse(data.Part);
            Console.WriteLine($"NR_part: {parts.Sets.Length} bộ phần, byte thừa {parts.ExtraBytes}");
            Console.WriteLine("  số bộ theo loại: " + string.Join(" ", parts.Sets.GroupBy(s => s.Kind).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}")));
            int spriteCount = 0;
            try { spriteCount = ImageTable.Parse(data.Image).Sprites.Length; } catch (InvalidDataException) { }
            int outOfRange = parts.Sets.SelectMany(s => s.Parts).Count(p => p.Sprite < 0 || p.Sprite >= spriteCount);
            Console.WriteLine($"  phần trỏ ra ngoài bảng ảnh (mã sprite < 0 hoặc >= {spriteCount}): {outOfRange}");
            var all = parts.Sets.SelectMany(s => s.Parts).ToArray();
            Console.WriteLine($"  mã sprite q: nhỏ nhất {all.Min(p => p.Sprite)}, lớn nhất {all.Max(p => p.Sprite)}, âm {all.Count(p => p.Sprite < 0)}, = 0: {all.Count(p => p.Sprite == 0)}");
            var distinct = all.Select(p => (int)p.Sprite).Distinct().Order().ToArray();
            Console.WriteLine($"  mã khác nhau: {distinct.Length}; 10 mã đầu: {string.Join(",", distinct.Take(10))}; 10 mã cuối: {string.Join(",", distinct.TakeLast(10))}");
            Console.WriteLine("  số bộ theo loại, mã lớn nhất: " + string.Join(" ", parts.Sets.GroupBy(s => s.Kind).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.SelectMany(s => s.Parts).Max(p => p.Sprite)}")));
            if (parts.ExtraBytes != 0) exit = 3;
        }
        catch (InvalidDataException ex)
        {
            Console.WriteLine("NR_part: LỖI " + ex.Message);
            exit = 3;
        }
        return exit;
    }
}
