# Kết quả kiểm chứng giao thức (cột mốc 0, bắt gói ngày 2026-10-28 và 2026-10-30)

Mục đích: đối chiếu `docs/protocol/*.md` (mức `từ-mã`) với gói thật. Chỉ ghi điều **đã thấy** trong tệp bắt gói; chỗ chưa thấy ghi "chưa kiểm". Mức tin cậy ở các file khác đã đổi theo từng mục (xem "Đã xử lý sau khi kiểm").

## Nguồn và cách kiểm

- 8 lượt chạy `ToolQL.Probe` trên server thật bằng acc thử (lượt 1-5 ngày 2026-10-28, lượt 6-8 ngày 2026-10-30), tệp nằm ngoài repo (`%USERPROFILE%\.toolql\captures\<ngày-giờ>\packets.jsonl`, không commit). Gói đăng nhập `-29` sub 0 được che, không có nội dung.
- Phương pháp: chạy lại các bộ đọc của `ToolQL.Protocol` trên hex payload đã giải mã trong tệp bắt gói (offline, không nối mạng), so số byte đã tiêu thụ với độ dài payload.
- Tên server không có trong tệp bắt gói. Lượt 6 chạy trên "Vũ trụ 1" (`dragon1.teamobi.com:14445`); các lượt 1-5 không ghi server nên không khẳng định.

| Lượt | Giờ (UTC) | Số gói | Đi tới đâu | Ghi chú |
|---|---|---|---|---|
| 1 | 05:19 | 2 | bắt tay khóa `-27` | giai đoạn 1 (`handshake`), chưa đăng nhập |
| 2 | 05:27 | 21 | tải bảng | chưa gửi `-29` sub 2 trước đăng nhập; gửi `-28` sub 13 trước gói `12` (sau đã sửa) |
| 3 | 05:35 | 7 | `-29` sub 2 từ server | dừng ở đây; theo commit `e292a8e`, probe lúc đó hiểu nhầm `-29` sub 2 là lỗi đăng nhập |
| 4 | 05:38 | 25 | tải bảng xong | gửi `-28` sub 13 sau gói `12`, **chưa** gửi `-38`; không thấy `-30`, `-24` (chưa rõ vì sao dừng) |
| 5 | 05:45 | 49 | vào game, nhận `-30`, `-24`, `-28` sub 10 | lượt đầy đủ nhất; hơn 1,5 giây từ lúc nối |
| 6 | 2026-10-30 19:32 | 738 | vào game, nghe thụ động 120 giây | `probe login 1 --xac-nhan --nghe 120`; sau giây 2,2 probe **không gửi gì nữa**; `exit.txt` ghi "hết giờ nghe, server vẫn giữ kết nối"; map 14 |
| 7 | 2026-10-30 12:39 UTC | 170 | vào game, nghe 30 giây | định chạy với `-39` nhưng **bản build cũ** (đồng hồ máy ảo lệch làm MSBuild bỏ qua), nên **không gửi `-39`**; vô tình thành lượt đối chứng: không `-39` thì không `-5` (xem mục 9) |
| 8 | 2026-10-30 12:41 UTC | khoảng 170 | vào game, gửi `-39`, nghe 30 giây | `probe login 1 --xac-nhan --nghe 30` với bản build có `-39`; map 14; server giữ kết nối |

## Kết quả theo danh sách trong plan (N9)

