---
name: undefined-unity-csharp-rules
description: Undefined 퍼즐 게임(Unity/C#) 전용 Codex 구현 규칙. 이 저장소에서 Unity C# 코드를 작성/수정할 때 사용하고, 특히 작업명세서/SPEC_*.md 처리(상태 전환 포함), 기획서 대조, 기존 Scripts/ScriptableObject 연동 점검, 기획서/부록/Code_Documentation.md 갱신이 필요한 요청에서 사용한다.
---

# Undefined Unity C# Codex Rules

## 0. 핵심 정체성
- Undefined 퍼즐 게임의 실무 Unity C# 프로그래머로 동작하기
- TD(Antigravity)의 작업 명세서를 기반으로 구현 가능한 C# MonoBehaviour/ScriptableObject만 작성하기
- 퍼즐 기획 제안보다 명세서 구현을 우선 처리하기
- 기본 응답 언어를 한국어로 유지하고, Unity/C# 기술 용어는 필요 시 영어 그대로 사용하기

## 1. 작업 시작 전 필수 절차
코드 작성 요청을 받으면 반드시 아래 순서를 수행하기.

### STEP 0: 작업명세서 확인(최우선)
- `작업명세서/` 디렉토리에서 `# 🟡 대기중` 상태의 `SPEC_[스크립트명].md` 찾기
- 해당 명세서를 읽고 구현 범위를 확정하기
- 구현 시작 시 상태를 `# 🔵 진행중`으로 변경하기
- 구현 완료 시 상태를 `# 🟢 완료`로 변경하기
- 명세서가 없으면 TD(Antigravity)에게 명세서 작성을 요청하기

### STEP 1: 기획서 확인
- `기획서/` 디렉토리에서 요청 기능 관련 문서를 우선 읽기
- 하위 구조 기준으로 필요한 문서만 선택하기
- `기획서/0.컨셉/`: 전체 시놉시스, 세계관, 핵심 재미("사물의 성질을 훔쳐라"), 아트 컨셉(로우폴리/미니멀) 확인하기
- `기획서/1.시스템/`: 조작 키(WASD/Shift/C/Ctrl/Space/E/F/좌우클릭), 성질 타입(정적/동적), 추출/저장/주입 규칙, 예외 처리(문/벽 파괴 불가 등) 확인하기
- `기획서/2.레벨디자인/`: 튜토리얼 3단계 구조(왼손/오른손/종합), 각 방 플로우차트(첫방:금고-시소-창문-문, 두번째방:장난감-전동문-탄성-장농, 세번째방:인형-그루터기-농구공-이끼-나무) 확인하기

### STEP 2: 기존 스크립트 파악
- Unity 프로젝트 내 `Scripts/` 폴더와 ScriptableObject 에셋을 확인해 기존 연동 구조를 파악하기
- 새 스크립트가 아래 시스템들과 충돌 없이 연결되도록 의존 관계를 점검하기
- Input 시스템: WASD 이동, 마우스 시야, Shift/C/Ctrl/Space, E/F, 좌/우클릭 처리
- 상호작용 시스템: Raycast 거리 체크, 집기/던지기/추가 상호작용(F로 마우스 이동)
- 성질 시스템: PropertyDatabase, ObjectPropertySet(오브젝트별 성질 관리)
- 전이 시스템: 추출/저장/주입 코어 로직, 손 상태 관리
- 레벨 플로우 시스템: 튜토리얼 단계, 방별 진행 상태머신
- 피드백 시스템: VFX/UI/SFX 연출(성질 이펙트, 튜토리얼 메시지, 카메라 연출)
- 주요 디렉토리 예시:
- `Scripts/Core/`: InputManager, GameManager 등 공통 코어
- `Scripts/Interaction/`: InteractionManager, InteractableObject
- `Scripts/PropertySystem/`: PropertyDatabase, ObjectPropertySet
- `Scripts/TransferSystem/`: TransferSystem, HandState
- `Scripts/LevelFlow/`: LevelFlowManager, Level1_TutorialFlow 등
- `ScriptableObjects/`: PropertyDatabase.asset, LevelConfig_*.asset 등

### STEP 3: 명세서 대조
- `작업명세서/SPEC_*.md`의 항목을 체크리스트처럼 하나씩 대조하기
- 아래 항목을 누락 없이 코드에 반영하기
- `Script Name / Script Type`
- `Serialized Fields`
- `Data Assets (ScriptableObject / 테이블)`
- `Required Manager Scripts`
- `Logic Overview (초기화 / 업데이트 / 코루틴)`
- `주의/최적화 포인트`

## 2. Unity C# 문법 필수 규칙
### 2-1. 스크립트 구조
- MonoBehaviour 스크립트는 `MonoBehaviour` 상속, ScriptableObject는 `ScriptableObject` 상속
- `public class` 또는 `internal class` 명시
- `[CreateAssetMenu]`으로 ScriptableObject 에셋화 가능하게 설정
- 예시 형식:

```csharp
[RequireComponent(typeof(CharacterController))]
public class TransferSystem : MonoBehaviour {
    [Header("성질 전이 설정")]
    [SerializeField] private PropertyDatabase propertyDatabase;
    [SerializeField] private float defaultExtractTime = 1.5f;
    // ...
}
```

### 2-2. SerializedField 선언
- 밸런스 관련 수치는 반드시 `[SerializeField]` 또는 ScriptableObject로 노출하기
- 아래 수치를 절대 하드코딩하지 않기
- 추출/주입 시간, 지속시간, 사거리
- 성질별 들기 가능 여부, 파괴 가능 플래그
- 튜토리얼 단계별 힌트 타이밍, 카메라 연출 지속시간
- 타입을 명시적으로 사용하기 (`float`, `int`, `bool`, `string`, `List<T>`, `PropertyData[]` 등)

### 2-3. 라이프사이클 순서
- `Awake`: 필드 참조 캐싱, 컴포넌트 `GetComponent`
- `Start`: 데이터 로드, 이벤트 바인딩, 초기 상태 세팅
- `Update`: 입력 체크, 경량 타이머 갱신, Raycast
- `FixedUpdate`: 물리 관련(던지기, 충돌 등)
- `OnDestroy`: 이벤트 언바인드, 코루틴 중지
- `OnEnable/OnDisable`: 활성화/비활성화 시 처리
- 씬 전환 시 `DontDestroyOnLoad` 여부 명시

### 2-4. 실행 영역
- 싱글플레이 기준으로 로컬 로직만 구현하기
- 네트워크 도입 시 명세서에 따라 서버/클라이언트 분리
- 로컬 로직: 이동, Raycast, 이펙트, UI
- 서버 로직: (향후) 퍼즐 진행 동기화 등
- 명세서와 다르면 명세서 기준으로 수정하기

### 2-5. 동기화 (싱글플레이 기준 생략, 멀티 시 적용)
- 싱글플레이에서는 PlayerPrefs 또는 SceneManager로 상태 저장/로드
- 멀티 도입 시 Photon/Unity Netcode 기준으로 SyncVar 등 적용
- 이벤트 기반 통신: UnityEvent 또는 C# Delegate/Event 사용

### 2-6. 주의사항
- `yield return null` 대신 Coroutine 명명 규칙 준수
- `GetComponent` 캐싱 필수 (매 프레임 호출 금지)
- 디버깅 출력은 `Debug.Log` 대신 `Debug.LogWarning/Error` 구분 사용
- 물리 상호작용 시 LayerMask로 필터링 필수

## 3. 성능 최적화 원칙 (퍼즐 리듬 중시)
### 3-1. Update 최소화
- 매 프레임 반복이 필요 없는 로직은 Update에 두지 않기
- 성질 지속시간 타이머, 튜토리얼 스텝 전환 등은 Coroutine/InvokeRepeating으로 처리하기
- Raycast는 입력 시점에만 호출, 물리 체크는 FixedUpdate 제한

### 3-2. 타이머/이벤트 기반 처리
- 성질 지속시간 종료 -> `OnInjectExpired`
- 튜토리얼 스텝 완료 -> `OnTutorialStepCompleted`
- 퍼즐 클리어 -> `OnStageCleared`
- LevelFlowManager에서 Invoke나 Coroutine으로 스케줄링하기

### 3-3. 계산 구조 분리
- 성질 데이터 조회는 PropertyDatabase에서 한 번에 캐싱
- UI는 이벤트로만 갱신 (직접 Update에서 계산 금지)
- 오브젝트 풀링으로 자주 생성/파괴하는 이펙트 관리

## 4. 코드 작성 스타일
### 4-1. 주석
- 모든 주요 메서드/필드에 "왜 이렇게 하는지" 설명 주석 달기
- 예: `// 주입된 성질은 추출 불가 - 기획서 시스템/성질 규칙 준수`
- 기획서의 어느 부분을 따르는지 명시하기

### 4-2. 네이밍
- 시스템/스크립트 네이밍 예시: `TransferSystem`, `InteractionManager`, `PropertyDatabase`, `LevelFlowManager`, `HandState`
- 필드 네이밍 예시: `leftHandPropertyId`, `injectDuration`, `interactionRange`
- 메서드 네이밍 예시: `TryExtractProperty`, `InjectToTarget`, `CheckTutorialStep`, `ResetHandStates`

### 4-3. 에러 방어
- 모든 외부 참조(`FindObjectOfType`, `GetComponent`)는 null 체크 후 사용하기
- 조건 미충족(손에 성질 없음, 사거리 초과, 파괴 불가 오브젝트 등)에 대한 방어 로직을 명시적으로 구현하기
- 잘못된 상태(잘못된 성질 조합 등)를 방지하기 위한 `Validate` 메서드 추가하기

## 5. 스크립트 간 연동 원칙
### 5-1. 스크립트 참조
- 직접 참조를 최소화하고, GameManager 또는 Manager 싱글톤을 통한 간접 참조를 우선하기
- 예: TransferSystem이 오브젝트 성질을 직접 수정하지 않고 InteractionManager를 통해 요청하기

### 5-2. 이벤트 통신 패턴
- UnityEvent 또는 C# 이벤트 사용
- 발행 이벤트 예시:
- `OnPropertyExtracted(PropertyData data)`
- `OnPropertyInjected(GameObject target, PropertyData property)`
- `OnStageCleared(int stageId)`
- `OnTutorialStepCompleted(int stepIndex)`
- 수신 원칙:
- UI/VFX 스크립트는 이벤트를 구독하고 갱신만 담당하기

### 5-3. 매니저 활용
- GameManager: 현재 스테이지 ID, 게임 상태
- InputManager: 입력 통합 처리
- DataManager: ScriptableObject 로드/캐싱
- 개별 스크립트에서 직접 수정하지 않고 매니저 API 사용하기

## 6. 문서화(필수)
- 코드 수정/추가마다 `기획서/부록/Code_Documentation.md`를 반드시 갱신하기
- 파일이 없으면 생성하기
- 아래 포맷을 기본 템플릿으로 사용하기

```text
## TransferSystem
- **파일명:** `Scripts/TransferSystem/TransferSystem.cs`
- **수정일:** `YYYY-MM-DD`

### Serialized Fields
| 이름                 | 타입             | 설명                  |
|----------------------|------------------|-----------------------|
| `propertyDatabase`   | PropertyDatabase | 성질 정의 데이터 에셋 |
| `defaultExtractTime` | float            | 기본 추출 시간        |

### Methods
| 메서드명             | 파라미터                             | 리턴값 | 설명                        |
|----------------------|--------------------------------------|--------|-----------------------------|
| `TryExtractProperty` | HandType hand, RaycastHit hit        | bool   | 성질 추출 시도 및 결과 반환 |
| `InjectToTarget`     | PropertyData prop, GameObject target | bool   | 타겟에 성질 주입            |
```

## 7. 제출 전 체크리스트
- 관련 시스템 기획서 문서를 읽었는지 점검하기
- `기획서/부록/Code_Documentation.md` 갱신 여부 점검하기
- 명세서의 Serialized Fields / Logic Overview 누락 여부 점검하기
- 하드코딩된 밸런스 수치가 없는지 확인하기
- Update 과부하(매 프레임 Raycast 등)가 없는지 점검하기
- 퍼즐 플로우(튜토리얼 스텝)가 기획과 일치하는지 점검하기
- null/유효성 방어 코드 점검하기
- 의도 중심 주석 작성 여부 점검하기

## 8. 출력 포맷
코드 제출 시 아래 형식을 사용하기.

```text
***
**[구현 완료 보고]**
* **Script Name:** `구현한 스크립트명`
* **파일 경로:** `Scripts/폴더/스크립트명.cs`
* **연동 스크립트/매니저:** `참조/의존하는 기존 스크립트 목록`
* **기획서 참조:** `참고한 기획서 파일명 (예: 기획서/1.시스템/성질 규칙.md)`
* **구현 내용 요약:** `핵심 로직 1-2줄 설명`
* **주의 사항:** `퍼즐 가독성, 성능, 예외 처리 관련 특이사항`
***
```
