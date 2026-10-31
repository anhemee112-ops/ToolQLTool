namespace ToolQL.Core.Tests;

public class BackoffTests
{
    [Fact]
    public void Tang_gap_doi_va_dung_o_5_phut()
    {
        var b = new Backoff();
        var got = Enumerable.Range(0, 6).Select(_ => (int)b.Next().TotalSeconds).ToArray();
        Assert.Equal(new[] { 40, 80, 160, 300, 300, 300 }, got);
    }

    [Fact]
    public void Reset_quay_ve_ban_dau()
    {
        var b = new Backoff();
        b.Next(); b.Next(); b.Next();
        b.Reset();
        Assert.Equal(TimeSpan.FromSeconds(40), b.Next());
    }

    [Fact]
    public void Khong_tran_so_khi_thu_rat_nhieu_lan()
    {
        var b = new Backoff();
        for (int i = 0; i < 1000; i++) b.Next();
        Assert.Equal(TimeSpan.FromMinutes(5), b.Next());
    }
}
