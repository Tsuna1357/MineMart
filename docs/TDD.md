---
title: "MineMart: Idle Block Tycoon — Master Technical Design Document"
subtitle: "High-Performance ScriptableObject Architecture & Production Implementation Specification"
version: "1.0.0-PROD"
date: "2026-09-25"
author: "AI Technical Architect & Miyuki"
status: "Approved Master Technical Architecture"
unity_version: "6.2.0f1 (Unity 6)"
render_pipeline: "URP (Universal Render Pipeline)"
scripting_backend: "IL2CPP"
architecture_pattern: "so-first (ScriptableObject-Driven & Event-Decoupled)"
---

# MineMart: Idle Block Tycoon — Master Technical Design Document

> **Tài Liệu Thiết Kế Kỹ Thuật Tổng Thể (Master TDD)**  
> *Đặc tả toàn diện kiến trúc phân tầng Assembly, cấu trúc dữ liệu 100% Data-Driven (ScriptableObjects), các thuật toán cốt lõi, cơ chế tối ưu hóa hiệu năng Voxel và lộ trình thi công kỹ thuật từ Phase 1 (Micro-MVP) đến Master Game.*

---

## 1. Phân Tầng Assembly & Cấu Trúc Thư Mục (Assembly Architecture)

### 1.1 Sơ Đồ Phụ Thuộc Assembly Definitions (.asmdef)
Dự án được phân chia thành các Assembly độc lập tuân thủ nguyên tắc phụ thuộc một chiều (Strict Unidirectional Dependency), triệt tiêu hoàn toàn phụ thuộc vòng (Cyclic Dependency) và tối ưu hóa thời gian biên dịch (Compilation Time $< 2\text{s}$):

```
+-----------------------------------------------------------------------------------+
|                                  MineMart.UI                                      |
|                 (UIToolkit MVVM, HUD Binders, Floating Joystick)                  |
+-----------------------------------------------------------------------------------+
        │                                  │                                │
        ▼                                  ▼                                ▼
+--------------------+            +--------------------+            +--------------------+
|  MineMart.Gameplay |            |  MineMart.Economy  |            |   MineMart.Feel    |
| (Stalls, Workers,  |            | (Economy, Upgrades,|            | (DOTween Juice,    |
|  CounterBuffer)    |            |  Save/Load Engine) |            |  Audio, Cam Shake) |
+--------------------+            +--------------------+            +--------------------+
        │                                  │                                │
        +──────────────────────────────────┼────────────────────────────────+
                                           │
                                           ▼
                                 +--------------------+
                                 |   MineMart.Data    |
                                 |  (SO Data Schemas, |
                                 |   Save Data DTOs)  |
                                 +--------------------+
                                           │
                                           ▼
                                 +--------------------+
                                 |   MineMart.Core    |
                                 |  (SO Event Channels|
                                 |   Constants, Math) |
                                 +--------------------+
```

### 1.2 Cấu Trúc Thư Mục Chuẩn Feature-Based (Folder Strategy)

```
Assets/_Project/
├── Core/
│   ├── MineMart.Core.asmdef
│   ├── Events/               # ScriptableObject Event Channels (Void, Order, Economy)
│   ├── Constants/            # GameConstants.cs, LayerMasks.cs
│   └── Math/                 # NumberFormatter.cs (double notation)
├── Data/
│   ├── MineMart.Data.asmdef
│   ├── Schemas/              # BlockDataSO, StallDataSO, BiomeDataSO, EquipmentDataSO
│   └── Persistence/          # SaveDataDTO.cs, SerializationSurrogates
├── Features/
│   ├── Stalls/               # StallController.cs, JobSiteBlock.cs
│   ├── StackingBuffer/       # CounterBuffer.cs, BlockTower.cs
│   ├── Workers/              # WorkerAgent.cs, TaskDispatcher.cs
│   ├── Customers/            # CustomerAgent.cs, CustomerSpawner.cs
│   ├── HybridControl/        # HybridInputController.cs, FloatingJoystick.cs
│   ├── Economy/              # EconomyManager.cs, UpgradeManager.cs, OfflineAFKEngine.cs
│   ├── Equipment/            # EquipmentManager.cs, EquipmentStatCache.cs
│   ├── GuestBook/            # GuestBookManager.cs
│   └── Feel/                 # JuiceManager.cs, AudioPoolManager.cs, CameraShakeController.cs
├── UI/
│   ├── MineMart.UI.asmdef
│   ├── Documents/            # *.uxml files (HUD, Settings, UpgradeMenu)
│   ├── Styles/               # *.uss files
│   └── Binders/              # MainHudBinder.cs, SettingsModalBinder.cs
└── Tests/
    ├── EditMode/             # EconomicMathTests.cs, NumberFormatterTests.cs
    └── PlayMode/             # StackingBufferTests.cs, TaskDispatcherTests.cs
```

