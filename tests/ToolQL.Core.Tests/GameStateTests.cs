using ToolQL.Protocol;
using Xunit;

namespace ToolQL.Core.Tests;

/// <summary>
/// Áp chuỗi gói dựng tay theo docs/protocol vào <see cref="GameState"/>. Chứng minh logic dựng trạng thái;
/// CHƯA chứng minh khớp server thật (cần lượt chạy M1 với acc thử).
/// </summary>
public class GameStateTests
{
    // ---------- dựng gói ----------

    private static Packet P(sbyte command, JavaStreamWriter w) => new(command, w.ToArray());

    private static Packet Character(int id = 1000, string name = "Tester")
    {
        var w = new JavaStreamWriter();
        w.WriteSByte(0);
        w.WriteInt(id); w.WriteSByte(0); w.WriteSByte(2); w.WriteShort(5); w.WriteUtf(name);
        w.WriteSByte(0); w.WriteSByte(0); w.WriteLong(123);
        w.WriteShort(0); w.WriteShort(0); w.WriteSByte(1);
        w.WriteSByte(1); w.WriteShort(7);                       // 1 skill
        w.WriteLong(500); w.WriteInt(30); w.WriteInt(20);       // aI, cQ, gF
        w.WriteSByte(1); w.WriteShort(-1);                      // trang bị: 1 ô trống
        w.WriteSByte(0); w.WriteSByte(0);                       // túi, rương rỗng
        w.WriteShort(0);                                        // cặp short
        w.WriteShort(0); w.WriteShort(0); w.WriteShort(0);
        w.WriteSByte(0); w.WriteInt(1234); w.WriteSByte(0);
        return P(-30, w);
    }

    private static Packet Map(int mapId, int mobs = 2, params short[] itemIds)
    {
        var w = new JavaStreamWriter();
        w.WriteByte(mapId); w.WriteSByte(1); w.WriteSByte(4); w.WriteSByte(0); w.WriteSByte(9);
        w.WriteUtf("Map " + mapId); w.WriteSByte(2);
        w.WriteShort(100); w.WriteShort(200);
        w.WriteSByte(0);                                        // cổng
        w.WriteSByte(mobs);
        for (int i = 0; i < mobs; i++)
        {
            for (int f = 0; f < 5; f++) w.WriteBool(false);
            w.WriteShort(3); w.WriteSByte(1); w.WriteLong(1000); w.WriteSByte(0); w.WriteLong(1000);
            w.WriteShort(50 + i * 10); w.WriteShort(60); w.WriteSByte(0); w.WriteSByte(0); w.WriteBool(false);
        }
        w.WriteSByte(0);
        w.WriteSByte(1);                                        // 1 NPC
        w.WriteSByte(0); w.WriteShort(10); w.WriteShort(20); w.WriteSByte(3); w.WriteShort(4);
        w.WriteSByte(itemIds.Length);
        foreach (var id in itemIds) { w.WriteShort(id); w.WriteShort(1); w.WriteShort(30); w.WriteShort(40); w.WriteInt(-1); }
        w.WriteShort(0); w.WriteShort(0);
        w.WriteSByte(0); w.WriteSByte(0); w.WriteSByte(0);
        return P(-24, w);
    }

    private static Packet Grid(int wd, int ht)
    {
        var w = new JavaStreamWriter();
        w.WriteSByte(10); w.WriteByte(wd); w.WriteByte(ht);
        w.WriteBytes(new byte[wd * ht]);
        w.WriteByte(0);
        return P(-28, w);
    }

    private static Packet Appear(int id, string name, short x, short y, long hp = 800)
    {
        var w = new JavaStreamWriter();
        w.WriteInt(id); w.WriteInt(0);
        w.WriteSByte(0); w.WriteBool(false); w.WriteSByte(0); w.WriteSByte(1); w.WriteSByte(2);
        w.WriteShort(1); w.WriteUtf(name); w.WriteLong(hp); w.WriteLong(1000);
        w.WriteShort(2); w.WriteShort(3); w.WriteShort(4); w.WriteSByte(0);
        w.WriteShort(x); w.WriteShort(y); w.WriteShort(0); w.WriteShort(0);
        w.WriteSByte(0);                                        // trạng thái
        w.WriteSByte(0); w.WriteSByte(0); w.WriteShort(0); w.WriteSByte(0); w.WriteSByte(0);
        return P(-5, w);
    }

    private static Packet Moved(int id, short x, short y)
    {
        var w = new JavaStreamWriter(); w.WriteInt(id); w.WriteShort(x); w.WriteShort(y);
        return P(-7, w);
    }

    private static Packet Left(int id)
    {
        var w = new JavaStreamWriter(); w.WriteInt(id);
        return P(-6, w);
    }

