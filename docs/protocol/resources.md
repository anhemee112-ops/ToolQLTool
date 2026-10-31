# Tài nguyên và bảng dữ liệu (map, skill, item, ảnh, hiệu ứng)

Nguồn: `Dragonboy250 v3.5.jar`; hàm đọc nằm ở `cj_0` (bảng map/skill/item/dữ liệu), `cu_0` (phần, skill, mũi tên, phi tiêu, hiệu ứng), `at_0` (ảnh nhỏ), `cV.a` (đọc khối byte). Tin cậy: **từ-mã**, chưa bắt gói. Tên trường là cách gọi của tôi, tên trong jar ở ngoặc.

Mức hoàn thành: **cấu trúc byte** của các bảng đã ghi đủ để viết bộ đọc. **Ý nghĩa** từng trường (và cách vẽ từ chúng) chưa khảo sát, để dành cho `render.md`.

## Tài nguyên trong jar và tài nguyên do server gửi

Client chia tài nguyên làm hai nguồn:

- **Trong jar** (một phần nhỏ, cho một mức phóng `x1`): thư mục `x1/mainImage` (144 file), `x1/bg` (93), `x1/mapBackGround` (64), `x1/e` (45), `x1/effectdata/<N>` (38 + biến thể `violet`, `blue`), `x1/myfont`, `x1/radar`, `x1/t` (17 bộ ô lát), `x1/button`, `x1/img`, `x1/SmallImage` (chỉ 2 file `Small1085`, `Small1086`), và `mymap/39..41` (+ `mapTable39..41`, chưa đọc).
- **Server gửi**, client lưu vào kho cục bộ (RMS của J2ME) theo khóa chuỗi và chỉ tải lại khi phiên bản lệch: các bảng `NRdata`, `NRmap`, `NRskill`, `NRitem*`, ảnh nhỏ, nền, hiệu ứng, lưới ô lát (xem dưới).

Hệ quả cho ToolQL: **gần như toàn bộ dữ liệu phải lấy từ server khi chạy**, không thể chỉ dựa vào jar. Lõi cần một kho cục bộ (tệp) theo khóa + phiên bản.

## Phiên bản và cách tải bảng

Gói `-28` sub 4 báo 4 phiên bản (data, map, skill, item). Với từng loại, nếu khác bản đã lưu client xin tải lại, nếu giống thì đọc từ kho. Kho dùng các khóa:

| Khóa | Nội dung | Gói tải (S→C) | Gói xin (C→S) |
|---|---|---|---|
| `NRdata` (+ `NR_dart`, `NR_arrow`, `NR_effect`, `NR_image`, `NR_part`, `NR_skill`) | dữ liệu đồ họa chung | `-87` | `-87` |
| `NRmap`, `NRmapVersion` | bảng map | `-28` sub 6 | `-28` sub 6 |
| `NRskill`, `NRskillVersion` | bảng skill | `-28` sub 7 | `-28` sub 7 |
| `NRitem0`, `NRitem1`, `NRitem100`, `NRitem101`, `NRitemVersion` | bảng item (4 phần) | `-28` sub 8 | `-28` sub 8 |

Mỗi khối được lưu **nguyên văn byte** (client đọc để dựng bảng rồi ghi lại đúng khối nhận được).

## `NRdata` (gói `-87`, hàm `cj_0.a(dis, bool)`)

| Trường | Kiểu |
|---|---|
| phiên bản dữ liệu | byte |
| khối `NR_dart` | int `độDài`, rồi `độDài` byte |
| khối `NR_arrow` | như trên |
| khối `NR_effect` | như trên |
| khối `NR_image` | như trên |
| khối `NR_part` | như trên |
| khối `NR_skill` | như trên |

Mỗi khối là một luồng riêng, định dạng như dưới.

### `NR_image` (bảng ảnh nhỏ — `at_0.cf`)

