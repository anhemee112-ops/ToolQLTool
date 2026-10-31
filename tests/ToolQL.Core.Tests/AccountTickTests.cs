using System.Text.Json;

namespace ToolQL.Core.Tests;

public class AccountTickTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "toolql-tick-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private sealed class PlainProtector : IPasswordProtector
    {
        public string Protect(string plain) => plain;
        public string Unprotect(string protectedText) => protectedText;
    }

    private AccountStore NewStore() => new(Path.Combine(_dir, "accounts.json"), new PlainProtector());

    [Fact]
    public void Mac_dinh_khong_tick_dat_tick_luu_xuong_dia_va_doc_lai_duoc()
    {
        var store = NewStore();
        var a = store.Add("a", "p", "Vũ trụ 1");
        var b = store.Add("b", "p", "Vũ trụ 1");
        Assert.False(a.Tick);

        store.SetTick(new[] { a.Id }, true);

        var reread = NewStore();
        reread.Load();
        Assert.True(reread.Entries.Single(e => e.Id == a.Id).Tick);
        Assert.False(reread.Entries.Single(e => e.Id == b.Id).Tick);
    }

    [Fact]
    public void Bo_tick_nhieu_acc_mot_lan_va_bo_qua_id_la()
    {
        var store = NewStore();
        var a = store.Add("a", "p", "Vũ trụ 1");
        var b = store.Add("b", "p", "Vũ trụ 1");
        store.SetTick(new[] { a.Id, b.Id }, true);

        store.SetTick(new[] { a.Id, b.Id, Guid.NewGuid() }, false);

        Assert.All(store.Entries, e => Assert.False(e.Tick));
    }

    [Fact]
    public void Sua_acc_giu_nguyen_dau_tick()
    {
        var store = NewStore();
        var a = store.Add("a", "p", "Vũ trụ 1");
        store.SetTick(new[] { a.Id }, true);

        var updated = store.Update(a.Id, "Vũ trụ 2", "ghi chú");

        Assert.True(updated.Tick);
    }

    [Fact]
    public void File_cu_khong_co_truong_Tick_van_doc_duoc()
    {
        Directory.CreateDirectory(_dir);
        var id = Guid.NewGuid();
        File.WriteAllText(Path.Combine(_dir, "accounts.json"),
            JsonSerializer.Serialize(new[] { new { Id = id, User = "cu", ProtectedPassword = "x", Server = "Vũ trụ 1", Note = "" } }));

        var store = NewStore();
        store.Load();

        Assert.False(Assert.Single(store.Entries).Tick);
    }
}
