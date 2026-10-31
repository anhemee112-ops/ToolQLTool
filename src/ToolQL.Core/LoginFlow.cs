using ToolQL.Protocol;
using ToolQL.Protocol.Messages;

namespace ToolQL.Core;

public enum LoginPhase { LoggingIn, Loading, InGame }

public enum FlowOutcome { Continue, NeedCaptcha, NeedCharacter, Rejected }

/// <summary>Kết quả xử lý một gói: các gói cần gửi lại và có phải dừng không.</summary>
public sealed record FlowStep(IReadOnlyList<Packet> Send, FlowOutcome Outcome = FlowOutcome.Continue, string? Message = null)
{
    public static readonly FlowStep None = new(Array.Empty<Packet>());
}

/// <summary>
/// Máy trạng thái đăng nhập + nạp map, tách khỏi mạng để test bằng gói dựng tay. Thứ tự gửi là thứ tự đã chạy đúng
/// trên server thật (<c>docs/protocol/verification.md</c>): -29 sub 2, -29 sub 0; sau -28 sub 4 xin bảng
/// (-87, -28 sub 6/7/8); sau gói 12 gửi -28 sub 13 và -38; sau -24 xin lưới (-28 sub 10 + mapId); sau lưới gửi -39.
/// <para>
/// Đổi map về sau (-24 mới) lặp lại bước xin lưới và -39 theo mã client; điều này CHƯA kiểm chứng với server
/// (chưa có lượt nào đổi map).
/// </para>
/// </summary>
public sealed class LoginFlow
{
    private readonly AccountCredentials _credentials;
    private bool _sentTableRequests;
    private bool _sentLoadedFlags;

    public LoginFlow(AccountCredentials credentials) => _credentials = credentials;

    public LoginPhase Phase { get; private set; } = LoginPhase.LoggingIn;
    public DataVersions? Versions { get; private set; }
    public int? MapId { get; private set; }

    /// <summary>Hai gói gửi ngay sau bắt tay khóa.</summary>
    public IReadOnlyList<Packet> Start()
    {
        var login = new JavaStreamWriter();
        login.WriteSByte(0);
        login.WriteUtf(_credentials.User);
        login.WriteUtf(_credentials.Password);
        login.WriteUtf(ClientInfo.ProtocolVersion);
        login.WriteSByte(0);   // ý nghĩa chưa rõ (login.md); server nhận giá trị 0
        login.WriteSByte(0);   // đoán là mã ngôn ngữ; server nhận giá trị 0
        return new[] { new Packet(-29, ClientInfo.Build()), new Packet(-29, login.ToArray()) };
    }

    /// <summary>Xử lý một gói từ server. Có thể ném <see cref="InvalidDataException"/> nếu bộ đọc không đọc được.</summary>
    public FlowStep OnPacket(Packet packet)
    {
        sbyte? sub = packet.Payload.Length > 0 ? (sbyte)packet.Payload[0] : null;

        switch (packet.Command, sub)
        {
            case (-85, _):
                return new FlowStep(Array.Empty<Packet>(), FlowOutcome.NeedCaptcha, "Cần captcha");
            case (-26, _):
                return new FlowStep(Array.Empty<Packet>(), FlowOutcome.Rejected, "Server báo: " + ReadMessage(packet.Payload));
            case (2, _):
                // Theo mã client, mã 2 mở màn hình tạo nhân vật. Chưa từng thấy trong bắt gói thật.
                return new FlowStep(Array.Empty<Packet>(), FlowOutcome.NeedCharacter, "Acc chưa có nhân vật");

            case (-28, 4):
                Versions = DataVersions.Parse(packet.Payload);
                Phase = LoginPhase.Loading;
                if (_sentTableRequests) return FlowStep.None;
                _sentTableRequests = true;
                // Chưa có bảng cục bộ nên coi cả bốn bảng là lệch (kho tệp ở bước C4).
                return new FlowStep(new[]
                {
                    new Packet(-87, Array.Empty<byte>()),
                    new Packet(-28, new byte[] { 6 }),
                    new Packet(-28, new byte[] { 7 }),
                    new Packet(-28, new byte[] { 8 }),
                });

            case (12, _):
                // Client thật chỉ báo tải xong sau gói 12 (phần item lớn). -38 rỗng đi cùng; ý nghĩa chưa rõ.
                if (_sentLoadedFlags) return FlowStep.None;
                _sentLoadedFlags = true;
                return new FlowStep(new[]
                {
                    new Packet(-28, new byte[] { 13 }),
                    new Packet(-38, Array.Empty<byte>()),
                });

            case (-24, _):
            {
                int mapId = MapInfo.Parse(packet.Payload).Header.MapId;
                MapId = mapId;
                return new FlowStep(new[] { new Packet(-28, new byte[] { 10, (byte)mapId }) });
            }

            case (-28, 10):
            {
                _ = MapGrid.Parse(packet.Payload);   // kiểm gói hợp lệ; trạng thái game giữ lưới ở bước C3
                Phase = LoginPhase.InGame;
                return new FlowStep(new[] { new Packet(-39, Array.Empty<byte>()) });
            }
        }

        return FlowStep.None;
    }

    private static string ReadMessage(byte[] payload)
    {
        try
        {
            return new JavaStreamReader(new MemoryStream(payload)).ReadUtf();
        }
        catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException)
        {
            return "(không đọc được nội dung)";
        }
    }
}
