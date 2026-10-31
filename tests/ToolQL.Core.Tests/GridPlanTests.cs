namespace ToolQL.Core.Tests;

public class GridPlanTests
{
    private static (Guid, SessionState) A(SessionState state) => (Guid.NewGuid(), state);

    [Fact]
    public void Chi_lay_acc_dang_trong_game_theo_thu_tu_va_toi_da_bon()
    {
        var a = A(SessionState.InGame);
        var skip1 = A(SessionState.Connecting);
        var b = A(SessionState.InGame);
        var skip2 = A(SessionState.Stopped);
        var c = A(SessionState.InGame);
        var d = A(SessionState.InGame);
        var e = A(SessionState.InGame);   // thứ năm: bị bỏ

        var chosen = GridPlan.Choose(new[] { a, skip1, b, skip2, c, d, e });

        Assert.Equal(new[] { a.Item1, b.Item1, c.Item1, d.Item1 }, chosen);
    }

    [Fact]
    public void Khong_co_acc_nao_trong_game_thi_rong()
    {
        Assert.Empty(GridPlan.Choose(new[] { A(SessionState.Idle), A(SessionState.Rejected), A(SessionState.Reconnecting) }));
        Assert.Empty(GridPlan.Choose(Array.Empty<(Guid, SessionState)>()));
    }

    [Fact]
    public void Gioi_han_so_o_co_the_doi()
    {
        var all = Enumerable.Range(0, 6).Select(_ => A(SessionState.InGame)).ToArray();
        Assert.Equal(2, GridPlan.Choose(all, max: 2).Count);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(2, 2, 1)]
    [InlineData(3, 2, 2)]
    [InlineData(4, 2, 2)]
    public void Chia_luoi_theo_so_o(int count, int columns, int rows)
    {
        Assert.Equal((columns, rows), GridPlan.Layout(count));
    }
}
