# Dựng môi trường phát triển ToolQL (đọc đầu tiên)

Dành cho Claude Code chạy **trong máy ảo Windows**. Làm theo thứ tự; bước nào cần người làm tay thì ghi "CẦN CHỦ REPO".
Máy đích cuối cùng là **VPS Windows Server 2012 R2 (1 GB RAM, 1 CPU)**, không phải máy ảo. Máy ảo chỉ để phát triển và đo xu hướng.

Nhãn độ tin cậy dùng trong tài liệu: **[đã xác minh]** = đã đối chiếu tài liệu chính thức ngày 2026-10-27; **[chưa xác minh]** = viết theo hiểu biết, phải kiểm tra khi chạy.

## 0. Việc CẦN CHỦ REPO làm trước

1. Dựng máy ảo Windows theo `docs/vm-setup.md` (khuyên dùng **Windows 10 Enterprise LTSC 2021**, 6 GB RAM, 4 vCPU, 60 GB đĩa).
2. Cài **Git for Windows** và **Claude Code** vào máy ảo (mục 2), đăng nhập Claude Code.
3. Đưa repo này vào `C:\Dev\ToolQL` (khuyên: `git clone`; hoặc chép tay).
4. Chép client cần phân tích (file `.jar`) vào `C:\Dev\ToolQL-input\` (thư mục **ngoài repo**, không commit).
5. (Tùy chọn) Tài khoản thử: tạo `%USERPROFILE%\.config\toolql\test-account.env` gồm ba dòng `TOOLQL_TEST_USER=...`, `TOOLQL_TEST_PASS=...` và `TOOLQL_TEST_SERVER=...` (tên server của acc, ví dụ `dragon1`; danh sách server cũ ở `docs/reference/README.md`). Không đưa vào repo, log hay tài liệu.

## 1. Chạy script kiểm tra và cài công cụ

```powershell
# Từ thư mục gốc repo
powershell -ExecutionPolicy Bypass -File scripts\setup-dev.ps1 -CheckOnly   # chỉ kiểm tra
powershell -ExecutionPolicy Bypass -File scripts\setup-dev.ps1               # kiểm tra và cài thứ thiếu
```

Script in bảng `OK / THIẾU / ĐÃ CÀI / LỖI`. Nó không sửa repo, không đọc file acc thử. Nếu máy không có `winget` (LTSC, Server) nó tải trực tiếp từ nguồn chính thức và cài vào thư mục người dùng (`C:\Dev\Tools`, `%LOCALAPPDATA%`), không cần Administrator. Sau khi cài, **mở terminal mới** rồi chạy lại để xác nhận.

## 2. Công cụ cần có

| Công cụ | Dùng để | Cài bằng | Trạng thái xác minh |
|---|---|---|---|
| **Claude Code** | chạy Claude trong máy ảo | `irm https://claude.ai/install.ps1 \| iex` (PowerShell), hoặc `winget install Anthropic.ClaudeCode`; sau đó `claude --version` và `claude` để đăng nhập | **[đã xác minh]** (code.claude.com/docs/en/setup). Yêu cầu Windows 10 1809+ hoặc Server 2019+, RAM từ 4 GB; không cần Administrator. WinGet không tự cập nhật; bản cài native tự cập nhật |
| **Git for Windows** | Claude Code dùng Git Bash cho công cụ Bash; quản lý mã | `winget install Git.Git` hoặc bộ cài từ git-scm.com/downloads/win | Cần là **[đã xác minh: tùy chọn nhưng khuyến nghị]**; không có thì Claude Code dùng PowerShell. ID `Git.Git` **[chưa xác minh]** |
| **.NET 8 SDK** | build/test lõi và giao diện | `winget install Microsoft.DotNet.SDK.8` hoặc `dotnet-install.ps1 -Channel 8.0` | **[đã xác minh]** ID winget và script (learn.microsoft.com/dotnet/core/install/windows) |
| **JDK 22 trở lên** (khuyên **Temurin 25 LTS**) | chạy Recaf | `winget install EclipseAdoptium.Temurin.25.JDK`; không có winget thì script tải zip từ Adoptium | Yêu cầu Java 22+ **[đã xác minh]** (hướng dẫn chạy tay của Recaf: "Download Java 22 or higher"; launcher tự nó chạy với Java 8+). **Java 21 KHÔNG đủ** cho Recaf (bản nháp trước của mình ghi sai 21). Thư mục `Temurin/25/JDK` có trong winget-pkgs **[đã xác minh]**, nhưng việc Recaf chạy tốt trên Java 25 và URL Adoptium API trong script là **[chưa xác minh]** → dùng lệnh `compatibility` của launcher, nếu lỗi thì thử Temurin 22 hoặc 24 |
| **Recaf Launcher** | mở jar, xem mã dịch ngược, đổi tên lớp | tải `.jar` từ `github.com/Col-E/Recaf-Launcher/releases` vào `C:\Dev\Tools\Recaf`; chạy `java -jar <tên>.jar` | Cách dùng **[đã xác minh]** (recaf.coley.software/user/install/via-launcher.html): lần đầu hỏi chế độ chạy, tự dò Java, tự kéo JavaFX; Recaf lưu ở `%APPDATA%\Recaf` (đổi bằng biến môi trường `RECAF`). Tên file cụ thể của bản phát hành **[chưa xác minh]** |
| VC++ 2015-2022 x64 Redistributable | .NET trên Windows cũ | `winget install Microsoft.VCRedist.2015+.x64` hoặc `https://aka.ms/vs/17/release/vc_redist.x64.exe` | **Bắt buộc trên VPS 2012 R2** (mục 6). Thư mục winget `Microsoft/VCRedist/2015+/x64` có thật **[đã xác minh]** |
| 7-Zip, VS Code | tùy chọn | `7zip.7zip`, `Microsoft.VisualStudioCode` | |

