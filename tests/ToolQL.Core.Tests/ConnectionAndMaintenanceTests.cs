namespace ToolQL.Core.Tests;

public class ConnectionWatcherTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset At(int seconds) => T0 + TimeSpan.FromSeconds(seconds);

    private static ConnectionWatcher Make() => new(stableAfter: TimeSpan.FromSeconds(20), lostAfter: TimeSpan.FromSeconds(15));

    [Fact]
    public void Ket_noi_on_dinh_roi_mat_du_lau_thi_bao_mat_ket_noi_dung_mot_lan()
    {
        var w = Make();
        for (int s = 0; s <= 25; s++) Assert.False(w.Observe(At(s), true));
        Assert.True(w.WasStable);

        Assert.False(w.Observe(At(26), false));
        Assert.False(w.Observe(At(40), false));      // mới 14 giây
        Assert.True(w.Observe(At(41), false));       // đủ 15 giây
        Assert.False(w.Observe(At(42), false));      // không báo lại
    }

    [Fact]
    public void Chua_tung_on_dinh_thi_mat_ket_noi_khong_tinh()
    {
        var w = Make();
        for (int s = 0; s <= 10; s++) w.Observe(At(s), true);   // chỉ 10 giây (ví dụ lúc client mới mở nối thử)
        Assert.False(w.WasStable);

        for (int s = 11; s <= 200; s++) Assert.False(w.Observe(At(s), false));
    }

    [Fact]
    public void Ket_noi_quay_lai_truoc_han_thi_khong_mat()
    {
        var w = Make();
        for (int s = 0; s <= 25; s++) w.Observe(At(s), true);
        w.Observe(At(26), false);
        Assert.False(w.Observe(At(35), false));
        Assert.False(w.Observe(At(36), true));        // nối lại sau 10 giây
        Assert.False(w.Observe(At(60), true));
        Assert.False(w.Observe(At(61), false));       // đếm lại từ đầu
        Assert.False(w.Observe(At(70), false));
        Assert.True(w.Observe(At(76), false));
    }

    [Fact]
    public void Khong_xac_dinh_thi_bo_qua_va_ghi_nho_lan_quan_sat_cuoi()
    {
        var w = Make();
        for (int s = 0; s <= 25; s++) w.Observe(At(s), true);

        Assert.False(w.Observe(At(30), null));
        Assert.False(w.Observe(At(300), null));       // không xác định dù bao lâu cũng không phải mất kết nối
        Assert.True(w.Connected);

        w.Observe(At(301), false);
        Assert.False(w.Connected);
    }
}

public class MaintenanceScheduleTests
{
    private static DateTimeOffset Vn(int hour, int minute) =>
        new(new DateTime(2026, 11, 1, hour, minute, 0), TimeSpan.FromHours(7));   // giờ Việt Nam (UTC+7)

    [Fact]
    public void Khung_04h_den_05h_theo_gio_Viet_Nam_ke_ca_khi_may_o_mui_gio_khac()
    {
        var s = MaintenanceSchedule.Create(true, "04:00", "05:00")!;

        Assert.False(s.IsMaintenance(Vn(3, 59)));
        Assert.True(s.IsMaintenance(Vn(4, 0)));
        Assert.True(s.IsMaintenance(Vn(4, 59)));
        Assert.False(s.IsMaintenance(Vn(5, 0)));
        // cùng thời điểm biểu diễn ở UTC (21:00 ngày trước) vẫn phải là bảo trì
        Assert.True(s.IsMaintenance(Vn(4, 30).ToUniversalTime()));
    }

    [Fact]
    public void Khung_qua_dem_va_tat_thi_khong_bao_tri()
    {
        var overnight = MaintenanceSchedule.Create(true, "23:30", "01:00")!;
        Assert.True(overnight.IsMaintenance(Vn(23, 45)));
        Assert.True(overnight.IsMaintenance(Vn(0, 30)));
        Assert.False(overnight.IsMaintenance(Vn(1, 0)));
        Assert.False(overnight.IsMaintenance(Vn(12, 0)));

        Assert.False(MaintenanceSchedule.Create(false, "04:00", "05:00")!.IsMaintenance(Vn(4, 30)));
        Assert.False(MaintenanceSchedule.Create(true, "04:00", "04:00")!.IsMaintenance(Vn(4, 0)));   // rộng 0: không có khung
    }

    [Fact]
    public void Gio_sai_dinh_dang_thi_khong_tao_duoc()
    {
        Assert.Null(MaintenanceSchedule.Create(true, "25:00", "05:00"));
        Assert.Null(MaintenanceSchedule.Create(true, "04:00", "năm giờ"));
        Assert.NotNull(MaintenanceSchedule.Create(true, "4:00", "5:00"));
    }

    [Fact]
    public void Mo_ta_khung_gio_de_hien_thi()
    {
        Assert.Equal("04:00-05:00", MaintenanceSchedule.Create(true, "4:00", "05:00")!.Describe());
    }
}
