# Map và người chơi trong map (gói `-24`, `-5`, `-28` sub 10)

Nguồn: `Dragonboy250 v3.5.jar`, `cj_0.i` (thân `-24`), `cj_0.b` nhánh `-5`, `cj_0.a(ce, cN)` (thông tin người chơi), `aw.m`/`aw.o` (nạp ô lát). Tin cậy: **từ-mã**; `-24`, `-28` sub 10 và `-5` đã khớp bắt gói (xem `verification.md`). Server chỉ gửi `-5` sau khi client gửi `-39`. Mã dịch ngược của CFR có `goto` ở nhánh `-5`, nên thứ tự đọc ở đó cần xác nhận bằng gói thật.

Tên trường là cách gọi của tôi; tên trong jar để trong ngoặc.

## Hai nguồn dữ liệu của một map

1. **Gói `-24`** (server → client khi vào/đổi map): vị trí của mình, cổng, quái, NPC, vật rơi, hình nền, hiệu ứng. **Không chứa ô lát.**
2. **Lưới ô lát** (rộng × cao + loại từng ô): client tìm trước trong tài nguyên cục bộ `/mymap/<mapId>`. Jar này chỉ có `mymap/39`, `40`, `41`. Không có thì client gửi **`-28` sub 10 + byte mapId** (`an.s`) và server trả `-28` sub 10 bên dưới. Gói `-24` được **giữ lại** (`this.a = var1_1`) rồi đọc tiếp khi lưới về.

Hệ quả cho ToolQL: lõi phải tự xin và lưu lưới ô lát; không dựng được map chỉ từ `-24`.

## Lưới ô lát

Định dạng file `/mymap/<id>`: byte `rộng` (không dấu), byte `cao`, rồi `rộng × cao` byte loại ô lát (không dấu), từng hàng một (chỉ số = `y * rộng + x`).

Gói `-28` sub 10 (S→C): byte sub = 10, byte `rộng`, byte `cao`, rồi `rộng × cao` byte (client cộng 256 nếu âm, tức đọc không dấu), và **1 byte cuối** (`bắt-gói`: giá trị `00`, ý nghĩa chưa rõ, có thể là `bf`; bộ đọc `MapGrid.Trailer`). Thân giống `-24` không nằm trong gói này. *(Lịch sử: bản đầu ghi gói "hết" sau lưới; bắt gói ngày 2026-10-28 cho thấy còn 1 byte nên đã sửa. Bắt gói map 23: 32x20, 640 ô.)*

- Rộng/cao được đọc bằng `readByte` có dấu rồi dùng trực tiếp: map rộng hơn 127 ô sẽ ra số âm. **Chưa rõ** server có gửi map như vậy không; ToolQL nên đọc không dấu như file cục bộ.

## Ô lát → cờ va chạm (đã trích)

Hai bảng này **không nằm trong jar**: server gửi trong gói **`-82`** (S→C, `cj_0.b`). Chưa thấy client gửi yêu cầu riêng; có vẻ server đẩy khi vào game (chưa rõ lúc nào, cần bắt gói).

Gói `-82`:

| Trường | Kiểu |
|---|---|
| `nBộ` | byte |
| mỗi bộ: `nCờ` | byte |
| mỗi cờ: `giáTrịCờ` | int |
| | `nMã` byte |
| | `nMã` × `mãÔLát` byte (**có dấu**) |

Cách dùng (`aw.o(id)`, với `n = id - 1`, `id` = trường `bộÔLát` của `-24`): với mỗi ô của map, với mỗi cờ `j` trong bộ `n`, nếu mã ô lát của ô nằm trong danh sách `mãÔLát` của cờ `j` thì `cờÔ |= giáTrịCờ`. Nhãn `block3` đặt trên vòng lặp theo cờ, nên khi khớp thì chỉ thôi duyệt danh sách mã của cờ đó và xét **cờ kế tiếp**: một ô có thể nhận **nhiều cờ** (OR lại).

Lưu ý quan trọng:

- Giá trị ban đầu của `cờÔ` là 0 (mảng `aw.bs` mới tạo cùng kích thước lưới).
- Mã trong bảng là byte **có dấu** còn mã ô lát của map là **không dấu** (0-255). Mã ≥ 128 trong bảng thành số âm nên **không bao giờ khớp** ô lát có mã ≥ 128. ToolQL phải giữ đúng quirk này nếu muốn khớp va chạm với client.
- Cờ có thể bị sửa lúc chạy: `aw.b` OR thêm `512`, `aw.c` AND với `~512`.

Kích thước ô `U` = **24** (`aw.bt[33]`, đã tính bằng mô phỏng bytecode, không còn là "có vẻ"). Ô chứa điểm `(x, y)` là `cờÔ[(y/24) * rộng + x/24]`.

