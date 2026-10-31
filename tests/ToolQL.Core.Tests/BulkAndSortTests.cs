using ToolQL.Protocol;

namespace ToolQL.Core.Tests;

public class BulkAccountParserTests
{
    [Fact]
    public void Doc_dong_TK_SV_MK_va_co_ghi_chu_bo_dong_trong_va_dong_chu_thich()
    {
        var r = BulkAccountParser.Parse("# danh sách\nnick01|1|123456\n\n  nick02|2|abc def|ghi chú 2  \n");

        Assert.Empty(r.Errors);
        Assert.Equal(2, r.Accounts.Count);
        Assert.Equal(new ParsedAccount("nick01", ServerList.All[0].Name, "123456", ""), r.Accounts[0]);
        Assert.Equal(new ParsedAccount("nick02", ServerList.All[1].Name, "abc def", "ghi chú 2"), r.Accounts[1]);
    }

    [Fact]
    public void Moi_dong_sai_deu_bao_kem_so_dong_va_ly_do_va_khong_tra_acc_nao()
    {
        var r = BulkAccountParser.Parse("ok|1|mk\n|1|mk\nthieu|1\nsv|99|mk\nsv|x|mk\nthua|1|mk|ghi chu|them\n" + "ok|1|mk2");

        Assert.Empty(r.Accounts);   // có dòng sai là chưa thêm acc nào
        Assert.Contains(r.Errors, e => e.StartsWith("Dòng 2:") && e.Contains("tên đăng nhập"));
        Assert.Contains(r.Errors, e => e.StartsWith("Dòng 3:") && e.Contains("mật khẩu"));
        Assert.Contains(r.Errors, e => e.StartsWith("Dòng 4:") && e.Contains("99"));
        Assert.Contains(r.Errors, e => e.StartsWith("Dòng 5:") && e.Contains("x"));
        Assert.Contains(r.Errors, e => e.StartsWith("Dòng 6:") && e.Contains("cột"));
        Assert.Contains(r.Errors, e => e.StartsWith("Dòng 7:") && e.Contains("dòng 1"));   // trùng ok|1 ở dòng 1
    }

    [Fact]
    public void Loi_khong_chua_mat_khau()
    {
        var r = BulkAccountParser.Parse("a|99|matkhau-bi-mat-xyz");
        Assert.DoesNotContain(r.Errors, e => e.Contains("matkhau-bi-mat-xyz"));
    }

    [Fact]
    public void Chuoi_rong_thi_khong_co_acc_va_khong_loi()
    {
        var r = BulkAccountParser.Parse("  \n# chỉ có chú thích\n");
        Assert.Empty(r.Accounts);
        Assert.Empty(r.Errors);
    }
}

public class NaturalComparerTests
{
    [Fact]
    public void So_trong_ten_duoc_hieu_nhu_so_va_khong_phan_biet_hoa_thuong()
    {
        var sorted = new[] { "acc10", "Acc2", "acc1", "abc", "acc2" }.OrderBy(x => x, NaturalComparer.Instance).ToArray();

        Assert.Equal(new[] { "abc", "acc1", "Acc2", "acc2", "acc10" }, sorted);
    }

    [Fact]
    public void So_dai_khong_tran_va_chuoi_rong_khong_loi()
    {
        Assert.True(NaturalComparer.Instance.Compare("a99999999999999999999999", "a100000000000000000000000") < 0);
        Assert.Equal(0, NaturalComparer.Instance.Compare("", ""));
        Assert.True(NaturalComparer.Instance.Compare("", "a") < 0);
    }
}
