using System.IO;
using ToolQL.Protocol;
using ToolQL.Protocol.Messages;
using Xunit;

namespace ToolQL.Protocol.Tests;

/// <summary>
/// Gói dựng tay theo mô tả trong docs/protocol (đã đối chiếu mã dịch ngược). Chứng minh bộ đọc khớp tài liệu
/// và tiêu thụ đúng số byte; CHƯA chứng minh khớp server thật.
/// </summary>
public class MessageTests
{
    // ---------- -28 sub 4 ----------

    private static byte[] DungSub4(int nCaptions = 2)
    {
        var w = new JavaStreamWriter();
        w.WriteSByte(4);
        w.WriteSByte(1); w.WriteSByte(2); w.WriteSByte(3); w.WriteSByte(-4);
        w.WriteSByte(9);
        w.WriteSByte(nCaptions);
        for (int i = 0; i < nCaptions; i++) w.WriteLong(1000L + i);
        return w.ToArray();
    }

    [Fact]
    public void Sub4_doc_dung_va_tieu_thu_het()
    {
        var v = DataVersions.Parse(DungSub4());
        Assert.Equal((1, 2, 3, -4), (v.Data, v.Map, v.Skill, v.Item));
        Assert.Equal(9, v.Skipped);
        Assert.Equal(new long[] { 1000, 1001 }, v.Captions);
        Assert.Equal(0, v.ExtraBytes);
    }

    [Fact]
    public void Sub4_byte_thua_va_thieu()
    {
        Assert.Equal(3, DataVersions.Parse(DungSub4().Concat(new byte[] { 1, 2, 3 }).ToArray()).ExtraBytes);
        var full = DungSub4();
        Assert.Throws<InvalidDataException>(() => DataVersions.Parse(full[..^1]));
        Assert.Throws<InvalidDataException>(() => DataVersions.Parse(new byte[] { 5, 0, 0, 0, 0, 0, 0 })); // sub sai
    }

    // ---------- -82 ----------

    private static byte[] Dung82()
    {
        var w = new JavaStreamWriter();
        w.WriteSByte(2);                       // 2 bộ
        w.WriteSByte(2);                       // bộ 0: 2 cờ
        w.WriteInt(2); w.WriteSByte(3); w.WriteSByte(10); w.WriteSByte(11); w.WriteSByte(-5);
        w.WriteInt(64); w.WriteSByte(0);
        w.WriteSByte(1);                       // bộ 1: 1 cờ
        w.WriteInt(4); w.WriteSByte(1); w.WriteSByte(7);
        return w.ToArray();
    }

    [Fact]
    public void Bang_82_doc_dung_ma_o_co_dau()
    {
        var t = TileFlagTables.Parse(Dung82());
        Assert.Equal(2, t.Sets.Length);
        Assert.Equal(2, t.Sets[0][0].Value);
        Assert.Equal(new sbyte[] { 10, 11, -5 }, t.Sets[0][0].TileCodes);
        Assert.Empty(t.Sets[0][1].TileCodes);
        Assert.Equal(4, t.Sets[1][0].Value);
        Assert.Equal(0, t.ExtraBytes);
    }

    [Fact]
    public void Bang_82_so_luong_am_hoac_thieu_thi_loi()
    {
        Assert.Throws<InvalidDataException>(() => TileFlagTables.Parse(new byte[] { 0xFF }));
        var full = Dung82();
        Assert.Throws<InvalidDataException>(() => TileFlagTables.Parse(full[..^1]));
    }

    // ---------- -30 sub 0 ----------

    private static void GhiMuc(JavaStreamWriter w, short id, int qty, bool optionKhongDau, params (short, int)[] opts)
    {
        w.WriteShort(id);
        if (id == -1) return;
        w.WriteInt(qty); w.WriteUtf("a"); w.WriteUtf("bé");
        w.WriteByte(opts.Length);
        foreach (var (oid, ov) in opts) { w.WriteShort(oid); w.WriteInt(ov); }
    }

