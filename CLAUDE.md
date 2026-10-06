# MultiMystery — 팀 프로젝트 메모리

이 파일은 Claude Code가 이 저장소에서 작업할 때마다 자동으로 읽는 공용 컨텍스트입니다.
git에 커밋되어 팀원 전원이 같은 내용을 공유합니다. 개인적인 메모는 `CLAUDE.local.md`(gitignore 처리됨)에 남겨주세요.

## 게임 개요
- 장르: 미스터리·추리·퍼즐, 2인 협동
- 톤: 갑작스러운 점프스케어(갑툭튀) 없이, 조명·소리·사물의 이상 상태로 긴장감을 조성
- 핵심 기믹(논의 중): 청소·정리하면서 단서 발견 + 카메라로 사물의 전/후 상태 비교
- 2인 협동의 핵심: 두 플레이어가 서로 다른 정보/시야를 보게 해서 실제 대화를 유도 (정보 비대칭)
- 최신 기획 결정사항은 이 저장소가 아니라 Notion **"📋 팀 프로젝트 진행 기록"** 페이지의 날짜별 토글에 있습니다. 기획이 코드 작업에 영향을 주면 그 페이지를 먼저 확인하세요.

## 기술 스택 / 현재 상태
- Unity **6000.6.0f1**, Universal Render Pipeline (URP) 17.6.0
- Input System(신규) 사용, 기본 InputSystem_Actions 자산 포함
- 현재 패키지: AI Navigation, Timeline, Visual Scripting, UGUI, Test Framework 등 기본 템플릿 구성 그대로
- **네트워킹 확정(2026-10-02)**: 중계 호스트(Relay) 방식, `com.unity.netcode.gameobjects@2.13.3` + `com.unity.services.multiplayer@2.3.3`(Sessions API) 조합. Sessions API가 Relay 할당/참가 코드/NGO 연결을 한 번에 처리하며, `NetworkManager.StartHost/StartClient`는 직접 호출하지 않음. (단, 이번 세팅은 KSM 개인 작업 공간에만 반영됨 — 아래 "네트워킹 세팅 현황" 참고. 설계 근거는 Artifact "릴레이 호스트 아키텍처" 참고)
- `Assets/Scripts` 등 폴더는 만들어졌지만 실제 게임플레이 코드는 아직 없는 초기 상태(3D 코어 템플릿 잔재인 `TutorialInfo`, `Readme.asset` 포함, 정리 필요 시 팀 논의 후 삭제)

## 팀 구성 (6명)
- PM: 이채원 — 기획 방향, 일정/성과, 제작 범위, BM
- 영상·미디어: 고우진, 이현지 — 카메라 구도, 사진 전/후 연출
- 개발: 고수민, 김진호, 이건호 — 오브젝트/촬영/사건 상태·판정 시스템, Unity 통합 빌드 (Claude Code로 바이브 코딩)

## 협업 시스템
- claude.ai에 **"팀 프로젝트"** Team Project가 있고, 게임 기획 맥락과 이 CLAUDE.md의 상위 내용이 그 Project Instructions에도 반영되어 있습니다.
- Notion "팀 프로젝트 진행 기록" 페이지가 매일 밤 11:30(KST)에 예약 작업으로 자동 정리됩니다(원본 메모 → 요약).
- Unity 관련 작업을 하다가 기획이 바뀌면, 그 이유를 팀원이 이해할 수 있게 설명하고 Notion에도 반영되도록 안내할 것.

## Unity CLI / MCP 연결
- 이 저장소는 `.mcp.json`에 Unity 공식 CLI가 제공하는 MCP 서버(`unity mcp`)를 프로젝트 범위로 등록해뒀습니다. 열려 있는 Unity 에디터가 있으면 Claude Code가 씬/게임오브젝트/에셋을 직접 조작할 수 있습니다.
- 팀원 각자 로컬에 **Unity CLI(베타)** 가 설치되어 있어야 이 MCP 서버가 동작합니다:
  - Windows(PowerShell): `$env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex`
  - macOS/Linux: `curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash`
  - 설치 후 `unity --version`으로 확인
