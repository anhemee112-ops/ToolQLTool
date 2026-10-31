# Checklist khảo sát client bằng Recaf (bước đầu tiên sau setup)

Mục tiêu: hiểu client đủ để viết lại lõi bằng C#. **Chưa viết mã sản phẩm.** Kết quả ghi vào `docs/protocol/` (xem `docs/protocol/README.md`).

## A. Chuẩn bị (ngoài repo)

- [ ] Jar nằm ở `C:\Dev\ToolQL-input\`. Ghi lại: tên file, kích thước, SHA-256, nguồn do chủ repo cung cấp.
- [ ] Mở bằng Recaf. Xuất mã dịch ngược ra `C:\Dev\ToolQL-input\decompiled-<tên>\` (**ngoài repo**, không commit).
- [ ] Nếu mã bị làm rối: dùng chức năng đổi tên/ánh xạ (mappings) của Recaf; lưu file ánh xạ cạnh jar, ghi quy tắc đặt tên vào `docs/protocol/naming.md`.
- [ ] Chọn **phiên bản mục tiêu**. Ứng viên: 2.3.7 (repo cũ, đăng nhập đã thành công), 2.4.7 (jar bị làm rối, chủ repo đưa 2026-10-27), bản chủ repo chọn là tốt nhất. Chốt bằng chứng: server chấp nhận chuỗi phiên bản nào.

## B. Đường truyền và đăng nhập

- [ ] Lớp phiên làm việc (socket): địa chỉ, cổng, luồng đọc/ghi, khung gói tin (độ dài, mã lệnh), kiểu số (big-endian?).
- [ ] Khóa XOR hoặc mã hóa: sinh khóa, thời điểm bắt đầu áp dụng (từ gói nào), cách tiến khóa.
- [ ] Gói đầu tiên, `ClientInfo`, `Login` và các gói server trả (`-111`, `-74`, `-29`, `-77`, `-93`, `-28`, `-31`, `-82`, `-89` đã biết từ repo cũ): đối chiếu từng gói với mã client.
- [ ] Chuỗi phiên bản, cách chọn server, danh sách server (`dragon1..7`, `dragon11` cổng 14445...).
- [ ] Giữ kết nối: ping, thời gian chờ, cách client nối lại.

## C. Bảng mã lệnh (cmd)

- [ ] Lập bảng: mã → tên → hướng (client→server / server→client) → cấu trúc trường → nơi xử lý trong client.
- [ ] Ưu tiên: vào game, di chuyển, đổi map/khu, tấn công, nhặt đồ, dùng vật phẩm, chat, thông báo (`-70` thông báo khẩn), NPC/hộp thoại (case 32), nhiệm vụ (case 96), chết/hồi sinh, túi đồ.

## D. Trạng thái game cần giữ ở lõi

- [ ] Nhân vật (HP, MP, vị trí, trạng thái chết `14`/`5`, sức mạnh, tiền: vàng/ngọc/hồng ngọc).
- [ ] Map: kích thước, khối va chạm, cổng, NPC, quái, vật phẩm rơi, người chơi khác.
- [ ] Quái: id loại, HP, vị trí, trạng thái; boss.
- [ ] Túi đồ và vật phẩm trang bị.
- [ ] Danh sách skill, thời gian hồi.

## E. Tài nguyên và vẽ (để làm bộ vẽ)

- [ ] Nơi chứa tài nguyên: trong jar hay tải từ server; đường dẫn, định dạng (ảnh PNG? gói tự định nghĩa? nén thế nào?).
- [ ] Định dạng sprite/nhân vật/quái (khung hình, điểm neo, bảng ghép phần thân), bảng màu/chuyển màu.
- [ ] Định dạng map: ô lát (tile), lớp nền, va chạm.
- [ ] Hệ tọa độ, độ phân giải gốc (ví dụ 240×320), tỷ lệ phóng khi vẽ.
- [ ] Vòng lặp: tốc độ khung hình, thứ tự vẽ, hoạt ảnh phụ thuộc thời gian hay khung hình.
- [ ] Đánh giá: tách được phần logic khỏi phần vẽ không (client có lẫn `Graphics` vào logic?).

## F. Dữ liệu tham chiếu

- [ ] Đối chiếu `docs/data/*.json` với dữ liệu trong client (ID vật phẩm, quái, map, NPC, skill) và ghi chỗ lệch.
- [ ] Đồ thị map/cổng (tham khảo `JavaClient/res/xmap-*.txt` ở repo cũ nếu có).

## G. Rủi ro cần ghi lại

- [ ] Những chỗ server kiểm tra client (phiên bản, chữ ký, tốc độ gói) có thể khiến tool bị phát hiện.
- [ ] Giấy phép/nguồn gốc của jar và tài nguyên (hỏi chủ repo trước khi đưa bất cứ thứ gì lên repo công khai).
- [ ] Captcha: ghi **ở đâu và khi nào** client sinh captcha; chỉ để dừng và báo, **không** viết mã giải/né.

## H. Điều kiện kết thúc khảo sát

Khảo sát xong khi `docs/protocol/` đủ để một người chưa từng đọc jar viết được: (1) kết nối và đăng nhập, (2) đọc trạng thái nhân vật/map/quái, (3) gửi di chuyển và tấn công, (4) nạp và vẽ một map cùng nhân vật. Sau đó cùng chủ repo **chốt phạm vi bản đầu** rồi viết spec.

## I. Phép thử đầu tiên với server thật (sau khi khảo sát, cần chủ repo cho phép)

Một acc thử, một lần, đăng nhập bằng lõi tối thiểu rồi ngắt. Báo chủ repo trước; bị từ chối thì dừng.
