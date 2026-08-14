# RUSTY FISHING — PROJECT DOCUMENT

## 1. Tổng quan

**Rusty Fishing** là game câu cá 2D màn hình dọc, kết hợp điều khiển tàu, khai thác tài nguyên, quản lý thời gian và rủi ro trên biển. Người chơi rời cảng, tìm vùng cá, câu cá, tránh chướng ngại vật rồi quay về bán hàng, sửa chữa và nâng cấp tàu.

- Engine: Unity 6.3 LTS, uGUI, Input System.
- Nền tảng hiện tại: Windows 64-bit; bố cục chuẩn portrait `1080 × 1920`.
- Ngôn ngữ trong game: English.
- Art direction: tranh minh họa hàng hải cũ, tối và sần, lấy cảm hứng từ không khí Rusty Lake.
- Scene chính: `Assets/Scenes/SampleScene.unity`.

## 2. Gameplay loop

1. Tại cảng: xem kho cá, bán cá, vứt cá hỏng, sửa và nâng cấp tàu.
2. Chọn **SET SAIL** để ra khơi.
3. Giữ nút trái/phải để lái; tàu tăng tốc và giảm tốc có quán tính.
4. Tìm vùng cá, giữ dial **FISH** và điều khiển joystick để dẫn móc câu.
5. Móc gây damage; cá bị đánh sẽ bỏ chạy. Khi hết HP, cá được đưa vào kho nếu còn chỗ.
6. Quay lại phạm vi cảng và chọn **DOCK** để bán cá hoặc chuẩn bị chuyến tiếp theo.

## 3. Thời gian và rủi ro

- Ban ngày: 3 phút, tương ứng `06:00–18:00`.
- Ban đêm: 3 phút, tương ứng `18:00–06:00`.
- Đồng hồ bán nguyệt hiển thị tiến trình ngày/đêm.
- Khi trời tối, màn hình phủ màu đêm; ở ngoài cảng xuất hiện cảnh báo thủy quái.
- Khi đang ở cảng vào ban đêm, nút **REST UNTIL DAWN** bỏ qua tới 06:00 ngày kế tiếp.
- AI thủy quái truy đuổi và combat boss chưa được triển khai; hiện mới dừng ở visual/cảnh báo đúng phạm vi Phase 2 hiện tại.

## 4. Thế giới biển

### Cảng

| Cảng | Vị trí | Vai trò |
|---|---:|---|
| Home Harbor | 6 | Cảng khởi đầu, cảng duy nhất cho nâng cấp |
| Coral Harbor | 38 | Cảng trung gian, giá cá riêng |
| Merchant Harbor | 76 | Cảng xa, phần lớn giá mua cao hơn |
| Frontier Harbor | 116 | Cảng cuối vùng biển nguy hiểm |

Mỗi cảng dùng art riêng, có vùng cập cảng an toàn và bảng giá riêng cho từng loài. Cá đặc hữu thường có giá thấp gần nơi đánh bắt và giá cao hơn tại cảng xa.

### Chướng ngại vật

| Chướng ngại | Vị trí | Tốc độ an toàn | Damage gốc |
|---|---:|---:|---:|
| Driftwood | 25 | 2.4 kn | 12 |
| Shipwreck | 58 | 1.5 kn | 30 |
| Death Reef | 96 | 0.95 kn | 48 |

- Chướng ngại chỉ gây va chạm khi tàu vượt tốc độ an toàn.
- Damage tăng theo mức vượt tốc; sau va chạm tàu bị giảm tốc mạnh.
- Speedometer có kim tốc độ hiện tại và kim ngưỡng an toàn khi vào vùng nguy hiểm.
- Art obstacle đổi màu cảnh báo khi tàu ở gần.

## 5. Cá và câu cá

Game hiện có 9 loài với sprite, kích thước, HP, tốc độ, độ sâu, giá và độ hiếm khác nhau:

`Coastal Bream`, `Silver Sardine`, `Blue Mackerel`, `Barracuda`, `Red Snapper`, `Lanternfish`, `Anglerfish`, `Black Grouper`, `Ghost Tuna`.

- Gần bờ chủ yếu có bream, sardine và mackerel; cá hiếm/nguy hiểm tập trung xa bờ và sâu hơn.
- Số lượng cá gần cảng thấp, mật độ tăng khi đi xa.
- Mỗi con cá có **cân nặng random** (hệ số `[0.6, 1.7]`); **HP, giá bán và kích thước hiển thị** tỉ lệ thuận với cân nặng (cá nặng = khó câu hơn, bán được giá hơn).
- Móc xoay theo hướng joystick, không bị giới hạn bởi mép màn hình và tự thu khi thả tay hoặc hết thời gian dây.
- Dây câu là trail cong theo quỹ đạo móc; độ dày và độ mượt có thể tuning.
- Cá có art tươi và art ươn/hỏng phục vụ kho hàng và UI thị trường.

## 6. Kho cá và kinh tế

