# 코드 리뷰 & 기술부채 로그 (Code Review Log)

## 2026-11-23 — 리뷰 대상: eed2c81 탈출 구역 E·F9 QA 개선

- `EscapeScrollZone.Update()` 중복 선언으로 인한 컴파일 실패를 확인하고 하나의 Update 흐름으로 합쳤다.
- 출격 가방 직속만 검색하던 스크롤 자동 사용을 하위 가방까지 재귀 검색·소모하도록 수정했다.
- 문·상자·바닥 더미·일반 계단의 E 우선순위와 일시정지·인벤토리·루팅 모달 입력 차단은 기존 게이트로 유지됨을 확인했다.
- F9 던전 경과 UI는 활성 원정에서만 표시되고, 스크롤 상점 상태는 영지·던전 모두 현재 해금·재고·충전 값을 표시한다.

> **역사 기록:** 당시 구현 로그이며 현재 Tick 명세가 아니다. 0.2Tick 이동 항목은 현재 검증 기준으로 사용하지 않는다.

> `DEV_WORKFLOW_RULES.md` 규칙 2-2에 따라 작성. 코드 작업 → `DEV_LOG.md` 기록 이후, 사용자와의 코드 리뷰 세션마다 새 항목을 맨 위에 추가한다(최신이 위).
> 각 항목에는 리뷰 대상(어떤 커밋/기능), 피드백 내용, 발견된 기술부채, 후속 조치 여부를 남긴다.

---

## 2026-11-17 — 리뷰 대상: M9 단계 4~7 임시 보관함·세이브 v6·판매 바구니

**리뷰 방식**:
- Stash로 들어오는 생환 정리·구매·보험·보상·장착 해제·마이그레이션 경로가 공통 초과 보관 함수를 사용하는지 대조했다.
- v6 캡처와 복원에서 Stash 좌표·회전, 재귀 가방, 임시 보관함, 판매 미확정 아이템이 손실 없이 한 번만 기록되는지 확인했다.
- 판매는 바구니 이동과 확정을 분리하고, 확정 전 Gold 불변·내용물 가방 차단·보험 재확인·취소 복구를 검토했다.

**피드백**:
- 새 Singleton·Canvas 없이 `LootRuntimeState` partial과 기존 HUD를 확장했다.
- 임시 보관함이 비어 있지 않으면 출격 버튼과 실행 진입점 모두 차단한다.
- v5 원본 백업 실패 시 v6 변환을 중단하며, 깊이 초과·중복 ID·배치 실패는 경고 후 안전 처리한다.
- 판매 바구니 상태도 자동 저장 시 원래 Stash/임시 보관함 위치로 직렬화되어 강제 종료 시 판매되지 않는다.
- 납품·운반 판정과 납품 소비가 하위 가방 내부까지 재귀적으로 처리된다.

**발견된 기술부채**:
- TD-015: `BaseCampGridPanel` 역할별 분리 필요.
- TD-016: `M5SaveService` 직렬화·마이그레이션 분리 필요.

**남은 확인**:
- [ ] `Dungeon Tools/QA/M9 Stash Validation` PASS
- [ ] `QA/M9_통합검증_체크리스트.md` Play 수행
- [ ] v5 실제 표본으로 `save_v5.bak`과 v6 재실행 확인

**결론**: 런타임·에디터 정적 컴파일 오류 0. Play·메뉴 자동 검증 실행 대기.

## 2026-11-17 — 리뷰 대상: M9 단계 3 가방 장착·판매·던전 잠금

**리뷰 방식**:
- 가방 내용물 소유자를 `LootItemInstance.BagGrid`로 고정하고 장착 가방의 `runBagGrid`가 같은 격자를 참조하는지 확인했다.
- 장착·교체·해제·판매·사망·MIA·보험 반환 진입점을 대조했다.

**피드백**:
- 내용물 있는 가방 장착·해제 시 내부 아이템을 Stash에 풀지 않고 가방 격자째 이동한다.
- 던전 가방 교체는 기존 가방 전체가 새 가방에 들어갈 때만 성공하며 실패 상태는 원복한다.
- 내용물 있는 가방은 판매를 거부하고 사용자에게 이유를 표시한다.
- 던전 하위 가방 더블클릭은 로직을 실행하지 않아 0Tick이며 잠금 사유만 표시한다.
- 사망·MIA는 장착 가방 참조를 제거해 하위 내용물까지 함께 손실되고, 보험 반환은 새 인스턴스를 만들어 빈 가방으로 돌아온다.

