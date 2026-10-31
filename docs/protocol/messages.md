# Bảng mã lệnh (cmd)

Nguồn: `Dragonboy250 v3.5.jar` (xem `transport.md`). Mức tin cậy: **từ-mã** cho phần lớn; các dòng đánh dấu `bắt-gói` đã khớp gói thật ngày 2026-10-28 (xem `verification.md`).
Tên lớp/hàm là tên **trong jar** (đã làm rối). Mã lệnh gửi lấy từ mảng `an.bi` do chính lớp tính ra (`scripts/survey/DumpConst.java`), không tính tay.

## Cách đọc

- Mã lệnh là **byte có dấu**. Phần lớn gói dùng chung 3 "kênh" có byte phụ (sub) đứng đầu payload:
  - `-29`: nhóm đăng nhập (sub `0` = đăng nhập, `1` = đăng ký, `2` = khai báo thông tin client).
  - `-28`: nhóm "ngoài map" (sub `4` = phiên bản dữ liệu, `6/7/8` = cập nhật map/skill/item, ...). Client gọi là `messageNotMap`.
  - `-30`: thông tin nhân vật của mình (sub `0` = toàn bộ nhân vật).
- Bảng dưới là **kiểu trường theo thứ tự đọc/ghi trong mã**, lấy tự động bằng script từ bản dịch ngược. Với nhánh có `if`/vòng lặp, danh sách chỉ là các lần đọc xuất hiện, **không phải** một cấu trúc phẳng. Đừng cài đặt chỉ theo bảng này: mở hàm tương ứng đọc lại.
- Cột "Hàm con": nhánh giao cho hàm khác (cần đọc riêng); các nhánh `-30`, `-29`, `-28`, `-24`, `-107`, `-86`, `-44`, `-37..-35`, `66`, `90`... nằm ở đây.
- Mã quan trọng đã đặt tên ở bảng "Mã đã hiểu ý nghĩa"; các mã còn lại chỉ có kiểu trường.
- `ubyte`/`ushort` = `readUnsignedByte`/`readUnsignedShort`.

## Mã đã hiểu ý nghĩa

Tin cậy: `từ-mã` = đọc được rõ từ mã và/hoặc chuỗi log của client; `đoán` = suy từ tên biến/ngữ cảnh. `bắt-gói` = đã thấy trên đường truyền thật và (với gói S→C có bộ đọc) bộ đọc tiêu thụ đúng số byte. "Trường" chỉ liệt kê kiểu theo thứ tự đọc/ghi; tên trường là cách gọi của tôi.

### Kết nối và đăng nhập

| Mã | Hướng | Ý nghĩa | Trường | Tin cậy |
|---|---|---|---|---|
| -27 | S↔C | khóa XOR | xem `transport.md` | bắt-gói |
| -29 sub 0 | C→S | đăng nhập | UTF tên, UTF mật khẩu, UTF `"2.5.0"`, byte, byte | bắt-gói (server chấp nhận, suy ra) |
| -29 sub 1 | C→S | đăng ký (ToolQL không dùng) | 4 UTF | từ-mã |
| -29 sub 2 | C→S | khai báo thông tin client | xem `login.md` | bắt-gói (đã gửi; chưa rõ có bắt buộc) |
| -29 sub 2 | S→C | danh sách server (UTF "Tên:host:cổng:...,...") | byte sub, UTF | bắt-gói |
| -28 sub 4 | S→C | 4 phiên bản dữ liệu: data, map, skill, item | byte×4, byte bỏ, byte n, n×long | bắt-gói |
| -28 sub 6/7/8 | C→S | xin bảng map / skill / item | không | bắt-gói |
| -28 sub 6/7/8 | S→C | trả bảng map / skill / item | cả khối byte (client lưu nguyên) | bắt-gói (đã thấy gói; chưa kiểm cấu trúc bên trong) |
| -28 sub 10 | C→S | xin lưới ô lát của map (khi không có trong `/mymap/`) | byte mapId | bắt-gói |
| -28 sub 10 | S→C | trả lưới ô lát (byte rộng, byte cao, lưới, **1 byte cuối**; thân map nằm ở gói `-24` đã giữ) | xem `map.md` | bắt-gói (byte cuối `00`, ý nghĩa chưa rõ) |
| -87 | C→S | xin bảng dữ liệu chung | không | bắt-gói |
| -87 | S→C | trả bảng dữ liệu chung (log `GET UPDATE_DATA`) | khối byte | bắt-gói (đã thấy gói 3 byte độ dài; chưa kiểm cấu trúc) |
| -28 sub 13 | C→S | báo đã tải xong dữ liệu | không | đoán (đã gửi sau gói `12`; tác dụng chưa rõ) |
| -30 sub 0 | S→C | thông tin nhân vật của mình | xem bên dưới | bắt-gói (tiêu thụ đúng; trường chưa rõ chưa phân tích) |