- Sức chứa ban đầu: 20 cá; nâng cấp Hold tăng `+5` mỗi cấp.
- Cá tươi dưới 24 giờ: bán đủ giá.
- Cá ươn từ 24–48 giờ: bán 50% giá.
- Cá hỏng từ 48 giờ: không bán được, phải dùng **TOSS ROTTEN**.
- Giá bán = giá gốc loài × hệ số cân nặng × hệ số cảng × hệ số độ tươi.
- Khi kho đầy vẫn câu bình thường; câu trúng sẽ mở popup inventory (cuộn được, liệt kê **từng con** kèm cân nặng/độ tươi/giá) để chọn 1 con vứt bỏ.
- Freshness và Sell/Toss dùng **giờ tuyệt đối `AbsHour`** (cộng dồn `(day−1)*24 + …`) nên tính tuổi cá chính xác xuyên nhiều ngày.
- Market hiển thị từng loài kèm icon cá (dùng sprite `-rotten` khi nhóm phần lớn đã hỏng).

## 7. Tàu, damage và progression

- Tàu có tăng tốc, giảm tốc và vận tốc tối đa; nâng Engine tăng 15% tốc độ mỗi cấp.
- HP không tự hồi khi cập cảng hoặc Rest.
- Chỉ sửa tàu tại cảng; phí sửa bằng lượng HP thiếu × 2 coins.
- Khi HP về 0: mất toàn bộ cá, trở về Home Harbor đầu ngày với tàu hỏng và phải trả phí sửa trước khi ra khơi.
- Bốn nhánh nâng cấp, tối đa 3 cấp, giá `120 / 300 / 650` coins:

| Nhánh | Hiệu quả mỗi cấp |
|---|---|
| Hook | +25% damage câu |
| Hold | +5 sức chứa |
| Engine | +15% tốc độ |
| Hull | +25 max HP |

Chỉ **Home Harbor** cho phép nâng cấp. Art progression có 4 cấp tàu và 4 cấp móc (`C → A → D → B`); sprite tàu/móc **tự đổi theo level đã nối runtime** qua `SyncUpgradeArt()`.

## 8. UI/UX và asset

- Hai màn hình chính: `HarborScreen` và `SeaScreen`, đã bake thành hierarchy chỉnh trực tiếp trong Unity.
- HUD gồm đồng hồ ngày/đêm, cargo, coin, vùng an toàn/nguy hiểm, HP và speedometer hai kim.
- Điều khiển gồm giữ trái/phải để lái và dial trung tâm để câu.
- Asset production nằm tại `Assets/Resources/Art`; gồm background, tàu, 4 cảng, 4 obstacle variant, 9 loài cá tươi/hỏng, thủy quái và 24 UI element đã tách.
- Nếu cần dựng lại Canvas: menu `Rusty Fishing > Rebuild Editable UI` — thao tác này ghi đè layout đang chỉnh.
- Nếu reference art bị mất: `Rusty Fishing > Repair Art References`.

## 9. Tuning và lưu game

- Bật `Tuning Tool` trên `FishingGameController`; khi Play, nhấn **TUNE** ở góc trái để chỉnh live lực chìm/nổi, lực ngang, tốc độ thu dây, thời lượng dây, độ sâu, world scroll, chuyển động/khoảng sâu cá và độ dày dây.
- Nút **LOG** in thông số hiện tại ra Console; **RESET** trả về giá trị lúc bắt đầu Play.
- Kích thước cá chỉnh trực tiếp tại `FishingGameController > Fish Size`.
- Thông số mặc định và catalog nội dung: `Assets/Scripts/FishingGame/GameCatalog.cs`.
- Save JSON lưu coins, ngày, HP, cargo và level tại `Application.persistentDataPath/rusty-fishing-save.json`.

## 10. Cấu trúc kỹ thuật và build

- `FishingGameController.cs`: state machine Harbor/Sailing/Fishing/Night và gameplay loop.
- `GameCatalog.cs`: dữ liệu cá, cảng, obstacle và tuning mặc định.
- `PlayerSave.cs`: save, economy, freshness, repair và upgrades.
- `FishActor.cs`: hành vi bơi, nhận damage và bỏ chạy.
- `RuntimeUI.cs`: helper uGUI, sprite loading và EventSystem.
- `FishingLineTrail.cs`: vẽ dây câu theo quỹ đạo.
- `TuningPanel.cs`: tuning tool trong Play Mode.

Build Windows hiện có tại `Builds/Windows/RustyFishing.exe`. Tạo build mới qua `Rusty Fishing > Build Windows Development`.

## 11. Việc còn lại ưu tiên

1. Hoàn thiện AI thủy quái truy đuổi, damage và encounter ban đêm.
2. **Đêm ngắt phiên câu** như game gốc (hiện đang cho câu xuyên đêm — giữ tạm, xử lý sau).
3. Polish còn lại: glow obstacle, âm thanh, animation, cân bằng economy (đã có camera shake + red flash khi va chạm).
4. Kiểm thử đầy đủ một chu kỳ ngày–đêm, save/load và progression cấp 1–3.

_Đã hoàn thành gần đây: hệ thống cân nặng (HP/giá theo cân nặng); giỏ đầy → popup toss từng con; freshness giờ tuyệt đối; sprite tàu/móc theo level; art cá ươn/hỏng trong market; cá bơi lang thang; dây câu theo trail; joystick dynamic-origin; tuning tool; camera shake/flash va chạm._