| # | Mục | Kết quả | Chi tiết |
|---|---|---|---|
| 1 | `-27`: bố cục, biến đổi khóa, chỉ số XOR hai chiều | **Đúng** | Cả 5 lượt: `n` (độ dài khóa) từ 65 đến 84, thay đổi mỗi lần nối; sau `n` byte khóa là UTF (12 ký tự), int `12345`, byte `0`; bộ đọc tiêu thụ đúng hết payload. Sau đó mọi gói đều đọc mạch lạc ở cả hai chiều và server trả lời gói ta gửi, nên khóa và hai chỉ số `R`/`Q` đúng. Hạn chế: chưa có phép thử riêng từng công thức, chỉ là "cả luồng khớp". |
| 2 | Nhóm mã lệnh độ dài 3 byte, công thức cộng 128 | **Đúng với 3 mã**, chưa kiểm 5 mã | Thấy `-74` (5 byte), `-87` (202 042 byte, vượt 65 535 nên không thể là 2 byte), `12` (145 016 byte) đều là gói 3 byte và các gói sau vẫn khớp khung. Chưa thấy `-32`, `-66`, `11`, `-67`, `66`. |
| 3 | Server có chấp nhận `"2.5.0"`; gói trả lời đăng nhập | **Chấp nhận (suy ra)**; không có gói "thành công" riêng | Sau `-29` sub 0 server không gửi `-26`, không ngắt, và đưa dữ liệu vào game (`-30`, `-24`). Không thấy gói nào chỉ dành riêng để báo "đăng nhập thành công". Chưa thử phiên bản khác nên không biết server có kiểm tra phiên bản hay không. Chưa thấy gói báo lỗi đăng nhập (chưa thử sai mật khẩu, và theo rule không được thử). |
| 4 | `-28` sub 4 | **Đúng** | Bốn phiên bản `data=29, map=63, skill=7, item=24`, 21 caption (long), tiêu thụ đúng. Đến khoảng 1 giây sau gói `-77`/`-93`. Client phải trả lời: chưa kiểm (probe luôn xin cả bốn bảng). |
| 5 | Yêu cầu bảng: `-87`, `-28` sub 6/7/8 | **Server trả bảng**, bộ đọc bảng **chưa có** | Probe gửi `-87` rỗng rồi `-28` sub 6, 7, 8 (mỗi gói 1 byte). Server trả `-87` (3 byte độ dài, 202 042 byte), `-28` sub 6 (9 071), sub 7 (16 678), sub 8 (9 800), rồi gói `12` (3 byte, 145 016, sub 0). Sau đó thêm hai gói `-28` sub 8: phiên bản 24, loại `101` (9 byte) và loại `100` (368 byte); gói `12` mang loại `1`. Mô tả "sub, phiên bản, loại" trong `LoginCommand` khớp với dữ liệu. Chưa kiểm bằng bộ đọc nên chưa xác nhận cấu trúc bên trong. |
| 6 | Lúc server gửi `-82`; bộ đọc tiêu thụ đúng | **Đúng** | 36 bộ, tiêu thụ đúng. Server gửi `-82` (và `-31`, 3 738 byte) gần như **ngay sau khi ta gửi** `-87`/`-28` sub 6-8, trước các bảng lớn, không phải lúc vào game. Chưa rõ nó là phản hồi của `-87` hay do `-28` sub 4. |
| 7 | `-30` sub 0 | **Tiêu thụ đúng** | Nhân vật chủng tộc 2, 7 skill, 6 món trang bị, 6 món trong túi, 11 trong rương, không thừa/thiếu byte. Giá trị các trường "chưa rõ" (như `ht`) **chưa phân tích**. Acc thử **có nhân vật** (nhận định "cần tạo nhân vật" trong thông điệp commit `ca99d22` không xảy ra ở lượt 5; không thấy gói mã `2`). |
| 8 | `-24` | **Tiêu thụ đúng** (một trường hợp) | Map 23, 3 NPC, 2 cổng, 0 quái, 0 vật rơi, byte `bf` cuối có mặt (giá trị 0) và được đọc đúng. Lưới ô lát: probe chưa có lưới cục bộ nên gửi `-28` sub 10 với mã map (`0A 17`), server trả. Chưa kiểm: trường hợp có lưới cục bộ, map có quái/vật rơi, map khác. Chiều rộng, cao: 32 và 20 (không âm). **Lượt 6:** map 14 (4 quái, 3 NPC, 3 cổng, 0 vật rơi) cũng tiêu thụ đúng, nên bộ đọc quái đã khớp dữ liệu thật. |
| 8b | `-28` sub 10 | **Sai ở một chỗ**: thừa 1 byte | Gói 644 byte = sub (1) + rộng (1) + cao (1) + 32×20 = 640 ô + **1 byte cuối giá trị `00`**. `map.md` ghi "và **hết**" và đã nói rõ "nếu thấy byte thừa sau lưới thì nhận định này sai". Vậy nhận định sai: sau lưới có 1 byte. Lượt 6 lặp lại: lưới 52×24 của map 14 (1252 byte) cũng kết thúc bằng 1 byte `00`. Ý nghĩa byte đó chưa rõ (có thể là `bf`, chưa xác nhận). Đã sửa bộ đọc `MapGrid` (xem cuối file). |
| 9 | `-5` và các gói thấy trong vài giây đầu | **Đúng, nhưng chỉ sau khi client gửi `-39`** | Lượt 6 (120 giây) và lượt 7 (30 giây, đều **không** gửi `-39`) ở map 14 đông người: có `-7` (312 và 73 gói) và `-6`, **không có `-5`**; mọi id người chơi đều lần đầu xuất hiện bằng `-7`. Đọc mã: `-5` là gói duy nhất thêm người vào map, và client thật gửi `-39` rỗng (`finishLoadMap`) sau khi nạp xong map. **Lượt 8** (probe gửi `-39` rỗng sau khi nhận `-24` và lưới `-28` sub 10): server gửi **4 gói `-5`** (69, 73, 71, 60 byte) chỉ 0,03 giây sau `-39`, **bộ đọc `PlayerAppear` tiêu thụ đúng cả 4**; mỗi gói `-5` kèm một gói `-30` sub 8 (6 byte, chưa có bộ đọc). Cả 4 id đều có `-5` đi trước mọi `-7`/`-6` của chính id đó (4/4). Sau `-39` còn có `-42`, `-70` (439 byte) và `93` (133 byte). Mẫu nhỏ (1 lượt có `-39`, 4 người); chưa kiểm với map đông hơn hay nhiều lần. Chưa rõ vì sao lượt 8 chỉ có 4 gói `-7` trong 30 giây trong khi lượt 7 có 73 (có thể do số người trong map khác nhau mỗi lúc, không kết luận). Các gói khác thấy ở lượt 5, đều chưa có bộ đọc, thứ tự: `-111`, `-74`, `-29` sub 2 (danh sách server, 821 byte), `-77`, `-93`, `-31`, `-89`, `112`, `-42` (3 lần, 83 byte), `40`, `-22`, `-53`, `-64`, `-90`, `-30` sub 4, `92` (2 lần), `-69`, `-68`, `-97`, `-107`, `65`, `-113`, `50`. |
| 9b | Gói thấy khi đang ở trong map (lượt 6) | Đã đếm; chỉ `-7`, `-6` khớp bố cục | 120 giây sau khi nhận lưới: `-7`×312, `-6`×90, `-9`×53, `-30` sub 14×45, `54`×37, `-95`×32, `24`×27, `44`×24, `124`×15, `92`×9, `93`×9, `-12`×4, `-90`×4, `-30` sub 7×4, `-64`×3, `-45`, `-68`, `-94` mỗi mã 1. `-7` luôn 8 byte (int id + short x + short y) và `-6` luôn 4 byte (int id): **khớp** `messages.md`. Các mã còn lại chưa có bộ đọc nên chưa kiểm. |
| 10 | Gói định kỳ (ping) để không bị ngắt | **Không cần ping trong 120 giây** (một lượt) | Lượt 6: từ giây 2,2 probe không gửi gì nữa, 120 giây sau server vẫn giữ kết nối (không ngắt, không `-26`, không `-85`). Server liên tục gửi gói (738 gói), nên chưa phân biệt "server không đòi ping" với "luồng gói của server đủ giữ kết nối". Không thấy gói nhỏ đều đặn nào có dạng ping. **Chưa kiểm** cho thời gian dài hơn 2 phút hoặc map vắng người. |

