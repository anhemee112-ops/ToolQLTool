# ToolQL

Công cụ quản lý nhiều tài khoản Ngọc Rồng Online (C#, .NET 8, WPF), làm theo mẫu Tool 2: một cửa sổ quản lý, mỗi acc mở **một cửa sổ client riêng** (jar game chạy bằng trình J2ME AngelChip), có Xếp cửa sổ, Ẩn client, Hiện, Auto login.

**Trạng thái (2026-10-31):** cửa sổ quản lý (`src/ToolQL.App`) đã chạy được: bảng acc (mật khẩu mã hóa DPAPI), Login/Đóng, Login tất cả, Tắt tất cả, Tick, Auto login (mở lại sau ít nhất 40 giây), Xếp cửa sổ, Ẩn client, SHOW ALL, thêm nhiều tài khoản, cài đặt chung, tự bấm "Chơi TK". Đã thử với acc thử: vào game tự động, client mở ở đồ họa thấp nhất, popup "Đã có bản cập nhật mới" tự đóng, tự phát hiện mất kết nối (chờ ít nhất 40 giây rồi đăng nhập lại; đóng tay thì không phải chờ), tự đóng client trong khung giờ bảo trì và mở lại khi hết khung, nhật ký sự kiện `toolql.log`. **Chưa có lần mất kết nối thật nào để thử** (mới kiểm bằng test và giết tiến trình). Dữ liệu game tự tải lần đầu; trình chạy là AngelAChip bản gọn (`tools/angelachip-slim/`, chỉ chừa tự động ngủ). **Rủi ro lớn:** RAM nhiều acc chưa đo: 1 acc trong game ~34 MB working set (sau khi ToolQL ép RAM mỗi 30 giây) nhưng ~73 MB commit. Chưa thử trên Windows Server 2012 R2. Chi tiết: [`docs/superpowers/specs/2026-10-31-quan-ly-cua-so-client.md`](docs/superpowers/specs/2026-10-31-quan-ly-cua-so-client.md). Hướng cũ (tự viết client/bộ vẽ bằng SkiaSharp) vẫn nằm trong repo (`ToolQL.Protocol`, `ToolQL.Render`, `src/ToolQL.App/_cu/`) nhưng không còn là đường chính.

## Bắt đầu

1. Dựng máy ảo Windows: [`docs/vm-setup.md`](docs/vm-setup.md)
2. Dựng môi trường phát triển: [`docs/dev-setup.md`](docs/dev-setup.md), rồi chạy `scripts\setup-dev.ps1`
3. Đọc bối cảnh và quy tắc: [`CLAUDE.md`](CLAUDE.md), [`docs/decisions.md`](docs/decisions.md)
4. Khảo sát client: [`docs/survey-checklist.md`](docs/survey-checklist.md)

## Cấu trúc

| Đường dẫn | Nội dung |
|---|---|
| `docs/` | Tài liệu thiết kế, môi trường, khảo sát |
| `docs/data/` | Dữ liệu ID game (vật phẩm, quái, map, NPC, skill) |
| `docs/protocol/` | Giao thức và định dạng tài nguyên (điền khi khảo sát) |
| `docs/reference/` | Chỉ dẫn tới tài liệu của repo cũ |
| `src/` | `ToolQL.Protocol` (giao thức), `ToolQL.Core` (phiên acc, trạng thái game, kho acc), `ToolQL.Render` (vẽ map bằng SkiaSharp), `ToolQL.App` (giao diện WPF) |
| `tests/` | Test xUnit (gói dựng tay, máy chủ giả) |
| `tools/` | `ToolQL.Probe` (thử giao thức, đọc tệp bắt gói, vẽ map offline), `ToolQL.Host` (CLI giữ 1 acc) |
| `scripts/` | Script dựng môi trường |

## Giấy phép

**Chưa quyết định.** Chủ repo phải chọn trước khi đưa repo lên công khai (repo cũ dùng AGPLv3). Giấy phép của client và tài nguyên game cũng cần làm rõ.