- Unity 에디터를 씬 작업 전에 직접 편집하지 말고, 먼저 `unity status` / 연결된 MCP로 살아있는 에디터가 있는지 확인 후 그걸 통해 조작할 것 (씬 파일 수동 편집은 최후의 수단).

## 코딩 컨벤션
아직 팀 전체가 확정한 세부 컨벤션은 없습니다. 코드를 작성할 때는:
- Unity 표준 C# 네이밍(PascalCase 메서드/클래스, camelCase 필드)을 기본값으로 사용
- 네이밍 규칙에 대한 추가 팀 결정이 나면 이 섹션에 추가해주세요.

## Assets 폴더 구조
`Images`, `Scripts`, `Sounds`, `VARCO 3D` 하위에 팀원 이니셜별 작업 폴더가 있습니다(개인 작업 공간 분리용):

- `KSM` — 고수민
- `KJH` — 김진호
- `LGH` — 이건호
- `LCW` — 이채원
- `KWJ` — 고우진
- `LHJ` — 이현지

새 에셋/스크립트를 만들 때는 공용 코드가 아닌 이상 해당 담당자의 이니셜 폴더 아래에 배치하세요. 공용(팀 전체가 쓰는) 스크립트나 에셋을 어디에 둘지는 아직 미정이니, 애매하면 먼저 질문할 것.

## 네트워킹 세팅 현황
- 패키지: `com.unity.netcode.gameobjects@2.13.3`, `com.unity.services.multiplayer@2.3.3` 설치 완료 (프로젝트 전역 적용)
- **현재는 `KSM` 씬(`Assets/Scenes/KSM.unity`)에만 세팅이 있음**: `SessionManager`(`CoopSessionManager`), `NetworkManager`(`NetworkManager`+`UnityTransport`, 연결 완료), `Ground`(Plane, 바닥), UI `Canvas`(호스트/참가 버튼 + `EventSystem`의 `InputSystemUIInputModule`)
- 스크립트: `Assets/Scripts/KSM/Networking/CoopSessionManager.cs`, `Assets/Scripts/KSM/Player/PlayerMovement.cs`, `Assets/Scripts/KSM/UI/MultiplayerMenuUI.cs` (네임스페이스 `CoopDemo`)
- 프리팹: `Assets/Prefabs/KSM/Player.prefab` (`NetworkObject` + `NetworkTransform(AuthorityMode=Owner)` + `PlayerMovement`), `NetworkManager.NetworkConfig.PlayerPrefab`과 `DefaultNetworkPrefabs.asset`에 등록됨
- **`PlayerMovement.OnNetworkSpawn`의 스폰 위치 적용 주의사항**: `transform.position` 직접 대입은 `NetworkTransform`이 되돌리고, `NetworkTransform.Teleport()`만으로도 `CharacterController`가 다음 `Move()`에서 캐시된 위치로 되돌림 → `m_Controller.enabled = false` → `Teleport()` → `enabled = true` 순서로 처리해야 함(이미 반영됨)
- 에디터 Play 모드에서 `CoopSessionManager.HostGame()`을 직접 호출해 실제 Relay 세션 생성/플레이어 스폰(정상 위치, 바닥에 착지)까지 재확인함 — 콘솔 에러 없음
- UI는 좌측 하단 패널(어두운 반투명 배경)에 제목/호스트·참가 버튼/참가 코드 입력란/상태 텍스트를 그룹화한 형태로 재구성, 실제 플레이 테스트에서 사용자가 최종 확인함(2026-10-02, "특이사항 없음")
- 테스트 빌드: `C:\Users\mbc\Desktop\MultiTestBuild\MultiMystery.exe` (StandaloneWindows64, KSM 씬만 포함, 2026-10-02 빌드, 0 에러)
- 참고 자료: claude.ai Artifact "릴레이 호스트 아키텍처"(연결 흐름 시퀀스 다이어그램, 씬 구성요소 책임 분리, 현재 한계와 다음 단계 정리), `C:\Users\mbc\Desktop\TestMulti\MULTIPLAYER_SETUP.md`(패키지 버전 함정/Unity MCP 자동화 트랩)
- **위 항목은 KSM 씬(단독 프로토타입) 기준 기록입니다. 실제 팀 통합은 `Develop` 씬에서 진행 중이며, 아래 섹션을 참고하세요.**

