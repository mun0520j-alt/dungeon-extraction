# 유지보수성 가이드

> 이 프로젝트에서 실제로 겪은 문제를 바탕으로 만든 규칙이다. 새 기능·리뷰·Codex 지시서 작성 시 기준으로 쓴다.
> 규칙을 어긴 사례가 생기면 "근거" 칸에 버그·기술부채 번호를 추가한다.
> 관련: `AGENTS.md`, `TECHNICAL_DEBT.md`, `CODE_REVIEW_LOG.md`, `docs/QA_DEBUG_TOOLS.md`, `docs/VERSIONING.md`

## 1. 데이터와 수치

| 규칙 | 근거 |
|---|---|
| 게임 수치(가격·확률·Tick·우호도 기준)는 CSV(`Assets/Resources/Data/`) 또는 설정값으로 둔다. 코드에 하드코딩하지 않는다 | M7·M8 경제 수치 전부 임시값 → 재조정 예정 |
| 새 CSV 열은 `docs/ITEM_CSV_DATA_DICTIONARY.md`에 같이 적는다 | M8 퀘스트 CSV |
| 데이터 규칙은 에디터 자동 검증(`Dungeon Tools/QA/MX ...`)으로 고정한다. 결정이 바뀌면 검증도 같이 바꾼다 | M8 결정 15 → `M8QuestValidation` 운반 인레이드 검사 추가 |
| ID(`item_id`, `quest_id`)는 세이브에 저장되므로 바꾸거나 지우지 않는다. 바꿔야 하면 세이브 로드 시 없는 ID를 안전하게 처리한다 | 진행 중 퀘스트 ID 소프트락 (86cd4e1) |

## 2. 구조

| 규칙 | 근거 |
|---|---|
| 새 Singleton을 만들지 않는다. 상태는 기존 정적 상태(`LootRuntimeState`, `QuestRuntimeState`) 확장 또는 별도 정적 클래스로 | AGENTS.md |
| 상태 클래스는 상태만 소유하고, 원정 사실(보스 처치 등)은 기록 API로 전달받는다 | `ExpeditionRunRecord` → `QuestRuntimeState` |
| **새 Canvas를 만들지 않는다.** HUD는 `ScreenSpaceCanvasProvider.GetOrCreateHudRoot()`만 쓴다. 표시 전용 텍스트는 OnGUI | BUG-044, TD-014 |
| 400줄이 넘는 클래스는 역할별 `partial` 파일로 나눈다. 메인 파일 상단에 파일 구성을 주석으로 적는다 | `ExpeditionDebugCommands` 분할 (b2a0a8b) |
| 같은 목록(단축키·메뉴 등)을 두 곳에 쓰지 않는다. 표 하나에서 동작과 도움말을 같이 만든다 | F3 도움말과 실제 단축키 불일치 위험 |
| 월드 입력 차단은 `PauseMenuController.BlocksWorldInput` 하나로 판단한다. 새 모달 UI도 여기에 연결한다 | F6·F9 패널 입력 차단 |

## 3. QA·디버그 코드

| 규칙 | 근거 |
|---|---|
| 모든 QA 기능은 `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` 안에 둔다. 릴리즈 빌드에서 사라지는지 빌드 후 확인한다 | BETA 0.1 빌드 검사 |
| QA 기능을 쓴 원정은 `M3MeasurementAutomation.MarkDebugRun()`으로 측정 CSV에서 제외한다 | M3 측정 오염 방지 |
| 새 QA 기능은 `docs/QA_DEBUG_TOOLS.md`에 한 줄 추가한다 | QA 도구가 코드 곳곳에 흩어짐 |
| QA 패널이 열린 동안 월드 입력을 막는다 | 패널 조작 중 캐릭터 이동 (사용자 보고 2026-11-15) |

## 4. 세이브

| 규칙 | 근거 |
|---|---|
| 세이브 구조가 바뀌면 `save_version`을 올리고, 이전 원본을 `save_vN.bak`으로 먼저 보존한다. 백업 실패 시 변환하지 않는다 | v2→v3, v3→v4, v4→v5 |
| 새 필드의 이전 버전 기본값을 결정으로 남긴다 (예: v4 아이템 인레이드 = 없음) | M8 결정 13 |
| 공개 빌드마다 세이브 샘플을 `QA/evidence/saves/<버전>/`에 보관해 다음 마이그레이션 TC에 쓴다 | TC-M7-25 v3 세이브 부재 |
| 로드 값은 범위를 고정한다 (레벨 1~3, 음수 금지) | `QuestRuntimeState.RestorePersistentState` |

## 5. Unity·저장소

| 규칙 | 근거 |
|---|---|
| Unity 6 obsolete API를 새로 쓰지 않는다: `FindObjectOfType` → `FindFirstObjectByType`, `FindObjectsOfType` → `FindObjectsByType` | TD-013 |
| 새 `.cs`는 `.meta`와 함께 커밋한다 | GUID 누락 시 컴포넌트 참조 깨짐 |
| `ProjectSettings/PackageManagerSettings.asset`은 커밋하지 않는다 | AGENTS.md |
| Unity 배치 실행 후 `ProjectSettings.asset`의 `preloadedAssets`(InputSystem_Actions)가 빠지면 되돌린다 | M7 마감 때 자동 변경 |
| 파일 이름 변경은 `git mv`로 `.cs`와 `.meta`를 같이 옮겨 GUID를 유지한다 | ReturnRitualAnchor → EscapeScrollZone |
| PR merge 후 작업 브랜치는 바로 삭제한다. 버전 이력은 태그로 남는다 | 2026-11-13 브랜치 정리 |

## 6. 리뷰 체크리스트 (PR·Codex 결과 검수 시)

- [ ] QA 코드가 `#if` 안에 있는가, 새 QA 기능이 문서에 있는가
- [ ] 새 Canvas·새 Singleton이 없는가
- [ ] 수치가 CSV·설정값인가, 자동 검증이 결정과 맞는가
- [ ] 세이브 구조 변경 시 버전·백업·기본값·범위 고정이 있는가
- [ ] 사용자 결정에 없는 규칙을 코드가 정하지 않았는가 (있으면 결정 받고 회의록에 기록)
- [ ] TC가 추가·갱신됐는가, Play로만 확인 가능한 것은 체크리스트에 있는가
- [ ] `CODE_REVIEW_LOG.md`에 리뷰 항목을 남겼는가
