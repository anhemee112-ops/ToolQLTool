# Điều chỉnh hướng: quản lý cửa sổ client (hướng đi hiện tại; cập nhật theo lời chủ repo 2026-10-31)

> **Đính chính 2026-10-31 (chủ repo):** quy tắc 40 giây là sau khi **mất kết nối**, KHÔNG phải sau khi đóng/mở. Phần dưới viết trước khi được đính chính; mục "Quy tắc đăng nhập lại, mất kết nối, bảo trì" ở cuối file là bản đúng và thắng mọi chỗ mâu thuẫn.

Ngày 2026-10-31 chủ repo sửa yêu cầu: ToolQL phải giống **Tool 2** (ToolCikey, chỉ để tham chiếu): một cửa sổ quản lý, mỗi acc **một cửa sổ client riêng**. Hướng "một tiến trình tự vẽ map trong cửa sổ quản lý" (M2-M3 trước đó) **không còn là đường chính**. Tài liệu này ghi những gì đã chốt, đã đo và còn mở. `CLAUDE.md` và `decisions.md` (quyết định 2026-10-27) **chưa được sửa** cho khớp; đợi chủ repo duyệt.

## Đã chốt (chủ repo, 2026-10-31)

- Mỗi acc một **cửa sổ client riêng**, như Tool 2 (không nhúng thành tab trong ToolQL).
- Client là **chính jar** `Dragonboy250 v3.5.jar` (MIDlet J2ME), chạy bằng **một trình chạy J2ME**. Sau khi so sánh, chủ repo chọn trình chạy **`C:SERSDEVDOWNLOADSANGELACHIP.JAR`** THAY CHO FREEJ2ME-PLUS (XEM MụC "ANGELCHIP" BêN DướI). Không dùng `ModDP250.exe` (đã rút lại; không chạy nó).
- Bản đầu: bảng acc (thêm/sửa/xóa, mật khẩu DPAPI), Login/Đóng, Xếp cửa sổ (lưới), Ẩn client, Hiện, Auto login (rớt mạng thì mở lại sau **>= 40 giây**), cột trạng thái theo tiến trình/cửa sổ. **Không** có auto chơi game.

## Tool 2 làm thế nào (đọc từ `HUONG-DAN-SU-DUNG.txt`, không chạy, không dịch ngược)

Cửa sổ quản lý giữ danh sách acc; Login mở một cửa sổ game riêng tự đăng nhập; cửa sổ game báo tình hình về; nút Xếp cửa sổ (lưới), Ẩn client (thu nhỏ), Hiện (đưa ra trước); Auto login mở lại acc rớt mạng sau 35 giây; Auto sleep (cửa sổ không ai đụng thì thôi vẽ để nhẹ máy). Nó còn tự giải captcha: **ToolQL không làm** (quy tắc repo).

## Đã kiểm (2026-10-31)

- Jar là MIDlet MIDP 2.1/CLDC 1.1 (`nro.hero69`); dùng `javax.microedition` lcdui, rms, io, midlet. Không tự chạy bằng `java -jar`.
- Client lưu tài khoản trong RMS của chính nó (khóa `acc`, `pass`; `ah_0.java`), nên truyền acc bằng cách **ghi sẵn RMS** trước khi mở (chưa thử). Chưa biết cách bấm "Chơi mới" tự động.
- **FreeJ2ME-Plus 1.52** (GPLv3, kèm ObjectWeb ASM giấy phép BSD; tải từ bản phát hành chính thức, để ngoài repo ở `C:\Dev\ToolQL-input\freej2me-plus`) **chạy được jar**: hiện màn hình bắt đầu thật của game (v2.5.0, Chơi mới / Đổi tài khoản / Máy chủ / Cấu hình) trong cửa sổ 256×379 (canvas 240×320). Dữ liệu và cấu hình lưu ở thư mục làm việc, nên **mỗi acc một thư mục làm việc** là RMS riêng.
- Trình này **tự khởi động lại** (đổi mã hóa sang ISO_8859_1) nếu không truyền `-Dfile.encoding=ISO_8859_1`: tiến trình đầu thoát ngay, tiến trình con mới là thật (khó theo dõi PID). Truyền cờ đó trực tiếp thì không bị.
- Client tự nối tới `112.213.94.23:14445` (Vũ trụ 1) 5 lần khi vừa mở, **không đăng nhập**. Chưa rõ để làm gì.
- **RAM/CPU của MỘT cửa sổ, ở màn hình bắt đầu (JDK 25, Windows 10, đo 20 giây sau khi mở):**

