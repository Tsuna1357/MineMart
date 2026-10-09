---
title: "MineMart: Pocket Workshop — Sprint Plan"
sprint_number: "1-7"
start_date: "2026-09-20"
end_date: "2026-10-05"
goal: "Xây dựng hoàn chỉnh tựa game Idle Tycoon kết hợp Arcade Voxel trong 1 màn hình dọc tĩnh, đạt chuẩn nộp bài và demo mượt mà 60fps"
status: "In Progress (Sprint 5 UI Next)"
---

# 🚀 Kế Hoạch Sản Xuất Dự Án: MineMart: Pocket Workshop

> **Mục tiêu sản phẩm:** Phát triển hoàn chỉnh vòng lặp cốt lõi (Core Loop) game Idle Tycoon kết hợp Arcade điều khiển lai trên một màn hình tĩnh (Single-screen). Game vận hành ngoại tuyến 100%, không lag giật, đồ họa Voxel tươi sáng và cảm giác tương tác thỏa mãn (Juicy Feel).

## 🏛 Quyết Định Kiến Trúc Đã Giải Quyết (Resolved Architecture Decisions)
- **Kiến trúc dữ liệu:** ScriptableObject-First (`StallDataSO`, `BlockDataSO`, `MilestoneRuleSO`).
- **Phân tách trách nhiệm (Separation of Concerns):**
  - `StallDataSO`: Thuần túy lưu trữ hồ sơ kinh tế (giá mua, hệ số nhân giá, block sản xuất, mốc milestone).
  - `MiningStation`: Đối tượng thực thể trong Scene quản lý vị trí các slot (`CustomSlotAnchor`), cấp độ tối đa (`MaxLevel`), và mốc mở khóa slot (`SlotUnlockLevels`).
- **Cơ chế đơn hàng (Order Dispatching):** Hàng đợi phân ngăn FIFO $O(1)$ (`OrderManager`), mỏ chỉ khai thác khi có đơn đặt hàng từ khách (`NeedsMining`), tuyệt đối không sản xuất dư thừa.
- **Điều khiển lai (Hybrid Control):** Người chơi tự do kéo Joystick di chuyển trực tiếp; buông tay quá 1.5s hệ thống tự động kích hoạt Auto-Pilot làm việc thay.
- **Hệ thống Lưu trữ (Persistence):** Lưu trữ JSON cục bộ qua `SaveManager`, tính toán thu nhập Offline AFK khi khởi động lại game.

---

## 📊 Bảng Tổng Quan Tiến Độ Các Sprint

| Sprint | Nội dung trọng tâm | Trạng thái | Tỷ lệ hoàn thành |
| :--- | :--- | :---: | :---: |
| **Sprint 1** | Kiến trúc dữ liệu, Dây chuyền cung ứng & Điều phối đơn hàng | ✅ Done | 100% |
| **Sprint 2** | Mỏ đa slot, Bộ đệm xếp chồng 3D & Cơ chế điều khiển lai | ✅ Done | 100% |
| **Sprint 3** | Kéo thả quy hoạch mỏ, Nam châm hút & Tự động chạy theo mỏ mới | ✅ Done | 100% |
| **Sprint 4** | Động cơ kinh tế, Lưu trữ ngoại tuyến & Tính tiền AFK | ✅ Done | 100% |
| **Sprint 5** | Giao diện người dùng (UI), HUD thanh tiền & Bảng nâng cấp quầy | 🔄 In Progress | 0% (Ưu tiên tiếp theo) |
| **Sprint 6** | Cảm giác game (Game Feel), Âm thanh (SFX/BGM) & Tiền bay | ⬜ To Do | 0% |
| **Sprint 7** | Trang trí cảnh quan (Dressing), Ánh sáng URP & Tối ưu Demo | ⬜ To Do | 0% |

**Chú thích trạng thái:** ✅ Done (Đã xong) | 🔄 In Progress (Đang thực hiện) | ⬜ To Do (Chờ thực hiện) | ❌ Blocked (Bị nghẽn)

---

## 📦 Chi Tiết Kế Hoạch Từng Sprint

