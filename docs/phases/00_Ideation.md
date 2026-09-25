# Giai Đoạn 0: Lên Ý Tưởng (Ideation)

> **Nhân sự phụ trách (Agent)**: Game Designer  
> **Lệnh quy trình (Workflow)**: `/brainstorm`  
> **Kết quả đầu ra (Output)**: Bản thảo GDD + Bản thảo GFD  

## Mục Tiêu
Biến một ý tưởng game thô sơ thành tài liệu thiết kế có cấu trúc mạch lạc và chuẩn mực.

## Các Hoạt Động Chính

### 1. Định Hình Tầm Nhìn (Vision Definition)
Agent Game Designer sẽ dẫn dắt em đi qua:
- Câu tóm tắt ý tưởng ngắn trong một câu (One-sentence pitch)
- Cảm giác hóa thân cốt lõi (Người chơi sẽ *cảm thấy* gì?)
- Các cung bậc cảm xúc mục tiêu
- Phân tích các tựa game tham chiếu

### 2. Thiết Kế Vòng Lặp Cốt Lõi (Core Loop Design)
Xác định vòng lặp gameplay nền tảng:
```
Thao tác (Input) → Hành động (Action) → Phản hồi (Feedback) → Phần thưởng (Reward) → Lặp lại (Loop)
```

### 3. Đặc Tả Cơ Chế Bằng Cú Pháp Gherkin
Viết từng cơ chế thành các kịch bản `Given / When / Then`:
```gherkin
Scenario: Người chơi nhảy
  Given người chơi đang đứng trên mặt đất
  When người chơi nhấn nút nhảy
  Then người chơi bay lên trên với lực 10
```

### 4. Lượt Lên Cảm Giác Ban Đầu (Initial Feel Pass)
Với mỗi cơ chế, xác định phản hồi giác quan:
- Hiệu ứng VFX / SFX / Camera / Rung tay cầm nào sẽ xuất hiện?
- Bắt đầu điền vào Ma Trận Phản Hồi trong GFD.

## Tiêu Chí Đầu Vào (Entry Criteria)
- Em có một ý tưởng game (ở bất kỳ mức độ chi tiết nào).

## Tiêu Chí Đầu Ra (Exit Criteria)
- [ ] Các phần 1-3 trong GDD đã hoàn thành bản thảo (Tầm nhìn, Vòng lặp cốt lõi, Cơ chế)
- [ ] Ma Trận Phản Hồi trong GFD đã được khởi tạo
- [ ] Danh sách các câu hỏi thiết kế mở đã được liệt kê
- [ ] Em cảm thấy tự tin và hào hứng với định hướng đã chọn
