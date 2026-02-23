# 🟡 대기중

[Codex용 작업 명세서]

Script Name: InteractionManager

Script Type: MonoBehaviour / Manager

Serialized Fields:
float interactionDistance = 3f; // 상호작용 유효 사거리
LayerMask interactableLayer; // 상호작용 가능 레이어
float dragThreshold = 0.5f; // F키 드래그 판정 임계치

Data Assets (ScriptableObject / Table):
ObjectPropertySet; // 오브젝트가 가진 성질 및 들기 가능 여부(holdenable) 데이터

Required Manager Scripts:
InputManager, UIManager, TransferSystem

Logic Overview:
초기화(Awake/Start): 
- 메인 카메라 참조 캐싱.
- 크로스헤어 타겟 감지 로직 초기화.

업데이트(Update/코루틴): 
- Raycast 매 프레임(또는 일정 주기) 중앙에서 발사하여 타겟 감지.
- 타겟 근처 시 UI 피드백 제공 (예: "E - 문 열기", "E - 물건 들기").

- 기본 상호작용 (E):
  - 타겟이 오브젝트이고 `holdenable == true`이면: '물건 들기' 수행.
  - 타겟이 특정 장치(문, 사다리 등)이면: 상황에 맞는 함수 호출 (OpenDoor, ClimbLadder).

- 추가 상호작용 (F + Mouse Drag):
  - 물건을 들고 있는 상태에서 F키를 누르면 드래그 추적 시작.
  - 왼쪽 드래그: '던지기(Throw)' 수행. 물체의 Velocity 부여.
  - 오른쪽 드래그: '흔들기(Shake)' 수행.
  - 위/아래 드래그: 더미 데이터로 구현 (향후 확장 대비).

관련 기획서:
기획서/1.시스템/조작 및 상호작용.md

주의/최적화 포인트:
- 물건을 들고 있을 때 플레이어의 손(IK) 위치와 오브젝트 동기화.
- 던지기 시 물리 엔진(Rigidbody) 충돌 판정 주의.