## Develop 씬 통합 현황 (2026-10-06)
- `Develop` 씬이 팀 전체 통합 씬입니다. 호스트/참가 시 실제로 스폰되는 플레이어는 `Assets/Prefabs/KSM/Player.prefab`(KSM 단독 프로토타입용, WASD+고정 3인칭)이 **아니라 `Assets/Prefabs/KSM/FPSPlayer.prefab`**입니다 — `NetworkManager.NetworkConfig.PlayerPrefab`이 이걸 가리킴. `CharacterController` + `NetworkObject` + `NetworkTransform(Owner)` + `NetworkFirstPersonController`(1인칭 WASD+마우스 시점, IsOwner에서만 입력/카메라 활성화) 구성.
- KJH의 상호작용 시스템(`InteractionController`, `ObjectEchoController` 등, `Assets/Scripts/KJH/Photo`·`ObjectEcho`)을 FPSPlayer에 붙여서 네트워크 멀티플레이에서도 "바라보면 안내 문구 뜨고 E로 상호작용" 이 되도록 연결함:
  - `Assets/Scripts/KSM/Player/NetworkInteractionBridge.cs`(새 스크립트) — `OnNetworkSpawn`에서 `IsOwner`가 아니면 `InteractionController`/`ObjectEchoController`를 꺼서 상대방 화면에 중복 레이캐스트/안내 문구가 뜨지 않게 하고, 소유자면 자기 카메라와 씬의 `InteractionPromptUI`를 찾아 주입함 (프리팹 단계에서는 씬 참조를 미리 연결할 수 없어서 런타임 주입이 필요).
  - 이를 위해 KJH의 `InteractionController.cs`에 `ConfigureForOwner(Camera, InteractionPromptUI)`, `ObjectEchoController.cs`에 `SetViewCamera(Camera)` 공개 메서드를 추가함 (기존 동작 변경 없이 런타임 주입용 setter만 추가).
  - Canvas에 `InteractionPrompt`(CanvasGroup + Text, 화면 하단 중앙) 오브젝트를 추가해 `InteractionPromptUI`로 연결함.
  - FPSPlayer에 `SoundCue` 자식을 추가해 `ObjectEchoController`의 소리 재생이 가능하게 함.
  - **이동/시점 잠금 + 손 비주얼도 연결 완료(2026-10-06)**: `ObjectEchoController`의 `playerMovement` 필드를 `PlayerMovement` 전용 타입에서 `MonoBehaviour`(+ 새 인터페이스 `IPlayerLock`)로 일반화해서, KJH의 `PlayerMovement`든 KSM의 `NetworkFirstPersonController`든 똑같이 연출 중 이동/시점을 잠글 수 있게 함(`IHandReach`와 같은 패턴). `NetworkFirstPersonController`에 `SpeedMultiplier`/`LookEnabled`/`Yaw`/`Pitch`/`SetLookAngles`를 추가해 `IPlayerLock`을 구현함. FPSPlayer의 `Main Camera` 아래에 `PlaceholderHand`(단순 박스 손 모양, `PlaceholderHandReach`)도 추가해 `ObjectEchoController.handReach`에 연결함.
  - **범위 교정(같은 날, 2026-10-06 후속)**: 위 작업은 처음엔 `InteractionController`/`ObjectEchoController` 두 개만 FPSPlayer에 연결했는데, 사용자가 "KJH 씬에서 복사해온 모든 오브젝트들의 기능이 Develop에서도 똑같이 구동되게 해달라"고 재요청 — 확인해보니 Develop에 복사된 KJH의 비활성 `/Player` 오브젝트는 사실 `PlayerMovement`/`CameraModeController`/`PhotoInventory`/`PhotoCaptureSystem`/`InteractionController`/`ClueJournal`/`ObjectEchoController`를 전부 갖춘 완전한 세트였고, `ViewableClue`/`InspectableClue`(Clue 시스템) 같은 다른 KJH 오브젝트들은 여전히 `interactor.GetComponent<PlayerMovement>()`로 구체 타입을 직접 찾고 있어서 FPSPlayer(`NetworkFirstPersonController`) 앞에서는 항상 null → 이동/시점 잠금이 전혀 안 먹히는 상태였음. 또한 Develop 씬엔 `ClueViewerUI` 패널 자체가 없어서 `ViewableClue`의 "살펴보기" 기능이 경고 로그만 찍고 아예 열리지 않는 상태였음. 수정 내용:
    - `ViewableClue.cs`/`InspectableClue.cs`: `PlayerMovement` 전용 필드를 `IPlayerLock` 인터페이스로 교체(`interactor.GetComponent<IPlayerLock>()`), `CameraModeController.cs`/`PhotoInventory.cs`: `ObjectEchoController`와 같은 `MonoBehaviour`+런타임 캐스트 패턴으로 일반화 — 이제 KJH 쪽 모든 "이동/시점 잠금" 기능이 FPSPlayer에서도 동일하게 동작함.
    - Unity 메뉴 `Tools/KJH/Clue/Create Clue Viewer UI`를 실행해 Develop의 기존 Canvas 밑에 `ClueViewerUI`(어두운 배경 + 가운데 이미지 + 오른쪽 설명) 패널을 생성함 — 팀이 쓰던 에디터 도구를 그대로 사용해 KJH와 동일한 모양으로 만들어짐.
    - 새 `/GameSystems` 오브젝트에 `ClueJournal`을 추가함(직렬화 필드가 없는 순수 데이터 컴포넌트라 씬 로드 시점부터 항상 활성 상태인 곳에 둬야 함 — Player는 네트워크 스폰 전까지 없으므로 네트워크 플레이어 프리팹에 올리면 안 됨. 단, `ViewableClue_Sample`은 KJH 원본 씬에서부터 이미 자신의 `clueJournal` 필드가 그 비활성 `/Player`의 `ClueJournal`로 직접 연결되어 있어서 지금은 그쪽을 그대로 씀 — 컴포넌트는 GameObject가 비활성이어도 메서드 호출은 정상 동작하므로 문제없음. `/GameSystems`는 앞으로 참조가 비어있는 새 단서용 기본값 역할).
    - (이후 전면 포팅으로 대체됨 — 아래 "KJH 기능 전면 포팅" 섹션 참고)
  - 검증(최초 커밋 + 범위 교정 각각 Play 모드 실측): `ObjectEchoController.TryBegin()`으로 전체 시퀀스(Reaching→Narrowing→Listening→Returning) 통과 — 연출 중 `SpeedMultiplier=0`/`LookEnabled=false`로 잠기고, 손이 뻗어 나갔다가(`IsExtended=true`) 끝나면 복귀, 잠금도 해제됨. `ViewableClue.Interact()`로 "살펴보기" 흐름도 통과 — `ClueViewerUI.IsOpen=true`, `NetworkFirstPersonController`가 잠기고(`SpeedMultiplier=0`/`LookEnabled=false`), `ClueJournal.HasClue=true`로 획득 처리, `OnInteractPressed()`로 닫으면 전부 원복(`IsOpen=false`/`SpeedMultiplier=1`/`LookEnabled=true`). 둘 다 콘솔 에러 0. **(주의: 이 테스트는 전부 메서드 직접 호출로 한 것이라 실제 E키 입력 경로는 검증하지 못했고, 바로 다음 라운드에서 그 경로 자체가 깨져 있었다는 게 드러남 — 아래 참고.)**
  - **Unity MCP 자동화 팁**: `WaitForSecondsRealtime` 기반 연출(ObjectEcho 등)을 MCP `eval`로 단계별 확인할 때, 각 `eval` 호출 사이의 왕복 지연 자체가 이미 실시간 기준 수 초가 걸릴 수 있다 — "거의 즉시 다음 상태 확인" 할 생각으로 짧게 기다리면 이미 전체 연출이 끝나있을 수 있으니, 상태 변화를 보려면 호출 사이 간격을 그만큼 염두에 둘 것. 또한 `GameObject.Find()`는 비활성 오브젝트를 못 찾으므로(이번에 디버깅 중 헛갈렸던 부분) 비활성일 수 있는 대상은 `Object.FindAnyObjectByType<T>(FindObjectsInactive.Include)`를 써야 함.