### ✅ Sprint 1: Kiến Trúc Nền Tảng & Chuỗi Cung Ứng Khép Kín
*Mục tiêu: Dựng bộ khung ScriptableObject, hệ thống đơn hàng trung tâm và vòng lặp tiếp khách cơ bản.*

| # | Hạng mục công việc (Task) | Quy mô | Phụ trách | Trạng thái | Ghi chú kỹ thuật |
|---|---------------------------|:------:|:---------:|:----------:|------------------|
| 1 | Phân chia Assembly Definition (`Core`, `Data`, `Economy`, `Gameplay`, `UI`) | S | Architect | ✅ Done | Tối ưu hóa thời gian biên dịch, kiến trúc module độc lập |
| 2 | Thiết kế Schema dữ liệu (`BlockDataSO`, `StallDataSO`, `MilestoneRuleSO`) | M | Architect | ✅ Done | Single source of truth cho kinh tế và tài nguyên voxel 3D |
| 3 | Xây dựng Bộ điều phối đơn hàng trung tâm `OrderManager` FIFO O(1) | L | Gameplay Dev | ✅ Done | Phân ngăn theo loại Block, quản lý 4 trạng thái đơn hàng |
| 4 | Xây dựng Quầy tiếp khách `CustomerCounter` & các ô `CustomerSlot` | M | Gameplay Dev | ✅ Done | Điểm neo đón khách, tiếp nhận order và vị trí để lại tiền Tip |
| 5 | Quản lý Khách hàng `CustomerManager` & AI `CustomerAgent` | M | Gameplay Dev | ✅ Done | Sinh khách, xếp hàng lùi thẳng ra sảnh đón, quay mặt vuông góc |

---

### ✅ Sprint 2: Mỏ Đa Slot, Bộ Đệm Xếp Chồng & Điều Khiển Lai
*Mục tiêu: Đưa nhân vật chính và thợ dân làng vào vận hành dây chuyền sản xuất đa điểm.*

| # | Hạng mục công việc (Task) | Quy mô | Phụ trách | Trạng thái | Ghi chú kỹ thuật |
|---|---------------------------|:------:|:---------:|:----------:|------------------|
| 6 | Trạm mỏ đa slot `MiningStation`: Mở khóa N-slot theo cấp độ | L | Gameplay Dev | ✅ Done | Độc lập hóa `MaxLevel` và `_slotUnlockLevels`, logic đào theo order |
| 7 | Tùy biến vị trí slot tự do (`CustomSlotAnchor`) | M | Gameplay Dev | ✅ Done | Cho phép Designer đặt vị trí slot theo hình chữ L, vòng cung tùy biến |
| 8 | Quầy trung chuyển `CheckoutCounter`: Xếp chồng quặng kim tự tháp 3D | M | Gameplay Dev | ✅ Done | Đệm 4 block/ô, chia rõ `PlaceAnchor` cho thợ và `TakeAnchor` cho runner |
| 9 | AI Thợ Dân Làng `WorkerAgent` đa vai trò (Miner, Runner, DirectSeller) | L | Gameplay Dev | ✅ Done | Không va chạm vật lý (No Obstacle Avoidance), tự tìm việc tự động |
| 10| Người chơi điều khiển lai `PlayerController` (Manual Joystick + Auto-Pilot) | L | Gameplay Dev | ✅ Done | Tự động làm việc khi buông tay, ưu tiên điều khiển tay ngay khi chạm |

---

### ✅ Sprint 3: Quy Hoạch Kéo Thả Trạm Mỏ & Tự Động Định Vị Lại
*Mục tiêu: Đem lại sự tự do sắp xếp bố cục cửa hàng với phản hồi vật lý mượt mà.*

