# Vẽ màn hình (render)

Nguồn: `Dragonboy250 v3.5.jar` (J2ME/MIDlet). Lớp chính: `bs` (vòng lặp), `cs` (canvas), `cu` (cảnh game: vẽ và nạp bảng), `aw` (map/ô lát), `at` (ảnh nhỏ), `ce` (nhân vật), `cO` (quái), `cp` (gói sprite dữ liệu + ảnh), `aA` (lớp vẽ bọc `Graphics`). Tin cậy: **từ-mã**, chưa chạy client, chưa so với ảnh thật.

Mức hoàn thành: đã nắm **mô hình vẽ** (tọa độ, thứ tự, ô lát, sprite, nhân vật, gói sprite). **Chưa đủ để vẽ đúng từng điểm ảnh**: thiếu bảng "trạng thái nhân vật → số khung", phần hiệu ứng (chiêu, đòn đánh), chữ/giao diện, và chưa đối chiếu với ảnh chụp thật.

## Vòng lặp và thời gian

- Một luồng chạy `bs.run`: mỗi vòng gọi `dj()` (cập nhật logic) rồi `repaint()` + `serviceRepaints()` (vẽ), rồi ngủ cho đủ `E.N` mili giây tính từ đầu vòng; nếu trễ thì ngủ 2 ms.
- `E.N` mặc định **40 ms** (25 khung/giây). Có chỗ khác ghi đè (lệnh tốc độ game `tdX` trong ReadMe của bản mod).
- Mọi hoạt ảnh phụ thuộc **bộ đếm khung** `cs.ih`, không phải đồng hồ (ví dụ nước đổi hình mỗi `ih % 8 >> 1`). Hệ quả: ToolQL vẽ ở tốc độ khác phải tự tính khung từ thời gian (`khung = thờiGian / 40 ms`).
- Vẽ và cập nhật chạy cùng luồng, một lần mỗi khung. Có chỗ **hàm vẽ ghi trạng thái** (ví dụ `aw.Q` đặt `ds` và gọi `cb.d(...)` tạo hiệu ứng nước), nên không thể xem hàm vẽ là chỉ-đọc. Về tổng thể logic nằm ở `dj()` và các `Z()`/`update` của từng lớp, tách được khỏi vẽ, nhưng cần chọn lọc.

## Tọa độ, camera, kích thước

- Tọa độ thế giới tính bằng điểm ảnh, gốc ở góc trên trái map, `y` hướng xuống. Chân nhân vật nằm ở `(gV, gq)`.
- Ô lát cạnh **24** điểm ảnh (`aw.U`).
- Camera: góc trên trái là `(cu.iR, cu.iW)`; cảnh game vẽ sau `aA.g(-iR, -iW)` (tịnh tiến). Có rung màn hình bằng bảng `eI`, `eW` (`iK` đếm khung còn rung).
- Kích thước màn hình là kích thước canvas (`cs.cl` × `cs.dj`), **không cố định** (J2ME đưa vào). Hệ số phóng `aA.bH` (nguyên): tọa độ chạm chia cho `bH`, hình được phóng nguyên theo `bH`. Với ToolQL: chọn khung nhìn do mình quyết định, camera căn giữa nhân vật là hợp lý; chưa kiểm chứng cách client căn camera.
- Bộ ô lát nhìn thấy: các chỉ số cột `cu.dO..dJ`, hàng `cu.dP..dM` (tính từ camera).

## Thứ tự vẽ cảnh game (`cu`, đoạn quanh dòng 640-780)

Theo thứ tự gọi trong mã (tên hàm là tên trong jar; ý nghĩa là suy từ chỗ gọi, **chưa kiểm chứng bằng ảnh**):

