# Nhật ký dựng môi trường (máy ảo dev)

Ghi lại những gì đã làm và đã kiểm chứng thật trên máy ảo, ngày **2026-10-28**. Hướng dẫn chung nằm ở `docs/dev-setup.md`; file này là kết quả thực tế.

## Máy ảo

- Windows 10 Enterprise LTSC 2021 Evaluation (10.0.19044), 16 bộ xử lý logic hiển thị trong Java.
- Claude Code 2.1.312, cài ở `C:\Users\dev\.local\bin\claude.exe`.

## Công cụ đã cài và kiểm chứng

| Công cụ | Phiên bản | Vị trí | Kiểm chứng |
|---|---|---|---|
| Git for Windows | 2.56.0 | hệ thống | `git --version` |
| .NET SDK | 8.0.427 | `%LOCALAPPDATA%\Microsoft\dotnet` | `dotnet --list-sdks` |
| JDK Temurin | 25.0.5 LTS | `C:\Dev\Tools\jdk-25` | `java -version` (đã chạy Recaf thật) |
| Recaf Launcher (CLI, GUI) | 0.8.8 | `C:\Dev\Tools\Recaf\` | `compatibility>` không báo lỗi |
| Recaf | 4.0.0-SNAPSHOT (build `d5888cd`) | `%APPDATA%\Recaf\recaf.jar` (42 MB) | mở được jar đầu vào |
| JavaFX | 24.0.2-ea+1 | `%APPDATA%\Recaf\dependencies\` | Recaf lên giao diện không lỗi |

Chưa cài/chưa cần: VC++ 2015-2022 Redistributable (chỉ cần cho VPS 2012 R2). Chưa chạy `scripts\setup-dev.ps1 -CheckOnly` để có bảng xác nhận cuối.

## Đầu vào

- `C:\Dev\ToolQL-input\Dragonboy250 v3.5.jar` (1,5 MB), do chủ repo cung cấp, **ngoài repo**, không commit.

## Recaf: kết quả thử

- **Chạy được trên Java 25** (tài liệu trước ghi "chưa xác minh", nay đã xác minh). Không cần hạ xuống JDK 22.
- Launcher `recaf-cli-0.8.8.jar` chỉ là bộ tải. Các lệnh dùng được: `update-ci` (tải Recaf), `update-jfx` (tải JavaFX), `run`, và `compatibility>` (tên lệnh có dấu `>` thật).
- Lệnh `run` của launcher **không truyền được tham số** (`-i` bị từ chối). Muốn mở thẳng jar phải chạy tay:

```powershell
cd $env:APPDATA\Recaf
C:\Dev\Tools\jdk-25\bin\java -cp "recaf.jar;dependencies/*" software.coley.recaf.Main -i "C:\Dev\ToolQL-input\Dragonboy250 v3.5.jar"
```

- Lần `update-ci` đầu tiên báo lỗi ghi file vì `%APPDATA%\Recaf` chưa tồn tại. Chạy `update-jfx` trước (tạo thư mục), rồi `update-ci` lại thì thành công.
- Log xác nhận: `Reading input from ZIP container 'Dragonboy250 v3.5.jar'`, không có lỗi. **Chưa kiểm chứng bằng mắt** rằng cây lớp hiện đúng trên cửa sổ.
- Cảnh báo `sun.misc.Unsafe` / `Unsupported JavaFX configuration` trong log là vô hại (JavaFX chạy từ classpath).

## Xuất mã dịch ngược (không cần bấm GUI)

`recaf.jar` có sẵn CFR, Vineflower và Procyon. Dùng CFR trực tiếp, xuất ra **ngoài repo** (`C:\Dev\ToolQL-decomp`, ~2 phút 20 giây, 224 file, 3,9 MB):

```powershell
$J = "C:\Dev\Tools\jdk-25\bin"
$jar = "C:\Dev\ToolQL-input\Dragonboy250 v3.5.jar"
& $J\java -cp "$env:APPDATA\Recaf\recaf.jar" org.benf.cfr.reader.Main $jar --outputdir C:\Dev\ToolQL-decomp\cfr --silent true
# giải chuỗi bị mã hóa Y.b("...") -> thư mục cfr-dec
& $J\javac -encoding UTF-8 -d C:\Dev\ToolQL-decomp\tool scripts\survey\DecodeStrings.java scripts\survey\DumpConst.java
& $J\java -cp C:\Dev\ToolQL-decomp\tool DecodeStrings $jar C:\Dev\ToolQL-decomp\cfr C:\Dev\ToolQL-decomp\cfr-dec
# in hằng số tĩnh của lớp (ví dụ khóa, mã lệnh)
& $J\java -cp C:\Dev\ToolQL-decomp\tool DumpConst $jar nro.bV nro.ao
```

- Kết quả giải chuỗi: 4712 chuỗi, 0 lỗi, không còn `Y.b(` sót.
- `DumpConst` chỉ nạp được lớp không phụ thuộc MIDP (`nro.bV`, `nro.ao`, `nro.an`, `nro.bd`, `nro.cN`). Lớp phụ thuộc `javax.microedition.*` (như `nro.cj`, `nro.hero69`) báo `NoClassDefFoundError`.
- Hai công cụ chỉ giải chuỗi tĩnh và đọc hằng số, **không** chạy game, không kết nối mạng.
- Tên file CFR có đuôi `_0` khi trùng tên không phân biệt hoa/thường (`ao` → `ao_0.java`); tên thật trong jar không có đuôi.
- Kết quả khảo sát ghi ở `docs/protocol/` (`transport.md`, `servers.md`, `login.md`, `risks.md`).

## Tài khoản thử

File: `%USERPROFILE%\.config\toolql\test-account.env` (`C:\Users\dev\.config\toolql\test-account.env`). Thư mục `.config` ẩn tên bắt đầu bằng dấu chấm nên Explorer có thể khó thấy.

- Đã tạo file mẫu với ba dòng trống `TOOLQL_TEST_USER=`, `TOOLQL_TEST_PASS=` và `TOOLQL_TEST_SERVER=`. Dòng server được bổ sung sau, vì acc chỉ đăng nhập được vào đúng server của nó (repo cũ có `dragon1..7`, `dragon11`). **Chủ repo tự điền**, không gửi qua hội thoại.
- Cách ghi server (tên như `dragon1` hay địa chỉ + cổng) **chưa chốt**; ghi theo cách bạn quen, sẽ chuẩn hóa khi khảo sát phần chọn server trong client.
- Mở để sửa (PowerShell): `notepad $env:USERPROFILE\.config\toolql\test-account.env`
- Nằm ngoài repo nên không bị commit. Claude không đọc file này trừ khi chủ repo cho phép đăng nhập thật.

## Chưa làm

- Khảo sát client theo `docs/survey-checklist.md`, ghi vào `docs/protocol/`.
- Chốt phạm vi bản đầu và công nghệ giao diện.
- Chưa có solution/mã sản phẩm.
