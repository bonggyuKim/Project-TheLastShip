# DoodleUp AI 개발 시스템 기술서

> NAN 2026 Game × AI Hackathon 참가신청용 초안

- 문서 상태: DRAFT 0.1
- 기준일: 2026-08-10
- 대상 프로젝트: DoodleUp / 현재 주력 게임 LAST SHIFT
- 저장소 기준: D:\Project-DoodleUp, 기준 HEAD 1d9a04c
- 작성 목적: 예선 과제인 AI를 활용한 게임 제작에서 DoodleUp이 실제로 사용한 AI 개발 방식과 검증 근거를 설명한다.

이 문서는 완성된 홍보 문안이 아니라 사실 검증이 가능한 기술 초안이다. 저장소나 운영 기록으로 확인한 내용은 확정 사실로, 아직 증빙을 모으지 못한 내용은 제출 전 확인으로 구분한다.

## 0. 신청서용 요약 초안

DoodleUp은 생성형 AI로 에셋 한두 개를 만드는 데 그치지 않고, 게임 개발 전 과정을 반복 실행하는 AI 개발 시스템을 구축했다. 인간 디렉터가 목표와 플레이 감각을 결정하면 AgentDesk가 작업을 카드로 분해하고, 기획·게임 기술·아트·QA 역할의 AI 에이전트가 분리된 Git 작업공간에서 구현한다. AnchorMind 계열의 장기기억 계층인 Memento는 프로젝트의 결정, 오류, 검증 절차와 사용자 선호를 세션 사이에 유지한다. 구현 결과는 Unity 컴파일, EditMode·PlayMode 테스트, 수치 기반 씬 검증과 독립 QA를 거쳐야 통합된다. 이 과정으로 DoodleUp의 드로잉 입력, 네트워크 협동, 레벨 배치, 캐릭터 리깅·애니메이션 파이프라인을 실제 플레이 가능한 코드와 에셋으로 연결했다. 핵심은 AI가 게임을 대신 판단하는 것이 아니라, 인간의 창작 의도를 전문 에이전트가 구현하고 자동 증거로 검증하도록 만든 데 있다.

## 1. 기술서의 범위와 핵심 주장

### 1.1 핵심 주장

DoodleUp에서 AI는 단일 채팅 도구가 아니라 다음 폐쇄 루프를 수행하는 개발 운영 계층이다.

    인간 디렉터의 의도와 수용 기준
                ↓
    AgentDesk 작업 분해·역할 배정·상태 관리
                ↓
    전문 AI 에이전트의 기획·코드·아트·QA 작업
                ↓
    격리된 Git worktree와 Unity·Blender 도구 실행
                ↓
    컴파일·자동 테스트·수치 검증·독립 QA
                ↓
    인간 플레이 검토와 승인 또는 수정 지시
                ↺

AnchorMind/Memento 장기기억은 이 전 과정을 가로질러 결정, 오류, 절차, 선호와 작업 이력을 연결한다.

### 1.2 제출 범위

- DoodleUp 저장소와 LAST SHIFT는 해커톤 이전부터 개발한 기존 자산이다.
- 이 기술서는 기존 프로젝트에서 검증한 AI 개발 시스템과 활용 사례를 설명한다.
- 본선에서 새로 만드는 결과물은 기존 자산, 본선 기간 신규 제작분, 외부·AI 생성 자산을 명확히 구분해야 한다.
- 해커톤 규정상 요구되는 AI 도구와 활용 범위는 숨기지 않고 공개한다.

### 1.3 근거 등급

| 등급 | 의미 | 문서 내 처리 |
|---|---|---|
| 저장소 확인 | 코드, 테스트, 문서, 커밋으로 재확인 가능 | 경로·수치·커밋을 함께 적는다 |
| 운영 기록 확인 | AgentDesk 카드, 에이전트 세션, 로그에서 확인 | 제출 전 내보낸 로그나 화면 증빙을 붙인다 |
| 제출 전 확인 | 모델 버전, 생성 자산 라이선스, 시간·비용처럼 추가 대조가 필요 | 수치나 단정 대신 체크리스트로 남긴다 |

## 2. 해결하려 한 문제

소규모 팀이 게임을 만들 때 병목은 아이디어 생성보다 문맥 유지와 통합 검증에 가깝다.