1. Nền trời/phông (`cs.n`, `cu.A`), có tịnh tiến camera.
2. Hiệu ứng nền (`cb.n`, `cn.C`).
3. Ô lát (`aw.R`), nước (`aw.Q`).
4. Vật trang trí lớp giữa (`cu.a(aA, 2)`), đối tượng map (`cb.y`, `bt.b.V`, danh sách `br.C`), hiệu ứng dịch chuyển (`bi.s`, `T`).
5. NPC: lượt 1 cho NPC còn máu (`aa`), lượt 2 vẽ NPC (`C`).
6. Tịnh tiến `aA.g(0, cs.cF)`; người chơi khác (`aa`), nhân vật mình (`aa`), hiệu ứng (`cn.A`).
7. Quái (`cO.C`), hiệu ứng lớp trên (`bi.U`).
8. Người chơi khác (`C`), nhân vật mình (`C`), rồi hiệu ứng chiêu của từng người (`ac`, `ab`).
9. Sau đó: chữ nổi (sát thương, `cu.a(aA, ...)`), thanh máu, tên, giao diện, hộp thoại.

`aa` có vẻ vẽ phần phụ trước (bóng, tên?), `C` vẽ thân; điều này là đoán.

## Ô lát và nước (`aw.R`, `aw.Q`)

- Mã ô lát của ô `(cột, hàng)` là `aX[hàng * rộng + cột]`. Mã `0` là rỗng; **ảnh dùng là ảnh số `mã - 1`**. Cột đầu (0) và cột cuối (`rộng - 1`) không vẽ trong vòng chính, được vẽ riêng ở mép (khi camera sát mép).
- Ảnh ô lát (`aw.dg`): nếu có tệp `/t/<bộÔLát>$1.png` thì nạp 100 ảnh `$1..$100` (mỗi ô một ảnh, ô số `n` là ảnh `n`); nếu không thì dùng `/t/<bộÔLát>.png` là **một dải ngang** các ô 24×24 liền nhau, ô số `n` lấy từ `x = n * 24`. Một chế độ khác của `bQ` dùng `/t/<bộ>/<k>.png`. Jar chỉ giữ 17 tệp trong `x1/t`; bộ còn lại lấy từ server (chưa khảo sát lệnh xin).
- Cờ trong `aw.bs` (xem `map.md`) cũng quyết định cách vẽ:

| Bit (từ 0) | Giá trị | Cách vẽ |
|---|---|---|
| 8 | 256 | không vẽ ô này |
| 5 | 32 | ô nước hoạt hình: dải `/tWater/wtf.png`, 4 hình, hình số `(ih % 8) >> 1` |
| 7 | 128 | ô nước hoạt hình loại 2: `/tWater/twtf.png`, cùng cách |
| 4 | 16 | ô vẽ lệch ngang theo camera (hiệu ứng trượt/phối cảnh) |
| 9 | 512 | vẽ hai lần, lệch 1 điểm ảnh theo dọc (che khe hở) |
| 6 | 64 | mặt nước (vẽ bởi `aw.Q`, dùng `wts`, `wtsN`, `wtsN2` tùy bộ ô lát 5 hoặc 8) |

(Bit 64 cũng được physics dùng để làm chậm: xem `map.md`; đây là cùng một khái niệm "nước".)

Hình nền cảnh: `cd` quản lý danh sách hình nền (`/mapBackGround/<id>.png`, hoặc do server gửi qua `-32` rồi lưu `<zoom>bgItem<id>`), vị trí (x, y) lấy từ mục "hình nền" của `-24`.

## Sprite nhỏ (`at`, bảng `NR_image`)

Mọi hình nhân vật/vật phẩm đều tham chiếu **mã sprite** (số nguyên). Hàm vẽ: `at.b(aA, mãSprite, x, y, biếnĐổi, neo)`.

- Bảng `NR_image` (xem `resources.md`) cho mỗi mã sprite: `[atlas, x, y, rộng, cao]`.
- Nếu `atlas != 4` và ảnh atlas `d[atlas]` đã nạp: cắt hình chữ nhật `(x, y, rộng, cao)` từ atlas rồi vẽ tại `(x, y)` với phép biến đổi (lật gương) và điểm neo. Nếu không thì dùng ảnh riêng của sprite `Small<mã>.png` (trong jar chỉ có `Small1085`, `Small1086`; còn lại xin server bằng `-67`, lưu dưới khóa `<zoom>Small<mã>`).
- Chưa rõ khi nào `d` (mảng atlas) được nạp và từ đâu. Hai chế độ có thể cùng tồn tại.
- Điểm neo (`neo`) là tham số kiểu J2ME (`TOP|LEFT`...); hằng số bị làm rối, **chưa giải**.

