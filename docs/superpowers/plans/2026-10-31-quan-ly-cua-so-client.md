# Kế hoạch: quản lý cửa sổ client (thay plan v0.1 cũ)

Spec: `docs/superpowers/specs/2026-10-31-quan-ly-cua-so-client.md` (mục cuối thắng mọi chỗ mâu thuẫn). Hướng đi: mỗi acc một cửa sổ client riêng như Tool 2; client là jar game chạy bằng `AngelAChip.jar`.

## Trạng thái (2026-10-31)

| Mốc | Nội dung | Tình trạng |
|---|---|---|
| Q0 | Chọn client và trình chạy (jar + AngelChip; không dùng ModDP250.exe, không dùng FreeJ2ME) | **Xong** |
| Q1 | Lõi quản lý (Core): `ClientHub` (mở có khoảng nghỉ, đóng, mất kết nối, Auto login, bảo trì), RMS (`RmsFile`, `ClientDirectory`), `ClientCommand`, `WindowGrid`, `LoginClickScheduler`, `ConnectionWatcher`, `MaintenanceSchedule`, `PopupGuard`, `UpdatePromptDetector`, `BulkAccountParser`, `NaturalComparer`, `ToolLog` | **Xong**, 174 test (Core) |
| Q2 | Cửa sổ quản lý kiểu Tool 2 (`ToolQL.App`) + logo của chủ repo | **Xong bản đầu**, đã thử trên máy ảo |
| Q3 | Mất kết nối khi cửa sổ còn mở (bảng kết nối TCP), chờ >= 40 giây sau mất kết nối (không phải sau đóng tay), chờ lâu dần, dừng sau 3 lần liền | **Xong**, kiểm bằng test; **chưa có sự kiện mất kết nối thật** |
| Q4 | Bảo trì định kỳ (đóng hết, mở lại hết khung) | **Xong**, đã thử khung giả 2 phút; giờ thật chưa xác nhận |
| Q5 | Login tự đăng nhập vào game (ghi RMS + bấm "Chơi TK"), đồ họa thấp mặc định, tự đóng popup cập nhật | **Xong**, đã thử với acc thử (5 lượt) |
| Q6 | Giảm RAM mỗi cửa sổ (~99 MB trong game): cờ JVM gọn, ép working set mỗi 30 giây, AngelAChip bản gọn (`tools/angelachip-slim/`), tự tải dữ liệu game (`TemplateBuilder`) | **Xong bản đầu**: 1 acc trong game ~34 MB working set, ~73 MB commit, ~5% CPU. **Chưa đo nhiều acc, chưa thử Server 2012 R2** |
| Q7 | Windows Server 2012 R2: WPF, Java, Job object, chuột thật/`PrintWindow` khi Remote Desktop thu nhỏ; đóng gói | **Chưa làm** |
| Q8 | Tính năng Tool 2 còn thiếu (tab loại auto, proxy, khung giờ riêng từng acc, hành trang, tìm kiếm data...) | Khi chủ repo yêu cầu |

## Việc cần chủ repo

- Duyệt spec này và `CLAUDE.md` đã sửa.
- (Giờ bảo trì thật 03:30-05:00 đã xác nhận.) Cho biết có cần ToolQL tự nhận thông báo bảo trì trong game không.
- Quyết định mục tiêu số acc mỗi máy khi RAM mỗi cửa sổ ~99 MB.
- Giấy phép client, tài nguyên, `AngelAChip.jar` (nguồn gốc chưa rõ).
- Cho thử trên VPS thật (Server 2012 R2). Báo trước từng lượt đăng nhập thật.
