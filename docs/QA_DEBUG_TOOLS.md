# QA·디버그 도구 목록

> 모든 런타임 QA 기능은 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 안에 있다. **공개 빌드(Development Build 해제)에는 포함되지 않는다.**
> QA 기능을 쓴 원정은 `M3MeasurementAutomation.MarkDebugRun()`으로 측정 CSV에서 제외된다.
> 새 QA 기능을 추가하면 이 문서에 한 줄 추가한다.

## 1. 던전 단축키 (`ExpeditionDebugCommands`)

단축키 표는 `ExpeditionDebugCommands.cs`의 `Hotkeys` 한 곳에서 관리한다. F3 격자 도움말 문구도 이 표에서 자동 생성된다.

| 키 | 기능 | 파일 |
|---|---|---|
| F3 | 타일 격자 오버레이 (Floor/Wall/Room ID/문·상자·계단·액터) | `.TileGrid.cs` |
| F4 | 빠른 검증 맵: 플레이어 주변 9×9, 왼쪽 상자 / 위 Enemy / 오른쪽 탈출구 | `.DebugMap.cs` |
| F5 | 출격 가방을 첫 드롭 아이템으로 가득 채우기 | `.cs` |
| F6 | 측정 패널: Seed 고정·복사·Random 전환, 현재 맵 검증, CSV 경로 | `.Panels.cs` |
| F7 | 현재 Tick·장비·HP 유지하고 5층으로 이동 | `.cs` |
| F8 | 플레이어 칸 바닥 더미 25칸 채우기 | `.cs` |
| F9 | 아이템 지급 패널 + 보험 대기 Tick 진행 버튼 | `.Panels.cs` |

- **F6·F9 패널이 열려 있는 동안 월드 입력(이동·상호작용·인벤토리)은 막힌다** (`PauseMenuController.DebugPanelOpen`). 같은 키로 닫는다.
- 일시정지(Esc) 중에는 단축키도 동작하지 않는다.

## 2. 인스펙터 ContextMenu

| 컴포넌트 | 메뉴 | 기능 |
|---|---|---|
| ExpeditionDebugCommands | QA/Set Elapsed Tick | `targetElapsedTicks`로 누적 Tick 설정 (1,440 경계 확인) |
| ExpeditionDebugCommands | QA/Set Player HP | `targetPlayerHP`로 HP 설정 |
| ExpeditionDebugCommands | QA/Advance Insurance Ticks | 보험 대기 `insuranceAdvanceTicks`만큼 진행 후 저장 |
| ExpeditionDebugCommands | QA/Toggle Tile Grid · Build M1 Debug Map · Fill M2 Bag · Jump To Floor 5 · Fill Ground Loot Pile | 단축키와 동일 |
| DungeonRunManager | QA/Validate Generated Run | 현재 생성 맵 검증 |

## 3. 영지 QA 버튼 (상인 `퀘스트` 탭 하단, M8)

| 버튼 | 기능 |
|---|---|
| 납품품 지급 | 현재 납품 퀘스트 대상 아이템을 인레이드로 Stash에 지급 |
| 현재 목표 완료 | 진행 중 퀘스트 목표를 완료 상태로 |
| 우호도·Gold 지급 | 우호도 300 · Gold 2,000 |
| 퀘스트 초기화 | 퀘스트·분기·우호도·레벨 초기화 |

## 4. 에디터 메뉴 자동 검증 (`Dungeon Tools/QA/`)

| 메뉴 | 대상 |
|---|---|
| M3 Automation | 측정·Seed·분포 |
| M6 Grid Model Validation | 격자 인벤토리 모델 |
| M7 Meta Economy Validation | 잡템·구매·보험 분포 |
| M8 Quest Data Validation | 퀘스트 9개·선행·우호도 300·인레이드 납품/운반·누적 생환 |
| M9 Stash Validation | Stash CSV 크기·격자 배치·중첩 4단계·임시 보관함·세이브 v6 계약 |

## 5. 측정 CSV (`M3MeasurementAutomation`)

- 경로: `%USERPROFILE%\AppData\LocalLow\Watshoi_Maker\Dungeon Extraction\M3Measurements\expedition_runs_v4.csv`
- 디버그 기능을 쓴 원정은 기록하지 않는다.