    private static byte[] Dung30(bool coTail = true)
    {
        var w = new JavaStreamWriter();
        w.WriteSByte(0);
        w.WriteInt(777); w.WriteSByte(5); w.WriteSByte(2); w.WriteShort(31); w.WriteUtf("Goku");
        w.WriteSByte(1); w.WriteSByte(0); w.WriteLong(123456789012L);
        w.WriteShort(10); w.WriteShort(20); w.WriteSByte(3);
        w.WriteSByte(2); w.WriteShort(100); w.WriteShort(101);   // 2 skill
        w.WriteLong(5_000_000_000L); w.WriteInt(11); w.WriteInt(22);
        // trang bị: 3 ô (có, trống, có 2 option)
        w.WriteSByte(3);
        GhiMuc(w, 5, 1, true);
        GhiMuc(w, -1, 0, true);
        GhiMuc(w, 6, 1, true, (50, 100), (-1, 0));
        // túi: 2 ô
        w.WriteSByte(2);
        GhiMuc(w, 7, 99, false, (1, 2));
        GhiMuc(w, -1, 0, false);
        // rương: 1 ô
        w.WriteSByte(1);
        GhiMuc(w, 8, 3, false);
        // m = 2 cặp, 3 short, byte, int giờ, byte
        w.WriteShort(2); w.WriteShort(1); w.WriteShort(2); w.WriteShort(3); w.WriteShort(4);
        w.WriteShort(7); w.WriteShort(8); w.WriteShort(9);
        w.WriteSByte(1); w.WriteInt(1_700_000_000); w.WriteSByte(6);
        if (coTail) { w.WriteShort(300); w.WriteSByte(-2); w.WriteShort(400); }
        return w.ToArray();
    }

    [Fact]
    public void Nhan_vat_30_doc_dung_va_tieu_thu_het()
    {
        var c = CharacterInfo.Parse(Dung30());
        Assert.Equal(777, c.Id);
        Assert.Equal("Goku", c.Name);
        Assert.Equal(2, c.Race);
        Assert.Equal(31, c.Head);
        Assert.Equal(123456789012L, c.Power);
        Assert.Equal(new short[] { 100, 101 }, c.SkillIds);
        Assert.Equal(5_000_000_000L, c.LongAi);
        Assert.Equal(3, c.Equipment.Length);
        Assert.Null(c.Equipment[1]);
        Assert.Equal(2, c.Equipment[2]!.Options.Length);
        Assert.Equal(new ItemOption(50, 100), c.Equipment[2]!.Options[0]);
        Assert.Equal("bé", c.Equipment[0]!.Text2);
        Assert.Equal(99, c.Bag[0]!.Quantity);
        Assert.Null(c.Bag[1]);
        Assert.Equal((short)8, c.Chest[0]!.ItemId);
        Assert.Equal(new (short, short)[] { (1, 2), (3, 4) }, c.Pairs);
        Assert.Equal(new short[] { 7, 8, 9 }, c.ThreeShorts);
        Assert.Equal(1_700_000_000, c.ServerTimeSeconds);
        Assert.Equal(((short)300, (sbyte)-2, (short)400), c.Tail);
        Assert.Equal(0, c.ExtraBytes);
    }

    [Fact]
    public void Nhan_vat_30_thieu_phan_cuoi_van_hop_le()
    {
        var c = CharacterInfo.Parse(Dung30(coTail: false));
        Assert.Null(c.Tail);
        Assert.Equal(0, c.ExtraBytes);
    }

    [Fact]
    public void Nhan_vat_30_cat_ngan_o_moi_diem_deu_bao_loi()
    {
        var full = Dung30(coTail: false);
        // Cắt bớt ở mọi độ dài trước đoạn cuối (đoạn cuối chính là phần bắt buộc: byte az)
        for (int len = 0; len < full.Length; len++)
        {
            var cut = full[..len];
            Assert.Throws<InvalidDataException>(() => CharacterInfo.Parse(cut));
        }
    }

