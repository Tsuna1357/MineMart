# Giai Đoạn 4: Sản Xuất (Production)

> **Nhân sự phụ trách (Agents)**: Tất cả (tùy theo nhiệm vụ)  
> **Quy trình chính (Primary Workflow)**: `/plan` → `/implement-feature` (từng tính năng) → commit → lặp lại  
> **Quy trình hỗ trợ (Supporting Workflows)**: `/test`, `/debug`, `/create` (cho code không phải gameplay)  

## Mục Tiêu
Xây dựng game thông qua các sprint tính năng lặp đi lặp lại. Mỗi tính năng khi xuất xưởng đều phải hoàn chỉnh — gồm code, cảm giác game và tài liệu — trước khi chuyển sang tính năng kế tiếp.

## Vòng Lặp Tính Năng (The Feature Loop)

```
/plan → chọn tính năng → /implement-feature → tests vượt qua → cảm giác game tích hợp → commit
  ↑                                                                                           |
  └────────────────────────── Tính năng kế tiếp ◄─────────────────────────────────────────────┘
```

Đánh bóng **không phải là việc để dành cho Giai đoạn 5**. Mỗi lượt chạy `/implement-feature` đều bao gồm:
1. Phỏng vấn sâu (tựa game tham chiếu, các trường hợp biên, phạm vi)
2. Bộ câu hỏi cảm giác game ngay từ đầu (VFX, SFX, camera, rung)
3. Cập nhật tài liệu GDD + GFD trước khi code
4. Triển khai code theo hướng kiểm thử TDD
5. Tích hợp cảm giác game trong cùng một lượt (hoặc ghi nhận TODO nếu hoãn lại)

---

## Chu Kỳ Một Sprint (Sprint Cycle)

### 1. Lập Kế Hoạch Sprint (`/plan`)
- Xác định mục tiêu sprint từ danh sách tính năng trong GDD.
- Chia nhỏ thành từng tính năng người chơi trải nghiệm (dùng `/implement-feature`) và các tác vụ hỗ trợ (dùng `/create`).
- Ước tính độ phức tạp, phân công nhân sự AI và tạo task trên Linear (nếu có kết nối).

### 2. Triển Khai Tính Năng (`/implement-feature`)
Đối với mọi tính năng người chơi trực tiếp tương tác:
- Phỏng vấn → tài liệu → code → cảm giác game → commit.
- Xem chi tiết tại [implement-feature.md](../.agent/workflows/implement-feature.md).

Đối với các tiện ích phi gameplay (hàm helper, lớp dữ liệu, công cụ editor):
- Dùng trực tiếp lệnh `/create`.

### 3. Kiểm Thử (`/test`)
- Ánh xạ các kịch bản GDD Gherkin thành bài test NUnit.
- EditMode cho logic thuần, PlayMode cho vòng đời Unity.
- Chạy test qua Unity MCP hoặc bấm thủ công trong Test Runner.

### 4. Gỡ Lỗi (`/debug`)
- Quy trình 4 giai đoạn: Thu thập → Khoanh vùng → Giả thuyết → Sửa lỗi.
- Dùng `GameDebug` để ghi log có định hướng.
- Cú pháp commit sửa lỗi: `fix({scope}): {root cause}`.

### 5. Tích Hợp (Integration)
- Gộp các nhánh tính năng (merge) thông qua Pull Request (GitHub MCP).
- Chạy lại toàn bộ bài test trên nhánh `develop`.
- Xử lý xung đột code (nếu có).

---

## Quy Ước Đặt Tên Commit
```
feat(player): implement jump mechanic with game feel
feat(health): add damage system
fix(health): prevent negative health values
style(player): tune jump feel values
refactor(ui): extract health bar to ViewModel
test(combat): add damage edge case tests
chore(project): update package versions
```

---

## Tiêu Chí Đầu Vào
- Giai đoạn 3 hoàn tất (khung dự án, GameDebug, packages, git đã cấu hình xong).
- GDD có ít nhất danh sách tính năng dự kiến ban đầu.
- Đã chọn chế độ `ai_mode` trong `ProjectConfig.yaml`.

## Tiêu Chí Đầu Ra
- [ ] Tất cả các tính năng dự kiến đã được triển khai và commit an toàn
- [ ] Toàn bộ kịch bản GDD Gherkin đều có bài test vượt qua thành công
- [ ] Không còn bug nghiêm trọng
- [ ] Ma Trận Phản Hồi trong GFD có đủ dòng cho từng tính năng (đã tích hợp hoặc có `TODO(gamefeel)`)
- [ ] Không còn các TODO về cảm giác game bị bỏ quên