| # | Hạng mục công việc (Task) | Quy mô | Phụ trách | Trạng thái | Ghi chú kỹ thuật |
|---|---------------------------|:------:|:---------:|:----------:|------------------|
| 11| Ô Quy Hoạch `WorkstationPlot`: Hỗ trợ offset vị trí và góc xoay riêng | M | Gameplay Dev | ✅ Done | Tránh reset về giữa ô, hiển thị WireCube preview trong Editor |
| 12| Bộ điều khiển kéo thả `WorkstationPlacementController`: Nhấc bổng, theo chuột | M | Gameplay Dev | ✅ Done | Nhấn giữ 0.2s để nhấc, nâng bổng trên sàn, bay theo ngón tay |
| 13| Cơ chế Nam Châm Hút Quầy (Magnetic Snap) vào Bounding Box của ô | M | Gameplay Dev | ✅ Done | Tự động nhận diện khi vào phạm vi khu đất, tự căn chỉnh góc xoay |
| 14| Cơ chế Hoán Đổi Vị Trí (Swap Stations) giữa 2 ô mỏ | M | Gameplay Dev | ✅ Done | Trượt mượt mà hai mỏ đổi chỗ cho nhau khi thả đè lên ô đã có mỏ |
| 15| Ngắt đào an toàn & Bảo lưu tiến độ khai thác dở dang (`SavedCraftTimer`) | M | Gameplay Dev | ✅ Done | Không bị mất % đào dở khi mỏ bị bốc đi, tránh cuốc vào không khí |
| 16| Tự động bắt mục tiêu và chạy sang mỏ mới (`OnStationRelocated`) | M | Gameplay Dev | ✅ Done | Thợ và Auto-Pilot tự động chạy bộ sang vị trí mới để đào tiếp |

---

### ✅ Sprint 4: Động Cơ Kinh Tế, Lưu Trữ & Tính Thu Nhập AFK
*Mục tiêu: Đảm bảo tiến trình người chơi được bảo toàn và tạo động lực tích lũy vàng.*

| # | Hạng mục công việc (Task) | Quy mô | Phụ trách | Trạng thái | Ghi chú kỹ thuật |
|---|---------------------------|:------:|:---------:|:----------:|------------------|
| 17| Động cơ kinh tế `EconomyManager`: Ví vàng, công thức nâng cấp lũy tiến | M | Architect | ✅ Done | $Cost = Base \times Multiplier^{(Level - 1)}$, tích hợp tiền tip và thưởng đơn |
| 18| Hệ thống lưu trữ `SaveManager`: Serialization JSON cục bộ | M | Gameplay Dev | ✅ Done | Tự động lưu tiến trình (Vàng, Level mỏ, vị trí Plot...) không mất dữ liệu |
| 19| Thuật toán tính thu nhập ngoại tuyến (Offline AFK Welcome-Back) | M | Gameplay Dev | ✅ Done | Ước tính sản lượng và tặng vàng tích lũy khi mở lại game |

---

### 🔄 Sprint 5: Giao Diện Người Dùng (UI / HUD) & Tương Tác
*Mục tiêu: Cung cấp giao diện trực quan cho người chơi theo dõi dòng tiền và nâng cấp xưởng.*

| # | Hạng mục công việc (Task) | Quy mô | Phụ trách | Trạng thái | Ghi chú kỹ thuật |
|---|---------------------------|:------:|:---------:|:----------:|------------------|
| 20| Thanh HUD Chính: Hiển thị Vàng (Icon Vàng + Rút gọn số 1.2K, 3.4M) | M | UI Specialist | ✅ Done | Thanh trên cùng màn hình, nảy số mượt, layout khớp 100% bản vẽ |
| 21| Bảng Thông Tin & Nâng Cấp Trạm Mỏ (Station Upgrade Card / Popup) | L | UI Specialist | ⬜ To Do | Hiện tên mỏ, cấp độ, số slot, chi phí nâng cấp và nút bấm `Upgrade` |
| 22| Vùng Mua / Nâng Cấp Dưới Sàn (Interactive Purchase Zone under Station) | M | Gameplay Dev | ⬜ To Do | Vòng tròn mở khóa/nâng cấp dưới chân mỏ khi người chơi bước vào |
| 23| Menu Cài Đặt (Floating Settings Modal) | S | UI Specialist | ✅ Done | Modal Voxel: Bật/Tắt Auto-Pilot, Bật/Tắt Joystick, SFX, Haptics |
| 24| Hiệu ứng Chữ Bay & Tiền Bay (Floating Text `+$15G` & Popups) | M | UI Specialist | ⬜ To Do | Hiển thị số tiền bay lên khi giao block hoặc nhặt tiền tip |