`short n`, rồi `n` mục: `ubyte atlas`, `short x`, `short y`, `short rộng`, `short cao` (lưu vào mảng 5 phần tử theo thứ tự `[0]=atlas, [1]=x, [2]=y, [3]=rộng, [4]=cao` theo cách client đặt chỉ số: `bn[1]=0, bn[2]=1, bn[3]=2, bn[4]=3, bn[0]=4`). Mục `i` bị bỏ (đặt null) trừ khi `i` thuộc các đoạn 84-90, 371-384 hoặc cả bốn số `x, y, rộng, cao` đều < 256. Ảnh `atlas` là PNG: `/SmallImage/Small<atlas>.png` trong jar (chỉ có 2 file), hoặc do server gửi qua `-67`.

### `NR_part` (bảng phần thân — `cu_0`)

`short n`, rồi `n` mục: `byte loại`, rồi một **số phần tử cố định theo loại** (loại 0 → 3 phần, loại 1 → 17, loại 2 → 14, loại 3 → 2; theo `ac.<init>`), mỗi phần tử: `short q`, `byte M`, `byte ag`. Mỗi mục là một "bộ phần" ghép nhân vật; `q` là mã sprite (trỏ vào `NR_image`), `M` và `ag` là độ lệch x, y của phần đó (xem `render.md`).

### `NR_skill` (hoạt ảnh skill — `cu_0`)

`short n`, rồi `n` mục: `short id` (nếu bằng `1111` (`ez[16]`) thì client đổi thành `n - 1`, tức mục cuối), `short x` (nếu ≤ 0 thì đặt mặc định `80`, `ez[17]`), `byte` bỏ, `byte m`, rồi `m` khung: `byte`, rồi **11 short** (`bH, bB, bF, bD, cu, di, cJ, bu, dm, dn, bV, do`, tên thật chưa gán; danh sách đọc dừng ở đây trong đoạn đã xem, còn thêm trường ở phần sau chưa đọc).

### `NR_arrow` (mũi tên — `cu_0`)

`short n`, rồi `n` mục: `short` (bỏ), `short`, `short`, `short`.

### `NR_dart` (phi tiêu — `cu_0`)

`short n`, rồi `n` mục: `short` (bỏ), `short K`, `short bu` (client dịch trái 8 bit, `ez[15]`, tức nhân 256), `short e`; rồi 4 mảng `short` có độ dài đứng trước (`short len`, `len` short) cho `e[]`, `b[]`, `c[]`, `m[]`; rồi 2 mảng hai chiều: `short len`, mỗi phần tử `short len2` + `len2` short (cho `b[][]`, `c[][]`).

### `NR_effect` (hiệu ứng — `cu_0`)

`short n`, rồi `n` mục: `short` (bỏ), `byte m`, rồi `m` phần tử: `short bB`, `byte bC`, `byte bH`.

## `NRmap` (gói `-28` sub 6, hàm `cj_0.a(dis)`)

| # | Trường | Kiểu |
|---|---|---|
| 1 | phiên bản map | byte |
| 2 | số map `nTênMap` | short |
| 3 | `nTênMap` × tên map (**theo id map: chỉ số = id**) | UTF |
| 4 | số mẫu NPC `nNPC` | byte |
| 5 | mỗi mẫu NPC: tên | UTF |
| | `bB`, `bF`, `bD` (có vẻ id hình đầu/thân/chân) | short ×3 |
| | số dòng `r` | byte |
| | mỗi dòng: số cột `c` (byte), rồi `c` UTF (có vẻ là lời thoại/menu) | |
| 6 | số mẫu quái `nQuái` | short |
| 7 | mỗi mẫu quái: `ag` byte, tên UTF, `Z` long (có vẻ HP), `L`, `M`, `ae` byte | |

Hệ quả cho Xmap: **tên map theo id** nằm ở mục 3. Cổng trong `-24` chỉ có `tên đích` nên có thể ánh xạ tên → id. **Chưa kiểm chứng** việc tên là duy nhất (nhiều map có thể trùng tên).

Bảng `loạiQuái` (trường trong `-24`) là chỉ số vào mục 7; bảng `mẫuNPC` vào mục 5.

## `NRskill` (gói `-28` sub 7, hàm `cj_0.b(dis)`)

