---
title: "MineMart: Pocket Workshop — Technical Design Document (MVP Edition)"
subtitle: "Kiến trúc hệ thống và kỹ thuật cho bản Minimum Viable Product (Single-Screen)"
version: "1.0.0-MVP"
date: "2026-09-27"
author: "AI Technical Architect & Miyuki"
status: "Final MVP Technical Spec"
unity_version: "6.2.0f1 (Unity 6)"
architecture_pattern: "so-first (ScriptableObject-Driven & Event-Decoupled)"
---

# MineMart: Pocket Workshop — Technical Design Document (MVP Edition)

> **Tài Liệu Thiết Kế Kỹ Thuật Dành Riêng Cho Phiên Bản MVP**  
> *Đặc tả giới hạn nghiêm ngặt trong phạm vi GDD MVP: 1 màn hình, 3 quầy khai thác (Đất, Gỗ, Đá), hệ thống 2 quầy chuyên biệt, thuật toán điều phối thợ Dân làng, và cơ chế lưu trữ JSON cục bộ. Không bao gồm các hệ thống trang bị (Equipment), Biome, hay IAP.*

---

## 1. Phân Tầng Assembly & Cấu Trúc Thư Mục MVP

Dự án áp dụng Assembly Definitions (.asmdef) nhằm phân tách rõ ràng trách nhiệm và tối ưu thời gian biên dịch:

```
+--------------------+            +--------------------+
|  MineMart.Gameplay |            |    MineMart.UI     |
| (Stalls, Workers,  |            | (HUD, Joystick,    |
|  CounterBuffer)    |            |  Settings Toggle)  |
+--------------------+            +--------------------+
        │                                  │
        ▼                                  ▼
+--------------------+            +--------------------+
|   MineMart.Data    |            | MineMart.Economy   |
| (SO Data Schemas,  |◄───────────┤ (Save/Load JSON,   |
|  Save Data DTOs)   |            |  AFK Math Engine)  |
+--------------------+            +--------------------+
        │
        ▼
+--------------------+
|   MineMart.Core    |
| (Event Channels,   |
|  Math Formatter)   |
+--------------------+
```

---

## 2. Lõi Dữ Liệu ScriptableObject (Config Schemas)

Sử dụng `ScriptableObject` để Designer có thể dễ dàng cân bằng game mà không cần mở code. Ở bản MVP, chúng ta chỉ cần 2 Schema cốt lõi:

### 2.1 `BlockDataSO` (Dữ Liệu Khối Nguyên Liệu)
Định nghĩa thông số cơ bản của 1 khối lượng (Đất, Gỗ, Đá).
```csharp
namespace MineMart.Data
{
    [CreateAssetMenu(fileName = "Block_", menuName = "MineMart/MVP/Block Data")]
    public class BlockDataSO : ScriptableObject
    {
        public string blockId;
        public string displayName;
        public double baseValue = 1.0;
        public float baseCraftTime = 1.5f;
        public Sprite icon;
        public GameObject prefab3D;
    }
}
```

### 2.2 `StallDataSO` (Dữ Liệu Quầy Nghề)
Cấu hình giá trị kinh tế và các mốc thưởng đột phá.
```csharp
namespace MineMart.Data
{
    [CreateAssetMenu(fileName = "Stall_", menuName = "MineMart/MVP/Stall Data")]
    public class StallDataSO : ScriptableObject
    {
        [Header("Identity")]
        public string stallId;
        public BlockDataSO producedBlock;
        
        [Header("Economics")]
        public double buyPrice;
        public double baseUpgradePrice;
        public float priceMultiplier = 1.15f; // Cost(L) = base * (1.15)^(L-1)

        [Header("Milestones (10, 25, 50, 100)")]
        public List<MilestoneRuleSO> milestoneRules;
        
        public double CalculateUpgradeCost(int currentLevel) => 
            baseUpgradePrice * System.Math.Pow(priceMultiplier, currentLevel - 1);
    }
}
```

---

## 3. Thuật Toán Cốt Lõi MVP (Core Algorithms)

### 3.1 Hệ Thống Quầy Trung Chuyển (Pass-through Counter & Buffer)
Quầy trung chuyển ngăn cách hậu trường và mặt tiền. Mỗi `CounterSlot` chứa được tối đa 4 block xếp thành hình kim tự tháp tam giác.
- **Biểu diễn Dữ liệu**: `List<GameObject>[] _slotStacks = new List<GameObject>[MAX_SLOTS];`
- **Thuật toán Xếp hàng**:
  1. Nếu có ô trống hoàn toàn $\rightarrow$ Xếp vào.
  2. Nếu không có ô trống $\rightarrow$ Tìm ô chứa ĐÚNG loại Block đó và có số lượng $< 4$.
  3. Tính toán Vector3 tọa độ của block dựa trên vị trí Index (0, 1, 2 dưới đáy; 3 trên đỉnh).
- **Ràng buộc Hướng đứng**: Phương thức `TryPlaceBlock` chỉ cho phép gọi từ `PlaceAnchor` (Phía Mỏ). `TryTakeBlock` chỉ gọi được từ `TakeAnchor` (Phía Shop).