**남은 확인**:
- [ ] TC-M9-10~14 Unity Play 검증
- [ ] 장착 가방 교체 실패 시 기존 배치·Tick 불변 회귀

**결론**: 정적 컴파일 오류 0. 새 Canvas·Singleton·수치·세이브 변경 없음. Play 검증 대기.

## 2026-11-17 — 리뷰 대상: M9 단계 5.5 영지 UI 3열 정렬

**리뷰 방식**:
- `BaseCampManagementPanel`의 OnGUI 영역과 `BaseCampGridPanel`·`ExpeditionResultPanel`의 uGUI 영역을 단일 레이아웃 계산 결과로 대조했다.

**피드백**:
- 새 Canvas 없이 기존 HUD 루트를 재사용한다.
- 상인·Stash·출격 준비 열과 오른쪽 보험·원정 결과 하위 영역이 서로 다른 Rect를 사용한다.
- Stash 48px 격자는 전용 `ScrollRect`·`RectMask2D` 안에서만 스크롤한다.
- 가운데 텍스트 Stash 패널 호출을 제거하고 보험을 오른쪽 전용 영역으로 이동했다.

**남은 확인**:
- [ ] 1920×1080, 1600×900, 1280×720 Play 스크린샷 확인
- [ ] 10×12 Stash 마지막 행과 버튼 비중첩 확인
- [ ] 판매 목록 제거는 단계 7 판매 바구니 격자 구현과 함께 완료
- [ ] 임시 보관함 표시는 단계 4 구현 후 연결

**결론**: 정적 컴파일 오류 0. Play 검증 대기.

## 항목 작성 템플릿 (참고용, 실제 리뷰 시 복사해서 사용)

```
## YYYY-MM-DD — 리뷰 대상: (예: TickManager 초안)

**피드백**:
-

**발견된 기술부채**:
-

**후속 조치**:
- [ ] (조치 항목)
```

---

## 2026-11-15 — 리뷰 대상: M8 우호도·퀘스트 (`feature/m8-reputation-quest`, Codex 87a7b4c~5279776) + 디버그 코드

**리뷰 방식**: Codex 구현 후 Play 검증 전에 Claude가 판정·세이브·QA 게이트를 코드로 검수하고, 사용자 결정이 필요한 설계 포인트를 분리해 결정받은 뒤 Claude가 직접 수정.

**피드백 (문제없음 확인)**:
- QA 버튼 4개·디버그 단축키 전부 `UNITY_EDITOR || DEVELOPMENT_BUILD` 안. 새 Canvas 없음(BUG-044 패턴 없음)
- 퀘스트 CSV 9줄이 확정 표와 일치. 인레이드는 상자·몬스터 드롭만 true, 구매·보험 반환·퀘스트 보상·기본 장비 false
- 납품 시 해당 아이템 보험 정리, 레벨업 Gold 부족 시 상태 불변, 보상 아이템 인레이드 없음
- 세이브 v5: `save_v4.bak` 실패 시 변환 중단, v4 아이템 인레이드 false, 레벨 1~3 클램프, MIA 처리 순서(복원 → q06a 초기화) 정상
- 가격 공식 확인: 판매 `×(1+0.1×(Lv-1))`, 구매 `×4×(1-0.1×(Lv-1))`. `Mathf.RoundToInt`는 .5에서 짝수 반올림(15G → Lv2 16)

**설계 포인트 → 사용자 결정**:
- 운반 퀘스트가 Stash 장비·상점 Rare 가방으로 쉽게 완료됨 → **운반도 인레이드만** (결정 15). 장비 범위는 전 부위·Rare 유지 (결정 16)
- q06a 보스 미처치 생환 처리 → **누적** (결정 17, 사용자가 Codex에 지시한 대로)

**발견된 기술부채 → 이번에 처리**:
- (해결) 세이브의 진행 중 퀘스트 ID가 CSV에서 사라지면 새 퀘스트를 못 받는 소프트락 → 경고 후 해제 (86cd4e1, TC-M8-17A)
- (해결) TD-014 HUD Canvas 첫 Canvas 재사용 → 클릭 가능한 루트 Overlay Canvas만 재사용 (0612e1f)
- (부분 해결) TD-013 obsolete API → `FindObjectOfType`·`FindObjectsOfType` 교체. `GetInstanceID` 2곳(M2BagPanel·StashPanel)은 반환형 차이로 보류
- (해결) 디버그 코드 448줄 단일 파일 → partial 4개, 단축키 표 단일화, F6·F9 패널 열린 동안 월드 입력 차단(사용자 요청) (b2a0a8b)