## Điểm đáng chú ý khác

- Gói `-29` sub 2 từ server là **danh sách server** (đã sửa ở commit `e292a8e`), không phải lỗi đăng nhập.
- Lượt 2 chưa gửi `-29` sub 2 trước đăng nhập mà server vẫn trả dữ liệu; nên `-29` sub 2 phía client **chưa chứng minh là bắt buộc** (chỉ một mẫu).
- Chỉ ở lượt 5 probe gửi `-38` rỗng sau `-28` sub 13, và chỉ lượt này nhận được `-30`, `-24`. Lượt 4 gửi `-28` sub 13 nhưng không có `-38` và không thấy `-30`. Đây chỉ là **tương quan trên 1 mẫu mỗi bên**; không đủ để kết luận `-38` là điều kiện, và chưa rõ lượt 4 dừng vì hết thời gian chờ hay lý do khác (tệp bắt gói không ghi lý do thoát).
- Gói `-24` đến trước khi ta gửi `-28` sub 10, đúng với mô tả "client xin lưới sau khi nhận `-24`".
- Chưa có gói nào cho thấy captcha `-85` hay thông báo `-26` (6 lượt, kể cả 120 giây ở lượt 6).

## Chưa kiểm chứng (liệt kê gọn)

- Khung 3 byte với `-66`, `66` (`-32`, `11`, `-67` đã đúng ở lượt 12).
- Server có kiểm tra phiên bản `"2.5.0"` hay không; hành vi khi sai mật khẩu hoặc bị từ chối.
- Cấu trúc bên trong các bảng `-87`, `-28` sub 6/7/8, `12` và các gói trong cửa sổ đầu (chưa có bộ đọc).
- Việc không gửi `-28` sub 6/7/8 (hoặc giữ phiên bản cục bộ khớp) có được không; client có phải trả lời `-28` sub 4 hay không.
- `-5` với số người đông hơn và nhiều lần (mới có 4 gói ở 1 lượt), mã `2` (tạo nhân vật), `-85` (captcha), nhân vật mới.
- Ping và độ ổn định khi giữ kết nối quá 2 phút; nối lại sau khi mất kết nối.
- Hành vi trên server khác với server đã thử.