- 겸사겸사 발견한 것: Develop 씬에 `EventSystem`이 2개 중복돼 있어 매 프레임 경고가 쌓이던 문제를 발견해 하나 삭제함 (KJH 씬 내용을 복사해오면서 같이 따라온 것으로 보임).
- **다른 팀원 씬/폴더(KJH 등)에는 네트워킹 세팅 자체가 반영 안 됨** — Develop이 사실상 통합 지점. 테스트 방법은 빌드 1개 + 에디터 Play 1개로 2개 프로세스를 띄워야 함(에디터는 동시에 Play 2번 불가).

## KJH 기능 전면 포팅 (2026-10-06, 같은 날 세 번째 라운드)
사용자가 실제로 플레이해보니 "손을 얹는다" 안내 문구는 뜨는데 E를 눌러도 아무 연출(화면 어두워짐/파형/소리)이 일어나지 않는다고 보고 — 직전 라운드의 검증이 전부 메서드 직접 호출(`TryBegin()`, `Interact()`)이었어서 실제 E키 입력 경로 자체는 한 번도 테스트되지 않았던 게 원인이었다. 추가로 "KJH 씬의 모든 기능(상호작용/사운드/사진 촬영)이 Develop에서 작동하게 해달라, KJH 씬은 건드리지 말고 다시 전부 복사해오고, 건물 현관에 배치해서 테스트하기 쉽게 해달라"는 요청.

