namespace ToolQL.Protocol;

/// <summary>Một gói tin đã bóc khung. <paramref name="Command"/> là mã lệnh có dấu như giao thức (ví dụ -27).</summary>
/// <param name="UsedThreeByteLength">True nếu gói nhận được dùng nhánh độ dài 3 byte (nghi vấn, cần ghi lại khi bắt gói).</param>
public sealed record Packet(sbyte Command, byte[] Payload, bool UsedThreeByteLength = false);
