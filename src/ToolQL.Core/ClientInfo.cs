using ToolQL.Protocol;

namespace ToolQL.Core;

/// <summary>Gói <c>-29</c> sub 2 (client khai báo thông tin), theo <c>an_0.dF</c>.</summary>
public static class ClientInfo
{
    public const string ProtocolVersion = "2.5.0";

    // Hai byte đầu là hằng số khởi tạo của client (aA.bH = 1, bQ.bB = 1). Các giá trị còn lại là trạng thái runtime
    // của máy khách J2ME nên CHỈ LÀ ĐOÁN (kích thước màn hình, cờ bàn phím/cảm ứng, chuỗi nền tảng). Server thật đã
    // nhận các giá trị này (bắt gói 2026-10-28/30), chưa biết nó có kiểm tra hay không.
    private const int Byte1 = 1, Byte2 = 1, ScreenWidth = 240, ScreenHeight = 320;
    private const string PlatformString = "j2me|" + ProtocolVersion;

    public static byte[] Build()
    {
        var w = new JavaStreamWriter();
        w.WriteSByte(2);
        w.WriteSByte(Byte1);
        w.WriteSByte(Byte2);
        w.WriteBool(false);
        w.WriteInt(ScreenWidth);
        w.WriteInt(ScreenHeight);
        w.WriteBool(false);   // av_0.bo (bàn phím qwerty?)
        w.WriteBool(false);   // cs_0.cS (có cảm ứng?)
        w.WriteUtf(PlatformString);
        return w.ToArray();
    }
}
