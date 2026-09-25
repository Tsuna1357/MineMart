# Giai Đoạn 5: Đánh Bóng (Polish)

> **Nhân sự phụ trách (Agents)**: Game Designer + Art Director  
> **Lệnh quy trình (Workflow)**: `/polish`  
> **Kỹ năng sử dụng (Skill)**: `game-feel-integrator`  

## Mục Tiêu

Tinh chỉnh hoàn thiện lần cuối, tích hợp đồ họa mỹ thuật và đo đạc tối ưu hiệu năng. Cảm giác game đã được tích hợp cho từng tính năng trong suốt Giai đoạn 4 — Giai đoạn 5 là nhằm nâng tầm trải nghiệm lên đỉnh cao, chứ không phải bắt đầu từ con số không. Đây là lượt hoàn thiện cuối cùng: giải quyết các TODO bị hoãn lại, cân chỉnh lại các thông số sau các buổi chơi thử (playtesting) mở rộng, thay thế đồ họa tạm bằng đồ họa thật và chạm mốc mục tiêu hiệu năng.

Nếu có bất kỳ tính năng nào có ghi chú `TODO(gamefeel):`, giai đoạn này sẽ ưu tiên giải quyết chúng trước tiên.

---

## Các Hoạt Động Chính

### 0. Giải Quyết Cảm Giác Game Bị Hoãn Lại
Trước khi làm việc khác — quét toàn bộ codebase để tìm các ghi chú `TODO(gamefeel):`:
- Với mỗi mục tìm thấy, chạy một lượt game feel rút gọn của `/implement-feature` (chỉ gồm các Bước 3–7).
- Cập nhật dòng tương ứng trong Ma Trận Phản Hồi GFD từ "chờ xử lý" (pending) sang hoàn thành.
- Xóa dòng ghi chú TODO sau khi đã giải quyết xong.

### 1. Đợt Tinh Chỉnh Cuối Cùng (`/polish`)
Với những tính năng đã có cảm giác game, tiến hành tinh chỉnh và khuếch đại:
- Xem lại từng dòng trong Ma Trận Phản Hồi GFD.
- Cân chỉnh lại các con số (biên độ rung màn hình, thời lượng tween, âm lượng SFX) sau khi chơi thử thực tế.
- Nguyên tắc *"bắt đầu hơi phóng đại, sau đó giảm dần"* vẫn áp dụng cho mọi giá trị chưa được chơi thử kỹ càng.

### 2. Tích Hợp Mỹ Thuật Đồ Họa
Agent Art Director thay thế các đồ họa giữ chỗ bằng tài nguyên thật:
- Thay các khối hình học thô sơ bằng model 3D / sprite 2D hoàn chỉnh.
- Thiết lập vật liệu (materials), shaders và hệ thống chiếu sáng.
- Cấu hình mức độ chi tiết LOD cho từng nền tảng đích.
- Áp dụng quy ước đặt tên cho mọi tài nguyên (`M_`, `T_`, `SPR_`...).

### 3. Tối Ưu Hóa Hiệu Năng
- Dùng Unity Profiler để đo đạc — xác định các điểm nghẽn ngốn CPU trong các hàm Update.
- Sử dụng Object Pooling cho hạt particle và đạn đạo nếu chưa làm.
- Nén texture phù hợp với nền tảng đích.
- Gom nhóm lệnh vẽ (Draw call batching) và GPU Instancing ở những nơi thích hợp.
- Xác minh toàn bộ chỉ số hiệu năng mục tiêu trong `docs/TDD.md` đều đạt chuẩn.

### 4. Kiểm Thử Hồi Quy (Regression Testing)
- Chạy lại toàn bộ bài test sau mỗi lần thay đổi.
- Đảm bảo các chỉnh sửa làm đẹp không vô tình làm hỏng logic gameplay.
- Điểm chuẩn hiệu năng phải đạt mục tiêu TDD.

### 5. Buổi Chơi Thử Toàn Diện (Final Playtest)
- Chơi thử toàn bộ game từ đầu đến cuối — cảm giác chơi đã thực sự trọn vẹn chưa?
- Kiểm tra xem mọi mục trong Ma Trận Phản Hồi GFD đã được triển khai hết chưa.
- Bất kỳ khoảnh khắc nào cảm thấy còn "khô khan" sẽ được đưa qua một lượt `/polish` cuối cùng.

---

## Tiêu Chí Đầu Vào
- Giai đoạn 4 hoàn tất (tất cả tính năng đã triển khai và commit).
- Ma Trận Phản Hồi GFD có đầy đủ các dòng cho từng tính năng.

## Tiêu Chí Đầu Ra
- [ ] Tất cả các ghi chú `TODO(gamefeel):` đã được giải quyết triệt để
- [ ] Mọi mục trong Ma Trận Phản Hồi GFD đã được triển khai và cân chỉnh hoàn hảo
- [ ] Đồ họa mỹ thuật cuối cùng đã tích hợp (không còn đồ họa thô trong Scene xuất xưởng)
- [ ] Hiệu năng đạt chuẩn mục tiêu TDD
- [ ] Toàn bộ bộ test đều vượt qua
- [ ] Trải nghiệm chơi game mang lại cảm giác bóng bẩy, mượt mà và đã tay