    // ---------- -24 và -28 sub 10 ----------

    private static void GhiThanMap(JavaStreamWriter w, bool vatCoExtra = true)
    {
        w.WriteShort(120); w.WriteShort(240);                 // vị trí mình
        w.WriteSByte(2);                                      // 2 cổng
        w.WriteShort(0); w.WriteShort(10); w.WriteShort(20); w.WriteShort(30); w.WriteBool(false); w.WriteBool(true); w.WriteUtf("Làng Aru");
        w.WriteShort(1); w.WriteShort(2); w.WriteShort(3); w.WriteShort(4); w.WriteBool(false); w.WriteBool(false); w.WriteUtf("");
        w.WriteSByte(2);                                      // 2 quái
        for (int i = 0; i < 2; i++)
        {
            for (int b = 0; b < 5; b++) w.WriteBool(b == 1);
            w.WriteShort(70 + i); w.WriteSByte(5 + i); w.WriteLong(1000 + i); w.WriteSByte(0); w.WriteLong(2000 + i);
            w.WriteShort(300 + i); w.WriteShort(400 + i); w.WriteSByte(1); w.WriteSByte(2); w.WriteBool(i == 1);
        }
        w.WriteSByte(77);                                     // byte bỏ
        w.WriteSByte(1);                                      // 1 NPC
        w.WriteSByte(0); w.WriteShort(50); w.WriteShort(60); w.WriteSByte(13); w.WriteShort(1234);
        w.WriteSByte(vatCoExtra ? 2 : 1);                     // vật rơi
        w.WriteShort(1); w.WriteShort(2); w.WriteShort(3); w.WriteShort(4); w.WriteInt(99);
        if (vatCoExtra)
        {
            w.WriteShort(5); w.WriteShort(6); w.WriteShort(7); w.WriteShort(8); w.WriteInt(-2); w.WriteShort(42);
        }
        w.WriteShort(1); w.WriteShort(9); w.WriteShort(11); w.WriteShort(12);   // 1 hình nền
        w.WriteShort(2);                                                        // 2 khóa
        w.WriteUtf("eff"); w.WriteUtf("1.2.3.4"); w.WriteUtf("beff"); w.WriteUtf("5");
        w.WriteSByte(3);                                      // dP
        w.WriteSByte(4);                                      // byte đưa vào cj_0.a(byte)
    }

    private static byte[] Dung24(bool coBf = true, bool vatCoExtra = true)
    {
        var w = new JavaStreamWriter();
        w.WriteByte(200);                                     // mapId (không dấu)
        w.WriteSByte(1); w.WriteSByte(4); w.WriteSByte(0); w.WriteSByte(9);
        w.WriteUtf("Đảo Kame"); w.WriteSByte(2);
        GhiThanMap(w, vatCoExtra);
        if (coBf) w.WriteSByte(1);
        return w.ToArray();
    }

    [Fact]
    public void Map_24_doc_dung_va_tieu_thu_het()
    {
        var m = MapInfo.Parse(Dung24());
        Assert.Equal(200, m.Header.MapId);
        Assert.Equal(4, m.Header.TileSetId);
        Assert.Equal("Đảo Kame", m.Header.Name);
        Assert.Equal((short)120, m.Body.X);
        Assert.Equal(2, m.Body.Portals.Length);
        Assert.Equal("Làng Aru", m.Body.Portals[0].TargetName);
        Assert.True(m.Body.Portals[0].FlagB);
        Assert.Equal(2, m.Body.Mobs.Length);
        Assert.Equal(1, m.Body.Mobs[1].Index);
        Assert.Equal((short)71, m.Body.Mobs[1].Type);
        Assert.Equal(2001, m.Body.Mobs[1].HpMax);
        Assert.True(m.Body.Mobs[1].Special);
        Assert.False(m.Body.Mobs[0].Special);
        Assert.Equal(77, m.Body.SkippedByte);
        Assert.Equal((short)1234, m.Body.Npcs[0].Icon);
        Assert.Equal(2, m.Body.Items.Length);
        Assert.Null(m.Body.Items[0].Extra);
        Assert.Equal((short)42, m.Body.Items[1].Extra);
        Assert.Single(m.Body.Backgrounds);
        Assert.Equal(("beff", "5"), m.Body.Keys[1]);
        Assert.Equal(3, m.Body.UnknownDp);
        Assert.Equal(4, m.Body.FinalByte);
        Assert.Equal(1, m.Bf);
        Assert.Equal(0, m.ExtraBytes);
    }