- 기획 의도가 코드, 씬, 아트 단계로 넘어가며 달라진다.
- 긴 개발에서 과거 결정과 이미 해결한 오류가 잊혀진다.
- 여러 작업이 동시에 진행되면 같은 Unity 프로젝트와 씬을 건드려 충돌한다.
- AI가 코드를 빠르게 생성해도 실제 컴파일, 플레이, 네트워크 동작을 증명하지 못하면 부채가 된다.
- 결과가 좋다는 인상만으로는 누가 무엇을 만들었고 왜 통합했는지 추적하기 어렵다.

DoodleUp은 이를 기억, 역할, 격리, 자동 검증, 인간 승인이라는 다섯 축으로 해결했다.

## 3. AI 개발 시스템 아키텍처

### 3.1 구성 요소

| 계층 | 기능 | DoodleUp에서의 역할 |
|---|---|---|
| 인간 디렉터 | 목표, 우선순위, 재미와 감각 판단, 최종 승인 | 플레이 경험과 범위의 최종 책임자 |
| AgentDesk | 칸반, 작업 분해, 역할 배정, 세션·진행 상태 관리 | 큰 기능을 카드와 수용 기준으로 바꾸고 담당 에이전트에 연결 |
| 역할형 AI 에이전트 | 기획, 기술, 아트, QA 등 전문 작업 | 역할별 관점과 완료 기준을 유지 |
| AnchorMind/Memento | 세션 간 장기기억과 검색 | 결정·오류·절차·선호를 프로젝트 범위로 저장하고 작업 전 회수 |
| Git 격리 계층 | 카드별 branch/worktree/commit | 동시 작업 충돌을 줄이고 변경 출처를 보존 |
| 제작 도구 계층 | Unity, Blender, 코드·파일 도구 | AI가 코드뿐 아니라 씬 빌드, 임포트 설정, 리그와 검증 도구를 실행 |
| 검증 계층 | 컴파일, EditMode, PlayMode, 빌드, 정량 검사, QA | 자연어 완료 선언을 기계 판정 가능한 증거로 변환 |

### 3.2 역할형 에이전트 조직

| 역할 | 주된 책임 | 완료 기준의 예 |
|---|---|---|
| project-manager | 범위, 우선순위, 카드 분해, 담당 배정, 칸반 상태 | 의존성과 수용 기준이 명확하고 실제 상태와 보드가 일치 |
| game-planning | 플레이어 동기, 규칙, UX 흐름, 구현 부담 검토 | 규칙이 플레이 행동과 구현 가능한 상태 전이로 연결 |
| game-tech-director | Unity 런타임, 네트워크, 에디터 자동화, 성능·테스트 | 컴파일과 대상 테스트 통과, 재현 가능한 로그 확보 |
| game-art | 비주얼 방향, 모델링·리깅·애니메이션 제작 | 원본과 교환 포맷, 포즈·실루엣 증빙, 엔진 전달 조건 충족 |
| game-qa | 재현, 경계값, 회귀, 독립 증거 검토 | 수용 기준별 PASS/FAIL과 원시 증거를 분리해 보고 |

역할 분리는 모델을 여러 번 부르는 장치가 아니라 판단 기준을 분리하는 장치다. 구현자가 스스로 완료를 선언하는 대신 QA 역할이 같은 수용 기준을 독립적으로 다시 계산한다.

## 4. 범용적이지만 핵심적인 AI 활용 기술

### 4.1 AnchorMind 계열 장기기억: Memento

일반 대화형 AI는 세션이 바뀌면 프로젝트의 결정과 실패 이력을 잃기 쉽다. DoodleUp에서는 Memento를 AnchorMind 계열의 프로젝트 기억 계층으로 사용한다.

#### 기억 단위

- fact: 확인된 프로젝트 사실과 환경
- decision: 선택한 설계와 그 이유
- error: 재현 조건, 원인, 실패한 접근
- preference: 인간 디렉터의 작업·표현 선호
- procedure: 다시 실행할 빌드·검증·복구 절차
- relation: 기능, 결정, 오류 사이의 연결
- episode: 한 작업의 목표, 사건, 결과를 포함한 맥락

#### 작업 루프