---

## 2. Hệ Thống Dữ Liệu 100% Data-Driven (ScriptableObject Schemas)

Mọi chỉ số, giá bán, thời gian và công thức đều được định nghĩa qua `ScriptableObject`, không hardcode vào MonoBehaviour:

### 2.1 Schema Dữ Liệu Quầy & Block

```csharp
namespace MineMart.Data
{
    [CreateAssetMenu(fileName = "Block_", menuName = "MineMart/Data/Block Data")]
    public class BlockDataSO : ScriptableObject
    {
        [SerializeField] private string _blockId;
        [SerializeField] private string _displayName;
        [SerializeField] private double _baseValue = 1.0;
        [SerializeField] private float _baseCraftTime = 1.5f;
        [SerializeField] private Sprite _icon;
        [SerializeField] private GameObject _prefab3D;

        public string BlockId => _blockId;
        public string DisplayName => _displayName;
        public double BaseValue => _baseValue;
        public float BaseCraftTime => _baseCraftTime;
        public Sprite Icon => _icon;
        public GameObject Prefab3D => _prefab3D;
    }

    [CreateAssetMenu(fileName = "Stall_", menuName = "MineMart/Data/Stall Data")]
    public class StallDataSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _stallId;
        [SerializeField] private string _stallName;
        [SerializeField] private BlockDataSO _producedBlock;

        [Header("Economic Base Numbers")]
        [SerializeField] private double _buyPrice;
        [SerializeField] private double _baseUpgradePrice = 5.0;
        [SerializeField] private float _priceMultiplier = 1.15f;

        [Header("Milestones")]
        [SerializeField] private List<MilestoneRuleSO> _milestoneRules;

        public string StallId => _stallId;
        public string StallName => _stallName;
        public BlockDataSO ProducedBlock => _producedBlock;
        public double BuyPrice => _buyPrice;
        public double BaseUpgradePrice => _baseUpgradePrice;
        public float PriceMultiplier => _priceMultiplier;
        public IReadOnlyList<MilestoneRuleSO> MilestoneRules => _milestoneRules;

        public double CalculateUpgradeCost(int currentLevel)
        {
            return _baseUpgradePrice * System.Math.Pow(_priceMultiplier, currentLevel - 1);
        }
    }
}
```

### 2.2 Schema Trang Bị Đơn Nhất & Chỉ Số Tự Do (Freeform RPG Gear)

```csharp
namespace MineMart.Data
{
    public enum EquipmentSlot { MainHand, Helmet, Pet, Chestplate, OffHand, Leggings, Boots }
    public enum StatType { AllProfitMultiplier, WalkSpeed, CraftSpeed, InstantCraftChance, DoubleValueChance, TipBonusRate }

    [System.Serializable]
    public struct StatModifier
    {
        public StatType statType;
        public float value; // Ví dụ: 0.25f tương đương +25%
    }

    [CreateAssetMenu(fileName = "Equip_", menuName = "MineMart/Data/Equipment Data")]
    public class EquipmentDataSO : ScriptableObject
    {
        [SerializeField] private string _itemId;
        [SerializeField] private string _itemName;
        [SerializeField] private EquipmentSlot _slot;
        [SerializeField] private Sprite _icon;
        [SerializeField] private List<StatModifier> _modifiers;

        public string ItemId => _itemId;
        public EquipmentSlot Slot => _slot;
        public IReadOnlyList<StatModifier> Modifiers => _modifiers;
    }
}
```

---

## 3. Kiến Trúc Sự Kiện Rời Rạc (ScriptableObject Event Channels)

Hệ thống giao tiếp hoàn toàn không phụ thuộc (Decoupled) qua các kênh sự kiện ScriptableObject:

```csharp
namespace MineMart.Core.Events
{
    [CreateAssetMenu(fileName = "Event_OrderDelivered", menuName = "MineMart/Events/Order Delivered Channel")]
    public class OrderDeliveredEventChannelSO : ScriptableObject
    {
        public event System.Action<OrderDeliveryPayload> OnOrderDelivered;

        public void RaiseEvent(OrderDeliveryPayload payload)
        {
            OnOrderDelivered?.Invoke(payload);
        }
    }

    public struct OrderDeliveryPayload
    {
        public string stallId;
        public string blockId;
        public double basePrice;
        public Vector3 deliveryPosition;
        public bool isVipCustomer;
    }
}
```

**Nguyên tắc vận hành**:
- Khi khách nhận hàng: `StallController` gọi `_orderDeliveredChannel.RaiseEvent(payload)`.
- `EconomyManager` lắng nghe $\rightarrow$ áp dụng công thức $Total = BasePrice \times Level \times MilestoneBuff \times EquipmentCache$.
- `JuiceManager` lắng nghe $\rightarrow$ sinh đồng xu nảy Bézier và phát âm thanh leng keng.

---

## 4. Các Thuật Toán Cốt Lõi & Lớp Kỹ Thuật (Core Engineering Algorithms)

### 4.1 Thuật Toán Xử Lý Số Lớn & Rút Gọn Hiển Thị (Large Number Formatter)
Triệt tiêu hoàn toàn lỗi tràn số nguyên bằng kiểu `double` và bộ chuẩn hóa chuỗi tĩnh:

```csharp
namespace MineMart.Core.Math
{
    public static class NumberFormatter
    {
        private static readonly string[] Suffixes = 
        { 
            "", "K", "M", "B", "T", "aa", "ab", "ac", "ad", "ae", "af", 
            "ag", "ah", "ai", "aj", "ak", "al", "am", "an", "ao", "ap" 
        };

        public static string Format(double value)
        {
            if (value < 0) return "-" + Format(-value);
            if (value < 1000.0) return System.Math.Floor(value).ToString("F0");

            int exp = (int)(System.Math.Log10(value) / 3);
            if (exp >= Suffixes.Length) exp = Suffixes.Length - 1;

            double normalized = value / System.Math.Pow(10, exp * 3);
            return $"{normalized:F2}{Suffixes[exp]}";
        }
    }
}
```

---

### 4.2 Thuật Toán Bộ Đệm Quầy & Xếp Chồng Block 3D (Counter Stacking Buffer)

```csharp
namespace MineMart.Gameplay.StackingBuffer
{
    public class CounterBuffer : MonoBehaviour
    {
        private const int MAX_SLOTS = 3;
        private const int MAX_STACK_HEIGHT = 5;

        // Quản lý 3 cọc hàng
        private readonly List<GameObject>[] _slotStacks = new List<GameObject>[MAX_SLOTS];

        public bool TryPlaceBlock(GameObject blockInstance, out Vector3 targetPosition)
        {
            // Bước 1: Ưu tiên ô trống hoàn toàn
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                if (_slotStacks[i].Count == 0)
                {
                    _slotStacks[i].Add(blockInstance);
                    targetPosition = GetSlotAnchorPosition(i, 0);
                    return true;
                }
            }

            // Bước 2: Tìm cọc hàng thấp nhất còn chỗ (< 5 block)
            int lowestSlot = -1;
            int minHeight = MAX_STACK_HEIGHT;

            for (int i = 0; i < MAX_SLOTS; i++)
            {
                if (_slotStacks[i].Count < minHeight)
                {
                    minHeight = _slotStacks[i].Count;
                    lowestSlot = i;
                }
            }

            if (lowestSlot != -1)
            {
                int currentHeight = _slotStacks[lowestSlot].Count;
                _slotStacks[lowestSlot].Add(blockInstance);
                targetPosition = GetSlotAnchorPosition(lowestSlot, currentHeight);
                return true;
            }

            targetPosition = Vector3.zero;
            return false; // Hết sạch chỗ chứa (Full 15 block)
        }
    }
}
```

---