    [Fact]
    public void Map_24_thieu_byte_bf_van_hop_le()
    {
        var m = MapInfo.Parse(Dung24(coBf: false));
        Assert.Null(m.Bf);
        Assert.Equal(0, m.ExtraBytes);
    }

    [Fact]
    public void Map_24_vat_roi_khong_extra_va_cat_ngan_thi_loi()
    {
        Assert.Equal(0, MapInfo.Parse(Dung24(vatCoExtra: false)).ExtraBytes);
        var full = Dung24(coBf: false);
        for (int len = 0; len < full.Length; len++)
        {
            var cut = full[..len];
            Assert.Throws<InvalidDataException>(() => MapInfo.Parse(cut));
        }
    }

    [Fact]
    public void Luoi_sub10_doc_khong_dau_va_tieu_thu_het()
    {
        var w = new JavaStreamWriter();
        w.WriteSByte(10); w.WriteByte(200); w.WriteByte(2);   // 200 > 127: không dấu
        var tiles = new byte[400];
        tiles[0] = 0xFF; tiles[399] = 7;
        w.WriteBytes(tiles);
        var g = MapGrid.Parse(w.ToArray());
        Assert.Equal((200, 2), (g.Width, g.Height));
        Assert.Equal(0xFF, g.Tiles[0]);
        Assert.Equal(7, g.Tiles[399]);
        Assert.Equal(0, g.ExtraBytes);
    }

    [Fact]
    public void Luoi_sub10_byte_thua_duoc_bao_va_thieu_thi_loi()
    {
        var w = new JavaStreamWriter();
        w.WriteSByte(10); w.WriteByte(2); w.WriteByte(2);
        w.WriteBytes(new byte[] { 1, 2, 3, 4, 9, 9 });
        Assert.Equal(1, MapGrid.Parse(w.ToArray()).ExtraBytes);   // 1 byte cuối vào Trailer, còn 1 byte thừa
        Assert.Throws<InvalidDataException>(() => MapGrid.Parse(new byte[] { 10, 2, 2, 1, 2, 3 }));
    }

    [Fact]
    public void Luoi_sub10_byte_cuoi_sau_luoi_duoc_doc_vao_Trailer()
    {
        // Bắt gói thật (map 23): sub, rộng 32, cao 20, 640 ô, rồi 1 byte 00. Ý nghĩa byte cuối chưa rõ.
        var w = new JavaStreamWriter();
        w.WriteSByte(10); w.WriteByte(2); w.WriteByte(2);
        w.WriteBytes(new byte[] { 1, 2, 3, 4 });
        w.WriteByte(0);
        var g = MapGrid.Parse(w.ToArray());
        Assert.Equal(0, g.ExtraBytes);
        Assert.Equal((byte)0, g.Trailer);
        Assert.Equal(4, g.Tiles.Length);

        var khongCo = new JavaStreamWriter();
        khongCo.WriteSByte(10); khongCo.WriteByte(1); khongCo.WriteByte(1); khongCo.WriteByte(5);
        var h = MapGrid.Parse(khongCo.ToArray());
        Assert.Null(h.Trailer);
        Assert.Equal(0, h.ExtraBytes);
    }

    // ---------- -5 ----------