### Map và di chuyển

| Mã | Hướng | Ý nghĩa | Trường | Tin cậy |
|---|---|---|---|---|
| -24 | S→C | thông tin map (log `MAP_INFO`) | đầu gói + thân: xem `map.md`; ô lát **không** nằm trong gói | bắt-gói (map 23, tiêu thụ đúng; map có quái, vật rơi chưa thấy) |
| -23 | C→S | đổi map qua cổng dưới chân (không dữ liệu) | không | từ-mã |
| -39 | C→S | báo đã tải xong map (`finishLoadMap`, `an_0.dS`), không dữ liệu. Client gửi khi màn hình game hiện lại sau khi nạp map (`cu_0.cv` gọi từ cuối `cj_0.i`), hoặc ngay khi nạp map lỗi (`e_0.o`) | không | bắt-gói (probe gửi sau khi nhận lưới `-28` sub 10: server gửi `-5` 0,03 giây sau; không gửi thì không có `-5`) |
| -85 | S→C | **captcha** (ảnh + chuỗi); ToolQL chỉ nhận ra và báo "Cần captcha" | byte sub; sub 0: ushort độDài, PNG, UTF | đoán có cơ sở |
| -85 | C→S | trả ký tự captcha (không dùng) | char | từ-mã |
| -67 / -32 / -66 | C↔S | xin và nhận ảnh nhỏ / ảnh nền / hiệu ứng (PNG) | xem `resources.md` | từ-mã |
| -82 | S→C | bảng ô lát → cờ va chạm (cần để dựng va chạm) | xem `map.md` | bắt-gói (36 bộ, tiêu thụ đúng) |
| -7 | C→S | mình di chuyển | byte cờ (1 nếu `aw.f(x/U,y/U)==0`, ngược lại 0; ý nghĩa cờ chưa rõ), short x, short y (chỉ gửi khi y đổi) | từ-mã |
| -7 | S→C | người chơi khác di chuyển | int id, short x, short y | bắt-gói (312 gói, luôn 8 byte) |
| -5 | S→C | người chơi khác xuất hiện trong map | xem `map.md` | bắt-gói (4 gói, tiêu thụ đúng; server chỉ gửi sau `-39`, xem `verification.md`) |
| -6 | S→C | người chơi khác rời map (log `PLAYER REMOVED`) | int id | bắt-gói (90 gói, luôn 4 byte) |
| -90 | S→C | người chơi khác đổi trang phục | **12 byte** (1079 gói): byte `1`, int id, short đầu, short thân, short chân, byte; **5 byte** (24 gói): byte `-1`, int id (ý nghĩa chưa rõ, id có thể âm) | bắt-gói (1103 gói, hai dạng, 0 lỗi đọc; 98% id trùng người chơi đang có trong map; "đầu/thân/chân" là suy luận, chưa đối chiếu hình) |
| 18 | S→C | cập nhật vị trí + HP nhiều người chơi | byte n; n×(int id, short x, short y, long hp) | từ-mã |
| -8 | S→C | người chơi khác đổi vị trí/trạng thái (đặt lại vị trí) | int id, byte, short, short | đoán |
| -17 | S→C | đặt trạng thái/vị trí cho **mình** | byte, short, short, long (có thể thiếu) | đoán |

