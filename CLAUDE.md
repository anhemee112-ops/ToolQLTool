# ToolQL (bản viết lại): quản lý nhiều tài khoản Ngọc Rồng Online, mỗi acc một cửa sổ client (mẫu: Tool 2)

**Việc đầu tiên khi mở repo trong máy ảo mới:** làm theo `docs/dev-setup.md` (dựng môi trường; máy ảo dựng theo `docs/vm-setup.md`), rồi đọc `docs/decisions.md` và `docs/superpowers/specs/2026-10-31-quan-ly-cua-so-client.md` (hướng đi hiện tại). Setup môi trường là ưu tiên số một.

Giao tiếp bằng tiếng Việt; comment, XML doc và chuỗi hiển thị trong code cũng bằng tiếng Việt.

> Các rule frontend (Blade, SCSS, BEM, Core Web Vitals) trong `~/.claude/CLAUDE.md` **không áp dụng**. Đây là dự án C# (.NET 8).

## Chủ repo (ngữ cảnh để làm việc đúng ý)

- Giao tiếp tiếng Việt, ngắn gọn, dễ hiểu; giải thích việc quan trọng trước, chi tiết sau.
- Chủ repo treo acc **xuyên ngày đêm** trên **VPS thật** (Windows Server 2012 R2, 1 GB RAM, 1 CPU, khoảng 10 acc/máy) qua Remote Desktop, chép bản build sang bằng tay (zip qua Google Drive) và tự thử. Hay gửi ảnh chụp màn hình. Cần hướng dẫn **từng lệnh** khi bắt họ làm việc trên VPS (nói rõ PowerShell hay CMD, `cd` vào thư mục nào).
- Treo xuyên ngày đêm nên **mất kết nối** và **game bảo trì hằng ngày (khoảng 4h sáng)** là chuyện thường: tool phải tự xử lý (xem "Quy tắc đăng nhập lại").
- Muốn: nhẹ, mượt, đẹp, dễ dùng; không lag. Mẫu sản phẩm là **Tool 2** (ToolCikey, `C:\Users\Dev\Desktop\Tool 2.zip`): một cửa sổ quản lý, mỗi acc một cửa sổ game riêng.
- Việc lớn: brainstorming → spec → plan trước khi viết mã. Khi chưa chắc thì nói rõ là chưa chắc, đừng khẳng định. **Đã hiểu sai yêu cầu hai lần (2026-10-31): khi chưa rõ thì hỏi một câu chốt thay vì đoán.**
- Gọi sản phẩm là **ToolQL** (tên cũ QLTK). Đây là bản viết lại; bản cũ (repo riêng tư `quyenlei/Dragonboy`: client Java + JavaHost + WPF) chỉ còn là nguồn tham chiếu và **không đọc được** nếu chủ repo không clone về máy.
- Báo cáo trung thực: test fail thì nói, bước bỏ qua thì nói, phần chưa kiểm chứng thì ghi rõ.

## Dự án là gì

Cửa sổ quản lý WPF (`src/ToolQL.App`) giữ danh sách acc; mỗi acc bấm **Login** thì ToolQL mở **một cửa sổ client riêng** (jar game `Dragonboy250 v3.5.jar` chạy bằng trình J2ME `AngelAChip.jar`), **tự đăng nhập acc đó vào game**, và có Xếp cửa sổ, Ẩn client, Hiện, Auto login, bảo trì định kỳ, tắt ToolQL thì mọi client tắt theo. Bản cũ bị bỏ vì lag và gói quá lớn (zip 101 MB).

## Hướng đi hiện tại (chủ repo chốt 2026-10-31, **thay quyết định 2026-10-27**)

Quyết định 2026-10-27 ("lõi một tiến trình, không cửa sổ, tự vẽ bằng SkiaSharp, chỉ vẽ acc đang xem") **bị thay thế**: đó là hiểu sai yêu cầu. Phần đã làm theo hướng cũ (`ToolQL.Protocol`, `ToolQL.Render`, `GameState`, `SessionHub`, `src/ToolQL.App/_cu/`) vẫn nằm trong repo làm tham chiếu, **không nằm trên đường chính**, không xóa.