| Cấu hình JVM | RAM (working set) | RAM riêng | CPU lúc rảnh |
|---|---|---|---|
| mặc định | 480 MB | 543 MB | 2,8% của 1 lõi |
| `-Xmx48m -Xms16m -XX:+UseSerialGC -XX:TieredStopAtLevel=1 -Xss512k` | **119 MB** | 105 MB | **19,4%** của 1 lõi |

Chỉ một mẫu mỗi dòng, chưa lặp, chưa đo khi đang trong game. Với cấu hình gọn, 10 cửa sổ sẽ vào khoảng 1,1-1,2 GB RAM và gần 200% CPU **chỉ riêng client**: không vừa VPS 1 GB / 1 CPU của chủ repo. Cũng là nguyên nhân bản QLTK cũ bị lag. Đây là rủi ro lớn nhất của hướng này.

## Còn mở / chưa kiểm

1. Có hạ được RAM/CPU xuống mức dùng được không (giới hạn FPS của trình chạy `fps`, JVM nhỏ hơn, cửa sổ ẩn thì thôi vẽ). Mục tiêu 10 acc/1 GB **chưa có cơ sở**.
2. Mở acc tự động: ghi RMS `acc`/`pass` rồi bấm "Chơi mới" bằng cách nào (gửi phím vào cửa sổ?).
3. JDK/JRE nào chạy trên Windows Server 2012 R2 và có kèm gói phát hành không (quyết định cũ là không kèm JRE).
4. Trạng thái trong game (map, HP...) như cột Status của Tool 2: chưa có kênh từ client.
5. Giấy phép: GPLv3 của FreeJ2ME-Plus nếu kèm trong gói phát hành; giấy phép client/tài nguyên vẫn chưa rõ.
6. Repo cũ `quyenlei/Dragonboy` (JavaHost, QLTK cũ) là repo riêng tư, mình không đọc được; chỉ vào được nếu chủ repo clone về máy.

## Dùng lại được từ phần đã làm

Kho acc DPAPI (`AccountStore`), quy tắc chờ 40 giây (`SessionHub` / `Backoff`), khung WPF và bảng acc. Phần giao thức, `ToolQL.Render`, bộ vẽ map, lưới 4 ô tự vẽ không nằm trên đường chính (giữ nguyên trong repo, không xóa).

## AngelChip (`AngelAChip.jar`), trình chạy chủ repo chọn (2026-10-31)

**Là gì (xem tĩnh, chưa chạy ngoài phép thử dưới):** 490 KB, SHA-256 `559bb3d7…1b3352`, một bản **MicroEmulator 2.0.4** (`org.microemu.app.Main`) có thêm khoảng 181 lớp tên một chữ cái ở gốc jar (không thuộc MicroEmu chuẩn; trông như thư viện chỉnh bytecode đã bị làm rối tên). Quét các lớp đó: có `ClassLoader`/`URL`, reflection, `FileOutputStream`, `DatagramPacket`; **không** thấy chạy lệnh hệ thống, `Socket`, `Robot` hay mã hóa; không có chuỗi nào về game hay server. Đây là phân tích tĩnh tầng ngoài, **không chứng minh an toàn**, và không biết nguồn gốc hay giấy phép của bản chỉnh này (MicroEmulator gốc là Apache 2.0).

**Cách chạy (đã thử):**
`java -Duser.home="<thư mục acc>\home" -jar AngelAChip.jar --rms file --id <tên> --resizableDevice 240 320 --appclasspath "<jar game>" nro.hero69`
- Chạy thẳng lớp MIDlet (`nro.hero69`) để bỏ qua màn hình Launcher; truyền tệp jar thẳng thì dừng ở Launcher chờ chọn.
- Cấu hình và RMS nằm dưới `user.home\.microemulator`: đổi `-Duser.home` cho từng acc là có kho RMS riêng.
- Cửa sổ "AngelChip Emulator" khoảng 256×404 (canvas 240×320). Màn hình đầu của game: **"Vui lòng tải dữ liệu để chơi / Tải dữ liệu"** (kho RMS trống nên client đòi tải dữ liệu từ server; mình chưa bấm). Nên có sẵn một kho dữ liệu mẫu để sao chép cho mỗi acc.
- Client **tự nối `112.213.94.23:14445` và gửi gói `-27` (bắt tay khóa)** ngay khi mở, chưa đăng nhập.
- Không thấy tiến trình tự khởi động lại như FreeJ2ME.

**RAM/CPU (JDK 25, Windows 10, đo sau 25 giây ở màn hình "Tải dữ liệu", chưa trong game, chưa lặp):**