**치명적 근본 원인 발견**: FPSPlayer 프리팹의 `InteractionController.interactAction`/`cancelAction`(`Player/Interact`, `Photo/ClosePhoto` 입력 액션 참조)이 **둘 다 null**이었다 — 과거 세션에서 `InteractionController`를 FPSPlayer에 연결할 때 `lookCamera`/`promptUI`만 런타임 주입하고 이 두 입력 액션 필드는 빼먹었던 것. 그 결과 `InteractionController.OnEnable()`의 `interactAction.action.performed += OnInteractPerformed` 구독 자체가 전혀 일어나지 않아 **E/Esc 입력이 아무 효과가 없었다** — 조준 감지(레이캐스트)는 `Update()`에서 별도로 돌아가서 안내 문구는 멀쩡히 떴지만, 실제 상호작용(ObjectEcho, Clue, 나중에 추가한 PhotoSpot 전부 포함)은 어느 것도 작동할 수 없는 상태였다. `mcp__unity__simulate_key`로 실제 E키를 눌러보고서야(메서드 직접 호출이 아니라) 발견함.
- 수정: `InputActionReference.Create(action)`으로 새로 만든 참조는 프리팹에 영구 저장되지 않는다(비영속 런타임 객체라 저장 시 `fileID: 0`으로 날아감 — 처음 이 방법으로 시도했다가 저장 후에도 여전히 null이어서 알아챔). 대신 `AssetDatabase.LoadAllAssetsAtPath("Assets/InputSystem_Actions.inputactions")`로 실제 영속 서브에셋(`InputActionReference`, 이름이 `"Player/Interact"`처럼 `맵/액션` 형태)을 찾아서 연결해야 프리팹에 제대로 저장된다. `ClueSetupMenu.FindActionReference()`가 이미 이 방식을 쓰고 있었음 — 입력 액션을 코드로 와이어링할 땐 항상 이 패턴을 쓸 것.
- 추가로 FPSPlayer의 태그가 `Untagged`였던 것도 발견 — `PhotoSpot.OnTriggerEnter`가 `other.CompareTag("Player")`로 체크하므로 태그를 `Player`로 바꾸지 않으면 트리거 기반 상호작용(PhotoSpot 등)이 영원히 작동하지 않는다. `Player` 태그로 변경함.