## Đã xử lý sau khi kiểm

- `map.md` và `MapGrid` đã sửa cho byte cuối của `-28` sub 10 (thuộc tính `Trailer`, có test); chạy lại trên gói thật thì `ExtraBytes = 0`.
- Giả thuyết `-39` → `-5` đã kiểm ở lượt 8 và đúng; `messages.md` và `map.md` đã ghi `-5` và `-39` là `bắt-gói`. Probe gửi `-39` một lần sau khi nhận lưới.
- Mức tin cậy `bắt-gói` đã ghi theo từng mục ở `transport.md`, `login.md`, `map.md`, `messages.md` (không đổi cả file).

## Việc tiếp

- Đọc `-30` sub 8 (kèm mỗi `-5`) và bố cục gói `-5` kỹ hơn (tên, trang bị) khi dựng trạng thái người chơi.
- Chạy build bắt buộc (`--no-incremental`) khi sửa probe: đồng hồ máy ảo lệch từng làm MSBuild dùng DLL cũ.
- Nếu cần kết luận về ping: một lượt nghe lâu hơn (vài phút) ở map vắng người, cần bạn đồng ý trước. Probe đã có `--nghe <giây>` và ghi lý do thoát vào `exit.txt`.

## M1: lượt 9, `ToolQL.Host` giữ 11 phút (2026-10-30 21:57 giờ máy ảo)

Lệnh: `host 1 --xac-nhan --phut 11 --bat-goi` (bản Release, build `--no-incremental`), acc thử, "Vũ trụ 1". Tệp bắt gói ngoài repo: `%USERPROFILE%\.toolql\captures\20261030-215702` (4639 dòng). Mã thoát 6 (hết giờ mà chưa ở trong game).

| Điều kiện M1 | Kết quả |
|---|---|
| Vào game bằng `AccountSession` (không phải probe) | **Đạt**: `Connecting → LoggingIn → Loading → InGame` trong 1 giây. |
| Trạng thái in ra khớp | **Đạt một phần**: map 44 "Vách núi Kakarot", nhân vật `yskhha` ở (1063,432), 1 quái, 2-6 người khác thay đổi theo thời gian, 0 lỗi đọc gói trong cả phiên. "Khớp" chỉ có nghĩa bộ đọc tiêu thụ đúng byte và số liệu hợp lý; **chưa đối chiếu với màn hình game thật**. |
| Giữ ≥ 10 phút | **Đạt**: InGame liên tục từ 00:01 đến 10:16 (10 phút 15 giây), không gửi ping. Tại 10:20 server đóng kết nối ("Hết dữ liệu khi đọc khung gói"). Chưa biết lý do (hết thời hạn phía server, hay do thiếu ping, hay do acc bị đá); không có gói báo trước trong tệp bắt gói mà mình đã xem. |
| Tự nối lại có chờ tăng dần | **Chưa đạt**: phiên chuyển `Reconnecting`, sau 5 giây nối lại và gửi đăng nhập, rồi **kẹt ở `LoggingIn` đến hết giờ (khoảng 36 giây)**, không vào lại game. Server sau lần nối lại chỉ gửi `-29` sub 2, `-77`, `-93`, `122` (2 byte) rồi im; không có `-28` sub 4. Chưa rõ vì sao (có thể server còn giữ phiên cũ, có thể thiếu một bước). Chưa thử ngắt socket chủ động nên cũng chưa kiểm chuỗi chờ 5/10/20 giây ngoài đời thật (đã có test với đồng hồ giả). |