### 4.3 Thuật Toán Điều Phối Thợ Dân Làng (Task Dispatcher)
- Hàng đợi `Queue<CraftingTask>` tiếp nhận đơn theo chuẩn **FIFO**.
- Khi một thợ rảnh, `TaskDispatcher` tìm kiếm đơn hàng chưa nhận và chọn quầy có cự ly Euclid gần thợ nhất:
  $$\text{SelectedTask} = \arg\min_{T \in \text{PendingTasks}} \|\text{WorkerPosition} - \text{StallPosition}_T\|$$
- Dân làng sử dụng Unity `NavMeshAgent` hoặc Simple Transform Translation với `Physics.IgnoreLayerCollision(WorkerLayer, WorkerLayer, true)` để di chuyển xuyên thấu mượt mà.

---

### 4.4 Tối Ưu Hóa Cache Chỉ Số Trang Bị (Dirty Flag Stat Cache)
Tối ưu hóa $O(1)$ khi giao hàng, loại bỏ việc duyệt mảng 7 món đồ liên tục:

```csharp
namespace MineMart.Economy
{
    public class EquipmentStatCache : MonoBehaviour
    {
        [SerializeField] private EquipmentChangedEventChannelSO _equipmentChangedEvent;

        public float TotalAllProfitMultiplier { get; private set; } = 1.0f;
        public float TotalWalkSpeedMultiplier { get; private set; } = 1.0f;
        public float TotalInstantCraftChance { get; private set; } = 0.0f;

        private void OnEnable()
        {
            _equipmentChangedEvent.OnEventRaised += RebuildCache;
        }

        private void RebuildCache()
        {
            // Chỉ chạy 1 lần duy nhất khi người chơi Mặc / Tháo / Nâng cấp đồ
            float profitBonus = 0f;
            float speedBonus = 0f;

            foreach (var item in InventoryManager.EquippedItems)
            {
                foreach (var mod in item.Data.Modifiers)
                {
                    if (mod.statType == StatType.AllProfitMultiplier) profitBonus += mod.value;
                    if (mod.statType == StatType.WalkSpeed) speedBonus += mod.value;
                }
            }

            TotalAllProfitMultiplier = 1.0f + profitBonus;
            TotalWalkSpeedMultiplier = 1.0f + speedBonus;
        }
    }
}
```

---

### 4.5 Tối Ưu Hóa Hiệu Năng Voxel (GPU Instancing & Object Pooling)
- **Object Pooling**: Sử dụng `UnityEngine.Pool.ObjectPool<GameObject>` quản lý riêng cho từng loại Block (Đất, Gỗ, Đá) và Coin.
- **GPU Instancing Batching**: Khối Block 3D sử dụng chung 1 Material Texture Atlas duy nhất. Bật cờ `material.enableInstancing = true;` để đảm bảo 150 khối block trên màn hình được render trong **tối đa 2 Draw Calls**.

---

## 5. Kiến Trúc Lưu Trữ Cục Bộ (Local Persistence & Save Engine)

### 5.1 Cấu Trúc DTO Lưu Trữ (`SaveDataDTO.cs`)

```csharp
namespace MineMart.Data.Persistence
{
    [System.Serializable]
    public class SaveDataDTO
    {
        public int saveVersion = 2;
        public long lastOfflineTimestamp;

        public GlobalProfileDTO globalProfile = new GlobalProfileDTO();
        public StageStateDTO currentStage = new StageStateDTO();
    }

    [System.Serializable]
    public class GlobalProfileDTO
    {
        public int emeralds = 0;
        public int piggyBankEmeralds = 0;
        public bool isJoystickEnabled = true;
        public bool isHapticsEnabled = true;
        public float masterVolume = 0.8f;
        public float sfxVolume = 1.0f;
        public List<string> unlockedGearSlots = new List<string> { "MainHand", "Helmet", "Pet" };
        public List<string> guestBook = new List<string>();
        public Dictionary<string, int> emeraldVaultLevels = new Dictionary<string, int>();
    }

    [System.Serializable]
    public class StageStateDTO
    {
        public string stageId = "stage_01_pocket_workshop";
        public string stageGold = "0"; // Lưu String tránh lỗi overflow JSON
        public List<StallSaveDTO> stallManagerMap = new List<StallSaveDTO>();
        public List<string> stageUpgradesPurchased = new List<string>();
    }

    [System.Serializable]
    public class StallSaveDTO
    {
        public string id_stall;
        public bool stall_Unlocked;
        public int stall_level;
    }
}
```