**또 다른 함정(두 번째로 발견)**: 프리팹의 직렬화 필드는 **씬 전용 오브젝트(Canvas UI, Volume 등)를 참조할 수 없다** — `SerializedObject`로 프리팹 인스턴스에 씬 오브젝트 참조를 넣고 `create_prefab`으로 저장해도, 저장된 프리팹 에셋에는 전부 `fileID: 0`(null)으로 사라진다(프리팹은 독립된 에셋이라 특정 씬에만 존재하는 오브젝트를 영구 참조할 수 없기 때문 — 이게 애초에 `ConfigureForOwner`/`SetViewCamera` 같은 런타임 주입 패턴이 필요했던 이유였다는 걸 이번에 다시 확인). `CameraModeController`/`PhotoInventory`/`PhotoCaptureSystem`도 똑같이 `ConfigureForOwner(...)` 메서드를 추가해서 `NetworkInteractionBridge.OnNetworkSpawn()`에서 Develop 씬의 Canvas 하위 오브젝트를 이름으로 찾아 주입하도록 고침.

**KJH → Develop 전면 재확인**: `open_scene(..., additive: true)`로 KJH를 Develop과 동시에 로드해서(KJH는 절대 `save_scene` 하지 않음 — 세션 내내 `isDirty: false` 유지) 비교한 결과, 이미 Develop에 들어와 있던 것: `ViewfinderVolume`, `Inspect_Volume`, `Echo_Wall_Portrait`+`Wall`+`Ambient_RoomTone`, `Cube`/`Cube (1)`, SoundDesk(`Echo_Desk_MusicBox`), `ViewableClue_Sample`. 빠져 있던 것(이번에 `Instantiate()` 후 `SceneManager.MoveGameObjectToScene`/부모 교체로 복사 — **원본은 건드리지 않음**):
- `PhotoSpot_01`(+`CameraAnchor`) — 건물 현관 근처 spawn 지점 옆 빈 공간 `(3, 0, 3)`에 배치(바닥 비어있음을 `Physics.CheckSphere`로 사전 확인).
- Develop의 `Canvas` 밑에: `FlashImage`(`FlashEffect`), `PhotoInventoryPanel`(+`ThumbnailParent`), `EnlargedViewPanel`(+`EnlargedImage`/`TitleText`/`DescriptionText`), `ViewfinderFrame`(+`RemainingCountText`+Bar 4개), `SpectrumOverlay`(+`InkLine`), `AcquiredToast`(+`Text`) — KJH의 `PhotoUICanvas`에서 그대로 복제. (`ClueViewer`/`InteractionPrompt`는 이미 있어서 건너뜀.)
- FPSPlayer 프리팹에 `CameraModeController`/`PhotoInventory`/`PhotoCaptureSystem` 컴포넌트 추가, `InteractionController.photoCaptureSystem`/`photoInventory` 연결(기존엔 둘 다 null이라 "인벤토리 열려있는 동안 바라보기 감지 멈추기" 등의 기능이 조용히 빠져 있었음), `NetworkInteractionBridge`에 이 셋도 `IsOwner` 기준으로 껐다 켰다 하도록 확장.
- **검증(이번엔 전부 `mcp__unity__simulate_key`로 실제 E/Tab 키 입력 경로를 통해서 확인)**: SoundDesk 앞에서 실제 E키 → `ObjectEchoController` 전체 시퀀스 진행 + `SpectrumOverlay.alpha=1`(파형 표시 확인) + 소리 재생. `PhotoSpot_01` 트리거 범위 진입 → 안내 문구 `[E] 사진 찍기` → E → 카메라가 `CameraAnchor`로 이동·고정(`IsLockedOnAnchor=true`)+뷰파인더 프레임/비네팅 등장(`frameAlpha=1`, `volWeight=1`) → E → 촬영 확정, `PhotoInventory`에 사진 1장 추가 확인 → Tab → 인벤토리 패널 열림(`IsOpen=true`) 확인. 콘솔 에러 0.
- 포팅하지 않은 것: KJH의 `Cube`/`Cube (1)` 역할을 하는 일반 장식용 큐브는 Develop에 이미 동일하게 있어서 추가 작업 없음. KJH의 `/Player`(비활성 원본), `EventSystem`, `Directional Light`, `Plane`은 Develop에 각자의 버전이 이미 있어서 중복 생성하지 않음(KJH 버전을 복사해오면 오히려 중복/충돌을 일으킴).