- Mỗi acc một **cửa sổ client riêng** (không nhúng tab), như Tool 2.
- Client là **chính jar** `Dragonboy250 v3.5.jar` (MIDlet J2ME, lớp `nro.hero69`), chạy bằng **`AngelAChip.jar`** (MicroEmulator 2.0.4 bản chỉnh do chủ repo cung cấp). **Không** dùng `ModDP250.exe` (đã rút lại), không dùng FreeJ2ME-Plus (đã so sánh, nặng hơn).
- Bản đầu (không có auto chơi game): bảng acc (mật khẩu mã hóa DPAPI), Login/Đóng, Login tất cả, Tắt tất cả, Tick, Auto login, Xếp cửa sổ, Ẩn client, SHOW ALL, Hiện, thêm nhiều tài khoản, tự đăng nhập, phát hiện mất kết nối, bảo trì định kỳ, đồ họa thấp nhất mặc định, tự đóng popup cập nhật, nhật ký sự kiện.
- Công nghệ: C# (.NET 8), WPF thuần (không thư viện UI nặng). Đóng gói framework-dependent (VPS cài .NET Runtime 8 và Java), không kèm jar game.
- **Rủi ro lớn đã đo:** mỗi cửa sổ client trong game ≈ 99 MB RAM và ≈ 5% một lõi CPU, nên 10 acc ≈ 1 GB RAM: **chưa vừa VPS 1 GB**. Chưa thử trên Windows Server 2012 R2.

## Quy tắc đăng nhập lại (chủ repo, 2026-10-31) — cần giữ đúng

- Sau khi **MẤT KẾT NỐI** (rớt mạng, game đá ra, client sập, kết nối rớt khi cửa sổ còn mở) phải chờ **ít nhất 40 giây** mới đăng nhập lại; sớm hơn thì **server khóa acc**. Rớt liên tiếp thì chờ lâu dần (40, 80, 160... tối đa 5 phút); mất kết nối **3 lần liên tiếp** mà mỗi lần chạy chưa tới 10 phút (ví dụ sai mật khẩu) thì **dừng tự mở lại** và báo, người dùng bấm Login mới thử lại.
- **Đóng tay** (Đóng, Tắt tất cả) rồi mở lại **không phải** mất kết nối: không phải chờ 40 giây. Mở lỗi (thiếu Java...) cũng không chặn người dùng, nhưng Auto login chờ 40 giây giữa các lần thử.
- **Bảo trì hằng ngày** (chủ repo xác nhận 2026-10-31: **3h30 đến 5h** sáng): tới giờ bắt đầu khung thì tự đóng mọi client; suốt khung không tự mở lại; hết khung thì mở lại đúng các acc đã bị đóng. Mặc định 03:30-05:00, chỉnh được trong Cài đặt (giờ Việt Nam). ToolQL **không đọc được thông báo bảo trì trong game**, chỉ theo giờ cài đặt.
- **Login trong ToolQL phải tự đăng nhập acc vào game.** Client chỉ đọc sẵn tài khoản từ RMS rồi hiện nút "Chơi TK" chờ người bấm, nên ToolQL ghi RMS và tự bấm nút đó bằng chuột thật (`PostMessage` vào Java AWT không có tác dụng).
- Mất kết nối khi cửa sổ còn mở được phát hiện từ bảng kết nối TCP của Windows (tiến trình có kết nối tới cổng server game): kết nối từng ổn định >= 20 giây rồi biến mất >= 15 giây. Kết nối ổn định **không** chứng minh đã đăng nhập (client giữ kết nối từ màn hình menu).
- Chủ repo nói acc chưa nạp trong tháng bị game ngắt sau ~10 phút (acc treo thật của họ đã nạp). **Chưa thấy với client thật**: acc thử giữ kết nối liền 15 phút. Hai lần bị ngắt ở 10:20 trước đó chỉ xảy ra với công cụ `Host`/`Probe` (không gửi gói giữ kết nối). Nên chưa có lần mất kết nối thật nào để thử Auto login; phần đó mới kiểm bằng test giả lập và giết tiến trình.
- Mỗi sự kiện (mở acc, bấm "Chơi TK", đóng popup, mất kết nối, bảo trì) được ghi vào `toolql.log` trong thư mục dữ liệu (tên acc che bớt, không có mật khẩu). Đọc nhật ký này trước khi kết luận gì về một đêm treo.
- **Trình chạy là AngelAChip bản gọn** (chủ repo yêu cầu 2026-10-31: xóa hết chỉ chừa "tự động ngủ", luôn bật, không có công tắc). Mã nguồn bản gọn ở `tools/angelachip-slim/` (`Main.java` mới thay `Main` cũ + `Slim.java` cắt các lớp giao diện thừa); jar kết quả để NGOÀI repo (`C:\Dev\ToolQL-input\AngelAChip-slim.jar`), không commit. Không còn menu/thanh trạng thái nên vùng client là đúng **240×320** (bản gốc 240×365 có 22 px menu ở trên): mọi tọa độ trong ToolQL (nút "Chơi TK", popup cập nhật, nút "Tải dữ liệu") tính cho khung 240×320, **không dùng được với AngelAChip gốc**. Đã thử đăng nhập thật bằng bản gọn (2026-10-31, một acc thử, Vũ trụ 1): tự bấm "Chơi TK" lúc +13 giây, vào game, popup cập nhật tự đóng; trong game ~34 MB working set (sau khi ToolQL ép RAM mỗi 30 giây), 73 MB commit, CPU ~5%. Dữ liệu game tự tải trong ~17 giây (148 tệp, 773 KB). Chưa thấy màn hình ngủ chạy thật.
- **Dữ liệu game tự tải, mặc định, không có công tắc** (chủ repo yêu cầu): lần đầu chưa có bản mẫu thì ToolQL tự mở một client không đăng nhập, bấm "Tải dữ liệu" bằng chuột thật rồi đóng (`TemplateBuilder`, quyết định xong bằng `DownloadProgress`). Menu "Tải lại dữ liệu game…" chỉ để tải lại khi game cập nhật.
- **Client luôn mở ở đồ họa thấp nhất** (chủ repo yêu cầu): ToolQL ghi sẵn `lowGraphic=1`, `isPaintAura=0`, `isPaintAura2=0` vào RMS mỗi lần mở (đúng các khóa menu Cấu hình của client ghi). Tắt được trong Cài đặt.
- **Popup "Đã có bản cập nhật mới" (Có/Không) luôn tự đóng, hành vi mặc định của tool, không có công tắc tắt** (chủ repo yêu cầu 2026-10-31): popup hiện ~15 giây sau khi bấm "Chơi TK"; ToolQL chụp cửa sổ client bằng `PrintWindow`, nhận ra (nền trắng + hai nút cam) hai lần liên tiếp rồi bấm "Không" bằng chuột thật. Đã thử với acc thật (nhật ký có đủ các bước).