    private static Packet MobHit(int idx, long hp)
    {
        var w = new JavaStreamWriter();
        w.WriteByte(idx); w.WriteLong(hp); w.WriteLong(10); w.WriteBool(false); w.WriteSByte(0);
        return P(-9, w);
    }

    private static Packet MobDeath(int idx, params short[] dropIds)
    {
        var w = new JavaStreamWriter();
        w.WriteByte(idx); w.WriteLong(10); w.WriteBool(false);
        w.WriteSByte(dropIds.Length);
        foreach (var id in dropIds) { w.WriteShort(id); w.WriteShort(1); w.WriteShort(55); w.WriteShort(66); w.WriteInt(-1); }
        return P(-12, w);
    }

    private static Packet Respawn(int idx, long hp)
    {
        var w = new JavaStreamWriter(); w.WriteByte(idx); w.WriteSByte(0); w.WriteSByte(0); w.WriteLong(hp);
        return P(-13, w);
    }

    private static Packet MeHp(long hp)
    {
        var w = new JavaStreamWriter(); w.WriteSByte(5); w.WriteLong(hp);
        return P(-30, w);
    }

    private static Packet ItemGone(short id)
    {
        var w = new JavaStreamWriter(); w.WriteShort(id);
        return P(-21, w);
    }

    private static GameState InMap(params short[] itemIds)
    {
        var g = new GameState();
        g.Apply(Character());
        g.Apply(Map(14, 2, itemIds));
        g.Apply(Grid(8, 4));
        return g;
    }

    // ---------- test ----------

    [Fact]
    public void Nhan_vat_roi_map_dat_vi_tri_va_luoi()
    {
        var s = InMap().Snapshot();
        Assert.Equal("Tester", s.Me!.Name);
        Assert.Equal((100, 200), ((int)s.Me.X, (int)s.Me.Y));
        Assert.Equal((500L, 20, 30), (s.Me.MoneyA, s.Me.MoneyB, s.Me.MoneyC));
        Assert.Equal(14, s.Map!.Id);
        Assert.Equal((8, 4, 32), (s.Map.Width, s.Map.Height, s.Map.Tiles!.Length));
        Assert.Equal(2, s.Mobs.Count);
        Assert.Single(s.Map.Npcs);
        Assert.Equal(0, s.ParseErrors);
    }

    [Fact]
    public void Nguoi_choi_xuat_hien_di_chuyen_roi_di()
    {
        var g = InMap();
        g.Apply(Appear(7, "Bạn", 10, 20));
        g.Apply(Moved(7, 33, 44));
        var p = Assert.Single(g.Snapshot().Players);
        Assert.Equal(("Bạn", (short)33, (short)44), (p.Info.Name, p.X, p.Y));

        g.Apply(Left(7));
        Assert.Empty(g.Snapshot().Players);
    }

    [Fact]
    public void Doi_trang_phuc_cap_nhat_dau_than_chan_cua_dung_nguoi()
    {
        var g = InMap();
        g.Apply(Appear(7, "Bạn", 10, 20));
        g.Apply(Appear(8, "Khác", 30, 40));
        var w = new JavaStreamWriter();
        w.WriteSByte(1); w.WriteInt(7); w.WriteShort(252); w.WriteShort(253); w.WriteShort(254); w.WriteSByte(0);
        g.Apply(P(-90, w));

        var players = g.Snapshot().Players.ToDictionary(x => x.Id);
        Assert.Equal(((short)252, (short)253, (short)254), (players[7].Info.Head, players[7].Info.Body, players[7].Info.Leg));
        Assert.Equal(((short)1, (short)2, (short)3), (players[8].Info.Head, players[8].Info.Body, players[8].Info.Leg));   // người khác không đổi
        Assert.Equal(0, g.Snapshot().ParseErrors);
        Assert.DoesNotContain(-90, g.Snapshot().UnhandledPackets.Keys);
    }

    [Fact]
    public void Doi_trang_phuc_dang_5_byte_duoc_dem_khong_loi_va_khong_doi_trang_thai()
    {
        var g = InMap();
        g.Apply(Appear(7, "Bạn", 10, 20));
        var w = new JavaStreamWriter();
        w.WriteSByte(-1); w.WriteInt(7);
        g.Apply(P(-90, w));

        var s = g.Snapshot();
        Assert.Equal(0, s.ParseErrors);
        Assert.Equal(1, s.UnhandledPackets[-90]);
        Assert.Equal((short)1, s.Players.Single().Info.Head);
    }

    [Fact]
    public void Doi_trang_phuc_nguoi_chua_biet_thi_bo_qua_khong_loi()
    {
        var g = InMap();
        var w = new JavaStreamWriter();
        w.WriteSByte(1); w.WriteInt(99); w.WriteShort(1); w.WriteShort(2); w.WriteShort(3); w.WriteSByte(0);
        g.Apply(P(-90, w));
        Assert.Empty(g.Snapshot().Players);
        Assert.Equal(0, g.Snapshot().ParseErrors);
    }

