---
title: "MineMart: Idle Block Tycoon — Master Game Feel Document"
subtitle: "Juice Engineering, Sensory Feedback Matrix & Kinesthetic Design"
version: "2.0.0-MASTER"
date: "2026-09-25"
author: "AI Game Designer & Miyuki"
status: "Official Master Specification (Production Approved)"
target_platform: "Android (Portrait) / PC Cross-Platform"
feel_middleware: "DOTween Pro, Cinemachine, Unity AudioPool"
---

# MineMart: Idle Block Tycoon — Master Game Feel Document

> **Bản Đặc Tả Cảm Giác Game Toàn Diện (Master GFD)**  
> *Định nghĩa toàn bộ các yếu tố "Nước ép" (Juice), hiệu ứng tương tác giác quan (Visual, Audio, Haptics), kỹ thuật nảy vật lý và cấu hình camera nhằm mang lại trải nghiệm tương tác thỏa mãn tối đa cho người chơi.*

---

## 1. Triết Lý Cảm Giác Cốt Lõi (Core Sensory Philosophy)

### 1.1 Quy Tắc Số Ba (Rule of Three)
Mọi tương tác của người chơi hoặc hành vi kinh tế trong MineMart bắt buộc phải kích hoạt đồng thời **3 tầng phản hồi giác quan**:
1. **Thị Giác (Visual Juice)**: Hiệu ứng co giãn đàn hồi (Squash & Stretch), hạt bụi/tia lửa (Particles), chuyển động nảy Bézier, popup con số vàng bay bổng.
2. **Thính Giác (Dynamic Audio)**: Âm thanh chế tác cộp cộp quen thuộc của Minecraft, tiếng tiền leng keng vui tai với cơ chế biến thiên cao độ (Pitch Randomization) chống mỏi tai, tiếng "Hừm!" đặc trưng của Dân làng.
3. **Xúc Giác (Kinesthetic / Haptics)**: Rung nhịp vi mô (Micro-haptic) khi block xếp chồng, rung đầm chắc khi đạt mốc Milestone hoặc mở Rương trang bị hiếm *(Có tùy chọn Bật/Tắt trong Cài đặt)*.

---

## 2. Công Cụ & Thư Viện Thực Thi (Feel Middleware Stack)