**남은 기술부채**:
- TC-M8-07B(보험 아이템 납품 경고)는 현재 납품 대상이 전부 잡템이라 재현 불가 — 장비 납품 퀘스트 추가 시 회귀
- 퀘스트 상태 변경마다 세이브 쓰기(납품 선택 토글 포함) — 지금 규모에선 문제없음, 체감 지연 시 재검토

**후속 조치**:
- [ ] Unity 컴파일 확인 (Claude 수정분 5794bca·86cd4e1·0612e1f·b2a0a8b는 에디터 컴파일 미확인)
- [ ] `QA/M8_통합검증_체크리스트.md` 1~6판 Play
- [ ] 디버그 패널 입력 차단 확인: F9 열고 WASD 무반응, 닫으면 이동 (TC-M8-22)
- [ ] HUD Canvas 변경 회귀: 영지 버튼·던전 인벤토리 클릭 (TC-M8-23)

**결론**: M8 코드 리뷰 완료. 버그 없음, 설계 결정 3건 반영, 기술부채 3건 처리. Play 검증 후 push·PR.

---

## 2026-08-28 — 리뷰 대상: Tick 폴더 재구성 + 플레이어 이동(`PlayerTurnActor`/`TilePathfinder`/`CameraFollow`/`DungeonSceneBootstrapper`)

**리뷰 방식**: 사용자가 코드를 먼저 읽고 자기 이해를 말하면, 그걸 기준으로 (1) 맞는 부분 확인 (2) 틀리거나 오해한 부분 교정 (3) 요청받은 네이밍/주석 보완을 적용하는 순서로 진행(사용자 지정 방식, 이후 리뷰도 이 형식 유지 예정).

**정적 리뷰 & 네이밍/주석 개선**:
- `TickCost.cs`: 구조 변경 불필요 확인(사용자 판단 동의)
- `ITurnActor.cs`: `CompleteTurn(this, tickCost)` 참조하는 주석으로 갱신(TickManager 리네이밍과 일관성 맞춤), 인터페이스 자체는 변경 없음
- `DungeonClock.cs`: `TicksPerMIA` → `MIAThresholdTicks`(의미가 더 명확), `LockDisplayToHour(int hour)` → `LockDisplayToMidnight()`(파라미터 제거 — `ALTAR_AND_TIME_SYSTEM_DESIGN.md`의 실제 설계가 "항상 자정 고정"이라 파라미터화가 과설계였음), `OnEnable` 이벤트 구독에 방어적 `-=` 선추가, `[Summary]` 헤더 및 필드별 설명 주석 추가. 리네이밍 전 `/tmp` 클린 클론으로 다른 참조 없는지 확인 후 진행
- `TickManager.cs`: `currentTurnElapsedSeconds` → `watchdogElapsedSeconds`, `warnedAboutCurrentTurn` → `stuckTurnAlreadyReported`, `CompleteTurn(actor, cost)` → `CompleteTurn(actor, tickCost)`(모든 호출부가 위치 인자라 안전 확인 후 진행). tick-시간(`NextActionTime`, 게임 논리 턴 순서)과 실시간(`watchdogElapsedSeconds`, 워치독 전용)이 서로 다른 개념이라는 걸 명시하는 `[주의]` 블록 추가
- **오해 교정**: `NextActionTime`이 10초 워치독과 관련 있다는 사용자 추측 — 틀림, 둘은 완전히 다른 시간 도메인(하나는 턴 순서용 추상 단위, 하나는 `Time.deltaTime` 누적 실초)이라는 걸 짚어줌
- **오해 교정**: `PlayerTurnActor.OnEnable`이 불필요하다는 추측 — 틀림. 플레이어는 씬에 항상 존재해서 스스로 등록/해제하는 self-registering 패턴이 맞는 설계(테스트 더미처럼 외부 부트스트래퍼가 등록해주는 방식과 의도적으로 다름)
- **오해 교정**: `Update()`에서 수동 입력이 자동 경로보다 우선이라 "경로 변경이 불가능해진다"는 추측 — 틀림. 오히려 수동 입력이 들어오면 기존 예약 경로를 버리고(`ClearQueuedAutoPath`) 새 입력을 따르는 구조라 언제든 자동 경로를 취소/변경 가능
- **오해 교정**: `StepAlongAutoPath()` 안에 반복문이 있다는 추측 — 틀림. 매 `Update()` 프레임마다 큐에서 한 칸씩만 꺼내는 구조(반복문 없음), 그래야 한 칸마다 애니메이션/몹 감지를 확인할 수 있음
- `PlayerTurnActor.cs`: 방향 벡터를 `Vector3Int`(2D인데 3 아닌가?)로 쓴 이유 설명 — `Tilemap`/`Vector3Int` API 자체가 셀 좌표를 3성분으로 다뤄서(z는 항상 0) 굳이 `Vector2Int`로 바꿔서 변환 비용을 만들 필요가 없음
- `HopTo()` 코루틴이 애니메이션 종료 즉시 `CompleteTurn`을 호출하는 구조라는 사용자 이해 확인 — 정확함. 단, 그 직전에 있던 `isMyTurn=false` 순서 버그(위 `DEV_LOG.md` 참고)까지 포함해서 최종 구조 확정
- `TilePathfinder.cs`: `cameFrom`/`visited`/`queue`/`found` → `previousCellOnPath`/`visitedCells`/`frontierQueue`/`foundGoal`로 개명, BFS 동작 원리(물결 확장 비유) + 메인 while 루프 + goal→start 역추적/`Reverse()` 부분에 단계별 주석 추가, `[Summary]` 클래스 헤더에 "BFS를 쓰는 이유(A* 대비 이득 없음)"와 "Pathfinding 폴더에 독립 배치한 이유(Enemy 재사용 목적)" 명시
- `CameraFollow.cs`: 구조 변경 불필요 확인(사용자 판단 동의), 이동 재구성 과정에서 GUID 재할당 버그가 있었으나 코드 자체 문제는 아니었음(`.meta` 복원으로 해결, 위 DEV_LOG 참고)
- `DungeonSceneBootstrapper.cs`: 흐름(TickManager/DungeonClock 준비 → ResetForNewRun → BeginSchedule) 이해 확인, `if (tickManager == null)` 폴백은 실질적으로 데드 코드(Unity의 Awake/OnEnable 순서상 `PlayerTurnActor.OnEnable`이 항상 먼저 TickManager를 만들어둠)라는 점 짚어줌 — 지금은 안전망으로 유지, 수정 불필요