### 3.2 Bộ Điều Phối Đơn Hàng & Thợ Dân Làng (Autonomous Task Dispatcher)
Quản lý sự tương tác tự động của Dân làng và Trạm mỏ.
- **OrderManager**: Quản lý `Queue<OrderTicket>` theo quy tắc FIFO.
- Khi khách đến `CustomerCounter`, đẩy 1 `OrderTicket(BlockId)` vào hàng đợi.
- Dân làng (NPC AI) liên tục gọi `TaskDispatcher.GetNextTask()`. 
- **Distance Tie-breaker**: Nếu có 2 đơn hàng, chọn đơn có `Stall` gần với vị trí của Dân làng nhất (tính bằng khoảng cách Euclid).
- Dân làng dùng `Physics.IgnoreLayerCollision` để đi xuyên nhau không bị kẹt góc.

### 3.3 Hệ Thống Điều Khiển Lai (Hybrid Control & Magnet Pop-Snap)
Người chơi trực tiếp điều khiển Steve (Player) bằng Floating Joystick, có cơ chế hút nam châm.
- **Floating Joystick**: Khởi tạo tâm ảo (Dynamic Origin) ngay tại điểm chạm trên nửa dưới màn hình.
- **Magnet Pop-Snap**:
  ```csharp
  // Khi ở gần mỏ có order chờ:
  if (Vector3.Distance(player.Position, minerAnchor.Position) <= 1.6f && stall.NeedsMining) {
      player.SnapTo(minerAnchor.Position);
      player.LookAt(stall.transform);
      player.LockMovement();
      player.PlaySquashAndStretch();
      stall.StartMiningCycle(OnMiningComplete);
  }
  ```
- **Action Lock**: Biến `isMovementLocked = true` trong suốt thời gian `MiningTimer < BaseCraftTime`. Khi hoàn thành, gọi `UnlockMovement()` và đính block lên `CarryAnchor` trên đầu nhân vật.

---

## 4. Kiến Trúc Sự Kiện (Decoupled Event Channels)

Sử dụng kiến trúc không phụ thuộc để liên kết các hệ thống rời rạc.

- `OrderDeliveredEventChannelSO`: Kích hoạt khi giao hàng thẳng tay cho khách.
  - **Payload**: `StallId`, `BaseValue`, `TipAnchorPosition`.
  - **Listener 1**: `EconomyManager` (Cộng tiền gốc vào số dư Vàng).
  - **Listener 2**: `JuiceManager` (Sinh ra hiệu ứng bọc Tiền Tip tại `TipAnchorPosition` để người chơi chạy lại nhặt).

---

## 5. Dữ Liệu Lưu Trữ Cục Bộ (Save Engine & AFK Math)

Lưu trữ theo chuẩn JSON, serialize bằng lớp `SaveDataDTO`. Không dính dáng đến cấu trúc MonoBehaviour.

### 5.1 Cấu Trúc `SaveDataDTO`
```csharp
[System.Serializable]
public class SaveDataDTO
{
    public int saveVersion = 1;
    public long lastOfflineTimestamp;
    
    public SettingsDTO settings = new SettingsDTO();
    public StageStateDTO currentStage = new StageStateDTO();
}

[System.Serializable]
public class StageStateDTO
{
    public string stageGold = "0"; // Dùng chuỗi để tránh tràn số lớn
    public List<StallSaveDTO> stallManagerMap = new List<StallSaveDTO>();
    public List<string> stageUpgradesPurchased = new List<string>();
}
```

### 5.2 Thuật Toán AFK (Offline Earnings)
Chạy khi khởi động lại game ở màn hình chào mừng:
```csharp
long deltaSeconds = CurrentTime - lastOfflineTimestamp;
long maxAFKSeconds = 7200; // 2 giờ trong bản MVP
long validSeconds = Math.Min(deltaSeconds, maxAFKSeconds);

double totalOfflineGold = 0;
foreach(var stall in ActiveStalls) {
    double goldPerSec = (stall.CurrentRevenue / stall.CurrentCraftTime);
    totalOfflineGold += (goldPerSec * validSeconds) * 0.5f; // Hệ số phạt 50%
}
// Show Popup và cộng totalOfflineGold vào Ví
```

---

## 6. Sơ Đồ Trách Nhiệm Lớp (Class Responsibilities)

1. **`StallController`**: Quản lý State của 1 quầy (Locked, Idle, Mining). Tính toán giá nâng cấp, apply các mốc Milestone.
2. **`OrderManager`**: Thùng chứa FIFO toàn cục, nhận đơn từ khách và phân bổ cho mỏ tương ứng.
3. **`PlayerController`**: Đọc Input từ Joystick, xử lý va chạm với các Anchor (Mỏ, Quầy Trung Chuyển, Quầy Khách) và chứa block trên đầu.
4. **`CustomerAgent`**: Tìm ô `CustomerSlot` trống, đứng chờ, thả tiền Tip, nhận block, và rời đi.
5. **`WorkerAgent`**: AI Dân làng tự động tìm mỏ, đào, mang block ra quầy trung chuyển.
6. **`EconomyManager`**: Xử lý Ví Vàng `double`, Format số lớn thành "1.25M", xử lý trừ tiền khi nâng cấp.
7. **`SaveManager`**: Chạy ngầm `BackgroundThreadAsync` mỗi 30s để serialize ra file JSON.

---
*Tài liệu giới hạn cho đúng đặc tả của bản GDD MVP. Các hệ thống mở rộng sẽ được thiết kế trong bản Master TDD.*