## Gói sprite "dữ liệu + ảnh" (`cp`) — quái, hiệu ứng

Quái dùng `/Mob/<loạiQuái>/data` + `/Mob/<loạiQuái>/img.png`; jar không có thư mục này nên quái luôn được nạp từ server: C→S mã `11` (short `loạiQuái`), S→C mã `11` trả: short `loạiQuái`, byte `kiểu`, khối dữ liệu (int `độDài` + byte), khối PNG (int `độDài` + byte), byte `bD`, (nếu `bD` là 1 hoặc 2 còn đọc thêm bảng phụ qua `cj.b(cN, id)`: dạng `byte n; n × (byte m, m × byte)`). Gói hiệu ứng `-66` có cùng khuôn (dữ liệu + PNG). Hai nơi này đều gọi cùng bộ nạp `cp`.

Khối dữ liệu (`cp.a(DataInputStream)` khi `kiểu = 0`; `cp.a(DataInputStream, kiểu)` khi `kiểu ≠ 0`, khác ở chỗ ghi chú):

| # | Trường | Kiểu |
|---|---|---|
| 1 | số hình con `nCon` | byte |
| 2 | mỗi hình con: `id` byte, `x`, `y`, `rộng`, `cao` (vùng cắt trong PNG) | byte, 4 × ubyte |
| 3 | số khung `nKhung` | short |
| 4 | mỗi khung: số mảnh `m` byte; mỗi mảnh: `x` short, `y` short, `chỉSốHìnhCon` byte | |
| 5 | độ dài danh sách hoạt ảnh `nSeq` | short |
| 6 | `nSeq` số short (xem dưới) | short |

Khác biệt của `cp.a(dis, kiểu)`: nếu `kiểu == 1` thì `x`, `y` của hình con là ubyte, ngược lại là short; mục (6) luôn là danh sách phẳng `g[]`.

Mục (6) khi `cp.bF >= 201` (nhánh "có tách hoạt ảnh"): các số `≥ 0` là **chỉ số khung**; một số âm `v` (với `k = v + 500`, từ 0 đến 15) chốt hoạt ảnh số `k`, gom các khung đã đọc trước đó. Hoạt ảnh thiếu thì dùng hoạt ảnh số 2; nếu không có dấu chốt nào thì tất cả khung thuộc hoạt ảnh 0. Có tối đa 16 hoạt ảnh. Ý nghĩa của `bF` và khi nào nhánh còn lại dùng (đọc thẳng `g[]`) **chưa rõ**.

Kích thước ô bao của gói: tính trên khung 0 (`cu` = rộng, `di` = cao).

## Nhân vật (`ce`)

Một nhân vật được ghép từ 3 bộ phần, lấy từ bảng `NR_part` (xem `resources.md`: mỗi mục là loại + một số phần cố định, loại 0 → 3 phần, 1 → 17, 2 → 14, 3 → 2, mỗi phần `{q: mã sprite, M: lệch x, ag: lệch y}`):

| Bộ | Lấy từ | Tên trường |
|---|---|---|
| đầu (`a`) | `NR_part[gD]` | `gD` = id đầu (`short` trong `-30`, `-5`) |
| chân/quần (`b`) | `NR_part[cv]` | `cv` = mã phần của quần (trường `e` trong mẫu vật phẩm loại 1) |
| thân/áo (`c`) | `NR_part[cg]` | `cg` = mã phần của áo (trường `e` trong mẫu vật phẩm loại 0) |

Khớp tên trường với `cj_0`: trong gói `-30` sub 0, vật phẩm trang bị loại `aJ == 0` đặt `cg`, loại `aJ == 1` đặt `cv`. Trong log của `-5` ghi `body= cg`, `leg= cv`, nên "thân = cg", "chân = cv" là đúng như mô tả trên. Có thêm `bY` (cánh/balo).

