# Dựng máy ảo Windows trên Ubuntu (chủ repo làm, Claude không làm được)

Mục tiêu: một máy ảo Windows **nhẹ** để Claude Code build, chạy và kiểm tra giao diện ToolQL. Máy ảo **không** thay thế VPS (Server 2012 R2); số đo RAM/CPU ở đây chỉ là xu hướng.

## 1. Chọn bản Windows

Căn cứ đã xác minh (tháng 10/2026):
- Claude Code cần **Windows 10 bản 1809 trở lên hoặc Windows Server 2019 trở lên**, RAM từ 4 GB. Cài thẳng (native), không cần Administrator. (Nguồn: code.claude.com/docs/en/setup)
- .NET 8 hỗ trợ trên Windows 10 (**chỉ LTSC/Enterprise**), Windows 11, và mọi Windows Server từ 2012 đến 2025. Windows 8.1 không còn được hỗ trợ. (Nguồn: learn.microsoft.com/dotnet/core/install/windows)
- Windows 10 Enterprise LTSC 2021 được hỗ trợ đến **13/01/2027** (nguồn: Microsoft Lifecycle). Đủ cho máy phát triển; bản 2019 (1809) thì lâu hơn (theo trí nhớ, chưa tra: đến 2029).

| Lựa chọn | Nhẹ | Ghi chú |
|---|---|---|
| **Windows 10 Enterprise LTSC 2021 (khuyên dùng)** | Rất nhẹ: không Store, không ứng dụng rác | Không có winget sẵn → script dùng tải trực tiếp (đã có sẵn đường dự phòng). Tải bản đánh giá (Evaluation) từ Microsoft Evaluation Center; **chưa xác nhận được trang hiện hành**, chủ repo tự tìm "Windows 10 Enterprise LTSC 2021 evaluation". Bản đánh giá dùng được ~90 ngày, sau đó cần cài lại hoặc có giấy phép. |
| Windows Server 2019/2022 (Desktop Experience), bản đánh giá | Nhẹ, gần VPS về dòng Server | Claude Code hỗ trợ (Server 2019+). Phiền hơn: cấu hình bảo mật trình duyệt, không có winget. |
| Windows 10/11 Pro/Home thường | Nặng hơn (ứng dụng đi kèm, cập nhật nền) | Có winget. Windows 10 thường đã hết hỗ trợ; Windows 11 yêu cầu TPM/Secure Boot, dễ chậm trong Boxes (có báo cáo chạy rất chậm). **Không khuyên.** |
| Bản Windows "rút gọn" không chính thức (Tiny10, v.v.) | Nhẹ nhất | **Không dùng**: nguồn không rõ, dễ hỏng cập nhật và công cụ. |

**Kết luận: Windows 10 Enterprise LTSC 2021 (64-bit, tiếng Anh hoặc tiếng Việt đều được).** Nếu không tải được bản đánh giá, phương án dự phòng là Windows Server 2019/2022 bản đánh giá.

## 2. Cấu hình máy ảo (máy Ubuntu này: 14 GB RAM, 16 luồng CPU, có KVM)

| Thông số | Đề xuất | Tối thiểu |
|---|---|---|
| RAM | **6 GB** | 4 GB (Claude Code yêu cầu từ 4 GB; thêm Recaf, `dotnet build` sẽ sát) |
| CPU | **4 vCPU** | 2 |
| Đĩa | **60 GB** (cấp phát động) | 40 GB |
| Mạng | NAT mặc định | |

Để lại cho Ubuntu ít nhất 6-8 GB RAM.

## 3. Cài bằng GNOME Boxes (đã cài sẵn tại `/usr/bin/gnome-boxes`)

1. Mở Boxes → Tạo máy ảo → chọn file ISO (cài tuần tự, **không** dùng "express install" nếu nó đòi khóa sản phẩm).
2. Trước khi cài, vào Tùy chọn của máy ảo, đặt RAM/CPU/đĩa theo bảng trên.
3. Cài Windows xong, **cài SPICE guest tools và driver VirtIO** trong Windows (Boxes không tự cài; bản Boxes gợi ý có thể cũ, nên lấy `virtio-win-gt` mới từ trang VirtIO của Fedora, theo hướng dẫn trên ctrl.blog "how to win10 in gnome boxes"). Có driver mới có chuột liền mạch, clipboard, đổi độ phân giải và đĩa/mạng nhanh hơn.
4. Lưu ý: Boxes **không có tăng tốc 3D**. Giao diện WPF vẫn chạy được; thử nghiệm đồ họa nặng (vẽ SkiaSharp tốc độ cao) trong máy ảo sẽ chậm hơn máy thật, nên đừng dùng nó để kết luận về FPS.
5. Nếu Boxes quá chậm: dùng `virt-manager` (chưa cài; `sudo apt install virt-manager`) với card QXL/SPICE, hoặc bật các tùy chọn VirtIO (chưa kiểm chứng trên máy này).
6. **Chụp snapshot** ngay sau khi cài Windows + driver sạch (Boxes: menu máy ảo → Snapshots). Dùng để quay lại khi hỏng.

## 4. Chia sẻ file Ubuntu ⇄ Windows

- Cách đơn giản nhất: kéo-thả/clipboard của SPICE (sau khi cài guest tools), hoặc thư mục dùng chung của Boxes.
- Với repo: cách sạch nhất là đẩy repo lên GitHub (riêng tư) rồi `git clone` trong máy ảo, thay vì chép tay. Chỉ cần hỏi chủ repo trước khi đẩy.
- **Không** chép file acc thử qua thư mục dùng chung nếu thư mục đó được đồng bộ ra ngoài; gõ tay hoặc tạo trong máy ảo.

## 5. Trong máy ảo, làm lần lượt

1. Cập nhật Windows (nếu có), cài driver/guest tools, tắt chế độ ngủ màn hình.
2. Cài **Git for Windows** và **Claude Code** (xem `docs/dev-setup.md` mục 2).
3. Chép/clone repo vào `C:\Dev\ToolQL`.
4. Chạy `scripts\setup-dev.ps1` (xem `docs/dev-setup.md`).
