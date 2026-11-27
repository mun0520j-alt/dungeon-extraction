# 아이템 CSV 데이터 사전

> 원본: `Assets/Resources/Data/items.csv`. M6부터 출격 가방 격자 크기의 권위 데이터도 이 파일에 둔다. M7 경제 수치는 `Assets/Resources/Data/m7_economy.csv`, M8 퀘스트·우호도 수치는 `m8_quests.csv`와 `m8_economy.csv`, M9 Stash 크기는 `m9_stash.csv`에서 관리한다.

## 컬럼

| 컬럼 | 형식 | 필수 | 의미 | 검증 규칙 |
|---|---|---|---|---|
| `item_id` | string | Y | 저장·참조용 고유 ID | 중복 금지, 표시명으로 사용 금지 |
| `item_name` | string | Y | UI 표시명 | 빈 값 금지 |
| `item_type` | enum | Y | Weapon, Shield, Armor, Bag, Junk, Consumable | 지원하지 않는 값은 로드 실패 처리. Junk·Consumable은 장착 불가 |
| `rarity` | enum | Y | Normal, Rare, Epic, Unique, Legendary | 1~4층 Legendary 드롭 금지 |
| `attack_bonus` | int | Y | 장착 공격력 보너스 | 0 이상 |
| `defense_bonus` | int | Y | 장착 방어력 보너스 | 0 이상 |
| `hp_bonus` | int | Y | 장착 최대 HP 보너스 | 방패·갑옷만 양수, 그 외 0 |
| `capacity_bonus` | int | Y | M5 목록 가방과 v2 세이브 해석용 용량 보너스 | M6 배치 판정에는 사용하지 않음. 마이그레이션 호환용 유지 |
| `icon_key` | string | N | 아이콘 리소스 키 | 빈 값이면 대체 표시 사용 |
| `drop_weight` | int | Y | 같은 등급·풀 안의 상대 가중치 | 0은 일반 드롭 제외 |
| `sell_price` | int | Y | BaseCamp 공용 상인 판매가 | Normal 10, Rare 30, Epic 80, Unique 200 초안 |
| `grid_w` | int | Y | 아이템의 기본 가로 칸 수 | 1 이상. 회전 시 `grid_h`와 교환 |
| `grid_h` | int | Y | 아이템의 기본 세로 칸 수 | 1 이상. 회전 시 `grid_w`와 교환 |
| `bag_grid_w` | int | Y | 해당 가방 장착 시 출격 격자 가로 | Bag만 1 이상, 그 외 0 |
| `bag_grid_h` | int | Y | 해당 가방 장착 시 출격 격자 세로 | Bag만 1 이상, 그 외 0 |

## 현재 데이터 요약

| 타입 | Normal | Rare | Epic | Unique | Legendary | 합계 |
|---|---:|---:|---:|---:|---:|---:|
| Weapon | 2 | 1 | 1 | 1 | 1 | 6 |
| Shield | 2 | 1 | 1 | 1 | 1 | 6 |
| Armor | 2 | 1 | 1 | 1 | 1 | 6 |
| Bag | 1 | 1 | 1 | 1 | 1 | 5 |
| Junk | 2 | 1 | 1 | 1 | 1 | 6 |
| Consumable | 2 | 1 | 1 | 0 | 0 | 4 |
| 합계 | 11 | 6 | 6 | 5 | 5 | 33 |

## M10 전투 수치

| 타입 | Normal | Rare | Epic | Unique | Legendary |
|---|---:|---:|---:|---:|---:|
| Weapon 공격 보너스 | 20 | 35 | 55 | 80 | 110 |
| Shield 방어 / HP | 5 / 15 | 8 / 30 | 12 / 50 | 16 / 75 | 22 / 110 |
| Armor 방어 / HP | 6 / 25 | 10 / 45 | 15 / 75 | 21 / 110 | 28 / 160 |

