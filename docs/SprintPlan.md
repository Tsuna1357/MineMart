---
title: "Game Collab 3D phase 01 — Sprint Plan"
sprint_number: "1-6"
start_date: "2026-04-18"
end_date: "TBD"
goal: "Create a complete, testable, and demo‑ready core system for the 3D Match‑3 game"
status: "Planning"
---

# 🚀 Overall Project Sprint Plan

## Goal
Deliver the core systems: Dynamic Tray, swap‑mechanic, pop‑out physics, admin tool, data sync (Firebase), Addressables, and Visual Time‑Pressure. Solo development with runnable demos after each sprint.

## Architecture Decisions (Resolved)
- **Addressables Naming**: Phân chia theo Loại Asset (Option A) `Assets/Items`, `Assets/Audio`, `Assets/UI`.
- **Version Field**: Format Integer (Option A: 1, 2, 3...) cho `LevelConfigSO.version`.
- **UI Framework**: TextMeshPro + Canvas truyền thống + DOTween (Option B) cho hiệu ứng rung, đếm ngược bật nảy.

---

## 🛠 Sprint 1: Foundations (Core Physics & Tray Snapping)

| # | Task | Size | Depends On | Agent | Status | Linear ID |
|---|------|------|-----------|-------|--------|-----------|
| 1 | Scaffold project structure & asmdef files (Core, Gameplay, Editor, Tests) | S | — | Architect | ⬜ | |
| 2 | Create `Item` prefab (Rigidbody, ZeroFriction PhysicMaterial, placeholder mesh) | M | 1 | Gameplay Dev | ⬜ | |
| 3 | Implement `TrayManager` (slot grid, snap, add/remove slots) | M | 2 | Gameplay Dev | ⬜ | |
| 4 | Basic `LevelLoader` – load local JSON, instantiate Items via Object Pool | M | 3 | Gameplay Dev | ⬜ | |
| 5 | Demo scene: pause → load level → resume → simultaneous fall (Ω‑effect) | S | 4 | Gameplay Dev | ⬜ | |
| 6 | PlayMode unit tests for `TrayManager` snapping & `LevelLoader` parsing | S | 4 | QA Tester | ⬜ | |

**Status Legend**: ⬜ To Do | 🔄 In Progress | ✅ Done | ❌ Blocked

---

## 🔄 Sprint 2: Swap Mechanic & Pop‑out Physics

| # | Task | Size | Depends On | Agent | Status | Linear ID |
|---|------|------|-----------|-------|--------|-----------|
| 7 | Extend `TrayManager` to detect 3 identical items and trigger swap | M | 5 | Gameplay Dev | ⬜ | |
| 8 | Swap animation: items 1 & 3 slide out, converge to item 2, play “merge” sound | L | 7 | Gameplay Dev | ⬜ | |
| 9 | Pop‑out logic: on tap, `AddForce` + `AddTorque`, then `DOPath` Bézier to slot | L | 7 | Gameplay Dev | ⬜ | |
| 10| Install DOTween, create reusable `TweenHelper` (spiral lift + Bézier drop) | S | 9 | Gameplay Dev | ⬜ | |
| 11| PlayMode tests for swap detection & pop‑out logic | M | 9 | QA Tester | ⬜ | |

---

## 🏗 Sprint 3: Admin Tool & Data Architecture

| # | Task | Size | Depends On | Agent | Status | Linear ID |
|---|------|------|-----------|-------|--------|-----------|
| 12| Create `ItemDataSO` (itemId, prefab, defaultScale) | S | — | Architect | ⬜ | |
| 13| Build `LevelConfigSO` (list of ItemData, placements, int version) | M | 12 | Architect | ⬜ | |
| 14| Extend `TrayDesigner` EditorWindow: drag‑drop items, edit x/y, export JSON | L | 13 | Tool Developer | ⬜ | |
| 15| Implement JSON export/import for `LevelConfigSO` (`JsonUtility`) | M | 13 | Tool Developer | ⬜ | |
| 16| Editor tests for `TrayDesigner` round‑trip save/load | S | 15 | QA Tester | ⬜ | |

---

## 🌐 Sprint 4: Remote Data Sync & Offline Fallback

