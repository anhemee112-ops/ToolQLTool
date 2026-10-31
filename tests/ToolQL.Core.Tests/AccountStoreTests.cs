using System.Security.Cryptography;
using ToolQL.Core;
using Xunit;

namespace ToolQL.Core.Tests;

public class AccountStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "toolql-acc-" + Guid.NewGuid().ToString("N"));
    private string PathFile => Path.Combine(_dir, "accounts.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    /// <summary>Mã hóa giả có thể "đổi user": chuỗi bắt đầu bằng khóa hiện tại thì mới giải được.</summary>
    private sealed class FakeProtector : IPasswordProtector
    {
        public string Key = "A";
        public string Protect(string plain) => Key + ":" + plain.Reverse().Aggregate("", (s, c) => s + c);
        public string Unprotect(string text) =>
            text.StartsWith(Key + ":") ? new string(text[(Key.Length + 1)..].Reverse().ToArray())
                                       : throw new CryptographicException("sai khóa");
    }

    [Fact]
    public void Them_roi_nap_lai_giu_du_lieu_va_giai_ma_dung()
    {
        var protector = new FakeProtector();
        var a = new AccountStore(PathFile, protector);
        var entry = a.Add("nguoi1", "mat-khau-bi-mat", "Vũ trụ 1", "acc chính");

        var b = new AccountStore(PathFile, protector);
        b.Load();
        var loaded = Assert.Single(b.Entries);
        Assert.Equal(("nguoi1", "Vũ trụ 1", "acc chính"), (loaded.User, loaded.Server, loaded.Note));
        Assert.Equal(new AccountCredentials("nguoi1", "mat-khau-bi-mat"), b.GetCredentials(entry.Id));
    }

    [Fact]
    public void Mat_khau_that_khong_nam_trong_file_va_khong_in_ra()
    {
        var store = new AccountStore(PathFile, new FakeProtector());
        var entry = store.Add("nguoi1", "mat-khau-bi-mat", "Vũ trụ 1");

        Assert.DoesNotContain("mat-khau-bi-mat", File.ReadAllText(PathFile));
        Assert.DoesNotContain(entry.ProtectedPassword, entry.ToString());
        Assert.DoesNotContain("mat-khau-bi-mat", entry.ToString());
    }

    [Fact]
    public void Khong_cho_trung_user_tren_cung_server_nhung_cho_server_khac()
    {
        var store = new AccountStore(PathFile, new FakeProtector());
        store.Add("nguoi1", "x", "Vũ trụ 1");
        Assert.Throws<InvalidOperationException>(() => store.Add("nguoi1", "y", "Vũ trụ 1"));
        store.Add("nguoi1", "y", "Vũ trụ 2");
        Assert.Equal(2, store.Entries.Count);
        Assert.Throws<ArgumentException>(() => store.Add("  ", "y", "Vũ trụ 1"));
    }

    [Fact]
    public void Sua_giu_mat_khau_cu_khi_khong_dua_mat_khau_moi()
    {
        var store = new AccountStore(PathFile, new FakeProtector());
        var e = store.Add("nguoi1", "cu", "Vũ trụ 1");

        store.Update(e.Id, "Vũ trụ 3", "ghi chú mới");
        Assert.Equal("cu", store.GetCredentials(e.Id).Password);
        Assert.Equal("Vũ trụ 3", store.Entries[0].Server);

        store.Update(e.Id, "Vũ trụ 3", "ghi chú mới", newPassword: "moi");
        Assert.Equal("moi", store.GetCredentials(e.Id).Password);
    }

    [Fact]
    public void Sua_thanh_trung_acc_khac_bi_tu_choi()
    {
        var store = new AccountStore(PathFile, new FakeProtector());
        store.Add("nguoi1", "x", "Vũ trụ 1");
        var e2 = store.Add("nguoi1", "x", "Vũ trụ 2");
        Assert.Throws<InvalidOperationException>(() => store.Update(e2.Id, "Vũ trụ 1", ""));
    }

    [Fact]
    public void Xoa_va_id_la_khong_co()
    {
        var store = new AccountStore(PathFile, new FakeProtector());
        var e = store.Add("nguoi1", "x", "Vũ trụ 1");
        store.Remove(e.Id);
        Assert.Empty(store.Entries);
        Assert.Throws<KeyNotFoundException>(() => store.Remove(e.Id));

        var reloaded = new AccountStore(PathFile, new FakeProtector());
        reloaded.Load();
        Assert.Empty(reloaded.Entries);
    }

    [Fact]
    public void Chua_co_file_thi_danh_sach_rong()
    {
        var store = new AccountStore(PathFile, new FakeProtector());
        store.Load();
        Assert.Empty(store.Entries);
    }

    [Fact]
    public void File_hong_thi_bao_loi_va_khong_bi_ghi_de()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathFile, "{ không phải json");
        var store = new AccountStore(PathFile, new FakeProtector());
        Assert.Throws<InvalidDataException>(() => store.Load());
        Assert.Equal("{ không phải json", File.ReadAllText(PathFile));
    }

    [Fact]
    public void File_chep_tu_user_khac_thi_bao_nhap_lai_mat_khau()
    {
        var protector = new FakeProtector();
        var store = new AccountStore(PathFile, protector);
        var e = store.Add("nguoi1", "x", "Vũ trụ 1");

        protector.Key = "B";   // như đổi user Windows
        var ex = Assert.Throws<InvalidDataException>(() => store.GetCredentials(e.Id));
        Assert.Contains("nhập lại mật khẩu", ex.Message);
        Assert.DoesNotContain("x:", ex.Message);
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void Dpapi_ma_hoa_roi_giai_ma_lai_dung_va_khong_lo_ban_ro()
    {
        if (!OperatingSystem.IsWindows()) return;   // DPAPI chỉ có trên Windows
        var p = new DpapiPasswordProtector();
        string secret = "mật-khẩu-thử-123";
        string stored = p.Protect(secret);
        Assert.DoesNotContain(secret, stored);
        Assert.Equal(secret, p.Unprotect(stored));
        Assert.Throws<CryptographicException>(() => p.Unprotect("khong-phai-base64!"));
    }
}
