# Danh sách server trong client

Nguồn: `al_0` (tên gốc `al`) trong `Dragonboy250 v3.5.jar`, dòng khai báo `T`, `ab`, `ad`, `U`, `V`, `aa`; `dn` có thêm một mục mặc định. Mức tin cậy: **từ-mã**. Chưa thử kết nối.

Định dạng một mục: `Tên:host:cổng:loại:?:?` (loại 0 = server chính, 1 và 2 = Universe/Naga của bản Indonaga; hai trường cuối chưa rõ). Danh sách cách nhau bằng dấu phẩy, kết thúc bằng hai số (`,0,0`) chưa rõ ý nghĩa.

| Tên | Tên miền | IP dự phòng trong client | Cổng |
|---|---|---|---|
| Vũ trụ 1 | dragon1.teamobi.com | 112.213.94.23 | 14445 |
| Vũ trụ 2 | dragon2.teamobi.com | 210.211.109.199 | 14445 |
| Vũ trụ 3 | dragon3.teamobi.com | 112.213.85.88 | 14445 |
| Vũ trụ 4 | dragon4.teamobi.com | 27.0.12.164 | 14445 |
| Vũ trụ 5 | dragon5.teamobi.com | 27.0.12.16 | 14445 |
| Vũ trụ 6 | dragon6.teamobi.com | 27.0.12.173 | 14445 |
| Vũ trụ 7 | dragon7.teamobi.com | 112.213.94.223 | 14445 |
| Vũ trụ 8 | dragon10.teamobi.com | 27.0.14.66 | 14446 |
| Vũ trụ 9 | dragon10.teamobi.com | 27.0.14.66 | 14447 |
| Vũ trụ 10 | dragon10.teamobi.com | 27.0.14.66 | 14445 |
| Vũ trụ 11 | dragon11.teamobi.com | 112.213.85.35 | 14445 |
| Võ đài liên vũ trụ | dragonwar.teamobi.com | 27.0.12.173 | 20000 |
| Universe 1 | dragon.indonaga.com | 52.74.230.22 | 14445 |
| Naga | dragon.indonaga.com | 52.74.230.22 | 14446 |

- Máy chủ mặc định cứng trong `hero69.Q` = `112.213.94.23`.
- Địa chỉ trong client có thể đã cũ (jar không rõ ngày). **Chưa kiểm tra** tên miền còn phân giải hay không.
- Dòng `TOOLQL_TEST_SERVER` trong file acc thử nên dùng đúng tên ở cột đầu (ví dụ `Vũ trụ 1`) hoặc tên miền; chuẩn hóa khi viết mã.
