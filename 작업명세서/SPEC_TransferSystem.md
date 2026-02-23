# 🟡 대기중

[Codex용 작업 명세서]

Script Name: TransferSystem

Script Type: MonoBehaviour / Manager

Serialized Fields:
float defaultExtractTime = 1.5f; // 기본 성질 추출 시간 (채널링)
float defaultInjectTime = 1.0f; // 기본 주입 시간
float maxPropertyHoldTime = 600f; // 손에 저장 가능한 최대 시간

Data Assets (ScriptableObject / Table):
PropertyDatabase propertyDatabase; // 성질 데이터베이스

Required Manager Scripts:
InputManager, InteractionManager, VFXManager, UIManager

Logic Overview:
초기화(Awake/Start): 
- 양손 상태(HandState) 초기화.
- 타이머 리셋.

이벤트 처리:
- 좌클릭 (왼손 - 정적 성질):
  - 왼손에 저장된 성질이 없으면: 타겟의 '정적 성질' 추출(Extract) 시도.
  - 왼손에 저장된 성질이 있으면: 타겟에 '정적 성질' 주입(Inject) 시도.
- 우클릭 (오른손 - 동적 성질):
  - 오른손에 저장된 성질이 없으면: 타겟의 '동적 성질' 추출 시도.
  - 오른손에 저장된 성질이 있으면: 타겟에 '동적 성질' 주입 시도.

상세 로직:
- 추출(Extract): 
  - 추출 시간 동안 채널링 UI/VFX 출력. 중간에 끊기면 실패.
  - 성공 시 HandState에 성질 저장 및 아이콘 표시.
- 주입(Inject):
  - 주입 시간 동안 채널링. 
  - 성공 시 대상 오브젝트의 ObjectPropertySet 성질 교체 및 시각적 변화 적용.
  - 지속시간(Duration) 종료 후 원래 성질로 복귀하는 타이머 로직 포함.

관련 기획서:
기획서/1.시스템/조작 및 상호작용.md
기획서/1.시스템/성질 시스템.md

주의/최적화 포인트:
- "주입된 성질"은 다시 추출할 수 없도록 예외 처리 필수.
- 문/벽/바닥 등 파괴 불가 오브젝트에 대한 성질 주입 제한 확인.