| # | Trường | Kiểu |
|---|---|---|
| 1 | phiên bản skill | byte |
| 2 | số nhãn `nNhãn` | byte |
| 3 | `nNhãn` × (một UTF, giá trị bỏ) | UTF |
| 4 | số lớp `nLớp` | byte |
| 5 | mỗi lớp: tên UTF (đọc rồi bỏ), `nSkill` byte | |
| 6 | mỗi skill: `S` byte, tên UTF, `bB` byte, `ek` byte, `em` byte, `cu` short, mô tả UTF, chuỗi biểu tượng UTF (client đổi thành ảnh), `nCấp` byte | |
| 7 | mỗi cấp: `h` short, `eg` byte, `Z` long, `ee` short, `eh` int, `di` short, `cJ` short, byte bỏ, `g` short, short bỏ, mô tả UTF `ac` | |

`lớp` ở đây tương ứng chỉ số `cu_0.a[...]` mà gói nhân vật (`-30` sub 0, `-5`) dùng (ví dụ byte "lớp" trong thông tin người chơi). Ý nghĩa các trường skill (hồi chiêu, mana tiêu hao, tầm đánh...) chưa gán: cần khi làm Train.

## `NRitem*` (gói `-28` sub 8, hàm `cj_0.b(dis, bool)`)

Mỗi khối: `byte phiênBảnItem`, `byte loại`, rồi thân theo `loại`. Client lưu khối vào khóa tương ứng.

| Loại | Khóa | Thân |
|---|---|---|
| 0 | `NRitem0` | `short n`; `n` × (UTF tên, byte bỏ) = mẫu **tùy chọn**/thuộc tính (`cH`); rồi `short m`; `m` × (short chỉ số, ubyte `bB`) gán thêm `bB` cho mẫu `chỉ số` |
| 1 | `NRitem1` | `short n`; `n` mục: byte `aJ` (loại vật phẩm), byte `ag`, UTF tên, UTF mô tả, byte (bỏ), int `bu`, short `N` (có vẻ id biểu tượng), short `e` (id phần thân dùng khi mặc), bool `bh`. Id vật phẩm = chỉ số mục (0..n-1) |
| 24 | `NRitem100` | `short n`; `n` × (`byte m`, `m` short) |
| 93 | `NRitem101` | `short n`; `n` × short |
| 2 | (không lưu) | rỗng, dừng |

Với `NRitem1`: loại `aJ` = 0 là áo (gán thân), 1 là quần (gán chân) trong cách client dựng nhân vật (xem gói `-30` sub 0). Danh sách loại đầy đủ chưa lập; bảng id vật phẩm có sẵn ở `docs/data/`.

## Ảnh nhỏ, nền, hiệu ứng (gói theo yêu cầu)

| Mã | Hướng | Nội dung | Trường |
|---|---|---|---|
| -67 | C→S | xin ảnh nhỏ (atlas) | int `id` |
| -67 | S→C | trả ảnh nhỏ | int `id`, int `độDài`, `độDài` byte PNG |
| -32 | C→S | xin ảnh nền | short `id` |
| -32 | S→C | trả ảnh nền | short `id`, int `độDài`, `độDài` byte PNG |
| -66 | C→S | xin dữ liệu hiệu ứng | short `id` |
| -66 | S→C | trả hiệu ứng | short `id`, khối (int len + bytes), byte `loại`, khối PNG (int len + bytes) |

Ảnh nhận về được lưu vào kho theo khóa `<mứcPhóng>Small<id>`, `<mứcPhóng>bgItem<id>`. Client kiểm tra bản lưu bằng cách lấy `độDài % hằng` so với bảng phiên bản ảnh (`cd_0.l[...]`, `at_0.d[...]`); cơ chế này suy từ mã, chưa rõ nguồn của bảng phiên bản. Khối đầu của `-66` (dữ liệu khung) được hai hàm `co_0.c`/`co_0.a` nạp, **chưa đọc**.

## Việc còn lại

