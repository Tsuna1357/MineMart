# KỊCH BẢN THUYẾT TRÌNH & BỐ CỤC SLIDE BẢO VỆ DỰ ÁN: MINEMART (BẢN CHÍNH THỨC)

> **Dự án**: MineMart: Pocket Workshop  
> **Thời lượng tổng**: 10 Phút (3' Slide + 6' Demo + 1' Tương lai & Cảm ơn)  
> **Trọng số chấm điểm**: Core Loop & Game Feel (35%) | Art, Audio & Sáng tạo (25%) | Ứng dụng AI (20%) | Thuyết trình (10%) | Bình chọn (10%)

---

## PHẦN 1: KHUNG NỘI DUNG SLIDE (DÀNH CHO CANVA)
*Tiêu chí Canva: Tối giản chữ (chỉ để từ khóa/số liệu cốt lõi), hình ảnh chiếm 65-75% diện tích, màu sắc chủ đạo Voxel Pastel / Xanh lá - Vàng Gold năng động.*

```
Palette gợi ý trên Canva:
- Màu nền (Background): Trắng ngà kem (#FDFBF7) hoặc Xanh Slate tối (#1E232A)
- Màu nhấn (Accent): Vàng Gold tiền xu (#FFB800) & Xanh Ngọc Emerald (#00D26A)
- Font chữ Canva khuyên dùng: Montserrat / Be Vietnam Pro (Hiện đại, nét đậm rõ ràng)
```

---

### SLIDE 1: COVER & THÀNH VIÊN NHÓM (30 giây)
- **Bố cục (Layout)**: 
  - Bên trái (40%): Tên dự án lớn, Slogan, Logo nhóm & Thành viên.
  - Bên phải (60%): Hình ảnh Poster MineMart hoặc Render 3D Chibi Voxel siêu nét.
- **Nội dung chữ trên Slide**:
  - **MINEMART: POCKET WORKSHOP**
  - *Single-Screen Arcade Idle Tycoon*
  - **Đội ngũ phát triển**:
    - **Lâm Tuyết Nhi (Miyuki)** — *Trưởng nhóm / 3D Voxel Artist & Unity Developer*
    - *(Điền tên thành viên khác nếu có)*
  - **Giảng viên hướng dẫn**: *(Điền tên thầy cô)*
  - **Đơn vị đào tạo**: Cao đẳng FPT Polytechnic
- **Hình ảnh cần dùng**:
  - Ảnh Poster chính: `docs/MineMart_Poster_A2.png` hoặc Icon chính thức `docs/MineMart_Icon.jpg`.

---

### SLIDE 2: CẢM HƯNG "THỰC HỌC - THỰC NGHIỆP" & NĂNG LỰC BLOCKBENCH (45 giây)
- **Bố cục (Layout)**: 2 cột đối xứng (Khát vọng & Năng lực thực thi).
  - Cột 1: *Triết lý FPT Polytechnic & Mục tiêu Dự án*.
  - Cột 2: *Thế mạnh 3D Handcrafted Blockbench*.
- **Nội dung chữ trên Slide**:
  - **Cảm hứng: Triết lý "Thực học - Thực nghiệp" (FPT Polytechnic)**
    - Dốc 100% sức lực để đo lường giới hạn năng lực bản thân.
    - Không dừng lại ở đồ án môn học — Mục tiêu kiên định là **phát hành thực tế** để đón nhận phản hồi từ thị trường xã hội.
  - **Thế mạnh thực thi: 100% 3D Voxel Handcrafted**
    - Tận dụng bề dày kinh nghiệm tạo dựng mô hình 3D và vẽ texture pixel bằng **Blockbench**.
    - Tối ưu hóa Low-Poly, phong cách Voxel Chibi ấm cúng, 60 FPS mượt mà trên di động.
- **Hình ảnh cần dùng**:
  - Ảnh 1: Logo Cao đẳng FPT Polytechnic.
  - Ảnh 2: Ảnh chụp màn hình phần mềm **Blockbench** đang mở model nhân vật/quầy mỏ của em.

---

### SLIDE 3: TỔNG QUAN GAME & CƠ CHẾ IDLE LAI (60 giây)
- **Bố cục (Layout)**: 3 Thẻ Card nổi bật kèm icon minh họa.
- **Nội dung chữ trên Slide**:
  - **Tiêu đề**: **BẢN SẮC GAMEPLAY: HYBRID ARCADE IDLE**
  - **Card 1: 1 Màn Hình Tĩnh (Single-Screen)**
    - Góc nhìn Isometric 2.5D bao quát toàn bộ xưởng sản xuất: Trạm mỏ $\rightarrow$ Quầy tiếp khách / Bàn phục vụ $\rightarrow$ Thu tiền tip.
  - **Card 2: Can Thiệp Tay (Active Value)**
    - Kéo Joystick: Tốc độ đào x2, nhặt tip +20%. Cơ chế nhấc quầy để tái quy hoạch không gian xưởng.
  - **Card 3: Tự Động Hóa (Auto-Pilot)**
    - Buông tay: Nhân vật tự hành làm việc cùng công nhân, dáng đi lắc lư (waddling) cưng xỉu, sinh lời ngoại tuyến.
- **Hình ảnh cần dùng**:
  - Ảnh chụp toàn cảnh Scene trong Unity (`Plain_Map.unity`) đang có đầy đủ các trạm mỏ, quầy đón khách/bàn và nhân vật.

---

### SLIDE 4: ỨNG DỤNG AI ĐỒNG HÀNH (45 giây)
- **Bố cục (Layout)**: Sơ đồ 3 nhánh hợp tác Người - AI.
- **Nội dung chữ trên Slide**:
  - **Tiêu đề**: **AI CO-PILOT: TỐI ƯU TOÀN DIỆN VÒNG ĐỜI SẢN PHẨM**
  - **1. Game Design & Logic (30%)**
    - Biên soạn GDD/GFD chuẩn công nghiệp; thuật toán điều phối đơn hàng FIFO Queue $O(1)$.
  - **2. Game Feel & Toán Học Chuyển Động (40%)**
    - Tính toán chuyển động lắc lư (Procedural Waddle), hiệu ứng đàn hồi nảy Squash & Stretch.
  - **3. Tooling & Tối Ưu Hiệu Năng (30%)**
    - Custom Editor sinh Milestone Slider trong Scene View; Zero-GC World Space UI Pooling.
- **Hình ảnh cần dùng**:
  - Ảnh chụp màn hình Inspector component `StationUpgradeBubble` có nút sinh Milestone Bar hoặc sơ đồ luồng dữ liệu.

---

## PHẦN 2: KỊCH BẢN THUYẾT TRÌNH (SPEECH SCRIPT - 10 PHÚT)
*Phân bổ thời gian chính xác từng giây. Hãy giữ phong thái tự tin, mỉm cười và nói với tốc độ vừa phải.*

---

### [0:00 - 0:30] MỞ ĐẦU & GIỚI THIỆU NHÓM (Slide 1)
> *"Kính chào quý thầy cô trong Hội đồng phản biện và toàn thể các bạn sinh viên.  
> Em tên là **Lâm Tuyết Nhi**, đại diện cho nhóm phát triển dự án hôm nay.  
> Đến với buổi bảo vệ hôm nay, nhóm em xin trân trọng giới thiệu tựa game: **MineMart: Pocket Workshop** — một tựa game Arcade Idle Tycoon thu nhỏ được vận hành hoàn toàn trên một màn hình dọc tĩnh."*

---

### [0:30 - 1:15] NGUỒN CẢM HƯNG "THỰC HỌC - THỰC NGHIỆP" & NĂNG LỰC BLOCKBENCH (Slide 2)
> *"Thưa thầy cô, câu nói **'Thực học - Thực nghiệp'** của trường Cao đẳng FPT Polytechnic chính là nguồn cảm hứng lớn nhất dẫn dắt em trong suốt quá trình xây dựng dự án này.  
> Với em, dự án này không chỉ đơn thuần là một bài tập để lấy điểm, mà là nơi em **dốc hết 100% sức mình** để đo lường xem bản thân đã đi được đến đâu, năng lực thực tế của mình đạt tới ngưỡng nào. Và mục tiêu kiên định của em là **chắc chắn sẽ phát hành trò chơi này ra thị trường thực tế**, để lắng nghe chính xác xã hội và người chơi bên ngoài đón nhận, phản hồi về sản phẩm của mình ra sao.  
> Để hiện thực hóa mục tiêu đó với chất lượng cao nhất, em đã tận dụng thế mạnh và kinh nghiệm sẵn có của mình trong việc tạo dựng **mô hình 3D bằng Blockbench** để tự tay nhào nặn toàn bộ nhân vật, quầy hàng và thế giới Voxel độc bản cho game."*

---

### [1:15 - 2:15] BẢN SẮC GAMEPLAY & YẾU TỐ IDLE (Slide 3)
> *"Về bản sắc trò chơi: Khác với các tựa game Idle thông thường chỉ bấm nâng cấp thụ động, **MineMart** mang cơ chế **Hybrid Arcade Idle**:  
> - **Khi người chơi muốn chủ động (Active)**: Chạm tay kéo cần Joystick, nhân vật chính sẽ trở thành 'thợ cả' với tốc độ đào nhanh gấp đôi thợ AI, nhặt tiền boa được thưởng thêm +20%. Người chơi có toàn quyền nhấc các quầy mỏ lên để quy hoạch và sắp xếp lại không gian xưởng theo ý thích.  
> - **Khi người chơi bận rộn (Pure-Idle)**: Chỉ cần buông tay khỏi màn hình, cơ chế Auto-Pilot sẽ tự động kích hoạt, nhân vật tự đi khai thác và phục vụ cùng công nhân, đảm bảo xưởng luôn sinh lời liên tục."*

---

### [2:15 - 3:00] VAI TRÒ CỦA AI TRONG DỰ ÁN (Slide 4)
> *"Trong dự án này, AI không chỉ là công cụ hỗ trợ gõ code đơn thuần, mà đóng vai trò như một **AI Co-Pilot & Game Designer song hành**:  
> - **Thứ nhất**: AI cùng em chuẩn hóa tài liệu thiết kế GDD, GFD và xây dựng thuật toán điều phối đơn hàng FIFO Queue $O(1)$.  
> - **Thứ hai**: AI hỗ trợ tính toán toán học chuyển động procedural waddling nghiêng lắc lư cực kỳ dễ thương và các hiệu ứng nảy đàn hồi (Squash & Stretch).  
> - **Thứ ba**: Tự động hóa công cụ Editor trong Unity, giúp sinh nhanh và căn chỉnh các thanh tiến trình Milestone ngay trong Scene View chỉ với 1 cú click.  
> Và sau đây, em xin phép chuyển sang phần **Live Demo trực tiếp trên Unity** để chứng minh toàn bộ những điều em vừa trình bày!"*

---

### [3:00 - 9:00] PHẦN DEMO TRỰC TIẾP TRÊN UNITY (6 PHÚT)
*(Lúc này chuyển sang màn hình Game đang chạy trong Unity `Plain_Map`)*

#### Phút 3:00 - 4:30: Chứng minh Main Core Loop Trực Tiếp
- **Hành động**: Đứng quan sát một lượt khách bước vào quầy đón khách hoặc bàn phục vụ.
- **Lời nói**:
  > *"Thưa thầy cô, đây là giao diện chính của MineMart trên một màn hình đơn 2.5D.  
  > **Core Loop** của game vận hành khép kín và trực quan:  
  > 1. Khách Dân làng bước tới **Quầy Phục Vụ Khách** hoặc bàn để gọi món.  
  > 2. Đơn hàng đẩy vào hàng đợi, trạm mỏ Đất hoặc Gỗ tương ứng kích hoạt cần khai thác.  
  > 3. Thợ mỏ hoặc người chơi sau khi đào xong khối block sẽ mang thẳng ra trao tận tay cho khách đang chờ.  
  > 4. Khách nhận hàng, tiền vàng thanh toán gốc bay thẳng vào ví, đồng thời một bọc **Tiền Tip** rơi lại ngay ngắn trên đĩa để người chơi thu gom."*

#### Phút 4:30 - 6:00: Game Feel & Flow State (Can thiệp tay vs Auto-Pilot & Nhấc quầy)
- **Hành động**: 
  1. Thả tay khỏi chuột/phím: Cho nhân vật chính tự động đi làm việc (Auto-Pilot).
  2. Bấm phím điều khiển nhân vật chạy lại mỏ đào: Đào xong mang block ra trao cho khách và nhặt tiền tip.
  3. Thao tác nhấc quầy mỏ lên để dời vị trí: Cho hội đồng thấy cơ chế ngắt quầy và quy hoạch xưởng.
- **Lời nói**:
  > *"Điểm mấu chốt tạo nên **Flow State (Trạng thái cuốn hút không dứt)** nằm ở các yếu tố:  
  > - **Một là Dáng đi lắc lư (Waddling) của Auto-Pilot**: Khi em buông tay, nhân vật tự động tìm việc và có động tác nghiêng người lắc lư cưng xỉu đồng bộ với công nhân.  
  > - **Hai là Giá trị thặng dư khi can thiệp tay**: Khi em tự tay lái nhân vật, tốc độ đào nhanh gấp đôi thợ thường, và khi chạy qua chạm vào bọc tiền Tip trên đĩa sẽ được bonus thêm 20% giá trị.  
  > - **Ba là Cơ chế Nhấc quầy quy hoạch**: Khi muốn sắp xếp lại không gian xưởng hoặc giải tỏa vị trí, người chơi có thể nhấc quầy mỏ lên để ngắt thao tác đào dở dang và di chuyển quầy sang một ô đất thuận tiện hơn."*

#### Phút 6:00 - 7:30: Mỹ Thuật (Blockbench), Âm Thanh & AI Can Thiệp
- **Hành động**: Zoom gần vào nhân vật, quầy hàng, nâng cấp trạm mỏ để hiện Milestone Slider.
- **Lời nói**:
  > *"Về **Mỹ thuật**: Toàn bộ nhân vật từ Steve, Thợ mỏ, Khách hàng nam/nữ đến các quầy gỗ, khối quặng than, sắt, kim cương hồng đều do chính tay em tự dựng mô hình 3D và vẽ texture pixel bằng **Blockbench**. Em đã tối ưu hóa lưới đa giác cực nhẹ để game chạy mượt mà 60fps trên di động.  
  > Về **Âm thanh**: Nhạc nền Piano Felt nhẹ nhàng tạo cảm giác thư giãn, còn tiếng búa gõ và tiếng tiền leng keng được xử lý biến thiên cao độ ngẫu nhiên (Pitch Variation) để người chơi chơi lâu không bao giờ bị mỏi tai.  
  > Về **Dấu ấn AI**: Các hiệu ứng nảy đàn hồi (Squash & Stretch), chữ số tiền bay lên (Floating Cash Text Pooling), và công cụ tạo thanh Milestone Bar trong Unity Editor chính là thành quả của quá trình em phối hợp chặt chẽ cùng AI."*

#### Phút 7:30 - 8:30: Nâng Cấp & Mở Rộng Tiệm
- **Hành động**: Nhấn nút nâng cấp mỏ Đất lên mốc Milestone tiếp theo (Lv 10, 25...), quầy nảy sáng.
- **Lời nói**:
  > *"Khi em nâng cấp trạm mỏ, thanh tiến trình Milestone co dãn mượt mà. Khi chạm các mốc đột phá 10, 25, 50, quầy hàng sẽ bùng nổ hiệu ứng và tăng sản lượng bán ra. Vòng lặp 'Kiếm tiền -> Nâng cấp -> Mở rộng xưởng' diễn ra trơn tru, tạo động lực liên tục cho người chơi."*

---

### [8:30 - 10:00] TƯƠNG LAI, MÔ HÌNH KIẾM TIỀN & LỜI CẢM ƠN (Slide 5 hoặc Màn hình kết)
- **Lời nói**:
  > *"Như em đã chia sẻ ở phần đầu, mục tiêu kiên định của em là **đưa MineMart phát hành thương mại thực tế** lên Google Play và App Store. Để làm được điều đó, em đã vạch sẵn lộ trình cụ thể:  
  > 1. **Kiến trúc Data-Driven**: Đưa toàn bộ chỉ số vào ScriptableObjects, dễ dàng mở rộng thêm các Bản đồ mới như Sa mạc, Đảo Băng, Rừng nhiệt đới.  
  > 2. **Hệ thống Trang bị & Thú cưng**: Bổ sung rương đồ nghề, cuốc xịn và thú cưng hỗ trợ tăng năng suất.  
  > 3. **Chiến lược Kiếm tiền (Monetization)**: Áp dụng mô hình Hybrid Casual:  
  >    - **Rewarded Video Ads**: Xem quảng cáo nhận thưởng x2 doanh thu trong 5 phút hoặc gọi Siêu Thợ Mỏ.  
  >    - **In-App Purchase**: Gói gỡ quảng cáo vĩnh viễn (No-Ads) và các bộ Skin nhân vật độc quyền (như Skin Chibi Voxel của em hay đồng phục FPT Poly).  
  >  
  > Em xin chân thành cảm ơn quý thầy cô đã lắng nghe phần thuyết trình của nhóm. Kính chúc thầy cô thật nhiều sức khỏe và mong nhận được những góp ý quý báu từ Hội đồng! Em xin sẵn sàng trả lời các câu hỏi phản biện."*

---

## 📸 CHECKLIST ẢNH CẦN CHỤP BỎ VÀO CANVA (DƯỚI 3 PHÚT)

| STT | Tên ảnh cần chụp | Hướng dẫn chụp nhanh | Dùng cho Slide |
|---|---|---|---|
| **1** | **Poster / Icon game** | Đã có sẵn file: [MineMart_Poster_A2.png](file:///g:/Projects/Project_1/docs/MineMart_Poster_A2.png) hoặc [MineMart_Icon.jpg](file:///g:/Projects/Project_1/docs/MineMart_Icon.jpg). | Slide 1 (Cover) |
| **2** | **Cửa sổ Blockbench** | Mở Blockbench lên, load model nhân vật hoặc quầy mỏ của em, chụp màn hình (`Windows + Shift + S`). | Slide 2 (Năng lực) |
| **3** | **Logo FPT Poly** | Lên Google gõ nhanh `Logo FPT Polytechnic PNG` tải về thả vào. | Slide 2 (Cảm hứng) |
| **4** | **Gameplay Isometric** | Bấm Play trong Unity, chụp toàn cảnh màn hình khi xưởng đang đông khách và có tiền tip trên đĩa. | Slide 3 (Gameplay) |
| **5** | **Editor Milestone Tool** | Chụp Inspector component `StationUpgradeBubble` có nút `⚡ Tạo / Định Vị Milestone Bar`. | Slide 4 (AI Tooling) |

---
*Bản quyền tài liệu: Miyuki Studio & AI Co-Pilot — Sẵn sàng cho buổi bảo vệ thành công rực rỡ!*
