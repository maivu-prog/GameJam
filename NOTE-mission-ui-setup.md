# Setup: dựng UI Mission bằng tay

> Component: `MissionLedgerView` — [MissionLedgerView.cs](GameJam/Assets/Scripts/FishingGame/MissionLedgerView.cs)

---

## 🧒 ELI5 — đọc 1 phút là nắm hết

**Bạn vẽ giao diện, game chỉ đổ chữ vào.**

Giống hệt cách `Title Screen` đang làm: bạn dựng panel trong scene cho đẹp theo ý mình, rồi kéo từng mảnh vào các ô trong component. Game **không tạo, không di chuyển, không resize** bất cứ thứ gì — nó chỉ ghi text và bật/tắt object.

Nghĩa là:
- Muốn đổi vị trí, màu, font, kích thước → **chỉnh thẳng trong scene**, không cần đụng code
- Chỉnh xong bấm Ctrl+S là xong, không có trò copy/paste component nữa
- **Kéo thiếu ô nào cũng không sao** — ô trống thì game bỏ qua mảnh đó, không lỗi. Cứ dựng dần, Play thử bất cứ lúc nào

Có **20 ô** nhưng chỉ **6 ô đầu** là đủ chạy được. Phần còn lại thêm dần.

Không kéo gì cả thì nhiệm vụ vẫn chạy ngầm, vẫn cộng tiền thưởng — chỉ là không thấy gì trên màn hình.

---

## 1. Hierarchy cần dựng

Dựng dưới `HarborScreen` (hoặc chỗ nào bạn muốn bảng hiện ra):

```
HarborScreen
└── MissionLedger              ← panel gốc, tắt sẵn (uncheck ô active)
    ├── Portrait               Image
    ├── NpcName                TMP Text
    ├── NpcRole                TMP Text
    ├── Dialogue               TMP Text
    ├── StoryCard              Image
    │   ├── Title              TMP Text
    │   ├── Description        TMP Text
    │   ├── Objectives         TMP Text   ← căn TRÁI
    │   ├── WhereLine          TMP Text
    │   ├── Reward             TMP Text
    │   └── ReadyStamp         Image      ← tắt sẵn
    ├── ClaimButton            Button
    ├── TrackButton            Button
    │   └── Label              TMP Text
    └── CloseButton            Button
```

Và trên màn biển:

```
SeaScreen
├── MissionTracker             ← tờ note nhỏ
│   ├── TrackerTitle           TMP Text
│   ├── TrackerLines           TMP Text
│   └── (Button trên chính MissionTracker, để chạm vào bung ra)
└── ProgressStamp              Image  ← tắt sẵn
```

**Art có sẵn** ở `Assets/Resources/Art/`:

| File | Dùng cho |
|---|---|
| `UI/Missions/mission-ledger-panel` | nền panel LEDGER |
| `UI/Missions/mission-card-large` | nền StoryCard |
| `UI/Missions/mission-card-small` | nền contract phụ (chưa dùng) |
| `UI/Missions/mission-complete-stamp` | ReadyStamp + ProgressStamp |
| `UI/Missions/mission-tracker-note` | nền tờ note |
| `UI/Missions/mission-button-teal` | nền nút |
| `Characters/Narrative/*-portrait` | 6 chân dung NPC |

---

## 2. Gắn component và kéo slot

1. Chọn GameObject có `FishingGameController`
2. **Add Component → Mission Ledger View**
3. Kéo từng object vào slot tương ứng
4. Kéo chính component đó vào slot **`Missions ▸ Mission View`** trên `FishingGameController`
5. Kéo nút MISSIONS vào slot **`Missions ▸ Mission Button`**
6. **Ctrl+S**

> 💡 Kéo xong bấm chuột phải lên tiêu đề component → **"Kiểm slot còn trống"**. Nó in ra Console danh sách ô nào chưa kéo, đỡ phải dò tay.

---

## 3. Sáu ô tối thiểu để chạy được

Muốn thấy cái gì đó ngay thì kéo đúng 6 ô này, bỏ qua phần còn lại:

| Ô | Vì sao cần |
|---|---|
| `Root` | Không có thì bảng không bật lên được |
| `Title` | Tên nhiệm vụ |
| `Objectives` | Danh sách việc phải làm + x/y |
| `Dialogue` | Thoại NPC |
| `Claim Button` | Không có thì không trả nhiệm vụ được |
| `Close Button` | Không có thì không thoát bảng được |

---

## 4. Ai điều khiển cái gì

Để biết chỗ nào chỉnh trong scene, chỗ nào game tự lo:

| Thứ | Ai lo |
|---|---|
| Vị trí, cỡ, màu, font, sprite nền | **Bạn**, trong scene |
| Nội dung chữ | Game |
| Bật/tắt cả bảng | Game (nút MISSIONS / CLOSE) |
| Ẩn/hiện nút CLAIM | Game — chỉ hiện khi đã xong **và** đang đứng đúng cảng |
| Ẩn/hiện `ReadyStamp` | Game — hiện khi xong hết objective |
| Sprite chân dung | Game — đổi theo NPC đang nói |
| Chữ trên nút Track | Game — đổi giữa `TRACK` / `UNTRACK` |
| Cỡ tờ note lúc bung/thu | Game, theo 2 ô `Tracker Expanded/Collapsed Size` |

**Chỉ có 2 ô là số:** `Tracker Expanded Size` và `Tracker Collapsed Size` — vì game phải đổi cỡ tờ note khi nó tự thu lại sau 3 giây. Còn lại không có toạ độ nào trong component.

---

## 5. Vài lưu ý khi dựng

**`Objectives` phải căn TRÁI** và bật **Auto Size** — nó có thể là 1 đến 3 dòng tuỳ nhiệm vụ, mỗi dòng dạng `[x]  Catch Coastal Bream   0/3`.

**`Dialogue` bật Auto Size** — thoại 2–3 câu, dài ngắn khác nhau.

**Panel gốc nên có `Image` bật `Raycast Target`** để chặn tap xuyên xuống chợ cá phía dưới.

**`MissionLedger` và `ReadyStamp` nên tắt sẵn** trong scene (uncheck ô active góc trên Inspector). Game sẽ bật khi cần.

**Nút chạm vào tờ note**: gắn `Button` lên chính `MissionTracker`, rồi kéo vào ô `Tracker Tap Button`. Không cần set OnClick trong Inspector — game tự gán.

⚠️ **Đừng set OnClick tay cho ClaimButton / TrackButton / CloseButton.** Game gọi `RemoveAllListeners()` trước khi gán nên OnClick bạn set trong Inspector sẽ bị xoá.

---

## 6. Nhiệm vụ đầu tiên để test

Save mới sẽ tự nhận **"A Fisher's Morning"** — Mara giao, 2 objective:

```
[ ]  Catch Coastal Bream        0/3
[ ]  Return to Home Harbor
```

Bắt 3 con `bream` rồi cập Home Harbor → hiện `READY TO CLAIM` → nút CLAIM hiện ra → bấm → +60c và chuyển sang mission 2.

Test nhanh hơn: `Rusty Fishing ▸ Reset Progression (delete save)` để về mission 1.
