# Kế hoạch cột mốc 0: thử giao thức

> Đã xong (2026-10-30). Từ 2026-10-31 hướng sản phẩm đổi (xem `2026-10-31-quan-ly-cua-so-client.md`); phần giao thức vẫn là tham chiếu.

Spec: `docs/superpowers/specs/2026-10-28-v0.1-pham-vi.md` (phạm vi đã được duyệt). Giao thức tham chiếu: `docs/protocol/` (toàn bộ **từ-mã**).

## Mục tiêu

Một chương trình dòng lệnh nhỏ, không giao diện, nối server thật bằng **một acc thử**, đi từ bắt tay khóa tới nhận được thông tin nhân vật và map, **lưu gói thô** ra ngoài repo, và kiểm chứng từng nhận định trong tài liệu giao thức. Thư viện `ToolQL.Protocol` viết ở đây sẽ được dùng lại cho lõi v0.1.

Không làm ở cột mốc 0: giao diện, vẽ, auto, nhiều acc, tự nối lại, giải captcha.

## Giả định mặc định (các câu hỏi mở chưa được trả lời)

- Chạy cột mốc 0 trên **máy ảo** (VPS thật để sau). Nếu bạn muốn chạy trên VPS thật thì chỉ đổi cách chép và chạy, không đổi plan.
- Mật khẩu chưa cần lưu lâu dài: cột mốc 0 đọc acc thử từ `%USERPROFILE%\.config\toolql\test-account.env` mỗi lần chạy. Cách lưu (DPAPI) để v0.1.
- Chưa đóng gói: chạy bằng `dotnet run`.

## Quy tắc an toàn (cổng chặn, không được bỏ qua)