### Cài Recaf thủ công (nếu script lỗi)

1. Cài JDK 22+ (Temurin), mở terminal mới, chạy `java -version` phải thấy 22 trở lên.
2. Tải launcher `.jar` từ trang releases ở trên, đặt vào `C:\Dev\Tools\Recaf\`.
3. `java -jar C:\Dev\Tools\Recaf\<tên-launcher>.jar`, chọn JDK vừa cài khi launcher hỏi, để launcher kéo JavaFX và Recaf.
4. Hoặc chạy tay không qua launcher: tải `recaf.jar` và 4 gói JavaFX (base, graphics, controls, media) vào thư mục `dependencies` cạnh nó, rồi `java -cp "recaf.jar;dependencies/*" software.coley.recaf.Main` (trên Windows dấu `;`).
5. Launcher có lệnh CLI `compatibility` để kiểm tra môi trường **[đã xác minh]**.
6. Mở thẳng một jar: `run` của launcher không nhận tham số, nên chạy tay `java -cp "recaf.jar;dependencies/*" software.coley.recaf.Main -i <jar>` trong `%APPDATA%\Recaf`. Đã thử trên Java 25 và chạy được; chi tiết trong `docs/setup-log.md`.

## 3. Kiểm tra môi trường đã đủ

```powershell
claude --version
git --version
dotnet --list-sdks          # phải có 8.0.x
java -version               # phải 22 trở lên
```

Khi dự án đã có mã:

```powershell
dotnet build ToolQL.sln -c Release
dotnet test  ToolQL.sln
```

## 4. Quy tắc môi trường

- **Thư mục tải về/giải nén không tin cậy** (jar, mã dịch ngược): để ngoài repo, mỗi thứ một thư mục riêng, đừng chạy công cụ ngay trong thư mục đó. Không commit `.jar`, `jre/`, `bin/`, `obj/`.
- **Lõi nhắm `net8.0` thuần** (không `net8.0-windows`, không API chỉ có từ Windows 10) để chạy được trên Server 2012 R2. Chỉ lớp giao diện được dùng `net8.0-windows`.
- **Đo tài nguyên** ở máy ảo chỉ là xu hướng (Boxes không có tăng tốc 3D). Số liệu chốt phải đo trên VPS thật.
- Windows 11 có **Smart App Control**: nếu bật sẽ làm `dotnet build` chậm; nên tắt trên máy dev **[đã xác minh]** (trang cài .NET).
- Claude Code native trên Windows **không có sandbox** **[đã xác minh]**; đó là lý do repo có quy tắc hỏi trước khi chạy lệnh nguy hiểm.
- Trước khi đăng nhập server thật: báo chủ repo, mỗi lần một acc, bị từ chối thì dừng, không thử lại.

## 5. Sau khi setup xong, làm gì tiếp

Đọc `CLAUDE.md` rồi `docs/decisions.md`. Bước tiếp theo là **khảo sát client bằng Recaf** (chưa viết mã sản phẩm): ghi giao thức và định dạng tài nguyên vào `docs/protocol/`, rồi cùng chủ repo chốt phạm vi bản đầu.

## 6. Điều kiện để chạy trên VPS Server 2012 R2 (không phải máy ảo)

- **.NET 8 hỗ trợ Server 2012 và 2012 R2 (x64)** **[đã xác minh]**, nhưng cần cài **Microsoft Visual C++ 2015-2019 Redistributable x64** trước. Thiếu thì lỗi `hostfxr.dll` / `api-ms-win-crt-runtime-l1-1-0.dll` / `api-ms-win-cor-timezone-l1-1-0.dll`. Phải đưa bước này vào hướng dẫn người dùng, hoặc đóng kèm bộ cài.
- Vì vậy khi chọn cách đóng gói (xem `docs/decisions.md`) phải tính tới: **framework-dependent** (nhỏ, nhưng phải cài .NET Runtime 8 trên VPS) hay **self-contained** (không cần cài, nhưng lớn hơn).
- Windows 8.1 và Windows 7 không còn được .NET 8 hỗ trợ; VPS là Server 2012 R2 nên vẫn ổn.
- Mọi API chỉ có từ Windows 10 (ví dụ `SetThreadDescription`, từng làm Unity không chạy được trên VPS) phải tránh trong lõi.

## 7. Chạy ToolQL (cửa sổ quản lý) trên máy ảo hoặc VPS (cập nhật 2026-10-31)

ToolQL mở mỗi acc thành một cửa sổ client, nên máy chạy cần thêm (ngoài .NET Runtime 8, mục 6):

- **Java** (`javaw.exe`): đã thử với JDK 25 (Temurin) trên Windows 10. **Chưa thử** trên Server 2012 R2: JDK mới có thể không hỗ trợ hệ điều hành đó; có thể phải dùng JRE 8/11. Chọn trong Cài đặt → Cài đặt chung (có nút "Tự tìm").
- **AngelAChip bản gọn** (`AngelAChip-slim.jar`) và **`Dragonboy250 v3.5.jar`** (jar game): để ngoài repo, trỏ tới trong Cài đặt chung. Không kèm trong repo hay gói phát hành. Bản gọn dựng từ `AngelAChip.jar` gốc (chủ repo cung cấp) bằng `tools/angelachip-slim/`: JDK 25 (cần `javac` và `java` có `java.lang.classfile`), rồi
  `javac --release 8 -cp <AngelAChip.jar gốc> -d <thư mục lớp> tools\angelachip-slim\src\org\microemu\app\Main.java` và
  `java tools\angelachip-slim\Slim.java <AngelAChip.jar gốc> <thư mục lớp> <AngelAChip-slim.jar>`. Bản gọn không còn menu/thanh trạng thái (vùng client đúng 240×320); chỉ giữ tính năng tự động ngủ (luôn bật). ToolQL **chỉ đúng với bản gọn** (tọa độ bấm tính cho khung 240×320).
- **Dữ liệu game** (tải sẵn cho từng acc): ToolQL **tự tải** lần đầu (không có công tắc): mở một client không đăng nhập, bấm "Tải dữ liệu" bằng chuột thật rồi đóng, mất khoảng 15-20 giây, **đừng đụng chuột** lúc đó. Kết quả nằm ở `%LOCALAPPDATA%\ToolQL\client-template` (~770 KB, sao chép cho từng acc). Bước này kết nối tới server game nhưng **không đăng nhập**. Tải lại khi game cập nhật: Cài đặt → "Tải lại dữ liệu game…".
- Phiên Remote Desktop phải **đang mở** (không thu nhỏ/ngắt): ToolQL tự bấm nút "Chơi TK" bằng chuột thật. Chưa thử khi phiên bị ngắt.
- Thử mà không đăng nhập: `ToolQL.exe --khong-dang-nhap --thu-muc-du-lieu <thư mục tạm>` (không ghi tài khoản/mật khẩu cho client). Nạp acc thử vào kho: `probe import-test-account <thư mục dữ liệu>` (không in mật khẩu).