**Bảng khung `ce.b`** (mảng `int[33][4][3]`, dựng trong khối khởi tạo tĩnh của `ce`, đã trích bằng mô phỏng bytecode): có **33 khung** hoạt ảnh nhân vật; mỗi khung có 4 mục `[chỉSốMảnh, dx, dy]`:

- mục 0 → mảnh của bộ đầu `a`; mục 1 → mảnh của bộ chân `b`; mục 2 → mảnh của bộ thân `c`; mục 3 → mảnh của cánh/balo (`bY`), dùng ở hàm `e()`.
- `chỉSốMảnh` chọn phần tử trong mảng `a` của bộ phần (`ac.a[chỉSốMảnh]`), nên số mảnh trong bộ (3, 17, 14, 2) giới hạn giá trị này.
- Ví dụ khung 0: `[[0,-13,34],[1,-8,10],[1,-9,16],[1,-9,45]]`. Khung 32 (cuối): `[[0,-16,32],[8,-5,13],[9,-8,29],[0,0,0]]`.

Công thức vẽ một mảnh (từ `ce.b(aA, ...)` quanh dòng 455-520), với `(n, n2)` là vị trí chân `(gV, gq)`, `h` là hướng (`+1` quay phải, `-1` quay trái) và `f` là khung:

```
spriteId = bộPhần.a[ b[f][mục][0] ].q
x        = n  + ( b[f][mục][1] + mảnh.M ) * h
y        = n2 - b[f][mục][2] + mảnh.ag
vẽ at.b(spriteId, x, y, lậtTheo h)
```

(công thức đọc trực tiếp từ mã; chưa kiểm chứng bằng ảnh chụp.) Thứ tự gọi quan sát được: bóng (sprite hằng), một sprite phụ, mảnh của bộ đầu, cánh/balo (`e`), mảnh bộ chân, mảnh bộ thân, hiệu ứng trên đầu (`a`). Khi nhân vật dùng trang phục đặc biệt (`gD` thuộc một danh sách id hay `ce.h(gD)`), bộ đầu được thay bằng bộ khác (`m(gD)`).

Chưa có: bảng **trạng thái → số khung** (đứng, chạy, nhảy, đánh...): nằm trong logic `ce` (biến `hw`, `dF`, `hK`...), chưa trích. Không có nó thì chưa chọn được khung đúng.

## Đánh giá cho ToolQL

1. **Tách được logic khỏi vẽ.** Cập nhật logic mỗi khung nằm ngoài các hàm `C`/`paint`; vẽ chỉ cần dữ liệu trạng thái (vị trí, khung, bộ phần, mã sprite). ToolQL (lõi không cửa sổ) chỉ cần giữ dữ liệu này; bộ vẽ SkiaSharp chạy trên bản sao trạng thái khi có người xem.
2. **Acc không xem thì ngủ** hợp lý: hoạt ảnh phụ thuộc bộ đếm khung nên bỏ qua khung không ảnh hưởng game; chỉ cần tính lại `khung = thờiGian / 40 ms` khi bật xem.
3. **Vật lý di chuyển nằm ở client** (`ce` cập nhật trọng lực, va chạm với cờ ô lát, rồi gửi `-7`). Server chấp nhận vị trí do client báo, nên bot phải tự mô phỏng đi/nhảy/rơi nếu muốn di chuyển tự nhiên (Xmap, Train). Tin cậy: suy từ mã, chưa thử với server.
4. **Tài nguyên phụ thuộc server** (quái, bộ ô lát, nhiều sprite): nên kèm kho tệp đĩa; lần đầu tải chậm.
5. Giấy phép: ảnh và dữ liệu trong jar và từ server là tài nguyên của bên thứ ba (xem `risks.md`). Không đưa vào repo.

## Việc còn lại

- Trích bảng **trạng thái → khung** (`ce`) và các bảng offset còn lại (`ce.p`...).
- Giải hằng số điểm neo/biến đổi của `aA` (J2ME `Graphics`).
- Lệnh xin bộ ô lát `/t/<id>.png`, phông chữ, giao diện; cách `d` (atlas) được nạp.
- Đối chiếu với ảnh chụp thật của client để xác nhận công thức vẽ và thứ tự.
- Hiệu ứng chiêu (`cn`, `cb`, `bi`...) và thanh máu/tên.

