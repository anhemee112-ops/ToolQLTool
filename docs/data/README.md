# Dữ liệu ID game (xuất ngày 2026-10-27)

Nguồn: trang tra cứu công khai `https://duncannro.id.vn/check-id`. Trang này tải dữ liệu từ `GET /api/gamedata/search?type=<loại>&query=&page=<n>&limit=<n>` (không cần đăng nhập). Mỗi loại được lấy hết bằng yêu cầu tuần tự, nghỉ 1 giây giữa các lần.

| File | Loại API | Số dòng | Trường |
|---|---|---|---|
| `items.json` | `item` | 2103 | `id`, `name`, `description`, `iconId` |
| `mobs.json` | `monster` | 97 | `description` dạng `HP: <số>` |
| `maps.json` | `map` | 187 | |
| `npcs.json` | `npc` | 83 | |
| `skills.json` | `skill` | 27 | `description` mở đầu bằng thẻ hành tinh, ví dụ `[Trái Đất]` |

`_source.json` ghi nguồn, ngày xuất và số lượng. Đã kiểm: số dòng khớp `totalItems` của API, không trùng ID. Đã đối chiếu vài mã biết trước: 74 Đùi gà nướng, 76 Vàng, 194 Viên Capsule đặc biệt, 22 Nhà Moori, 26 Trạm tàu vũ trụ.

## Lưu ý

- **Chưa biết dữ liệu ứng với phiên bản game nào.** ID và tên có thể khác giữa các bản/server. Khi khảo sát jar, đối chiếu lại với dữ liệu thật trong client (ví dụ bảng vật phẩm khi đăng nhập) trước khi tin hoàn toàn.
- Lệch với bảng cũ của ToolCikey trong repo cũ: số dòng khác (map 202 dòng so với 187, quái 115 so với 97, skill 375 so với 27). Bảng cũ có thể gộp thêm cột/biến thể; **chưa so sánh chi tiết**.
- Mã `193` ở đây là **"Gói 10 viên Capsule"**, không phải viên Capsule thường. Repo cũ coi 193 là Capsule thường; cần kiểm lại khi làm Capsule.
- Không gồm biểu tượng ảnh. Trang phục vụ ảnh ở `/img/nro_icons/...`; nếu cần, tải riêng và hỏi lại về bản quyền.
- Cần cập nhật: chạy lại cùng cách lấy dữ liệu và ghi ngày vào `_source.json`. Đừng gọi dồn dập vào trang của người khác.