| Cấu hình | RAM working set | RAM riêng | CPU lúc rảnh |
|---|---|---|---|
| mặc định, 1 cửa sổ | 126 MB | 172 MB | 1,7% của 1 lõi |
| `-Xmx64m -Xms16m -XX:+UseSerialGC -XX:TieredStopAtLevel=1 -Xss512k`, 1 cửa sổ | **96 MB** | 88 MB | **1,3%** |
| cùng cấu hình gọn, **3 cửa sổ cùng lúc** | **282 MB tổng** (~94 mỗi cái) | 261 MB | 3,0% tổng |

So với FreeJ2ME-Plus (gọn: 119 MB, 19% CPU): AngelChip nhẹ hơn rõ. Ngoại suy tuyến tính (chưa đo) 10 cửa sổ ≈ 950 MB RAM: **vẫn sát/vượt giới hạn VPS 1 GB** cùng Windows và ToolQL; CPU thì ổn khi rảnh. RAM thật trong game có thể cao hơn. Chưa thử trên Windows Server 2012 R2 và JDK/JRE phù hợp ở đó.

## Cửa sổ quản lý đã làm (2026-10-31, `src/ToolQL.App`)

Theo mẫu Tool 2 (chỉ đọc tài liệu hướng dẫn của nó). Logo của chủ repo: `src/ToolQL.App/Assets/ql-logo.png` (biểu tượng cửa sổ, tệp `.exe` và thẻ vàng).

**Có:** thanh menu (File, Cài đặt, View, Trợ giúp); hàng nút trên (+ Thêm, Xóa, ▶ Login, Đóng, Login tất cả, Tắt tất cả, Tick, Bỏ tick); hàng nút dưới (Auto login: TẮT/BẬT, Xếp cửa sổ, Ẩn client, SHOW ALL); bảng acc (STT, TK, SV, Ghi chú, Status, Hiện, ✓), thẻ vàng thông tin acc đang chọn; File → Thêm nhiều tài khoản (`TK|SV|MK|GHI CHÚ`, có một dòng sai là không thêm acc nào); View → sắp xếp theo thứ tự thêm / server / bảng chữ cái "tự nhiên", ẩn tên tài khoản; Cài đặt chung (Java, trình chạy, jar game, dữ liệu mẫu, kích thước màn hình, delay mở acc, tự bấm "Chơi TK"); Cài đặt → Tạo dữ liệu mẫu cho client.
**Không có (chưa làm):** các tab theo loại auto, cột Sức mạnh/Vàng/Khu..., proxy, khung giờ, đổi loại auto, Hành trang, Reset data, tìm kiếm data, tự cập nhật.

**Cách hoạt động:** mỗi acc có thư mục riêng `%LOCALAPPDATA%\ToolQL\clients\<id>\home` (sao chép từ bản mẫu, khoảng 770 KB). Trước khi mở, ToolQL ghi `vjacc.rs`, `vjpass.rs` (tài khoản, mật khẩu) và `vjsvselect.rs` (server) vào kho RMS; khi client dừng thì xóa hai tệp đầu. **Mật khẩu nằm dạng chữ trong kho RMS khi client đang chạy** (client đọc từ đó). `ClientHub` (Core) quản lý tiến trình: mở cách nhau "delay mở acc", **chờ >= 40 giây sau khi một lần chạy kết thúc (đóng tay, thoát, bị giết, mở lỗi)** mới cho mở lại, Auto login mở lại acc có ✓. Mỗi tiến trình client nằm trong một Job object nên tắt ToolQL thì mọi cửa sổ client tắt theo.