- Đọc phần còn lại của `NR_skill` (sau 11 short) và ý nghĩa các trường: cần cho Train.
- `mymap/mapTable*` trong jar (chưa biết dùng làm gì).
- Định dạng khối khung của `-66` và cách ghép `NR_part` + `NR_image` để vẽ nhân vật: để `render.md`.
- Nguồn bảng phiên bản ảnh (`cd_0.l`, `at_0.d`) và quy tắc xóa/ghi kho.
- Xác nhận mọi thứ bằng gói thật.

## Kiểm với dữ liệu thật (2026-10-31, offline, `probe data data-0.v30.bin`)

Gói `-87` phiên bản 30 (202 598 byte) lưu trong kho cục bộ từ lượt đăng nhập thật. Bộ đọc: `NrData`, `ImageTable`, `PartTable` (`src/ToolQL.Protocol/Resources/NrData.cs`).

- **`NR_image`: bắt-gói, đúng.** 2997 sprite, tiêu thụ đúng 26 975 byte (= 2 + 2997 × 9), không byte thừa, không sprite rộng/cao ≤ 0. Sprite theo atlas 0-4: 1052, 392, 532, 518, 503. Kích thước tối thiểu suy từ bảng: atlas 0 ≈ 395×376, 1 ≈ 355×255, 2 ≈ 357×255, 3 ≈ 355×255, 4 ≈ 354×256.
- **`NR_part`: bắt-gói, đúng.** 2094 bộ (loại 0: 837, loại 1: 632, loại 2: 625; **không có loại 3**), tiêu thụ đúng 90 116 byte. Số phần tử theo loại (3, 17, 14) được xác nhận vì cộng dồn khớp từng byte.
- **Sau 6 khối còn 180 byte** mà tài liệu cũ không nhắc: byte `0x15` (21), rồi các số long tăng dần (1000, 3000, 15000, 40000, ...) và 11 byte còn lại. Trông như bảng mốc 21 phần tử giống caption ở `-28` sub 4. **Chưa xác nhận, chưa đọc**, không liên quan tới vẽ.
- **Mã sprite `q` trong `NR_part` KHÔNG phải chỉ số vào `NR_image`.** `q` từ 0 đến 17 797 (14 182 mã khác nhau, không âm) trong khi bảng chỉ có 2997 mục: 13 106 trong 22 005 phần có `q` ≥ 2997. Phù hợp với mô tả "sprite ngoài bảng là ảnh riêng `Small<q>.png` do server gửi qua `-67`", nhưng **chưa kiểm** vì chưa xin ảnh nào từ server.
- **Jar không chứa atlas.** `x1/mainImage` (144 tệp) toàn ảnh giao diện (nút, thanh máu, nền menu...). Không có tệp nào là sprite nhân vật/quái; `x1/SmallImage` chỉ có `Small1085`, `Small1086`. Vậy ảnh nhân vật, quái, vật phẩm phải lấy từ server khi chạy.
- **Chưa biết:** ảnh atlas 0-3 xin bằng gói nào, `-67` với id gì; khung gói 3 byte của `-67`/`-66`/`-32`; cách client chọn "atlas 4" (có vẻ là "dùng ảnh riêng").

## Kiểm với server thật: ảnh (2026-10-31, lượt 12)

Chi tiết ở `verification.md`. Tóm tắt: `-67`, `-32`, `11` đúng khung 3 byte và đúng bố cục mô tả; `-67` id 0-4 trả về ảnh nhỏ riêng (1×1 đến 20×8), **không** phải atlas. Chưa biết atlas của `NR_image` lấy từ đâu; chưa kiểm `q` của `NR_part` có xin được bằng `-67` hay không.

**Cập nhật lượt 13 (2026-10-31):** `-67` với id `i` trả đúng sprite `i` của `NR_image`, kích thước PNG khớp rộng×cao trong bảng ở 30/30 ảnh. Atlas không cần để vẽ. Chưa kiểm id ≥ 2997.

**Cập nhật R2 (2026-10-31):** có thể vẽ map thật từ ô lát trong jar (không cần xin server) cho các bộ ô lát có trong jar; xem `verification.md` mục R2.
