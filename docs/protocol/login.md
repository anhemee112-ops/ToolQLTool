# Đăng nhập (phần đã đọc được)

Nguồn: `Dragonboy250 v3.5.jar`. Mức tin cậy: **từ-mã**; phần đánh dấu `bắt-gói` đã thấy trên server thật ngày 2026-10-28 (xem `verification.md`). Chi tiết khung gói xem `transport.md`.

## Gói đăng nhập client → server

Hàm gửi: `an_0.a(String, String, String, byte)` (lớp `an`), mã lệnh `an.bi[4]` = **0**.

| Thứ tự | Kiểu | Nội dung |
|---|---|---|
| 1 | UTF | tên đăng nhập |
| 2 | UTF | mật khẩu (gửi nguyên văn trong payload; chỉ được che bởi khóa XOR của tầng truyền) |
| 3 | UTF | phiên bản giao thức, client dùng cứng `"2.5.0"` |
| 4 | byte | chưa rõ; client truyền `bg[6]` (từ `al`) hoặc 0/1 theo cờ `bd` (từ `cK`) |
| 5 | byte | `aM.Z`; đoán là mã ngôn ngữ (`aM` chứa bản dịch Việt, Anh, Indonesia) |

Client có cơ chế captcha qua gói `-85` (xem `risks.md`). Nếu server gửi `-85` trong khi đăng nhập hoặc treo thì dừng acc và báo "Cần captcha" (rule trong `CLAUDE.md`); không xử lý thêm.

## Gói thông tin client (đoán là `setType` / ClientInfo)

Nguồn: `cj_0` quanh dòng 338. Payload theo thứ tự: byte (`bQ.bB`), byte (`aA.bH`), boolean (false), int (`cs_0.cl`), int (`cs_0.dj`), boolean (`av_0.bo`), boolean (`cs_0.cS`), UTF `"<microedition.platform>|2.5.0"`. Mã lệnh là `cj.dN[0]`, **chưa tính** (lớp `cj` nạp được cần thư viện MIDP; chưa có giả lập).

## Gói server → client liên quan

Bộ xử lý gói: `cj_0.b(cN)` (dòng 1999), một `switch` trên mã lệnh với 169 nhánh. Một số nhánh cho thấy:

- `-29` → `cj_0.h`: nhánh con `2` đọc UTF (lưu `al.W`, gọi `al.l`) rồi byte; có vẻ là thông báo/trả lời đăng nhập, **chưa phân tích**.
- `-28` → `this.f`: chưa đọc. `-30` → `this.j`: chưa đọc.
- `-31`: danh sách vật phẩm (số lượng short, mỗi mục short, byte, short, short, rồi mảng byte cặp).
- `-26`: nhận một UTF thông báo (`cs_0.l`).

Bảng mã lệnh và luồng sau đăng nhập (`-28` sub 4, tải bảng dữ liệu, `-30` sub 0) đã ghi ở `messages.md`.

## Đã thấy trên server thật (`bắt-gói`, 2026-10-28)

- Server nhận gói `-29` sub 0 với `"2.5.0"` (hai byte cuối `0, 0`): không gửi `-26`, không ngắt, đưa dữ liệu vào game. **Suy ra**, không có gói riêng báo "thành công".
- `-29` sub 2 từ server là **danh sách server** (UTF), không phải lỗi đăng nhập.
- Gói `-29` sub 2 từ client có đủ như `BuildClientInfo` của probe; một lượt không gửi nó vẫn nhận dữ liệu, nên chưa chứng minh là bắt buộc.
- Luồng thấy được: `-111`, `-74`, `-29` sub 2, `-77`, `-93`, `-28` sub 4, (client xin bảng) `-31`, `-82`, `-89`, `-87`, `-28` sub 6/7/8, `12`, rồi (client gửi `-28` sub 13 và `-38`) `112`, `-42`, `40`, `-22`, `-30` sub 0, `-53`, `-64`, `-90`, `-24`.

## Chưa biết

- Gói báo lỗi đăng nhập, mã lỗi (chưa thử sai mật khẩu; theo rule không thử).
- Server có kiểm tra chuỗi phiên bản hay không (chưa thử giá trị khác).
- Luồng chọn nhân vật và tạo nhân vật (acc thử đã có nhân vật).
- `-38` rỗng có phải điều kiện để server gửi `-30`/`-24` hay không (tương quan trên 1 mẫu).