**Đã kiểm trên máy ảo (Windows 10, JDK 25):**
- 3 acc giả ở chế độ `--khong-dang-nhap` (không ghi tài khoản/mật khẩu vào RMS): mở lần lượt cách nhau 3 giây (1 → 2 → 3 `javaw`), tổng 287 MB RAM; Xếp cửa sổ đặt đúng lưới (x = 0, 260, 520); Ẩn client thu nhỏ cả 3, SHOW ALL khôi phục; Đóng một acc thì tiến trình tắt, trạng thái "Đã đóng"; Login lại ngay bị chặn ("thu1: chờ thêm 26 giây", không mở thêm tiến trình); tiêu đề cửa sổ client là "tài khoản · SVn".
- Auto login: bật công tắc thì chỉ acc có ✓ được mở; giết tiến trình từ bên ngoài hiện "Đã thoát (mã -1)"; mở lại sau **41,9 giây** (không sớm hơn 40).
- Tắt ToolQL: 0 `javaw` còn lại; thông tin đăng nhập bị xóa khỏi kho RMS.
- **Hai lượt đăng nhập thật bằng acc thử (Vũ trụ 1) qua giao diện:** client đọc đúng tài khoản đã ghi sẵn (nút hiện "Chơi TK: …") nhưng **không tự đăng nhập**, phải có người bấm nút đó. `PostMessage` chuột vào cửa sổ **không có tác dụng** (Java AWT không nhận); chuột thật thì được. Vì vậy ToolQL bấm bằng chuột thật tại điểm ~(50%, 40%) vùng client (mới kiểm với màn hình 240×320) sau 12 giây: đưa cửa sổ lên trên cùng trong khoảng 0,4 giây, bấm, trả con trỏ và cửa sổ cũ về. Lượt cuối: Login, không chạm gì thêm, **acc vào game trong vòng 40 giây** (Làng Kakarot).
- **RAM/CPU thật khi acc đang trong game: 99 MB và 4,8% của một lõi cho MỖI cửa sổ.** 10 acc ≈ 1 GB RAM và gần 50% một lõi chỉ riêng client: vẫn không vừa VPS 1 GB.

**Chưa kiểm / còn thiếu:**
- Khi đã vào game client luôn hiện hộp thoại "Đã có bản cập nhật mới, bạn có muốn cập nhật không?" (Có/Không). ToolQL chưa xử lý; acc vẫn ở trong game, nhưng nút bấm "Không" chưa tự động.
- Chỉ số server trong `vjsvselect` (thứ tự danh sách của client) mới xác nhận với 0 (Vũ trụ 1); các server khác chưa kiểm.
- Không biết client có đang bị rớt mạng hay đang ở màn hình nào (chỉ thấy tiến trình còn sống); nên "rớt mạng" mà cửa sổ còn mở thì Auto login không mở lại.
- Cú bấm chuột thật cần màn hình đang mở và cửa sổ không bị che hoàn toàn; trên VPS qua Remote Desktop **đã thu nhỏ/ngắt phiên có thể không bấm được**: chưa thử.
- Chưa thử trên Windows Server 2012 R2 (WPF, JDK/JRE phù hợp, Job object lồng nhau); chưa thử tỉ lệ DPI khác 100%.
- `ToolQL.Render`, `ToolQL.Core.SessionHub`/`GameState`, giao thức: vẫn trong repo, không nằm trên đường chính. Giao diện tự vẽ cũ nằm ở `src/ToolQL.App/_cu/` (không biên dịch).

## Quy tắc đăng nhập lại, mất kết nối, bảo trì (làm rõ 2026-10-31; **mục này thắng mọi chỗ mâu thuẫn ở trên**)

Lời chủ repo: "login lại sau 40s sau khi mất kết nối, chứ không phải đóng mở tab; treo xuyên ngày đêm có thể bị mất kết nối, hoặc game bảo trì định 4h sáng, từ lúc có thông báo bảo trì thì tự động logout, sau đến 5h mới login vào lại; login vào ở QLTK thì tự login tài khoản đó vào game của client luôn".

**Sửa các chỗ đã viết sai ở trên:**
- Đóng tay rồi mở lại **không** bị chặn 40 giây (các dòng ghi "Login lại ngay bị chặn", "chờ >= 40 giây sau khi một lần chạy kết thúc (đóng tay, thoát, bị giết, mở lỗi)" là theo cách hiểu cũ, sai).
- "Acc thử bị game ngắt ở 10:20" **không đúng với client thật**: chỉ thấy khi dùng `Host`/`Probe`. Client thật giữ kết nối liền 15 phút.

**Cài đặt đúng (`ClientHub`, Core):**
| Tình huống | Hành vi |
|---|---|
| Người dùng bấm Đóng / Tắt tất cả | Không phải chờ; mở lại ngay (chỉ còn khoảng nghỉ giữa hai lần mở acc). |
| **Mất kết nối** (client thoát/sập/bị giết; hoặc kết nối TCP tới server rớt khi cửa sổ còn mở) | Đóng client; **chờ >= 40 giây** mới mở lại (người dùng bấm Login cũng bị chặn tới lúc đó); Auto login mở lại. Rớt liên tiếp (mỗi lần chạy < 10 phút) thì chờ 80, 160... tối đa 5 phút; **3 lần liền thì dừng tự mở lại** (tránh lặp đăng nhập mãi, ví dụ sai mật khẩu); bấm Login thì thử lại. |
| Mở lỗi (thiếu Java...) | Người dùng thử lại ngay được; Auto login chờ 40 giây giữa các lần thử. |
| **Bảo trì** (khung giờ cài đặt, mặc định 03:30-05:00 giờ Việt Nam, chủ repo xác nhận; qua nửa đêm được) | Vào khung: đóng mọi client và nhớ các acc đang chạy hoặc xếp hàng; suốt khung không tự mở lại (Auto login tạm dừng; người dùng tự mở tay vẫn được); hết khung: mở lại đúng các acc đó, cách nhau khoảng nghỉ, **không** phải chờ 40 giây. ToolQL **không đọc được thông báo bảo trì trong game**, chỉ theo giờ; giờ mặc định đã được chủ repo xác nhận. |