## KJH 추가 머지 반영 — 화면 암전 + 파형 중앙 이동 + 능력 시스템 (2026-10-06, 네 번째 라운드)
사용자가 KJH 브랜치를 Develop에 머지(`e726708`)한 뒤 "오브젝트 상호작용 후 화면이 좀 더 어두워졌으며 파형이 가운데에서 출력된다. 똑같이 Develop 씬으로 가져와달라"고 요청. `git show --stat`으로 머지 범위를 확인(KJH.unity + Common/PlayerAbility* 신규 + ObjectEchoController/SpectrumOverlay 대폭 수정 등 27개 파일) — **스크립트는 공용(Assets/Scripts)이라 이미 Develop에도 똑같이 적용돼 있고, 이번엔 "씬에만 있는 값/오브젝트"만 다시 포팅하면 되는 상황이었다**.

- **새 능력 시스템(`PlayerAbility`/`PlayerAbilities`/`IRequiresAbility`)**: 2인 협동에서 플레이어 A(소리 듣기)/B(사진 촬영) 능력을 분리. `PlayerAbilities` 컴포넌트가 없으면 능력이 필요한 상호작용(ObjectEcho=SoundEcho, PhotoSpot/PhotoInventory=PastPhoto)이 전부 막히고 경고가 뜬다 — FPSPlayer엔 이 컴포넌트 자체가 없었으므로 추가하지 않으면 이번 머지 이후 모든 상호작용이 조용히 깨지는 상황이었음. KJH 자신의 테스트 Player가 "혼자 테스트할 때는 두 능력 다 켜 둔다" 관례를 따라 `abilities = SoundEcho | PastPhoto`(값 3)로 두고 있어서 FPSPlayer도 동일하게 맞춤(2인 역할 분리는 아직 이 네트워크 프로토타입에 구현 안 됨 — 추후 과제).
- **화면 암전(`ObjectEchoController.darkenScreen`/`blackoutOverlay`/`blackoutAlpha` 등)**: 시야가 좁아진 뒤 전체 화면을 `blackoutAlpha`(KJH 값 0.9)까지 어둡게 덮는 새 `Darkening` 상태 추가. `blackoutOverlay`는 Canvas 전용 오브젝트라 프리팹에 직접 못 담음 → `ObjectEchoController.SetBlackoutOverlay(CanvasGroup)`를 새로 추가하고 `NetworkInteractionBridge.OnNetworkSpawn()`에서 Develop Canvas의 `EchoBlackout`을 찾아 주입하도록 확장(기존 `ConfigureForOwner` 계열과 동일 패턴).
- **파형이 화면 정가운데로 이동 + 스펙트럼 반응 전면 개편**: `SpectrumOverlay`가 "하단 중앙 작은 선(620×110)"에서 "화면 정가운데 큰 영역(1000×260)"으로 바뀌고, 내부 반응 계산도 완전히 새 구조(`SpectrumResponse`/`SpectrumResponseProfile` — 자동 음량 맞춤·dB 곡선·실제 파형 섞기 등)로 교체됨. 이런 값들은 손으로 재구성하는 대신 **KJH 씬의 실제 오브젝트를 그대로 복제**하는 쪽을 택함(`open_scene(..., additive: true)`로 KJH를 같이 열어 `Instantiate()` 후 Develop Canvas로 재부모화, KJH는 역시 한 번도 저장 안 함):
  - `EchoBlackout`(새로 추가): KJH에서 그대로 복제, Canvas 하위에서 `InteractionPrompt`보다 앞 sibling index로 배치.
  - `SpectrumOverlay`: Develop의 기존 구버전(하단 중앙)을 삭제하고 KJH의 최신 버전을 복제해 맨 뒤 sibling로 배치("암전 위에서도 파형은 항상 최상단에 보여야" 하므로).
  - `Echo_Desk_MusicBox`(Develop에서 echoClip guid `6afe45b1...`로 식별되는 쪽)에 `spectrumResponse = SpectrumResponse_Sensitive` 프리셋을 연결(KJH의 해당 오브젝트와 동일 — 단, Develop의 "Desk"/"Portrait" 이름과 KJH의 값 매핑이 echoClip 교체 이력(`a516fa8`)때문에 이름 기준과 어긋나 있어서 **반드시 echoClip guid로 대조**해야 했음).
