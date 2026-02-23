# 🟡 대기중

[Codex용 작업 명세서]

Script Name: InputManager

Script Type: MonoBehaviour / Manager / Singleton

Serialized Fields:
float walkSpeed = 5f; // 기본 걷기 속도
float runSpeedMultiplier = 1.5f; // 달리기 속도 배율
float crouchSpeedMultiplier = 0.5f; // 앉기 속도 배율
float proneSpeedMultiplier = 0.3f; // 엎드리기 속도 배율
float jumpForce = 5f; // 기본 점프 힘
float mouseSensitivity = 2f; // 마우스 감도

Data Assets (ScriptableObject / Table):
// Unity Input System 사용 권장 (.inputactions 에셋 연동)

Required Manager Scripts:
GameManager, LevelFlowManager

Logic Overview:
초기화(Awake/Start): 
- Singleton 인스턴스 설정.
- 플레이어 캐릭터의 초기 상태(Standing) 세팅.
- 마우스 커서 잠금(CursorLockMode.Locked).

업데이트(Update/코루틴): 
- WASD: 1인칭 이동 처리. 현재 상태(런닝, 앉기, 엎드리기)에 따른 속도 변환 적용.
- Mouse Move: 1인칭 카메라 시야 회전. 화면 중앙에 십자(+) 크로스헤어 UI 출력 필요.
- LShift: 달리기 토글/유지. 앉기/엎드리기 상태에서는 무시.
- C: 앉기(Crouch). 이동 자세 낮춤. 달리기 불가.
- LCtrl: 엎드리기(Prone). 가장 낮은 자세. 달리기/점프 불가.
- Space Bar: 점프. 
  - 앉아있는 도중 점프 시 앉기 해제 후 서있는 상태로 전환.
  - 달리면서 점프 시 관성에 의한 비거리 보너스 적용.
  - 엎드려 있을 때는 점프 불가.
- E / F: 기본/추가 상호작용 키 이벤트를 InteractionManager로 전달.
- 좌/우클릭: 추출/주입 이벤트를 TransferSystem으로 전달.

관련 기획서:
기획서/1.시스템/조작 및 상호작용.md

주의/최적화 포인트:
- Input Action 콜백 방식을 사용하여 입력 처리 부하 최소화.
- 상태 변화(전환) 시 애니메이션 파라미터 연동 구조 설계.