**Phát hiện mất kết nối khi cửa sổ còn mở:** `TcpTableProbe` đọc bảng kết nối TCP của Windows (IPv4, `GetExtendedTcpTable`, chỉ đọc, không cần quyền quản trị) xem tiến trình có kết nối ESTABLISHED tới cổng server game (14445, 14446, 14447, 20000) không. `ConnectionWatcher`: kết nối phải từng liên tục >= 20 giây rồi biến mất liên tục >= 15 giây mới tính. Kết quả không xác định thì bỏ qua. **Kết nối ổn định không chứng minh đã đăng nhập:** client giữ kết nối tới server ngay từ màn hình menu (thấy ở chế độ không đăng nhập), nên nhãn trên giao diện là "Đã kết nối", không phải "Trong game".

**Đăng nhập tự động:** client chỉ hiện nút "Chơi TK: …" từ RMS rồi chờ bấm; ToolQL bấm bằng chuột thật sau 12 giây (xem mục trước). Đã thử 5 lượt đăng nhập thật với acc thử; lượt 2, 4, 5 vào game không cần chạm tay.

**Đồ họa thấp nhất mặc định:** ToolQL ghi sẵn `lowGraphic=1`, `isPaintAura=0`, `isPaintAura2=0` vào RMS mỗi lần mở (các khóa này lấy từ mã client: menu Cấu hình ghi đúng chúng; `cs_0`: `lowGraphic == 1` là chế độ thấp). Đã xem ảnh: cảnh game mất nền trời/núi/hào quang. **Không giảm RAM** (vẫn ~99 MB); CPU đo được 8,7% một lõi ở một lần đo (lần trước 4,8% ở đồ họa thường, nhưng khác cảnh và khác thời điểm, **không so sánh được**).

**Popup "Đã có bản cập nhật mới" (Có/Không) tự đóng:** hiện khoảng 16 giây sau khi bấm "Chơi TK". `PrintWindow` chụp vùng client (chụp đủ dù bị cửa sổ khác đè, không giành tiêu điểm); `UpdatePromptDetector` nhận nền trắng + hai nút cam (đo trên ảnh thật); `PopupGuard` yêu cầu thấy 2 lần liên tiếp rồi bấm "Không" (thử lại tối đa 3 lần, canh từ giây 10 tới phút 5 mỗi lần chạy). Nhật ký ToolQL của lượt thật: bấm "Chơi TK" 10:52:50, thấy popup 10:53:06, bấm "Không" 10:53:08, popup đóng 10:53:09. Cũng khớp mọi hộp thoại Có/Không cùng bố cục (bấm "Không" là hủy, an toàn). Mới đo ở màn hình game 240×320.

**Nhật ký sự kiện:** `toolql.log` trong thư mục dữ liệu, mỗi sự kiện một dòng có giờ (tên acc che bớt, không có mật khẩu), quá 1 MB thì chuyển thành `.1`.

**Chưa kiểm chứng:**
- **Mất kết nối thật** (rớt khi cửa sổ còn mở rồi tự đăng nhập lại sau >= 40 giây): chưa có sự kiện thật nào để thử. Đã có test giả lập (bộ dò giả, tiến trình giả, đồng hồ giả) và thử giết tiến trình ngoài đời thật (mở lại sau 41,9 giây). Phần "kết nối rớt, tiến trình còn sống" chỉ mới kiểm bằng test.
- Khung bảo trì thật của game; bảo trì ngoài đời thật (đã thử khung giả 2 phút: đóng đúng giờ, mở lại đúng giờ).
- Windows Server 2012 R2; phiên Remote Desktop thu nhỏ/ngắt (chuột thật và `PrintWindow`); DPI khác 100%.
- Hành vi khi client tự kết nối lại ngay sau khi rớt (có thể đăng nhập lại sớm hơn 40 giây trước khi ToolQL kịp đóng).