1. **Giai đoạn 1** (chỉ nối và nhận khóa, **không đăng nhập**) làm được ngay khi mã xong. Vẫn là nối vào server thật nên Claude báo bạn một câu trước khi chạy.
2. **Giai đoạn 2** (đăng nhập thật): Claude **dừng và hỏi bạn** ngay trước khi chạy; mỗi lần một acc; chỉ đọc `test-account.env` sau khi bạn đồng ý.
3. Server từ chối đăng nhập, ngắt kết nối bất thường, hay gửi `-85` (captcha): **dừng**, báo kết quả, không thử lại, không nối lại tự động.
4. Mật khẩu không vào log, tệp bắt gói, commit hay hội thoại. Gói đăng nhập (`-29` sub 0) khi lưu phải **che** (ghi mã lệnh và độ dài, bỏ nội dung).
5. Tệp bắt gói và dữ liệu tải từ server nằm ngoài repo (`%USERPROFILE%\.toolql\captures\`); không commit.
6. Nhắc: tự động hóa trên server chính thức có thể bị khóa acc.

Ghi chú: có một tệp `test-account.env` nằm ở gốc repo `C:\Dev\ToolQL`. Nó bị `.gitignore` (`*.env`) bỏ qua và chưa được commit, nhưng vị trí chuẩn là `%USERPROFILE%\.config\toolql\`. Claude không đọc nội dung nó.

## Cấu trúc

```
ToolQL.sln
src/ToolQL.Protocol/          net8.0, thư viện
tests/ToolQL.Protocol.Tests/  xUnit
tools/ToolQL.Probe/           net8.0, chương trình dòng lệnh
```

Namespace `ToolQL.Protocol` / `ToolQL.Probe`. Comment, XML doc, chuỗi hiển thị bằng tiếng Việt. Không thêm gói NuGet ngoài xUnit và gói test đi kèm.

## Các nhiệm vụ

Mỗi nhiệm vụ: viết test trước (TDD) với phần hàm thuần, chạy test, đọc kết quả, rồi commit nhỏ (`feat(protocol): ...`). Test chỉ chứng minh **mã khớp tài liệu**, không chứng minh khớp server; phần đó dành cho các giai đoạn 1-3.

### N1. Khung dự án
- Tạo solution, 3 dự án, tham chiếu. Thêm `captures/` vào `.gitignore` nếu chưa có; kiểm tra `*.env` đã bị bỏ qua.
- Kiểm tra: `dotnet build` và `dotnet test` (0 test) chạy được.

### N2. Đọc/ghi kiểu Java (`JavaStreamReader`, `JavaStreamWriter`)
- Big-endian: byte có dấu, ubyte, short, ushort, int, long, bool, `readUTF` (độ dài ushort + UTF-8), đọc `n` byte.
- Test: vòng ghi-đọc mọi kiểu; giá trị biên (byte -128/127, short âm, UTF tiếng Việt).
- Đọc quá dữ liệu thì ném lỗi rõ ràng (cần cho phép kiểm "bộ đọc tiêu thụ đúng bằng độ dài payload").

### N3. Mã hóa dòng XOR (`XorCipher`)
- Theo `transport.md`: biến đổi khóa thô `k[i+1] ^= k[i]` (tuần tự); hai chỉ số **riêng** cho chiều gửi (`R`) và nhận (`Q`); mỗi lần dùng 1 byte khóa thì tăng chỉ số modulo `n`.
- Test với vectơ tính tay từ công thức (ghi rõ trong test): khóa 1 byte, nhiều byte, quay vòng chỉ số, hai chiều độc lập.

### N4. Khung gói (`PacketWriter`, `PacketReader`)
- Gửi: 1 byte mã lệnh + 2 byte độ dài (big-endian) + payload; có khóa thì XOR từng byte (cả mã lệnh và độ dài) theo thứ tự trên đường truyền.
- Nhận: đọc mã lệnh; nếu thuộc nhóm độ dài 3 byte (`-32, -66, 11, -67, -74, -87, 66, 12`) đọc 3 byte theo công thức trong `transport.md` (nhánh nghi vấn, **ghi lại nó có được dùng không**), còn lại 2 byte; đọc đủ payload.
- Test: khung không khóa, có khóa, payload rỗng, payload 70 000 byte, nhóm 3 byte (kiểm đúng như tài liệu mô tả; ghi chú là chưa xác nhận với server).
- Chạy trên `Stream` (đồng bộ, dùng được trên luồng riêng), để thay bằng luồng bộ nhớ trong test.

### N5. Bắt tay khóa (`KeyHandshake`)
- Gửi gói `-27` rỗng (`E5 00 00`), nhận `-27`: `byte n`, `n` byte khóa, UTF, int, byte; trả khóa đã biến đổi và bật mã hóa; ghi lại 3 trường còn lại.
- Test: từ gói `-27` dựng tay → khóa đúng, trường đúng; báo lỗi khi `n = 0` hoặc thiếu byte.

### N6. Bộ đọc bảng/gói nghiệp vụ (chỉ phần cần cho cột mốc 0)
Mỗi bộ đọc trả cấu trúc **và số byte đã tiêu thụ**; probe so với độ dài payload (đây là phép kiểm chính):
- `-28` sub 4 (4 phiên bản + `n` long), `-30` sub 0 (nhân vật), `-82` (bảng ô lát → cờ), `-24` (thông tin map, kể cả thân dùng chung với `-28` sub 10), `-5` (người chơi xuất hiện), `-28` sub 10 (lưới ô lát).
- Test bằng gói **dựng tay** từ mô tả trong tài liệu (không đưa dữ liệu của client vào repo). Chỗ tài liệu ghi "chưa rõ" thì đọc và bỏ qua nhưng đếm byte.
- Chưa làm: `NR_*`, `-67/-66/-32` (ảnh), bảng skill/item (sau khi probe xác nhận khung cơ bản).

### N7. Bộ bắt gói (`CaptureSink`)
- Ghi mỗi gói thành một dòng JSON: thời điểm, hướng (C→S / S→C), mã lệnh, sub (nếu có), độ dài, hex payload đã giải mã. Gói `-29` sub 0 ghi **che** (không ghi nội dung).
- Thư mục: `%USERPROFILE%\.toolql\captures\<ngày-giờ>\`. Test: mật khẩu **không** xuất hiện trong tệp.

### N8. Chương trình Probe
Mỗi lệnh dừng gọn và không tự nối lại:

- `probe servers`: in danh sách server (bảng trong `servers.md`).
- `probe handshake <server>` (**giai đoạn 1**): nối, bắt tay khóa, in kết quả (độ dài khóa, ba trường còn lại), nhận thêm gói trong 5 giây nếu có, ngắt. Không đăng nhập.
- `probe login <server>` (**giai đoạn 2-3**, cần bạn đồng ý): đọc acc thử, bắt tay khóa, gửi `-29` sub 0 (phiên bản `"2.5.0"`) và `-29` sub 2 nếu cần; rồi nhận gói tối đa 60 giây hoặc tới khi nhận `-24`, theo các bước:
  1. nhận `-28` sub 4, so 4 phiên bản; gửi yêu cầu tải bảng nào lệch (`-87`, `-28` sub 6/7/8), lưu thô;
  2. gửi `-28` sub 13; nhận `-30` sub 0, `-82`, `-24` (và `-28` sub 10 nếu cần lưới);
  3. in tóm tắt: mã lệnh nhận được theo thứ tự, độ dài, bộ đọc nào tiêu thụ đúng/thiếu/thừa byte;
  4. ngắt kết nối sạch, không nối lại.
- Gặp `-85` hoặc đăng nhập bị từ chối: in mã/thông báo (nếu có) rồi thoát với mã lỗi riêng.
- Thứ tự gửi của client thật sau đăng nhập chưa chắc: probe **chỉ gửi gói tối thiểu** (đăng nhập, yêu cầu bảng lệch, báo tải xong) và ghi phản hồi; server im lặng thì dừng, không thử gói lạ.

### N9. Kiểm chứng và cập nhật tài liệu
Sau mỗi giai đoạn, viết `docs/protocol/verification.md` (kết quả thật, không thêm suy đoán) và đổi mức tin cậy `từ-mã` → `bắt-gói` ở các file giao thức cho mục đã xác nhận (hoặc ghi lại điều sai).

Danh sách phải kiểm (mỗi mục ghi: đúng / sai / không thấy / chưa kiểm):
1. `-27`: bố cục payload, biến đổi khóa, chỉ số XOR hai chiều.
2. Nhóm mã lệnh độ dài 3 byte và công thức cộng 128 (nhánh nghi vấn).
3. Server có chấp nhận `"2.5.0"`; gói trả lời đăng nhập thành công/thất bại là gói nào, trường gì.
4. `-28` sub 4: 4 phiên bản, `n` long cuối; client có cần trả lời gì.
5. Cách yêu cầu bảng: `-87`, `-28` sub 6/7/8 và bảng trả về (kiểm bằng độ dài và bộ đọc).
6. Lúc server gửi `-82`; bộ đọc `-82` tiêu thụ đúng.
7. `-30` sub 0: tiêu thụ đúng; các trường chưa rõ (`ht`, ...) nhận giá trị gì.
8. `-24`: tiêu thụ đúng cả khi có và không có lưới trong tài nguyên; map nào cần `-28` sub 10; rộng/cao có âm không.
9. `-5` và các gói khác thấy trong vài giây đầu.
10. Việc client phải gửi định kỳ (ping/giữ kết nối) để không bị ngắt: chỉ ghi quan sát, không suy.

## Tiêu chí hoàn thành cột mốc 0

- `dotnet test` toàn bộ xanh; ghi rõ số test và kết quả.
- Giai đoạn 1 chạy được, hoặc ghi lý do không (server không nối được, ...).
- Giai đoạn 2-3 chạy **chỉ khi bạn đồng ý**, một acc; nếu không chạy thì ghi "chưa kiểm chứng".
- `verification.md` có kết quả từng mục trong danh sách trên, phân biệt rõ đã kiểm / chưa kiểm.
- Không có mật khẩu hay tệp bắt gói trong repo (kiểm bằng `git status` và tìm chuỗi).

## Rủi ro và cách xử lý

- Server không nhận `"2.5.0"` hoặc kiểm tra thêm: dừng, ghi kết quả; bước tiếp (thử phiên bản của client chính thức, nếu có) do bạn quyết.
- Acc bị khóa vì thử: giai đoạn 2 dùng đúng một acc thử, báo trước khi chạy.
- Địa chỉ server trong client đã cũ: giai đoạn 1 phát hiện sớm (không phân giải được / không nối được).
- Khung gói sai ở chỗ nghi vấn: N4 đã tách nhánh; sửa theo bắt gói rồi cập nhật `transport.md`.

## Thứ tự

N1 → N2 → N3 → N4 → N5 (phần thuần, có test, không đụng mạng) → N7 → N8 (`servers`, `handshake`) → **giai đoạn 1** → N6 → N8 (`login`) → **cổng: hỏi bạn** → giai đoạn 2-3 → N9.

Chưa ước lượng thời gian: sẽ nói sau khi xong N5 và biết khung gói có khớp không.
