# Giai Đoạn 1: Tiền Sản Xuất (Pre-Production)

> **Nhân sự phụ trách (Agent)**: Architect + Game Designer  
> **Kết quả đầu ra (Output)**: File ProjectConfig.yaml hoàn chỉnh, GDD được chốt  

## Mục Tiêu
Đưa ra toàn bộ các quyết định kỹ thuật và thiết kế trước khi bắt tay vào viết code.

## Các Hoạt Động Chính

### 1. Bảng Câu Hỏi Tiền Sản Xuất
Agent Architect sẽ dẫn dắt em đi qua một bảng câu hỏi toàn diện:

> **Mẹo — Đầu vào tốt = Đầu ra xuất sắc**: Với bất kỳ quyết định mở nào (tầm nhìn, cảm giác người chơi, game tham chiếu), hãy sử dụng khung **TCREI** khi trò chuyện cùng chị:
> - **T** — Task: Chính xác thì em cần đưa ra quyết định gì?
> - **C** — Context: Các ràng buộc hiện tại, nền tảng, quy mô, các quyết định trước đó.
> - **R** — References: Ví dụ, tựa game, công cụ truyền cảm hứng cho em.
> - **E** — Evaluate: Sau phản hồi đầu tiên của chị, hãy cho chị biết điểm nào đúng/sai/còn thiếu.
> - **I** — Iterate: Tinh chỉnh cho đến khi khớp hoàn hảo với tầm nhìn của em.
>
> Nếu kết quả vẫn chưa ưng ý sau một lượt, áp dụng **RSTI**: chia nhỏ yêu cầu thành các gạch đầu dòng, diễn đạt lại mục tiêu hoặc thêm một ràng buộc cứng (ví dụ: *"chỉ đưa ra đúng 3 phương án lựa chọn"*).

| Câu hỏi | Lựa chọn quyết định | Trường trong ProjectConfig |
|---|---|---|
| Chế độ dev? | Assistant / Mix / Automatic | `ai_mode` |
| Phiên bản Unity? | 6.2.0f1 | `unity_version` |
| Render pipeline? | URP / HDRP | `render_pipeline` |
| Hệ thống UI? | UIToolkit / UGUI | `ui_system` |
| Kết nối mạng? | None / NGO / Mirror / Photon | `networking` |
| Chiến lược thư mục? | Feature-based / Type-based | `folder_strategy` |
| Mô hình bất đồng bộ? | Awaitable / UniTask | `async_pattern` |
| Nền tảng đích? | PC, Mobile, Console | `target_platforms` |
| Thư viện bên thứ ba? | DOTween, Cinemachine... | `third_party` |
| Quản lý công việc? | Linear / Jira / None | `task_management` |
| Tích hợp MCP? | Unity / GitHub / Linear | `mcp` |

### 1b. Tổng Quan Trực Quan Về Game (Tùy chọn nhưng khuyên dùng)
Sau khi trả lời bảng câu hỏi, hãy cân nhắc phác thảo nhanh một cái nhìn tổng thể bằng hình ảnh:

> *"Em có muốn phác thảo tổng quan về game trước khi tụi mình bắt tay vào code không? Một sơ đồ luồng màn hình đơn giản (tất cả các màn hình + cách kết nối với nhau) giúp hai chị em mình phát hiện sớm nguy cơ phình to phạm vi (scope creep) và tạo ra một mốc tham chiếu hình ảnh chung trong suốt quá trình sản xuất."*
>
> **Gợi ý công cụ**: Miro (tốt nhất để chèn link vào GDD), Excalidraw (nhanh nhất, chạy offline), hoặc Figma Community (chính xác nhất cho màn hình UI).
> **Kết quả**: Một sơ đồ luồng màn hình thô được lưu link tại `GDD.md → Overview`. Không bắt buộc — có thể bỏ qua nếu quy mô game nhỏ gọn.

### 2. Hoàn Thiện ProjectConfig
Điền toàn bộ các quyết định vào file `ProjectConfig.yaml`.

### 3. Chốt Tài Liệu GDD
Hoàn thành các phần còn lại của GDD:
- Bảng dữ liệu thực thể và cân bằng game (Entity & Balance Data)
- Ánh xạ phím điều khiển (Input Mapping)
- Cấu trúc màn chơi / Thế giới
- Hệ thống tiến trình nâng cấp (Progression system)

### 4. Thiết Lập MCP
Cấu hình các tích hợp MCP đã lựa chọn:
- Unity MCP — cài đặt vào dự án Unity
- GitHub MCP — cấu hình trong IDE
- Linear / Notion — kết nối tài khoản

## Tiêu Chí Đầu Vào
- Giai đoạn 0 đã hoàn thành (GDD + GFD đã có bản thảo).

## Tiêu Chí Đầu Ra
- [ ] ProjectConfig.yaml đã được điền đầy đủ 100%
- [ ] GDD hoàn thiện và đã được duyệt
- [ ] Ma Trận Phản Hồi trong GFD hoàn chỉnh
- [ ] Các kết nối MCP đã được cấu hình
- [ ] Quy ước đặt tên đã được thống nhất
