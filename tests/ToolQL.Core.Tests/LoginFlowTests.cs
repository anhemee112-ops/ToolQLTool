using ToolQL.Protocol;

namespace ToolQL.Core.Tests;

public class LoginFlowTests
{
    private static readonly AccountCredentials Creds = new("nguoi-thu", "mat-khau-thu");

    private static LoginFlow New() => new(Creds);

    private static string Describe(IEnumerable<Packet> packets) =>
        string.Join(", ", packets.Select(p => p.Payload.Length > 0 && p.Command is -28 or -29
            ? $"{p.Command}/{(sbyte)p.Payload[0]}" : p.Command.ToString()));

    [Fact]
    public void Start_gui_thong_tin_client_roi_dang_nhap_dung_truong()
    {
        var sent = New().Start();
        Assert.Equal("-29/2, -29/0", Describe(sent));

        var r = new JavaStreamReader(new MemoryStream(sent[1].Payload));
        Assert.Equal(0, r.ReadSByte());
        Assert.Equal("nguoi-thu", r.ReadUtf());
        Assert.Equal("mat-khau-thu", r.ReadUtf());
        Assert.Equal("2.5.0", r.ReadUtf());
        Assert.Equal(0, r.ReadSByte());
        Assert.Equal(0, r.ReadSByte());
        Assert.Equal(sent[1].Payload.Length, r.Position);
    }

    [Fact]
    public void Sub4_xin_bon_bang_mot_lan_va_chuyen_sang_Loading()
    {
        var flow = New();
        var step = flow.OnPacket(new Packet(-28, Packets.Versions()));
        Assert.Equal("-87, -28/6, -28/7, -28/8", Describe(step.Send));
        Assert.Equal(LoginPhase.Loading, flow.Phase);
        Assert.Equal(24, flow.Versions!.Item);

        Assert.Empty(flow.OnPacket(new Packet(-28, Packets.Versions())).Send);   // gói lặp không xin lại
    }

    [Fact]
    public void Goi_12_bao_tai_xong_va_gui_38_dung_mot_lan()
    {
        var flow = New();
        Assert.Equal("-28/13, -38", Describe(flow.OnPacket(new Packet(12, new byte[] { 0, 24, 1 })).Send));
        Assert.Empty(flow.OnPacket(new Packet(12, new byte[] { 0, 24, 1 })).Send);
    }

    [Fact]
    public void Goi_24_xin_luoi_voi_dung_mapId_roi_luoi_ve_thi_gui_39_va_vao_game()
    {
        var flow = New();
        var ask = flow.OnPacket(new Packet(-24, Packets.Map(23)));
        Assert.Equal("-28/10", Describe(ask.Send));
        Assert.Equal(new byte[] { 10, 23 }, ask.Send[0].Payload);
        Assert.Equal(23, flow.MapId);
        Assert.NotEqual(LoginPhase.InGame, flow.Phase);

        var done = flow.OnPacket(new Packet(-28, Packets.Grid(2, 2)));
        Assert.Equal("-39", Describe(done.Send));
        Assert.Equal(LoginPhase.InGame, flow.Phase);
    }

    [Fact]
    public void MapId_lon_hon_127_gui_dung_byte_khong_dau()
    {
        var ask = New().OnPacket(new Packet(-24, Packets.Map(200)));
        Assert.Equal(new byte[] { 10, 200 }, ask.Send[0].Payload);
    }

    [Fact]
    public void Captcha_dung_va_khong_gui_gi()
    {
        var step = New().OnPacket(new Packet(-85, new byte[] { 1, 2 }));
        Assert.Equal(FlowOutcome.NeedCaptcha, step.Outcome);
        Assert.Empty(step.Send);
    }

    [Fact]
    public void Goi_26_la_tu_choi_kem_noi_dung_goi_2_la_thieu_nhan_vat()
    {
        var w = new JavaStreamWriter();
        w.WriteUtf("Sai mật khẩu");
        var rejected = New().OnPacket(new Packet(-26, w.ToArray()));
        Assert.Equal(FlowOutcome.Rejected, rejected.Outcome);
        Assert.Contains("Sai mật khẩu", rejected.Message);

        Assert.Equal(FlowOutcome.NeedCharacter, New().OnPacket(new Packet(2, Array.Empty<byte>())).Outcome);
    }

    [Fact]
    public void Goi_la_duoc_bo_qua_va_goi_hong_nem_loi_doc()
    {
        var flow = New();
        Assert.Empty(flow.OnPacket(new Packet(-7, new byte[] { 1, 2, 3, 4, 0, 5, 0, 6 })).Send);
        Assert.Throws<InvalidDataException>(() => flow.OnPacket(new Packet(-24, new byte[] { 1, 2 })));
    }

    [Fact]
    public void ToString_cua_thong_tin_dang_nhap_khong_lo_mat_khau()
    {
        Assert.DoesNotContain("mat-khau-thu", Creds.ToString());
        Assert.Contains("nguoi-thu", Creds.ToString());
    }
}
