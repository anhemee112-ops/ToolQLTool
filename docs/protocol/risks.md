# Rủi ro và điều cần lưu ý (từ khảo sát jar)

Nguồn: `Dragonboy250 v3.5.jar`. Mức tin cậy: **từ-mã**.

## Nguồn gốc client và giấy phép

- Đây là **bản mod của bên thứ ba** ("Dragonboy250 v3.5", tác giả ghi trong client "Hùng Hero", giao thức 2.5.0), không phải client chính thức. Gói `nro`, J2ME/MIDlet.
- Mã bị làm rối: tên lớp 1-2 chữ cái, chuỗi mã hóa XOR (`Y.b`), hằng số viết bằng phép toán. Đã giải chuỗi (4712 chuỗi, không lỗi) để đọc.
- **Giấy phép chưa rõ.** Theo `CLAUDE.md`, hỏi chủ repo trước khi push công khai bất cứ thứ gì dẫn xuất từ client này. Mã dịch ngược để ngoài repo (`C:\Dev\ToolQL-decomp`), không commit.

## Điều khiển từ xa qua Firebase (tính năng của bản mod)

- Lớp `dj` (màn "Thiết Lập > Điều Khiển Từ Xa") và `S`, `H` (HTTP): khi bật, client gửi/đọc dữ liệu nhân vật qua `https://nro-controller-default-rtdb.firebaseio.com` (đường dẫn `/users/<UserID>/chars/<tên nhân vật>.json` và `.../chat_cmd.json`). Người dùng có thể đổi URL và UserID.
- Mặc định là **Tắt** (chỉ số chọn 1 = "Tắt"). Chưa kiểm chứng khi chạy.
- ToolQL **không dùng** tính năng này. Lưu ý: nếu từng bật, dữ liệu nhân vật đã gửi ra dịch vụ bên thứ ba.

## Server có thể phát hiện bản mod hoặc tool

- Client khai báo phiên bản `"2.5.0"` và chuỗi `"<nền tảng>|2.5.0"`. Chưa biết server kiểm tra gì.
- Tự động hóa trên server chính thức có thể bị khóa acc (nhắc theo `CLAUDE.md`).
- Tần suất gói, thứ tự gói bất thường là chỗ dễ bị phát hiện: chưa khảo sát.

## Captcha

- **Có cơ chế captcha trong giao thức (gói `-85`).** Sửa nhận định trước đó ("không thấy captcha"): không có chuỗi "captcha" nhưng log của client ghi `cap char c=` ở hàm gửi. Dấu hiệu: server gửi `-85` sub 0 kèm một ảnh PNG (`ushort độDài` + byte) và một chuỗi UTF; client hiển thị ảnh, người chơi nhập, client trả lại **từng ký tự** bằng gói `-85` (một `char`). Sub 1 và 2 của `-85` đặt cờ trạng thái (`cP.bj`, `cP.aJ`). Mức tin cậy: đoán có cơ sở (từ-mã, chưa thấy trên server).
- **Quy tắc của dự án:** không viết code đọc, giải hay né captcha. ToolQL chỉ **nhận ra** gói `-85` và dừng acc đó, báo "Cần captcha" cho người dùng. Không có đoạn nào trong tài liệu này nhằm giải nó.

## Việc cần làm tiếp cho mục này

- Tìm mọi chỗ client gửi dữ liệu ra ngoài khác (`H`, `S`, `ab_0` có URL `dragonball.indonaga.com/coda/?username=`).
- Đối chiếu với bản client chính thức khi chủ repo cung cấp.
