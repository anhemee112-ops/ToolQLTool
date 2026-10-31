namespace ToolQL.Core.Tests;

public class DownloadProgressTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private static DownloadProgress Make() => new(T0, quietFor: TimeSpan.FromSeconds(4), timeout: TimeSpan.FromSeconds(120));

    [Fact]
    public void Chua_thay_du_lieu_doi_thi_van_cho()
    {
        var p = Make();
        Assert.Equal(DownloadStatus.Waiting, p.Observe(0, 0, T0));
        Assert.Equal(DownloadStatus.Waiting, p.Observe(0, 0, T0.AddSeconds(30)));
        Assert.False(p.Changed);
    }

    [Fact]
    public void Co_du_lieu_roi_yen_lang_du_lau_thi_xong()
    {
        var p = Make();
        p.Observe(0, 0, T0);
        Assert.Equal(DownloadStatus.Waiting, p.Observe(120, 500_000, T0.AddSeconds(5)));     // vừa đổi
        Assert.True(p.Changed);
        Assert.Equal(DownloadStatus.Waiting, p.Observe(120, 500_000, T0.AddSeconds(8)));     // yên lặng 3 giây, chưa đủ 4
        Assert.Equal(DownloadStatus.Done, p.Observe(120, 500_000, T0.AddSeconds(9)));
    }

    [Fact]
    public void Dang_tai_tiep_thi_dong_ho_yen_lang_tinh_lai()
    {
        var p = Make();
        p.Observe(0, 0, T0);
        p.Observe(10, 1_000, T0.AddSeconds(5));
        Assert.Equal(DownloadStatus.Waiting, p.Observe(40, 9_000, T0.AddSeconds(8)));        // đổi tiếp
        Assert.Equal(DownloadStatus.Waiting, p.Observe(40, 9_000, T0.AddSeconds(11)));
        Assert.Equal(DownloadStatus.Done, p.Observe(40, 9_000, T0.AddSeconds(12)));
    }

    [Fact]
    public void Du_lieu_doi_chi_o_dung_luong_van_tinh_la_dang_tai()
    {
        var p = Make();
        p.Observe(5, 100, T0);
        p.Observe(5, 900, T0.AddSeconds(2));
        Assert.True(p.Changed);
    }

    [Fact]
    public void Qua_han_ma_khong_xong_thi_het_gio()
    {
        var p = Make();
        p.Observe(0, 0, T0);
        Assert.Equal(DownloadStatus.Waiting, p.Observe(0, 0, T0.AddSeconds(119)));
        Assert.Equal(DownloadStatus.TimedOut, p.Observe(0, 0, T0.AddSeconds(120)));
    }

    [Fact]
    public void Xong_truoc_han_thi_khong_bi_tinh_het_gio()
    {
        var p = Make();
        p.Observe(0, 0, T0);
        p.Observe(50, 777, T0.AddSeconds(100));
        Assert.Equal(DownloadStatus.Done, p.Observe(50, 777, T0.AddSeconds(105)));
    }
}