### Quái, chiến đấu, vật phẩm rơi

| Mã | Hướng | Ý nghĩa | Trường | Tin cậy |
|---|---|---|---|---|
| 54 | C→S | mình đánh quái | mỗi quái: byte `chỉSố`; nếu quái đặc biệt (`dX`): byte -1 rồi int id; cuối byte hướng (`bU`) | từ-mã |
| -60 | C→S | mình đánh người chơi | mỗi người: int id; cuối byte hướng | từ-mã |
| -4 / 67 | C→S | đánh cả quái lẫn người (`n`=1 dùng -4, `n`=2 dùng 67) | byte số quái, số quái×byte, số người×int (hoặc -1 nếu null), byte hướng | từ-mã |
| -45 | C→S | dùng skill / chọn skill (log `SKILL_NOT_FOCUS`) | byte… (xem bảng) | đoán |
| -9 | S→C | quái bị đánh: HP mới | ubyte `chỉSốQuái`, long hp, long dame, bool chímạng, byte hiệu ứng | từ-mã |
| -12 | S→C | quái chết (log `SERVER SEND MOB DIE`) | ubyte `chỉSốQuái`, long dame, bool chímạng, byte n; n×(short, short, short, short, int chủ) = vật phẩm rơi | từ-mã |
| -13 | S→C | quái hồi sinh (log `HOI SINH`) | ubyte `chỉSốQuái`, byte, byte, long hp | từ-mã |
| -11 | S→C | quái đánh **mình** | ubyte `chỉSốQuái`, long dame, long (có thể thiếu) | từ-mã |
| -10 | S→C | quái đánh người chơi khác | ubyte `chỉSốQuái`, int id, long dame, long (có thể thiếu) | từ-mã |
| 74 | S→C | quái chết, biến thể có vật rơi | ubyte `chỉSốQuái`, short×4, short×2 | đoán |
| 45 | S→C | quái bị đánh trượt/hiện chữ (dùng chuỗi `aM.bP`) | ubyte `chỉSốQuái`, long hp | đoán |
| 68 | S→C | vật phẩm rơi xuất hiện trong map (log `ADD ITEM TO MAP`) | short×4, int, short (chỉ khi int = giá trị đặc biệt `dN[14]`) | từ-mã (thứ tự nghĩa chưa rõ) |
| -20 | C→S | mình nhặt vật phẩm rơi | short `id` vật rơi | từ-mã |
| -20 | S→C | kết quả nhặt: vật rơi bay về mình, cộng số lượng | short `id`, UTF thông báo, short số lượng… | từ-mã |
| -21 | S→C | vật rơi biến mất khỏi map | short `id` | từ-mã |

Mã quái trong `-9`, `-12`... là **chỉ số trong danh sách quái của map hiện tại** (do `-24` nạp), không phải id loại quái.

### Chat, thông báo, tài sản