| # | Task | Size | Depends On | Agent | Status | Linear ID |
|---|------|------|-----------|-------|--------|-----------|
| 17| Add Firebase SDK, initialize `FirebaseManager` singleton | M | — | Architect | ⬜ | |
| 18| Version check: compare local int version with remote; fetch newer levels | M | 17 | Gameplay Dev | ⬜ | |
| 19| Offline mode: load local JSON; store temporary results in `PlayerPrefs` | S | 18 | Gameplay Dev | ⬜ | |
| 20| Sync cached results to Firebase when online (background coroutine) | M | 19 | Gameplay Dev | ⬜ | |
| 21| Integration tests (mock Firebase) for version check & sync logic | M | 20 | QA Tester | ⬜ | |

---

## ⏳ Sprint 5: Visual Time‑Pressure System

| # | Task | Size | Depends On | Agent | Status | Linear ID |
|---|------|------|-----------|-------|--------|-----------|
| 22| `WaterFill` component: raise water level, trigger countdown at character height | M | 5 | Gameplay Dev | ⬜ | |
| 23| TMP UI overlay: red flash, nảy số, urgent music, character struggle (DOTween) | L | 22 | UI Specialist | ⬜ | |
| 24| Integrate goals: when required items matched, play glass‑break & stop water | M | 22 | Gameplay Dev | ⬜ | |
| 25| Sound effects (splash, alarm, struggle, glass break) via Addressables | S | 23 | Audio Engineer | ⬜ | |
| 26| PlayMode tests for water rise & goal completion handling | S | 24 | QA Tester | ⬜ | |

---

## ✨ Sprint 6: Polish, Addressables & Final Demo

| # | Task | Size | Depends On | Agent | Status | Linear ID |
|---|------|------|-----------|-------|--------|-----------|
| 27| Convert prefabs, materials, audio to Addressables (`Assets/Items`, etc.) | M | 1‑26 | Architect | ⬜ | |
| 28| Build Level Map UI (Candy Crush style), Win/Lose screens (w/ Shop links) | M | 27 | UI Specialist | ⬜ | |
| 29| Build Global Settings Popup (Slider & Toggle Mute) & Gameplay UI quick mute | S | 28 | UI Specialist | ⬜ | |
| 30| Full play‑through testing on target Android device (60 fps) | L | 29 | QA Tester | ⬜ | |
| 31| Create demo build (APK), record walkthrough video | S | 30 | Gameplay Dev | ⬜ | |

---

---

## 💰 Sprint 7: Meta-Game Economy & Boosters

| # | Task | Size | Depends On | Agent | Status | Linear ID |
|---|------|------|-----------|-------|--------|-----------|
| 32| Implement Soft Currency system: award coins based on time/score | M | 24 | Gameplay Dev | ⬜ | |
| 33| Build Booster Pre-game Selection Popup (Configurable slots & amounts) | L | 32 | UI Specialist | ⬜ | |
| 34| Implement "Hint" Booster (highlight target / matching items) | M | 33 | Gameplay Dev | ⬜ | |
| 35| Implement "Shake" Booster (physics impulse to rearrange tray) | S | 33 | Gameplay Dev | ⬜ | |
| 36| Implement IAP Unity In-App Purchasing | L | 33 | Architect | ⬜ | |

---

## 🌍 Sprint 8: Social, Auth & Analytics

| # | Task | Size | Depends On | Agent | Status | Linear ID |
|---|------|------|-----------|-------|--------|-----------|
| 37| Integrate Firebase Auth (Anonymous + Google/Apple Sign-in) | M | 17 | Architect | ⬜ | |
| 38| Implement Leaderboard Data Structure (Firebase DB) | M | 37 | Gameplay Dev | ⬜ | |
| 39| Integrate Top-3 Avatars & Scores directly onto Level Map nodes | M | 38 | UI Specialist | ⬜ | |
| 40| Integrate Firebase Analytics & Crashlytics | S | 37 | QA Tester | ⬜ | |

---

## Acceptance Criteria
- [ ] All tasks completed across all Sprints.
- [ ] All Unity Test Runner tests pass.
- [ ] No critical bugs open.
- [ ] APK demo runs smoothly at 60fps on Android.

## Retrospective
### What went well
<!-- Fill after completion -->
### What could improve
<!-- Fill after completion -->
### Action items for the future
<!-- Fill after completion -->