- 기본 지급 장비는 기본 검 공격 +15, 기본 방패 방어 +4/HP +10, 기본 갑옷 방어 +5/HP +20이다.
- 아이템 크기는 무기 1×3, 방패 2×2, 갑옷 2×3이다. 가방은 외부 크기와 내부 수납 크기를 동일하게 사용한다(Normal 4×2, Rare 3×3, Epic 5×3, Unique 4×5).
- 출격 가방 격자는 기본 3×2, Normal 4×2, Rare 3×3, Epic 5×3, Unique 4×5다.
- 하위 가방은 부모 격자에서 내부 수납량과 같은 면적을 차지하므로 순수 용량 이득이 없고 분류·정리 용도로만 사용한다.
- `capacity_bonus`의 +2/+3/+4/+6은 v2 데이터 해석을 위해 남기며 M6 격자 배치에는 영향을 주지 않는다.
- `basic_weapon`, `basic_shield`, `basic_armor`는 크기를 코드에 하드코딩하지 않기 위한 드롭 가중치 0의 기본 지급 데이터다.
- 가방 `drop_weight=50`은 같은 등급 내 약 14% 드롭을 노린 초안으로, 통합 Play 후 확정한다.
- M7 잡템 5종의 표시명은 임시다. 잡템은 장착할 수 없으며 판매·향후 퀘스트 납품에 사용한다.
- M7 `escape_scroll`은 1×1 Consumable이며 일반 드롭에서는 제외(`drop_weight=0`)하고 상인 구매로 공급한다.
- 같은 등급 내 가중치 기준 잡템 비율은 Normal 200/550(36.36%), Rare·Epic·Unique 각각 50/400(12.5%)다.

## 변경 절차

1. ID 중복과 enum 값을 확인한다.
2. 코드 수정 없이 CSV만 변경한다.
3. `Dungeon Tools > QA > M6 Grid Model Validation`으로 크기·가방 형상·경계·겹침·회전을 확인한다.
4. TC-M2-42로 이름·등급·스탯·가중치 반영을 확인한다.
5. M3 분포 자동화를 실행한다.
6. 스키마 변경이면 세이브 마이그레이션 영향을 기록한다.

## M8 퀘스트 CSV

원본: `Assets/Resources/Data/m8_quests.csv`

| 컬럼 묶음 | 의미 |
|---|---|
| `quest_id`, `order`, `route`, `type`, `prerequisite_id` | 고유 ID·순서·Common/Combat/Collection 분기·Delivery/Escape/Carry 종류·선행 조건 |
| `target_item_id` | 단일 또는 복합 납품 조건. 복합은 `item_id:개수|item_id:개수` 형식 |
| `target_rarity`, `target_category`, `target_count`, `target_floor`, `target_value` | 운반 등급·범주·수량·도달 층·판매가 합계 조건 |
| `requires_found_in_raid`, `requires_boss_kill` | 인레이드 필요(납품·운반 퀘스트, 0/1)·보스 처치 필요 여부(0/1). 운반 퀘스트는 인레이드 아이템만 센다 |
| `reward_reputation`, `reward_gold`, `reward_item_id`, `reward_item_count` | 우호도·Gold·아이템 보상 |
| `title`, `description` | 상인 퀘스트 UI 표시 문자열 |

- `equipment_epic_random`은 Epic 무기·방패·갑옷 중 하나를 보상하는 예약 키다.
- 두 루트 모두 공통 퀘스트를 포함한 우호도 합계가 300이어야 한다.
- `Dungeon Tools > QA > M8 Quest Data Validation`에서 정의 수·선행·분기 합계·복합 납품을 검사한다.

## M8 경제 CSV

원본: `Assets/Resources/Data/m8_economy.csv`

- Lv2/Lv3 필요 우호도와 Gold, 레벨당 판매 보너스·구매 할인·보험 반환 보너스를 관리한다.
- 런타임 가격과 보험 판정은 UI가 아니라 `LootRuntimeState`에서 이 설정을 적용한다.

## M9 Stash CSV

원본: `Assets/Resources/Data/m9_stash.csv`

| 키 | 의미 | 현재값 |
|---|---|---:|
| `base_width` | Stash 기본 가로 | 10 |
| `base_height` | Stash 기본 세로 | 20 |
| `rows_after_q03` | q03 완료 확장 행 | 2 |
| `rows_after_q06` | q06a 또는 q06b 완료 확장 행 | 2 |
| `sale_basket_width` | 판매 바구니 가로 | 5 |
| `sale_basket_height` | 판매 바구니 세로 | 20 |