### Ý nghĩa các bit cờ

Giá trị lấy từ hằng số trong `ce`/`cO` bằng `scripts/survey/IntArray.pl` (mô phỏng bytecode, chỉ số mảng `dG` của lớp `ce`, `fp` của `cO`, `bt` của `aw`). Hai cách tính khác nhau cho `dG[82]`, `dG[147]`, `dG[148]` khớp với biểu thức CFR tính tay.

| Bit | Giá trị | Dùng ở đâu | Nghĩa (tin cậy) |
|---|---|---|---|
| 1 | 2 | `(cờ & 2) == 2` tại chân; quái cũng dùng | ô **đứng được** (sàn) — từ-mã |
| 2 | 4 | kiểm tra tại `(x + nửaRộng, y - 1)` khi quay phải | **tường bên phải**, chặn đi sang phải — từ-mã |
| 3 | 8 | kiểm tra tại `(x - nửaRộng - 1, y - 1)` khi quay trái | **tường bên trái**, chặn đi sang trái — từ-mã |
| 6 | 64 | tại `(x, y - 1)`: tốc độ ngang giảm một nửa (`dA >> 1`) | **nước**, làm chậm (cờ 64 cũng được dùng để vẽ mặt nước, xem `render.md`) — từ-mã |
| 9 | 512 | `aw.b` đặt, `aw.c` xóa; `ce` đặt khi chân ở ô có cờ 1024 | cờ động do client đặt — đoán |
| 10 | 1024 | tại `(x, y + 1)`: nếu có thì đặt 512 | ô có hiệu ứng khi đạp — đoán |
| 13 | 8192 | tại `(x, y - chiềuCao + 24)`; nếu có và không đứng trên sàn thì chuyển sang trạng thái rơi | **chặn phía đầu/trần** — đoán |

`nửaRộng` là trường `fl` của nhân vật (chưa tính giá trị). Chiều cao nhân vật là `eL`. Ý nghĩa bit 1 (giá trị 1) và các bit khác chưa thấy dùng.

Hướng nhân vật: `bU = 1` là quay phải, `bU = -1` là quay trái (mã `-24` đặt `bU = 1` khi x gần mép trái, `-1` khi gần mép phải).

## Thân gói `-24`

### Phần đầu (chỉ có trong `-24`)

| Trường | Kiểu | Ghi chú |
|---|---|---|
| `mapId` (`aw.h`) | ubyte | id map |
| `aw.N` | byte | chưa rõ (có thể hành tinh/loại map) |
| `bộÔLát` (`aw.bD`) | byte | **id bộ ô lát**: ảnh `/t/<id>.png`, và `aw.o(id)` dùng `id - 1` để chọn bảng cờ va chạm |
| `aw.dO` | byte | chưa rõ |
| bỏ | byte | đọc rồi bỏ |
| `tênMap` (`aw.al`) | UTF | |
| `aw.f` | byte | chưa rõ |

Sau đó client nạp lưới (cục bộ hoặc xin `-28` sub 10) rồi đọc thân dưới đây.

### Thân (`cj_0.i`)

| # | Trường | Kiểu |
|---|---|---|
| 1 | x của mình | short |
| 2 | y của mình | short |
| 3 | số cổng `nCổng` | byte |
| 4 | mỗi cổng (`ay`): x1, y1, x2, y2, cờ `bA`, cờ `bz`, tên đích | short, short, short, short, bool, bool, UTF |
| 5 | số quái `nQuái` | byte |
| 6 | mỗi quái: 5 bool, `loạiQuái`, `dD`, `hp`, byte không dùng, `hpMax`, x, y, `jL`, `aK`, rồi bool `bB` | bool×5, short, byte, long, byte, long, short, short, byte, byte, bool |
| 7 | một byte | byte (đọc rồi bỏ) |
| 8 | số NPC `nNPC` | byte |
| 9 | mỗi NPC: `chỉSố`, x, y, `mẫuNPC`, `biểuTượng` | byte, short, short, byte, short |
| 10 | số vật rơi `nVật` | byte |
| 11 | mỗi vật rơi: short, short, short, short, int, và short **chỉ khi** int bằng một giá trị đặc biệt (`dN[14]` = **-2**, tính bằng `IntArray.pl` trên `cj.gY`) | |
| 12 | `nHìnhNền` | short |
| 13 | mỗi hình nền: `id`, x, y | short, short, short |
| 14 | `nKhóa` | short |
| 15 | mỗi khóa: `khóa`, `giáTrị` | UTF, UTF |
| 16 | `aw.dP` | byte |
| 17 | một byte đưa vào `cj_0.a(byte)` | byte |

Ghi chú:

