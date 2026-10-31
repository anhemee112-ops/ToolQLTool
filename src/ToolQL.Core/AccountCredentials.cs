using System.Text;

namespace ToolQL.Core;

/// <summary>Tên đăng nhập và mật khẩu của một acc. <c>ToString</c> KHÔNG in mật khẩu (tránh lọt vào log).</summary>
public sealed record AccountCredentials(string User, string Password)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("User = ").Append(User);
        return true;
    }
}
