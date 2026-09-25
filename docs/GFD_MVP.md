---
title: "MineMart: Pocket Workshop — Game Feel Document"
subtitle: "Juice, Feedback Matrix & Sensory Design (Academic & Production Specification)"
version: "1.0.0-PROD"
date: "2026-09-24"
author: "AI Game Designer & Miyuki"
status: "Final Approved for Phase 1 Submission"
target_platform: "Android / PC (Offline Single-screen)"
evaluation_deadline: "2026-10-01"
---

# MineMart: Pocket Workshop — Game Feel Document (MVP Edition)

> **Dự án**: MineMart: Pocket Workshop (Phiên bản Màn hình đơn quy chuẩn)  
> **Mục tiêu**: Định nghĩa các yếu tố "Nước ép" (Juice), hiệu ứng phản hồi giác quan (Audio, Visual, Haptics) giúp bản game 1 màn hình chơi cực kỳ đã tay và thỏa mãn.  
> **Nguyên tắc cốt lõi**: **Quy tắc Số Ba (Rule of Three)** — Bất kỳ hành động nào của người chơi cũng phải kích hoạt đủ 3 tầng phản hồi: Thị giác (Visual) + Thính giác (Audio) + Xúc giác (Haptics).  

---

## 1. Ma Trận Cảm Giác Game MVP (MVP Feedback Matrix)

| Hành Động Trong Game | Phản Hồi Thị Giác (Visual / Tween) | Phản Hồi Thính Giác (Audio SFX) | Phản Hồi Rung (Haptics) |
|---|---|---|---|
| **Chạm Tay (Tap) Khai Thác Block** | Khối block co giãn Squash & Stretch ($X,Z \times 1.15, Y \times 0.85$ trong 0.1s rồi nảy về). Bắn ra 3-5 hạt bụi vụn (Dust Particle). | Tiếng đập búa/xẻng gỗ giòn tan (`hit_wood.wav` hoặc `hit_stone.wav`). Pitch biến thiên ngẫu nhiên $\pm 0.1$. | Rung chạm cực nhẹ (Light Tap - 15ms). |
| **Thợ Chế Tác Xong 1 Block** | Block nhảy bật lên khỏi bàn thợ theo quỹ đạo Parabol (Jump Tween $0.25$s) rơi vào ô quầy. | Tiếng 'Plop' / 'Boing' vui nhộn khi rơi xuống bàn. | Không rung (tránh mỏi tay khi treo máy). |
| **Xếp Chồng Block Lên Cột** | Block rơi từ trên xuống cọc, đè nhẹ lên block dưới làm cả cột hơi lún xuống 5% rồi nảy lại (Squish Elastic). | Tiếng gõ gỗ trầm đục (`wood_stack.wav`). | Rung vi mô (Micro Haptic - 10ms). |
| **Khách Nhận Hàng & Thanh Toán** | Đồng tiền vàng (Gold Coin) từ tay khách nảy ra đất kèm số `+15` màu vàng kim bay lên (Floating Text Tween). Khách gật đầu vui vẻ. | Tiếng 'Ching-ching' tiền leng keng sảng khoái (`coin_pickup.wav`). | Rung nhẹ khi tiền bay vào ví (30ms). |
| **Bấm Nút Nâng Cấp Quầy** | Nút bấm lún xuống 10% khi chạm (Button Press Tween). Thanh tiến trình Lv nháy sáng vàng (Flash White/Gold). | Tiếng 'Click' cơ học đanh gọn (`ui_upgrade.wav`). | Rung phản hồi thao tác UI (20ms). |
| **Chạm Cột Mốc Đột Phá (Milestone 10, 25, 50, 100)** | Nổ bung chùm hạt pháo hoa ngôi sao (Confetti Particle). Quầy hàng phóng to 1.2 lần rồi đập mạnh xuống làm **Camera rung nhẹ** (Micro Screen Shake: Biên độ 0.15 trong 0.2s). | Hợp âm chúc mừng vang dội (`fanfare_level_up.wav`). | Rung đầm chắc (Medium Impact - 60ms). |

---

## 2. Thông Số Chi Tiết Kỹ Thuật (Juice Specifications)

### 2.1 Hiệu Ứng Co Giãn (Squash & Stretch Specs)
- **Công cụ**: DOTween (C#)
- **Hàm thực thi chuẩn**:
```csharp
transform.DOKill(true);
transform.DOScale(new Vector3(1.15f, 0.85f, 1.15f), 0.08f)
         .SetEase(Ease.OutQuad)
         .OnComplete(() => transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutBack));
```

### 2.2 Âm Thanh Động (Dynamic Audio Variation)
- Để tránh gây khó chịu khi chơi lâu (Ear Fatigue):
  - Mọi âm thanh lặp lại (tiếng búa gõ, tiếng tiền coin) **bắt buộc phải có Pitch Randomization**:
    $$\text{Pitch} = \text{Random.Range}(0.92f, 1.08f)$$
  - Âm thanh tiền coin khi thu hoạch dồn dập sẽ tăng dần cao độ theo thang âm (Pentatonic Scale: Đô - Rê - Mi - Son - La) để tạo cảm giác hưng phấn tột độ khi xưởng làm ăn phát đạt.

### 2.3 Phản Hồi Rung Haptic & Tùy Chọn Cài Đặt (Accessibility)
- **Tùy chọn Cài đặt**: Người chơi có toàn quyền Bật hoặc Tắt Haptics trong menu Cài Đặt (`isHapticEnabled: true/false`).
- Khi tắt, game tự động bỏ qua toàn bộ lệnh rung, bảo vệ thời lượng pin trên thiết bị di động.

---

## 3. Cảm Giác Điều Khiển Joystick Nổi (Floating Joystick Feel)

- **Vị trí xuất hiện**: Ngay tại điểm ngón tay chạm xuống màn hình (Dynamic Origin), không bắt người chơi với tay sang góc cố định.
- **Vùng đệm chết (Deadzone)**: Bán kính 15px để tránh việc vô tình quẹt tay bị hiểu nhầm là di chuyển.
- **Hút êm ái vào quầy (Soft Magnetic Snap)**: Khi người chơi điều khiển nhân vật chạy đến gần Quầy cần hỗ trợ (bán kính < 1.0m), nhân vật sẽ tự động hút êm vào vị trí làm việc và bắt đầu gõ búa hỗ trợ, không cần người chơi phải căn chỉnh tọa độ chính xác từng milimet.

---

## 4. Đồng Bộ Trạng Thái Cảm Giác Game Vào Database (Game Feel Persistence)

Mọi tùy chọn trải nghiệm của người chơi đều được lưu tự động vào **Nhóm 2: Global Profile** của file `savegame.json` để không bao giờ bị mất khi khởi động lại game:

```json
{
  "globalProfile": {
    "settings": {
      "isJoystickEnabled": true,
      "isHapticsEnabled": true,
      "masterVolume": 0.8,
      "sfxVolume": 1.0
    }
  }
}
```
- Khi người chơi gạt tắt Joystick hoặc Rung trong menu Cài Đặt, `GameFeelManager` phát tín hiệu cập nhật tức thì vào `SaveManager` để ghi nhớ vĩnh viễn.