1. 작업 시작 전에 프로젝트명, 현재 작업과 관련 키워드로 기억을 검색한다.
2. 검색 결과를 현재 코드, 씬, 로그와 대조한다.
3. 오래되었거나 충돌하는 기억은 현재 사실처럼 사용하지 않는다.
4. 확정된 결정, 해결된 오류, 재사용할 절차만 저장한다.
5. 긴 작업이 끝나면 핵심 흐름을 회고 단위로 정리한다.

#### DoodleUp에서 얻는 효과

- Unity 버전, 프로젝트 경로, 네트워크 권한 모델 같은 기반 사실을 세션마다 다시 설명하지 않는다.
- 과거 QA의 차단 요인과 해결 절차를 다음 기능의 회귀 조건으로 재사용한다.
- 인간 디렉터의 실제 플레이 감각 판단은 자동 테스트와 구분해 기억한다.
- 역할과 프로젝트 범위를 나누어 다른 프로젝트의 기억이 섞이는 것을 줄인다.

#### 안전 원칙

- 기억은 현재 저장소와 실행 상태보다 우선하지 않는다.
- 액세스 키, 비밀번호, 토큰, 인증 헤더와 개인식별정보는 저장하지 않는다.
- 추측, 임시 로그, 쉽게 재생성되는 출력은 장기기억으로 남기지 않는다.
- 검색 결과의 관련성과 충분성을 피드백해 이후 검색 품질을 개선한다.

### 4.2 칸반 기반 작업 분해와 수용 기준

큰 요청을 한 번에 구현하도록 맡기지 않고, 기능 카드를 기획·구현·검증 단계로 나눈다. 각 카드에는 다음을 포함한다.

- 플레이어에게 보이는 목표
- 변경 허용 범위와 금지 범위
- 선행 카드와 후속 카드
- 컴파일·테스트·플레이 수용 기준
- 담당 역할과 검토 역할
- 결과 커밋, 로그, 문서 링크

이 구조는 AI의 자연어 자신감을 완료 판정으로 사용하지 않게 한다. 완료는 수용 기준과 증거가 닫혔을 때만 성립한다.

### 4.3 카드별 Git worktree와 변경 출처

각 작업은 가능하면 별도 branch와 worktree에서 수행한다. 이를 통해 다음을 얻는다.

- 서로 다른 에이전트가 같은 저장소를 동시에 수정할 때 충돌 범위를 제한한다.
- 카드 ID, 커밋, 변경 파일과 테스트 결과를 연결할 수 있다.
- 실패한 시도를 main에서 분리하고 필요한 변경만 검토해 통합한다.
- 특정 기능이 언제, 어떤 근거로 들어왔는지 재현할 수 있다.

커밋 수는 품질 점수가 아니다. 중요한 것은 커밋과 카드, 수용 기준, 테스트 증거 사이의 연결이다.

### 4.4 도구 실행형 에이전트

DoodleUp의 AI 에이전트는 답변만 생성하지 않고 실제 개발 도구를 실행한다.

- 저장소 검색과 코드 수정
- Unity 컴파일, Editor 명령, 테스트와 플레이어 빌드
- 씬·프리팹·Animator Controller 생성 및 검증
- Blender 원본, FBX, 리그·웨이트·애니메이션 산출물 관리
- 정량 검산 스크립트 실행
- Git diff, 상태, 커밋을 통한 변경 추적

이를 통해 자연어 기획을 코드, 에셋, 테스트, 로그라는 서로 대조 가능한 산출물로 변환한다.

### 4.5 자동 검증과 인간 승인

자동화가 잘 판단하는 항목과 사람이 판단해야 하는 항목을 분리한다.

| 자동화에 맡기는 것 | 인간이 최종 판단하는 것 |
|---|---|
| 컴파일 오류와 예외 | 조작감, 가독성, 긴장감, 재미 |
| 상태 전이와 경계값 | 애니메이션의 생동감과 캐릭터성 |
| 좌표, 충돌, 네트워크 권한 | 화면 구도와 플레이 맥락 |
| EditMode·PlayMode 회귀 | 기능 우선순위와 범위 |
| 빌드와 로그 무결성 | 출시·제출 여부 |

따라서 자동 테스트 PASS는 게임이 재미있다는 뜻이 아니다. 기계 검증이 기술적 불확실성을 줄인 뒤 인간이 실제 플레이 감각을 승인한다.

### 4.6 관측, 재개와 문맥 예산

