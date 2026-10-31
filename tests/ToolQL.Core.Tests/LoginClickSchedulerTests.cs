namespace ToolQL.Core.Tests;

public class LoginClickSchedulerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(12);

    [Fact]
    public void Chua_den_han_thi_chua_bam_den_han_thi_tra_acc_do()
    {
        var s = new LoginClickScheduler(Delay);
        var a = Guid.NewGuid();
        var running = new[] { (a, T0) };

        Assert.Null(s.NextDue(running, T0 + TimeSpan.FromSeconds(11)));
        Assert.Equal(a, s.NextDue(running, T0 + TimeSpan.FromSeconds(12)));
    }

    [Fact]
    public void Moi_lan_goi_toi_da_mot_acc_cai_mo_som_nhat_truoc()
    {
        var s = new LoginClickScheduler(Delay);
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var running = new[] { (b, T0 + TimeSpan.FromSeconds(3)), (a, T0) };
        var now = T0 + TimeSpan.FromSeconds(30);

        Assert.Equal(a, s.NextDue(running, now));
        s.MarkDone(a, T0);
        Assert.Equal(b, s.NextDue(running, now));
        s.MarkDone(b, T0 + TimeSpan.FromSeconds(3));
        Assert.Null(s.NextDue(running, now));
    }

    [Fact]
    public void Da_bam_cho_lan_chay_nay_thi_khong_bam_lai_nhung_lan_chay_moi_thi_bam_lai()
    {
        var s = new LoginClickScheduler(Delay);
        var a = Guid.NewGuid();
        s.MarkDone(a, T0);

        Assert.Null(s.NextDue(new[] { (a, T0) }, T0 + TimeSpan.FromMinutes(5)));

        var restarted = T0 + TimeSpan.FromMinutes(1);   // mở lại sau khi thoát: StartedAt mới
        Assert.Equal(a, s.NextDue(new[] { (a, restarted) }, restarted + Delay));
    }

    [Fact]
    public void Acc_khong_con_chay_thi_bi_don_khoi_bo_nho()
    {
        var s = new LoginClickScheduler(Delay);
        var a = Guid.NewGuid();
        s.MarkDone(a, T0);
        Assert.Equal(1, s.TrackedCount);

        s.NextDue(Array.Empty<(Guid, DateTimeOffset)>(), T0 + TimeSpan.FromMinutes(1));

        Assert.Equal(0, s.TrackedCount);
    }
}