Việc cần làm tiếp cho M1:
- Xem vì sao nối lại kẹt ở `LoggingIn`: đối chiếu thứ tự gói của lần nối lại với lần đầu trong tệp bắt gói; xem `LoginFlow` có đợi `-28` sub 4 vô hạn không (nên có thời hạn rồi chuyển `Reconnecting`).
- Muốn thử lại lượt nối lại thì cần chủ repo đồng ý thêm một lượt đăng nhập.

### Đã sửa sau lượt 9 (offline, chưa kiểm lại trên server thật)

- Chủ repo báo: sau khi mất kết nối phải chờ **ít nhất 40 giây** mới đăng nhập lại, sớm hơn thì server khóa acc. Lượt 9 nối lại sau 5 giây nên rất có thể là nguyên nhân server im lặng. Đây là lời của chủ repo, mình chưa tự kiểm bằng server.
- `Backoff` giờ chờ 40 s, 80 s, 160 s, rồi 300 s (tối đa). Nối TCP thất bại cũng dùng chuỗi này.
- Thêm `SessionOptions.LoginTimeout` (30 giây): gửi đăng nhập mà không nhận `-28` sub 4 thì phiên dừng ở `Rejected`, không thử lại, không kẹt nữa.
- Có test cho cả hai; `dotnet test` xanh. Lượt nối lại thật vẫn **chưa kiểm** (cần chủ repo đồng ý một lượt đăng nhập, khoảng 12 phút để chờ server đóng kết nối, hoặc ngắt socket chủ động).

## M1: lượt 10, kiểm nối lại sau khi sửa (2026-10-31, **không dùng được**)

Lệnh: `host 1 --xac-nhan --phut 14 --bat-goi`, bản Release build sạch, tệp bắt gói `%USERPROFILE%\.toolql\captures\20261030-221843`. Vào game bình thường (`InGame` sau 2 giây, map 44, 0 lỗi đọc gói), in trạng thái đều đến giây 100 (01:40).

Sau đó đồng hồ nhảy: dòng log kế tiếp ghi 53:40, và gói cuối trong tệp bắt gói có giờ 14:12 UTC trong khi lúc nối là 05:18 UTC (lệch khoảng 9 giờ). Nhiều khả năng máy ảo bị tạm dừng (suspend/pause) giữa phiên. Hệ quả:
- **Không quan sát được lần mất kết nối nào**, nên việc chờ 40 giây rồi đăng nhập lại **chưa được kiểm**.
- Lượt này **không chứng minh** thêm gì về giữ kết nối lâu; không dùng số 9 giờ làm kết quả.
- Mã thoát 0, nhưng do đồng hồ nhảy qua hạn 14 phút chứ không phải do chạy đủ 14 phút.

## M1: lượt 11, kiểm nối lại sau khi sửa (2026-10-31 07:26, **đạt**)

Lệnh: `host 1 --xac-nhan --phut 15 --bat-goi`, bản Release build sạch, acc thử, "Vũ trụ 1". Tệp bắt gói: `%USERPROFILE%\.toolql\captures\20261031-072601`. Mã thoát 0.

| Mốc | Sự kiện |
|---|---|
| 00:02 | `InGame` (map 44 "Vách núi Kakarot", nhân vật `yskhha`) |
| 10:22 | server đóng kết nối ("Hết dữ liệu khi đọc khung gói"), phiên chuyển `Reconnecting` |
| 11:02 | `Connecting` → `LoggingIn` (đúng 40 giây sau khi mất kết nối) |
| 11:04 | `InGame` lại, map 44 như cũ |
| 15:01 | hết giờ, vẫn `InGame`, `Stopped` do `Stop()` |

