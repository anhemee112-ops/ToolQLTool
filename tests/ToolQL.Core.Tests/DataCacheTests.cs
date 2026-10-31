using ToolQL.Protocol;
using ToolQL.Protocol.Messages;
using Xunit;

namespace ToolQL.Core.Tests;

public class DataCacheTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "toolql-cache-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Luu_roi_doc_lai_dung_phien_ban()
    {
        var cache = new DataCache(_dir);
        cache.Save("map-0", 63, new byte[] { 1, 2, 3 });
        Assert.Equal(new byte[] { 1, 2, 3 }, cache.TryLoad("map-0", 63));
        Assert.Null(cache.TryLoad("map-0", 64));
        Assert.Null(cache.TryLoad("skill-0", 63));
        Assert.Equal(63, cache.StoredVersion("map-0"));
    }

    [Fact]
    public void Luu_phien_ban_moi_xoa_phien_ban_cu()
    {
        var cache = new DataCache(_dir);
        cache.Save("map-0", 63, new byte[] { 1 });
        cache.Save("map-0", 64, new byte[] { 2 });
        Assert.Null(cache.TryLoad("map-0", 63));
        Assert.Equal(new byte[] { 2 }, cache.TryLoad("map-0", 64));
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void Chua_co_thu_muc_thi_StoredVersion_la_null()
    {
        Assert.Null(new DataCache(_dir).StoredVersion("map-0"));
    }

    [Theory]
    [InlineData("..\\x")]
    [InlineData("a/b")]
    [InlineData("")]
    [InlineData("HOA")]
    public void Ten_khong_hop_le_bi_tu_choi(string name)
    {
        var cache = new DataCache(_dir);
        Assert.Throws<ArgumentException>(() => cache.Save(name, 1, Array.Empty<byte>()));
        Assert.Throws<ArgumentException>(() => cache.TryLoad(name, 1));
    }

    [Fact]
    public void Recorder_luu_dung_loai_dung_phien_ban_va_danh_so_goi_cung_loai()
    {
        var cache = new DataCache(_dir);
        var rec = new TableRecorder(cache);
        var v = DataVersions.Parse(Packets.Versions(data: 29, map: 63, skill: 7, item: 24));

        Assert.True(rec.Record(new Packet(-87, new byte[] { 9 }, true), v));
        Assert.True(rec.Record(new Packet(-28, new byte[] { 6, 1 }), v));
        Assert.True(rec.Record(new Packet(-28, new byte[] { 7, 2 }), v));
        Assert.True(rec.Record(new Packet(-28, new byte[] { 8, 3 }), v));
        Assert.True(rec.Record(new Packet(-28, new byte[] { 8, 4 }), v));
        Assert.True(rec.Record(new Packet(12, new byte[] { 0, 5 }, true), v));
        Assert.False(rec.Record(new Packet(-28, new byte[] { 10, 0 }), v));   // lưới map không phải bảng
        Assert.False(rec.Record(new Packet(-7, new byte[8]), v));

        Assert.Equal(new byte[] { 9 }, cache.TryLoad("data-0", 29));
        Assert.Equal(new byte[] { 6, 1 }, cache.TryLoad("map-0", 63));
        Assert.Equal(new byte[] { 7, 2 }, cache.TryLoad("skill-0", 7));
        Assert.Equal(new byte[] { 8, 3 }, cache.TryLoad("item-0", 24));
        Assert.Equal(new byte[] { 8, 4 }, cache.TryLoad("item-1", 24));
        Assert.Equal(new byte[] { 0, 5 }, cache.TryLoad("itemlist-0", 24));
    }

    [Fact]
    public void Recorder_chua_biet_phien_ban_thi_khong_luu()
    {
        var rec = new TableRecorder(new DataCache(_dir));
        Assert.False(rec.Record(new Packet(-87, new byte[] { 9 }), null));
        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public void Recorder_loi_ghi_tep_khong_nem()
    {
        File.WriteAllText(_dir, "tệp chặn đường");   // thư mục cache trùng tên một tệp
        var rec = new TableRecorder(new DataCache(_dir));
        var v = DataVersions.Parse(Packets.Versions());
        Assert.False(rec.Record(new Packet(-87, new byte[] { 9 }), v));
        File.Delete(_dir);
    }
}