**Kiểm với gói thật (2026-10-31, một mẫu, quái loại 94, kiểu 0):** bố cục hình con và khung ở bảng trên **đúng**; danh sách hoạt ảnh (mục 6) rỗng và sau nó còn **2 byte `00`** chưa giải. Xem `verification.md`.

## Ảnh trong jar bị XOR (kiểm 2026-10-31, offline)

- Một số ảnh trong jar **không phải PNG thường**: `x1/t/*.png` (bộ ô lát) và `x1/SmallImage/*.png` bắt đầu bằng `63 52 42 43` thay vì `89 50 4E 47`. Chúng bị **XOR với khóa lặp 7 byte `EA 02 0C 04 05 02 F6`** (khóa suy ra bằng cách so 16 byte đầu với chữ ký PNG chuẩn rồi kiểm chu kỳ lặp). Giải xong, cả 4 tệp thử (`t/9`, `t/1`, `t/23`, `SmallImage/Small1085`) là PNG hợp lệ kết thúc bằng khối `IEND`. Ảnh khác (`x1/mainImage/*`, `icon.png`) là PNG thường, không XOR. Chưa biết quy tắc phân biệt ngoài việc kiểm chữ ký PNG; nơi giải trong client chưa tìm ra (có thể nằm ở lớp J2ME của môi trường chạy, không phải trong `nro/`).
- **Bộ ô lát là dải DỌC, không phải ngang:** `t/9.png` rộng 24, cao 624 = 26 ô 24×24 xếp từ trên xuống; `t/1` cao 937 (không chia hết 24, chưa rõ), `t/23` cao 552 = 23 ô. Ô số `n` (mã trong lưới, từ 1) nằm ở `y = (n - 1) * 24`.
- Khớp dữ liệu thật: map 23 và 44 (cả hai bộ ô lát 9) có **mã ô lớn nhất 26** = số ô trong dải, nên quy tắc `mã − 1` đúng (tính từ 0 thì cần 27 ô).
- Jar chứa bộ ô lát 1-15, 22, 23. Bộ khác (nếu có) phải lấy từ server (chưa biết lệnh xin).

## Camera (đọc từ mã client `cu_0.l`, 2026-10-31; đã dựng trong `ToolQL.Render.Camera`)

Hằng số giải từ bảng `ez` đã làm rối: với màn hình rộng `W`, cao `H`, bản đồ `cột × hàng`, ô 24:

- Nhân vật ở giữa chiều ngang, chân ở 2/3 chiều cao: `x = chânX − W/2 + (W/6)·hướng`, `y = chânY − (H·2/3) − 1` (`hướng` = `ce.bU`, -1/0/1).
- Kẹp: `x` vào `[24, (cột − 1)·24 − W]`, `y` vào `[0, (hàng − 1)·24 − H]`. Cận trên được áp sau cận dưới, nên map hẹp hơn khung cho giá trị âm.
- Hệ quả: **hai cột ngoài cùng của map không bao giờ hiện ra** (cận 24 và `cột − 1`), khớp khoảng trống ở hai mép ảnh vẽ cả map.
- Làm mượt mỗi khung (`jk = (đích − hiện tại) << 2; hiện tại += (cL += jk) >> 4`): mỗi khung đi khoảng 1/4 quãng tới đích, đích tính lại bằng công thức trên. ToolQL hiện **chưa** làm mượt, đặt thẳng vào đích.
- Kích thước màn hình của client (`cs.cl × cs.dj`) và hệ số phóng do thiết bị quyết định; ToolQL tự chọn khung 640×360 (kích thước này không phải của client).
- Chưa biết: hướng nhìn của nhân vật mình (ToolQL không theo dõi `bU`, dùng 0); nơi `bV`/`do` (đích) được cập nhật mỗi khung khi nhân vật chạy (chưa tìm thấy, ngoài lúc đặt vị trí ở `cu_0.l`). Chưa đối chiếu với ảnh chụp thật.