**동적 리뷰(Test Scene 실행 결과)**:
- 방향키 4방향 이동, 마우스 클릭 자동 이동(경로 탐색 → 한 칸씩 순차 이동) 모두 정상 동작 확인
- 이동 1회당 0.2 tick 소비, 5칸 이동 시 DungeonClock 실시계 5분 갱신 확인
- 홉 애니메이션(사인 곡선 점프) 정상 재생 확인, 좌우 이동 시 스프라이트 반전 정상 확인
- 카메라가 플레이어 위치로 지연 없이 즉시 스냅 확인

**발견된 기술부채**:
- `IsMonsterVisibleNow()`가 항상 false를 반환하는 스텁 상태 — Enemy/시야 시스템 붙기 전까지는 정상, 다음 단계에서 실제 구현 필요
- 씬 참조 스크립트를 폴더 이동할 때 Unity가 GUID를 재할당해버리는 버그 패턴 확인됨 — 앞으로 스크립트 이동 시 반드시 이동 후 `.meta` GUID를 재확인하는 습관 필요(현재는 코드/툴로 자동화되어 있지 않음, 수동 체크에 의존)

**후속 조치**:
- [x] `DungeonClock`/`TickManager`/`ITurnActor`/`TilePathfinder` 네이밍·주석 보완
- [x] Y좌표 `.5` collider 문제 수정(`GetCellFootWorldPosition`)
- [x] 좌우 이동 스프라이트 반전 추가
- [x] "한 번 움직이면 멈춘다" 버그 두 원인 모두 수정
- [ ] `IsMonsterVisibleNow()` 실제 구현 — Enemy 시스템 붙을 때 진행
- [ ] Enemy `ITurnActor` 구현체로 Tick 파이프라인 end-to-end 실전 검증 (다음 세션 작업)

**결론**: 플레이어 이동 + Tick 파이프라인 연결 1차 리뷰 통과. 코드 구조/네이밍/주석 모두 보완 완료. 다음 단계는 경량 Enemy 구현으로 파이프라인이 Player 외의 실제 액터에서도 정상 동작하는지 검증.

---

## 2026-08-27 — 리뷰 대상: Tick 시스템 코어 (`TickCost`/`ITurnActor`/`TickManager`/`DungeonClock`) + 검증 하네스(`TickQueueTestActor`/`TickQueueTestBootstrapper`)