| Mã | Hướng | Ý nghĩa | Trường | Tin cậy |
|---|---|---|---|---|
| 44 | C→S | chat trong map | UTF nội dung | từ-mã |
| 44 | S→C | chat trong map của người khác | int id, UTF nội dung | từ-mã |
| 92 | S→C | tin chat có tên / thông báo hệ thống (tên rỗng = hệ thống) | UTF tên, UTF nội dung; nếu có tên: int id, short×5, byte | từ-mã |
| -70 | S→C | thông báo lớn (log `BIG MESSAGE`) | short, UTF | từ-mã |
| 94 | S→C | thông báo chữ | UTF | từ-mã |
| -26 | S→C | thông báo rồi đặt lại màn hình (có vẻ mất kết nối/lỗi) | UTF | đoán |
| -3 | S→C | thay đổi chỉ số (hiện `+n`) | byte loại (0, 3 hoặc khác), int lượng | đoán (ý nghĩa loại chưa rõ) |
| -2 | S→C | thay đổi một loại tiền (hiện `+n`) | int | đoán |
| -1 / 95 | S→C | cộng tiền (hiện `+n`; trường `aI` được định dạng số) | int | đoán (chưa chắc là vàng) |
| 66 (lồng) | S→C | mình chết, mất kinh nghiệm (log `ME DIE XP DOWN NOT IMPLEMENT YET`) | không | từ-mã |
| 40 / 41 | S→C | nhiệm vụ (log `TASK GET`, `TASK NEXT`) | xem bảng | từ-mã |
| -65 | S→C | dịch chuyển (log `TELEPORT`) | int, byte | từ-mã |
| -64 / -63 | S→C | cập nhật túi (log `UPDATE BAG`, `GET BAG`) | int, short / short, byte, short | từ-mã |
| -53 / -62 | S→C | bang hội (log `MY CLAN INFO`, `CLAN_IMAGE`) | xem bảng | từ-mã |

Chưa gán ý nghĩa: các mã còn lại trong bảng bên dưới (ví dụ -107, -86, -44, -37..-35 là các gói lớn do hàm con đọc).

## Gói `-30` sub 0: nhân vật của mình (`cj_0.j`)

Thứ tự đọc (sau byte sub): int `id`, byte, byte `ht` (chủng tộc, theo log `chung toc` của client), short `đầu`, UTF `tên`, byte, byte, long `sức mạnh?`, short, short, byte (chỉ số `cu_0.a[...]`, có thể là loại/phái), byte `n` rồi `n` short (danh sách skill/phần thân), long, int, int, byte `nTrangBị` rồi mỗi mục: short mã vật phẩm (`-1` = trống), int số lượng, 2 UTF, ubyte `nOption` rồi mỗi option đọc bằng `cj_0.a(cN)`; tương tự cho túi (`a[]`) và rương (`b[]`); sau đó short `m` rồi `m` cặp short, 3 short, byte, int (giờ server, giây), byte; rồi client **gửi lại một gói** (mã `dN[80]`, chưa tính) mang `ht`. Cuối cùng short, byte, short (bọc `try`, có thể thiếu).
Tên các trường (HP, MP, vàng, ngọc...) **chưa gán**: cần đối chiếu với nơi hiển thị.
Đã đối chiếu lại với `cj_0.j` (2026-10-28): một option (`cj_0.a(cN)`) là **short mã + int giá trị** (mã -1 client bỏ qua nhưng vẫn đọc đủ 6 byte); ô vật phẩm mã -1 **không đọc thêm gì**; số option của trang bị đọc **không dấu**, của túi và rương đọc có dấu; thứ tự sau tên là byte, byte, long, short, short, byte lớp, byte n + n short, long, int, int, rồi ba mảng ô. Bộ đọc: `ToolQL.Protocol.Messages.CharacterInfo`.

## Luồng sau đăng nhập (đọc từ mã, chưa chạy)

1. C→S `-29` sub 0 (đăng nhập) và `-29` sub 2 (thông tin client). Xem thứ tự thật ở `login.md`.
2. S→C `-28` sub 4: server báo 4 phiên bản dữ liệu. Client so với bản đã lưu (kho `NRdata`, `NRmap`, `NRskill`, `NRitem0/1/100/101`).
3. Nếu lệch: C→S yêu cầu `-87` (dữ liệu chung), `-28` sub 6 (map), 7 (skill), 8 (item); S→C trả cùng sub; client lưu lại.
4. Khi đủ: C→S `-28` sub 13 (`dR`); S→C `-30` sub 0 (nhân vật) và `-24` (map).
5. Cuối gói `-28` sub 4 còn `byte n` rồi `n` long (`cu_0.h`, nhãn log "CAPTION").