- 카드 상태와 세션 진행을 별도로 기록해 장시간 작업이 실제 실행 중인지, 대기 중인지 구분한다.
- 장시간 명령은 로그 파일로 남기고 에이전트가 요약을 읽는다.
- 이미지와 대형 로그는 필요한 구간만 사용해 문맥 압축 비용을 관리한다.
- 중단된 세션은 작업 카드, worktree, 커밋과 기억을 이용해 이어서 수행한다.
- Unity처럼 공유 자원을 많이 쓰는 작업은 동시성보다 자원 충돌과 cold cache 비용을 함께 고려한다.

## 5. 표준 개발 흐름

1. 인간 디렉터가 플레이 목표와 반드시 지켜야 할 감각을 말한다.
2. project-manager가 요청을 카드, 의존성, 수용 기준으로 정리한다.
3. 관련 과거 결정과 오류를 AnchorMind/Memento에서 검색한다.
4. 담당 전문 에이전트가 격리된 worktree에서 구현한다.
5. AI가 Unity 또는 Blender 도구로 산출물을 만들고 자체 정적 검사를 수행한다.
6. 컴파일, 대상 테스트, 필요 시 전체 회귀와 정량 검사를 실행한다.
7. game-qa가 원시 증거와 수용 기준을 독립 검토한다.
8. 인간 디렉터가 실제 Editor 플레이와 시각·조작 감각을 확인한 뒤 승인하거나 새 수정 카드를 만든다.

## 6. DoodleUp 실제 적용 사례

### 6.1 DU-02: 리셋 가능한 솔로 코스와 독립 QA

AI 에이전트는 부트스트랩 가능한 Unity 씬, 상태 리셋, 런타임 증거 수집과 QA 집계를 구현했다. 구현 에이전트와 별도의 QA 역할이 원시 CSV, 보고서와 실행 파일 해시를 다시 계산했다.

확인된 결과:

- Unity 6000.4.0f1에서 C# 컴파일, 씬 빌드, Windows player build PASS
- EditMode 12/12 PASS
- PlayMode 2/2 PASS
- 30·60·144 FPS standalone sampling 3/3 PASS
- 3 lanes × 2 reset paths 6/6 PASS
- runtime task-state 4/4 PASS
- 최종 독립 QA 판정 PASS

근거: [DU-02 REV2 최종 독립 QA 수용 검토](qa/reports/2026-07-31-doodleup-du02-acceptance-review.md)

이 사례의 의미는 AI가 테스트 코드를 만들었다는 데만 있지 않다. 상태를 실제로 교란한 before와 canonical state로 돌아온 after를 해시와 raw 데이터로 증명하고, QA가 이를 독립 재계산했다.

### 6.2 DU-03B/C: 두 입력 방식과 같은 게임 규칙

Aim과 Trajectory 두 입력 어댑터가 같은 StrokeSession과 드로잉 규칙을 사용하도록 구현했다. 입력 edge를 LateUpdate에서 정확히 한 번 소비하고, release 프레임의 CANDIDATE → RELEASE → AUTO_COMMIT 순서를 테스트로 고정했다.

확인된 결과:

- 공식 Unity CLI/Pipeline live Editor EditMode 41/41 PASS
- 공식 Unity CLI/Pipeline live Editor PlayMode 9/9 PASS
- ray-plane intersection과 HandMarker mapping 오차 허용치 1e-5u
- 물리 키보드·마우스 조작감과 시야는 인간 플레이 체크포인트로 유지

근거: [DU-03B/C 에디터 플레이 검증](qa/du-03bc-verification.md)

### 6.3 LAST SHIFT 중앙 광장: 감각적 배치를 정량 관문으로 변환

기획 에이전트가 중앙 허브 배치를 좌표와 규칙으로 정의하고, 기술 에이전트가 검산 스크립트와 Unity EditMode 검사로 옮겼다.

확인된 설계 검산:

- 7개 구조물의 모든 21개 쌍 검사에서 겹침 0
- 광장에 연결되는 문 6개의 경계 정합 확인
- 0.05m 격자, 코어 제외 51,200점에서 3구역 동시 판독 0점
- RG-1 최악 이탈 개산 4.26초, 설계 한도 10초

근거:

- [중앙 광장 허브 배치 v1](central-plaza-hub-layout-v1.md)
- [좌표 정합 검산 스크립트](tools/plaza_hub_check.py)

