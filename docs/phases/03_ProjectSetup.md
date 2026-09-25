# Giai Đoạn 3: Khởi Tạo Dự Án (Project Setup)

> **Nhân sự phụ trách (Agent)**: Architect  
> **Lệnh quy trình (Workflow)**: `/setup-project`  
> **Kỹ năng sử dụng (Skill)**: `unity-project-setup`  

## Mục Tiêu
Dựng khung xương hoàn chỉnh cho dự án Unity với đầy đủ mọi hạ tầng kỹ thuật sẵn sàng.

## Các Hoạt Động Chính

### 0. Lựa Chọn Chế Độ Phát Triển (Dev Mode)
Chạy lệnh `/setup-project` — quy trình bắt đầu bằng việc hỏi em muốn làm theo chế độ nào (Assistant / Mix / Automatic). Nếu chọn Automatic, AI sẽ phỏng vấn đầu vào để có thể tự động triển khai. Xem chi tiết tại Bước 0 của [setup-project.md](../../.agent/workflows/setup-project.md).

### 1. Cấu Trúc Thư Mục
Tạo thư mục `Assets/_Project/` với đầy đủ các thư mục con dựa theo `folder_strategy`.

### 2. Assembly Definitions
Tạo các file `.asmdef` khớp hoàn toàn với sơ đồ Assembly trong TDD.

### 3. Cài Đặt Package
Cài đặt tất cả các package được liệt kê trong `ProjectConfig.yaml → packages`.

### 4. Các Tiện Ích Cốt Lõi
- Sinh lớp bao bọc `GameDebug.cs` (thông qua kỹ năng `unity-debugging`).
- Thêm cờ `ENABLE_LOGS` vào Scripting Define Symbols của bản build Development.
- Tạo các lớp cơ sở (base classes) cho ScriptableObject.

### 5. Cấu Hình Git
Thông qua GitHub MCP:
- Tạo file `.gitignore` chuẩn Unity.
- Tạo nhánh `develop`.
- Thiết lập chiến lược phân nhánh.
- Thực hiện commit khởi tạo.

### 6. Cấu Hình Editor
Thông qua Unity MCP hoặc thiết lập trực tiếp:
- Thiết lập Render pipeline.
- Cấu hình chất lượng đồ họa theo từng nền tảng.
- Không gian màu Linear.
- Cài đặt thông số vật lý (Physics settings).

### 7. Thiết Lập Tài Liệu
- Sao chép các file mẫu từ `templates/` sang `docs/`.
- Thiết lập bảng theo dõi sprint.
- Nếu `prompt_logging: true` trong `ProjectConfig.yaml`, tạo file `docs/PromptLog.md`:

```markdown
# Nhật Ký Câu Lệnh (Prompt Log)

> Tự động tạo bởi quy trình. Mỗi mục lưu lại câu lệnh của em và phần tóm tắt dưới 200 từ về những gì chị đã làm.
```

## Tiêu Chí Đầu Vào
- Giai đoạn 2 đã hoàn thành (TDD đã được phê duyệt).
- Dự án Unity đã tạo và đang mở trong Unity Editor.

## Tiêu Chí Đầu Ra
- [ ] Cấu trúc thư mục đã được tạo
- [ ] Tất cả các file asmdef đã tạo và biên dịch thành công
- [ ] Tất cả các package cần thiết đã được cài đặt
- [ ] File GameDebug.cs đã được sinh tự động
- [ ] Git đã được khởi tạo kèm file .gitignore chuẩn
- [ ] Cài đặt Editor đã được cấu hình xong
- [ ] Tài liệu mẫu docs/ đã sẵn sàng