| Thành phần | Công cụ / Thư viện | Vai trò thực thi chi tiết |
|---|---|---|
| **Chuyển động đàn hồi (Tweening)** | **DOTween Pro** (C#) | Xử lý toàn bộ logic co giãn Squash & Stretch, quỹ đạo Parabol rơi của block, tiền xu nảy và hiệu ứng nút bấm UI (`Ease.OutBack`, `Ease.OutQuad`). |
| **Kiến trúc phản hồi (Feedback Channels)** | **ScriptableObject Event Channels** | Tách rời 100% logic gameplay khỏi hiệu ứng. Hàm giao hàng chỉ bắn event `OnOrderDelivered`, hệ thống Juice tự động bắt tín hiệu và phát hiệu ứng. |
| **Hệ thống âm thanh (Audio Engine)** | **Unity AudioSource Pool** | Pool quản lý 32 kênh âm thanh độc lập. Hỗ trợ Audio 3D Spatial cho tiếng xưởng lò nung và Audio 2D phẳng cho tiếng tiền/UI. |
| **Hệ thống máy quay (Camera System)** | **Cinemachine Isometric** | Camera góc nhìn 45 độ theo sát nhân vật khi bật Joystick, tự động zoom out nhẹ khi xưởng mở rộng và thực hiện các rung chấn vi mô (Camera Shake). |

---

## 3. Ma Trận Phản Hồi Toàn Diện (Master Feedback Matrix)

| Sự Kiện / Hành Động | Phản Hồi Thị Giác (Visual) | Phản Hồi Thính Giác (Audio SFX) | Rung Màn Hình (Camera) | Chuyển Động Đàn Hồi (Tween) | Rung Thiết Bị (Haptic) | Ưu Tiên |
|---|---|---|---|---|---|---|
| **Chạm Khai Thác Thủ Công (Tap Ore)** | Hạt bụi vụn bắn ra 4 hướng | Tiếng búa gõ đanh giòn (`hit_wood.wav`) | — | Khối quặng co giãn Squash ($X,Z \times 1.15, Y \times 0.85$) | Rung chạm cực nhẹ (15ms) | High |
| **Hút Vào Quầy (Smart Magnetic Snap)** | Vòng hào quang sáng fade in dưới chân quầy | Tiếng "Click" cơ học đầm chắc | — | Nhân vật trượt êm vào tâm quầy ($0.15\text{s}$) | Rung nhẹ xác nhận (20ms) | High |
| **Thợ Chế Tác Xong 1 Block** | Block nhảy vọt lên khỏi bàn thợ | Tiếng 'Plop' / 'Boing' vui nhộn | — | Quỹ đạo Parabol rơi vào ô quầy ($0.25\text{s}$) | — | Critical |
| **Xếp Chồng Block 3D Lên Cột** | Bụi nảy nhẹ khi block chạm nóc | Tiếng gõ gỗ trầm đục (`wood_stack.wav`) | — | Cột block lún xuống 5% rồi nảy lại (Squish Elastic) | Rung vi mô (10ms) | Medium |
| **Khách Nhận Block & Thanh Toán** | Block bay vào tay khách, tiền xu rơi đất | Tiếng 'Ching-ching' tiền leng keng | — | Tiền nảy 2 nhịp rồi bay vòng cung về ví UI | Rung nhịp nảy (25ms) | Critical |
| **Pet Chạy Nhặt Tiền Boa (Tips)** | Vệt sáng đuôi (Trail Renderer) theo chân Pet | Tiếng 'Ting!' cao vút vui sướng | — | Pet nhảy chồm lên nhặt coin rồi nảy về ví | Rung nhẹ (15ms) | High |
| **Nâng Cấp Quầy (Level Up)** | Cột ánh sáng bốc lên từ chân quầy | Tiếng "Chime" trong trẻo | — | Toàn bộ quầy nảy nhẹ (PunchScale $Y: 1.15$) | Rung tương tác UI (20ms) | High |
| **Đột Phá Cột Mốc (Milestone x2, x4)** | Pháo hoa giấy (Confetti) nổ tung khắp quầy | Hợp âm Fanfare kèn đồng chiến thắng | **Micro Shake** (Biên độ 0.08, thời gian 0.15s) | Chữ "MILESTONE!" dập mạnh giữa màn hình | Rung xung mạnh (60ms) | Critical |
| **Mở Khóa Quần Xã Mới (New Biome)** | Khói bụi mờ ảo tan biến, map mới hiện ra | Nhạc nền Biome mới ngân vang rộn rã | **Medium Shake** (Biên độ 0.25, thời gian 0.3s) | Toàn bộ xưởng mới trồi lên từ lòng đất | Rung kép (Double Pulse) | Critical |
| **Quay Rương Trang Bị (Lootbox)** | Ánh sáng hào quang tỏa ra từ khe nắp rương | Tiếng cọt kẹt mở rương gỗ kinh điển | **Light Shake** khi nổ đồ Epic/Legendary | Rương rung lắc 3 nhịp trước khi bật tung nắp | Rung nhịp dồn dập (Pattern Pulse)| Critical |
| **Phù Phép Thành Công (Enchant)** | Hạt ký tự Rune huyền bí xoay quanh trang bị | Tiếng chuông ma thuật ngân vang huyền ảo | — | Trang bị phóng to rồi phát ánh tím huyền bí | Rung êm đầm (45ms) | High |
| **Dân Làng Bước Đi 1:1 (Integer Tile Stepping)** | Hạt bụi nhỏ văng gót chân mỗi bước nhịp | Tiếng bước chân lụp bụp theo bề mặt (`step_wood.wav`) | — | Lắc lư nhẹ Waddle/Bobbing ($Y \pm 0.05\text{m}$) $0.25\text{s/ô}$ | — | High |
| **Đặt Quầy / Block Hít Lưới (Grid Snapping)** | Khung viền sáng vuông vức quanh ô $1\times 1\text{m}$ | Tiếng "Thump" đầm chắc khi vật chạm đất | — | Vật thể nhún nhẹ tiếp đất (PunchScale $Y: 0.95$) | Rung xác nhận nhẹ (20ms) | Medium |
| **Kích Hoạt Lệnh Admin Tool (Debug Cheat)** | Toast Popup nổi giữa màn hình + Sao vàng bay | Tiếng chuông 'Ding!' công nghệ cao | — | Popup nảy nở `Ease.OutBack` $0.2\text{s}$ rồi bay lên fade out | Rung kép phản hồi (Double Click) | Low |

---

## 4. Tiêu Chuẩn Kỹ Thuật Chi Tiết (Technical Juice Specifications)

### 4.1 Quy Chuẩn Co Giãn Squash & Stretch (DOTween Specs)
Để tạo cảm giác khối hộp có sức sống và tính đàn hồi như thạch:
```csharp
// Chuẩn co giãn khi gõ búa hoặc block rơi xuống quầy
public static void PlaySquashAndStretch(Transform target)
{
    target.DOKill(true);
    target.DOScale(new Vector3(1.15f, 0.85f, 1.15f), 0.08f)
          .SetEase(Ease.OutQuad)
          .OnComplete(() => target.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutBack));
}
```

### 4.2 Kỹ Thuật Âm Thanh Chống Mỏi Tai (Anti-Ear Fatigue Dynamic Audio)
Vì là game Idle có tần suất sản xuất và thu tiền cực cao, việc lặp lại một âm thanh đơn điệu sẽ gây ức chế thần kinh cho người chơi sau 5 phút:
1. **Biến Thiên Cao Độ Ngẫu Nhiên (Random Pitch Modulation)**:
   Mọi âm thanh gõ búa, nung quặng và nhặt coin đều phải dao động ngẫu nhiên:
   $$\text{Pitch} = \text{Random.Range}(0.92f, 1.08f)$$
2. **Thang Âm Ngũ Cung Khi Thu Tiền Dồn Dập (Pentatonic Audio Progression)**:
   Khi khách thanh toán liên tiếp hoặc Pet nhặt chuỗi tiền boa, cao độ âm thanh sẽ nhảy từng bậc theo thang âm Ngũ Cung (Đô - Rê - Mi - Son - La: $1.0 \rightarrow 1.12 \rightarrow 1.26 \rightarrow 1.41 \rightarrow 1.58$) để tạo cảm giác hưng phấn tột độ khi doanh thu bùng nổ.

### 4.3 Cấu Hình Rung Màn Hình (Cinemachine Camera Shake Profiles)

| Profile Shake | Thời Gian (Duration) | Biên Độ (Amplitude) | Tần Số (Frequency) | Tình Huống Kích Hoạt |
|---|---|---|---|---|
| **Micro Shake** | $0.12\text{s}$ | $0.06$ | $15\text{Hz}$ | Đạt Milestone cấp 10, 25, 50 quầy hàng |
| **Light Shake** | $0.20\text{s}$ | $0.14$ | $18\text{Hz}$ | Mở Rương trang bị trúng đồ Hiếm (Epic/Legendary) |
| **Medium Shake** | $0.35\text{s}$ | $0.28$ | $20\text{Hz}$ | Chuyển sang Quần xã mới (Biome Expansion) |

### 4.4 Quy Chuẩn Nhịp Bước Đi Nguyên Tố 1:1 (Integer Stepping Kinesthetics)
Để việc di chuyển theo từng ô gạch số nguyên không bị thô cứng kiểu robot, chuyển động của Dân làng kết hợp chuyển động học (Kinematics) và nảy nhấp nhô:
1. **Chuyển dịch mượt mà (Smooth Tile Lerp)**:
   - Thời gian di chuyển mỗi ô: $T_{\text{step}} = 0.25\text{s}$ (tương ứng vận tốc chuẩn $4.0\text{m/s}$).
   - Đường cong chuyển vị: `Ease.Linear` trên mặt phẳng $(X, Z)$ giúp tốc độ bước chân đều đặn.
2. **Lắc lư nhịp bước (Waddle & Bobbing Curve)**:
   - Trên trục thẳng đứng $Y$: Nhân vật nảy lên đỉnh $+0.05\text{m}$ ở giữa bước ($t = 0.125\text{s}$) và hạ xuống $0.0\text{m}$ khi chạm tâm ô kế tiếp theo đồ thị hình sin ($\sin$).
   - Trên trục xoay $Z$ (Góc nghiêng thân): Nghiêng nhẹ $\pm 3^{\circ}$ luân phiên trái/phải theo từng bước chân tạo cảm giác Dân làng lon ton chăm chỉ, sống động.

---

## 5. Đồng Bộ Trạng Thái & Khả Năng Tiếp Cận (Accessibility & Persistence)

Mọi tùy chọn trải nghiệm cảm giác game của người chơi đều được đồng bộ hóa và lưu vĩnh viễn vào **Nhóm 2: Global Profile** của file `savedata.json`:
- **Tùy chọn Bật/Tắt Cần Điều Khiển (`isJoystickEnabled`)**:
  - Gạt Tắt: Nhân vật tự động vận hành 100%, ẩn toàn bộ UI cần gạt nổi.
- **Tùy chọn Bật/Tắt Rung Haptics (`isHapticsEnabled`)**:
  - Gạt Tắt: Hệ thống ngắt toàn bộ lệnh rung xúc giác, bảo vệ pin cho thiết bị di động.
- **Thanh trượt Âm Lượng (`masterVolume`, `sfxVolume`, `bgmVolume`)**:
  - Tự động lưu giá trị thập phân từ $0.0$ đến $1.0$, khôi phục chính xác mỗi khi khởi động lại game.

---

*Hết tài liệu Master Game Feel Document.*
