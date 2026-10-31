# Quyết định và việc còn mở

> **Cập nhật 2026-10-31:** hướng "lõi một tiến trình, tự vẽ bằng SkiaSharp" ghi ở các mục dưới (quyết định 2026-10-27) **đã bị thay thế**: ToolQL là cửa sổ quản lý mở một cửa sổ client riêng cho từng acc, như Tool 2. Xem mục "Điều chỉnh hướng 2026-10-31" ở cuối file và `docs/superpowers/specs/2026-10-31-quan-ly-cua-so-client.md`. Các mục cũ giữ lại làm lịch sử.

## Vì sao viết lại (2026-10-27)

Bản ToolQL cũ (client Java 2.3.7 trong JavaHost + WPF) bị chủ repo đánh giá không phù hợp:
- Treo **2 tab** mà CPU và RAM đã nhảy liên tục (nghi: mỗi acc một cửa sổ MicroEmulator có vòng vẽ riêng, trong cùng một JVM).
- Gói build quá lớn (zip 101 MB, vì kèm JRE 8 và exe .NET tự đóng gói).
- Phần lớn tính năng chưa kiểm chứng trên server thật vì môi trường Linux cũ không kết nối được server. Môi trường mới (máy ảo Windows) giải quyết điểm này.

## Các hướng đã cân nhắc

| Hướng | Kết luận |
|---|---|
| Giữ Java, lõi không giao diện + bộ vẽ bật/tắt | Ít viết lại nhất nhưng phải kèm JRE; **không chọn** |
| **Viết lại C# (.NET 8) + SkiaSharp** | **Chọn** (nhẹ nhất; mốc: Tool 2 của ToolCikey là client .NET + Skia, gói 42 MB, chỉ để tham chiếu kích thước) |
| Rust / C++ | Nhẹ nhất nhưng tốn công, khó bảo trì; không chọn |
| Giao diện web cục bộ (WebView2) | WebView2 không hỗ trợ Server 2012 R2; loại cho phần chạy trên VPS |

## Kiến trúc dự kiến (bản nháp, chưa là spec)

1. **Khảo sát** (không có mã sản phẩm): Recaf → giao thức gói tin, định dạng tài nguyên (sprite, map, dữ liệu), quy tắc game.
2. **Lõi** (`net8.0`): kết nối, đăng nhập, trạng thái, ngủ/thức. Chạy và test được cả trên Linux/Ubuntu.
3. **Bộ vẽ** (SkiaSharp): vẽ theo yêu cầu cho acc đang xem; lưới xem tốc độ thấp.
4. **Giao diện quản lý**: công nghệ chọn sau.
5. **Auto**: port logic từ repo cũ khi đến lượt.

## Việc còn mở

- [x] Chốt phạm vi bản đầu (2026-10-28): lõi + xem màn hình + ngủ + nhận captcha; Xmap/Train để v0.2. Xem `docs/superpowers/specs/2026-10-28-v0.1-pham-vi.md`.
- [x] Công nghệ giao diện (2026-10-28): WPF thuần, không thư viện UI nặng.
- [x] Đóng gói (2026-10-30): framework-dependent; VPS cài .NET Runtime 8 một lần, cộng VC++ 2015-2022 x64 Redistributable (xem `docs/dev-setup.md` mục 6).
- [x] Lưu mật khẩu acc (2026-10-30): DPAPI theo user Windows (`CurrentUser`).
- [x] FPS lưới xem (2026-10-30): đo thử rồi chọn; mục tiêu tạm 5 khung/giây cho lưới.
- [ ] Giấy phép client/tài nguyên được dùng; quyết định trước khi push công khai.
- [x] Server có chấp nhận phiên bản client `"2.5.0"` (2026-10-28/30): chấp nhận (suy ra: không `-26`, không ngắt, vào game). Chưa thử phiên bản khác. Xem `docs/protocol/verification.md`.
- [ ] Cách đo RAM/CPU: máy ảo cho xu hướng, VPS thật cho số chốt.

## Tài liệu tham chiếu từ repo cũ

Xem `docs/reference/README.md`.

## Điều chỉnh hướng 2026-10-31 (chủ repo)

**Vì sao:** sau M1-M3 làm theo hướng cũ, chủ repo cho biết đó là sai yêu cầu: "tool QLTK sẽ mở ra cửa sổ của bản client tôi cung cấp", mẫu là Tool 2 (ToolCikey). Hai lần đầu mình hiểu sai (tự vẽ map trong cửa sổ quản lý; rồi coi mọi lần đóng đều phải chờ 40 giây).

**Đã chốt:**
- Mỗi acc một cửa sổ client riêng (không nhúng tab). Client là chính jar `Dragonboy250 v3.5.jar` chạy bằng `AngelAChip.jar` (MicroEmulator bản chỉnh, chủ repo cung cấp). Không dùng `ModDP250.exe`.
- Đăng nhập lại chờ **>= 40 giây sau khi MẤT KẾT NỐI**, không phải sau khi đóng tay. Treo xuyên ngày đêm nên phải tự phát hiện mất kết nối (kể cả khi cửa sổ client còn mở) và tự đăng nhập lại.
- Game bảo trì hằng ngày khoảng 4h sáng: tới giờ thì logout, 5h mới login lại. ToolQL theo giờ cài đặt (mặc định 03:30-05:00, chủ repo xác nhận 2026-10-31), không đọc được thông báo trong game.
- Login trong ToolQL phải tự đăng nhập acc vào game.
- Bản đầu không có auto chơi game; không tự giải captcha.

**Đã đo / đã thấy:** xem spec. Điểm đáng chú ý: client không tự đăng nhập (phải bấm "Chơi TK"); ~99 MB RAM và ~5% một lõi CPU mỗi cửa sổ trong game; client giữ kết nối TCP tới server từ màn hình menu nên "kết nối ổn định" không chứng minh đã đăng nhập; acc chưa nạp bị game ngắt sau ~10 phút.

**Chốt thêm cùng ngày (chủ repo):**
- AngelAChip bản gọn: xóa hết, chỉ chừa "tự động ngủ" (luôn bật, không công tắc). Mã nguồn `tools/angelachip-slim/`, jar kết quả để ngoài repo. Vùng client thành 240×320 nên ToolQL không còn chạy với jar gốc.
- Dữ liệu game tự tải lần đầu, mặc định, không có công tắc.
- Tự đóng popup cập nhật là hành vi mặc định, không công tắc. Giờ bảo trì thật 03:30-05:00.
- Giảm RAM: cờ JVM gọn hơn + cứ 30 giây ToolQL trả RAM ít dùng của client về Windows (`EmptyWorkingSet`). Đo trong game, 1 acc: working set ~34 MB, commit ~73 MB. Chưa đo nhiều acc, chưa thử Server 2012 R2.

**Còn mở:**
- [x] Giờ bảo trì thật của game: 03:30-05:00 (đã xác nhận). [ ] Có cần phát hiện thông báo bảo trì trong game không.
- [ ] 10 acc/VPS 1 GB: RAM mỗi cửa sổ (~99 MB) chưa vừa. Cách giảm? Hay đổi mục tiêu (số acc/máy)?
- [ ] Giấy phép client, tài nguyên, `AngelAChip.jar` (nguồn gốc chưa rõ), FreeJ2ME-Plus (GPLv3, không dùng).
- [x] Hộp thoại "Đã có bản cập nhật mới" của client: tự bấm "Không" (đã làm, mặc định).
- [ ] Windows Server 2012 R2: WPF, Java, Job object, bấm chuột thật khi phiên Remote Desktop bị thu nhỏ/ngắt.