---

### ⬜ Sprint 6: Âm Thanh (SFX/BGM) & Cảm Giác Game (Juice Pass)
*Mục tiêu: Bổ sung phản hồi giác quan đậm đặc (Audio & Haptics) giúp game chơi cuốn hút.*

| # | Hạng mục công việc (Task) | Quy mô | Phụ trách | Trạng thái | Ghi chú kỹ thuật |
|---|---------------------------|:------:|:---------:|:----------:|------------------|
| 25| Tích hợp Hệ thống Âm thanh `AudioManager` (AudioSource Pool) | M | Audio Engineer | ⬜ To Do | Quản lý phát âm thanh không giật lag, tối ưu bộ nhớ |
| 26| Thiết kế & Gắn SFX Khai thác: Tiếng cuốc đập đá (`Mining SFX`) | S | Audio Engineer | ⬜ To Do | Biến thiên cao độ (Pitch variation $\pm 0.1$) tránh cảm giác nhàm chán |
| 27| Thiết kế & Gắn SFX Giao dịch: Tiếng block rơi, tiếng leng keng nhặt tiền Tip | S | Audio Engineer | ⬜ To Do | Âm sắc trong trẻo, kích thích dopamine |
| 28| Tích hợp Nhạc nền Lo-Fi thư giãn (Cozy Voxel Workshop BGM) | S | Audio Engineer | ⬜ To Do | Vòng lặp êm dịu, âm lượng nền nhẹ nhàng |
| 29| Tinh chỉnh Squash & Stretch và rung nảy (DOTween Feel Pass) | M | Gameplay Dev | ⬜ To Do | Khối nảy nhẹ khi nhặt, quầy mỏ đàn hồi khi đặt xuống sàn |

---

### ⬜ Sprint 7: Trang Trí Cảnh Quan, Ánh Sáng URP & Đóng Gói Demo
*Mục tiêu: Đưa chất lượng thẩm mỹ lên mức cao nhất và kiểm thử toàn diện trên thiết bị.*

| # | Hạng mục công việc (Task) | Quy mô | Phụ trách | Trạng thái | Ghi chú kỹ thuật |
|---|---------------------------|:------:|:---------:|:----------:|------------------|
| 30| Thiết lập Ánh sáng URP Isometric ấm áp & Đổ bóng mềm | M | Art Director | ⬜ To Do | Màu nắng ấm dịu, góc chiếu Isometric chuẩn tôn lên nét đẹp Voxel |
| 31| Trang trí cảnh quan sảnh xưởng (Cây cối, thảm cỏ viền, đèn lồng Voxel) | M | Art Director | ⬜ To Do | Xóa bỏ cảm giác màn hình xám xịt, tạo không gian xưởng ấm cúng |
| 32| Kiểm thử luồng chơi PlayMode và cân bằng kinh tế ban đầu | M | QA Tester | ⬜ To Do | Đảm bảo 10 phút chơi đầu tiên nhịp độ nhanh và gây nghiện |
| 33| Build Demo (PC Executable / APK Android) & Quay video trải nghiệm | S | Gameplay Dev | ⬜ To Do | Kiểm tra độ mượt đạt 60fps ổn định |

---

## 🎯 Tiêu Chí Nghiệm Thu Dự Án (Project Acceptance Criteria)
- [x] Vòng lặp cung ứng (Order -> Mine -> Counter -> Customer -> Tip) chạy trơn tru 100% không nghẽn.
- [x] Kéo thả di dời trạm mỏ tự do, tự động hút vào ô quy hoạch, hoán đổi mượt mà.
- [x] Thợ và người chơi tự động ngắt cuốc và tự chạy sang vị trí mới tiếp tục đào khi mỏ di chuyển.
- [x] Dữ liệu tiền vàng và cấp độ được lưu an toàn sang file JSON, mở lại game không mất.
- [ ] UI HUD hiển thị tiền vàng và bảng nâng cấp quầy hoạt động mượt mà, bấm nâng cấp trừ tiền chính xác.
- [ ] Âm thanh gõ cuốc và nhặt tiền leng keng sống động.
- [ ] Tốc độ khung hình duy trì 60 FPS ổn định.
