# Tầng truyền (socket, khung gói, khóa XOR)

Nguồn: `Dragonboy250 v3.5.jar` (client J2ME/MIDlet, gói `nro`, bản mod của bên thứ ba; tác giả ghi trong client là "Hùng Hero", giao thức khai báo **2.5.0**). Tên lớp dưới đây là tên **trong jar** (đã bị làm rối); CFR thêm đuôi `_0` khi trùng tên chữ hoa/thường (`ao` → `ao_0`).
Mức tin cậy: **từ-mã**; các mục đánh dấu `bắt-gói` đã khớp bắt gói thật ngày 2026-10-28 (xem `verification.md`). Hằng số bị làm rối bằng phép toán; các giá trị dưới đây do chính lớp trong jar tính ra (`scripts/survey/DumpConst.java`), không tính tay.

## Kết nối

- TCP thường: `Connector.open("socket://host:cổng")` trong `aO` (không TLS, không HTTP). Mỗi acc một socket.
- Hai luồng: một luồng đọc (`bV`) và một luồng gửi (`bC`, hàng đợi `Vector`), cộng luồng `bn_0` trong `bd_0`.
- Thứ tự (`bd_0.run`): mở socket → mở luồng đọc/ghi → **gửi ngay gói `-27` không có dữ liệu** (khung `E5 00 00`) → server trả gói `-27` mang khóa → từ đó mã hóa.
- Danh sách server: xem `servers.md`.

## Khung gói client → server (`ao.a(cN)`, hàm gửi)

| Chưa có khóa | Có khóa (`bf` = true) |
|---|---|
| 1 byte mã lệnh | 1 byte mã lệnh, XOR khóa |
| 2 byte độ dài (big-endian, `writeShort`) | 2 byte độ dài, từng byte XOR khóa (hi trước, lo sau) |
| payload | payload, từng byte XOR khóa |

Gói không có payload gửi độ dài 0.

## Khung gói server → client (`bV.run`, hàm đọc)

1. Đọc 1 byte mã lệnh, giải XOR nếu đã có khóa.
2. Nếu mã lệnh thuộc nhóm **độ dài 3 byte**: `-32, -66, 11, -67, -74, -87, 66, 12` thì đọc 3 byte độ dài (mỗi byte giải XOR rồi cộng 128, ghép từ thấp đến cao: `((b2<<8)+b1)<<8)+b0`). Cách cộng 128 của CFR trông lạ → **cần xác nhận bằng bắt gói** trước khi tin.
3. Ngược lại: 2 byte độ dài `(hi & 0xFF) << 8 | (lo & 0xFF)`, mỗi byte giải XOR (nếu có khóa).
4. Đọc đủ độ dài byte payload, giải XOR từng byte.
5. Gói có mã `-27` đi vào xử lý khóa (`bV.b`), còn lại giao cho bộ xử lý `cj_0.b` (switch ~169 mã lệnh).

**Đã xác nhận (`bắt-gói`):** khóa `-27` (bố cục payload, biến đổi khóa, hai chỉ số XOR; cả luồng hai chiều đọc đúng); khung 3 byte cho `-74`, `-87`, `12` (gói `-87` dài 202 042 byte, không thể là khung 2 byte). Chưa thấy `-32`, `-66`, `11`, `-67`, `66`. Trường `-27` sau khóa: UTF 12 ký tự, int `12345`, byte `0` (cả 5 lượt); độ dài khóa 65-84, đổi mỗi lần nối.

## Khóa XOR (gói `-27` từ server)

Payload: `byte n` (độ dài khóa), `n` byte khóa thô, rồi `UTF`, `int`, `byte`.

1. Khóa thô `k[0..n-1]`. Biến đổi: với `i` từ 0 đến `n-2`: `k[i+1] ^= k[i]` (tuần tự, dùng giá trị `k[i]` đã đổi).
2. Bật mã hóa (`bf = true`).
3. Ba trường còn lại: `hero69.N` (UTF, chưa rõ ý nghĩa; có thể là địa chỉ/tên), `hero69.bF` (int, chưa rõ), `hero69.bo` (byte→boolean, nhãn trong log: "connect 2"; nếu true và đang ở chế độ tự nối lại thì gọi `cs_0.bZ`).

Dòng khóa (`ao.a(byte)` gửi, `ao.a(ao, byte)` nhận):

```
out = (byte)((k[idx] & 0xFF) ^ (b & 0xFF))     // tức b ^ k[idx]
idx = (idx + 1) mod n                           // mỗi lần dùng khóa 1 byte
```

- **Hai chỉ số riêng**: `R` cho chiều gửi, `Q` cho chiều nhận, cùng một khóa `k`. Cả hai bắt đầu từ 0.
- Chỉ số tăng theo từng byte được XOR, theo thứ tự trên đường truyền: mã lệnh, độ dài (2 hoặc 3 byte), rồi payload.

## Hằng số đã tính

| Tên trong jar | Giá trị | Ý nghĩa |
|---|---|---|
| `bd.cu[2]` | -27 | mã lệnh gói mở đầu |
| `bV.do[16]` | -27 | mã lệnh gói khóa |
| `bV.do[0..7]` | -32, -66, 11, -67, -74, -87, 66, 12 | nhóm mã lệnh có độ dài 3 byte |
| `ao.bj[5]`, `bV.do[8]` | 255 | mặt nạ byte |
| `ao.bj[3]`, `bV.do[9]` | 8 | dịch bit |
| `cN.fo[0]` | 1024 | kích thước đệm ban đầu của gói gửi |

## Chưa biết / việc tiếp

- Ý nghĩa `N`, `bF`, `bo` vẫn chưa rõ (giá trị thấy: UTF 12 ký tự, `12345`, `0`). Khung 3 byte còn 5 mã chưa gặp.
- Chưa tìm cơ chế ping/giữ kết nối và logic nối lại (`ao.cE`, `ao.d`, `bC`, `bn_0`).
- Mọi thứ phải thử bằng acc thử theo rule trong `CLAUDE.md` (báo chủ repo trước khi đăng nhập thật).