    private static byte[] Dung5(bool coTail = true)
    {
        var w = new JavaStreamWriter();
        w.WriteInt(4242); w.WriteInt(0);
        w.WriteSByte(0); w.WriteBool(true); w.WriteSByte(2); w.WriteSByte(1); w.WriteSByte(1);
        w.WriteShort(30); w.WriteUtf("Vegeta");
        w.WriteLong(500); w.WriteLong(900);
        w.WriteShort(10); w.WriteShort(11); w.WriteShort(12);
        w.WriteSByte(0);
        w.WriteShort(321); w.WriteShort(654); w.WriteShort(0); w.WriteShort(0);
        w.WriteSByte(1); w.WriteSByte(3); w.WriteInt(4); w.WriteInt(5); w.WriteShort(6);
        w.WriteSByte(0); w.WriteSByte(0); w.WriteShort(-1); w.WriteSByte(8); w.WriteSByte(1);
        if (coTail) { w.WriteShort(1); w.WriteSByte(2); w.WriteShort(3); }
        return w.ToArray();
    }

    [Fact]
    public void Nguoi_choi_5_doc_dung_va_tieu_thu_het()
    {
        var p = PlayerAppear.Parse(Dung5());
        Assert.Equal(4242, p.Id);
        Assert.Equal("Vegeta", p.Info.Name);
        Assert.Equal(900, p.Info.HpMax);
        Assert.Equal((short)321, p.Info.X);
        Assert.Single(p.Info.Statuses);
        Assert.Equal(new PlayerStatus(3, 4, 5, 6), p.Info.Statuses[0]);
        Assert.Equal((short)-1, p.State);
        Assert.NotNull(p.Tail);
        Assert.Equal(0, p.ExtraBytes);
    }

    [Fact]
    public void Nguoi_choi_5_khong_tail_van_hop_le_va_cat_ngan_thi_loi()
    {
        var full = Dung5(coTail: false);
        Assert.Null(PlayerAppear.Parse(full).Tail);
        for (int len = 0; len < full.Length; len++)
        {
            var cut = full[..len];
            Assert.Throws<InvalidDataException>(() => PlayerAppear.Parse(cut));
        }
    }

    // ---------- -90 ----------

    [Fact]
    public void Doi_trang_phuc_90_doc_dung_va_tieu_thu_het()
    {
        var w = new JavaStreamWriter();
        w.WriteSByte(1); w.WriteInt(0x00E3D47D); w.WriteShort(252); w.WriteShort(253); w.WriteShort(254); w.WriteSByte(0);
        var bytes = w.ToArray();
        Assert.Equal(12, bytes.Length);   // như 76 gói bắt được

        var m = PlayerOutfitChanged.Parse(bytes);
        Assert.Equal((1, 0x00E3D47D, (short?)252, (short?)253, (short?)254), ((int)m.Kind, m.Id, m.Head, m.Body, m.Leg));
        Assert.Equal(0, m.ExtraBytes);
        Assert.Equal(3, PlayerOutfitChanged.Parse(bytes.Concat(new byte[] { 1, 2, 3 }).ToArray()).ExtraBytes);
        for (int len = 0; len < bytes.Length; len++)
        {
            var cut = bytes[..len];
            Assert.Throws<InvalidDataException>(() => PlayerOutfitChanged.Parse(cut));
        }
    }

    [Fact]
    public void Doi_trang_phuc_90_dang_5_byte_chi_co_id()
    {
        var w = new JavaStreamWriter();
        w.WriteSByte(-1); w.WriteInt(unchecked((int)0xFEB1E075));
        var m = PlayerOutfitChanged.Parse(w.ToArray());
        Assert.Equal((-1, unchecked((int)0xFEB1E075)), ((int)m.Kind, m.Id));
        Assert.Null(m.Head);
        Assert.Equal(0, m.ExtraBytes);
        Assert.Throws<InvalidDataException>(() => PlayerOutfitChanged.Parse(w.ToArray()[..4]));
    }
}
