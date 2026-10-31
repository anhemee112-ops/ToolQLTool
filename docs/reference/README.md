# Tài liệu tham chiếu từ repo cũ

Repo cũ: `github.com/quyenlei/Dragonboy` (nhánh `feature/idle-host`). Chưa chép gì vào đây; chép khi cần và ghi nguồn trong commit.

| Cần | Nằm ở repo cũ | Ghi chú |
|---|---|---|
| Giao thức đăng nhập đã kiểm chứng (khung gói, khóa XOR, `-111`, `-74`, `-29`, `-77`, `-28`...) | `docs/superpowers/specs/` và `IdleHost/IdleHost.Core` (`FrameCodec`, `GameSession`, `LoginMessages`) | Đăng nhập thật đã thành công bằng bản này |
| Logic auto (Xmap, Train, Bò Mộng, hồng ngọc) | `JavaClient/src/nro/auto/` (Java, hàm thuần có test) | Port sang C# khi đến lượt |
| Đồ thị map | `JavaClient/res/xmap-*.txt` | |
| Bảng ID (item, map, npc, quái, skill) | **Đã có bản mới ở `docs/data/*.json`** (xuất từ duncannro.id.vn/check-id, 2026-10-27) | Bảng cũ `docs/data/ID-*.txt` của repo cũ (nguồn ToolCikey) chỉ còn để đối chiếu; số dòng khác nhau, chưa so sánh |
| Danh sách server | `scripts/Servers.txt`, `ServerCatalog` | `dragon1..7`, `dragon11` cổng 14445... |
| Quy tắc/bài học | `CLAUDE.md` của repo cũ | Nhiều mục đã gộp vào `CLAUDE.md` mới |

Bài học chính: các auto cũ phần lớn **chưa kiểm chứng trên server thật**; đừng coi là đã chạy đúng.