**정적 리뷰(코드 구조)**:
- `TickCost.cs`: 승인 — 상수만 들고 로직 없음, 이동배율 확장 여지도 주석으로 명시됨
- `ITurnActor.cs`: 초안 이름(`ITickActor`)을 `ITurnActor`로 변경 — "턴을 갖는 액터"라는 의미가 더 직관적
- `TickManager.cs`: 워치독을 `Time.realtimeSinceStartup` 기준 5초 경고에서 `Time.deltaTime` 누적 기준 10초 `LogError`(버그 리포트)로 변경 — 일시정지 중 오작동 방지, 심각도 격상
- **[발견/수정] `CompleteTurn → 다음 액터 호출`이 재귀 호출로 구현되어 있었음** — AI처럼 `OnTurnStart` 안에서 즉시 `CompleteTurn`을 부르는 액터가 연쇄로 이어지면 재귀 깊이가 액터 수만큼 쌓여 극단적인 경우 `StackOverflowException` 위험. `isAdvancingSchedule` 가드 + `AdvanceSchedule()` 반복문으로 리팩터링해서 해결 — 실행 스택 깊이가 항상 일정하게 유지됨

**동적 리뷰(Test Scene 실행 결과)**:
- `TickQueueTestBootstrapper`로 Fast(0.2)/Medium(1)/Slow(2) tick 비용 액터 3개 등록 후 실행
- Fast 액터가 Medium/Slow보다 훨씬 자주 차례를 받음 확인 — 큐 스케줄링 정상
- `DungeonClock`이 tick 누적에 맞춰 실시계(06:01 → 06:49) 정확히 갱신 확인 — Manager→Clock 이벤트 흐름 정상
- 20턴째 의도적으로 `CompleteTurn` 생략 → 10초 뒤 정확히 `LogError` 버그 리포트 발생 확인 — 워치독 정상
- 재귀→반복문 수정 후 재실행 대기 중(결과는 이전과 동일해야 함 — 스택 사용 방식만 변경, 로직 동일)

**발견된 기술부채**:
- (해결됨) 재귀 호출로 인한 잠재적 StackOverflow — 위 수정으로 해소
- `FindEarliestActor()`가 매번 전체 리스트를 선형 탐색(O(n)) — 지금 규모(수십 개 액터)에서는 문제없지만, 나중에 액터 수가 크게 늘면 우선순위 큐(힙)로 교체 고려 가능. 지금은 조치 안 함(과설계 방지)

**후속 조치**:
- [x] 재귀 → 반복문 리팩터링 (`TickManager.AdvanceSchedule`)
- [x] 재귀 수정 후 재실행 결과 확인 — 스택 트레이스가 `AdvanceSchedule → BeginSchedule → Start` 한 겹으로 평탄화됨, 순서/시계값/워치독 발동 결과는 수정 전과 동일함을 사용자가 확인
- [ ] Enemy 여러 마리 실제로 붙을 때 `FindEarliestActor()` 성능 재검토

**결론**: Tick 시스템 코어(4파일) + 검증 하네스(2파일) 1차 리뷰 통과. 다음 단계는 플레이어 이동을 Test Scene의 임시 맵 제네레이터에 연결하는 작업.
## 2026-11-20 — M10 던전 확장 정적 검토

- 숫자 설정을 CSV/공통 Tick 상수로 분리하고 5·10층 보스 잠금 조건을 공통화했다.
- 기존 씬·프리팹 및 Canvas는 변경하지 않았다.
- Play 검증은 `QA/TESTCASES_M10.md`로 이관했다.

# 2026-11-23 — M10 레벨·포션 밸런스

- 원정 상태는 `M10RunProgression`, CSV 로드는 `M10ProgressionDatabase`로 분리하고 영구 세이브 구조는 변경하지 않았다.
- 행동 비용 차감 전에 포션 효과 등록/아이템 소비가 성공해야만 턴을 완료하도록 기존 원자적 행동 경로를 재사용했다.
- 지속 회복은 액터별 비용 합계가 아니라 TickManager의 절대 시간축 최댓값을 기준으로 진행한다.
- 몬스터 카탈로그 비주얼이 존재할 때 공용 Animator가 스프라이트를 덮지 않도록 수정했다.
- 런타임 Roslyn 컴파일 오류 0. Play 체감·Unity Editor 자동 검증은 대기.

# 2026-11-23 — M10 몬스터 애니메이션 프레임

- 부분 일치 프레임 검색을 파일명 전체 정규식 매칭으로 교체하고 `f` 숫자로 정렬했다.
- 카탈로그의 runFrames가 런타임에서 사용되지 않던 연결 누락을 수정했다.
- 액션 없는 프레임은 idle/run 양쪽에 재사용하고, chest_mimic은 idle f0·run open f0~f2로 제한했다.
- Validate Data에 모든 sprite_key의 idle/run 존재와 타 몬스터 프레임 혼입 검사를 추가했다.