- **Cổng (4):** hình chữ nhật (x1, y1)-(x2, y2) trên map hiện tại. Bước vào hình đó rồi gửi `-23` để đổi map; id map đích **không có trong gói**, chỉ có `tên đích`. Muốn biết cổng nối tới map nào (Xmap) thì phải dựa vào tên, hoặc dữ liệu map đã tải (`NRmap`). Ý nghĩa cờ `bA`, `bz` chưa rõ (cả hai false = cổng thường).
- **Quái (6):** `chỉSố` là thứ tự đọc (0, 1, 2...), và là mã quái dùng trong các gói `-9`, `-12`, `54`... Trong năm bool đầu, thứ 2, 4, 5 được lưu (`cT`, `bD`, `cN`); thứ 1 và 3 không thấy dùng; ý nghĩa chưa rõ. `loạiQuái` làm chỉ số bảng mẫu quái `cO.a[...]`. `dD` có vẻ là cấp (gói hồi sinh `-13` cũng gửi lại trường này). Bool cuối `bB` có vẻ là "quái đặc biệt" (tạo lớp `bg` riêng). Vài `loạiQuái` (35, 70, 71) tạo lớp con riêng (`bc`, `aK`, `bp`).
- **NPC (9):** `mẫuNPC` là chỉ số bảng NPC; vài mẫu bị bỏ qua tùy cấp nhân vật (mẫu 16 luôn bị bỏ; 17, 22, 41 bị bỏ khi cấp thấp).
- **Khóa/giá trị (15):** ví dụ khóa `"eff"` (giá trị dạng `a.b.c.d[.e.f[.g[.h.i]]]`, số cách nhau bằng dấu chấm: hiệu ứng nền) và `"beff"` (số nguyên). Chỉ liên quan vẽ.
- Mục (12)-(15) có hai biến thể tùy điều kiện (map đặc biệt/chế độ nào đó): cả hai **đọc cùng số byte**, biến thể kia chỉ bỏ dữ liệu. Dùng cấu trúc ở bảng.

## Người chơi khác xuất hiện (`-5`)

Thứ tự đọc (S→C):

| # | Trường | Kiểu |
|---|---|---|
| 1 | `id` người chơi (`hf`) | int |
| 2 | `loại` (`cC`); 12 tạo lớp con (`aJ`), có vẻ là đệ tử/pet; 14 đặt cờ riêng | int |
| 3 | **thông tin người chơi** (`cj_0.a`), bảng dưới | |
| 4 | hiệu ứng xuất hiện: 0 = vào ngay, khác 0 = có hiệu ứng (1 thì dùng `ht`) | byte |
| 5 | `ar` | byte |
| 6 | cờ trạng thái | short (-1 = không; 55-57, 58-60, 61, 62 và các giá trị ≥ một ngưỡng đặt cờ khác nhau, chưa rõ nghĩa) |
| 7 | `at` | byte |
| 8 | `bm` | byte (==1 là true) |
| 9 | `y`, `u`, `w` | short, byte, short (bọc `try`, có thể thiếu) |

Thông tin người chơi (`cj_0.a`):

| Trường | Kiểu | Ghi chú |
|---|---|---|
| `dt` | byte | chưa rõ |
| `cF` | bool | chưa rõ |
| loại PK (`al`) | byte | log `ADD TYPE PK` |
| lớp (`a`) | byte | chỉ số `cu_0.a[...]`, bảng chung với nhân vật của mình |
| chủng tộc (`ht`) | byte | log ghi `chung toc` |
| đầu (`gD`) | short | id hình đầu |
| tên (`gu`) | UTF | |
| hp (`aN`) | long | |
| hpMax (`ar`) | long | |
| thân (`cg`) | short | id hình thân |
| chân (`cv`) | short | id hình chân |
| balo/cánh (`bY`) | short | |
| bỏ | byte | |
| x, y | short, short | |
| `dz`, `dx` | short, short | chưa rõ |
| `n` | byte | số mục; mỗi mục: byte, int, int, short (`bG`; có vẻ là hiệu ứng/trạng thái đang có) |

Nếu `hp = 0` client đặt trạng thái chết (`hw = 81`).

Khi người chơi khác vào map, gói `-5` đi trước; sau đó vị trí cập nhật qua `-7` (một người) hoặc `18` (nhiều người). Rời map: `-6`.

## Việc còn lại cho phần map

- Xác nhận bằng gói thật: lúc server gửi `-82`, ý nghĩa bit 64, 512, 1024, 8192, và các trường `chưa rõ` ở trên.
- Tìm id map đích của từng cổng (có thể nằm trong bảng `NRmap`, định dạng chưa khảo sát).
- Xác nhận các trường `chưa rõ` bằng gói thật.