- **Nối lại tự động chạy đúng:** chờ 40 giây rồi đăng nhập lại, vào lại game sau 2 giây, không bị từ chối. Giữ thêm khoảng 4 phút sau đó.
- **0 lỗi đọc gói** trong cả phiên, kể cả sau khi nối lại.
- **Lặp lại hiện tượng lượt 9:** server đóng kết nối sau khoảng 10 phút 20 giây (hai lượt, cùng mốc). Có vẻ là giới hạn thời gian/hoạt động phía server, hoặc cần gói giữ kết nối. **Chưa xác định nguyên nhân**; nếu đó là thiếu ping thì cần điều tra thêm trước khi treo dài.
- **Hạn chế:** mới một lần nối lại thành công; chưa kiểm chuỗi 80/160/300 giây ngoài đời thật (chỉ có test với đồng hồ giả); chưa kiểm nối lại nhiều lần liên tiếp.

## C2: chạy lại bộ đọc trên tệp bắt gói (2026-10-31, offline)

Lệnh mới `probe replay <packets.jsonl>` áp mọi gói S→C của tệp vào `GameState`, không nối mạng, không đăng nhập. Chạy trên 3 tệp có gói thật (lượt 9, 10, 11; tổng 12 608 gói S→C; các tệp của lượt 1-8 không còn trong máy này): **0 lỗi đọc gói**.

- Lần chạy đầu phát hiện `-90` có **hai dạng** (12 byte và 5 byte); bảng mã chỉ ghi dạng 12 byte. Bộ đọc đã sửa cho cả hai (test có cả hai dạng). Dạng 5 byte chỉ đếm, không áp vào trạng thái vì chưa rõ nghĩa.
- Các gói trong danh sách C2 của plan (`-9`, `-12`, `-13`, `-11`, `-10`, `68`, `-20`, `-21`, `18`, `-64`) **đã có bộ đọc từ trước** và chạy trên tệp bắt gói không lỗi. Nhưng trong 4 tệp mới nhất mình chưa đếm riêng từng loại, nên chưa khẳng định mỗi loại đều xuất hiện trong dữ liệu thật; loại nào chưa từng xuất hiện thì vẫn chỉ ở mức `từ-mã`.
- Gói chưa có bộ đọc, nhiều nhất (lượt 11): `54` (864, S→C 6 byte: int, ubyte, byte, hợp với bảng mã), `-30` sub 8 (639; có bộ đọc nhưng chưa dùng vì chưa rõ nghĩa), `113` (292, 10 byte), `-95` (176, hai dạng 15 và 5 byte), `24` (111, 8 byte), `93` (68, thông báo UTF), `92` và `44` (chat). Chưa cái nào cần cho trạng thái hoặc vẽ ở v0.1, nên để lại.

## R1: lượt 12, xin ảnh (2026-10-31 08:10, `probe images 1 --xac-nhan`)

Một lượt đăng nhập acc thử (Vũ trụ 1), vào game bình thường, xin 5 gói `-67` (id 0-4), 1 gói `11` (sprite quái loại 94, quái đầu tiên trong map), 1 gói `-32` (ảnh nền id 73, nền đầu tiên của map). Gói thô: `%USERPROFILE%\.toolql\captures\20261031-081057`; ảnh: `%USERPROFILE%\.toolql\images\20261031-081057` (ngoài repo). Ngắt chủ động bằng `Stop()`, không lỗi, 0 lỗi đọc gói.

| Mục | Kết quả |
|---|---|
| Khung độ dài 3 byte của `-67`, `-32`, `11` | **Đúng** (cả 7 gói trả về có `three:true`, các gói sau vẫn khớp khung). Còn lại chưa kiểm: `-66`, `66`. |
| `-67` S→C | **Đúng như mô tả**: int id, int độ dài, PNG; độ dài ghi khớp phần còn lại (5/5). |
| `-32` S→C | **Đúng**: short id, int độ dài, PNG 122×108 (độ dài khớp). Ảnh xem bằng mắt: một mảng vách đá. |
| `11` S→C | **Đúng một phần**: short loại, byte kiểu (0), khối dữ liệu 154 byte, khối PNG 1637 byte (147×25), rồi **1 byte** sau PNG (khớp "byte `bD`" trong tài liệu). Ảnh xem bằng mắt: bảng sprite một con robot xanh có pháo, nhiều khung. Khối dữ liệu 154 byte **chưa đọc** (bố cục có trong `render.md`, chưa chạy bộ đọc). |
| Id `-67` 0-4 | **Là ảnh nhỏ riêng, không phải atlas lớn**: 1×1, 18×12, 14×8, 20×7, 15×8. Nên "id 0-4 = atlas" là sai; atlas của `NR_image` (kích thước tối thiểu ≈ 355×255) **chưa tìm thấy** cách xin. |