이 접근은 좋을 것 같은 레이아웃을 AI가 제안하는 데서 멈추지 않고, 가시성과 이동 거리를 재실행 가능한 수치로 만들었다.

### 6.4 LAST SHIFT 네트워크 협동과 방 코드

기술 에이전트는 네트워크 플레이어와 아이템의 owner-authoritative transform, 서버가 쓰는 상태 변수, 잡기·놓기·배치 흐름을 코드와 검사기로 연결했다. 이후 외부 서비스 의존 없이 같은 LAN에서 6자리 방 코드로 호스트를 찾는 질의·응답형 discovery를 추가했다.

확인 가능한 구현:

- 플레이어와 아이템용 LastShiftOwnerNetworkTransform
- 상태 NetworkVariable의 server write 권한
- 네트워크 씬 builder와 verifier
- room lobby와 lobby blocking PlayMode test
- 6자리 room code와 UDP LAN discovery

관련 커밋:

- bfa63ff: 방 코드와 LAN discovery
- 6f64fb8: 방 코드 lobby
- aa4be45: 방 코드 networking 통합
- 303db5c: network player animation 연결

AI는 초기 네트워크 골격을 빠르게 만들었지만 권한, 보간, 메시지 순서와 화면 소유권 같은 경계 문제는 테스트와 후속 수정으로 닫았다.

### 6.5 라임 외계인: 리깅·애니메이션에서 Unity 연결까지

아트와 기술 역할을 나눠 Blender 원본, FBX 교환 파일, 웨이트 증빙과 Unity Animator 구성을 연결했다.

확인된 리깅 근거:

- root → pelvis → spine → chest → head 중심 계층
- 팔·다리 변형 체인과 2-bone IK, pole control
- 5,772개 결합 메시 정점의 웨이트 할당 확인
- 정점당 최대 4개 이하 bone influence
- 자동 웨이트 후 topology-neighbor 기반 국소 smoothing
- 위치·회전 0, scale 1로 runtime transform 정규화

Unity 자동 설정 도구 LimeAlienAnimatorSetup.Build는 다음을 수행한다.

- 8개 FBX clip의 Generic Rig import와 loop 설정
- main avatar 생성과 clip avatar 공유
- locomotion/jump Base Layer 구성
- 상체 Avatar Mask 기반 Carry Override Layer 구성
- animated prefab과 preview scene 생성
- root motion 비활성화
- avatar, clip, layer, state와 preview 산출물 검증

근거:

- [라임 외계인 리그 v1](art/last-shift-lime-alien-rig-v1.md)
- [LimeAlienAnimatorSetup.cs](../Assets/DoodleUp/Editor/LimeAlienAnimatorSetup.cs)
- 10acf30: Generic animation controller 연결
- 3e2dd29: 전체 animation과 controller 통합

사람은 중립·극단 포즈의 silhouette, deformation, animation feel을 검토하고 AI 에이전트는 반복적인 import, controller wiring, 검증과 수정 이력을 담당했다.

## 7. 사용 도구와 AI 활용 공개 초안

| 도구·계층 | 사용 목적 | 주요 입력 | 주요 출력 | 인간 검토 |
|---|---|---|---|---|
| AgentDesk | 칸반, 작업 분해, 역할 배정, 실행 상태 관리 | 목표, 카드, 수용 기준 | 담당 세션, 상태, 작업 이력 | 우선순위와 통합 승인 |
| AnchorMind/Memento | 프로젝트 장기기억 검색·저장 | 결정, 오류, 절차, 선호 | 재사용 가능한 기억 파편과 연결 | 현재 코드와 대조, 민감정보 제외 |
| Claude 계열 코딩 에이전트 | 역할별 기획·구현·검토 | 카드 문맥, 저장소, 테스트 | 코드, 문서, 분석, 수정안 | diff·테스트·플레이 검토 |
| OpenAI Codex | 저장소 분석, 구현, 문서화, 검증 자동화 | 요청, 저장소, 도구 결과 | 코드·문서 변경과 검증 결과 | diff·테스트·플레이 검토 |
| Unity 6와 Editor/CLI | 게임 실행·빌드·자동 테스트 | C# 코드, scene, prefab | build, EditMode·PlayMode 결과, logs | 실제 Editor 플레이 |
| Blender | 모델링, 리깅, 웨이트, 애니메이션 원본 | mesh, rig, animation data | .blend, .fbx, pose evidence | silhouette와 deformation 검토 |
| Git branch/worktree | 변경 격리와 provenance | 카드별 변경 | commit, diff, merge history | 통합 범위 승인 |
| Tripo·SF3D 계열 도구 | 3D concept 또는 생성 후보 자산 | concept image 또는 prompt | image·mesh 후보 | 제출 전 도구·모델 버전과 라이선스 확인 |

