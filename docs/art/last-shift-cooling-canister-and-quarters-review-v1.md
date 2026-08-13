# LAST SHIFT — CoolingCanister 제작 및 숙소 퀄리티 재검토

## CoolingCanister

- 실제 제작 자산: `ArtSource/LastShift/CoolingCanister/LPK_CoolingCanister.blend`
- Unity 전달 모델: `Assets/Art/LastShift/Props/LPK_CoolingCanister.fbx`
- 실제 소품 프리팹: `Assets/DoodleUp/Prefabs/Dressing/RealProps/LP_CoolingCanister.prefab`. 다른 RealProps와 같은 중첩 모델 패턴이며 루트 Transform은 단위값, 원통형 CapsuleCollider는 `반지름 0.275m / 높이 1.10m`다. `.cs` wiring은 포함하지 않는다.
- 치수: `0.44 × 1.07 × 0.44m`. 게임플레이 판정 치수 `0.55 × 1.10 × 0.55m` 안에 들어가며 손잡이까지 포함한 높이는 거의 일치한다.
- 시각 방향: 원통 압력용기, 상·하 충격 링, 양측 가드, 상단 운반 손잡이로 무거운 교체 부품 실루엣을 만들었다. 청록 냉매 띠는 냉각 계통, 주황 밸브와 전면 표식은 상호작용 면을 읽힌다.
- 제작 스크립트: `Tools/art/generate_cooling_canister.py`. Blender 4.5 LTS에서 `.blend`, `.fbx`, 검토 PNG를 재생성한다.

## 숙소 재검토

숙소의 정본 면적은 `8 × 6m`, 천장고 `3.0m`이고 드레싱에는 4인 침상, 개인 사물함, 개별 취침등이 이미 들어가 있다. 현재 `LPK_Quarters_Bunk` 실제 FBX와 드레싱용 침상 프리팹의 치수도 `약 2.0 × 0.45 × 0.9m`로 맞는다.

재검토 결과 중앙등 하나만 실제 배치되어 침상 4개의 개인 영역이 평평하게 읽혔다. 네 침상 각각에 기존 `LSDress_BerthLight`를 추가했다. 좌우 침상은 서로 마주보도록 `Y 0°/180°`로 갈라졌고, 하단 `0.92m`, 상단 `2.02m`에 배치해 침상 프레임과 겹치지 않게 했다. 드레싱 슬롯은 `102 → 106`개가 됐다.

1. 숙소 문에서 네 침상의 실루엣이 한 번에 겹쳐 뭉치지 않는가.
2. 상단 침상과 천장 사이가 답답하지 않고, 하단 침상 머리맡 조명이 통로로 새지 않는가.
3. 개인화 소품이 이동 동선보다 밝게 튀지 않는가.

## 검수 기준

- CoolingCanister는 3m 거리에서도 손잡이와 청록 띠로 배터리와 구분되어야 한다.
- 랙에 놓였을 때 양측 가드가 외곽 실루엣을 유지하고, 잡기 콜라이더 `0.55 × 1.10 × 0.55m` 밖으로 크게 튀어나오지 않아야 한다.
- Blender GUI 브리지 미연결 상태에서 중립 제품 렌더로 실루엣·명도 분리를 확인했다. Unity 씬 조명 아래 최종 확인은 수동 항목으로 남긴다.