## Quyết định cũ còn hiệu lực

- Mật khẩu acc lưu bằng DPAPI (`CurrentUser`); chép file sang user/máy khác thì phải nhập lại.
- Máy ảo Windows để Claude build, chạy, kiểm giao diện; đích thật là VPS Windows Server 2012 R2, không nâng cấp Windows.
- Mục tiêu gói: nhỏ hơn 50 MB (mốc tham chiếu: Tool 2 là 42 MB), không kèm JRE. **Chưa đạt/chưa đo** với hướng mới.

## Rule không thương lượng

- **Không viết code đọc, giải hay né captcha.** Gặp captcha thì dừng và báo "Cần captcha". (Tool 2 có tự giải captcha: ToolQL **không** làm.)
- **Không ghi mật khẩu** vào log, file trong repo, commit hay hội thoại. Ngoại lệ không tránh được: client đọc tài khoản/mật khẩu từ kho RMS dạng chữ khi nó đang chạy (`vjacc.rs`, `vjpass.rs`); ToolQL ghi ngay trước khi mở và **xóa khi client dừng**. Không đọc `data.ini`, `keyAPI.ini`, `Login\*` của `Mod DP`, `system\accounts.json` của Tool 2, hay `vjacc.rs`/`vjpass.rs` (đã chặn trong `.claude/settings.json`).
- **Tài khoản thử:** tên/mật khẩu nằm ở `%USERPROFILE%\.config\toolql\test-account.env` (ngoài repo). Dùng `probe import-test-account <thư mục dữ liệu>` để nạp vào kho acc mà không in mật khẩu. **Báo chủ repo trước từng lượt đăng nhập thật** (kể cả qua giao diện ToolQL), mỗi lần một acc, server từ chối thì dừng không thử lại, không thử sai mật khẩu. Mở client đã tự nối server (gói `-27`) dù chưa đăng nhập: nhắc khi liên quan. Tự động hóa trên server chính thức có thể bị khóa acc: nhắc khi liên quan. Muốn thử mở/xếp/ẩn nhiều cửa sổ mà không đăng nhập: chạy `ToolQL.exe --khong-dang-nhap` với thư mục dữ liệu tạm (`--thu-muc-du-lieu <thư mục>`).
- **ToolCikey/Tool 2** là sản phẩm thương mại: chỉ đọc `HUONG-DAN-SU-DUNG.txt` và các `ID-*.txt`; **không chạy, không decompile, không chép binary/asset**.
- **Không chạy `ModDP250.exe`** (chủ repo đã rút lại). Tệp lạ chủ repo đưa (`AngelAChip.jar`...): xem tĩnh trước, mỗi thứ tải về/giải nén để trong thư mục riêng ngoài repo, không chạy trong thư mục đó.
- Mã/tài nguyên từ bên ngoài: chủ repo cho phép đọc và decompile những gì họ cung cấp, họ chịu trách nhiệm bản quyền; ghi nguồn trong commit; file tải về để ngoài repo; không commit `.jar`. `AngelAChip.jar` chưa rõ nguồn gốc và giấy phép (quét tĩnh tầng ngoài không thấy gì bất thường, **không chứng minh an toàn**). FreeJ2ME-Plus là GPLv3 (nếu kèm trong gói phải theo giấy phép).
- Hành động ra ngoài (push `master`, tạo release, đổi chế độ công khai, gửi dữ liệu ra dịch vụ ngoài, gửi tệp cho Microsoft báo nhầm) phải hỏi trước. **Giấy phép client/tài nguyên chưa rõ**: hỏi trước khi push công khai.
- **Windows Defender** từng báo nhầm `ToolQL.Render.dll` (`Trojan:MSIL/AsyncRat.ASY!MTB`, heuristic) vì bộ giải XOR dùng `static readonly byte[]`; đã đổi sang `ReadOnlySpan<byte>`. **Không tự thêm ngoại lệ hay tắt bảo vệ**: hỏi chủ repo.
- Số phiên bản phát hành: chỉ tăng một bước khi chủ repo yêu cầu bản để đăng (bài học từ repo cũ: nút cập nhật cần tag lớn hơn bản đang chạy). Hiện hiển thị 0.1.0.