제출 전 확인:

- 각 AI 서비스의 정확한 제품명, 모델명과 사용 시점
- API와 GUI 사용을 구분한 실행 기록
- Tripo·SF3D 관련 현재 작업공간 산출물의 실제 생성 경로
- 생성 자산별 입력 출처, 상업 이용 조건, 수정 내역
- 외부 에셋과 학습 데이터에 대한 권리를 단정하는 표현 제거

## 8. 저장소 기반 증거와 측정 계획

### 8.1 2026-08-10 main 스냅샷

| 항목 | 현재 값 | 해석상 주의 |
|---|---:|---|
| 기준 HEAD까지의 main commit | 321 | 1d9a04c까지의 값이며 생산성이나 품질 점수가 아니라 변경 provenance 규모 |
| Git tracked file | 968 | 생성·캐시 파일을 포함하지 않은 추적 파일 수 |
| docs Markdown | 63 | 설계, QA, 운영 문서를 합친 수 |
| test C# source | 67 | 파일명 기준 자동 집계 |
| Test/UnityTest annotation | 618 | 현재 전체 PASS 수가 아니라 소스 내 test annotation 수 |

집계 명령과 날짜를 제출 부록에 남겨 재현 가능하게 한다. 단계별 QA 보고서의 PASS 수와 저장소 전체 test annotation 수를 섞어 쓰지 않는다.

### 8.2 AgentDesk 운영 실측과 남은 계측

`2026-08-10`에 AgentDesk API에서 `[LAST SHIFT]` 카드와 연결된 디스패치를 읽기 전용으로 집계했다. 재현 절차와 해석 한계는 `docs/nan2026-ai-workflow-metrics-evidence.md` 및 `docs/tools/measure_nan2026_ai_workflow.ps1`에 고정했다.

| 지표 | 실측 결과 | 해석상 주의 |
|---|---:|---|
| 카드 lead time | 완료 89건 중앙값 19.2분, 25~75% 12.1~30.2분 | 생성부터 완료까지의 wall-clock이며 장기 체류 outlier 때문에 평균보다 중앙값을 우선 |
| 카드 작업 구간 | 88건 중앙값 17.8분, 25~75% 11.4~26.2분 | 시작부터 완료까지이며 모델 active, tool 대기, queue 대기를 분리하지 못함 |
| 구현 디스패치 구간 | 완료 93건 중앙값 16.9분, 25~75% 10.5~24.4분 | 구현 요청 생성부터 완료까지의 wall-clock |
| 검증 관문 | 완료 phase gate 55건 | 통과 건수이며 시간 절감률은 아님 |
| 동일 카드 재실행 프록시 | 완료 카드 10/89건, 11.2% | 완료·실패·취소를 합친 구현 디스패치 시도가 2회 이상인 카드 비율이며 정식 QA 반려율은 아님 |
| 인간 개입 시간 | 산정 불가 | 승인 사건은 남지만 플레이 검토·아트 승인·방향 수정의 소요시간 필드가 없음 |
| 모델·API 비용 | 산정 불가 | provider 사용량이 카드에 직접 연결되지 않고 실제 청구 비용 필드가 없음 |
| 검증 자동화 효과 | baseline 필요 | 동일 범위의 반복 수동 검증 시간 표본이 필요 |
| 산출물 채택률 | 정의 확정 필요 | AI 제안 단위와 수락·수정·기각 기준을 먼저 고정해야 함 |

현재 데이터는 AI가 작업을 구조화하고 완료까지 추적했다는 사실을 보여 주지만, AI가 인간 대비 몇 퍼센트 시간을 줄였다는 주장은 증명하지 않는다. 측정되지 않은 값은 제출 문구에서 추정하지 않는다.

## 9. 실패 사례와 개선

AI 개발 시스템의 신뢰성은 성공 사례뿐 아니라 실패를 어떻게 관측하고 수정했는지에서 나온다.