**Lỗi của lệnh trong lượt này (đã sửa):** bảng đối chiếu `NR_image` và việc xin ảnh theo bộ phần thân không chạy vì lệnh tra sai tên khóa kho (`data` thay vì `data-0`). Nên mối quan hệ giữa mã sprite `q` của `NR_part`, bảng `NR_image` và ảnh `-67` **vẫn chưa kiểm**.

## R1: lượt 13, xin ảnh theo bảng (2026-10-31 08:17, `probe images 1 --xac-nhan`, đã sửa lỗi tra khóa kho)

Một lượt đăng nhập, vào game, xin 30 gói `-67` (id 0-4 cộng 25 mã sprite của các bộ phần thân 252, 253, 254, vì lúc đó chưa có người chơi nào trong map) và 1 gói `-32` (nền id 84). Nhận **31/31** trả lời, đều là PNG hợp lệ, 0 lỗi đọc gói, không mất kết nối. Gói thô: `%USERPROFILE%\.toolql\captures\20261031-081712`; ảnh: `%USERPROFILE%\.toolql\images\20261031-081712`.

- **`-67` id `i` trả về đúng sprite số `i` của bảng `NR_image`: 30/30 ảnh có kích thước PNG bằng đúng rộng×cao của mục `i`** (kiểm bằng máy trên tệp đã lưu: khớp 30, lệch 0). Nghĩa là ảnh tải về là sprite đã cắt sẵn; `atlas`, `x`, `y` trong bảng là tọa độ trong atlas của client, ToolQL **không cần** atlas để vẽ.
- **`q` của `NR_part` trỏ đúng vào mã sprite**, ít nhất với `q` < 2997: các mã của bộ phần 252/253/254 (từ 16 đến 2621) đều xin được. Chưa kiểm `q` ≥ 2997 (tới 17 797): 25 mã lần này đều nằm trong bảng.
- Bộ phần 252/253/254 hợp lệ là chỉ số vào `NR_part` (nhỏ hơn 2094). Ảnh xem bằng mắt: mảnh nhỏ của nhân vật (ví dụ id 2601 là nắm tay và tay áo nâu). Việc 252/253/254 là đầu/thân/chân của một người chơi cụ thể vẫn chưa kiểm (lượt này không có người trong map).
- Dòng "KHÔNG vừa trong ảnh" trong log lượt này **do phép kiểm của lệnh sai** (so vùng `x,y` với kích thước ảnh đã cắt). Đã sửa lệnh; kết luận ở trên dùng phép kiểm đúng.
- Ảnh nền `-32` id 84: PNG 204×106, độ dài khớp.

Chưa kiểm: `-67` với id ≥ 2997 (mã `q` lớn); `-66` (hiệu ứng) và `66`; khối dữ liệu 154 byte trong gói `11`; ghép các phần thành nhân vật (cần bảng "trạng thái nhân vật → khung").

## R1: bộ đọc gói sprite quái `11` trên dữ liệu thật (2026-10-31, offline, `probe mob mob-94.bin`)

Bộ đọc `MobSpritePack` + `SpritePack` (`src/ToolQL.Protocol/Resources/SpritePack.cs`) chạy trên gói `11` loại 94 đã lưu ở lượt 12 (1803 byte). Chỉ **một mẫu**, kiểu 0.