Hệ quả cho ToolQL: **bảng map/skill/item không nằm trong jar mà do server gửi**, ToolQL phải tự tải và lưu cục bộ (xem `resources.md` khi viết). Chưa khảo sát định dạng.


## Bảng server → client (`cj_0.b`)

| Mã | Dòng | Trường đọc trực tiếp (theo thứ tự trong mã) | Hàm con |
|---|---|---|---|
| -112 | 2008 | byte, byte, byte, short |  |
| -107 | 2020 | byte, short, ubyte, short, int, utf, utf, ubyte, long, long, long, long, long, utf, utf, long, long,… (27 trường) | cj_0.a |
| -99 | 2096 | byte, ubyte, int, short, short, short, short, short, utf, utf, boolean |  |
| -98 | 2124 | byte, utf, utf |  |
| -97 | 2131 | int |  |
| -96 | 2135 | byte, utf, byte, int, int, short, short, short, short, utf, utf, utf |  |
| -95 | 2166 | byte, int, short, long, int, byte, int, int, long, long, int, int, long, long, int, byte, int, long,… (21 trường) |  |
| -94 | 2338 | short, int |  |
| -93 | 2350 | short, byte |  |
| -92 | 2358 | byte |  |
| -91 | 2369 | byte, utf, utf |  |
| -90 | 2382 | byte, int, short, short, short, byte |  |
| -88 | 2441 |  |  |
| -87 | 2446 | (đọc hết) |  |
| -86 | 2462 | byte, int, int, byte, int, byte, short, int, ubyte | cj_0.a |
| -85 | 2534 | byte, ushort, utf |  |
| -84 | 2556 | ubyte, long |  |
| -83 | 2569 | byte, short, short, ubyte, int, utf, short, short, byte |  |
| -82 | 2607 | byte, byte, int, byte, byte |  |
| -81 | 2626 | byte, utf, utf, byte, byte, short, short, short, short, short, short, short, short |  |
| -80 | 2710 | byte, ubyte, int, short, short, short, short, short, utf, boolean, utf, utf, int, boolean, int |  |
| -79 | 2760 | int, long, utf |  |
| -77 | 2771 | short, byte |  |
| -76 | 2781 | byte, byte, utf, utf, short, boolean, boolean, ubyte |  |
| -75 | 4732 | ubyte, byte |  |
| -73 | 4746 | byte, byte |  |
| -70 | 2806 | short, utf |  |
| -69 | 2816 | short |  |
| -68 | 2820 | short |  |
| -67 | 2824 | int |  |
| -66 | 2847 | short, byte |  |
| -65 | 2861 | int, byte |  |
| -64 | 2890 | int, short |  |
| -63 | 2904 | short, byte, short |  |
| -62 | 2919 | ubyte, byte, short |  |
| -61 | 2932 | int, int, int |  |
| -60 | 2945 | int, byte, byte, int, ubyte, byte, int, byte, byte, long, boolean, boolean, long, boolean, boolean |  |
| -59 | 3028 | byte, int, int, utf |  |
| -57 | 3036 | utf, int, int |  |
| -53 | 3043 | int, utf, utf, short, utf, utf, ubyte, ubyte, byte, int, byte, int, short, short, short, short, utf,… (25 trường) |  |
| -52 | 3102 | byte, int, short, short, short, short, utf, byte, utf, int, int, int, int, byte, int, short, short, … (26 trường) |  |
| -51 | 3155 |  |  |
| -50 | 3162 | byte, int, short, short, short, short, utf, byte, utf, int, int, int, int |  |
| -47 | 3189 | byte, int, utf, utf, short, utf, utf, ubyte, ubyte, int |  |
| -46 | 3219 | byte, short, short, utf, int, int, short, utf |  |
| -45 | 3251 | byte, int, short, byte, byte, short, byte, byte, byte, short, short, short, short, byte, byte, byte,… (29 trường) |  |
| -44 | 3486 | byte, ubyte, utf, ubyte, ubyte, short, int, int, int, utf, int, int, long, short, int, int, byte, in… (29 trường) | cj_0.a |
| -43 | 3591 | byte, byte, byte, utf |  |
| -42 | 3599 | int, long, long, long, long, byte, byte, byte, byte, long, long, byte, long, short, int, byte, byte, short |  |
| -41 | 3623 | byte, utf |  |
| -37 | 3631 | byte, short, ubyte, short, int, utf, utf, ubyte | cj_0.a |
| -36 | 3672 | byte, ubyte, short, int, utf, utf, ubyte, byte, int | cj_0.a |
| -35 | 3736 | byte, ubyte, short, int, utf, utf, ubyte, byte, byte, int | cj_0.a |
| -34 | 3786 | byte, short, utf, short, short, byte, short, short, utf, int, byte, byte, byte, boolean, utf, short, int |  |
| -32 | 3836 | short, int |  |
| -31 | 3857 | short, short, byte, short, short, byte, byte, byte |  |
| -30 | 3879 |  | this.j |
| -29 | 3883 |  | cj_0.h |
| -28 | 3887 |  | this.f |
| -26 | 3891 | utf |  |
| -25 | 3901 | utf |  |
| -24 | 3905 | ubyte, byte, byte, byte, byte, utf, byte, byte | this.i |
| -22 | 3946 |  |  |
| -21 | 3966 | short |  |
| -20 | 3975 | short, utf, short, short, short, short |  |
| -19 | 4042 | short, int |  |
| -18 | 4062 | byte, short, short, short |  |
| -17 | 4763 | byte, short, short, long |  |
| -16 | 4777 |  |  |
| -14 | 4068 | int, short, short, short, short |  |
| -13 | 4790 | ubyte, byte, byte, long |  |
| -12 | 4810 | ubyte, long, boolean, byte, short, short, short, short, int |  |
| -11 | 4852 | ubyte, long, long |  |
| -10 | 4881 | ubyte, int, long, long |  |
| -9 | 4909 | ubyte, long, long, boolean, byte |  |
| -8 | 4946 | int, byte, short, short |  |
| -7 | 4953 | int, short, short |  |
| -6 | 4976 | int |  |
| -5 | 4994 | int, int, byte, byte, short, byte, byte, short, byte, short |  |
| -4 | 4076 | int, ubyte, ubyte, byte, byte, int |  |
| -3 | 5083 | byte, int |  |
| -2 | 5109 | int |  |
| -1 | 5116 | int |  |
| 0 | 4117 |  | cj_0.l |
| 1 | 4121 | boolean, utf |  |
| 2 | 4134 |  |  |
| 6 | 4145 | long, int, int |  |
| 7 | 4155 | byte, short, utf |  |
| 11 | 4162 | short, byte, byte |  |
| 12 | 4199 |  | this.d |
| 18 | 5125 | byte, int, short, short, long |  |
| 19 | 5144 | ushort, ushort |  |
| 20 | 4207 |  | this.k |
| 24 | 4211 |  | this.n |
| 27 | 4215 | utf, byte, utf, short |  |
| 29 | 4227 |  |  |
| 32 | 4231 | short, utf, byte, utf, utf, byte, utf, short |  |
| 33 | 4270 | utf |  |
| 38 | 4295 |  |  |
| 39 | 4298 |  |  |
| 40 | 4303 | short, byte, utf, utf, byte, utf, byte, short, utf, short, short |  |
| 41 | 4342 |  |  |
| 43 | 4351 | short, short, short |  |
| 44 | 5151 | int, utf |  |
| 45 | 5160 | ubyte, long |  |
| 46 | 4366 | short, short |  |
| 47 | 4371 |  |  |
| 50 | 4375 | byte, short, utf, utf |  |
| 54 | 4389 | int, ubyte, byte |  |
| 56 | 4424 | int, long, long, boolean, byte, long, long, boolean, byte |  |
| 57 | 4482 | utf |  |
| 58 | 4488 | int, short, short, int |  |
| 62 | 4508 | int |  |
| 63 | 4520 | int |  |
| 64 | 4529 | int |  |
| 65 | 4540 | byte, utf, short |  |
| 66 | 4563 |  | this.e |
| 66 | 5174 |  |  |
| 68 | 4567 | short, short, short, short, int, short |  |
| 69 | 4589 | byte |  |
| 74 | 5178 | ubyte, short, short, short, short |  |
| 81 | 4593 | ubyte, boolean |  |
| 82 | 4598 | ubyte, boolean |  |
| 83 | 4602 | int, ubyte |  |
| 84 | 4613 | int, short, short |  |
| 85 | 4630 | ubyte, boolean |  |
| 86 | 4635 | ubyte, ubyte, boolean |  |
| 87 | 4642 | ubyte, boolean |  |
| 88 | 4646 | utf, short |  |
| 90 | 4652 |  | this.g |
| 92 | 4656 | utf, utf, int, short, short, short, short, short, byte |  |
| 94 | 4688 | utf |  |
| 95 | 5198 | int |  |
| 96 | 5206 | byte, short, short, utf, utf, byte, byte |  |
| 97 | 5210 | byte, short, short, utf |  |
| 112 | 4692 | byte, short, utf, byte, utf, byte, short, utf |  |

