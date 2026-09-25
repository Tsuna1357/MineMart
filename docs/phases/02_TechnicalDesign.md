# Giai Đoạn 2: Thiết Kế Kỹ Thuật (Technical Design)

> **Nhân sự phụ trách (Agent)**: Architect  
> **Kết quả đầu ra (Output)**: Tài liệu TDD hoàn chỉnh  

## Mục Tiêu
Định hình toàn bộ kiến trúc kỹ thuật trước khi viết code sản phẩm. Toàn bộ kiến trúc được trực quan hóa bằng **sơ đồ Mermaid** trong TDD — giúp em nhìn thấy rõ kết cấu trước khi viết dù chỉ một dòng code.

> **Công cụ vẽ sơ đồ**: [Mermaid](https://mermaid.js.org) — vẽ bằng văn bản, hiển thị trực tiếp trên GitHub và Obsidian, AI tự sinh mã được.  
> Để phác thảo tự do trước khi chốt, có thể dùng bảng vẽ [Miro](https://miro.com).

## Các Hoạt Động Chính

### 1. Sơ Đồ Cây Assembly Definition
Thiết kế cấu trúc module và sinh sơ đồ **Mermaid `graph TD`**:
- Những assembly nào sẽ tồn tại trong dự án
- Chiều luồng phụ thuộc (luôn hướng vào trong — tính năng → core, tuyệt đối không có chiều ngược lại)
- Thiết lập assembly cho kiểm thử (Test assembly)
- Kết quả: Sơ đồ Mermaid được nhúng trực tiếp vào file TDD

### 2. Các Mẫu Thiết Kế (Design Patterns)
Lựa chọn pattern cho từng hệ thống và vẽ sơ đồ cho các thành phần có trạng thái:
- Máy trạng thái (State machines) cho bộ điều khiển nhân vật/AI → vẽ **Mermaid `stateDiagram-v2`** cho mỗi controller
- Kiến trúc ScriptableObject cho dữ liệu
- Event Channels cho việc giao tiếp xuyên hệ thống
- MVVM cho giao diện người dùng (nếu dùng UI Toolkit)
- Kết quả: Mỗi hệ thống có trạng thái đều có một sơ đồ state diagram trong TDD

### 3. Kiến Trúc Dữ Liệu (Data Architecture)
Xác định cách dữ liệu luân chuyển và lưu trữ lâu dài:
- Danh mục các ScriptableObject
- Chiến lược Lưu / Tải dữ liệu (Save/Load strategy)
- Quản lý cấu hình thông số

### 4. Kiến Trúc Mạng (Nếu có chơi nhiều người)
Dựa trên mục `ProjectConfig.yaml → networking`:
- Ma trận quyền hạn sở hữu (Authority matrix)
- Chiến lược đồng bộ trạng thái (State sync strategy)
- Các mẫu gọi hàm từ xa (RPC patterns)

### 5. Ràng Buộc API
Quy định danh sách API cho phép và cấm:
- Giới hạn theo phiên bản package
- Các luồng xử lý đòi hỏi hiệu năng tối đa
- Tiêu chuẩn thực hành tốt nhất của Unity API

### 6. Mục Tiêu Hiệu Năng (Performance Targets)
Đặt ra các con số mục tiêu đo lường được:
- Tốc độ khung hình (FPS) cho từng nền tảng
- Ngân sách bộ nhớ RAM tối đa
- Giới hạn thời gian tải màn chơi (Load time limits)
- Ngân sách số lượt vẽ đồ họa (Draw calls budget)

## Tiêu Chí Đầu Vào
- Giai đoạn 1 đã hoàn thành (ProjectConfig đã được điền đầy đủ).

## Tiêu Chí Đầu Ra
- [ ] TDD hoàn thiện và được em duyệt
- [ ] Sơ đồ Assembly Definition bằng Mermaid được thông qua
- [ ] Sơ đồ trạng thái (State diagrams) cho các hệ thống (người chơi, AI, Game Manager)
- [ ] Sơ đồ luồng màn hình UI hoàn tất (nếu game nặng về giao diện)
- [ ] Mẫu thiết kế đã được chọn cho mọi hệ thống
- [ ] Danh sách API cho phép/cấm đã được quy định rõ ràng
- [ ] Các chỉ số mục tiêu hiệu năng đã được thiết lập