## Quy trình

- Việc lớn: `superpowers:brainstorming` → spec (`docs/superpowers/specs/`) → `superpowers:writing-plans` → thực hiện. Plan hiện tại: `docs/superpowers/plans/2026-10-31-quan-ly-cua-so-client.md`.
- TDD cho phần hàm thuần (Core). Chỉ báo "xong" khi đã chạy lệnh kiểm tra và đọc kết quả; nói rõ phần **chưa kiểm chứng**. Phần giao diện kiểm bằng cách chạy thật và chụp màn hình/UI Automation.
- Commit nhỏ, thông điệp tiếng Việt (`feat(core): ...`, `fix(ui): ...`). Không commit `bin/`, `obj/`, `*.jar`, `jre/`. (Repo git khởi tạo 2026-10-31, nhánh `master`, chưa có remote.)
- Build: `dotnet build ToolQL.sln --nologo -v q -nodeReuse:false`, ghi kết quả ra tệp rồi đọc (đừng nối ống `| grep` ngay sau build/test: các nút MSBuild giữ ống mở nên có thể treo); `dotnet test` **không** nhận `--no-incremental`.
- Không dùng `perl`/`sed` để chèn văn bản có dấu `\` (đường dẫn Windows): chúng làm hỏng tệp. Dùng công cụ Edit/Write.
- Vòng đọc/ghi: mỗi client là một tiến trình riêng nên không còn luồng đọc socket trong ToolQL; `ClientHub` chỉ gọi từ luồng giao diện (bộ đếm giờ 1 giây).

## Việc tiếp theo (cập nhật 2026-10-31)

1. (Đã xong) Cửa sổ quản lý, mở/xếp/ẩn/hiện, Auto login, phát hiện mất kết nối, bảo trì, tự đăng nhập, đồ họa thấp, tự đóng popup cập nhật, nhật ký sự kiện. Xem plan.
2. Giảm RAM mỗi cửa sổ: đã làm (2026-10-31) cờ JVM gọn hơn (~-3 MB private) và cứ 30 giây ToolQL trả RAM ít dùng của client về Windows (`EmptyWorkingSet`): đo ở màn hình menu, working set ~95 MB -> ~12-18 MB. **Chưa đo khi đang trong game và nhiều acc**; commit (bộ nhớ riêng) vẫn ~77 MB/client nên 10 acc vẫn cần pagefile/RAM đủ. Còn thử: tắt âm thanh, ẩn cửa sổ thì thôi vẽ.
3. Thử trên Windows Server 2012 R2 (WPF, Java, Job object), đo RAM/CPU thật với nhiều acc; đóng gói.
4. Các tính năng Tool 2 còn thiếu (tab loại auto, proxy, khung giờ riêng từng acc...) khi chủ repo yêu cầu.
5. Chủ repo duyệt spec/plan, chốt giờ bảo trì thật và giấy phép client/tài nguyên.