## Bảng client → server (lớp `an`)

Một hàm có thể gửi nhiều mã (ví dụ tấn công). Cột "Trường ghi" gộp mọi lệnh ghi trong hàm.

| Mã | Hàm gửi (tên trong jar) | Trường ghi |
|---|---|---|
| -127 | `b(byte by, byte by2)` | byte, byte |
| -125 | `a(av_0[] av_0Array)` | byte, utf |
| -121 | `dO()` |  |
| -120 | `dE()` |  |
| -118 | `O(int n)` | int |
| -114 | `dC()` |  |
| -113 | `b(byte[] byArray)` | byte |
| -111 | `b(cT cT2)` | short, utf |
| -110 | `a(byte by, int n, byte[] byArray)` | byte, int, short |
| -108 | `g(byte by)` | byte |
| -107 | `dG()` |  |
| -105 | `dH()` |  |
| -104 | `v(int n)` | int |
| -103 | `d(byte by, byte by2)` | byte, byte |
| -101 | `o(String string)` | utf, byte |
| -100 | `a(byte by, int n, byte by2, int n2, int n3)` | byte, short, byte, int, int, short, short, byte, int, byte, byte, short |
| -99 | `b(byte by, int n)` | byte, int |
| -96 | `a(String string, byte by)` | utf, byte |
| -91 | `E(int n)` | byte |
| -87 | `dW()` |  |
| -86 | `a(byte by, int n, byte by2, int n2)` | byte, int, byte, int, byte |
| -85 | `a(char c)` | char |
| -81 | `a(byte by, cT cT2)` | byte, byte, byte |
| -80 | `a(byte by, int n)` | byte, int |
| -79 | `u(int n)` | int |
| -78 | `K(int n)` | int |
| -76 | `H(int n)` | byte |
| -74 | `b(byte by, cT cT2)` | byte |
| -72 | `c(String string, int n)` | int, utf |
| -71 | `p(String string)` | utf |
| -67 | `w(int n)` | int |
| -66 | `b(short s)` | short |
| -63 | `B(int n)` | short |
| -62 | `d(byte by)` | byte |
| -59 | `a(byte by, byte by2, int n)` | byte, byte, int |
| -57 | `a(byte by, int n, int n2, int n3)` | byte, int, int, int |
| -56 | `b(int n, byte by)` | int, byte |
| -55 | `dN()` |  |
| -54 | `J(int n)` | int |
| -51 | `a(int n, String string, int n2)` | byte, utf, int |
| -50 | `P(int n)` | int |
| -49 | `a(int n, byte by)` | int, byte |
| -47 | `r(String string)` | utf |
| -46 | `a(byte by, int n, String string)` | byte, short, utf |
| -45 | `b(byte by)` | byte |
| -45 | `a(byte by, byte by2, short s, short s2)` | byte, byte, short, short, byte, short, short |
| -43 | `a(byte by, byte by2, byte by3, short s)` | byte, byte, byte, short |
| -40 | `c(byte by, byte by2)` | byte, byte |
| -39 | `dS()` |  |
| -38 | `z(int n)` | int |
| -38 | `dI()` |  |
| -34 | `c(byte by)` | byte |
| -34 | `f(byte by)` | byte |
| -33 | `dQ()` |  |
| -32 | `c(short s)` | short |
| -30 | `f(int n, int n2)` | byte, byte, short |
| -30 | `a(int n, short s)` | byte, int, short |
| -30 | `N(int n)` | byte, int |
| -29 | `a(byte by)` | byte |
| -29 sub 0 | `a(String string, String string2, String string3, byte by)` | utf, utf, utf, byte, byte |
| -29 sub 1 | `a(String string, String string2, String string3, String string4)` | utf, utf, utf, utf |
| -29 sub 2 | `dF()` | byte, byte, boolean, int, int, boolean, boolean, utf, short |
| -28 | `a(String string, int n, int n2)` | byte, utf, byte, byte |
| -28 | `b(byte by)` | byte |
| -28 sub 10 | `s(int n)` | byte |
| -28 sub 13 | `dR()` |  |
| -28 sub 16 | `c(String string, String string2)` | utf, utf |
| -28 sub 18 | `b(String string, int n)` | int, utf |
| -28 sub 37 | `M(int n)` | int |
| -28 sub 39 | `y(int n)` | int |
| -28 sub 41 | `I(int n)` | int |
| -28 sub 6 | `dB()` |  |
| -28 sub 7 | `dT()` |  |
| -28 sub 8 | `dU()` |  |
| -23 | `dD()` |  |
| -20 | `A(int n)` | short |
| -16 | `dK()` |  |
| -15 | `dX()` |  |
| -7 | `dJ()` | byte, byte, short, short |
| -4/67/54/-60 | `a(cT cT2, cT cT3, int n)` | byte, byte, int, int, byte, byte, int, int, byte |
| 6 | `a(byte by, int n, int n2)` | byte, short |
| 7 | `a(byte by, byte by2, short s)` | byte, byte, short |
| 11 | `t(int n)` | short |
| 13 | `a(cE[] cEArray)` | byte |
| 17 | `s(String string)` | utf |
| 18 | `L(int n)` | int |
| 21 | `G(int n)` | byte |
| 22 | `a(int n, int n2, int n3)` | byte, byte, byte |
| 27 | `a(short s)` | short |
| 29 | `dV()` |  |
| 32 | `a(short s, byte by)` | short, byte |
| 33 | `F(int n)` | short |
| 34 | `C(int n)` | short |
| 37 | `D(int n)` | int |
| 42 | `a(String string, String string2, String string3, String string4, String string5, String string6, String string7, String string8, String string9)` | utf, utf, utf, utf, utf, utf, utf, utf, utf |
| 44 | `n(String string)` | utf |
| 50 | `dL()` |  |
| 53 | `m(String string)` | utf |
| 66 | `q(String string)` | utf |
| 69 | `dP()` |  |
| 76 | `Q(int n)` | int |
| 77 | `x(int n)` | int |
| 88 | `a(short s, String string)` | short, utf |
| 112 | `e(byte by)` | byte |
| 125 | `h(byte by)` | byte |
| 127 | `e(int n, int n2)` | byte, short |