| 관찰된 문제 | 원인 | 적용한 대응 | 다음 개선 |
|---|---|---|---|
| game-tech 작업이 예상보다 오래 걸림 | 같은 Unity 프로젝트의 두 카드를 한 역할에 동시 배정하고 fresh worktree 두 곳에서 cold import·compile 수행 | 카드와 세션이 실제 active인지 확인하고 로그 기반으로 진행을 분리 | Unity project affinity와 shared-resource-aware scheduler 도입 |
| 한 카드가 38개 실패와 전체 회귀를 함께 처리 | 수정 범위와 검증 범위가 너무 큰 카드 | 기능 수정, 대상 테스트, 전체 회귀를 단계로 분리 | 실패 cluster별 child card와 검증 budget 설정 |
| screenshot 중심 세션에서 문맥 압축이 반복됨 | 고용량 이미지가 대화 문맥을 빠르게 차지 | screenshot을 최소화하고 로그·파일·수치 요약을 우선 | 이미지 budget과 artifact link 기반 검토 |
| 장기기억이 오래된 상태일 수 있음 | 기억은 특정 시점의 사실을 저장 | recall 뒤 현재 코드·실행 상태와 대조 | 기억에 기준 commit과 검증 시각 추가 |
| 자동 테스트 PASS와 플레이 감각이 다름 | 수치 검증은 재미와 feel을 직접 판단하지 못함 | 실제 Editor 플레이를 인간 승인 관문으로 유지 | 정성 평가 양식과 playtest 기록 연결 |
| 생성형 아트의 provenance가 불명확할 수 있음 | 도구명, 모델 버전, 입력·라이선스 기록이 산출물과 분리 | 제출 전 asset ledger에서 역추적 | 생성 시 자동 metadata sidecar 저장 |

운영 기록 수치는 제출 전 AgentDesk export와 대조한다. 이 표의 목적은 문제를 숨기는 것이 아니라 시스템 개선이 가능한 관측값으로 바꾸는 것이다.

## 10. 품질, 보안, 권리와 인간 책임

### 10.1 품질

- AI의 완료 선언보다 코드, 빌드, 테스트, raw evidence를 우선한다.
- 구현 역할과 QA 역할을 분리한다.
- 정량 검증과 실제 플레이 검토를 모두 통과해야 통합한다.
- 회귀가 큰 변경은 대상 테스트와 전체 회귀를 구분해 실행한다.

### 10.2 보안과 개인정보

- 비밀값과 개인식별정보를 프롬프트, 기억, 저장소 문서에 남기지 않는다.
- 외부 서비스 연결 정보는 환경변수와 로컬 설정으로 분리한다.
- destructive action, 배포와 외부 공개는 인간 승인을 받는다.

### 10.3 저작권과 라이선스

- AI 생성·변형 자산, 외부 에셋, 자체 제작 자산을 asset ledger로 구분한다.
- 원본 입력, 사용 도구와 모델, 생성일, 수정자, 라이선스, 프로젝트 내 경로를 기록한다.
- 출처와 권리가 확인되지 않은 산출물은 최종 제출물에서 제외한다.
- AI 도구 사용 사실과 적용 범위를 신청서에 공개한다.

### 10.4 NAN 2026 제출 경계

