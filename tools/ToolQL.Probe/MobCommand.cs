using ToolQL.Protocol.Resources;

namespace ToolQL.Probe;

/// <summary>
/// `probe mob &lt;mob-N.bin&gt;`: đọc một gói <c>11</c> (sprite quái) đã lưu bởi `probe images` bằng <see cref="MobSpritePack"/> và in
/// thống kê, kèm các phép kiểm chéo: vùng hình con nằm trong ảnh PNG, mảnh của khung trỏ tới hình con có thật. KHÔNG nối mạng.
/// </summary>
public static class MobCommand
{
    public static int Run(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine("Không thấy tệp: " + path);
            return 1;
        }

        MobSpritePack mob;
        try
        {
            mob = MobSpritePack.Parse(File.ReadAllBytes(path));
        }
        catch (InvalidDataException ex)
        {
            Console.WriteLine("Không đọc được gói 11: " + ex.Message);
            return 3;
        }

        int exit = 0;
        var pack = mob.Pack;
        Console.WriteLine($"Loại quái {mob.Type}, kiểu {mob.Kind}; byte cuối {mob.Tail}; bảng phụ {(mob.Extra is null ? "không có" : mob.Extra.Length + " mục")}");
        Console.WriteLine($"Byte thừa sau gói: {mob.ExtraBytes}; byte thừa sau khối dữ liệu: {pack.ExtraBytes}");
        if (mob.ExtraBytes != 0 || pack.ExtraBytes != 0) exit = 3;

        bool isPng = mob.Png.Length >= 24 && mob.Png[0] == 0x89 && mob.Png[1] == 0x50 && mob.Png[2] == 0x4E && mob.Png[3] == 0x47;
        int pw = isPng ? (mob.Png[16] << 24) | (mob.Png[17] << 16) | (mob.Png[18] << 8) | mob.Png[19] : 0;
        int ph = isPng ? (mob.Png[20] << 24) | (mob.Png[21] << 16) | (mob.Png[22] << 8) | mob.Png[23] : 0;
        Console.WriteLine($"PNG: {mob.Png.Length} byte, {(isPng ? $"{pw}x{ph}" : "KHÔNG phải PNG")}");
        if (!isPng) exit = 3;

        Console.WriteLine($"Hình con ({pack.SubImages.Length}):");
        foreach (var s in pack.SubImages)
        {
            bool inside = isPng && s.X >= 0 && s.Y >= 0 && s.X + s.Width <= pw && s.Y + s.Height <= ph;
            Console.WriteLine($"  id {s.Id}: ({s.X},{s.Y}) {s.Width}x{s.Height} {(inside ? "trong ảnh" : "NGOÀI ẢNH")}");
            if (!inside) exit = 3;
        }

        var ids = pack.SubImages.Select(s => s.Id).ToHashSet();
        int bad = pack.Frames.SelectMany(f => f.Parts).Count(p => !ids.Contains(p.SubImage));
        Console.WriteLine($"Khung: {pack.Frames.Length}; số mảnh mỗi khung: {string.Join(" ", pack.Frames.Select(f => f.Parts.Length))}");
        Console.WriteLine($"Mảnh trỏ tới hình con không có: {bad}");
        if (bad != 0) exit = 3;
        int distinctFrames = pack.Frames.Select(f => string.Join(";", f.Parts.Select(p => $"{p.X},{p.Y},{p.SubImage}"))).Distinct().Count();
        Console.WriteLine($"Số khung khác nhau về nội dung: {distinctFrames}");
        Console.WriteLine($"Danh sách hoạt ảnh ({pack.Sequence.Length} số): {string.Join(",", pack.Sequence)}");
        return exit;
    }
}