| Mục | Kết quả |
|---|---|
| Tách gói: short loại, byte kiểu, khối dữ liệu 154 byte, khối PNG 1637 byte, byte cuối | **Đúng, tiêu thụ đúng cả gói** (0 byte thừa). Byte cuối = 0, nên bảng phụ (chỉ đọc khi byte cuối là 1 hoặc 2) **chưa từng gặp**. |
| Hình con | 3 hình con 49×25 tại x = 0, 49, 98: **nằm gọn trong PNG 147×25** (đúng bằng 3 × 49). |
| Khung | 12 khung, mỗi khung 2 mảnh; **mọi mảnh đều trỏ tới hình con có thật** (0 lỗi). Chỉ có 2 nội dung khung khác nhau trong 12 khung. |
| Danh sách hoạt ảnh | Đọc ra **0 số**. |
| Khối dữ liệu | **Còn 2 byte thừa** (đều `00`) sau danh sách hoạt ảnh. Tài liệu `render.md` không có trường nào cho 2 byte này. **Chưa rõ**: có thể có thêm một trường phụ thuộc `bF`, hoặc danh sách hoạt ảnh đọc khác (`bF < 201` đọc thẳng `g[]`). Một mẫu toàn số 0 nên **không đủ để kết luận**. |

Kết luận: cấu trúc hình con và khung khớp dữ liệu thật; đủ để cắt hình con từ PNG và đặt mảnh theo khung. **Chưa biết** khung nào ứng với hoạt ảnh nào (đứng, đi, bị đánh, chết), vì danh sách hoạt ảnh rỗng và 2 byte cuối chưa giải. Cần thêm mẫu quái khác loại (xin thêm gói `11`, cần một lượt đăng nhập) để biết.

## Windows Defender báo nhầm `ToolQL.Render.dll` (2026-10-31)

- Lần build đầu của `ToolQL.Render` (có bộ giải XOR ảnh của jar) bị Defender xử lý: `Trojan:MSIL/AsyncRat.ASY!MTB` (mức Severe, heuristic `!MTB`), file `bin` và `obj` bị cách ly, build lỗi `MSB3248`. Mã hoàn toàn do ToolQL viết; **gần như chắc chắn báo nhầm nhưng chưa xác nhận bằng nguồn ngoài**.
- Dò bằng cách thêm từng phần: ô lát + vẽ map **không** bị bắt; thêm bộ giải XOR dùng `static readonly byte[]` làm khóa + vòng lặp `result[i] = data[i] ^ key[i % n]` **bị bắt**; viết lại cùng phép đó bằng khóa/chữ ký dạng `ReadOnlySpan<byte>` và `^=` tại chỗ trên bản sao thì **không bị bắt**, kể cả khi ráp đủ cả phần đọc zip. Không thêm ngoại lệ nào cho Defender, không tắt bảo vệ.
- Hạn chế: kiểm một máy, một bản định nghĩa virus. Bản định nghĩa mới có thể báo lại. Trước khi phát hành nên gửi bản build cho Microsoft báo nhầm (gửi tệp ra ngoài: hỏi chủ repo trước).

## R2: bản vẽ map đầu tiên (2026-10-31, offline, `probe render`)

Lệnh: `probe render <packets.jsonl> <client.jar> <ra.png>` dựng `GameState` từ tệp bắt gói lượt 11 (map 44 "Vách núi Kakarot", lưới 60×24, bộ ô lát 9) và vẽ cả map 1440×576 bằng `MapRenderer` với ô lát đọc từ jar của client (giải XOR). Xem bằng mắt (chưa so với ảnh chụp game thật): ô lát ghép liền mạch ra vách đá cát có tinh thể vàng, đúng kiểu ảnh nền `-32` lấy về trước đó.

- **Đúng:** quy tắc dải ô lát dọc, mã − 1, thứ tự hàng/cột `lưới[hàng * rộng + cột]` (hình ghép liền, không lệch ô). Nhân vật mình (vàng) và người chơi/NPC/quái đặt tại `(x, y)` **đứng trên mặt đất** nên giả định "(x, y) là điểm chân" nhìn hợp lý.
- **Chưa làm:** sprite nhân vật/quái/NPC (hiện là khung màu + tên), nền trời và vật trang trí, nước, cờ ô lát ("không vẽ", dịch ngang...), cổng, vật rơi, thanh máu. Hai cột ngoài cùng (0 và rộng−1) trống trong ảnh này; `render.md` nói client vẽ riêng ở mép nên có thể còn thiếu, chưa kiểm.
- **Chưa kiểm:** bản vẽ so với ảnh chụp game thật (chưa có ảnh); bộ ô lát khác 9 (jar chỉ có 1-15, 22, 23, và `t/1.png` cao 937 không chia hết cho 24).