    [Fact]
    public void Di_chuyen_nguoi_chua_biet_thi_bo_qua()
    {
        var g = InMap();
        g.Apply(Moved(99, 1, 1));
        Assert.Empty(g.Snapshot().Players);
        Assert.Equal(0, g.Snapshot().ParseErrors);
    }

    [Fact]
    public void Vao_map_moi_xoa_nguoi_quai_vat_roi_cu()
    {
        var g = InMap(5);
        g.Apply(Appear(7, "Bạn", 1, 1));
        g.Apply(Map(21, 1));
        var s = g.Snapshot();
        Assert.Equal(21, s.Map!.Id);
        Assert.Null(s.Map.Tiles);          // lưới của map mới chưa tới
        Assert.Empty(s.Players);
        Assert.Empty(s.Items);
        Assert.Single(s.Mobs);
    }

    [Fact]
    public void Quai_bi_danh_chet_roi_hoi_sinh()
    {
        var g = InMap();
        g.Apply(MobHit(1, 400));
        Assert.Equal(400, g.Snapshot().Mobs[1].Hp);

        g.Apply(MobDeath(1, 77));
        var dead = g.Snapshot();
        Assert.False(dead.Mobs[1].Alive);
        Assert.Equal(0, dead.Mobs[1].Hp);
        Assert.True(dead.Mobs[0].Alive);
        Assert.Equal((short)77, Assert.Single(dead.Items).A);

        g.Apply(Respawn(1, 1000));
        var back = g.Snapshot().Mobs[1];
        Assert.True(back.Alive);
        Assert.Equal(1000, back.Hp);
    }

    [Fact]
    public void Chi_so_quai_ngoai_danh_sach_bi_bo_qua()
    {
        var g = InMap();
        g.Apply(MobHit(9, 1));
        Assert.Equal(2, g.Snapshot().Mobs.Count);
        Assert.Equal(0, g.Snapshot().ParseErrors);
    }

    [Fact]
    public void Vat_roi_bien_mat_khi_bi_nhat()
    {
        var g = InMap(5, 6);
        g.Apply(ItemGone(5));
        Assert.Equal((short)6, Assert.Single(g.Snapshot().Items).A);
    }

    [Fact]
    public void Hp_cua_minh_theo_goi_30_sub_5()
    {
        var g = InMap();
        g.Apply(MeHp(321));
        Assert.Equal(321, g.Snapshot().Me!.Hp);
    }

    [Fact]
    public void Goi_la_duoc_dem_khong_lam_hong_phien()
    {
        var g = InMap();
        g.Apply(new Packet(-111, new byte[] { 1, 2 }));
        g.Apply(new Packet(-111, new byte[] { 3 }));
        g.Apply(new Packet(-30, new byte[] { 8, 0, 0, 0, 1, 0 }));    // sub 8: đọc được nhưng chưa dùng
        var u = g.Snapshot().UnhandledPackets;
        Assert.Equal(2, u[-111]);
        Assert.Equal(1, u[-30 * 256 + 8]);
    }

    [Fact]
    public void Goi_hong_duoc_dem_loi_va_trang_thai_giu_nguyen()
    {
        var g = InMap();
        long before = g.Version;
        g.Apply(new Packet(-9, new byte[] { 0, 1 }));        // thiếu dữ liệu
        var s = g.Snapshot();
        Assert.Equal(1, s.ParseErrors);
        Assert.Equal(before, s.Version);
        Assert.Equal(1000, s.Mobs[0].Hp);
    }

    [Fact]
    public void Snapshot_la_ban_sao_khong_doi_theo_goi_sau()
    {
        var g = InMap();
        g.Apply(Appear(7, "Bạn", 1, 1));
        var before = g.Snapshot();
        g.Apply(Moved(7, 99, 99));
        g.Apply(MobHit(0, 5));
        Assert.Equal((short)1, before.Players[0].X);
        Assert.Equal(1000, before.Mobs[0].Hp);
        before.Map!.Tiles![0] = 9;
        Assert.Equal(0, g.Snapshot().Map!.Tiles![0]);
    }

    [Fact]
    public void Reset_xoa_het()
    {
        var g = InMap();
        g.Reset();
        var s = g.Snapshot();
        Assert.Null(s.Me);
        Assert.Null(s.Map);
        Assert.Empty(s.Mobs);
    }

    [Fact]
    public void Version_chi_tang_khi_goi_da_xu_ly()
    {
        var g = InMap();
        long v = g.Version;
        g.Apply(new Packet(-90, new byte[] { 1 }));
        Assert.Equal(v, g.Version);
        g.Apply(MobHit(0, 5));
        Assert.True(g.Version > v);
    }
}
