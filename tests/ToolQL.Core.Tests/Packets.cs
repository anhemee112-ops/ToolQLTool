using ToolQL.Protocol;

namespace ToolQL.Core.Tests;

/// <summary>Gói S→C dựng tay theo mô tả trong docs/protocol (không dùng dữ liệu của client).</summary>
internal static class Packets
{
    /// <summary>-28 sub 4: 4 phiên bản, 1 byte bỏ, n caption (long).</summary>
    public static byte[] Versions(int data = 29, int map = 63, int skill = 7, int item = 24)
    {
        var w = new JavaStreamWriter();
        w.WriteSByte(4);
        w.WriteSByte(data); w.WriteSByte(map); w.WriteSByte(skill); w.WriteSByte(item);
        w.WriteSByte(0);
        w.WriteSByte(1);
        w.WriteLong(1000);
        return w.ToArray();
    }

    /// <summary>-24 rỗng quái, NPC, cổng, vật rơi (đủ để bộ đọc tiêu thụ đúng).</summary>
    public static byte[] Map(int mapId)
    {
        var w = new JavaStreamWriter();
        w.WriteByte(mapId);
        w.WriteSByte(1); w.WriteSByte(4); w.WriteSByte(0); w.WriteSByte(9);
        w.WriteUtf("Map thử"); w.WriteSByte(2);
        w.WriteShort(100); w.WriteShort(200);   // vị trí mình
        w.WriteSByte(0);                        // cổng
        w.WriteSByte(0);                        // quái
        w.WriteSByte(0);                        // byte bỏ
        w.WriteSByte(0);                        // NPC
        w.WriteSByte(0);                        // vật rơi
        w.WriteShort(0);                        // hình nền
        w.WriteShort(0);                        // khóa
        w.WriteSByte(0); w.WriteSByte(0);
        w.WriteSByte(0);                        // bf
        return w.ToArray();
    }

    /// <summary>-28 sub 10: sub, rộng, cao, ô lát, rồi 1 byte cuối (như bắt gói thật).</summary>
    public static byte[] Grid(int width, int height)
    {
        var w = new JavaStreamWriter();
        w.WriteSByte(10); w.WriteByte(width); w.WriteByte(height);
        w.WriteBytes(new byte[width * height]);
        w.WriteByte(0);
        return w.ToArray();
    }
}