- **중요 교훈 — Enter Play Mode에서 씬 리로드가 꺼져 있음**: 이 프로젝트는 Play 모드 진입 시 씬을 다시 로드하지 않도록 설정돼 있어서(확인됨: Play 모드 중에 `Instantiate`/`DestroyImmediate`/`SerializedObject` 편집을 해도 Stop 후에도 그대로 남아있음), 평소 "Play 중 편집은 Stop하면 사라진다"는 Unity 기본 가정이 이 프로젝트에선 성립하지 않는다. 그래도 **되도록 Edit 모드에서 씬을 편집하고 저장할 것** — Play 모드 중 편집이 우연히 남는 데 의존하지 말 것(다른 머신 설정에선 사라질 수 있음).
- **검증(Play 모드, 직접 `TryBegin()` 반복 호출로 빠른 폴링)**: `Reaching→Narrowing→Darkening→Listening→Returning` 전체 통과. `Darkening` 단계에서 `blackoutAlpha`가 0→0.9로 상승 확인, `Listening` 진입 시 `spectrumAlpha=1`(파형 표시, 중앙 배치 `anchorMin=(0.5,0.5)` 확인)+`blackoutAlpha=0.9` 동시 확인, 종료 후 둘 다 0으로 복원. 콘솔 에러 0(Awake 시점의 "Blackout Overlay가 비어있다" 경고 1건은 런타임 주입 전 타이밍이라 기존 lookCamera/promptUI와 같은 패턴의 정상적인 과도 경고).

## 사용 가능한 Unity 전용 스킬
이 환경에는 `unity:` 접두사의 스킬들(unity-cli, ui, urp-postprocessing, physics-3d-collision 등)이 이미 연결되어 있습니다. 관련 작업을 할 때는 먼저 해당 스킬을 확인하세요.

## 다음에 결정해야 할 것
1. 캐릭터 조작 방식 vs 디오라마 고정 시점 (프로토타입 필요)
2. 팀 공통 코딩 컨벤션, 공용 에셋/스크립트 배치 규칙
