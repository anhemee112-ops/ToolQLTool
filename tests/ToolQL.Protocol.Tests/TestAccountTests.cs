using System.IO;
using ToolQL.Probe;
using Xunit;

namespace ToolQL.Protocol.Tests;

/// <summary>Dùng tài khoản GIẢ trong file tạm; không đụng file thật và không nối mạng.</summary>
public class TestAccountTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "toolql-acc-" + Guid.NewGuid().ToString("N") + ".env");

    public void Dispose()
    {
        if (File.Exists(_file)) File.Delete(_file);
    }

    [Fact]
    public void Doc_ba_khoa_bo_qua_chu_thich_va_dau_nhay()
    {
        File.WriteAllText(_file, "# ghi chú\n\nTOOLQL_TEST_USER = gia-user\nTOOLQL_TEST_PASS=\"gia=pass\"\nTOOLQL_TEST_SERVER=Vũ trụ 2\n");
        var a = TestAccount.Load(_file);
        Assert.Equal("gia-user", a.User);
        Assert.Equal("gia=pass", a.Password);
        Assert.Equal("Vũ trụ 2", a.Server);
    }

    [Fact]
    public void Server_trong_la_null()
    {
        File.WriteAllText(_file, "TOOLQL_TEST_USER=u\nTOOLQL_TEST_PASS=p\nTOOLQL_TEST_SERVER=\n");
        Assert.Null(TestAccount.Load(_file).Server);
    }

    [Fact]
    public void Thieu_ten_hoac_mat_khau_thi_loi()
    {
        File.WriteAllText(_file, "TOOLQL_TEST_USER=\nTOOLQL_TEST_PASS=\n");
        Assert.Throws<InvalidDataException>(() => TestAccount.Load(_file));
        File.WriteAllText(_file, "TOOLQL_TEST_USER=u\n");
        Assert.Throws<InvalidDataException>(() => TestAccount.Load(_file));
    }

    [Fact]
    public void Khong_co_file_thi_loi()
    {
        Assert.Throws<FileNotFoundException>(() => TestAccount.Load(_file + ".khong-co"));
    }

    [Fact]
    public void ToString_khong_lo_mat_khau()
    {
        var text = new TestAccount("u", "bi-mat-xyz", null).ToString();
        Assert.DoesNotContain("bi-mat-xyz", text);
    }
}