[NAN 2026 공식 사이트](https://nan2026.nhn.com/)와 [이용약관](https://nan2026.nhn.com/terms)을 최종 제출 직전에 다시 확인한다. 특히 기존 DoodleUp IP와 본선 신규 결과물의 경계, 수상작 홍보 이용, 우선 협상 조건, 제3자 권리 보증 범위를 팀이 명시적으로 검토한다. 이 문서는 법률 자문을 대신하지 않는다.

## 11. Game × AI 관점의 차별점

DoodleUp의 차별점은 AI 캐릭터 하나나 생성 이미지 한 장이 아니다.

1. AI를 기획·기술·아트·QA 역할로 나누고 각 역할에 다른 완료 기준을 부여했다.
2. AnchorMind/Memento가 프로젝트의 결정과 실패를 세션 사이에 유지한다.
3. 자연어 결과를 Git diff, Unity build, test, raw data와 연결한다.
4. AI가 만든 것을 다른 AI 역할이 검증하고 인간이 실제 플레이로 승인한다.
5. 실패와 지연도 운영 데이터로 남겨 scheduling과 context policy를 개선한다.

즉, DoodleUp은 게임 안에 AI 기능을 넣는 것과 별개로 게임을 만드는 조직 자체를 AI-native system으로 설계했다.

## 12. 제출 전 체크리스트

### 필수 사실 검증

- [ ] 공식 신청서 문항과 글자 수에 맞춰 요약을 재편집한다.
- [ ] 제출 시점의 NAN 2026 일정, 참가 조건과 약관을 다시 확인한다.
- [ ] AgentDesk 카드·세션 export로 역할과 작업 흐름을 증빙한다.
- [ ] 문서에 적은 commit hash와 main 포함 여부를 다시 확인한다.
- [ ] 최신 Unity 전체 EditMode·PlayMode 결과를 별도 snapshot으로 남긴다.
- [ ] 문서의 단계별 PASS 수가 당시 QA 결과임을 명확히 표시한다.

### AI 도구 공개

- [ ] 실제 사용한 제공자, 제품, 모델과 사용 기간을 확정한다.
- [ ] 역할별로 어떤 입력을 주고 무엇을 채택했는지 대표 사례를 고른다.
- [ ] 모델이 생성한 부분과 사람이 직접 결정·수정한 부분을 구분한다.
- [ ] 사용량·비용·시간 절감은 원본 기록이 있는 값만 기재한다.

### 에셋 권리

- [ ] Tripo·SF3D 관련 산출물의 도구 버전과 라이선스를 확인한다.
- [ ] 캐릭터, animation, texture, sound별 asset ledger를 완성한다.
- [ ] 외부 원본 이미지나 상표가 포함된 concept을 제거하거나 허가를 확인한다.
- [ ] 본선 결과물과 기존 DoodleUp 자산의 경계를 README와 commit tag로 남긴다.

### 발표 증거

- [ ] 인간 요청 → 카드 → 에이전트 작업 → commit → test → 플레이 승인 한 건을 end-to-end로 캡처한다.
- [ ] DU-02 raw evidence와 독립 QA 사례를 한 장으로 요약한다.
- [ ] 중앙 광장 51,200-point 검산을 시각 자료로 만든다.
- [ ] Lime Alien Blender 원본 → FBX → Animator → network play 흐름을 짧은 영상으로 만든다.
- [ ] game-tech 지연 사례와 개선안을 honest retrospective로 정리한다.

## 부록 A. 저장소 증거 인덱스

| 주제 | 증거 |
|---|---|
| 프로젝트 Unity 버전 | [ProjectSettings/ProjectVersion.txt](../ProjectSettings/ProjectVersion.txt) |
| DU-02 최종 QA | [docs/qa/reports/2026-07-31-doodleup-du02-acceptance-review.md](qa/reports/2026-07-31-doodleup-du02-acceptance-review.md) |
| DU-03B/C 검증 | [docs/qa/du-03bc-verification.md](qa/du-03bc-verification.md) |
| 중앙 광장 설계 | [docs/central-plaza-hub-layout-v1.md](central-plaza-hub-layout-v1.md) |
| 중앙 광장 검산 도구 | [docs/tools/plaza_hub_check.py](tools/plaza_hub_check.py) |
| 라임 외계인 리그 | [docs/art/last-shift-lime-alien-rig-v1.md](art/last-shift-lime-alien-rig-v1.md) |
| Unity Animator 자동 구성 | [Assets/DoodleUp/Editor/LimeAlienAnimatorSetup.cs](../Assets/DoodleUp/Editor/LimeAlienAnimatorSetup.cs) |
| Network scene verifier | [Assets/DoodleUp/Editor/LastShiftNetworkSceneVerifier.cs](../Assets/DoodleUp/Editor/LastShiftNetworkSceneVerifier.cs) |
| Room discovery | [Assets/DoodleUp/Scripts/Runtime/LastShiftRoomDiscovery.cs](../Assets/DoodleUp/Scripts/Runtime/LastShiftRoomDiscovery.cs) |
| Scene dressing 검증 | [docs/scene-dressing-authoring.md](scene-dressing-authoring.md) |

## 부록 B. 발표용 한 문장

DoodleUp은 AI에게 게임을 맡긴 프로젝트가 아니라, 인간 디렉터의 의도를 기억하고 전문 역할로 구현하며 Unity 증거로 검증하는 AI 개발 조직을 만든 프로젝트다.