- 기본 크기는 10×20, 최대 크기는 10×24이며 Gold 확장은 없다.
- Stash와 판매 바구니는 각자 세로 스크롤한다. 판매 바구니는 좁은 UI 폭에 맞춰 5칸 폭을 사용한다.
- 확장 여부는 세이브에 중복 저장하지 않고 완료 퀘스트에서 계산한다.

## 알려진 제한

- 현재 `items.csv`는 M6 장비·가방 최소 데이터이며 장신구·소모품·수집품 전체 설계를 포함하지 않는다.
- `ITEM_SYSTEM_DESIGN.md`는 장기 설계 근거이고 CSV는 현재 런타임 데이터다.
- 가방 이름과 드롭 가중치는 M5 통합 Play 전 임시값이다.
## M10 추가 CSV (2026-11-20)

- `m10_dungeon.csv`: 최종 층, 보스 층, 깊은 층 방 개수, 탈출 스크롤 충전 Tick·수량·재고 상한.
- `m10_enemies.csv`: 몬스터 ID·스프라이트 키·등급·HP·공격·방어·행동·크기·스크롤 확률.
- `m10_floor_spawns.csv`: 층별 일반/엘리트 후보와 생성 수.
- `m10_levels.csv`: 원정 레벨 1~12의 누적 XP와 HP·공격·방어 보너스. 영구 세이브하지 않는다.
- `m10_potions.csv`: 힐 포션 `item_id`, 최대 HP 비율(`heal_pct`), 지속 Tick, 사용 Tick.
- `m10_enemies.csv` 추가 열: `xp`, `potion_drop_chance`. 수치는 M10 임시 밸런스다.
- `m10_dungeon.csv` 추가 키: 얕은/깊은 층 도착 XP, 층당 빠른 도착 Tick, 보너스 %, 피해 공식 K, 휴식 회복 %, 보스 광역 고정 피해, 심야 층 배율 설계값.
- `m10_floor_themes.csv`: 층 구간별 바닥·벽 색조와 장식 세트 키.
- `m10_floor_decorations.csv`: 장식 세트별 바닥·벽 1,000칸당 밀도와 0x72 장식 키 목록.
- `m10_loot_rarity.csv`: 층별 Normal~Legendary 및 보스 수량·등급 확률. 각 확률 묶음은 100이어야 한다.
- `items.csv` Legendary 5종: 고대 왕의 대검(1×3), 심연 수호자의 방패(2×2), 고대 왕의 판금갑옷(2×3), 심연 원정 배낭(외부·내부 5×6), 오크 왕의 인장(1×1).
- Legendary 아이템은 상점 판매 대상이 아니며 6~10층 드롭 풀에서만 사용한다. 뽑힌 등급에 후보가 없으면 바로 Normal로 가지 않고 한 단계씩 낮은 등급을 탐색한다.

## M11 상인·퀘스트·궁수 CSV (2026-11-27)

- `m11_merchants.csv`: 상인 ID, 이름, 취급 아이템 태그와 태그 일치/불일치 판매 배율을 관리한다. 현재 무기상·방어구상·잡화상·수집상 4명이다.
- `m8_quests.csv`: `merchant_id`, 누적 여부, 층 범위, 엘리트·심야·시간 제한, 핵심 완료 수 제외 여부를 추가했다. 핵심 퀘스트는 상인별 10개, 보호 주머니 확장 2개는 별도 집계한다.
- `m10_enemies.csv`: `attack_type`, `range`, `ranged_damage_pct`, `melee_damage_pct`를 추가했다. `Archer`는 직선 사거리·원거리/인접 피해 비율을 이 열에서 읽는다.
- `m10_floor_spawns.csv`: `archer_id`를 추가했다. 값이 있는 비보스 층은 일반몹 3마리 중 마지막 1마리를 궁수로 생성한다.
- 세이브 v9는 상인별 우호도·레벨을 저장하고, v8 공용 우호도는 기존 퀘스트를 이어받은 방어구상으로 이관하며 `save_v8.bak`을 보존한다.