### 5.2 Cơ Chế Lưu Ngầm & Tính Tiền Ngoại Tuyến (SaveManager)
- **Lưu trữ ngầm phi chặn (Async Non-blocking)**: Sử dụng `Awaitable.BackgroundThreadAsync()` trong Unity 6 để ghi file JSON ra `Application.persistentDataPath/savegame.json` mà không gây drop dù chỉ 1 frame.
- **Thuật toán tính tiền AFK O(1)**:
  $$\text{OfflineGold} = \sum_{i=1}^{n} \left(\frac{\text{Revenue}_i}{\text{CraftTime}_i}\right) \times \min(\Delta t, \text{MaxSeconds}) \times 0.5$$

---

## 6. Lộ Trình Triển Khai Kỹ Thuật (Production Phasing)

```
+-----------------------------------------------------------------------------------+
| PHASE 1: MICRO-MVP / POCKET WORKSHOP (Deadline 01/10/2026 - Bài Assignment)       |
| • Hoàn thiện MineMart.Core, MineMart.Data (BlockData, StallData).                 |
| • Xây dựng 3 Quầy nghề: Đất, Gỗ, Đá trên 1 màn hình duy nhất.                    |
| • Triển khai CounterBuffer (Xếp chồng 3D) & Floating Joystick có nút Bật/Tắt.    |
| • Triển khai SaveManager (JSON Local Storage) & Công thức AFK Engine.             |
+-----------------------------------------------------------------------------------+
                                         │
                                         ▼
+-----------------------------------------------------------------------------------+
| PHASE 2: BIOME EXPANSION & RECIPES (Mở Rộng Sa Mạc & Dây Chuyền Ghép Block)       |
| • Kích hoạt Biome Sa Mạc & Hang Khai Khoáng (BiomeDataSO).                        |
| • Triển khai CraftingRecipeSO (Đuốc = Gậy + Than).                                |
| • Triển khai Sổ Tay Dân Làng VIP (GuestBookManager).                              |
+-----------------------------------------------------------------------------------+
                                         │
                                         ▼
+-----------------------------------------------------------------------------------+
| PHASE 3: FULL MASTER META (7 Slot RPG, Pet Nhặt Tip, Emerald Vault & LiveOps)     |
| • Hoàn thiện mở khóa lũy tiến 7 Slot Trang Bị & Bàn Phù Phép.                    |
| • Tích hợp AI Thú Cưng tự nhặt Tip tiền boa trên sàn.                             |
| • Tích hợp Heo Đất Phá Băng ($0.99 Piggy Bank) & Lịch Trình LiveOps Cuối Tuần.   |
+-----------------------------------------------------------------------------------+
```

---

## 7. Kế Hoạch Kiểm Thử Kỹ Thuật (NUnit Automated Test Matrix)

| Test Fixture | Phạm Vi Kiểm Thử | Điều Kiện Đạt (Pass Criteria) |
|---|---|---|
| **NumberFormatterTests** | Test độ lớn `double` từ $10^2$ đến $10^{60}$ | Trả về đúng hậu tố `1.25M`, `45.80B`, `12.00aa` không bị tràn chuỗi. |
| **StallUpgradeCostTests** | Kiểm tra công thức tăng giá $Cost = Base \times 1.15^{L-1}$ | Cấp 1 $\rightarrow$ Cấp 25 giá trị tăng chính xác theo hàm mũ, không âm. |
| **CounterBufferTests** | Mô phỏng xếp hàng 20 block liên tiếp vào quầy | 3 block đầu vào 3 ô trống; từ block 4 bắt đầu xếp chồng; tối đa 15 block thì báo đầy. |
| **OfflineAFKCalcTests** | Mô phỏng người chơi thoát game vắng mặt 10 giờ | Chỉ tính tối đa $7200\text{s}$ (2 giờ), kết quả doanh thu nhân đúng hệ số $0.5$. |
| **StatCacheDirtyFlagTests**| Thay đổi trang bị và kiểm tra số lần tính toán | Hàm `RebuildCache()` chỉ chạy đúng 1 lần khi bắn event, truy xuất biến cache mất $0\text{ms}$. |

---

*Hết tài liệu Master Technical Design Document.*
