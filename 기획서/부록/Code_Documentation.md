## Code Documentation

기준 기획서 참조: `기획서/조작 및 상호작용.md`

| 파일명 | 수정일 | Serialized Fields | Methods |
|---|---|---|---|
| `Assets/Scripts/Core/InputManager.cs` | 2026-02-24 | 이동/점프/감도 수치, InputAction 이름, 선택적 `gameManager`, `levelFlowManager` 참조, 1인칭 데모용 `CharacterController`/Camera/자세 높이 필드 | `TrySetPosture`, `TryJump`, 입력 액션 초기화/조회, 자세 토글, 속도 재계산, 1인칭 시야/이동/자세 시각 반영 |
| `Assets/Scripts/Interaction/InteractionManager.cs` | 2026-02-24 | `interactionDistance`, `interactableLayer`, `dragThreshold`, Raycast 주기, Hold/Throw 튜닝, 선택적 `inputManager`, `uiManager`, `transferSystem` 참조 | `TryHandlePrimaryInteraction`, `TryGetTransferTarget`, `HandleSecondaryInteractHeld`, `HandleSecondaryInteractDragDelta`, Raycast 타겟 갱신, 프롬프트 생성, 들기/놓기/던지기 |
| `Assets/Scripts/TransferSystem/TransferSystem.cs` | 2026-02-24 | 추출/주입 시간, 손 보관 최대시간, 기본 주입 지속시간, 채널 취소 허용 오차, 선택적 `propertyDatabase`, `inputManager`, `interactionManager`, `vfxManager`, `uiManager` 참조 | `HandleTransferClick`, `CancelCurrentChanneling`, 채널링 코루틴, 추출/주입 완료 처리, 주입 복구 타이머, 메타데이터(reflection) 읽기/쓰기, 손 상태 만료 처리 |
| `Assets/Scripts/Debug/ManagerDebugCheckLogger.cs` | 2026-02-24 | 대상 매니저 참조, 로그 on/off 플래그 | 각 매니저 이벤트 구독/해제, 입력/상호작용/전이 이벤트 로그 출력 |
| `Assets/Scripts/Debug/ManagerSpecRuntimeHUD.cs` | 2026-02-24 | 매니저 참조, HUD 표시 옵션/폰트/색상 | 크로스헤어, 프롬프트, 입력/상호작용/전이 상태 OnGUI 표시, 좌클릭(정적)/우클릭(동적) 추출·주입 안내 및 클릭 알림 표시 |
| `Assets/Scripts/Debug/DebugInteractableCheckTarget.cs` | 2026-02-24 | `holdenable`, 프롬프트, 주입 가능/파괴 가능/주입 상태/시각 토큰, 체크용 색상 필드 | 체크용 상호작용/드래그/전이 콜백, 프롬프트 제공, 추출/주입 시 색상 변경 표시 |
| `Assets/Scripts/TransferSystem/PropertyDatabaseCheckPlaceholder.cs` | 2026-02-24 | 체크용 안내 문자열 | 체크용 `ScriptableObject` placeholder (실제 DB 구현 전 임시 할당) |
| `Assets/Scripts/Editor/ManagerCheckSceneBuilder.cs` | 2026-02-24 | (Editor 유틸) 경로 상수, 자동 세팅 값 | 체크용 씬 자동 생성, 플레이어(이동/시야) + 추출/주입 테스트 오브젝트 2개 + HUD/placeholder 자동 배치 및 참조 연결 |

### 메모

- `PropertyDatabase`, `ObjectPropertySet`, `UIManager`, `VFXManager` 등 외부 의존 타입은 1차 범위에서 미구현이므로 reflection/이벤트 훅 기반으로 안전 실패 처리하도록 구현함.
- 추후 데이터 시스템 구현 시 `TransferSystem`의 reflection 메타데이터 접근 부분을 강타입 API 호출로 교체 필요.
- 체크용으로 `ManagerDebugCheckLogger`, `DebugInteractableCheckTarget`, `PropertyDatabaseCheckPlaceholder`를 추가해 씬에서 즉시 입력/상호작용/전이 흐름 검증 가능.
- `ManagerSpecRuntimeHUD`를 추가해 크로스헤어/프롬프트/손 상태 등 명세 핵심 값이 화면에서 보이도록 함.
- 추가로 `Tools/Undefined/Create Manager Check Scene` 메뉴를 통해 테스트 씬을 자동 생성할 수 있게 함.
- 자동 생성 씬에서 `앉기/엎드리기` 시 시야 높이 차이가 크게 보이도록 `InputManager` 높이 값을 설정하고, 추출/주입 테스트 큐브 2개를 생성해 주입 시 색상 변화로 적용 여부를 확인할 수 있게 함.
- 자동 생성 씬에서 `종이박스(가벼움 추출 소스)`와 `무거운 구(기본 들기 불가, 가벼움 주입 시 들기 가능)`를 생성해, 좌클릭(정적) 추출/주입 프로세스를 눈으로 검증할 수 있게 함.
