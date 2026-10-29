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
- **⚠️ 누군가 Unity 에디터로 씬을 열어 작업 중일 때는 다른 터미널에서 그 씬이 포함된 브랜치를 체크아웃/풀하지 말 것.** git이 `.unity` 파일을 디스크에서 다른 버전으로 덮어쓰면 Unity가 외부 변경을 감지해 씬을 자동으로 다시 불러오면서, 그때까지 저장은 됐지만 커밋 전이었던 모든 작업(가구 배치, 조명 설정 등)이 조용히 사라진다(2026-10-16 실제 발생: 가구·나무·조명 설정 대량 소실, 다행히 직전 커밋으로 복구). 다른 사람이 KSM/Develop 씬에서 라이브로 작업 중인 게 확인되면, 브랜치 전환 전에 반드시 알리고 상대가 저장·커밋을 끝낼 때까지 기다릴 것.

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

## 약품 제조 퍼즐 GIM-00x (2026-10-19, KJH 씬)
- 스크립트 `Assets/Scripts/KJH/PotionCraft/`(네임스페이스 `PotionCraft`), 프리팹/데이터 `Assets/Prefabs/KJH/PotionCraft/`. 씬 배치·데이터 생성은 `Tools/KJH/PotionCraft/Build All`(재실행 가능, 덮어씀).
- 서버 권한 동기화는 `DoorSync_LGH`에 붙은 `PotionCraftSync` 하나가 담당(손에 든 아이템 + 모든 장치 상태 + 완료 여부, 늦게 접속하면 전체 상태 수신). 장치는 전부 `PotionStation`(= `IWorldInteractable`) 파생이라 LGH 플레이어 프리팹 수정이 없다. 새 장치는 `ServerInteract`/`GetState`/`ApplyState`/`Refresh`만 구현하면 된다.
- 도르래 `Dumbwaiter`는 `acceptedCargo` 필터(Candle/PotionLump)로 받는 화물을 정한다. 현재 PotionLump, 용량 1, 싣기 창=1층·꺼내기 창=2층. 예전 양초 테스트로 되돌리려면 필터를 Candle로.
- `LGH/PlayerInteractor.ShowMessage(msg, seconds)` 오버로드 추가(긴 자막용, 기존 2초 호출은 그대로).
- 검증 한계: Relay(UGS) 방 생성은 이 환경에서 불가, `NetworkManager.StartHost()` 로컬 호스트로만 확인. `simulate_key`는 Game 뷰 포커스가 없으면 키가 안 들어가므로 E 입력 경로는 `Interact()` 직접 호출로 대신 확인.

## 인형극 퍼즐 GIM-002 (2026-10-20, KJH 씬)
- 명세: `GIM-002_ClaudeCode_제작명세서.md` + 기획 원본 `GIM-002_반주로완성하는인형극(세부화)`(이채원). 결정: KJH 씬에 구현, 들기·꽂기 방식 도입, 놀이책 읽기 키 = F(R은 LGH 양초 바꾸기), 네트워크 = NGO 호스트 판정.
- 판정 로직은 순수 C# `Assets/Scripts/KJH/Gim002/Core/Gim002Logic.cs`(별도 asmdef `Gim002.Core`, 시간은 `Tick(dt)`) + EditMode 테스트 29개(`Gim002/Tests/Editor`, Test Runner > EditMode). 수치·장면·문구는 전부 `Assets/Prefabs/KJH/Gim002/Data/Gim002Config.asset`과 장면 데이터 에셋에 있다.
- 화면 스크립트는 `Gim002Controller.Instance`의 `State`/`StateChanged`/`EventReceived`만 보고 움직이고, 요청은 `IGim002Net`으로 보낸다. 오프라인은 `LocalGim002Net`, 네트워크는 `Gim002Sync.prefab`(`Gim002NetSync`, `DefaultNetworkPrefabs.asset`에 등록) — 호스트가 `LocalGim002Net`을 판정 엔진으로 돌리고 상태는 NetworkVariable, 사건은 Rpc(Everyone).
- 공용 들기 시스템: `Assets/Scripts/KJH/Common/Hold/`(`PlayerHand`, `HoldableItem`, `IHoldableSocket`). 손은 로컬 전용 자동 생성 오브젝트라 LGH 플레이어 프리팹을 수정하지 않는다. 물건을 들면 원래 자리에 `HoldableHomeSpot` 콜라이더가 켜져 "제자리에 두기"가 된다.
- GIM-001 연결: `PortraitPuzzle.SolvedEvent`(static event, `Solve()` 끝에서 호출) 한 줄만 추가함. 판정 로직은 그대로.
- 씬 배치는 `Tools/KJH/GIM-002/Build All`(재실행 가능, GIM002 루트를 지우고 다시 만듦, 데이터 에셋은 없을 때만 생성 → 튜닝 값 보존). 기존 오브젝트에도 컴포넌트를 붙인다: 음악실 문 2개(`Gim002KeyTarget`, 열쇠 쓰기 전 잠김), `Key_Behind_E3`/`Key_Behind_2F`(`Gim002KeyItem` + BoxCollider).
- 검증 한계: Play 모드에서 `StartHost()` 로컬 호스트로 GIM-001 해결 → 열쇠 2개 → 장면 1~3 → Complete까지 통과(오답·재접속 리셋 포함, 콘솔 에러 0). 단 `simulate_key`/`QueueStateEvent` 키 입력이 Game 뷰 포커스 문제로 들어가지 않아 **손잡이 2초 누르기와 놀이책 F/←/→ 입력은 직접 플레이로 확인 필요**, 두 클라이언트 동시 테스트도 미실행.

## 덤불 정원 미로 GIM-005 (2026-10-22, KJH 씬)
- 명세: `GIM-005_덤불정원미로.md`(김진호). 위치: 1층 정원 = `Courtyard`(x 36~52, z -16~0), 2층 정원 관리실 = `F2_MaidRoom`(북쪽 내벽 z=-9). 기존 맵 오브젝트는 수정하지 않고 별도 KJH 프리팹으로 제작.
- 명세서의 `Gim003*` 이름은 `Gim005*`로 바꿔 구현(복사 오기로 판단). 스크립트 `Assets/Scripts/KJH/Gim005/`(네임스페이스 `Gim005`), 프리팹·데이터 `Assets/Prefabs/KJH/Gim005/`. 재생성은 `Tools/KJH/GIM-005/Build All`(프리팹은 같은 경로에 덮어씀, `Data/`의 Config·SymbolData는 없을 때만 생성 → 튜닝 값 보존).
- 판정 로직은 순수 C# `Core/Gim005Logic.cs`(asmdef `Gim005.Core`) + 미로 논리 구조 `Core/Gim005MazeLayout.cs`(7×7칸, 가운데 3×3 광장, 진짜 길 G1→G3→G6→G7). EditMode 테스트 27개(`Gim005/Tests/Editor`)가 규칙과 "가짜 길 4개는 모두 막다른 길"을 검사한다. 미로 모양을 바꾸려면 `Gim005MazeLayout.Edges`를 고치고 Build All.
- 네트워크: GIM-002와 같은 구조(`IGim005Net` / `LocalGim005Net` / `Gim005NetSync`). `Gim005Sync.prefab`은 `DefaultNetworkPrefabs.asset`에 등록됨. 다른 플레이어 접속 시 진행 중 시도는 Ready로 리셋(완료 여부 유지).
- 완료 시 1/2층 철창 `Rail_StairFront`를 **이름으로 찾아** 모든 컴퓨터에서 비활성화한다(`Gim005Config.hideOnCompleteNames`, 부모 경로에 `Ravenwood_LGH` 포함 조건). 상태 기반이라 늦게 들어온 사람에게도 적용되고, 씬 참조를 다시 연결할 필요가 없다.
- **Develop 적용 방법**: Develop 씬에 `GIM005_Maze.prefab`(위치 44, 0, -8), `GIM005_GardenRoom.prefab`(26.5, 3.9, -9.13), `Gim005Sync.prefab`(최상위)을 프리팹 인스턴스로 놓으면 끝. Develop의 Ravenwood_LGH도 같은 좌표(68, 0, -16)라 위치가 맞는다. 놓은 뒤 `Tools/Network/Validate Scene NetworkObjects`로 확인.
- 패널 실제 모델(2026-10-27): `OBJ_SYMBOL_PANEL`의 임시 Frame/Board를 VARCO `Prop_ButtonPanel`(자식 `PanelVisual`, 회전 y 180, 높이 1.75, 2.5×1.84×0.5m)로 교체. 앞면 깊이는 벽에서 0.415~0.427m(위쪽이 살짝 튀어나옴), 테두리는 0.46m. 타이머 글자는 모델의 화면 홈(패널 로컬 -0.57, 2.38, 화면 깊이 0.435)으로 옮기고 임시 검은 배경(Back)은 삭제. 앞면 폭이 2.0m라 버튼 13개를 0.85배·간격 0.40m·줄 높이 1.99/1.61/1.23으로 줄이고, 줄마다 앞면 깊이+2mm에 붙임. 제목·상태 글자·표시등은 오른쪽 위 빈 곳. 몸통 콜라이더 `PanelCollider`는 앞면까지만(버튼 콜라이더를 가리지 않게). Builder(`BuildRoom`)도 같은 값으로 갱신 — 모델이 없으면 임시 판으로 대체.
- 중앙 버튼 실제 모델(2026-10-27): `OBJ_CENTER_BUTTON`의 임시 Pedestal/Top/Cap 원기둥을 VARCO `Prop_PuzzleButtonPedestal`(자식 `Pedestal`, 0.36×0.36×1.0m, 상판·기둥 일체)과 그 `ButtonMount`(높이 1.0008) 위의 `Prop_PuzzleButton`(자식 `Cap`, 지름 0.18·높이 0.1m)으로 교체. `Gim005CenterButton.cap` = 버튼 모델(완료 시 기존처럼 3cm 내려가 기둥이 받침대 속으로 들어감). 조준 콜라이더는 루트에 BoxCollider 2개(받침대·버튼). Builder(`BuildCenterButton`)도 같은 구성 — 모델이 없으면 임시 원기둥으로 대체.
- 임시 숫자 글자는 깊이 검사 셰이더 `Gim005_WorldText`를 쓴다(기본 TextMesh 글꼴 머티리얼은 URP에서 벽 너머로 비쳐 보임). 글꼴 텍스처는 실행 중에 만들어지므로 에디터(비실행)에서는 숫자가 안 보이는 게 정상.
- 검증 한계: `StartHost()` 로컬 호스트로 입구 통과→타이머, 오답(-15초·3초 잠금), 진짜 길 개방, 시간 초과(흐려짐→시작점 복귀→재배정), 중앙 버튼→완료→철창 비활성화까지 확인(콘솔 GIM-005 에러 0). E키는 Game 뷰 포커스 문제로 `PlayerInteractor`가 조준한 대상의 `Interact()`를 직접 호출해 확인. **두 클라이언트 동시 테스트와 실제 걸어서 통과는 직접 플레이로 확인 필요.**

## 온실 화분 퍼즐 GIM-004 (2026-10-22, KJH 씬)
- 명세: `GIM-004_온실화분퍼즐v2.md`(이채원). 1층 온실 `F1_Conservatory`(x 55~68, z -16~-9)에 중앙 선반 + 화분 7개(A~G), 2층 서고 `F2_Library/ReadingTable` 위에 온실 기록일지. 정답 D-E-A-F(선반 정면 기준 왼쪽부터, `Gim004Config.rules.answer`에서 변경 가능).
- 결정(2026-10-22): 정원문 = `Door_Courtyard_L/R`(남쪽 복도→정원 미로 입구 양문), 정원 관리실 문 = `Door_MaidRoom`. 두 문은 완료 전까지 런타임에 잠그고 완료 시 연다. GIM-003 완료 전 온실·서고 문 잠금은 **기본 꺼짐**(`Gim004Config.lockEntryUntilPotionCraft`로 켤 수 있음). 기록일지는 E로 펼치고 들고 나갈 수 없음. 선반은 뒤쪽·양옆 난간, 정면이 온실 입구(서쪽)를 봄.
- LGH 맵 프리팹은 건드리지 않음: 문은 `Gim004Controller`가 이름으로 찾아(`completeDoors`, 부모 경로에 `Ravenwood_LGH` 포함) 매 프레임 상태에 맞게 잠그고, 완료 시 호스트가 `DoorNetworkSync.RequestInteract`로 연다.
- 스크립트 `Assets/Scripts/KJH/Gim004/`(네임스페이스 `Gim004`). 판정은 순수 C# `Core/Gim004Logic.cs`(asmdef `Gim004.Core`) + EditMode 테스트 23개(배치 기록 4문장의 해가 D-E-A-F 하나뿐인지도 검사). 네트워크는 GIM-005와 같은 구조(`IGim004Net`/`LocalGim004Net`/`Gim004NetSync`), `Gim004Sync.prefab`은 `DefaultNetworkPrefabs.asset`에 등록됨. 화분은 공용 들기 시스템(`HoldableItem`), 손은 동기화 안 함.
- 재생성: `Tools/KJH/GIM-004/Build All`(재실행 가능). **기능/겉모습 분리**: `Assets/Prefabs/KJH/Gim004/Visuals/`의 `Gim004_PotVisual_A~G`, `Gim004_ShelfVisual`, `Gim004_PotBenchVisual`, `Gim004_JournalVisual`은 "없을 때만" 만들어짐 → 모델 교체는 이 프리팹 안만 바꾸고 Build All 재실행(화분 콜라이더 재조정 + 기록일지 사진 재촬영). 임시 모델로 되돌리기는 `Rebuild Placeholder Visuals`. `Data/Gim004Config.asset`도 없을 때만 생성.
- 실제 모델 적용(2026-10-26): 화분 겉모습 A~G = `Prop_GreenhousePotA~G`, 받침 벤치 = `Furn_GreenhouseBench` 2개(x ±1.5, 윗면 0.7m, 화분 간격 0.85m), 선반 자리 4곳의 `Pad` = `Prop_PressPlate`(화분 Anchor는 발판 `PotMount` 높이 0.119). Builder도 같은 값으로 갱신돼 Build All을 다시 돌려도 유지됨.
- 기록일지 사진 1~4(F·E·D·A)는 `Tools/KJH/GIM-004/Capture Journal Photos`가 실제 화분 겉모습 프리팹을 찍어 `Data/Photos`에 만든다(명세서 "사진은 게임 속 모델을 촬영" 원칙). 모델을 바꾸면 사진도 반드시 다시 찍을 것.
- **Develop 적용 방법**: `GIM004_Conservatory.prefab`(61.5, 0, -12.5), `GIM004_Journal.prefab`(62.55, 4.665, 3.5, 회전 y 180), `Gim004Sync.prefab`(최상위)을 프리팹 인스턴스로 두면 끝(Develop의 Ravenwood_LGH도 같은 좌표). 놓은 뒤 `Tools/Network/Validate Scene NetworkObjects`.
- 검증 한계: Play 모드 `StartHost()` 로컬 호스트에서 `Interact()` 직접 호출로 오답(D-F-A-E → 4개 원위치, 오답 1)·교체·빼기·정답(완료, 두 양문 잠금 해제+열림, 선반 잠금)·기록일지 열고 닫기까지 확인(콘솔 GIM-004 에러 0). **실제 E키 입력, 두 클라이언트 동시 플레이, 1층/2층 안내 문구·실패음 분리는 직접 플레이로 확인 필요.**

## 함께 미는 안방 가구 GIM-007 (2026-10-23, KJH 씬)
- 명세: `GIM-007_함께 미는 안방 가구.md`(김진호). 2층 안방 `F2_Master`(x 20~36, z 3~16, 바닥 y=3.9). 행동 동기화형: 두 사람이 서랍장 뒷면 손잡이 2개를 각각 잡고, 라운드마다 각자 화면에만 뜨는 W/A/S/D·방향키(↑↓←→) 8개 중 하나를(2026-10-27 방향키 추가, enum 끝에 추가해 기존 byte 값 유지) 제한 시간 안에 둘 다 맞히면 한 칸 전진. 안방 가운데(x 29)에서 동쪽으로 8칸×0.5m 밀어 드레스룸 문 앞 무게 발판(x 33, z 12.9)에 올리면 완료.
- 결정(2026-10-23): 비밀문 = 기존 드레스룸 양문 `Door_Master_Dressing`(+짝 `_B`)을 이름으로 찾아 완료 전 잠금, 완료 시 열고 **열린 채로 잠가 고정**(`keepDoorOpenAfterComplete`). 사람이 발판에 올라가면 덜컥거리기만 하고 문은 안 열림(우회 방지). 오입력도 실패(`rules.wrongKeyFails`). 임시 가구 = 서랍장. 수치는 명세서 제안값(8회/1.5초/숨고르기 2초) — 전부 `Gim007Config.asset`에서 조절.
- 입력 지연 처리: 각 컴퓨터가 **자기 화면에 키가 뜬 순간부터** 제한 시간을 재서 성공/실패를 보고하고, 호스트는 제한 시간 + `latencyGraceSeconds`(0.35초)까지 기다린 뒤 안 온 쪽을 시간 초과로 처리. 손잡이 점유는 RPC 발신자 ID로 판정(먼저 도착한 쪽), 접속이 끊기면 호스트가 그 사람 손잡이를 놓는다(가구 위치 유지).
- 스크립트 `Assets/Scripts/KJH/Gim007/`(네임스페이스 `Gim007`). 판정은 순수 C# `Core/Gim007Logic.cs`(asmdef `Gim007.Core`) + EditMode 테스트 24개. 네트워크는 GIM-004/005와 같은 구조(`IGim007Net`/`LocalGim007Net`/`Gim007NetSync`), `Gim007Sync.prefab`은 `DefaultNetworkPrefabs.asset`에 등록됨. `Gim007PushPlayer`(로컬 전용, 프리팹 루트에 1개)가 잡는 동안 플레이어를 손잡이 뒤에 세우고 시점 고정·키 UI(OnGUI)·E로 손 떼기를 담당 — LGH 플레이어 프리팹 수정 없음.
- **이동 잠금 주의**: LGH `PistolLoader`가 매 프레임 `FirstPersonController.SpeedMultiplier`를 1로 되돌린다(`PistolLoader.cs:190`). 그래서 `IPlayerLock.SpeedMultiplier=0`만으로는 이동이 안 멈춘다 — GIM-007은 `LateUpdate`에서 플레이어를 손잡이 자리로 다시 옮겨 고정한다. 다른 기믹에서 이동 잠금이 안 먹으면 이 원인을 먼저 의심할 것.
- 재생성: `Tools/KJH/GIM-007/Build All`(재실행 가능). **기능/겉모습 분리**: `Assets/Prefabs/KJH/Gim007/Visuals/`의 `Gim007_FurnitureVisual`(피벗 바닥 가운데, +Z = 미는 방향/발판 쪽, -Z = 잡는 뒷면), `Gim007_PlateVisual`(내려앉을 부분 이름 `PlateTop`), `Gim007_ScratchVisual`(바닥 긁힌 자국)은 "없을 때만" 만들어짐 → 모델 교체는 이 프리팹 안만 바꾸고 Build All 재실행(가구 콜라이더·손잡이 위치가 새 모델 크기에 맞춰 다시 잡힘). 임시 모델로 되돌리기는 `Rebuild Placeholder Visuals`. 경로는 프리팹의 `Furniture/PathStart`·`PathEnd` 위치, 소리는 각 컴포넌트 AudioClip 칸(비우면 `Gim007Sounds` 임시 소리).
- 실제 모델 적용(2026-10-27): `Gim007_FurnitureVisual` = VARCO `Furn_HeavyDresser_Single` 2개를 나란히 붙인 모양(`Dresser_L/R`, x ±0.385, 서랍·손잡이 면이 -Z = 미는 사람 쪽, 민무늬 뒷판이 발판 쪽). 전체 크기 1.61×1.20×0.66m. 기능 프리팹의 Body 콜라이더·Handle_0/1(x ±0.442)·StandPoint·LookPoint는 Builder와 같은 계산으로 다시 맞춤 → Build All을 다시 돌려도 같은 값이 나온다.
- 발판 실제 모델(2026-10-27): `Gim007_PlateVisual` = VARCO `Prop_WeightPlate`. 한 덩어리 메시라 테두리/윗판을 `Art/Meshes/Gim007_WeightPlate_Frame`·`_Top`으로 나눠 `Frame`·`PlateTop`(내려앉는 부분)에 씀. 임시 발판 크기 그대로 2.1×1.1m, 높이 0.025m(비균일 스케일). 윗판이 얇아져 `pressDepth` 0.035→0.012(Builder에도 반영).
- 혼자 테스트: `GIM007_MasterBedroom`의 `Gim007Controller`에서 `Debug Fake Partner`를 켜면 네트워크 없이 Play할 때 가짜 동료가 반대쪽을 잡고 자동 입력한다(네트워크 게임 중에는 동작 안 함).
- **Develop 적용 방법**: `GIM007_MasterBedroom.prefab`(31, 3.9, 12.9, 회전 y 90), `Gim007Sync.prefab`(최상위)을 프리팹 인스턴스로 두면 끝(Develop의 Ravenwood_LGH도 같은 좌표·같은 문 이름). 놓은 뒤 `Tools/Network/Validate Scene NetworkObjects`. Develop에서 다른 기믹이 `Door_Master_Dressing`을 잠그거나 열고 있으면 충돌하니 먼저 확인.
- 검증 한계: Play 모드 `StartHost()` 로컬 호스트에서 확인함 — 핸들 조준 안내 문구, 혼자 잡기("꿈쩍도 하지 않는다", 손잡이 뒤 고정·시점 고정), 상대(클라이언트 ID 1을 판정 엔진에 직접 넣어 흉내) 잡기 → 라운드 성공·오입력 실패·로컬 시간 초과 실패, 도중 손 놓기(취소·칸 유지·"상대가 손을 놓았다"), 다시 잡고 완료(가구 발판 위, 발판 내려앉음, 자동 손 떼기, 양문 열림+고정, 완료 문구), 발판 위 사람 덜컥거림(문 잠김 유지), 이미 잡힌 손잡이 안내, 접속 끊김 해제, `Validate Scene NetworkObjects` 0건, 콘솔 GIM-007 에러 0. **실제 W/A/S/D·E 키 입력(Game 뷰 포커스 문제로 `simulate_key` 불가), 두 클라이언트 동시 플레이·지연 체감, 수치 튜닝은 직접 플레이로 확인 필요.**

## 다락 별 상자 보상 — 황금상 + 현관 열쇠 (2026-10-23, KJH 씬)
- 다락의 별 상자 `StarLockBox_LGH`(LGH `Ravenwood_LGH.prefab` 안, 월드 26.09, 9.115, 11.88) 안에 황금상과 현관 열쇠를 둔다. 둘 다 임시 큐브(모델링 예정).
- 결정(2026-10-23): 황금상은 E → **두 사람 모두에게** 획득 연출 → 연출이 끝나면 **집은 사람 인벤토리**에. 현관 열쇠는 E → 바로 인벤토리. 현관 양문 `Door_Front_L/R`은 이 열쇠로 열리고, 열리면 KSM `GameFlowSync.RequestEscapeSuccess()`(탈출 성공 → 엔딩)를 부른다. 새 아이템 `Item_GoldStatue`(`gold_statue`)·`Item_FrontKey`(`key_front`)는 `Assets/Prefabs/KJH/AtticTreasure/Items/`에 있고, LGH `ItemDatabase_LGH` 목록 **끝(6·7번)**에 추가함(기존 번호 유지).
- 스크립트 `Assets/Scripts/KJH/AtticTreasure/`(네임스페이스 `AtticTreasure`). `AtticTreasureSync`(NetworkBehaviour, `AtticTreasureSync.prefab`, DefaultNetworkPrefabs 등록)가 호스트 판정: 황금상 상태(InBox/Cutscene/Taken)와 열쇠 주움 여부를 NetworkVariable로 동기화. 연출 도중 집은 사람이 나가면 황금상은 상자로 돌아가고, 연출 종료 신호가 120초 넘게 안 오면 강제로 지급. 상자가 열려 있을 때만 집을 수 있다(`StarLockBox.IsOpen`을 이름으로 찾은 상자에서 읽음).
- **연출 연결 방법**: `AtticTreasureCutscene`(추상 클래스, `Play(bool isPicker, Action onFinished)`)을 상속해 연출을 만들고 `AtticTreasureSync.prefab`의 Cutscene 칸에 연결. 별도 씬이면 Play에서 Additive로 로드 → 끝나면 언로드 후 `onFinished` 호출. 비어 있으면 임시 연출(`AtticTreasurePlaceholderCutscene`, 2.5초 화면 어두워짐 + 문구).
- **실제 연출 연결(2026-10-29)**: `AtticTreasureGlowCutscene`(`AtticTreasureSync.prefab`의 Cutscene 칸, 임시 연출 컴포넌트는 뗌). 약 6.2초 동안 열린 상자 속 황금상으로 줌인하고, 금빛 후광·뒤쪽 조명·블룸이 점점 강해지며, 금색 입자가 나선으로 올라가고, 왕관 끝에 짧은 별빛이 반짝인다. 줌인이 끝나는 4.25초에 보석이 크게 반짝이고 문구가 뜬다. 집은 사람은 자기 시점에서 출발하고, 동료는 어둠에서 시작 화면으로 들어온다. 연출 전용 카메라를 잠깐 만들어 쓰고(플레이어 카메라·양초·아바타는 숨김), 커서를 풀어 조작을 멈춘다(`MultiplayerMenu.OverlayOpen`). 내 왼손에 든 물건도 숨긴다. 황금상은 **겉모습만 복제한 임시 황금상**(`AtticTreasure_StatueCopy`)을 상자 속 원래 자리(씬 시작 때 기억)에 세워 보여 주고, 연출이 끝나면 지운다. 이유: 진짜 황금상은 상태가 "연출 중"이면 `AtticTreasureBoxItem`의 표시 조건(상자 안/손/상대 손)에 하나도 안 걸려서 숨겨진다. 그래서 실제 플레이에서 빈 상자가 보였다(2026-10-29 수정). 진짜 황금상의 표시·상태 로직은 건드리지 않는다. 모델 메쉬는 실행 중에 못 읽어서 보석·왕관 위치는 높이 비율(`gemHeight01` 0.87 등)로 정한다 → 모델을 바꾸면 이 값을 다시 맞출 것. 재연결은 `Tools/KJH/AtticTreasure/Build Glow Cutscene`(Build All도 호출, 수치는 보존)으로 하고, 재질은 `Materials/M_TreasureGlowAdditive.mat`이다. 소리는 임시로 LCW `인형극정답전구켜지기`(`gemFlashClip` 칸으로 교체).
- 별 상자 안쪽 이음새(2026-10-29): 몸체 모델 윗면(뚜껑을 잘라 낸 자리)이 1cm쯤 울퉁불퉁하고, 예전 안쪽 메시 테두리가 바깥 벽까지 못 덮어서 열린 상자(연출 화면)에 거친 띠가 보였다. `Meshes/AtticTreasure_StarBox_Interior.asset`을 다시 만들어 테두리가 몸체 윗면 전체를 덮고(최고점보다 살짝 위), 안팎 모서리를 깎고, 바깥 벽 바로 안쪽으로 짧은 덧댐을 내려 윗선을 가린다. 재생성은 `Tools/KJH/AtticTreasure/Rebuild Star Box Interior`(메시 에셋만 바꿈, 씬 수정 없음). 뚜껑을 닫으면 사이에 밝은 나무색 선이 얇게 보인다 — 어둡게 하려면 테두리용 재질 칸을 씬 렌더러에 하나 더 둬야 한다.
- 현관문: `AtticTreasureFrontDoor`가 실행 중에 문을 이름으로 찾아 `keyItem`만 넣는다(LGH 문·씬 데이터 수정 없음). 열쇠 확인·잠금 해제·양문 동기화는 LGH `InteractableDoor`/`DoorNetworkSync` 기존 방식 그대로. 판정 담당이 "잠겨 있던 문이 풀리고 열림"을 보면 한 번 `onFrontDoorOpened` + 탈출 성공 요청. KJH 씬엔 GameFlowSync가 없어서 로그만 남김(Develop에는 있음, Playing 단계에서만 받아들임).
- 재생성: `Tools/KJH/AtticTreasure/Build All`(재실행 가능, 아이템 중복 추가 안 함). 겉모습 `Visuals/AtticTreasure_StatueVisual`·`AtticTreasure_KeyVisual`은 없을 때만 생성 → 모델 교체 후 Build All이면 콜라이더가 새 크기에 맞춰짐. 임시 큐브로 되돌리기는 `Rebuild Placeholder Visuals`.
- **LGH 쪽 문제 2건 (2026-10-23 발견, LGH 담당자 확인 필요 — 수정하지 않음)**:
  1. `ItemNetworkSync`(바닥 아이템 줍기/내려놓기/나간 사람 아이템 떨구기)가 LGH 씬의 씬 오브젝트 `DoorSync_LGH`에만 있고 **`DoorSync_LGH.prefab`에는 빠져 있음** → KJH·Develop 씬에서는 WorldItem(놋쇠 열쇠 등)을 아예 주울 수 없고, 인벤토리에서 내려놓기도 안 된다. 그래서 황금상·열쇠 줍기는 `AtticTreasureSync`가 직접 처리하게 만들었다. 내려놓기용 숨은 자리(`*_DropSpot`, WorldItem)는 넣어 뒀으니 ItemNetworkSync가 프리팹에 들어오면 내려놓기도 동작한다.
  2. `FinalDevice_Dial`(`Ravenwood_LGH/TempFurniture/Attic`)이 납작한 원판 모양인데 기본 원기둥의 **CapsuleCollider(0.9m)가 남아 있어서 별 상자 전체를 감싼다** → 실제 플레이에서 상자 안 물건은 물론 **별 자물쇠(LGH 기믹) 자체도 조준이 막힘**. 콜라이더를 원판 크기로 바꾸거나 없애야 한다.
- 망원경 실제 모델(2026-10-27): LGH `Telescope_LGH.prefab` 겉모습을 VARCO `Prop_AstroTelescope`(자식 `Model`, 비율 유지 1.3배 = 높이 1.3m, 경통이 기존처럼 로컬 +X·창문 쪽)로 교체. 옛 원시도형은 꺼진 `OldPlaceholder` 묶음에 보관, 루트 BoxCollider는 새 크기로 재조정. `Telescope.cs`는 자식을 참조하지 않아 동작 변경 없음.
- **Develop 적용 방법**: `AtticTreasure_Contents.prefab`(26.088, 9.115, 11.879, 회전 y 270 — 별 상자와 같은 위치·회전), `AtticTreasureSync.prefab`(최상위)을 프리팹 인스턴스로 두면 끝. 놓은 뒤 `Tools/Network/Validate Scene NetworkObjects`. `ItemDatabase_LGH` 변경(아이템 2개 추가)도 같이 머지돼야 한다.
- 검증 한계: Play 모드 `StartHost()` 로컬 호스트에서 확인 — 상자 닫힘 시 집기 불가, 열면 안내 문구, 황금상 → 연출(임시) → 인벤토리, 열쇠 → 인벤토리, 열쇠 없이 현관문 "현관 열쇠이(가) 필요하다", 열쇠로 양문 잠금 해제·열림, 탈출 요청 지점 도달(KJH엔 GameFlowSync 없어 로그), Validate 0건, 콘솔 에러 0. 위 2번 콜라이더 문제 때문에 E 입력은 `Interact()` 직접 호출로 확인. **두 사람 동시 연출, Develop에서 실제 엔딩 전환은 직접 확인 필요.**

## 기믹 완료 → 문 열림 연결 (2026-10-23, KJH 씬)
- 요청: GIM-002(인형극) 완료 전까지 `Door_Kitchen`·`Door_Kitchen_Dining`(식당 쪽 우회로)·`Door_Guest2` 잠금, 완료 시 열림. GIM-003(약품 제조) 완료 전까지 `Door_Library`·`Door_Library_Guest1`(손님방1 쪽 우회로)·`Door_Conservatory` 잠금, 완료 시 열림. 전부 양문(`_B` 짝 자동).
- 스크립트 `Assets/Scripts/KJH/GimmickDoors/GimmickDoorLink.cs`(네임스페이스 `GimmickDoors`): 기믹 하나 + 문 목록. 문은 이름으로 찾고(양문은 한쪽 이름만, 짝 자동), 모든 컴퓨터가 매 프레임 완료 여부로 잠금을 맞춘다(늦은 접속·방 초기화에도 같음). 완료되는 순간 판정 담당이 `DoorNetworkSync`로 한 번 열고, 그 뒤엔 자유롭게 여닫기. 완료 판정 출처: GIM-002 = `Gim002Controller.State.state == Complete`, GIM-003 = `PotionCraftSync.Completed`. 씬에 그 기믹이 없으면 잠그지 않음(`lockWhenGimmickMissing` 기본 꺼짐). 문은 복도 쪽(`openFrom`)에서 방 안으로 열린다.
- 새 기믹-문 연결 추가: `DoorGimmick` enum에 기믹 하나 추가 + `IsComplete()`에 완료 판정 한 줄, 프리팹에 `GimmickDoorLink` 하나 더 붙이기. 네트워크 오브젝트가 아니라 DefaultNetworkPrefabs 등록 불필요.
- 재생성: `Tools/KJH/GimmickDoors/Build`(프리팹 `Assets/Prefabs/KJH/GimmickDoors/GimmickDoors.prefab` 덮어쓰기 + 씬 `GimmickDoors_KJH` 재배치).
- 완료 문구는 각 기믹 쪽이 띄운다(2026-10-23 변경): GIM-002 `Gim002Config.asset`의 `msgComplete` = "인형극이 끝났다. 어디선가 박수 소리와 함께 문이 열리는 소리가 들렸다.", GIM-003 `PotionCraftSync.prefab`의 `completionSubtitle` = "제작에 성공하였다. 건물 왼쪽편에 문이 열리는 소리가 들렸다." (KJH·Develop 씬 어디에도 덮어쓴 값이 없어 이 두 파일이 그대로 적용됨)
- **Develop 적용 방법**: `GimmickDoors.prefab`을 아무 위치에 프리팹 인스턴스로 두면 끝(위치 무관, 문 이름만 같으면 됨). Develop에 GIM-002/GIM-003이 아직 없으면 그 문은 잠기지 않는다.
- 검증: Play 모드 `StartHost()`에서 시작 시 8짝 잠김 → `PotionCraftSync.ServerComplete()`로 서재·온실 4짝 잠금 해제+열림(방 안쪽으로), 주방·객실2는 잠금 유지 → GIM-002는 `Gim002Controller.State`를 Complete로 바꿔(전체 진행 생략) 주방·객실2 4짝 열림 확인. 콘솔 에러 0, 씬 변경 없음. 실제 GIM-002 3장면 완주와 두 클라이언트 동시 확인은 직접 플레이 필요.
- 우회로 확인 결과(2026-10-23): 서재는 `Door_Library_Guest1`, 주방은 `Door_Kitchen_Dining`으로도 들어갈 수 있어 같은 기믹에 묶어 같이 잠금. 온실 정원 쪽 `Door_GardenConservatory`는 원래 잠겨 있음. 맵에 문이 추가되면 우회로를 다시 확인할 것.

## 네트워크 동기화 정책 — "전면 재구축" 대신 "규칙 + 검사" (2026-10-21)
팀원들과 실제 멀티플레이 플레이테스트를 하면서 같은 유형의 버그가 반복 발견됐다: `DumbwaiterSync_KJH`, `PortraitPuzzleSync_KJH`, `DoorSync_LGH`(+빠진 `CandleNetworkSync`) 전부 **코드 로직 자체는 멀쩡했는데, Develop 씬에 프리팹이 아닌 맨몸 오브젝트로 배치**돼 있었다. `NetworkManager`의 `EnableSceneManagement=False`라서, 게스트가 접속하면 씬에 맨몸으로 둔 `NetworkObject`(또는 `DefaultNetworkPrefabs`에 등록 안 된 프리팹)를 전부 지우고 호스트가 보낸 정보로 "등록된 프리팹"만 다시 만들기 때문이다. 호스트 혼자 테스트하면 멀쩡해 보여서 게스트 접속 전까지 못 알아챈다.

이걸 계기로 "Develop에서 네트워크 동기화 전체를 처음부터 새로 구축할지" 논의했고, **하지 않기로 결정**했다. 이유:
- 각 기믹의 동기화 로직(`DumbwaiterNetworkSync`, `PortraitPuzzle`, `DoorNetworkSync`, `PotionCraftSync`, `Gim002NetSync` 등, 전부 KJH/LGH 개인 폴더 소속)은 이미 잘 설계돼 있다 — 문제는 로직이 아니라 Develop에 배치하는 절차였다.
- 전면 재구축은 각 담당자(KJH·LGH)의 개인 폴더 파일을 직접 고치게 될 가능성이 커서, 팀 작업 공간 분리 원칙("Assets 폴더 구조" 섹션)에 어긋난다.
- 로직을 텍스트로 복사해 Develop 전용 통합 스크립트로 재조립하는 방식도 기각했다 — 원본과 복사본 두 군데에 같은 로직이 생겨서, 담당자가 원본을 고쳐도 Develop 복사본엔 반영이 안 되는(이번에 반복된 바로 그) 포팅 누락 버그를 구조적으로 고정시키기 때문이다.

**대신 택한 방식 — 규칙 + 기존 자동 검사 도구 활용**:
1. 동기화 스크립트는 앞으로도 KJH·LGH님이 각자 씬에서 작성·테스트한다. Develop은 그 **프리팹 에셋을 그대로 인스턴스화**해서 가져다 쓴다(텍스트 복사 금지 — 반드시 같은 프리팹 원본을 참조).
2. 규칙: **씬에 두는 모든 `NetworkObject`는 프리팹이어야 하고, `Assets/DefaultNetworkPrefabs.asset`에 등록돼 있어야 한다.**
3. 이 규칙은 이미 `Assets/Scripts/KJH/Common/Editor/NetworkSceneObjectValidator.cs`가 자동으로 검사한다 — Play 모드 진입 직전, 빌드 직전, 메뉴 `Tools/Network/Validate Scene NetworkObjects` 세 시점에 돌며 위반 시 콘솔에 `[NetworkSceneValidator]` Error로 찍는다(Play/빌드를 막지는 않음 — 그래서 지금까지 놓쳤다).
4. **새 체크리스트 — 누군가의 브랜치를 Develop에 merge할 때마다**: merge 직후 Unity 에디터에서 Develop 씬을 열고 한 번 Play 모드에 진입(또는 `Tools/Network/Validate Scene NetworkObjects` 메뉴 실행)해서 콘솔에 `[NetworkSceneValidator]` 에러가 0개인지 확인한 뒤에 merge 완료로 친다. 에러가 있으면 그 오브젝트를 원본 프리팹 인스턴스로 교체하고 `DefaultNetworkPrefabs`에 등록.

## 팀 플레이테스트 발견 버그 수정 (2026-10-21, 두 번째 라운드)
실제 호스트+게스트 플레이테스트에서 "한 사람이 문을 열고 닫아도 다른 사람 화면에는 변화가 없다"는 버그를 보고받음. 원인은 이미 같은 날 앞서 고친 것과 동일한 패턴(`DoorSync_LGH`가 프리팹이 아닌 씬 오브젝트였고 `CandleNetworkSync`가 통째로 빠져 있었음, 위 "네트워크 동기화 정책" 섹션 참고)일 가능성이 높음 — 이 세션 환경은 에디터 1개로는 호스트+게스트 동시 테스트가 불가능해서(빌드 1개 + 에디터 Play 1개 필요) 100% 확답은 못 함. **다음 실제 2인 플레이테스트에서 문 동기화를 다시 확인해줄 것.**

같은 보고를 계기로 "이것과 비슷하게 동기화 안 되는 게 더 있을 것"이라는 우려에 따라 두 가지를 점검·조치함:
- **`DayNightCycle`(낮밤 주기, `Assets/Scripts/LGH/DayNightCycle.cs`) — 실제로 동기화 안 되고 있었음, 수정 완료**: 기존엔 순수 `MonoBehaviour`라 각 클라이언트가 자기 타이머를 따로 돌렸음(호스트·게스트가 서로 다른 시각을 보게 됨). `NetworkBehaviour`로 바꾸고 `NetworkVariable<float> syncedT01`(host-write, everyone-read)을 추가 — 호스트(또는 네트워크 꺼진 싱글)만 `t01`을 실제로 흘려보내 `syncedT01`에 써주고, 게스트는 받은 값을 그대로 반영만 함(`DrivesTime = !IsSpawned || IsServer`). 기존 다른 동기화 매니저와 같은 패턴으로 프리팹화: `Assets/Objects/LGH/DayNightSync_LGH.prefab` 생성(`DefaultNetworkPrefabs`엔 Unity Netcode가 자동 등록), 씬의 기존 `DayNightCycle_LGH` 오브젝트를 그 프리팹에 연결(`SaveAsPrefabAssetAndConnect`)하고 중복으로 남아있던 비활성 `DayNightCycle` 오브젝트는 삭제. Play 모드에서 호스트로 직접 검증: `freezeTime`을 잠깐 풀어서 `t01`이 흐르는 동안 `syncedT01.Value`가 정확히 같이 움직이는 것 확인, 다시 `freezeTime=true`로 복원. `NetworkSceneObjectValidator` 0건, 콘솔 에러 0건.
- **서랍(`InteractableDrawer`) — 점검해보니 이미 정상 동작 중, 수정 불필요(이전 보고 정정)**: 직전 턴에서 "상호작용 트리거가 없다"고 보고했던 건 제 점검 실수였음 — `Drawer0` 오브젝트 자체의 컴포넌트만 보고 판단해서 놓쳤는데, 실제로는 `PlayerInteractor.cs`가 레이캐스트에서 `InteractableDrawer`를 타입째로 찾아 `CandleNetworkSync.RequestDrawer()`로 넘기고, `CandleNetworkSync`가 `Awake()`에서 씬의 모든 `InteractableDrawer`(이 나이트스탠드 서랍 2개 포함)를 자동으로 찾아 인덱스를 매겨 완전히 동기화(RPC + 늦은 접속자 전체 상태 수신)하고 있었음. Play 모드에서 `CandleNetworkSync.Instance.RequestDrawer(drawer)`를 직접 호출해 실제로 열리고 닫히는 것까지 확인함.

## 옵션(설정) 시스템 P0 구현 (2026-10-22)
사용자가 상세 스펙(P0/P1/P2 우선순위, UI 구조, 진입 규칙)을 주고 P0 항목 구현을 요청함. 핵심 원칙: 옵션은 플레이어별 로컬 설정(상대 PC에 전파 금지), ESC 옵션 창은 `Time.timeScale`을 쓰지 않고 로컬 입력만 잠그며 네트워크 세션·음성·상대 플레이어는 그대로 유지.

**착수 전 발견한 중요 사실 — 생각보다 기반이 많이 갖춰져 있었음**:
- 음성 채팅이 이미 완전히 구현돼 있음(`Assets/Scripts/KSM/Voice/VoiceChatManager.cs`, Vivox v16 `VivoxService.Instance` 기반): 마이크 음소거 토글(M키), 마이크 입력 감도(게인), 거리 기반 상대 음량 감쇠(`UpdateDistanceVolume`), 상태 표시(`Status`)까지 전부 있었음. "상대방 음성 음량" 슬라이더만 추가하면 됐음(`SetRemoteVolumeScale(0~1)`을 추가해 기존 거리 감쇠 위에 곱산으로 결합).
- 마우스 감도 필드가 이미 `LGH.FirstPersonController.mouseSensitivity`로 존재 — 그대로 매 프레임 덮어쓰기만 하면 됨.
- ESC 처리가 이미 부분 구현돼 있었음: `FirstPersonController.Update()`가 ESC에서 커서만 풀어주고(잠그는 코드는 없음), `LGH.MultiplayerMenu.OnGUI()`가 "커서가 풀려있고 인게임이면 = 일시정지"로 판단해서 이미 "일시정지" 패널(닉네임/계속하기/방 나가기)을 그리고 있었음 — `Time.timeScale` 사용 안 함, 네트워크 안 끊음, 이미 요구사항 2번을 만족하는 상태. 패널에 "옵션" 버튼만 추가하면 됐음.
- `LGH.PlayerInventory.EscapeConsumed`(정적 프로퍼티)가 이미 "인벤토리 등 상위 모달이 이번 프레임에 Esc를 썼는지" 판단하는 용도로 존재 — 옵션의 ESC 처리가 이 값을 확인하게 해서 상위 모달 > 옵션 > 일반 인게임 우선순위를 그대로 재사용함.
- `/TitleCanvas/TitleRoot/ButtonPanel/OptionsButton`이 이미 씬에 있었지만 `TitleMenuUI.cs` 주석에 "옵션 버튼은 아직 기능이 없어서 연결 안 함"이라고 적혀 있던 상태 — 핸들러만 추가해서 연결함.
- UI 체계가 혼합 상태: 로비/타이틀은 uGUI(Canvas), 인게임 HUD·소지품·일시정지는 전부 `OnGUI()`(IMGUI, `PlayerInventory.cs`/`MultiplayerMenu.cs` 패턴). 인게임 옵션 창은 이 기존 관례를 따라 OnGUI로 만듦(별도 UI 체계를 섞지 않기 위함).
- 오디오 믹서(`Assets/Sounds/KJH/KJH_AudioMixer.mixer`)에 그룹이 `Master/Ambient/Echo` 셋뿐이고, 씬에 실제 `AudioSource` 컴포넌트는 2개(미사용 `Speaker_1F/2F`)뿐 — 현재 거의 모든 효과음이 `AudioSource.PlayClipAtPoint(...)`로 그때그때 재생되고 있어 믹서를 거치지 않음. **BGM 음량·효과음 음량을 진짜로 분리하려면 Audio Mixer 창에서 Master 밑에 "BGM"/"SFX" 자식 그룹을 만들고, 각 그룹 볼륨 페이더를 우클릭 → Expose to script → 이름을 각각 `BGMVolume`/`SFXVolume`으로 지어야 함(그룹 생성·파라미터 노출은 공개 API가 없어서 스크립트로 못 함, 팀원이 에디터에서 직접 해야 함)**. 그 전까지는 해당 슬라이더가 존재하고 저장도 되지만 실제로 들리는 소리에는 영향이 없음(그룹이 없으면 `OptionsManager`가 콘솔에 경고만 찍고 조용히 넘어감). "전체 음량"은 믹서 라우팅과 무관하게 `AudioListener.volume`을 직접 제어하므로 지금 당장도 모든 소리에 적용됨.

**구현 내용** (전부 `Assets/Scripts/KSM/Options/`, 네임스페이스 `CoopDemo`):
- `PlayerOptionsData`(값 묶음) + `PlayerOptions`(static, PlayerPrefs 키 `KSM_PlayerOptions_v1`에 JSON으로 저장/로드, `Preview()`=미리보기만/`Save()`=영구저장/`RevertToSaved()`=취소).
- `OptionsManager`(씬에 상주하는 `OptionsSystem` 오브젝트, `DontDestroyOnLoad`): `PlayerOptions.Changed` 구독, `AudioListener.volume`/믹서 BGM·SFX 파라미터/`VoiceChatManager` 마이크·상대음량/`Screen.fullScreenMode`/전역 Volume의 `ColorAdjustments.postExposure`(기존 아트 기준값 0.6 위에 슬라이더 오프셋을 더함, 대체 아님)/로컬 플레이어의 `mouseSensitivity`에 반영. 밝기는 기존 `Building_LGH_Volume`(전역 Volume, 이미 ColorAdjustments 보유)을 그대로 씀 — 새 Volume 안 만듦.
- `OptionsMenuController`: 오디오/그래픽/조작 3탭 + [기본값]/[취소]/[적용 및 저장]/[닫기]. 슬라이더는 즉시 `PlayerOptions.Preview()`로 미리보기 적용, 적용 및 저장을 눌러야 `PlayerPrefs`에 영구 반영. 취소=저장된 값으로 되돌리고 닫기, 닫기=지금 미리보기 값은 그대로 둔 채(이번 세션 한정) 그냥 닫기. `Open()`/`Close()`에서 로컬 플레이어의 `FirstPersonController.SpeedMultiplier=0`/`LookEnabled=false`만 잠그고(`Time.timeScale`/`NetworkManager`는 절대 안 건드림), ESC는 `PlayerInventory.EscapeConsumed`를 먼저 확인한 뒤에만 닫음.
- 진입점 3곳: 타이틀 화면(`TitleMenuUI.OnOptionsClicked` → 기존 OptionsButton에 연결), 대기실/방 목록 화면(`MultiplayerMenu`에 작은 옵션 버튼 추가, 타이틀이 꺼진 뒤에도 접근 가능하도록), 인게임 일시정지 패널(`MultiplayerMenu`의 "일시정지" 박스에 옵션 버튼 추가 — 옵션이 열려있는 동안은 `MultiplayerMenu.OnGUI()`가 자기 패널을 안 그리게 해서 화면을 넘겨줌).
- 검증: Play 모드에서 전부 직접 호출/상태확인으로 확인함(슬라이더 미리보기 → `AudioListener.volume`/`postExposure` 즉시 반영, 적용 및 저장 → `PlayerPrefs` 기록 확인, 취소 → 저장값 복원 확인, 인게임에서 옵션 열때 `SpeedMultiplier=0`/`LookEnabled=false`이면서 `NetworkManager.IsListening=true`·`Time.timeScale=1` 유지 확인). 실제 클릭(마우스)·ESC 키 입력 경로 자체는 이 환경에서 `simulate_key`가 Game 뷰 포커스 문제로 불안정해 직접 플레이로 추가 확인 필요.

**아직 안 한 것(P1, 이번엔 손 안 댐)**: 마이크/스피커 장치 선택, 해상도 변경, 키 재설정, FOV, 조준점/화면 흔들림, 자막 크기 — 전부 스펙 그대로 뒤로 미룸.

**후속 조정(같은 날)**: 사용자가 "배경음악/효과음 음량 분리는 폴리싱 과정에서 검토"하기로 결정 — 오디오 믹서에 BGM/SFX 그룹이 아직 없는 상태와도 맞물려, `OptionsMenuController`의 오디오 탭에서는 "전체 음량"/"상대방 음성"만 보이게 줄임. `PlayerOptionsData.bgmVolume`/`sfxVolume` 필드와 `OptionsManager`의 믹서 반영 로직은 그대로 남겨뒀으니(위 "Audio Mixer 창에서 BGM/SFX 그룹 추가" 안내도 유효), 나중에 폴리싱 때 슬라이더 두 줄만 `DrawAudioTab()`에 다시 추가하면 됨.

## 옵션 시스템 P1 확장 — 마이크/스피커 장치, 해상도, 조준점 (2026-10-22, 같은 날 후속)
사용자가 P1 중 "마이크 및 스피커 장치 목록·선택", "해상도 변경", "조준점 크기·색상"을 요청하고, 직접 크로스헤어 에셋(`Assets/AssetPacks/OccaSoftware/Crosshairs` — OccaSoftware의 "Massive Crosshair Bundle", 512×512 투명 PNG 7종)을 임포트해서 "플레이 중 마우스 커서 대신 조준점이 보이게" 요청.

**새로 추가**:
- `PlayerOptionsData`에 `resolutionWidth/Height`, `micDeviceId`/`speakerDeviceId`(Vivox `VivoxInputDevice.DeviceID`/`VivoxOutputDevice.DeviceID`), `crosshairSize`(0.5~2.5배), `crosshairColor` 필드 추가.
- 오디오 탭: Vivox가 준비되면(`VoiceChatManager.IsReady`) `VivoxService.Instance.AvailableInputDevices`/`AvailableOutputDevices`를 그대로 나열해서 클릭 한 번으로 `SetActiveDeviceAsync()` 호출(실제 테스트 환경에서도 "Default System Device/Communication Device/No Device" + 실제 사운드카드 이름까지 정상적으로 나열·선택됨을 확인함).
- 그래픽 탭: `Screen.resolutions`에서 중복 제거한 해상도 목록을 ◀▶로 순회. 고르는 즉시 `Screen.SetResolution()`으로 바로 적용해보고, 15초 "이 해상도를 유지할까요?" 타이머를 띄움 — [유지하기]를 누르거나 시간이 지나면 자동으로 이전 해상도로 복구(`Time.unscaledTime` 기준이라 일시정지 중에도 흐름, `Time.timeScale` 미사용 원칙과는 무관). **주의**: `Screen.SetResolution`은 에디터 Game 뷰에서는 실제로 창 크기를 안 바꾸는 게 Unity의 알려진 제약이라 에디터에서는 로직만 확인했고 실제 창 크기 변경은 빌드에서 확인 필요.
- 게임 탭(신규): 조준점 크기 슬라이더(0.5~2.5배) + 색상 프리셋 5종(흰/빨/초/노/하늘) 버튼.
- `LGH.PlayerInteractor`: 화면 중앙의 "+" 텍스트 크로스헤어를 `crosshairTexture`(현재 `Crosshair_04.png`를 `NetworkPlayer_LGH.prefab`에 연결함) 기반 그리기로 교체, 크기/색상은 `PlayerOptions.Current`에서 매 프레임 읽음. **`Cursor.lockState == Locked`일 때만 그리도록 조건을 추가**해서, 일시정지·소지품·옵션·책 읽기 등 커서가 풀리는 모든 상황에서는 조준점이 자동으로 사라지고 마우스 커서가 그 자리를 대신하게 함(기존 "+"는 이 조건이 없어서 일시정지 중에도 계속 그려지고 있었음 — 겸사겸사 고침).

**테스트 중 발견해 고친 버그**: `PlayerOptions.Load()`가 `JsonUtility.FromJson<T>()`로 저장된 JSON을 읽었는데, 이번에 필드를 추가한 뒤 "예전에 저장된(새 필드가 없는) JSON"을 불러오면 새 필드들이 전부 C# 기본값(0, 즉 `crosshairColor`는 완전 투명/`crosshairSize`는 0배)으로 비어서 **조준점이 통째로 안 보이는** 상태가 됐었음 — 실제로 이 증상으로 재현·발견함. `JsonUtility.FromJsonOverwrite(json, boxed Default())` 방식으로 바꿔서, 저장 파일에 없는 필드는 항상 `PlayerOptionsData.Default()` 값을 쓰도록 고침(`Load()`/`RevertToSaved()` 둘 다). **앞으로 `PlayerOptionsData`에 필드를 추가할 때마다 이 패턴 덕분에 예전 세이브 파일이 있어도 안전함** — 새 필드 추가 자체는 걱정 없이 해도 됨.

## 옵션 창 버그 수정 + 조준점 모양 선택 (2026-10-22, 같은 날 세 번째 후속)
사용자가 "타이틀에서 옵션 창을 켜도 뒤에 있는 시작/옵션/나가기 버튼이 눌린다"고 보고 + "조준점을 종류별로 고르고 옵션 창에서 미리보기 하고 싶다" 요청.

- **원인**: 옵션 창은 `OnGUI`(IMGUI)로 그리고 타이틀 화면은 uGUI(Canvas+EventSystem)라서 서로 다른 입력 체계다 — OnGUI가 화면 위에 그려지는 것과 별개로, uGUI의 `EventSystem`은 그 뒤에 깔린 버튼에 대해 클릭 판정을 계속한다. **수정**: `OptionsMenuController.Open()/Close()`에서 `EventSystem.current.enabled`를 꺼졌다 켜지게 함 — OnGUI 자체는 EventSystem과 무관하게 동작하므로 옵션 조작에는 영향 없음. Play 모드에서 `EventSystem.enabled`가 열 때 false, 닫을 때 true로 바뀌는 것 확인함.
- **조준점 모양 선택 추가**: `PlayerOptionsData.crosshairIndex` 필드 추가. `LGH.PlayerInteractor.crosshairTexture`(단일) → `crosshairOptions`(배열)로 변경, `OptionsMenuController`도 똑같은 순서로 `crosshairTextures` 배열을 따로 들고 있음(둘 다 `Assets/AssetPacks/OccaSoftware/Crosshairs`의 7종: 02/04/23/24/25/26/27 순서 — **둘의 순서를 반드시 맞춰야 함**, 인덱스로만 매칭됨). 기본값은 인덱스 1(Crosshair_04, 단순 십자형).
- "게임" 탭에 ◀ [미리보기 박스] ▶ 추가 — 어두운 배경 위에 지금 고른 모양을 실제 크기/색상 그대로 그려서 보여줌(Play 모드에서 모양 전환 시 미리보기가 즉시 바뀌는 것 확인함). 인게임 조준점도 같은 인덱스를 봐서 동일하게 적용됨.

## 일시정지 중 이동·상호작용이 안 멈추던 버그 수정 (2026-10-22, 같은 날 네 번째 후속)
사용자가 "일시정지해서 옵션을 열어도 마우스 시점만 멈추고 WASD 이동과 E 상호작용(문 등)은 계속된다"고 보고. 요구사항: 일시정지한 바로 그 플레이어만 전부 멈춰야 하고(개별 옵션 원칙), 상대 플레이어는 영향받으면 안 됨.

**원인**: 마우스 시점이 멈추는 건 애초에 `OptionsMenuController`가 `LookEnabled`를 잠가서가 아니라, `FirstPersonController`의 시점 코드가 우연히 `Cursor.lockState == Locked`일 때만 돌아가게 되어 있어서였음. **WASD 이동 코드와 `PlayerInteractor`의 E/R 상호작용 코드는 애초에 커서 잠금 상태를 전혀 확인하지 않아서**, 일시정지 패널만 떠 있어도(옵션을 열기 전부터) 계속 작동하고 있었음 — Options 기능 자체의 버그가 아니라 원래 일시정지 시스템에 있던 공백이었음.

**수정**: 이 프로젝트에서 "커서가 풀려 있다 = 이 플레이어만 메뉴가 떠 있다"는 신호로 이미 일관되게 쓰이고 있어서(조준점 표시/숨김도 이걸로 판단), 같은 기준을 움직임·상호작용에도 적용함:
- `LGH.FirstPersonController.Update()`: 기존에 있던 "닉네임 입력 중(`typing`)이면 WASD·점프 무시" 패턴 옆에 `bool paused = Cursor.lockState != CursorLockMode.Locked;`를 추가해서 `typing || paused`일 때 WASD·점프·사다리 오르내리기 입력을 전부 무시하도록 함.
- `LGH.PlayerInteractor.Update()`: 맨 앞에 `if (Cursor.lockState != CursorLockMode.Locked) return;`를 추가해서 조준 레이캐스트와 E/R 입력 자체를 멈춤(안내 문구도 같이 사라짐).
- 둘 다 `Cursor.lockState`는 그 컴퓨터(로컬 플레이어) 것만 읽으므로 상대 플레이어의 조작에는 전혀 영향 없음 — 개별 옵션 원칙 그대로 유지됨.
- **검증 한계**: `simulate_key`로 W를 눌러서 확인하려 했으나 이 환경에서 Game 뷰 포커스 문제로 입력 자체가 전달되지 않아(잠금 상태에서도 위치 변화 없음 — 즉 대조군조차 움직이지 않아서 시뮬레이션 자체가 무효였음) 코드 검토로만 확인함. 코드 패턴은 이미 검증된 기존 `typing` 가드와 동일한 방식이라 안전하다고 판단하지만, **실제 플레이로 한 번 확인 필요**.

## 게임 흐름(로비→도입부→플레이→엔딩→결과) 시스템 (2026-10-22, 다섯 번째 후속)
사용자가 상세 스펙(원하는 사용자 경험 다이어그램, 전환 조건, ESC/입력 처리 표)을 주고 "별도 씬 추가 없이 Canvas 기반으로" 구현을 요청함. 먼저 조사부터 함 — 스펙이 가정한 것과 실제가 다른 부분이 있었음:

**조사 결과 (스펙과 실제의 차이)**:
- "LobbyCanvas"/"GameCanvas"/"OptionCanvas"는 Canvas가 아니었음. 씬에 실존하는 Canvas는 `TitleCanvas`·`VoiceHudCanvas` 둘뿐이고, 로비/일시정지는 `MultiplayerMenu.cs`가, 옵션은 `OptionsMenuController.cs`가 `OnGUI`(IMGUI)로 그림 → 기존 OnGUI 두 개는 **손대지 않고**, Intro/Ending/Result만 신규 uGUI Canvas로 추가. `MultiplayerMenu.OnGUI()`에 "스테이지가 Playing이 아니면 아무것도 안 그림" 가드 한 줄만 추가함(OnGUI는 uGUI보다 나중에 그려져서 가드가 없으면 화면이 겹침).
- Input System 액션맵은 실제 게임플레이에 쓰이지 않음(전부 `Keyboard.current` 직접 폴링) — 지난 ESC 버그 수정 때 이미 확립한 "`Cursor.lockState`가 Locked가 아니면 이동/상호작용 정지" 패턴을 그대로 재사용. `GameFlowUI`가 Playing이 아닌 동안 커서를 계속 풀어두는 것만으로 로비/도입부/엔딩/결과 내내 이동·상호작용이 자동으로 막힘.
- "탈출 성공" 조건은 프로젝트에 아직 없음(문은 전부 동일한 `InteractableDoor`, 출구를 구분하는 스크립트 없음) — 특정 문을 임의로 "출구"로 지어내지 않고 `GameFlowSync.ServerConfirmEscapeSuccess()`/`RequestEscapeSuccess()`라는 연결 지점만 공개로 열어둠. **사용자 요청으로 임시 비밀 커맨드(`DebugEscapeTrigger`, Playing 중 키보드로 E-S-C-A-P-E 순서 입력)가 당분간 이 자리를 대신 채움 — 진짜 탈출 기믹이 생기면 그 스크립트에서 `GameFlowSync.Instance.RequestEscapeSuccess()`를 직접 부르고 `DebugEscapeTrigger`는 지우면 됨.**
- "타이틀로 돌아가기"가 이미 두 버전 있었음: `MultiplayerBackButton`(세션 시작 전 전용)과 `MultiplayerMenu`의 private `LeaveRoom()`(세션 정리 포함) — 후자를 `public async Task LeaveRoomAsync()`로 노출만 해서 재사용(기존 호출부는 `void LeaveRoom(){ _ = LeaveRoomAsync(); }` 래퍼로 그대로 유지).

**구현** (전부 `Assets/Scripts/KSM/GameFlow/`, 네임스페이스 `CoopDemo`, Gim002/PortraitPuzzle과 같은 "NetworkVariable+Rpc, 서버만 결정" 패턴):
- `GameFlowSync`(NetworkBehaviour, 프리팹 `Assets/Prefabs/KSM/GameFlow/GameFlowSync.prefab`, DefaultNetworkPrefabs 자동 등록): `enum GameStage{Lobby,Intro,Playing,Ending,Result}` 하나의 NetworkVariable + 슬롯(0/1)별 완료 플래그 4개(`introDone0/1`, `endingDone0/1`) + 서버 시계 기준 시작/클리어 시각. `Update()`에서 서버가 `LGH.NetworkPlayer.All.Count>=2`를 보고 Lobby→Intro 자동 전환. 모든 전환 메서드는 "지금 그 단계가 아니면 조용히 무시"라 중복 호출·연타에 안전.
- `IllustratedCutsceneController`: 도입부/엔딩 공용 컷 재생기(`Kind.Intro`/`Kind.Ending`). `Time.unscaledDeltaTime`만 쓰고 `Time.timeScale`은 안 건드림. 다음/스킵 다 "한 번만 보고"가 보장됨(`localFinished` 플래그).
- `GameFlowUI`: 로컬 전용 — `GameFlowSync.StageChanged`를 보고 Intro/Ending/Result Canvas 중 하나만 켜고, Playing에 막 들어선 순간에만 커서를 잠근다(그 뒤엔 옵션/소지품이 자유롭게 풀었다 잠갔다 함).
- `ResultCanvasController`: 클리어 시간(`playingStartedAt`~`clearedAt`, 서버 시간 기준 단일 값)을 HH:MM:SS로 표시, 타이틀 버튼은 연타 방지 + "세션 정리(`LeaveRoomAsync`) → 로컬 UI 숨김 → `TitleMenuUI.ShowTitle()`" 순서 보장.
- `GameFlowBuilder`(Editor, `Tools/KSM/GameFlow/Build All`): 이 프로젝트의 다른 Build All 도구들과 같은 방식 — 재실행 가능, Canvas 3개(IntroCanvas/EndingCanvas/ResultCanvas)를 전부 유니티 기본 UI(단색 Image+Text)로 임시 제작. **실제 일러스트가 생기면 각 Canvas의 Illustration Image에 스프라이트만 끼우면 됨, 코드 변경 불필요.**

**테스트 중 발견해 고친 버그**: `ResultCanvasController`가 `FindAnyObjectByType<TitleMenuUI>()`를 기본(비활성 제외) 옵션으로 호출하고 있었는데, `TitleMenuUI`는 `TitleRoot`에 붙어 있고 게임 중엔 `TitleRoot`가 꺼져 있어서 **항상 못 찾고 조용히 아무 일도 안 일어나고 있었음**(null 체크가 있어서 에러도 안 남) — 이 프로젝트에 이미 문서화된 "비활성 오브젝트는 FindObjectsInactive.Include로 찾아야 한다" 함정을 제가 새 코드에서 또 밟은 것. `FindObjectsInactive.Include`로 고침.

**검증**: Play 모드에서 호스트 기준 전체 사이클을 직접 메서드 호출/버튼 `onClick.Invoke()`로 끝까지 확인함 — Lobby→Intro(자동, 2명 스폰 조건)→스킵 연타해도 1회만 보고→상대 슬롯 완료 시 Playing 전환+커서 잠금→비밀 커맨드 대역(`RequestEscapeSuccess`)으로 Ending 전환+클리어 시간 기록→양쪽 완료 시 Result 전환→결과창에 시간/인원 정상 표시→타이틀 버튼 연타 방지+세션 정리+타이틀 복귀까지 스크린샷으로 확인. `NetworkSceneObjectValidator` 0건.
**검증 한계**: 이 환경은 호스트 1개 프로세스만 띄울 수 있어서 "상대방 완료"는 전부 슬롯 ID를 직접 지정해 흉내 낸 것 — 진짜 2인 동시 플레이, 그리고 `DebugEscapeTrigger`의 실제 키보드 입력 자체는 `simulate_key`의 Game 뷰 포커스 문제로 못 돌려봄(기존에도 반복된 한계). **다음 2인 플레이테스트에서 전체 흐름과 비밀 커맨드 입력을 실제로 확인 필요.**

## 대기방(로비) 준비 시스템 추가 (2026-10-22, 여섯 번째 후속)
사용자가 "방 생성 후 게스트를 기다리는 대기 방을 만들고, 게스트가 들어온 뒤 호스트·게스트가 모두 준비 버튼을 누르면 3초 후 인트로→플레이로 넘어가게 해달라"고 요청.

**착수 전 발견 — 사실 지금은 대기 화면 자체가 완전히 비어 있었음**: 직전(다섯 번째) 라운드에서 GameFlow 시스템을 넣으며 `MultiplayerMenu.OnGUI()`에 "스테이지가 Playing이 아니면 아무것도 안 그림" 가드를 추가했는데, 그 시점에 `Lobby` 단계에서 "상대를 기다리는 중..." 문구를 그리던 기존 코드 블록이 그 가드 뒤에 있어서 완전히 죽은 코드가 돼 있었다(그동안은 혼자 테스트하며 2인 자동 연결 조건이 거의 즉시 충족돼 눈치채기 어려웠음). 이번 요청은 그 자리를 제대로 채우는 작업이기도 함.

**구현**:
- `GameFlowSync.cs`: `Lobby` 단계에 슬롯별(0/1) 준비 상태(`readyLobby0/1`)와 카운트다운 종료 시각(`introCountdownEndAt`, 서버 시계 기준, 3초 `LobbyCountdownSeconds`)을 NetworkVariable로 추가. `Update()`(서버 전용)가 매 프레임: 2명 미만이면 준비 상태·카운트다운 전부 취소 → 2명이고 둘 다 준비면 카운트다운 시작/진행, 끝나면 기존 `ServerAdvanceToIntro()` 재사용 → 둘 중 하나라도 준비를 취소하면 카운트다운만 취소(Lobby 유지). 기존 "2명 접속되면 바로 Intro" 자동전환 로직은 이 흐름으로 대체됨. `RequestLobbyReady(bool)`을 기존 `RequestIntroFinished()`와 같은 패턴(호스트면 바로, 아니면 `Rpc(SendTo.Server)`)으로 추가. UI가 읽을 `LocalLobbyReady`/`OtherLobbyReady`/`LobbyCountdownRemaining`도 공개.
- **겸사겸사 발견해 고친 잠재 버그**: `GameFlowSync`는 Develop 씬에 프리팹 인스턴스로 계속 남아있는 유일한 오브젝트라(네트워크 세션이 끝나도 GameObject 자체는 안 지워짐), 같은 에디터/빌드 실행 중 두 번째로 호스팅을 시작해도 NetworkVariable 값이 이전 판의 값(예: `Result` 단계, 완료 플래그들)을 그대로 들고 있을 수 있었다 — 지금까지는 테스트할 때마다 매번 에디터를 새로 Play해서 드러나지 않았을 뿐, 실제 팀원이 한 번 플레이 끝내고 같은 창에서 바로 "다시하기"를 한다면 재현됐을 문제. `OnNetworkSpawn()`에서 `IsServer`일 때 `ServerResetForNewSession()`을 호출해 매 호스팅 시작마다 `Lobby`+모든 플래그 초기값으로 리셋하도록 고침.
- `MultiplayerMenu.cs`: 화면 소유권 가드를 "Playing이 아니면 전부 안 그림"에서 "Intro/Ending/Result일 때만 안 그림"으로 완화해서 `Lobby` 단계를 이 스크립트가 다시 그리게 함. 기존 "일시정지" 패널 내용은 `DrawPauseBody()`로 그대로 옮기고, 새 `DrawLobbyWaitingRoomBody()`를 추가해 같은 패널(박스)에 방 이름/코드, 닉네임 입력, 인원 현황("상대를 기다리는 중..." 또는 나/상대 닉네임), 준비 상태("나: 준비 완료 · 상대: 준비 안 됨"), 준비/준비취소 토글 버튼(2명 미만이면 비활성화), 카운트다운 중엔 버튼 대신 "모두 준비 완료! N초 후 시작합니다..." 문구를 그림. 옵션/방 나가기 버튼도 그대로 유지.
- 새 Canvas나 새 프리팹은 추가하지 않음 — 전부 기존 OnGUI 패널과 기존 NetworkVariable+Rpc 패턴 재사용.

**검증**: Play 모드에서 `NetworkManager.StartHost()`로 직접 호스트를 띄워 확인(이 환경은 실제 Relay 세션 생성이 불가해 기존 라운드들과 동일한 방식). 혼자(1명)일 때 `RequestLobbyReady(true)`를 불러도 다음 프레임에 강제로 `false`로 되돌아감(2명 미만 가드 확인). `LGH.NetworkPlayer.All`에 더미 엔트리를 추가해 2명 상태를 흉내 낸 뒤 양쪽 슬롯을 리플렉션으로 준비 완료 처리 → 카운트다운 진행 후 `Stage`가 자동으로 `Intro`로 전환됨을 확인(두 eval 호출 사이 왕복 지연만으로 3초가 넘어 카운트다운이 끝나 있었음). 호스트를 재시작(`Shutdown()` → `StartHost()`)해 두 번째 호스팅 세션을 흉내 내자 `Stage`가 `Lobby`로 제대로 리셋되는 것도 확인(위 리셋 버그 수정 검증). 둘 다 준비 후 한쪽이 바로 준비를 취소하면 `Stage`가 `Lobby`에 그대로 머물고 카운트다운이 `-1`로 취소되는 것도 확인. 콘솔 에러/예외 0건(테스트 중 플레이어를 흉내 내느라 생긴 "오디오 리스너 2개" 로그 1종만 있었음 — 테스트 아티팩트이지 실제 버그 아님).
**검증 한계**: 전부 호스트 1개 프로세스 + 리플렉션/더미 데이터로 흉내 낸 것이라, 실제 두 번째 사람이 방 목록에서 들어와 준비 버튼을 누르는 진짜 클릭 경로, 그리고 대기방 패널이 실제로 올바르게 그려지는지(레이아웃/버튼 클릭 판정)는 **다음 2인 플레이테스트에서 실제로 확인 필요**.

## GIM-005(덤불 정원 미로) Develop 통합 (2026-10-22, 일곱 번째 후속)
KJH 브랜치에서 GIM-005 구현을 머지해온 뒤, "다른 시스템을 해치지 않으면서 Develop에 병합"해달라는 요청. KJH 쪽 CLAUDE.md에 이미 "Develop 적용 방법"이 명시돼 있어(`GIM005_Maze.prefab`(44,0,-8), `GIM005_GardenRoom.prefab`(26.5,3.9,-9.13), `Gim005Sync.prefab`(최상위)을 프리팹 인스턴스로 두면 끝 — Develop의 `Ravenwood_LGH`도 KJH와 같은 좌표라 바로 맞음), 그 안내 그대로 세 프리팹을 Develop 씬 루트에 인스턴스화하고 위치만 맞췄다. 코드/프리팹 자체는 전혀 수정하지 않음(GIM-002/GIM-003 등과 동일하게 "KJH 프리팹 원본을 그대로 가져다 쓰기" 원칙 준수).

**겸사겸사 발견해 고친 버그 — 이번 작업과 무관하게 이미 깨져 있었음**: `Tools/Network/Validate Scene NetworkObjects`를 돌려보니 `GameFlowSync`가 `DefaultNetworkPrefabs.asset`에 등록 해제된 상태였다(게스트 접속 시 이 오브젝트가 삭제되는 문제 — "네트워크 동기화 정책" 섹션에서 반복 경고한 바로 그 패턴). 직전 라운드(13a521e 커밋)에는 분명히 등록돼 있었는데, 그 이후 KJH/KWJ 브랜치를 Develop에 머지하면서 `DefaultNetworkPrefabs.asset` 충돌("# Conflicts: Assets/DefaultNetworkPrefabs.asset")을 해결하는 과정에서 빠진 것으로 보인다. `NetworkPrefabsList.Add(...)` API로 다시 등록해서 수정(YAML 직접 편집 대신 Netcode 공식 API 사용). **앞으로 `DefaultNetworkPrefabs.asset`에서 충돌이 나면, 병합 후 반드시 `Validate Scene NetworkObjects`를 돌려서 양쪽 브랜치가 추가한 항목이 전부 살아있는지 확인할 것** — 자동 병합이 리스트 항목을 조용히 누락시킬 수 있다.
**검증**: `Validate Scene NetworkObjects` 재실행 → "문제 없음". Play 모드에서 `StartHost()`로 호스트 직접 시작 → `Gim005Controller.Instance`가 Config 연결된 채로 초기화되고 네트워크 시작 시 `LocalGim005Net`→`Gim005NetSync`로 정상 전환(`IsAuthority=True`) 확인. `RequestEnter()`(Ready→Running)→오답 1회(시간 -15초·3초 패널 잠금 확인)→정답 1회(덤불 열림 확인)→`RequestCenter()`(Running→Complete)까지 전체 흐름 통과. 완료 시 `Ravenwood_LGH/Floor2/Balustrade/Rail_StairFront`가 실제로 비활성화되는 것까지 확인(Develop 고유의 이름 기반 참조가 올바르게 맞아떨어짐을 검증한 부분). 테스트 종료 후 Play 모드를 나가자 씬이 `isDirty=false`로 깨끗하고 `Rail_StairFront`도 다시 `active=True`로 돌아와 있음을 확인(Play 중 상태가 저장된 씬에 새지 않음). 콘솔 에러 0건.
**검증 한계**: 전부 호스트 1개 프로세스에서 `RequestEnter/RequestPress/RequestCenter`를 직접 호출해 확인한 것 — 실제 두 사람이 각자 입구 통과(A)/문양 버튼 누르기(B)로 역할을 나눠 플레이하는 것, 그리고 덤불을 실제로 걸어서 통과하는 공간적 경로 자체는 **다음 2인 플레이테스트에서 확인 필요**(이 부분은 KJH 씬에서 이미 검증됐던 핵심 판정 로직이 아니라, Develop에 옮겨 놓았을 때 공간 배치가 실제로 잘 맞는지에 대한 것).

## 대기방에서 저택(플레이 화면) 가리기 (2026-10-22, 여덟 번째 후속)
사용자가 "대기방에 입장했을 때 저택 모습, 즉 플레이 화면이 안 보이도록 가려달라"고 요청. 대기방은 `MultiplayerMenu`의 OnGUI 패널로 그리는데, Canvas/OnGUI 모두 화면 전체를 덮지 않는 중앙 박스라서 그 뒤로 플레이어가 스폰된 저택 3D 장면이 그대로 비쳤음.

- `GameFlowUI`에 `lobbyCanvas` 필드를 추가해 Intro/Ending/Result와 같은 방식으로 관리: `Stage == Lobby`일 때만 켠다. 이 Canvas는 불투명 배경 이미지 하나뿐이고, 실제 대기방 내용(인원/준비 버튼 등)은 기존 `MultiplayerMenu`의 OnGUI 패널이 그 위에 그대로 그린다(OnGUI가 uGUI Canvas보다 나중에 그려지므로 자연스럽게 맨 위에 보임) — 새 UI 로직을 따로 만들지 않고 배경만 추가.
- `GameFlowBuilder.cs`(`Tools/KSM/GameFlow/Build All`)에 `BuildLobbyCanvas()`를 추가해 재실행 가능하게 유지. 이 도구를 실제로 다시 실행해 Develop에 `LobbyCanvas`를 추가함 — 재실행 후 `GameFlowSync`의 `DefaultNetworkPrefabs` 등록(fileID 포함)이 그대로 유지되는 것도 재확인함(이 프로젝트의 "idempotent Build All" 관례가 안전하다는 재확인).
- **테스트 중 발견해 고친 디테일**: 처음엔 Intro/Ending/Result Canvas와 같은 배경색 알파 0.97(연출용으로 살짝 어둡게)을 그대로 썼는데, 실제 스크린샷으로 확인해보니 밝은 촛불 광원이 3%만큼 비쳐서 은은하게 보임 — 다른 Canvas는 "연출 중 살짝 어둡게"가 목적이라 문제없지만, 대기방은 "아예 안 보이게"가 목적이라 적합하지 않음. 배경 알파를 1.0(완전 불투명)으로 바꿔서 해결. 스크린샷 비교로 촛불 빛샘이 완전히 사라지는 것까지 확인함.
- **검증**: Play 모드에서 실제 사용자 흐름 그대로 재현(`TitleMenuUI.OnStartClicked()` 호출 → 호스트 시작) → `capture_game_view`로 Game 뷰를 직접 캡처해 비교. 수정 전엔 좌하단 음성 HUD 텍스트와 촛불 빛이 은은하게 비쳤고, 알파 1.0으로 바꾼 뒤에는 완전한 검은 배경 위에 대기방 패널만 깔끔하게 보임. `Validate Scene NetworkObjects` 재실행 결과 "문제 없음", 콘솔 에러 0건.

## 기믹 더미 → VARCO 모델 교체 1차 (2026-10-22, 아홉 번째 후속)
KWJ님이 만든 실제 3D 모델(바르코, 인형극 소품·도르래 부품·약품 믹서)이 `Assets/VARCO 3D/KWJ/`에 새로 들어와서, Develop 씬 곳곳의 원시도형(Cube/Cylinder/Sphere) 더미를 실제 모델로 교체하는 작업.

**사전 조사**: `_NeedsCollider` 폴더(콜라이더가 아직 없다는 뜻)의 새 프리팹 17개를 전부 열어 메시·피벗·바운드를 확인하고, GIM-002 무대(`OBJ_STAGE`)·덤웨이터(`Dumbwaiter_Rig`)·PotionCraft 믹서(`Station_Mixer_F2`)의 기존 더미 오브젝트와 좌표·스크립트 참조(`Gim002StageCrank.handle`, `Dumbwaiter.cabin/counterweight/pulleyWheel/cabinRope/counterweightRope`, `MixerStation.layers` 등)를 하나하나 대조해서 교체 가능 여부와 매핑을 정리함. 사용자가 이 매핑으로 진행을 승인.

**발견한 문제 — 해결**: `Assets/VARCO 3D/KWJ/Prefabs/_NeedsCollider/Prop_TrashBin.prefab`이 깨져 있었음(원본 FBX가 같은 머지에서 삭제됐는데 이 프리팹은 여전히 그 GUID를 프리팹 변형 부모로 참조해서 `AssetDatabase`가 로드 자체를 못 함). 아무 씬/프리팹도 참조하지 않는 것을 확인하고 삭제. **주의**: KJH 브랜치를 다시 머지하면 이 깨진 파일이 또 복구될 수 있음(실제로 이번 세션에서 한 번 그랬음) — 머지 후 매번 재확인 필요.

**GIM-002 인형극 무대**: 처음엔 VARCO 원본 조각(`Furn_PuppetStage`/`Furn_PuppetStage_Details`/`Prop_Puppet*`)을 직접 재조립하려 했으나, 작업 중 사용자가 팀장님께 전달받은 안내를 공유함 — KJH님이 이미 `Assets/Prefabs/KJH/Gim002/Gim002_Stage.prefab`(무대 전체, 진행 램프 3개·반주자 표시등 포함)과 업데이트된 `Gim002_Lamp_Progress.prefab`/`Gim002_Lamp_Ready.prefab`을 만들어뒀다는 내용. 확인해보니 **당시엔 아직 저장소에 없었음**(Gim002_Stage.prefab 자체가 아예 없었고, 램프 프리팹 2개는 있었지만 내용은 옛날 더미 그대로)이라 사용자에게 상황을 알리고 진행 방식을 물음 → 사용자가 "무대는 보류, 다른 기믹(도르래/믹서)부터 진행"으로 결정.
이후 사용자가 KJH 최신 브랜치를 머지해오자(`4c4e71b` GIM-002 무대를 프리팹으로 분리 등) `Gim002_Stage.prefab`과 두 램프 프리팹이 실제 모델(Maya 메시 `pCylinder1`/`pSphere1`/`texture_mapping`, 기존 Unity 기본 도형 `Cylinder`/`Sphere`/`Cube`와 다름)로 들어온 것을 확인 — 팀장님 안내 그대로 진행:
- `GIM002_KJH/F2_Nursery/OBJ_STAGE`를 삭제하고 같은 부모 아래 `Gim002_Stage.prefab`을 위치 (60.5, 3.9, -8.45), 회전 0으로 배치.
- 1층 피아노 램프 4개(`Ravenwood_LGH/TempFurniture/F1_Music/PianoLamps` 아래 `OBJ_LAMP_PROGRESS_1F_1~3`, `OBJ_LAMP_STAGE`)는 프리팹 연결이 끊긴 옛 복사본이라 각각 삭제 후 `Gim002_Lamp_Progress`/`Gim002_Lamp_Ready` 프리팹 인스턴스로 교체, 기존 위치·회전과 `kind`/`progressIndex`/`blinkOnStageWrong` 값을 그대로 재적용.
- 검증: `Validate Scene NetworkObjects` 문제 없음. Play 모드에서 호스트로 `RequestGim001Solved`→`RequestStageWound`→`RequestMusicDoorOpened`→`RequestCrankCompleted` 순으로 진행해 커튼이 실제로 좌우 각각의 기둥 방향으로 열리는 것(새 `Gim002Curtain` 스크립트가 메시 폭을 직접 읽어 계산 — 하드코딩된 기본 Cube 가정 없음)과, 반주자 표시등의 렌즈 2개가 함께 켜지는 것(`Gim002IndicatorLamp.bulb`는 `Lens_L` 하나만 연결돼 있는데도, 강제로 다른 색을 넣어도 다음 프레임에 자동으로 원래 점등색으로 돌아옴 — 같은 머티리얼을 쓰는 GPU 인스턴싱 동기화로 추정)까지 확인. 콘솔 에러 0건.

**도르래(Dumbwaiter)**: `Ravenwood_LGH/Floor1/Dumbwaiter_Rig` 내부에서 `Dumbwaiter.cs`가 `cabin`/`counterweight`/`pulleyWheel`/`cabinRope`/`counterweightRope` 트랜스폼을 직접 움직이고 있어서, 이 트랜스폼 자체는 그대로 두고 **시각적 자식만** 새 모델로 바꿈(스크립트의 위치/회전/스케일 제어가 그대로 유지되도록):
- `PulleyWheel`의 더미 `Disc`/`Spoke` 삭제 → `Furn_PulleyAssembly` 프리팹에서 `Wheel`/`Axle` 조각만 꺼내 자식으로 연결.
- `Cabin`의 더미 `Floor`/`Side_*`/`Lip_West` 삭제(화물 표시용 `Cargo_0/1/2`는 유지) → `Furn_PulleyAssembly`의 `LiftBox/Model`을 바닥 피벗 기준으로 높이 절반만큼 띄워 자식으로 연결.
- `Counterweight`의 더미 `Body` 삭제 → `Furn_PulleyAssembly`의 `Counterweight/Model`을 같은 방식으로 연결.
- **로프는 코드 수정이 필요했음**: 기존 `StretchRope()`는 "기본 실린더(높이 2, 중심 피벗)"를 가정하고 위치를 중심점으로, 스케일을 길이의 절반으로 계산했는데, 새 로프 모델(`Pulley_RopeStrand`, 메시 로컬 높이 ≈0.98)은 피벗이 로프 **위쪽 끝(도르래 쪽)**에 있고 거기서 아래로 늘어지는 구조라 가정이 안 맞음. `Dumbwaiter.cs`에 `ropeMeshHeight` 필드를 추가하고 `StretchRope()`를 "위치는 도르래 높이로 고정, 세로 스케일만 `늘어진 길이 / ropeMeshHeight`로 계산"하도록 수정(실제 메시 바운드 값으로 자동 설정). `Rope_Cabin`/`Rope_Counterweight`는 하위 오브젝트를 추가하지 않고 자기 자신의 `MeshFilter`/`MeshRenderer`를 새 로프 메시·머티리얼로 직접 교체(피벗이 이미 맞으므로 가장 간단).
- 검증: Play 모드에서 `Dumbwaiter.GoTo(0)`으로 1층→2층 전체 구간 이동시켜, 도착 후 `Rope_Cabin`/`Rope_Counterweight`의 위치(도르래 높이로 고정)·스케일(계산식과 소수점까지 정확히 일치)을 직접 대조해 수식이 올바르게 동작함을 확인. 콘솔 에러 0건.
- **시각 확인 한계**: Scene 뷰 카메라를 도르래 위치로 옮겨 스크린샷을 여러 번 시도했으나 방이 어둡고 구도를 맞추기 어려워 깔끔한 사진을 못 건짐(화물 상자는 제자리에서 확인됨) — 수치 검증은 확실하나 **실제 눈으로 본 마감 상태는 다음 플레이테스트에서 확인 필요**.

**PotionCraft 약품 믹서**: `Station_Mixer_F2/Vat`은 스크립트에 이름으로 참조되지 않는 순수 장식 메시라 가장 간단 — 자기 자신의 `MeshFilter`/`MeshRenderer`를 `Prop_IngredientMixer.fbx`(아직 `_NeedsCollider`에 프리팹이 없어 FBX를 직접 사용) 메시·머티리얼로 교체하고, 기존 `CapsuleCollider`(상호작용용)는 그대로 둠. 크기는 기존 비율 대신 균등 스케일 0.70으로 맞춰서 가루 층(`Layer0/1/2`)이 나타나는 높이 바로 아래에 믹서 입구가 오도록 배치(기존 Vat의 Y 위치와 거의 일치해 위치는 그대로 둠).

**검증 공통**: 전 과정 `Tools/Network/Validate Scene NetworkObjects` 문제 없음, 콘솔 에러 0건. 새로 배치한 모델들은 전부 비상호작용 장식(부모 스크립트가 애니메이션)이거나 이미 콜라이더가 있던 자리(Vat)라서 콜라이더를 새로 추가할 필요는 없었음.
**남은 일**: GIM-002 무대 자체는 이제 완료됐지만, 아직 안 쓴 VARCO 프리팹(`Prop_PulleyCrank`, `Prop_PuppetBGFrame` 등 — 대응하는 더미가 불명확하거나 우선순위 밖)과 2인 실제 플레이로 커튼·표시등·도르래·믹서의 최종 비주얼을 확인하는 일이 남음.

## GIM-004(온실 화분 퍼즐) Develop 통합 (2026-10-23)
KJH 브랜치의 GIM-004 구현을 머지해온 뒤(GIM-005는 지난 세션에 이미 통합돼 있어 그대로 확인만 함), KJH 쪽 CLAUDE.md의 "Develop 적용 방법" 그대로 `GIM004_Conservatory.prefab`(61.5, 0, -12.5), `GIM004_Journal.prefab`(62.55, 4.665, 3.5, 회전 y 180), `Gim004Sync.prefab`(최상위)을 Develop 씬 루트에 프리팹 인스턴스로 배치. 코드/프리팹은 수정하지 않음(GIM-002/003/005와 동일한 "KJH 프리팹 원본 그대로 쓰기" 원칙).

**검증**: `Validate Scene NetworkObjects` 문제 없음. Play 모드에서 `StartHost()` 호스트로 `Gim004Controller`가 Config 연결된 채 초기화되고 네트워크 시작 시 `LocalGim004Net`→`Gim004NetSync`로 정상 전환(`IsAuthority=True`) 확인. 정답 순서(D-E-A-F)대로 `RequestPlace()` 호출 → `Judging`→`Complete` 상태 전환 확인. 완료 후 `Door_Courtyard_L`/`Door_Courtyard_R`/`Door_MaidRoom` 세 문이 전부 `IsOpen=True`로 열리는 것까지 확인(Develop 고유의 이름 기반 문 참조가 올바르게 맞아떨어짐). Play 모드 종료 후 문이 다시 `IsOpen=False`로 돌아와 저장된 씬에 새지 않는 것도 확인. 콘솔 에러 0건.

**겸사겸사 확인한 것 — 버그 아님**: 씬을 저장할 때마다 `TitleCanvas/TitleRoot/ButtonPanel`의 버튼 3개(`StartButton`/`OptionsButton`/`QuitButton`) RectTransform이 git diff에서 0으로 찍혀서 처음엔 데이터 손상으로 의심했으나, `ButtonPanel`에 `VerticalLayoutGroup`이 붙어 있어서 그 자식들의 위치/크기는 Unity의 "driven RectTransform"(레이아웃이 매 로드마다 재계산)으로 분류돼 항상 0으로 직렬화되는 게 정상 동작이었음(실행하면 레이아웃이 다시 계산해 정상 위치로 보임). **앞으로 이 3개 버튼의 RectTransform이 diff에 0으로 나타나도 무시해도 된다** — 실제 버그가 아니라 Layout Group이 있는 모든 UI에서 생기는 정상적인 직렬화 방식.

**검증 한계**: 호스트 1개 프로세스에서 `RequestPlace()`를 직접 호출해 확인한 것 — 실제 두 사람이 1층에서 화분을 직접 들고 날라 선반에 꽂는 것, 2층에서 기록일지를 펼쳐 정답을 확인하는 것은 **다음 2인 플레이테스트에서 확인 필요**.

## Develop 전체 기믹 동작 검증 (2026-10-23, 열 번째 후속)
"기믹들이 KJH 씬에서처럼 Develop에서 똑같이 동작하는지" 전수 확인 요청. GIM-002/004/005는 과거에도 개별적으로 확인했지만, GIM-001(초상화)과 GIM-003(PotionCraft 약품 제조)은 이번이 Develop 기준 첫 전체 로직 통과 확인이었다. Play 모드 1회(호스트) 안에서 5개 기믹을 실제 판정 로직 호출로 순서대로 전부 끝까지 통과시켰다:

- **GIM-001**: `PortraitPuzzleScene.Instance.answer`({1,1,-1})에 맞춰 `PortraitPuzzle.Instance.RequestShift()`로 3개 액자를 맞춤 → `Solved=True`, `SolvedEvent` 발화 → GIM-002가 자동으로 `Locked→Preparing` 전환되는 것까지 확인(두 기믹이 Develop에서도 올바르게 연결돼 있음).
- **GIM-002**: 문 열기·태엽 감기 → 커튼 열기 → 3개 장면(`CurrentScene.doll/background/symbols`를 실시간으로 읽어 정답 그대로 입력) 전부 무대 정답→반주 정답→공연 통과 → `Complete, progress 3/3`.
- **GIM-003(PotionCraft)**: 실제 스테이션 체인(재료/물 획득 → Brewer 숙성 → Grinder 10회 갈기 → Mixer 투입)으로 정답 레시피(`FinalRecipeData.sequence = Red,Blue,Red`, 각각 `SilverLeaf+Red물`/`GreenLeaf+Blue물`)를 3배치 실행 → `PotionCraftSync.Completed=True`, `MixerStation.IsLocked=True`. 지름길(`ServerSetHeld`)을 쓰지 않고 공급대→숙성기→분쇄기 전 과정을 실제로 통과시켜 확인.
- **GIM-004**: 정답 순서(D-E-A-F)로 `RequestPlace()` → `Complete` → `Door_Courtyard_L/R`·`Door_MaidRoom` 3개 문 전부 `IsOpen=True`.
- **GIM-005**: 진짜 길 게이트(G1,G3,G6,G7)에 배정된 문양을 리플렉션으로 `Gim005Logic.SymbolOfGate()`에서 직접 읽어 `RequestPress()` → `RequestCenter()` → `Complete` → `Rail_StairFront` 비활성화 확인.

전 과정 콘솔 에러 0건(경고 4건은 기존에 알려진 오디오 리스너 중복 — 테스트 아티팩트, 기믹과 무관). Play 모드 종료 후 씬 `isDirty=False`로 깨끗이 복구되어 테스트 상태가 저장된 씬에 새지 않음을 재확인.

**검증 한계는 그대로**: 전부 호스트 1개 프로세스에서 판정 메서드를 직접 호출해 확인한 것이라, 실제 E키 입력 경로·두 사람의 역할 분담(GIM-005 A/B, PotionCraft 손 나르기 등)·공간을 실제로 걸어서 통과하는 것은 여전히 **다음 2인 플레이테스트에서 확인 필요**.

## 타이틀 화면이 안 보이던 버그 수정 + 전체 캔버스 점검 (2026-10-23, 열한 번째 후속)
"에디터 플레이 시 타이틀 화면에 아무것도 안 보인다"는 보고를 받고 원인을 찾음.

**원인**: `GameFlowUI.cs`가 `GameFlowSync.Instance`를 찾자마자(이건 `Awake()`에서 즉시 설정되고 네트워크 세션 시작과 무관) `sync.Stage`를 바로 읽어 화면에 반영하는데, `Stage`는 `NetworkVariable<GameStage>`라서 **스폰되기 전에도** 선언 시 기본값인 `Lobby`를 그대로 돌려준다. 그 결과 Play 버튼만 눌러도(호스트/참가를 누르기도 전에) `lobbyCanvas`(불투명 전체화면 배경, 여덟 번째 후속에서 추가)가 곧바로 켜져서 `TitleCanvas`를 완전히 덮어버리고 있었다. `GameFlowSync`가 실제로 네트워크 세션에 참여한 적이 없으면(`IsSpawned=false`) `Stage` 값 자체가 아직 아무 의미가 없다는 걸 놓친 게 원인.

**수정**: `GameFlowUI.Update()`에서 `sync.IsSpawned`가 true일 때만 `Stage`를 반영하도록 가드 추가(`Assets/Scripts/KSM/GameFlow/GameFlowUI.cs`). 즉 타이틀 화면 단계(세션 시작 전)에는 어떤 GameFlow 캔버스도 건드리지 않고, 실제로 호스트/참가해서 `GameFlowSync`가 스폰된 뒤부터만 Lobby/Intro/Playing/Ending/Result 전환이 반영된다.

**검증**: Play 모드 진입 직후(아무것도 안 누른 상태) `LobbyCanvas.activeSelf=False`, `TitleCanvas.activeSelf=True` 확인(수정 전엔 `LobbyCanvas=True`였음). `StartHost()` 후에는 `GameFlowSync.IsSpawned=True`, `Stage=Lobby`, `LobbyCanvas.activeSelf=True`로 정상적으로 대기방이 뜨는 것도 확인. `stage` NetworkVariable을 리플렉션으로 직접 돌려가며 Lobby→Intro→Playing→Ending→Result→Lobby 전 구간의 캔버스 전환(각 단계에서 해당 캔버스만 켜짐, 커서는 Playing일 때만 잠김)이 전부 정상임을 확인. `EventSystem` 1개, 정상 활성화. 콘솔 에러 0건, Play 모드 종료 후 씬 `isDirty=False`.

**겸사겸사 확인한 것 — 버그 아니라 오래된 문서 미정리**: "모든 캔버스 점검" 과정에서 `ClueViewerUI`/`InteractionPrompt`/`PhotoInventoryPanel`/`EchoBlackout`/`SpectrumOverlay` 등 2026-10-06 라운드들에 기록된 UI가 현재 씬에 전혀 없는 것을 발견 — `git log`로 추적한 결과 `164d8d4` 커밋("Develop, KSM 초기화", 2026-10-08)에서 당시 `FPSPlayer.prefab`·`NetworkInteractionBridge.cs`·`NetworkFirstPersonController.cs` 등 KSM 프로토타입 플레이어 체계 전체를 LGH의 `NetworkPlayer`/`PlayerInteractor`(OnGUI 기반 HUD) 체계로 완전히 교체하면서 함께 삭제된 것이었다. 이번 세션에서 생긴 손실이 아니라 2주 넘게 이어진 상태이며, CLAUDE.md의 "Develop 씬 통합 현황"·"KJH 기능 전면 포팅"·"KJH 추가 머지 반영" 세 섹션은 그 이전 아키텍처 기준의 과거 기록이라 현재와 안 맞는다. **당장 동작에 영향은 없음**(그 코드/오브젝트를 참조하는 곳이 전혀 없어 컴파일·런타임 에러 0건) — 다만 문서 정리가 필요하면 별도로 처리할 것.

## 방 만들기 화면에서 타이틀로 돌아가는 버튼이 사라지던 버그 수정 (2026-10-23, 열두 번째 후속)
"방 만드는 화면에서 타이틀로 돌아가는 버튼이 없다"는 보고. 원인 조사 중 `LGH/MultiplayerMenu.cs`가 이 세션의 CLAUDE.md 기록(2026-10-02 KSM 단독 프로토타입 기준)보다 훨씬 발전해 있다는 걸 발견 — 지금은 Unity Multiplayer Services의 Sessions API로 방 목록 조회·방 만들기·방 이름으로 참가·코드로 참가를 전부 지원하는 완성된 로비 화면이다(이 발전 과정 자체는 이 세션 밖에서 있었던 일이라 CLAUDE.md에 별도 기록 없음 — "네트워킹 세팅 현황" 섹션도 그 이전 단계 기준이라 낡았다는 점 참고).

**원인**: `Assets/Scripts/KSM/UI/MultiplayerBackButton.cs`의 표시 조건이 `NetworkPlayer.Local == null && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)`이었다. 그런데 Sessions API로 "방 만들기"를 누르면 `MultiplayerService.Instance.CreateSessionAsync()`가 Relay 연결까지 전부 처리하면서 `NetworkManager.IsListening`이 **플레이어가 실제로 스폰되기 한참 전에** 먼저 True가 돼버린다 — 그 결과 "방을 만드는 중.../방을 만들었어요, 참가 코드: ..." 화면(아직 `NetworkPlayer.Local`은 없어서 `MultiplayerMenu` 자신은 계속 이 화면을 그리고 있음)에서 뒤로가기 버튼 조건의 `!IsListening`만 거짓이 돼 버튼이 먼저 사라져 있었다.

**수정**: 표시 조건에서 `NetworkManager.IsListening` 체크를 제거하고 `MultiplayerMenu` 자신이 "인게임"을 판단하는 기준과 똑같이 `NetworkPlayer.Local == null`만 본다 — 방 목록/만들기/참가 중 화면이 떠 있는 동안(플레이어가 실제로 스폰되기 전까지)은 계속 보이고, 스폰되는 순간(대기방/일시정지 화면으로 바뀌는 시점)에만 사라진다. 또한 `OnBackClicked()`가 예전엔 `TitleMenuUI.ShowTitle()`만 불러서 이미 만들어진 세션이 정리 안 된 채 방 목록에 남거나 `NetworkManager`가 켜진 채로 남을 수 있었던 것도 같이 고침 — `ResultCanvasController`가 쓰던 것과 같은 패턴(`LGH.MultiplayerMenu.LeaveRoomAsync()`로 세션 정리 → 타이틀 표시)을 적용하고 연타 방지 플래그를 추가함.

**검증**: Play 모드에서 타이틀 "시작" → 멀티플레이 메뉴 활성화 상태에서 뒤로가기 버튼 활성(`NetworkPlayer.Local==null`) 확인 → `NetworkManager.StartHost()`로 플레이어가 스폰된 뒤에는 버튼이 자동으로 꺼지는 것 확인(기존처럼 올바르게 인게임으로 판단) → 호스트를 종료한 뒤 `OnBackClicked()`를 직접 호출해 세션 정리 로직이 예외 없이 끝까지 돌고 타이틀 화면(`TitleRoot.activeSelf=True`, 멀티플레이 메뉴 `enabled=False`)으로 정상 복귀하는 것 확인. 콘솔 에러 0건, Play 모드 종료 후 씬 `isDirty=False`.

## 방을 나가면 타이틀 버튼이 가려지던 버그 수정 — 직전 수정의 부작용 (2026-10-23, 열세 번째 후속)
"방을 만들었다가 나오면 타이틀로 가는 버튼이 없어진다"는 보고. 직전(열한 번째 후속) "타이틀 화면이 안 보이던 버그" 수정이 만든 **새 부작용**이었다.

**원인**: 그때 고친 `GameFlowUI.Update()`는 `sync.IsSpawned`가 false면 그냥 `return`하도록만 했다 — "아직 스폰 안 됨(세션 시작 전)"과 "스폰됐다가 다시 해제됨(방 나가기 등으로 세션 종료)"을 구분하지 않은 것. 그 결과 호스팅 중 켜졌던 `lobbyCanvas`(불투명 전체화면 배경)가 방을 나가 `GameFlowSync`가 스폰 해제된 뒤에도 꺼지지 않고 그대로 남아 화면을 계속 가렸다. `MultiplayerBackButton`의 뒤로가기 버튼 자체는 `activeSelf=True`로 멀쩡했지만 그 위에 깔린 `lobbyCanvas`(sortOrder가 더 높음)에 완전히 가려져 안 보이는 것이었다.

**수정**: `GameFlowUI.cs`에 `HideAll()`을 추가 — `sync.IsSpawned`가 false로 바뀐 걸 감지하면(`shown != -1`일 때) 4개 캔버스를 전부 끄고 커서도 풀어서 `shown`을 "아직 반영 안 함" 상태로 리셋한다. 이러면 다음에 새로 호스팅/참가할 때 `Apply()`가 Stage를 처음부터 다시 반영한다.

**검증**: Play 모드에서 호스팅(`LobbyCanvas` 켜짐) → `MultiplayerMenu.LeaveRoomAsync()` 호출(실제 "방 나가기" 버튼과 동일 경로) → `LobbyCanvas.activeSelf=False`로 꺼지고 뒤로가기 버튼이 정상적으로 다시 보임, 커서도 풀림을 확인. 같은 세션에서 다시 호스팅해도(`StartHost()` 두 번째 호출) `LobbyCanvas`가 다시 정상적으로 켜지는 것까지 확인(재진입 경로도 안전). 콘솔 에러 0건, Play 모드 종료 후 씬 `isDirty=False`.

**교훈**: `IsSpawned` 같은 "지금 상태"만 보는 가드를 추가할 땐 "거짓이 됐을 때 이전에 켜 둔 것들을 정리하는 경로"도 같이 챙겨야 한다 — 한쪽 방향(꺼져있다가 켜짐)만 고치면 반대 방향(켜져있다가 꺼짐)에서 새 버그가 난다.

## 기믹 더미 → VARCO 모델 교체 2차 (2026-10-23, 열네 번째 후속)
"기믹 오브젝트들에 해당하는 모델이 있다면 교체"하는 작업. VARCO 3D 전체(특히 `Assets/VARCO 3D/KWJ/Prefabs/Gimmick`, 지금까지 전혀 안 쓰인 "기믹 전용" 폴더)와 `_NeedsCollider` 폴더를 전수 조사해 현재 원시도형인 기믹 오브젝트와 짝지을 수 있는 모델을 찾아 매핑을 제안하고 승인받아 진행함.

**중요하게 확인한 패턴 — KWJ 모델의 좌표계**: 이 폴더의 FBX는 대부분 루트(또는 "Model" 자식)에 `(270,0,0)` 임포트 보정 회전이 구워져 있다. 대부분의 소품(서서 놓이는 병·절구공이·펜던트·받침대 등)은 이 보정 회전을 그대로 쓰면 자연스럽게 "세워진" 모습이 된다. 다만 **원본 메시 자체가 이미 Y축이 얇은(평평하게 눕는) 모델**(은잎사귀 `Prop_SilverLeaf`, 조리법 책 `Prop_RecipeBook`)은 거꾸로 **회전 없이(identity)** 써야 평평하게 눕는다 — 이 둘을 반대로 적용하면 옆으로 선 채로 렌더링된다(실제로 한 번 틀렸다가 밝은 곳에 임시로 복제해 눈으로 확인하고 고침). 액자처럼 "벽에 거는" 특수 상황(GIM-001)은 추가로 90°대각 축 치환 회전이 더 필요했다(아래 참고).

**적용한 교체** (전부 기존 플레이스홀더 오브젝트에 자식 "Model"을 추가하는 방식 — 부모의 트랜스폼·콜라이더·스크립트 참조는 전혀 건드리지 않음, Dumbwaiter 때와 같은 원칙):
- **GIM-001 초상화 퍼즐**: 액자 6개(`Portrait_E0~E2,W0~W2`)와 떨어지는 액자(`Portrait_E3`) 전부 `Prop_PortraitFrame.fbx`로 교체. 이 모델은 "눕혀서" 모델링돼 있어 원본 임포트 보정(270,0,0) 위에 추가로 120° 대각축 치환 회전(`AngleAxis(-120, (1,1,1))`)을 더해야 "벽에 거는" 방향이 나온다 — `PortraitTilt.cs`가 "로컬 X축 = 벽 안쪽"이라고 가정하고 그 축으로 기울이기 때문에, 부모 오브젝트의 회전/콜라이더는 절대 손대지 않고 자식 모델의 회전만으로 보정함(기울이기 애니메이션은 자식이 부모를 따라가므로 그대로 정상 동작).
- **GIM-002 `Gim002_Lamp_Ready` 프리팹**: 렌즈 2개(Lens_L/Lens_R, Cube)를 지우고 `Prop_SuccessLightBulb.fbx` 하나로 교체, `Gim002IndicatorLamp.bulb` 참조를 새 렌더러로 재연결하고 `extraBulbs`는 비움(기존엔 두 렌즈를 같이 밝혔지만 이제 전구 하나). `MaterialPropertyBlock` 색칠 테스트로 점등 로직이 그대로 동작함을 확인(프리팹 자산을 직접 고쳐 모든 씬 인스턴스에 전파).
- **도르래 바퀴(`Dumbwaiter_Rig/PulleyWheel/Wheel`)**: 남아있던 원시 Cylinder를 `Prop_PulleyCrank.fbx`로 교체. 바퀴는 `PulleyWheel` 부모가 Z축으로 회전해 "돈다"를 표현하므로, 새 모델의 축면(정면)이 부모 로컬 Z를 보도록(`Wheel` 자체는 identity, 자식 Model만 임포트 보정 270 적용) 맞춤 — 기존엔 축 방향이 X였던 것을 Z로 바로잡아 오히려 더 올바른 회전처럼 보이게 됨.
- **GIM-003 PotionCraft**: 재료 3종(`Prop_SilverLeaf`=평평 눕힘, `Prop_GreenLeaf`/`Prop_BlueFlower`=임포트 보정만), 물 3종(`Prop_Bottle_Red/Blue/Green`), 분쇄기 사발→`Prop_Mortar`·공이→`Prop_Pestle`, 덩이(Brewer·Grinder 2곳 전부)→`Prop_GrayLump`, 조리법 책(`RecipeBook_F2`, Book+Pages 두 조각 → 책 모델 하나로 통합)→`Prop_RecipeBook`(평평 눕힘), 책/메모 받침대 2곳(`RecipeBook_F2`·`RecipeNote_F1`의 Stand)→`Furn_NotePedestal`.
- **교체 안 함(마땅한 모델 없음, 승인받은 범위 밖)**: 재료 RedRoot, 숙성기(Brewer) 솥/액체, 쓰레기통(TrashStation), 가루 더미(`Tint_Pile`)·공급대 상자(Crate/Shelf) — 특히 가루 더미는 `PotionVisuals.TintMarked`가 이름이 "Tint"로 시작하는 렌더러를 런타임에 아무 색으로나 칠하는 구조라서, 특정 색으로 구워진 모델(`Prop_PowderDish_*`)로 바꾸면 다른 색일 때 안 맞아 보류함. GIM-004 화분 몸체·GIM-005 산울타리는 지난 조사에서 이미 "마땅한 모델 없음"으로 확인된 그대로 유지.

**검증**: 각 교체마다 밝은 곳에 임시 조명+복제본을 놓고 눈으로 방향을 확인(도르래 샤프트처럼 원래 어두워서 촬영이 안 되던 곳은 수치 검증 + 별도 밝은 곳 복제본 확인으로 대체, 기존에 문서화된 한계와 동일). `Tools/Network/Validate Scene NetworkObjects` 문제 없음, 콘솔 에러 0건(교체 작업 자체에서 발생한 에러 없음 — 세션 중 도구 사용 실수로 찍힌 과거 에러 3건은 이 작업과 무관). 씬 저장 완료.

## 기믹 더미 → VARCO 모델 교체 3차 — GIM-002 인형 4체 + 기록일지 (2026-10-23, 열다섯 번째 후속)
직전 턴에서 "여전히 더미인 오브젝트"를 전수 스캔(모든 MeshFilter를 뒤져 builtin Cube/Sphere/Cylinder/Capsule/Quad 사용처를 기믹별로 나열)한 결과를 바탕으로 두 가지를 마저 진행함.

- **GIM-002 인형 4체**: `DOLL_FrogCan`/`DOLL_OwlBook`/`DOLL_CatViolin`/`DOLL_OwlScope`(전부 Capsule+Sphere 조합 더미)를 VARCO `_NeedsCollider` 폴더의 완성된 손인형 모델로 교체 — `Prop_PuppetFrog`(개구리+물뿌리개 일체형), `Prop_PuppetOwlBook`, `Prop_PuppetCatViolin`, `Prop_PuppetOwlTelescope`. 이 넷은 캐릭터+소품이 이미 하나로 합쳐진 완성품이라 기존 "Body" 자식을 통째로 지우고 프리팹을 그대로 인스턴스화하기만 하면 됐다(내부 회전은 프리팹 자체에 이미 올바르게 박혀 있음). 피벗이 중심이라 바닥에 닿도록 로컬 Y를 절반 높이만큼(0.15 또는 0.125) 띄워줬다.
  - **검증 중 헷갈렸던 점**: 밝은 곳에 복제해서 처음 봤을 때 개구리 인형이 "거꾸로"처럼 보였는데, 더 줌아웃해서 보니 실제로는 동그란 몸통 위에 손을 넣는 소매/커프스가 있는 정통 **장갑 인형(글러브 퍼펫)** 디자인이었다(얼굴이 뚜렷한 치비 인형이 아니라 실제 인형극 손인형 모양이라 달라 보였을 뿐). 부엉이 쪽은 부리가 위를 향한 게 명확히 보여서 방향이 올바르다는 걸 확인함 — **회전 보정 없이 identity 그대로 쓰는 게 맞았음**.
- **GIM-004 기록일지(`Gim004_JournalVisual.prefab`)**: Cover+Pages+PhotoMark+LineMark(전부 장식용 가짜 잉크 자국, `Gim004Builder.cs`가 굽는 정적 장식일 뿐 런타임 참조 없음을 코드로 확인) 전부 지우고 `Prop_RecipeBook` 하나로 교체.
- **GIM-004 선반(`OBJ_CENTER_SHELF`)·화분 받침대(`OBJ_POT_BENCH`)는 보류로 정정**: 직전 제안("Furn_WallShelf"/"Furn_Planter"로 교체)이 실측 없이 나온 성급한 추천이었음을 발견 — 실제 크기가 선반 2.9m, 받침대 7.3m로 VARCO 가구 소품(전부 2m 이하)보다 훨씬 길어서 1개로는 어림도 없고, 여러 개를 이어붙이는 건 배경 소품치고 과한 작업이라 판단해 보류함. 현재의 각목 조립형 원시도형 구조(도르래 Shell 벽체와 같은 성격)를 그대로 유지하는 게 더 합리적.

**검증**: `Tools/Network/Validate Scene NetworkObjects` 문제 없음, 콘솔 에러 0건(세션 중 과거 도구 사용 실수 3건만 남아있고 신규 에러 없음). 씬 저장 완료.

## PotionCraft 소지품을 오른손 대신 왼손에 들도록 수정 (2026-10-23, 열여섯 번째 후속)
"주방에서 재료를 집을 때 촛불을 든 오른손 말고 왼손으로 들게 해달라"는 요청.

**확인한 내용**: `PotionCraftSync.cs`의 `BuildHeldVisual()`이 집은 재료/물건의 모양을 카메라 자식으로 띄우는데, 로컬 플레이어 기준 위치가 `(0.3, -0.28, 0.6)`로 **양수 X(오른쪽)** — 촛불이 이미 쓰고 있는 자리였다. 같은 프로젝트의 `Assets/Scripts/LGH/PistolLoader.cs`에도 "the pistol is in your (left) hand" 주석과 함께 음수 X 좌표를 쓰고 있어서, 이 프로젝트의 관례가 **오른손 = 양수 X(촛불), 왼손 = 음수 X(그 외 손에 드는 것)** 임을 재확인함.

**수정**: `PotionCraftSync.BuildHeldVisual()`의 로컬 플레이어용 위치를 `(0.3, -0.28, 0.6)` → `(-0.3, -0.28, 0.6)`으로, 다른 플레이어가 보는 위치도 `(0.25, 1.15, 0.5)` → `(-0.25, 1.15, 0.5)`로 좌우 반전. 재료/물/덩이/가루 전부 이 한 함수를 거치므로 PotionCraft에서 손에 드는 모든 것에 적용됨.

**검증**: Play 모드에서 `StartHost()` → 재료 집기 → `pi.viewCamera` 자식의 `Held_*` 오브젝트 `localPosition`이 `(-0.30, -0.28, 0.60)`로 정확히 왼쪽에 생기는 것을 확인. 콘솔 에러 0건.

## GIM-002 열쇠를 손에 들지 않고 인벤토리로 줍게 변경 (2026-10-23, 열일곱 번째 후속)
"열쇠류를 습득 시 인벤토리에 들어가도록 하고 사용에 문제가 없도록" 요청.

**조사 결과 — 이미 완성된 공용 인벤토리 시스템이 있었음**: `LGH.PlayerInventory`(소지품 창, [I]키)·`LGH.ItemData`/`ItemDatabase`(아이템 정의, Resources의 `ItemDatabase_LGH`)·`LGH.WorldItem`(바닥에 놓인 아이템, 보기+E로 줍기)·`LGH.ItemNetworkSync`(호스트 권한으로 줍기/버리기 RPC 처리)로 구성된 "키·편지·동전" 전용 시스템이 이미 있었고, 실제로 놋쇠 열쇠(`Item_BrassKey`)·다락방 열쇠(`Item_AtticKey`) 등으로 이미 쓰이고 있었다. GIM-002의 두 열쇠(`Gim002KeyItem`)만 예전 방식(공용 들기 시스템 `HoldableItem`, 손에 들고 다님)을 쓰고 있어서 이 기존 인벤토리로 갈아끼우는 작업이었다.

**변경**:
- `Gim002KeyData.cs`: `LGH.ItemData inventoryItem` 필드 추가 — 열쇠 정보 에셋 하나에 인벤토리 아이템까지 같이 연결해서, 어느 쪽(WorldItem/인벤토리 조회)도 따로 안 맞춰도 되게 함.
- `Gim002KeyItem.cs`: `HoldableItem` 상속을 버리고 `[RequireComponent(typeof(LGH.WorldItem))]`를 쓰는 얇은 컴포넌트로 교체. "보기+E로 줍기"는 전부 `WorldItem`/`PlayerInteractor`/`ItemNetworkSync`가 전담하고, 여기서는 `keyData`만 들고 있다가 `Awake()`에서 `worldItem.item = keyData.inventoryItem`로 연결. GIM-001 전에는 못 줍게 하는 로직은 `HoldableItem.CanPickUp` 대신 매 프레임 콜라이더를 껐다 켜는 방식으로 재구현(조준 자체가 안 되게).
- `Gim002KeyTarget.cs`: `PlayerHand.LocalHeld`(손에 든 것) 체크를 `LGH.PlayerInventory.Local.HasItem(...)`(인벤토리 보유 여부)로 교체. 사용 시 `key.Consume()` 대신 `LGH.ItemNetworkSync.RequestConsume(...)`로 인벤토리에서 1개 소비. **주의**: `Assets/Scripts/KJH/KeySafe/PlayerInventory.cs`(다른 미니퍼즐용, 네임스페이스 없음)와 이름이 겹쳐서 `using LGH;`만으로는 전역 네임스페이스 쪽이 먼저 잡혀 컴파일 에러가 남 — 반드시 `LGH.PlayerInventory`로 완전한 이름을 써야 한다(주석으로 남겨둠).
- `LGH/ItemNetworkSync.cs`: `RequestConsume(PlayerInventory, ItemData, count)` 공개 메서드 + `ConsumeRpc` 추가. 인벤토리 쓰기는 서버만 할 수 있어서(기존 Pickup/Drop과 같은 이유), 게스트가 열쇠를 써도 항상 서버에서 제거되도록 하는 공용 경로.
- `Gim002Builder.cs`: `HookKey()`가 이제 `WorldItem`도 같이 붙이고 `item` 필드를 연결하도록 갱신(기존에 쓰던 `holdOffset` 설정은 그 필드 자체가 없어졌으므로 제거) — KJH님이 나중에 Build All을 다시 돌려도 안 깨지게.
- 인벤토리 아이템 신규 생성: `Assets/Objects/LGH/Items/Item_Gim002MusicKey.asset`(`gim002_music_key`), `Item_Gim002StageKey.asset`(`gim002_stage_key`) — 기존 `ItemDatabase_LGH`(인덱스 0~5, 네트워크로 인덱스째로 오가므로 절대 끼워넣지 않고 끝에 추가) 6·7번에 등록. `Gim002Key_MusicRoom.asset`/`Gim002Key_Windup.asset`의 새 `inventoryItem` 필드에 각각 연결.

**발견해 같이 고친 더 근본적인 문제**: Develop 씬에는 애초에 `ItemNetworkSync`가 어디에도 없어서(`Instance`가 `null`), 이 인벤토리 시스템 자체가 Develop에서 전혀 작동할 수 없는 상태였다. 원인은 `DoorSync_LGH.prefab`(Door+Candle 동기화를 묶어 둔 공용 프리팹) 자체에는 `ItemNetworkSync`가 없고, LGH님이 본인 씬(`LGH.unity`)에서 같은 오브젝트에 **프리팹에 반영 안 하고 직접** `ItemNetworkSync`·`LockBoxNetworkSync`를 추가해 테스트만 해 둔 상태였기 때문. GIM-002 열쇠와 직접 관련 있는 `ItemNetworkSync`만 `DoorSync_LGH.prefab` 자산에 추가해 모든 인스턴스(Develop 포함)에 전파되게 했다(`LockBoxNetworkSync`는 별개 기믹이라 범위 밖으로 남겨둠).

**검증**: Play 모드에서 `StartHost()` → GIM-001 해결 → 두 열쇠 모두 `ItemNetworkSync.RequestPickup()`으로 주움 → `PlayerInventory.Local.HasItem()` true 확인 → 각 사용처에서 `CanInteract=True`, 프롬프트 "[E] 음악실 열쇠 사용"/"[E] 태엽 열쇠 사용" 정상 표시 → `Interact()` 호출 → 인벤토리에서 소비(`HasItem` false로 복귀) + `musicDoorOpened`/`stageWound` 둘 다 true + 문이 실제로 열림(`door.IsOpen=True`) + GIM-002 상태가 `CurtainClosed`로 정상 전환까지 전부 확인. `Tools/Network/Validate Scene NetworkObjects` 문제 없음, 콘솔 에러 0건, 씬 저장 후 `isDirty=False`.

**검증 한계**: 전부 메서드 직접 호출로 확인한 것이라 실제 E키로 줍고 쓰는 입력 경로, 그리고 인벤토리 창([I])을 직접 열어 눈으로 보는 것은 다음 실제 플레이로 확인 필요(이 환경의 기존 한계와 동일).

## GIM-007(함께 미는 안방 가구)·GIM-008(다락 별 상자 보상) Develop 통합 (2026-10-23, 열여덟 번째 후속)
KJH님이 GIM-007·GIM-008을 KJH 브랜치에 구현·배치하고 Develop에 머지(`f5b5f63`)해온 뒤, 팀장님 안내 그대로 KJH 쪽 CLAUDE.md의 "Develop 적용 방법"을 따라 네 프리팹을 Develop 씬 루트에 배치: `GIM007_MasterBedroom.prefab`(31, 3.9, 12.9, 회전 y 90), `Gim007Sync.prefab`(최상위), `AtticTreasure_Contents.prefab`(26.088, 9.115, 11.879, 회전 y 270), `AtticTreasureSync.prefab`(최상위). 코드/프리팹 자체는 수정하지 않음(기존 기믹들과 동일한 "KJH 프리팹 원본 그대로 쓰기" 원칙). 배치 전 `Door_Master_Dressing`을 다른 기믹이 잠그거나 열고 있지 않은지 확인(문제 없음 — GIM-004는 `Door_Courtyard_L/R`·`Door_MaidRoom`만 건드림).

**머지 충돌로 사라진 GIM-002 열쇠 아이템 발견 — 복구함**: 배치 전 `ItemDatabase_LGH.asset`을 확인해보니, 이번 머지의 충돌(`# Conflicts: Assets/Objects/LGH/Resources/ItemDatabase_LGH.asset`) 해결 과정에서 내 직전 작업(GIM-002 열쇠 인벤토리화, 열일곱 번째 후속)이 추가한 `Item_Gim002MusicKey`/`Item_Gim002StageKey`(인덱스 6·7)가 통째로 사라지고 그 자리를 GIM-008의 `Item_GoldStatue`/`Item_FrontKey`로 덮어쓴 상태였다 — 즉 두 리스트를 합치는 대신 한쪽이 다른 쪽을 지워버린 머지 사고. 아이템 에셋 파일 자체는 남아 있어서(`Assets/Objects/LGH/Items/Item_Gim002*.asset`) `ItemDatabase.IndexOf()`가 더 이상 찾지 못해 GIM-002 열쇠의 줍기·사용이 조용히 깨지는 상태였다. **기존 인덱스(0~7)는 절대 건드리지 않고 끝(8·9번)에 다시 추가**해서 복구(네트워크가 인덱스로 아이템을 주고받으므로 끼워넣기 금지 원칙 유지).
- **앞으로 규칙**: `ItemDatabase_LGH.asset`에서 머지 충돌이 나면, 자동 병합이 양쪽이 추가한 항목을 하나씩 다 지켰는지 직접 확인할 것 — `DefaultNetworkPrefabs.asset` 충돌 때와 같은 유형의 함정(목록형 에셋은 자동 병합이 한쪽을 조용히 덮어쓸 수 있다)이다.

**검증**: Play 모드 `StartHost()` 단일 호스트로 GIM-007·GIM-008 각각 끝까지 확인.
- GIM-007: `Gim007Controller.Instance`가 Config 연결된 채 초기화되고 `Gim007NetSync`로 정상 전환(`IsAuthority=True`) 확인. `Gim007NetSync`의 private `authority`(`LocalGim007Net`) 필드를 리플렉션으로 꺼내 두 손잡이(호스트+가짜 클라이언트 ID)를 잡히고 라운드마다 `ReportAs(..., true)`로 8단계 전진시켜 `Complete`까지 도달 — `Door_Master_Dressing`과 그 짝문이 실제로 열리고 열린 채로 잠기는 것(`IsOpen=True`, `locked=True`)까지 확인.
- GIM-008: `AtticTreasureSync.Instance` 스폰 확인 → 현관 열쇠(`AtticTreasureKey.Interact()`) → 인벤토리로 바로 들어감 확인 → 황금상(`AtticTreasureStatue.Interact()`) → 연출(`Cutscene`) 재생 후(≈3초 대기) 인벤토리에 들어가고 `State=Taken`으로 전환 확인 → `Door_Front_L.Interact(..., inventory)`로 현관 열쇠를 써서 양문이 실제로 열리는 것(`IsOpen=True`)까지 확인. GIM-002 키 인덱스 복구가 실제로 유효한지 GIM-001을 `PortraitPuzzle.RequestShift()`로 직접 풀어 재확인 — 음악실 열쇠·태엽 열쇠 모두 다시 줍고(`ItemNetworkSync.RequestPickup`) 사용(`Gim002KeyTarget.Interact()`)해 `musicDoorOpened`/`stageWound` 둘 다 true, 문 잠금 해제까지 정상 동작 확인.
- `Tools/Network/Validate Scene NetworkObjects` 문제 없음, 콘솔 에러 0건(기존 오디오 리스너 중복 경고만 있음). Play 모드 종료 후 `git diff`로 저장된 씬 파일을 직접 확인해 **새 프리팹 인스턴스 4개 추가 외에 기존 오브젝트의 직렬화 값이 전혀 바뀌지 않았음**을 확인(문 잠금 상태 등은 씬에 직렬화되는 값이 아니라 각 기믹 컨트롤러가 Start/Update에서 매번 다시 적용하는 런타임 전용 상태라서, Play 테스트 중 바뀐 값이 Stop 후에도 메모리에 남아 있어도 저장 파일에는 반영되지 않음 — 다만 이 가정에 의존하지 말고 저장 후에는 항상 `git diff`로 실제 반영 범위를 확인할 것).

**발견했지만 이 시점엔 손대지 않은 것 — LGH 쪽 사전 존재 이슈**: `AtticTreasureBoxItem`이 상자를 찾을 때 쓰는 `LGH.StarLockBox`(별 자물쇠 상자) 컴포넌트가 **Develop 씬에는 전혀 없음**(`LGH.unity`/`KJH.unity`에는 있음 — `ItemNetworkSync`가 `DoorSync_LGH.prefab`에 안 들어가 있던 것과 같은 유형의 "LGH 씬 전용, 공유 프리팹에 안 올라간 컴포넌트"). `AtticTreasureBoxItem.BoxOpen`은 상자를 못 찾으면 `box == null || box.IsOpen`이 자동으로 `true`가 되도록 짜여 있어서(안전한 기본값) **Develop에서는 지금 별자물쇠 퍼즐을 풀지 않아도 황금상·현관 열쇠를 바로 주울 수 있는 상태**다 — 막히는 버그는 아니지만, 원래 기획대로라면 별자물쇠를 먼저 풀어야 하는 게이트가 Develop에서는 사실상 없는 셈. GIM-008 코드 자체가 아니라 LGH 쪽 맵 프리팹 문제라 범위 밖으로 남겨둠 — LGH님 확인 필요. **→ 바로 다음 요청으로 실제로 반영함, 아래 참고.**

## 다락 별 상자(StarLockBox_LGH) Develop 반영 (2026-10-23, 열아홉 번째 후속)
바로 위에서 발견만 하고 남겨뒀던 문제를 "Develop에 상자 반영해주세요" 요청으로 실제로 고침. 확인해보니 `ItemNetworkSync` 때와는 달리 이건 "씬에만 있고 프리팹엔 없는 컴포넌트"가 아니라, **원래 공유 프리팹으로 존재하는데 그냥 Develop에 아직 안 놓여 있었을 뿐**이었다(`Assets/Objects/LGH/Props/StarLockBox_LGH.prefab`, guid `e19b983cdc6123d4083041051d02f868`) — KJH.unity/LGH.unity 둘 다 이 프리팹의 인스턴스를 씬 루트에 두고 있었다.

- KJH.unity의 PrefabInstance 오버라이드(YAML `m_Modifications`)를 직접 읽어 위치·회전·`telescope` 연결을 그대로 재현: 위치(26.088, 9.115, 11.879)·회전 y=270은 이미 둔 `AtticTreasure_Contents.prefab`과 완전히 같은 자리(상자 겉모습과 그 속 보상이 같은 위치에 겹쳐 있는 구조). `telescope` 필드는 KJH 씬에서 `Telescope_LGH`(45.00, 7.84, 14.99)를 가리키고 있었고, Develop에도 같은 이름·같은 위치의 `Telescope_LGH`가 이미 있어서 그대로 연결(KJH.unity를 additive로 같이 열어 실제 값을 확인한 뒤 저장 없이 닫음 — 기존 확립된 방법 그대로).
- `revealOnOpen`(열리면 나타나는 오브젝트) 슬롯 2개는 KJH 쪽도 비어 있어서 그대로 빈 채로 둠 — 보상 겉모습은 `AtticTreasureBoxItem`이 따로 관리하므로 필요 없음.
- `LGH.StarLockBox`는 `NetworkBehaviour`가 아니라 평범한 `MonoBehaviour`라서(네트워크 동기화는 별도의 `LockBoxNetworkSync`가 담당) `DefaultNetworkPrefabs` 등록이나 `Validate Scene NetworkObjects` 대상이 아님 — 프리팹 인스턴스만 놓으면 끝.
- **`LockBoxNetworkSync`는 이 시점엔 반영하지 않음**: 확인해보니 이 컴포넌트는 LGH님 본인 씬(`LGH.unity`)에만 있고 KJH.unity에도 없다(즉 GIM-008을 만들고 테스트한 KJH 씬조차 별 상자는 아직 네트워크 동기화 없이 로컬로만 동작하는 상태) — `StarLockBox.Press()`가 `LockBoxNetworkSync.Instance`가 없으면 자동으로 로컬 판정(`Apply()`)으로 넘어가도록 짜여 있어서 지금도 동작은 하지만, 두 사람이 접속한 네트워크 게임에서는 각자 화면이 따로 노는(동기화 안 되는) 상태일 수 있다 — LGH님이 본인 씬에서 다듬고 있는 중인 것으로 보여 이번에도 건드리지 않음. **→ 바로 다음 요청으로 가져옴, 아래 참고.**

**검증**: Play 모드 `StartHost()`에서 `StarLockBox.IsOpen=False`일 때 `AtticTreasureBoxItem.CanInteract`가 둘 다 `False`로 막히는 것 확인(이전까지는 상자가 없어서 항상 `True`였음) → `box.Code`(텔레스코프 별 개수 그대로, [3,5,7,4])를 읽어 각 사분면을 `Press()`로 맞추고 버튼(`Press(4)`) → `IsOpen=True` → 두 아이템 `CanInteract=True`로 바뀜 → 현관 열쇠 `Interact()` → 인벤토리에 정상 추가까지 확인. `Tools/Network/Validate Scene NetworkObjects` 문제 없음, 콘솔 에러 0건, 저장 후 `git diff`로 새 프리팹 인스턴스 1개 추가 외 기존 오브젝트 변경 없음 확인.
**검증 한계**: 실제 E키로 사분면을 하나씩 돌리고 버튼을 누르는 입력 경로, 그리고 두 사람이 동시에 접속했을 때 번호·열림 상태가 서로 다르게 보이는지(`LockBoxNetworkSync` 부재로 인한 비동기화 가능성)는 다음 2인 플레이테스트에서 확인 필요.

## 별 상자 네트워크 동기화(LockBoxNetworkSync) 반영 (2026-10-23, 스무 번째 후속)
"상자 네트워크 동기화를 가져옵니다" 요청으로 바로 위에서 보류했던 `LGH.LockBoxNetworkSync`를 실제로 가져옴. `ItemNetworkSync` 때와 똑같은 유형의 문제였다: LGH님이 본인 씬(`LGH.unity`)의 `DoorSync_LGH` 오브젝트에 **직접** `LockBoxNetworkSync`를 추가해서 테스트만 해뒀고, 공유 프리팹 `Assets/Objects/LGH/DoorSync_LGH.prefab`에는 반영(Apply)하지 않은 상태였다 — KJH.unity에도 이 컴포넌트가 없어서, GIM-008을 만든 KJH 씬조차 별 상자는 로컬 전용으로만 동작하고 있었다.

- `PrefabUtility.LoadPrefabContents`/`AddComponent<LGH.LockBoxNetworkSync>`/`SaveAsPrefabAsset`로 `DoorSync_LGH.prefab`에 추가(이번에도 `LockBoxNetworkSync`만 — 범위 밖 컴포넌트는 추가하지 않음). Develop의 `DoorSync_LGH` 인스턴스가 이미 `Connected` 상태라 자동으로 전파됨(씬 파일 자체는 변경 없음, `git diff`로 확인 — 프리팹 1개만 +14줄).
- `StarLockBox`(`NetworkBehaviour`가 아닌 평범한 `MonoBehaviour`)는 그대로 둠 — 동기화는 어디까지나 옆에 붙는 `LockBoxNetworkSync`(기존 `ItemNetworkSync`/`DoorNetworkSync`/`CandleNetworkSync`와 같은 "호스트 권한 + RPC" 패턴)가 전담하므로 `StarLockBox_LGH.prefab` 자체는 수정할 필요가 없었다.

**검증**: Play 모드 `StartHost()`에서 `LockBoxNetworkSync.Instance` 스폰 확인 + `sync.Online=True`로 실제 네트워크 경로를 타는 것 확인(이전엔 `Instance`가 없어서 `StarLockBox.Press()`가 무조건 로컬 판정으로 빠졌음) → 망원경 코드([3,5,7,4])대로 `Press()` → 호스트 RPC 경유로 `IsOpen=True` 전환 → 두 보상 아이템 `CanInteract=True` 정상 전환까지 재확인. 같은 `DoorSync_LGH` 오브젝트의 `ItemNetworkSync`/`DoorNetworkSync`/`CandleNetworkSync` 세 `Instance`도 전부 정상 스폰되어 충돌 없음 확인. `Tools/Network/Validate Scene NetworkObjects` 문제 없음, 콘솔 에러 0건, Play 모드 종료 후 씬 `isDirty=False`(프리팹만 바뀌었으므로 씬 자체는 더티가 안 됨).
**검증 한계**: 전부 호스트 1개 프로세스에서 확인한 것이라, 실제로 게스트가 접속해 번호판을 돌릴 때 호스트 화면에도 같은 숫자·같은 순간에 열리는 것이 보이는지는 다음 2인 플레이테스트에서 확인 필요.

## 기믹 완료 → 문 잠금(GimmickDoors) Develop 반영 (2026-10-23, 스물한 번째 후속)
KJH 브랜치를 Develop에 다시 머지한 뒤 "방금 머지해온 KJH 씬에서 문들을 잠그는 기능을 가져와달라"는 요청. KJH 쪽이 이미 "Develop 적용 방법"으로 명시해둔 `GimmickDoors.prefab`(위치 무관, 문 이름만 같으면 됨)을 그대로 Develop 씬 루트에 인스턴스화.

- `GimmickDoorLink`(`Assets/Scripts/KJH/GimmickDoors/GimmickDoorLink.cs`) 두 개가 같은 프리팹 안에 있음: **GIM-002(인형극) 완료 전까지** `Door_Kitchen`(+ 짝문)·`Door_Kitchen_Dining`·`Door_Guest2`를 잠그고, **GIM-003(PotionCraft) 완료 전까지** `Door_Library`·`Door_Library_Guest1`·`Door_Conservatory`를 잠근다. 완료되는 순간 판정 담당(호스트, 혼자면 로컬)이 직접 연다(LGH `DoorNetworkSync` 경유 — 모두에게 같은 각도로 열림). 문은 전부 이름으로 찾으므로(부모 경로에 `Ravenwood_LGH` 포함 조건) LGH 맵 프리팹은 전혀 건드리지 않는다. 네트워크 오브젝트가 아니라(평범한 `MonoBehaviour`) `DefaultNetworkPrefabs` 등록이나 `Validate Scene NetworkObjects` 대상도 아니다.
- **새로 머지된 파일이라 처음엔 `AssetDatabase`가 못 찾았음**: `GimmickDoors.prefab`이 디스크엔 있는데 `AssetPathToGUID`가 빈 값을 반환 — `AssetDatabase.ImportAsset(..., ForceUpdate)` + `Refresh()`로 바로 해결(머지 직후 처음 다루는 새 에셋에서 가끔 나는 증상, 당황하지 않아도 됨).

**검증**: Play 모드 `StartHost()`에서 초기 상태 6개 문 전부 `locked=True` 확인 → GIM-001 풀고 GIM-002를 실제 판정 요청(`RequestMusicDoorOpened`/`RequestStageWound`/`RequestCrankCompleted`/`RequestSlotChanged`/`RequestPianoKey`)으로 3개 장면 전부 완주해 `Complete`까지 보냄 → `Door_Kitchen`/`Door_Kitchen_Dining`/`Door_Guest2` 3개 모두 `locked=False, IsOpen=True`로 자동 전환 확인. GIM-003은 `PotionCraftSync.ServerComplete()`(최종 혼합 정답 시 호출되는 공개 메서드 — 전체 양조 체인은 지난 "Develop 전체 기믹 동작 검증" 세션에서 이미 실제 체인으로 검증했으므로 이번엔 문 연동만 보기 위해 바로 호출)로 완료 처리 → `Door_Library`/`Door_Library_Guest1`/`Door_Conservatory` 3개 모두 `locked=False, IsOpen=True` 확인. `Tools/Network/Validate Scene NetworkObjects` 문제 없음, 콘솔 에러 0건. 저장 후 `git diff`로 새 프리팹 인스턴스 1개(+58줄) 추가 외 기존 오브젝트 변경 없음 확인.
**검증 한계**: 전부 판정 메서드 직접 호출로 확인한 것 — 실제로 문이 잠겨 있을 때 안내 문구("문이 잠겨 있다")가 뜨는지, 두 기믹을 실제로 플레이해서 완료하는 순간 문이 저절로 열리는 걸 눈으로 보는지는 다음 플레이테스트에서 확인 필요.

## 기믹 더미 → VARCO 모델 교체 4차 — 노션 모델링 체크리스트 기반 (2026-10-23, 스물두 번째 후속)
사용자가 노션 "[디자인] 기믹 관련 모델링 요청용" 문서(체크박스로 완료 여부 표시된 전체 모델 목록)와 "기믹 진행도" 문서를 붙여넣어 공유 — Notion 커넥터로는 두 페이지 모두 접근 불가(404, 연동 계정에 미공유)했으나 본문 내용으로 충분히 분석 가능했음. 이 문서를 `Assets/VARCO 3D/KWJ/Prefabs/Gimmick` 폴더(이전 라운드들에서 쓰던 `_NeedsCollider`와는 다른, 새로 정리된 폴더)와 대조해 Develop에 아직 반영 안 된 5가지를 찾아 전부 반영함.

**조사 중 확인한 것 — 노션 메모 정정**: "GIM-003 도르레 모델링... 반영X"라는 메모는 KJH.unity와 Develop.unity의 `Dumbwaiter_Rig` 계층을 직접 통째로 대조해보니 완전히 동일 — 이미 지난 1차 교체 라운드에서 반영된 상태였고, 이 메모는 그보다 먼저 쓰인 오래된 기록으로 확인됨. GIM-002 피아노(`Furn_GrandPiano`+`Furn_PianoBench`)도 이미 반영돼 있고(옛 더미는 비활성화된 채 남아있음) 손댈 필요 없었음.

**실제로 반영한 5가지**:
1. **GIM-001 장난감 상자**(`KeyBox`): `Boxupper`/`BoxUnder` 큐브를 `Furn_ToyChest_Gimmick.prefab`(몸통+경첩+뚜껑 분리)로 교체. `KeyBox` 자신이 비균일 스케일(0.63, 0.35, 0.48)을 갖고 있어서, 새 모델의 실제 크기가 일그러지지 않도록 자식 오브젝트에 그 역수 스케일을 곱해 상쇄(`1/0.63, 1/0.35, 1/0.48`)한 뒤 원하는 실물 크기를 적용. `KeyBoxLid.cs`의 `hingeLocalOffset`/`hingeLocalAxis`/`openAngle`를 새 모델의 실제 경첩 위치(`LidHinge` 자식 오브젝트)에서 역산해 재설정(`(0, 0.27, -0.06)` / `Vector3.right` / `-110`). **교훈**: 이 스크립트는 `Open()`이 처음 호출될 때 그 순간의 `lid.position/rotation`을 "닫힌 기준"으로 캐시하므로, 힌지 값을 바꿔가며 여러 번 테스트할 때마다 반드시 뚜껑 오브젝트를 진짜 닫힌 위치로 직접 되돌린 뒤 재시도해야 한다 — 안 그러면 이전 테스트의 틀어진 자세가 "새 닫힌 기준"으로 잘못 캐시되어 디버깅이 꼬인다(이번에 실제로 몇 번 이 함정에 빠졌다가 알아챔).
   - **사이드 이펙트로 생긴 버그와 수정**: 힌지 보정을 위해 `KeyBoxLid` 컴포넌트를 `DestroyImmediate`+`AddComponent`로 여러 번 리셋했는데, 이 때문에 `PortraitPuzzleScene.keyBox`(컴포넌트 직접 참조)가 고아 참조(null)가 되어 "GIM-001을 풀어도 상자가 안 열리는" 회귀가 생겼었다 — Play 모드 끝까지 통과시켜 재검증하는 과정에서 발견해 참조를 새 컴포넌트로 재연결해 수정함. **컴포넌트를 반복적으로 지웠다 새로 붙이는 디버깅 패턴을 쓸 때는, 그 컴포넌트를 가리키는 다른 스크립트의 참조도 끝에 반드시 재점검할 것.**
2. **GIM-001 보상 열쇠 2개**(`Key_Behind_E3`, `Key_Behind_2F`): 원기둥+큐브 조합을 `Prop_Key_Treble.prefab`(높은음자리표 모양)로 교체, 표준 임포트 보정(270,0,0) 그대로 사용.
3. **GIM-002 놀이책**(`OBJ_PLAYBOOK`): 큐브+장식 텍스트를 `Prop_PuppetBook.prefab`으로 교체. 이 모델은 **평평하게 눕는 쪽**이라(`Prop_SilverLeaf`/`Prop_RecipeBook`과 같은 예외) 표준 270 대신 **identity 회전**을 써야 책상 위에 눕는 모습이 된다 — 처음 270으로 적용했다가 책이 수직으로 선 모습(책등이 보이는)이 나와서 밝은 하늘 위 복제본으로 확인 후 identity로 정정함.
4. **GIM-003 재료 공급대 7곳**(`Supply_Material_*`/`Supply_Water_*`의 `Crate`/`Shelf`): `Furn_IngredientTable.prefab`(원래 1.58m 폭, 7곳이 1.1~1.3m 간격이라 그대로 쓰면 서로 겹침)을 X축만 0.506배 줄여서(폭 0.8m로) 기존 자리 간격에 맞춤 — 세로/깊이는 원래 비율 그대로 둬서 높이가 기존 받침대 상판 높이(0.6)와 거의 정확히 일치(0.607), 재료 아이템 위치를 손댈 필요가 없었음. 기존 `Crate`/`Shelf`는 콜라이더는 남기고 렌더러만 꺼서 숨김.
5. **GIM-003 가루 더미**(`Tint_Pile`): 예전엔 색상별 모델이 없어서 보류했었는데, 이제 `Prop_PowderDish_Red/Blue/Green/Pink/Black` 5종이 생겨서 실제로 반영함 — 이건 "하나를 염색해서 재사용"하던 구조라 단순 교체가 아니라 스크립트도 같이 고쳐야 했다:
   - `PotionCatalog.PowderInfo`에 `prefab` 필드 추가(기존 `MaterialInfo`/`WaterInfo`와 같은 패턴), `VisualPrefab()`이 색상별 프리팹이 있으면 그걸, 없으면(Gold — 아직 전용 모델 없음) 기존 공용 `powderPrefab`을 돌려주도록 변경.
   - `PotionVisuals.ShowPowderDish(root, color, fallbackTint)` 새 도우미 추가: `root` 밑에서 `"Dish_<색이름>"` 자식을 켜고 나머지는 끄며, 그런 자식이 없으면 `"Tint_Pile"`을 켜고 색칠(Gold용 대체 경로 유지).
   - `GrinderStation.Refresh()`가 `TintMarked` 대신 이 도우미를 쓰도록 변경. 분쇄기의 `PowderOnPaper` 밑에 `Dish_Green/Red/Blue/Pink/Black` 5개를 전부 비활성 상태로 미리 배치(`Tint_Pile`은 Gold 대체용으로 그대로 둠).
   - `PotionCatalog.asset`의 `powders` 배열 0~4번(Green/Red/Blue/Pink/Black)에 해당 프리팹 연결(5번 Gold는 비워둠 → 자동 대체 경로 탐).
   - **겸사겸사 발견해 고친 버그**: `PotionCraftSync.BuildHeldVisual()`이 손에 든 모습을 만들 때마다 `go.transform.localRotation = Quaternion.identity`로 강제 덮어쓰고 있었다 — 기존엔 `catalog.materials/waters`의 프리팹이 전부 `Item_Material_*`/`Item_Water_*`라는 "이미 identity가 정답"인 전용 래퍼라 문제가 없었는데, 이번에 새로 연결한 `Prop_PowderDish_*`는 VARCO 원본(루트 자체가 270 회전 필요)이라 손에 든 가루 접시가 옆으로 선 채로 보이는 버그가 났다. `localRotation` 강제 대입 줄을 지워서 프리팹 자신의 로컬 회전을 그대로 쓰도록 고침(기존 Item_* 래퍼는 identity라 영향 없음, 원본 VARCO 모델도 자기 정회전을 그대로 받게 됨) — 이제 향후 어떤 새 카탈로그 프리팹을 연결하든(래퍼든 원본이든) 자동으로 올바르게 들리게 된다.
   - 믹서(`MixerStation.layers[]`, 3칸짜리 `Renderer[]`)는 범위에서 제외함 — 칸마다 5가지 모델을 다 두려면 구조 변경(필드 타입 교체 + 15개 오브젝트 추가)이 더 크고, 믹서 통 안의 작은 표시등 수준이라 우선순위가 낮다고 판단. 여전히 기존 방식대로 단색 틴트로 표시됨.

**검증**: 각 교체마다 밝은 하늘 위 복제본으로 모양/회전 확인(기존 세션들과 같은 기법), GIM-001은 Play 모드에서 `PortraitPuzzle.RequestShift()`로 실제로 풀어 상자가 열리고(`KeyBoxLid.IsOpen=True`) 열쇠를 주울 수 있는 것까지 전체 플로우로 재확인(참조 버그 발견 전후 둘 다 테스트해서 수정 효과를 직접 확인). GIM-003은 `GrinderStation`에 리플렉션으로 `phase=PowderReady`/`color`를 바꿔가며 `Refresh()` 호출 → 해당 색의 `Dish_*`만 켜지고 나머지+`Tint_Pile`이 꺼지는 것, Gold는 `Tint_Pile` 켜지고 색칠되는 것 확인. `PotionCraftSync.ServerSetHeld()`로 빨간 가루를 손에 쥐어 봐서 회전 버그 수정 전/후 차이(수정 전: 접시가 옆으로 선 모습 / 수정 후: 평평하게 접시 담긴 모습)까지 스크린샷으로 확인. `Tools/Network/Validate Scene NetworkObjects` 문제 없음, 콘솔 에러 0건. 씬 저장 후 `isDirty=False` 확인.

## 실제 플레이테스트 버그 5건 수정 (2026-10-23, 스물세 번째 후속)
사용자가 직접 플레이해보고 보고한 5가지 문제를 모두 조사·수정함.

1. **1층 음악실 문이 처음부터 안 잠겨 있어서 GIM-001 열쇠가 무의미해짐 — 원인 발견, 수정**: `Door_Hall_Music`은 `Gim002KeyTarget.Start()`가 열쇠를 쓰기 전까지 `Lock()`을 걸어두는 방식인데(문 자체의 Inspector `locked` 기본값은 `false` — 진짜 잠금은 스크립트가 런타임에 건다), **호스트가 "방 만들기"를 누르면 `MultiplayerMenu`가 `RoomResetter.ResetAll()`을 호출해서 씬의 모든 `IRoomResettable`(문 포함)을 "에디터에 적힌 기본값"으로 되돌리는데, 이게 `Start()`보다 한참 뒤(유저가 버튼을 누른 시점)에 실행되면서 방금 걸어둔 잠금을 다시 풀어버린다.** `GimmickDoorLink`가 관리하는 문들(GIM-002 주방 등, GIM-003 서재 등)은 매 프레임 `Update()`에서 상태에 맞게 다시 잠그므로 스스로 복구되지만, `Gim002KeyTarget`은 "GIM-002 상태가 바뀔 때만" 반응하는 이벤트 구독 방식이라(`C.StateChanged += OnState`) `RoomResetter`가 상태를 안 바꾸고 문만 건드리면 영원히 안 고쳐진다 — 그래서 음악실 문만 이 증상을 보였다. **수정**: `Gim002KeyTarget`에 `Update()`를 추가해 매 프레임 `ApplyDoorLock()`을 다시 호출(다른 기믹 문들과 같은 패턴). Play 모드에서 `RoomResetter.ResetAll()`을 직접 호출해 재현 → 호출 직후엔 풀리지만 다음 프레임에 다시 잠기는 것으로 수정 확인.
2. **모든 책상에 서랍 열기 기능 추가**: `Furn_Desk_Set`(1층 서재·숨은 방, 2층 하녀방에 각 1개, 총 3세트×서랍 3개=9개)의 서랍(`Furn_Desk_Drawer_Top/Middle/Bottom`)에 이미 분리된 모델이 있었지만 `InteractableDrawer`·콜라이더가 하나도 없어서 상호작용이 안 됐다. 9개 전부에 렌더러 바운드로 계산한 `BoxCollider` + `InteractableDrawer`(LGH 공용 서랍 시스템, `CandleNetworkSync`가 씬의 모든 `InteractableDrawer`를 자동으로 찾아 동기화)를 추가. 확인해보니 활성화된 책상은 전부 `Furn_Desk_Set`뿐이고(서랍 없는 `Furn_Desk`나 원시도형 `Desk`/`OldDesk`는 전부 비활성 상태) 그 외에 "서랍 달린 활성 책상"은 없었음.
3. **아이방 장난감 상자 — 상자는 열리는데 열쇠를 못 집음**: 지난 라운드에서 `Furn_ToyChest_Gimmick`으로 교체하며 추가한 상자 몸체의 `BoxCollider`가 **닫힌 상자 전체(뚜껑 포함 높이)를 감싸는 크기라서, 상자가 열려도 그 안의 `Key_Behind_2F` 콜라이더를 완전히 둘러싸 가리고 있었다**(LGH `StarLockBox`의 `body.enabled=!on` 패턴과 같은 유형의 문제 — 열리면 안의 물건을 집을 수 있게 바깥 콜라이더를 꺼야 하는데 그 처리가 빠져 있었음). `KeyBoxLid.cs`에 `bodyCollider` 필드를 추가해서 `Open()`될 때 꺼지도록 수정, 씬의 `Model`(상자 모델 루트) 콜라이더를 연결. Play 모드에서 GIM-001을 실제로 풀어 상자가 열리는 순간 `bodyCollider.enabled=False`로 꺼지고, 그 상태에서 열쇠를 정상적으로 주울 수 있는 것까지 확인.
4. **아이방 침대 콜라이더가 메쉬보다 너무 큼**: `Furn_ChildBed`가 네 기둥 끝에 달린 둥근 장식(피니얼)까지 포함한 전체 바운딩 박스 크기의 `BoxCollider`를 쓰고 있어서, 기둥 위쪽의 빈 공간까지 전부 막고 있었다(스크린샷으로 확인: 기둥이 가늘고 그 사이는 뚫려 있는데 콜라이더는 꼭대기까지 꽉 찬 사각기둥). 바닥 높이는 유지하고 전체 높이의 55%까지만 덮도록(프레임·머리판 부분만, 피니얼 윗부분 제외) 줄임.
5. **인벤토리에 안 들어가고 손에 드는 물건들이 여전히 오른손(촛불 손)으로 집힘**: 전에 PotionCraft 쪽(`PotionCraftSync.BuildHeldVisual`)만 왼손으로 고쳤었는데, **공용 들기 시스템(`HoldableItem`, GIM-002 인형·배경카드·놀이책과 GIM-004 화분이 상속)의 `holdOffset` 기본값과 각 기믹 Build 도구가 설정하는 값이 전부 양수 X(오른손)로 남아있었다** — PotionCraft와는 완전히 다른 별도 시스템이라 그때 안 고쳐졌던 것. `HoldableItem.cs` 기본값, `Gim002Builder.cs`(인형 3곳·배경카드·놀이책), `Gim004Builder.cs`(화분)의 `holdOffset`을 전부 음수 X로 바꾸고, 씬에 이미 배치된 인스턴스 17개(배경카드 5+인형 4+놀이책 1+화분 7)도 전부 왼쪽으로 재설정.

**세션 중 겪은 함정 — 기록해 둘 것**: 이번에 처음 확인한 사실인데, **이 프로젝트는 "Play 모드 진입 시 씬을 다시 로드하지 않음" 설정이라 Play 중 새로 만들거나 지운 오브젝트/컴포넌트는 Stop 후에도 남지만, 기존에 있던 컴포넌트의 필드 값(SerializedObject로 바꾼 값 등)을 Play 모드 "중에" 수정하면 Stop 하는 순간 전부 원래 값으로 되돌아간다.** 이번에 침대 콜라이더 크기 조정과 holdOffset 17개를 전부 Play 모드 중에 고쳤다가 Stop 후 사라진 것을 뒤늦게 발견해서 Edit 모드에서 다시 했다 — **앞으로 필드 값을 바꾸는 작업은 반드시 Edit 모드(Play 시작 전이거나 Stop한 뒤)에서 하고, Play 모드는 그 결과를 확인만 하는 용도로 쓸 것.** 새 오브젝트 추가/삭제(서랍 콜라이더 추가 등)는 이 문제의 영향을 받지 않았음(둘의 저장 방식이 다른 것으로 보임).

**검증 공통**: `Tools/Network/Validate Scene NetworkObjects` 문제 없음, 콘솔 에러 0건. 전부 Edit 모드에서 값을 고친 뒤 Play 모드로 들어가 실제 동작(문 재잠금, 서랍 열기, 상자+열쇠, 손 위치)을 확인하고 Stop 후에도 Edit 모드 값이 그대로 남아있는지 재확인했다.

## 미사용 VARCO 에셋 전수 조사 + 피아노 벤치·GIM-002 배경카드 반영 (2026-10-23, 스물네 번째 후속)
사용자가 "모델링 에셋들 중 사용되지 못한 에셋이 있습니까?"라고 물어서 `Assets/VARCO 3D/KWJ/Prefabs`의 166개 프리팹을 Develop·KJH 씬이 실제로 참조하는 메시·머티리얼 기준으로 전수 대조함(단순히 "프리팹 자체가 씬에 인스턴스로 있는가"만 보면 안 됨 — 지금까지 여러 교체 작업을 자기 자신의 `MeshFilter`/`MeshRenderer`에 메시만 복사해 넣는 방식으로도 많이 해서, 프리팹 GUID 기준으로만 찾으면 오탐이 많이 남는다. 실제 사용된 메시/머티리얼 에셋 경로를 모아 대조해야 정확함).

**결과: 135개 사용 중, 31개 미사용.** 조사 중 단순 "미배치"가 아니라 실제 불일치 2건을 발견:
- **`Gimmick/Furn_PianoBench.prefab`** — 씬의 "Furn_PianoBench" 오브젝트가 실제로는 `Furn_BedBench.fbx`(침대 벤치) 메시를 쓰고 있었음. 반영함(아래 참고).
- **`Props/Prop_Candle.prefab`** — 플레이어가 상시 들고 다니는 촛불(`Candle`/`Candle0~2`)이 지금도 기본 큐브. 아직 미반영(이번엔 범위 밖, 다음에 처리).

나머지 29개는 배경 가구(아직 어디에도 안 놓임: `Furn_BookCart`/`CardCatalogue`/`ChildDesk`/`CoatStand`/`CoffeeTable`/`CopperBoiler`/`KitchenStove`/`Lectern`/`LibraryLadder`/`Ottoman`/`Stool`/`TelescopeStand`/`TowelRail`/`VanityTable`/`WingbackChair`), 장식 조명(`Prop_Chandelier`/`Prop_WallSconce`), 단서 소품(`Clues/Prop_Goblet_Broken`/`Prop_KeroseneCan`/`Prop_KeroseneCan_Burnt`), LGH 기믹 메커니즘 교체 후보(`Prop_CombinationLock`/`Prop_GearLarge`/`Prop_GearSmall`/`Prop_ServantBellBoard` — 지금 다락 자물쇠·종 등은 전부 원시도형), GIM-001 무전기(이전에 이미 확인한 미배치 장식용, `Prop_Radio`/`Furn_RadioTable`), 크기 안 맞아 의도적으로 보류 중인 것(`Furn_WallShelf`/`Furn_HallBench`, GIM-004 선반용으로 검토했다가 기각된 건)으로 분류됨.

**이번에 실제로 반영한 것 — 피아노 벤치 + GIM-002 배경카드 프레임**:
- **피아노 벤치**: 씬의 `Furn_PianoBench` 오브젝트(F1_Music)의 `MeshFilter`/`MeshRenderer`를 진짜 `Furn_PianoBench.prefab`의 메시·머티리얼로 교체(둘 다 단일 메시 루트라 자기 자신의 렌더러만 바꾸면 되는 단순 치환 — 회전(270,0,0 + 추가 180Y)·콜라이더는 이미 올바르게 맞춰져 있어서 그대로 둠).
- **GIM-002 배경카드 5장**(`BG_Castle`/`BG_NightForest`/`BG_Greenhouse`/`BG_MusicRoom`/`BG_Sea`): 큐브+텍스트 placeholder를 `_NeedsCollider/Prop_PuppetBGFrame.prefab`(Frame+Picture 2파츠, Picture는 머티리얼로 그림을 바꿔 끼우는 구조)로 교체. `Assets/VARCO 3D/KWJ/Images/`에 5장 전부 실제 일러스트(`BG_Castle_Tower`/`BG_Forest_Stars`/`BG_Glasshouse_Pots`/`BG_Room_GrandPiano`/`BG_Sea_Lighthouse`)가 이미 있어서, 각 그림을 `_BaseMap`으로 물린 머티리얼 5개(`Assets/Prefabs/KJH/Gim002/Materials/M_BGPicture_*.mat`, 공용 `M_PuppetBGPicture` 복제)를 새로 만들어 연결. 루트의 `BoxCollider`/`Gim002StageItem`(판정 로직)은 손대지 않고 시각 요소(자식)만 교체 — 기존 "교체 시 부모 그대로, 자식만 바꾸기" 원칙 그대로.
- **덤으로 발견**: `Assets/VARCO 3D/KWJ/Images/`에 `PianoSymbol_Cloud/Flower/Moon/Star/Sun.png`(건반에 그려질 5개 문양 이미지)도 이미 있음 — GIM-002 피아노 건반에 아직 적용 안 됐다면 다음 라운드 후보로 메모.

**검증**: 밝은 하늘 위에서 피아노 벤치·배경카드 5장 전부 스크린샷으로 모양·그림 매칭 확인(5장이 나란히 놓고 보니 각자 다른 그림이 정확히 들어간 것 확인). Play 모드에서 GIM-002 슬롯 판정(`RequestSlotChanged`)이 여전히 정상 작동(Staging→Accompany 전환)해서 자식 교체가 `Gim002StageItem`의 `doll`/`background` enum 판정에 영향 없음을 재확인. `Tools/Network/Validate Scene NetworkObjects` 문제 없음, 콘솔 에러 0건. 이번엔 전부 Edit 모드에서 작업(Play 모드는 확인 전용으로만 사용 — 지난 라운드 교훈 적용).

## 피아노 벤치 콜라이더 수정 + 건반 문양 이미지 적용 (2026-10-23, 스물다섯 번째 후속)
- **피아노 벤치 콜라이더**: 지난번 메시만 올바른 `Furn_PianoBench`로 바꾸고 콜라이더는 손 안 댔었는데, 옛 `Furn_BedBench` 메시 기준 크기(`size=(0.48,1.20,0.46)`)가 그대로 남아 있어서 실제 벤치 모양과 전혀 안 맞았음. 렌더러의 실제 월드 바운드를 로컬 좌표로 역산해서 새 콜라이더(`size=(0.90,0.57,0.44)`)로 교체 — 이제 `BoxCollider` 바운드가 `MeshRenderer` 바운드와 정확히 일치.
- **피아노 건반 문양**: GIM-002 건반 5개(`OBJ_PIANO_KEYS/Key_1~5`)에 붙어 있던 "Label"(한글 텍스트 TextMesh: 해/구름/달/꽃/별)을 실제 문양 이미지로 교체. `Assets/VARCO 3D/KWJ/Images/PianoSymbol_Sun/Cloud/Moon/Flower/Star.png`(투명 배경, 단순 선 형태 아이콘 — 지난번 "미사용 에셋 조사"에서 발견해둔 것)를 사용해 머티리얼 5개(`Assets/Prefabs/KJH/Gim002/Materials/M_PianoSymbol_*.mat`, URP/Unlit + Surface Type Transparent)를 새로 만들고, 기존 Label 자리에 똑같은 위치·회전으로 Quad를 놓아 교체. 크기는 처음 0.09m로 했다가 밝은 곳에서 확대해 보니 건반 대비 너무 작아서(원본 PNG 자체는 캔버스의 90% 이상을 아이콘이 채우고 있어 패딩 문제가 아니었음 — 그냥 처음 잡은 배율이 작았음) 0.15m로 키움. `Gim002PianoKey`의 `symbol`/`hasSymbol` 판정 로직은 전혀 건드리지 않음(시각 요소만 교체).

**검증**: 밝은 곳 확대 스크린샷으로 피아노 벤치 콜라이더 모양·건반 문양 5개 전부 확인(사분면마다 다른 문양이 맞게 들어간 것 확인). Play 모드에서 `Gim002PianoKey.Symbol`/`HasSymbol` 데이터 그대로 유지되는 것 재확인. `Tools/Network/Validate Scene NetworkObjects` 문제 없음, 콘솔 에러 0건. 전부 Edit 모드에서 작업 후 저장.

## LGH 담당 영역 기믹 모델링 후보 재확인 — 전부 보류 (2026-10-26)
10/23 "미사용 VARCO 에셋 전수 조사"에서 "LGH 기믹 메커니즘 교체 후보"로 분류해뒀던 `Prop_CombinationLock`/
`Prop_GearLarge`/`Prop_GearSmall`/`Prop_ServantBellBoard` 4종을 실제로 LGH 담당 영역까지 포함해 교체할 수 있는지
재조사함(사용자가 "LGH 담당 영역 기믹 오브젝트도 교체 대상으로 간주"하기로 결정). 결과: **전부 보류**.

- **`Prop_CombinationLock`(다락 자물쇠 후보)**: Develop 씬 `Ravenwood_LGH/TempFurniture/Attic/FinalDevice`+
  `FinalDevice_Dial`(스크립트 없는 순수 장식, 큐브+납작한 원통 디스크 조합, "다락 자물쇠"와 외형이 가장 비슷한
  유일한 후보)과 대조한 결과, `FinalDevice`의 실제 크기(2.9×1.2×1.8m, 트렁크/장치급)가 `Prop_CombinationLock`
  모델(약 0.18×0.26×0.12m, 손바닥 크기 자물쇠)보다 **15배 가까이 커서** 그대로 맞추려면 모델을 심하게 늘려야
  함 — GIM-004 선반·화분 받침대 때와 같은 유형의 "크기가 전혀 안 맞는" 케이스로 판단해 보류. 사용자 확인 후
  결정.
- **`Prop_GearLarge`/`Prop_GearSmall`/`Prop_ServantBellBoard`(톱니바퀴·하인 종 호출판)**: Develop·LGH.unity
  씬 전체(LGH.unity는 additive로 열어 대조 후 저장 없이 닫음)를 이름 기준("Gear"/"Bell")으로 전수 검색했으나
  **대응하는 씬 오브젝트 자체가 없었고, 관련 스크립트(`Bell`/`Gear`/`Safe`/`Vault` 등)도 전혀 없음** — 즉 이
  셋은 "원시도형으로 구현된 기믹"이 아니라 애초에 아직 어떤 기믹에도 쓰이지 않은 미배치 장식 후보였음.
  교체할 대상이 없으므로 지금은 손대지 않고, 해당 기믹(하인 호출벨 등)이 실제로 설계·구현될 때 다시 검토
  하기로 함.
- 검증: 이번 라운드는 씬 수정 없이 조사·판단만 진행(`FinalDevice_Dial`과 임시 `Prop_CombinationLock` 복제본을
  밝은 곳에서 대조한 뒤 바로 정리, LGH.unity는 읽기만 하고 닫음). `git diff` 변경 없음.

## 실제 플레이 피드백 18건 일괄 수정 — 손 보이기·웅크리기·달리기·충돌·위치 버그 (2026-10-26, 두 번째 라운드)
사용자가 직접 플레이하며 모은 18가지 문제를 한 번에 전달하고 "더미 모델링을 가져와야 하는 것만 빼고 나머지는 해결"을
요청. 모델링이 필요해서 제외한 항목은 2개뿐이고(§끝 참고), 나머지 16개는 전부 이번 라운드에서 처리함.

### 신규 기능
- **왼손 아이템을 상대방 화면에도 보이게** — 공용 들기 시스템(`HoldableItem`/`PlayerHand`)은 원래 "손에 든 물건은
  네트워크 동기화 안 함(기획: 상대가 못 봄)"이 명시적 설계였음(`PlayerHand.cs` 주석). 이번에 그 설계를 바꿔
  새 `Assets/Scripts/KJH/Common/Hold/HoldableNetworkSync.cs`(NetworkBehaviour, 프리팹
  `Assets/Prefabs/KJH/Common/HoldableNetworkSync.prefab`, `DefaultNetworkPrefabs` 자동 등록, Develop 씬 루트에 인스턴스 배치)를
  추가함 — 씬의 모든 `HoldableItem`(현재 17개: 배경카드 5·인형 4·화분 7·놀이책 1)에 계층 경로 기준 고정 인덱스를
  매기고, "어느 클라이언트가 몇 번 아이템을 드는지"만 `NetworkList`로 가볍게 공유한다. 집기/최종 배치 판정 자체는
  그대로 각 기믹의 기존 로직이 담당하고, 이 컴포넌트는 "소유권 충돌 방지 + 상대 손에 보여주기"만 맡는다.
  - `PlayerHand`에 `LocalTaken`/`LocalReleased` 정적 이벤트를 추가해 "방금 집었다/놓았다"를 네트워크를 몰라도
    되게 던지고, `HoldableItem`에는 `public static Func<HoldableItem,bool> HeldByOther` 훅을 추가해
    `CanInteract`/`InteractPrompt`/`Interact()`가 "상대가 들고 있다"를 확인하고 집기를 막을 수 있게 함(겸사겸사
    두 사람이 같은 물건을 동시에 집는 경쟁 상태도 막아줌 — 원래 설계에는 이 방지 장치 자체가 없었음).
  - 상대가 들고 있는 동안에는 이 컴퓨터의 로컬 복사본을 상대 플레이어 오브젝트의 자식으로 옮겨 붙여서
    "실제로 손에 든 모습"을 보여주고, 상대가 손을 놓으면(아직 자리에 꽂히지 않았다면) `HoldableItem.ForceReturnHome()`
    (신규 메서드 — 기존 `ReturnHome()`은 `Location==Home`이면 "할 일 없음"으로 보고 넘어가는 가드가 있어서,
    "로컬 Location은 안 바뀌었지만 보여주기용으로 트랜스폼만 옮겨둔" 이 상황에는 못 쓴다)으로 애니메이션 없이
    바로 원래 자리에 되돌린다.
  - 검증: 호스트 1개 프로세스에서 실제 E 흐름(`HoldableItem.Interact()`)으로 집기→`NetworkList`에 항목
    추가(clientId=0)→`HeldByOther`가 본인 것은 false 반환 확인, 가짜 다른 clientId 항목을 주입해 `CanInteract=False`
    + 안내 문구 "(상대가 들고 있다)"로 바뀌는 것, `Interact()`를 통한 집기 시도가 실제로 막히는 것까지 확인.
    콘솔 에러 0건. **실제 상대 손 위치에 보이는 모습 자체(두 번째 네트워크 플레이어 오브젝트가 필요)는 이 환경에서
    검증 불가 — 다음 2인 플레이테스트에서 확인 필요.**
- **웅크리기 + 달리기** (`Assets/Scripts/LGH/FirstPersonController.cs`): 왼쪽 Ctrl을 누르고 있는 동안 웅크림
  (`CharacterController.height`/`center`+카메라 높이를 부드럽게 보간, 이동속도 0.55배, 낮은 천장 아래에서는
  `SphereCast`로 막혀 있으면 못 일어남), 왼쪽 Shift를 누르고 실제로 움직이는 동안 달리기(이동속도 1.6배,
  웅크린 상태에서는 못 뜀). 기존 `SpeedMultiplier`(IPlayerLock, 컷신 등 외부 잠금용)와 곱연산으로 합쳐서
  기존 이동 잠금 시스템과 충돌 없음. **실제 키 입력 경로는 이 환경에서 시뮬레이션 불가 — 코드 검토 + Awake 시점
  높이/카메라 계산 값 확인까지만 함, 다음 플레이로 체감 확인 필요.**
- **플레이어끼리 서로 통과하지 않게** — `NetworkPlayer_LGH.prefab` 루트에 `CharacterController`와 같은 크기
  (Height 1.53 / Radius 0.255 / Center (0,0.765,0))의 `CapsuleCollider` + `Rigidbody`(`isKinematic=true`,
  `useGravity=false`)를 추가함. `CharacterController.Move()`는 원래 다른 `CharacterController`와는 서로 밀지
  않지만 커패슐+키네마틱 리지드바디 같은 일반 콜라이더와는 충돌하므로, 상대 플레이어를 "고정된 장애물"처럼
  인식해 통과하지 못하게 된다(이 프로젝트처럼 각자 자기 `CharacterController.Move()`만 쓰는 구조에서 흔히 쓰는
  해법). **실측은 2인 동시 접속이 필요해 다음 플레이테스트에서 확인.**

### 위치/크기 버그 수정 (전부 Edit 모드에서 수정, Play로 재확인)
- **피아노 손 촛불을 살짝 앞으로**: `NetworkPlayer_LGH.prefab`의 `HeldCandle` 로컬 Z를 0.55→0.67로.
- **아이방 인형 4체가 가구 위에 떠 있던 버그**: `DOLL_FrogCan`/`DOLL_OwlBook`/`DOLL_CatViolin`은 내부 구조가
  "Model(반 높이 0.15 들어올림) → 실제 모델(또 0.15 들어올림)"으로 반 높이 보정이 중첩 적용돼 있었음(모델 교체
  라운드에서 생긴 실수로 추정). `DOLL_OwlScope`만 바깥쪽이 0.13으로 달랐음. 중첩분을 상쇄하도록 각 인형 루트의
  로컬 Y를 -0.15(OwlScope만 -0.12)만큼 낮춰서 선반 윗면(월드 Y 4.50)에 정확히 닿게 함. 인형 들기/배치는
  `HoldableItem.PlaceAt()`/`ReturnHome()` 둘 다 "앵커 기준 로컬 (0,0,0)"으로 옮기므로, 이 수정 하나로
  **선반 위(평소)와 인형극 무대 위(연기 중) 양쪽의 뜨는 문제가 동시에 해결됨**.
- **아이방 초상화 기믹 후 열리는 상자 뚜껑이 벽에 파묻히던 버그**: `KeyBox`(`ChildrenRoom`)의 힌지가 상자 뒤쪽에
  있는데, 상자 자체가 벽(`F2_Int_Nursery_S_6_Seg0`)에서 겨우 0.11m 떨어져 있어서 뚜껑이 뒤로 젖혀질 때마다
  벽을 뚫고 들어갔음(`Physics.OverlapBox`로 실제 겹침 확인). 상자를 벽에서 0.30m 더 떨어뜨려서(로컬 Z 보정,
  이 계층엔 180도 회전이 있어 로컬 -Z가 월드 +Z로 매핑되는 걸 먼저 확인하고 반대 부호로 적용) 해결, 다시 열어
  겹침이 사라진 것 확인.
- **인형극 무대 위 인형이 뜨던 버그**: 위 "아이방 인형" 항목과 동일 원인·동일 수정으로 같이 해결됨.
- **아이방 그림액자 랙이 벽에 파묻혀 있던 버그**: `OBJ_CARD_RACK`(GIM-002 배경카드 5장이 꽂힌 선반, 몸체
  자체는 여전히 원시 큐브 — 모델링은 이번 범위 밖)가 동쪽 벽(`SecretStairB_Closet`)에 박혀 있었음(`Physics.OverlapBox`로
  확인). 랙 전체를 벽에서 0.08m 떨어뜨려 해결, 꽂혀 있던 카드 5장도 같이 이동해 배치가 흐트러지지 않음.
  **랙 자체를 실제 가구 모델로 바꾸는 것은 이번 요청에서 제외(모델링 필요).**
- **혼합 통(Vat)이 너무 작던 문제**: `PotionCraft_F2/Station_Mixer_F2/Vat`의 스케일을 0.70→1.05(1.5배)로 키우고,
  안의 가루 층(`Layer0/1/2`)·이펙트 지점도 같은 비율로 위치·크기를 다시 맞춤.
  - **쓰레기통 버그 여지 남음**: PotionCraft_F1의 쓰레기통(TrashStation)은 이번에 손대지 않음.
- **주방 물병류 재료가 가구에 파묻혀 있던 버그**: `Supply_Water_Red/Blue/Green`의 병 아이템들이 예전 받침대
  (`Shelf`, 지금은 렌더러만 꺼진 콜라이더 전용) 높이(0.6) 기준으로 놓여 있었는데, 실제 비주얼 테이블
  상판(`Model`, VARCO `Furn_IngredientTable`)도 거의 같은 높이(0.607)라 큰 문제는 아니었고 미세하게(4.7cm) 박혀
  있던 것만 교정함 — 처음엔 Y/Z 바운드 인덱스를 착각해 "0.79"로 잘못 계산했다가, 직접 재조회해 바로잡음
  (교훈: `Bounds.ToString()`의 Extents 순서를 눈대중으로 재구성하지 말고 코드로 다시 읽을 것).
- **온실 기록일지가 책상 위에 떠 있던 버그**: `GIM004_Journal`을 세로로 0.08m 내려서 `Furn_ReadingTable_Seg2`
  상판에 맞춤.
- **건물에서 양초를 추가로 주울 수 있던 기능 제거**: `CandleNetworkSync`(현재 `Infinite=true`라 양초가 원래
  안 닳는 상태라 이 기능 자체가 사실상 의미 없었음)의 `minCandles`/`maxCandles`를 0으로 낮춰서, 서랍 등
  `CandlePickup` 자리에 양초가 아예 안 생기게 함(`Assets/Objects/LGH/DoorSync_LGH.prefab` 수정 — 공유
  프리팹이라 KJH·LGH·Develop 전부에 적용됨). 벽걸이 촛대(`CandleSconce`)·서랍 열림 동기화는 그대로 유지.

### 입력/UX 정리
- **게스트룸 제조법 책 쪽 넘기기에서 E 제거**: `ReadableStation.cs` — 읽는 중 E를 눌러도 더는 다음 쪽으로
  넘어가지 않음(쪽 넘기기는 A/D·방향키 전담, 닫기는 Q/Esc 전담). 안내 문구·힌트 텍스트에서도 `[E]` 표기를 제거.
- **음악실 놀이책 쪽 넘기기를 게스트룸 방식과 통일**: `Gim002PlayBook.cs` — 기존엔 방향키(←/→)만 됐는데,
  게스트룸 제조법 책과 같이 A/D도 똑같이 쪽을 넘기도록 추가(책을 펴고 닫는 키는 그대로 F).
- **제조법 내용 직관성 개선**: 1층 최종 레시피 쪽지(`FinalRecipeData`)의 `noteText`는 분위기용 문구로 두고,
  실제 정답 순서(`sequence`)를 항상 "1) 빨간색 가루  2) 파란색 가루  3) ..." 형태로 자동 생성해 덧붙이도록
  `ReadableStation.cs`에 `BuildOrderHint()`를 추가함 — 데이터(`sequence`)에서 직접 만들어서 글 문구가 실제
  정답과 어긋날 걱정이 없고, 순서가 있다는 점이 훨씬 분명해짐. 2층 제조법 책의 재료 조합 힌트(함정 포함)는
  의도된 퍼즐 난이도라 손대지 않음.
- **책상 서랍**: 확인해보니 활성 상태인 책상(`Furn_Desk_Set`, 서재·숨은 방·하녀방 3곳×서랍 3개=9개)은 지난
  10/23 라운드에서 이미 전부 `InteractableDrawer`가 붙어 있어 정상 작동 중이었음 — 이번엔 추가 조치 없음
  (혹시 안 열리는 특정 책상이 더 있다면 위치를 짚어주면 확인하겠음).

### 이번에 제외한 것 (모델링 필요 — 사용자 직접 관여 요청대로 손대지 않음)
- **붉은 뿌리(PotionCraft 재료)**: 여전히 원시도형. 대응하는 VARCO 모델이 아직 없음.
- **아이방 그림액자 랙(`OBJ_CARD_RACK`) 몸체**: 벽 파묻힘은 고쳤지만 선반 자체는 여전히 원시 큐브.

### 검증 공통
전부 Edit 모드에서 수치를 고친 뒤 Play 모드(`StartHost()`)로 들어가 값이 그대로 유지되는지, 그리고 위 기능들의
핵심 로직(가짜 네트워크 항목 주입, 실제 `Interact()` 호출 등)을 리플렉션으로 확인. `Tools/Network/Validate Scene
NetworkObjects` 문제 없음, 콘솔 에러 0건. Play 모드 종료 후 모든 위치 수정값이 씬에 그대로 저장돼 있는 것 재확인
(이번 라운드는 전부 Edit 모드 수정이라 "Play 중 값 수정은 Stop하면 사라진다" 함정에 해당하지 않음).

## 실제 플레이 피드백 후속 9건 — 아바타 손 부착·웅크림 동기화·콜라이더 가림 버그 (2026-10-26, 세 번째 라운드)
직전 라운드(손 보이기·웅크리기·달리기·충돌 추가)를 실제 2인 플레이로 테스트한 뒤 받은 9가지 추가 피드백. 사용자가 실시간으로
"현재 플레이어가 양초를 쥐고있는 모습" 스크린샷을 보내줘서(파란 캐릭터가 오른손에 촛불을 든 실제 인게임 사진) 이 프로젝트에
**`CoopDemo.CharacterAvatar`/`CharacterAvatarModel`(KSM 제작)라는, 기존에 몰랐던 훨씬 발전된 3인칭 아바타 시스템**이 이미
있다는 걸 이번에 처음 파악함 — 뼈대 기반 절차적 애니메이션(걷기/뛰기/점프/말하기)과 손 부착(`AttachHandCandle`) 메커니즘이
이미 존재했고, 직전 라운드의 `HoldableNetworkSync`가 이걸 모르고 플레이어 루트에 직접 고정 오프셋으로 붙이고 있었던 게
"왼손 아이템이 공중에 뜨고 손이 안 움직이는" 버그의 원인이었음.

### 아바타 손·웅크림 (CharacterAvatarModel.cs/CharacterAvatar.cs 확장)
- **왼손 아이템이 공중에 뜨고 손 움직임이 안 보이던 버그**: `HoldableNetworkSync.Apply()`가 상대 플레이어의 **루트
  트랜스폼**에 고정 오프셋으로 붙이고 있었음 — 걷기 애니메이션은 아바타 모델의 뼈(`LeftArm`/`LeftForeArm`)만 움직이고
  루트는 안 움직이므로, 아이템이 손과 전혀 무관하게 고정된 채 떠 있었음. `CharacterAvatarModel`에 `AttachHandCandle`과
  같은 패턴의 `AttachLeftHandItem(Transform)`(왼손 뼈 `LeftHand` 기준 소켓)을 추가하고, `HoldableNetworkSync.Apply()`가
  `CharacterAvatar.Model.AttachLeftHandItem()`을 쓰도록 변경 — 이제 걷는 동안 팔이 흔들리면 아이템도 같이 움직인다.
  (아바타 모델이 아직 안 만들어졌을 스폰 직후 한두 프레임만 기존 방식의 대체 위치를 임시로 씀.)
- **웅크릴 때 상대에게 안 보이던 버그**: 웅크림은 로컬 `CharacterController`의 높이/중심만 바꿀 뿐 **루트 위치 자체는
  안 바뀌므로**(발이 땅에 붙어 있도록 중심만 내림), 네트워크로 동기화되는 건 루트 위치뿐이라 상대는 전혀 몰랐음.
  `CharacterAvatar`에 `NetworkVariable<bool> crouching`(소유자 전용 쓰기)을 추가해 `FirstPersonController.IsCrouching`을
  매 프레임 감지해 값이 바뀔 때만 보냄 — 받는 쪽은 `CharacterAvatarModel.SetCrouching()`으로 **모델 전체를 부드럽게
  내리고**(`crouchDrop`, 루트가 아니라 모델 자식 트랜스폼만 낮춤), 기존에 착지 순간에만 쓰던 다리/엉덩이 굽힘 공식
  (`crouch` 변수)을 재사용해 다리도 굽힌다 — 착지 떨림과 지속 웅크림이 같은 포즈 공식을 공유.
- **오른손 촛불이 손에 쥔 것처럼 안 보이는 문제**: 사용자가 보내준 실제 인게임 스크린샷으로 확인 — `AttachHandCandle`
  자체는 정상 동작 중(촛불이 손 근처에 붙음)이지만, 단순 뭉툭한 손 모양이라 "쥐는 손가락" 표현이 원래 없어 한계가 있음.
  이번엔 오프셋을 건드리지 않음(사용자 확인 없이 좌표를 임의로 더 바꾸면 오히려 어긋날 위험) — **다음에 다시 스크린샷을
  보내주면 그걸 보고 `handCandleOffset`을 미세 조정하겠음.**

### 위치/콜라이더 버그
- **책상(Furn_Desk_Set) 서랍 상호작용이 안 되던 진짜 원인 발견**: 지난 라운드엔 "컴포넌트+콜라이더가 다 있으니 정상"으로
  확인했었는데, 실제로는 **책상 몸체(`Furn_Desk_Body`)의 커다란 박스 콜라이더가 서랍 3개의 콜라이더를 통째로 감싸고
  있어서, 서랍 쪽으로 레이를 쏴도 몸체가 먼저 맞아버려 서랍 자체는 영원히 조준이 안 되는 상태**였음
  (`Physics.RaycastAll`로 직접 검증: 몸체가 거리 1.764, 서랍이 1.791로 몸체가 2.7cm 먼저 맞음). 몸체 콜라이더의
  앞면 쪽 두께를 줄여서(로컬 Y 0.71→0.59, 중심 이동) 서랍이 먼저 맞도록 수정, 서재·숨은 방·하녀방 책상 3개 전부
  동일하게 적용 후 레이캐스트로 재검증.
- **아이방 열쇠 상자가 열리기 전 충돌이 없던 버그**: `KeyBoxLid.bodyCollider`(상자 몸체 콜라이더, 열리면 꺼서 안의
  열쇠를 집을 수 있게 하는 용도)가 씬에 **처음부터 꺼진 채로 저장**돼 있어서, 게임 시작부터 상자를 그냥 통과해 걸어
  다닐 수 있었음(여는 로직은 "열릴 때 끈다"만 있고 "닫혀있을 때 켠다"가 없어서 기본값에 전적으로 의존하는 구조).
  씬의 기본값을 `enabled=true`로 바로잡음.
- **인형극 무대 위 인형이 여전히 떠 있던 버그 — 지난 수정이 틀렸던 원인**: 지난 라운드에 "인형 루트의 로컬 Y"를
  내려서 고쳤다고 생각했는데, `HoldableItem.PlaceAt()`은 꽂을 때 로컬 좌표를 **무조건 (0,0,0)으로 강제 리셋**하므로
  (앵커 기준), 선반(Home)에서는 통했지만 무대(Placed)에서는 내가 건 보정이 매번 지워지고 있었음. **진짜 원인은
  인형 내부 자식 메시(`Model/Frog`, `Model/Owl`, 안쪽 `Model`)의 중첩된 "반높이 들어올림" 오프셋**이었으므로, 루트가
  아니라 그 안쪽 자식의 로컬 Y를 낮춰서(인형별 0.15 또는 0.125) "루트 원점 자체가 발바닥"이 되도록 고쳤다 — 이러면
  Home이든 Placed든 로컬 (0,0,0)에 둬도 항상 바닥에 붙는다. 선반 위·무대 위 둘 다 재검증 완료.
- **아이방 그림액자 랙 — 벽 파묻힘 재확인, 원인 재정의**: `Physics.OverlapBox`로 재검증한 결과 랙(`OBJ_CARD_RACK/Rack`)
  자체는 벽과 전혀 겹치지 않음(0.18m 이상 여유). 스크린샷으로 직접 확인해보니 실제 문제는 "벽에 파묻힘"이 아니라
  **아직 모델링되지 않은 갈색 원시 박스가 벽지·걸레받이와 마감이 안 맞아 벽에 구멍이 뚫린 것처럼 보이는 것**으로
  재해석됨 — 이 자체는 모델링 범위(이번에 제외)라 추가 조치 안 함. 사용자가 다른 지점/다른 가구를 가리키는 것이라면
  스크린샷으로 짚어주면 다시 확인하겠음.

### 확인했지만 못 찾은 것
- **Study 벽지 안 발린 곳**: `Walls` 하위 모든 메쉬의 머티리얼을 전수 검사했으나 NULL/기본 머티리리얼이 붙은 벽이
  하나도 없었음(전부 `M_Wallpaper` 정상 배정) — 즉 "머티리얼 누락"이 아니라 애초에 벽 세그먼트 자체가 없는 틈(구조적
  빈틈)일 가능성이 높음. Scene 뷰로 여러 각도에서 찾아봤으나 정확한 위치를 특정하지 못함 — **스크린샷이나 대략적인
  위치(어느 벽/모서리인지)를 알려주면 바로 찾아 고치겠음.**

### 검증
전부 Edit 모드에서 수정 후 Play 모드(`StartHost()`)로 들어가 확인: `CharacterAvatarModel.AttachLeftHandItem()`을
직접 호출해 아이템이 `LeftHand` 뼈 근처(0.12m 이내)로 옮겨붙는 것, `SetCrouching(true)`로 모델 로컬 Y가
`-crouchDrop`까지 부드럽게 내려가는 것, 책상 콜라이더 수정 후 레이캐스트가 서랍을 먼저 맞추는 것, 열쇠상자 콜라이더가
`enabled=true`인 것, 인형 4체가 선반(Home)·무대(Placed) 양쪽에서 정확히 바닥에 닿는 것까지 확인. `Tools/Network/Validate
Scene NetworkObjects` 문제 없음, 콘솔 에러 0건. **아바타 쪽 수정은 전부 "다른 사람이 보는 모습"에 관한 것이라 호스트
1개 프로세스로는 최종 비주얼을 완전히 확인할 수 없음 — 다음 2인 플레이테스트에서 실제로 확인 필요** (다만 이번엔
사용자가 보내준 실제 인게임 스크린샷 덕분에 추측이 아니라 실측 기반으로 원인을 찾을 수 있었음).

## 왼손/오른손 들기 자세 추가 — 차렷 자세로 손목에 박혀 보이던 문제 (2026-10-26, 네 번째 라운드)
직전 라운드 수정을 실제로 2인 플레이해본 사용자가 스크린샷으로 다시 피드백: 오른손 촛불은 손목 쪽에 박힌 것처럼
보이고, 왼손 아이템은 손 위치까지는 내려왔지만 **왼팔 자체가 차렷 자세(옆으로 쭉 편 채)라서 손목에 붙은 채 몸통에
파고든 모습**이었음. 원인은 `CharacterAvatarModel.ApplyPose()`가 오른팔에는 항상 "촛불 든 자세"(`holdArmAngle`/
`holdForeArmAngle`로 팔을 앞으로 굽힘)를 적용하지만, 왼팔에는 그런 굽힘이 전혀 없어서 평소 걷기 흔들림만 적용된
채(즉 물건을 들어도 팔이 안 굽혀짐) 손 소켓 오프셋만 붙어 있었기 때문.

- **왼팔에도 오른팔과 같은 방식의 "드는 자세"(`leftHoldArmAngle`/`leftHoldForeArmAngle`, 기본값은 오른팔과 동일)를
  추가**하고, `holdingLeftItem` 상태(아이템을 붙이면 켜짐, `DetachLeftHandItem()`으로 꺼짐)에 따라 걷기 흔들림과
  드는 자세 사이를 부드럽게 블렌드(`leftHoldAmount`, `holdPoseBlendSpeed`)하도록 `ApplyPose()`를 수정. 이제 왼손에
  뭔가를 들면 오른팔처럼 앞으로 굽혀 들고, 놓으면 다시 자연스러운 걷기 흔들림으로 돌아온다.
- `HoldableNetworkSync`가 아이템을 놓을 때 어느 아바타 모델에 붙여놨었는지 기억해뒀다가(`attachedModel` 딕셔너리)
  `DetachLeftHandItem()`을 호출하도록 보완.
- **오프셋도 같이 조정**: `handCandleOffset`/`handItemOffset`의 앞쪽(Z) 성분을 0.03~0.06 → 0.10으로 늘려서, 팔이
  굽은 자세를 기준으로 손 앞쪽에 더 확실히 떨어져 보이게 함(굽은 팔 기준으로 다시 맞춘 값 — 전 라운드의 "차렷
  자세" 기준 오프셋은 이 자세 변경 후엔 더 이상 맞지 않았음).
- 검증: Play 모드에서 호스트 자신의 아바타 렌더러를 진단용으로만 강제로 보이게 켜고(`ShadowCastingMode.On`,
  최종 반영 전 되돌림), 촛불·인형을 양손에 쥐어준 뒤 Scene 뷰로 직접 확인 — 양손 모두 몸에서 떨어져 앞으로 내민
  자연스러운 "드는" 자세로 들려 있는 것을 확인. 콘솔 에러 0건, 이번 라운드는 씬 변경 없이 스크립트 3개만 수정.
  **다시 2인 플레이로 확인해 주시면 오프셋을 마저 미세 조정하겠음.**

## Study 벽지 빈 곳(비밀방 문) — 몰딩이 벽 뒤에 묻혀 있던 버그 (2026-10-26, 다섯 번째 라운드)
사용자가 실제 스크린샷(저택 125, 서재에서 양초 든 채)으로 "Study 벽지가 비어있는 곳 — 비밀방으로 통하는 문인데
다른 벽과 차이 안 나게 덮어달라"고 위치를 짚어줌. `Ravenwood_LGH/Floor1/Walls/SecretPanel_A_StudyBookcase`
(책장 사이, 비밀방으로 통하는 문짝 자리)를 직접 찾아 조사함.

**원인**: 이 패널에는 `PanelDress_LGH`(벽지 벗겨짐 데칼 `M_WP_Peeling_LGH` + 호두나무 몰딩 `M_VicWalnut_LGH`)
라는 꾸밈 장식이 이미 있었지만, **몰딩이 실제로는 벽 패널 몸체(두께 0.2m, 방쪽 면 X=59.1) 뒤쪽인 X≈58.85
자리에 묻혀 있어서 렌더러가 있어도 전혀 보이지 않는 상태**였다(`Physics`가 아니라 `Renderer.bounds`로 직접
X 범위를 대조해서 찾음 — 같은 자리에 완전히 가려진 오브젝트라 레이캐스트/겹침 검사로는 안 잡히고, 각 메쉬의
월드 바운드를 직접 비교해야 드러남). 그 결과 패널은 이웃 벽 조각들(`F1_Int_Study_E_9_Seg0/Seg1/Lintel0`)과
똑같은 기본 벽 재질(`M_Wallpaper`, 실제로는 민무늬 석고 질감 `T_Plaster_LGH`)만 보이는 밋밋한 사각형으로
남아 있었다 — 집 전체 벽 421개가 전부 이 똑같은 민무늬 재질을 쓰므로(조사로 확인), 재질 자체는 틀리지 않았지만
문틀 몰딩 하나 없는 매끈한 사각형이라 유독 "구멍처럼" 눈에 띄었던 것.
- 수정: 호두나무 몰딩(`M_VicWalnut_LGH`)만 월드 X로 +0.275m 옮겨서 패널 몸체 방쪽 면(X=59.1) 바로 앞으로
  꺼냄 — 이제 책장들과 어울리는 나무 몰딩 테두리가 실제로 보인다. 벽지 벗겨짐 데칼(`M_WP_Peeling_LGH`, 어두운
  적갈색 바탕에 회색 벗겨짐 무늬)은 **일부러 그대로 묻힌 채로 둠** — 사용자가 원한 건 "티 안 나게 숨기기"이지
  "여기 벗겨져서 수상하다"는 티를 내는 게 아니라서, 눈에 띄는 손상 무늬를 새로 드러내면 오히려 역효과이기
  때문(필요해지면 쉽게 되돌릴 수 있도록 삭제하지 않고 숨긴 채로만 둠).
- **이번 세션의 도구 문제 기록**: `capture_scene_view`/`capture_game_view` 스크린샷 도구가 이번 조사 중
  여러 번 똑같은(캐시된) 이미지를 반환해서(오브젝트를 껐다 켜도, 카메라를 옮겨도 전혀 안 바뀜 — 심지어 눈에 뻔히
  보이는 책장을 꺼도 그대로였음) 시각 확인이 불가능했다. 그래서 이번 조사는 전부 `Renderer.bounds` 월드 좌표
  직접 대조(수치 기반)로만 진행했고, 사용자가 보내준 실제 게임 스크린샷만 신뢰할 수 있는 시각적 근거로 썼다.
  **다음에 이 환경에서 Scene/Game 뷰 캡처가 필요하면, 먼저 아무 변화나 하나 주고 캡처해서 실제로 갱신되는지부터
  확인할 것** — 이번처럼 캐시된 그림을 실제 결과로 착각하면 안 됨.
- **부수적으로 발생했다가 되돌린 실수**: 조사 중 카메라(`Menu_LGH/MenuCamera`) 회전과 `TitleCanvas`를 임시로
  건드렸는데, 되돌리는 과정에서 미세한 부동소수 오차(쿼터니언 부호 반전)와 `TitleRoot/ButtonPanel`의 버튼 3개
  (`StartButton`/`OptionsButton`/`QuitButton`) RectTransform이 0이 아닌 값으로 구워지는 부작용이 생겼었음 —
  저장 전 `git diff`로 발견해 정확한 원래 값으로 되돌린 뒤 다시 저장함(이 버튼들이 `VerticalLayoutGroup`으로
  구동돼 평소 0으로 직렬화되는 건 2026-10-23 "GIM-004 Develop 통합" 섹션에 이미 기록된 정상 동작 — 거기서
  벗어나 실제 값이 박히면 오히려 그게 이상 신호임).
- 검증: 몰딩 이동 후 `Renderer.bounds`로 호두나무 조각의 X 범위가 패널 몸체 방쪽 면 앞([59.1, 59.15])으로
  정확히 나왔는지 재확인. `Tools/Network/Validate Scene NetworkObjects`는 이 변경과 무관(순수 장식 트랜스폼).
  저장 후 `git diff`로 의도한 변경 1줄(호두나무 `m_LocalPosition`)만 남았는지 확인.
- **한계**: 위에서 설명한 스크린샷 도구 문제로 **이번 수정의 실제 비주얼(몰딩이 책장과 잘 어울리는지, 여전히
  "빈 곳"처럼 보이진 않는지)은 직접 확인하지 못했다** — 다음에 실제로 플레이해보고 스크린샷을 보내주시면
  확인하겠음.

## Study 벽지 빈 곳 후속 — 남색 벽지 패치로 실제로 덮기 (2026-10-26, 다섯 번째 라운드 후속)
사용자가 자신의 Unity 에디터 Scene 뷰를 직접 캡처해서 보내줌 — 몰딩은 잘 붙었고, 주변 벽이 실제로 **남색 바탕에
금색 문양이 있는 벽지**(`T_WP_Navy_LGH`)로 덮여 있는 게 명확히 보임(이전 라운드에서 내 `capture_scene_view`
도구가 캐시된 그림만 반환해서 "이 패턴이 진짜인지 캐시 아티팩트인지" 확신 못 했던 부분이 이걸로 해소됨 —
처음 봤던 패턴은 실제였고, 그 이후 호출들만 캐시에 걸렸던 것). 사용자가 "벗겨진 것처럼 보이는(민무늬로 비어
보이는) 부분도 사방 벽지와 똑같이 덮어달라"고 요청.

**원인**: 집 전체 벽 밑바탕(`M_Wallpaper`, 민무늬 석고)과는 별도로, `VictorianInterior_LGH/Int_Paper_Navy`
(재질 `M_WP_Navy_LGH`, 건물 전체를 가로지르는 거대한 단일 장식 메쉬 하나)가 이 방 벽 위에 남색 벽지 무늬를
입히고 있었다. 이 메쉬의 정점을 직접 뒤져보니(`Physics`가 아니라 메쉬 정점 좌표를 월드로 변환해서 직접 대조),
**비밀문 자리(X=59.13, Z 3.2~5.2, Y 1.01~2.20)에 정확히 맞는 사각형 구멍이 뚫려 있었다** — 즉 문이 열고 닫혀야
하는 자리라서 저택을 만들 때 처음부터 벽지에 구멍을 내 둔 것이었고, 그 자리를 채우는 역할은 `SecretPanel_A_
StudyBookcase`(민무늬 석고)와 `PanelDress_LGH`(몰딩+벗겨짐 데칼) 몫이었는데 벽지 색을 못 맞추고 있었던 것.
- **벽지 데칼(`M_WP_Peeling_LGH`)을 재활용하지 않은 이유**: UV가 극단적으로 촘촘하게(패널 2m에 U값 범위 73↔거의
  노이즈처럼 뭉개짐) 구워져 있었고 색도 적갈색 "벗겨짐" 전용이라, 이 방의 진짜 남색 벽지와는 아예 다른 무늬였다.
  손대지 않고 계속 숨겨둔 채로 둠(이전 라운드와 동일 판단: "벗겨짐"을 보여주면 오히려 티가 남).
- **수정**: 새 평면 메쉬(`WallpaperPatch_Navy`, `SecretPanel_A_StudyBookcase`의 자식)를 코드로 직접 만들어 그
  구멍 자리에 꽂아 넣음. UV는 추측하지 않고, **`Int_Paper_Navy` 메쉬가 구멍 테두리에서 실제로 쓰고 있는 UV
  좌표를 그대로 읽어와서**(네 귀퉁이: (59.13,1.01,3.20)→uv(3.56,1.12), (59.13,2.20,3.20)→uv(3.56,2.44),
  (59.13,1.01,5.20)→uv(5.78,1.12), (59.13,2.20,5.20)→uv(5.78,2.44)) 새 패치에 그대로 옮겨 적었다 — 이러면
  이음매에서 무늬가 어긋나지 않고 주변과 자연스럽게 이어진다(짐작으로 타일링 값을 정하지 않고, 이미 맞는 걸로
  확인된 기존 메쉬의 실측값을 그대로 재사용한 것).
- **이 계층의 로컬→월드 X/Z 부호 반전 재확인**: `Ravenwood_LGH` 부모 체인에 걸린 180도 회전 때문에(이전
  KeyBox/호두나무 몰딩 때와 같은 함정), 새로 만든 메쉬의 로컬 좌표를 그대로 쓰면 X 오프셋도 Z쪽 UV 배정도
  둘 다 반대로 나왔다 — 직접 만든 정점을 `transform.TransformPoint()`로 월드 좌표를 재확인해서 실제 어느 쪽이
  어느 UV를 가져야 하는지 역산해서 바로잡음(부호를 미리 가정하지 않고 항상 변환 결과로 검증).
- **앞/뒷면 둘 다 그리도록 처리**: 이 패치는 기존 벽 박스 메쉬(이미 뒤집힌 법선으로도 정상적으로 보이는 것이
  확인된)와 달리 완전히 새로 만든 메쉬라, 같은 부모 체인의 반전이 삼각형 감김 방향에 어떤 영향을 주는지 미리
  확신할 수 없었다. 대신 **삼각형을 양방향으로 다 넣어서(앞면+뒷면 둘 다) 백페이스 컬링으로 안 보이게 될
  위험 자체를 없앴다** — 이번에도 스크린샷으로 확인이 어려운 상황이라 "이론상 맞는 방향"에 기대지 않고 아예
  양쪽 다 그리는 쪽을 택함.
- 검증: `Renderer.bounds`로 패치가 정확히 X=59.13(몰딩과 같은 깊이), Y/Z가 구멍 자리와 일치하는지 확인. 씬
  저장 후 `git diff`로 새 메쉬(`WallpaperPatch_Navy_Mesh`)와 오브젝트(`WallpaperPatch_Navy`)만 추가됐는지
  확인(다른 오브젝트 변경 없음).
- **한계**: 이번에도 이 환경의 스크린샷 도구가 불안정해서 **실제로 무늬가 자연스럽게 이어지는지는 직접 못 봤다**
  — 좌표·UV는 실측값을 그대로 가져다 썼으니 이론상 맞물려야 하지만, 다음에 에디터에서 Scene 뷰로 직접 보거나
  플레이해서 스크린샷을 보내주시면 바로 확인하고 필요하면 미세 조정하겠음.

**바로 발견한 버그 — 벽이 심하게 빛나 보임**: 사용자가 에디터에서 바로 확인해보니 이 패치 부분이 심하게 밝게
빛나 보인다고 보고. 원인은 양면 다 그리려고 삼각형을 앞/뒤 두 방향으로 겹쳐 넣은 뒤 `Mesh.RecalculateNormals()`
를 호출한 것 — 같은 정점에 서로 반대 방향을 향하는 삼각형들이 겹쳐 있으니 노멀을 평균 내면 서로 상쇄돼
거의 0에 가까운 비정상 노멀이 나왔고, 그걸 정규화하면서 조명 계산이 깨져 과다 노출처럼 보였던 것. 수정:
`RecalculateNormals()` 대신 노멀을 **직접 고정값으로 지정**(`transform.TransformDirection()`으로 어느 로컬
방향이 월드 기준 방쪽(+X)으로 나오는지 먼저 확인한 뒤 그 값만 사용) — 양면 렌더링(삼각형 두 방향)은 그대로
유지하되, 노멀은 더 이상 지오메트리에서 계산하지 않고 항상 올바른 방쪽 방향 하나로 고정해 조명이 정상으로
돌아오게 함. 탄젠트도 같이 고정값으로 재설정(이 재질엔 노멀맵이 없어 실제 영향은 없지만 일관성을 위해).

## 모델링 필요 목록 — 가구+기믹 전수 조사 (2026-10-26, 여섯 번째 라운드)
"가구와 기믹 오브젝트 총합해서 모델링이 필요한 것들의 목록을 뽑아달라"는 요청으로, Develop 씬의 모든
`MeshFilter`를 코드로 직접 뒤져 **활성 상태(`activeInHierarchy`+렌더러 `enabled`)인 것만** Unity 기본
도형(Cube/Sphere/Cylinder/Capsule/Quad)을 쓰는 오브젝트를 전수 스캔함(10/23 "미사용 VARCO 에셋 조사"와
같은 방식이지만, 그때는 기믹만 봤고 이번엔 가구도 포함 + 비활성 잔재는 걸러냄). 과거 기록과 대조해 이미
해결된 항목(인형·배경카드·건반 문양 등)은 제외하고, 실제로 아직 원시도형인 것만 정리:

**가구(배경 소품) — 이번에 새로 발견**:
- **안뜰 분수**(`Courtyard/Fountain_Basin/Column/Water`) — 전부 원기둥 조합
- **러그 4곳**(`F1_Hall/Furn_Rug_Hall`, `F1_Parlor/Furn_Rug_Parlor`, `F2_Master/Rug`, `F2_Nursery/Rug`) — 전부 단순 색 입힌 납작한 박스
- **음악실 피아노 램프 하우징**(`OBJ_LAMP_STAGE/Housing`) — 전구(`Bulb`)는 이미 실제 모델로 교체됐지만 감싸는 받침/하우징은 여전히 큐브

**가구 — 기존에 이미 알려져 있던 것**:
- GIM-007 안방 미는 가구 몸체(`Furniture/Body`) — 전용 겉모습 프리팹(`Gim007_FurnitureVisual`)이 아직 안 만들어져 기본 큐브 유지 중

**기믹 오브젝트 — 기존 기록과 일치, 재확인됨**:
- 플레이어 상시 촛불(`HeldCandle`), PotionCraft 붉은 뿌리(RedRoot)·숙성기(Brewer)·쓰레기통(1·2층), GIM-005
  산울타리 미로 벽, 다락 별 상자 보상(황금상·현관 열쇠), 다락 자물쇠 장치(`FinalDevice_Dial`), PotionCraft
  분쇄기 가루 더미(Tint_Pile — 금색만 아직 전용 모델 없음), GIM-004 온실 중앙 선반·화분 받침대(크기 안 맞아
  보류 중), 아이방 그림액자 랙(`OBJ_CARD_RACK`) 몸체

**확인해보니 이미 해결돼 있던 것(과거 기록이 낡았던 부분)**:
- GIM-002 인형 선반(`OBJ_DOLL_RACK`)의 옛 `Shelf` 큐브는 비활성 상태로만 남아있고, 실제로는 `Model_Table`
  (진짜 모델)이 대신 쓰이고 있음 — 해결됨.
- 거울들(`F1_WC/Mirror`, `F2_Bath/MirrorCabinet`, `F2_Dressing/StandingMirror`), 주방 스토브, 초상화
  `Portrait_W3/W4`, 축음기 나팔(`Gramophone_Horn`) 등은 전부 **비활성 잔재**(옛 더미, 실제 화면엔 안 보임) —
  목록에서 제외함.

**검증**: 전부 Edit 모드에서 `activeInHierarchy`/`MeshRenderer.enabled` 둘 다 켜져 있는 것만 추려서 확인,
비활성 중복 오브젝트는 걸러냄. 씬 변경 없음(조사만 진행).

## 다락 진입 방식 변경 — 계단 → 사다리 (2026-10-26, 일곱 번째 라운드)
"LGH씬에서 Attic 올라가는 방식이 계단에서 사다리로 바뀌었다, Develop에도 반영해달라"는 요청. LGH.unity를
additive로 열어(읽기만 하고 저장 없음, 끝에 닫음) 새 구조를 확인함: 기존 `Ravenwood_LGH/Floor2/Stairs/
AtticStair`는 **비활성 상태로 남겨둠**(삭제 안 함, LGH님이 되돌릴 경우 대비), 그 대신 씬 최상위에 새
`AtticLadder_LGH`(바닥·천장 구멍 패치 + 호두나무 액자 테두리 + 레일 2개·가로대 15개로 만든 사다리 +
`LGH.Ladder` 컴포넌트가 붙은 오르는 영역(`ClimbZone`) + 잠긴 다락방 뚜껑문(`AtticTrapdoor`, `InteractableDoor`,
`Item_AtticKey`로 잠금 해제))가 추가돼 있었음. `Ravenwood_LGH`의 월드 좌표가 LGH.unity·Develop 둘 다 동일해서
(68,0,-16) 좌표 변환 없이 그대로 가져올 수 있었음.

- 이 구조 전체(원시도형 30여 개 조합)가 **프리팹이 아니라 LGH님 씬에만 있는 오브젝트**였어서, 이번에 처음으로
  `Assets/Objects/LGH/AtticLadder_LGH.prefab`로 저장해 공유 프리팹화함(LGH.unity 쪽 인스턴스는 전혀 건드리지
  않음 — `SaveAsPrefabAsset`은 원본을 프리팹에 연결하지 않고 그냥 복제본을 에셋으로 저장하므로 LGH.unity가
  더러워지지 않음, 저장 후 `isDirty=False` 재확인). Develop에는 이 프리팹을 씬 최상위에 인스턴스화하고 기존
  `AtticStair`는 똑같이 비활성화만 함.
- **뚜껑문이 요구하는 `Item_AtticKey`가 숨어있던 곳도 같이 반영**: 자물쇠를 풀지 않으면 사다리를 타도 다락에
  못 들어가므로(뚜껑문이 잠겨 있음), 열쇠가 어디서 나오는지 확인이 필요했음 — LGH.unity에서 `StarLockBox_LGH`
  (별 자물쇠 상자, Develop에도 이미 있는 공유 프리팹)의 자식으로 `Item_AtticKey`(열쇠, WorldItem)와
  `Note_KeySecret`(쪽지, `PaperNote` — "열쇠를 찾았다고 알릴지 숨길지" 둘 중 하나를 고르는 선택형 쪽지, 동료에게
  알리면 `LockBoxNetworkSync.Announce()`로 전체 공지)가 **공유 프리팹 자산(`StarLockBox_LGH.prefab`)에는 반영
  안 되고 LGH님 씬 인스턴스에만 직접 추가**돼 있었음 — `ItemNetworkSync`/`LockBoxNetworkSync` 때와 똑같은 유형의
  함정. 두 자식 다 `StarLockBox_LGH.prefab` 자산에 추가하고(상자 열리기 전엔 비활성), Develop의 기존
  `StarLockBox_LGH` 씬 인스턴스에서 `StarLockBox.revealOnOpen` 배열(2칸, 둘 다 씬 전용 참조라 프리팹에는 저장
  안 되고 인스턴스마다 따로 연결해야 함)에 이 둘을 연결함 — 안 하면 상자를 열어도 영원히 안 나타남.
- `PaperNote`/`InteractableDoor`/`Ladder`는 전부 이미 존재하는 공용 스크립트라 새로 작성한 코드 없음(순수
  조립 작업).

**검증**: Play 모드 `StartHost()`에서 호스트로 전체 흐름 확인 — `StarLockBox.ResetForNewRoom()`으로 초기화 후
`Apply()`를 각 사분면 코드만큼 직접 호출해 열기(참고: `Press()`는 네트워크 경로라 즉시 반영 안 됨 — 직접
`Apply()`를 써야 그 자리에서 확인 가능, 처음에 `Press()`로 시도했다가 상태가 꼬여서 `ResetForNewRoom()`으로
되돌리고 `Apply()`로 재시도함) → `Item_AtticKey`/`Note_KeySecret` 둘 다 활성화 확인 → `ItemNetworkSync.
RequestPickup()`으로 열쇠 습득 → `PlayerInventory.HasItem` true 확인 → `AtticTrapdoor.Interact(pos, inventory)`
호출 → `locked=False, IsOpen=True`로 잠금 해제+열림 확인(열쇠는 `consumeKey=False`라 안 없어짐) → `Ladder.
Interact()`로 `FirstPersonController.Climbing`이 사다리로 바뀌는 것 확인 → `Note_KeySecret.Interact()`로
`PaperNote.Current`가 이 쪽지로 바뀌는 것(쪽지 읽기 UI 열림) 확인. `Tools/Network/Validate Scene NetworkObjects`
문제 없음(이 오브젝트들은 네트워크 오브젝트가 아니라 영향 없음 — 문·상자·사다리 전부 기존 `DoorSync_LGH`가
자동으로 찾아 동기화하는 방식 그대로). Play 모드 종료 후 열쇠·쪽지·상자 상태가 전부 기본값(닫힘/비활성)으로
깨끗이 되돌아온 것 확인하고 저장.
**검증 한계**: 전부 메서드 직접 호출로 확인한 것이라, 실제 E키로 사다리를 오르내리는 손맛, 별자물쇠를 실제로
돌리는 입력 경로, 두 사람이 함께 쪽지의 "비밀/공개" 선택을 주고받는 것은 다음 2인 플레이테스트에서 확인 필요.

**→ 되돌림 (같은 날, 사용자 요청)**: 사용자가 이 라운드 전체를 되돌려달라고 요청해 바로 위 다락 사다리
기능을 전부 제거함. Develop 씬에서 `AtticLadder_LGH` 인스턴스를 삭제하고 `AtticStair`를 다시 활성화, 새로
만들었던 `Assets/Objects/LGH/AtticLadder_LGH.prefab`도 삭제. `StarLockBox_LGH.prefab`에 추가했던
`Item_AtticKey`/`Note_KeySecret`은 `git checkout`으로 커밋된 원래 상태로 되돌리고(이 파일은 이번 라운드
전까지 이번 세션에서 전혀 건드리지 않았던 파일이라 통째로 되돌려도 안전했음), Develop 씬의 `StarLockBox_LGH`
인스턴스에 남아있던 `revealOnOpen` 오버라이드도 `PrefabUtility.RevertPropertyOverride()`로 깨끗이 제거함
(단순히 배열 크기를 0으로 다시 써넣으면 "값은 같지만 오버라이드가 새로 기록되는" 상태가 되어 diff가 지저분하게
남았음 — 진짜 되돌리려면 오버라이드 자체를 지워야 함). **주의**: `Assets/Scenes/Develop.unity`는 같은 세션에서
벽지 패치·호두나무 몰딩·쓰레기통 모델 교체 등 무관한 다른 수정도 섞여 있었던 파일이라, 이 파일은 `git checkout`
하지 않고 Unity 에디터에서 사다리 관련 오브젝트만 선택적으로 되돌렸음(통째로 되돌리면 관련 없는 다른 작업까지
같이 날아갈 뻔함) — 저장 후 `git diff`에 `AtticLadder`/`AtticStair`/`revealOnOpen` 관련 줄이 하나도 안 남은
것으로 확인함. 다락방은 다시 기존 계단으로 접근한다.

## 쓰레기통 모델 반영 — PotionCraft 1층·2층 (2026-10-26, 여덟 번째 라운드)
KWJ 브랜치 머지로 `Prop_TrashBin.fbx`(+프리팹)가 들어옴 — "모델링 필요 목록"에 있던 항목 중 하나. 과거
`_NeedsCollider/Prop_TrashBin.prefab`이 원본 FBX가 삭제돼 깨져 있던 적이 있었는데(10/22 기록), 이번 머지로
FBX가 같이 들어와서 **정상적으로 로드됨**을 먼저 확인하고 진행함.

- **임포트 주의점 발견**: 이 프리팹의 메시 노드 자체의 `localPosition`이 `(5715, 0, -16551)`처럼 터무니없이
  큰 값으로 들어있었음(아티스트가 원점으로 안 옮기고 내보낸 FBX export 실수로 추정) — 하지만 `mesh.bounds`
  (정점 데이터 자체)는 `(0,0,0)` 중심에 ±0.35 반지름으로 정상이었음. **트랜스폼 값은 무시하고 메시/머티리얼
  데이터만 뽑아 쓰면 문제없다**는 걸 확인 — 다른 VARCO 소품 교체 때처럼 "자기 자신의 MeshFilter/MeshRenderer만
  교체" 방식으로 진행.
- `PotionCraft_F1/Station_Trash`(1층 주방)·`PotionCraft_F2/Station_Trash`(2층 손님방1) 둘 다 자식 `Bin`
  (원기둥 더미 + `CapsuleCollider`)의 메시·머티리얼을 새 모델로 교체, 회전은 VARCO 표준 보정(270,0,0), 크기는
  실제 치수 그대로(scale=1) 사용. 안쪽 더미 원판(`Inside`)은 새 모델 자체에 내부가 있다고 보고 렌더러만 꺼서
  숨김(콜라이더·오브젝트는 남김, 기존 "숨기기" 관례 그대로).
  - **같이 고친 버그**: `Bin`의 `CapsuleCollider`(radius 0.5 / height 2)는 옛 더미의 축소된 트랜스폼
    (scale 0.5, 0.3, 0.5)을 전제로 맞춰둔 값이었음 — 메시만 바꾸고 트랜스폼 스케일을 1로 되돌리면서 그대로
    뒀다면 콜라이더가 실제 모델보다 훨씬 커져서(반지름 1m) 판정 범위가 맞지 않았을 것 — 직접 잡기 전에 발견해
    `radius=0.35, height=0.7`로 새 모델 크기에 맞게 다시 계산해 바꿈. 바닥에 묻히지 않도록 `Bin`의 로컬 Y도
    0.30→0.35로 재조정(새 모델 높이가 옛 원기둥보다 살짝 커서 바닥 아래로 파고들던 것 보정).
- `PotionCraft.TrashStation` 스크립트는 자식 이름이나 크기를 전혀 참조하지 않는 순수 레이캐스트 기반
  상호작용이라 스크립트 변경 없이 교체만으로 끝남.
- **같이 들어온 다른 모델(이번엔 손 안 댐)**: 같은 머지로 `Prop_GoldStatue.fbx`(다락 보상 황금상),
  `Prop_Key_Escape.fbx`, `Prop_AstroTelescope.fbx`, `Prop_WeightPlate.fbx`(GIM-007 발판), `Prop_TreasureBox.fbx`,
  `Furn_HeavyDresser_Single/Wide.fbx`(GIM-007 가구 후보로 보임)도 같이 들어와 있음 — "모델링 필요 목록"의 다른
  항목들과 연결될 가능성이 높아 보이니, 다음에 확인해서 반영할지 결정하면 좋을 것 같음.
- 검증: 월드 바운드 재계산으로 바닥 높이·콜라이더 크기 정합 확인(`renderer bottom Y` vs 바닥 Y 오차 4mm 이내
  둘 다), 콘솔 에러 0건. 씬 저장 후 `git diff`로 의도한 변경만 반영됐는지 확인.
- **한계**: 이번에도 스크린샷 도구 상태가 불안정해 실제 모양을 직접 보지 못함 — 치수·바닥 정렬은 수치로
  검증했지만, 실제로 방 안에서 봤을 때 비율이 자연스러운지는 다음 확인 필요.

## GIM-007 → 다락 접근 연결 (2026-10-26, 아홉 번째 라운드)
사용자가 기믹 설계를 알려줌: "둘이서 발판으로 가구를 밀면 다락방이 열리고, 다락방에서 별자리 기믹(별 자물쇠)을
한 뒤 탈출한다." LGH 씬의 건물 개선사항·다락방 형태는 가져오되, 기믹 연결은 Develop 기준을 따르라는 요청.

**조사 결과 — 생각보다 이미 많이 완성돼 있었음**:
- **별자리 기믹(`StarLockBox`+`Telescope`)과 탈출 트리거는 이미 완전히 연결돼 있음**: `AtticTreasureFrontDoor.cs`가
  현관문이 열리는 순간 실제로 `GameFlowSync.Instance.RequestEscapeSuccess()`를 부른다(디버그용 키보드 트리거
  `DebugEscapeTrigger`와 공존하지만, 진짜 경로가 이미 살아있었음). 즉 "별 자물쇠 풀기 → 황금상+현관 열쇠 획득 →
  현관문 열기 → 탈출"까지는 이미 동작하는 상태였고, 이번에 손댈 필요가 없었음.
- **GIM-007의 문 열기가 데이터 기반으로 이미 일반화돼 있었음**: `Gim007Config.secretDoors`가 배열이라
  `{doorName, openFrom}` 항목을 추가하기만 하면 `Gim007Controller.ResolveDoors()/ApplyDoors()`가 이름으로 문을
  찾아 똑같이 잠그고 열어준다 — 코드 수정 없이 설정 자산만 바꾸면 되는 구조였음.
- **빠진 건 "다락 입구에 잠글 문 자체"뿐**: 기존 `AtticStair`는 그냥 걸어 올라가는 디딤판뿐이라 애초에 잠글 대상이
  없었음(바로 전 라운드에서 되돌린 사다리+뚜껑문 조합도 이제 없음).

**조사 중 LGH 씬에서 발견했지만 반영 안 한 것**: 다락에 권총(`Item_FlintlockPistol`)+쪽지가 새로 놓여 있고,
플레이어 프리팹의 `PistolLoader.cs`(장전 → 정문이 열리면 발사 가능 → 상대를 쏘면 "배신 엔딩")가 이미 완전히
연결돼 있었음. 사용자에게 확인한 결과 **이 기획은 폐기됐다고 해서 반영하지 않음** — `PistolLoader.cs`는 건드리지
않았고(플레이어 프리팹에 이미 붙어 있지만 권총 자체를 주울 수 없으니 평소엔 발동 안 함), 권총/쪽지 오브젝트도
Develop에 가져오지 않음.

**이번에 추가한 것 — `Door_Attic`**:
- 1층 서재 문(`Door_Linen`, 열쇠로 잠기는 0.98m 단일문)을 복제해 `Door_Attic`으로 만들고 `keyItem`을 비워
  열쇠 없이 GIM-007로만 잠기게 함. `Ravenwood_LGH/Doors/Floor2` 밑에 배치.
- `Gim007Config.asset`의 `secretDoors`에 `{doorName:"Door_Attic", openFrom:(39.15,3.90,7.50)}` 항목을 추가(기존
  `Door_Master_Dressing` 항목은 그대로 둠 — 이제 완료 시 두 문이 같이 열림). 완료 문구(`msgComplete`)도 특정 방
  이름 대신 다른 다중 문 기믹들과 같은 관용구("어딘가에서 문이 열리는 소리가 들렸다")로 바꿔서 두 문 다 아우르게 함.
- **배치 중 발견한 난제 — 이 자리가 극도로 좁음**: 계단 입구(1.3m 폭)는 한쪽엔 첫 계단 디딤판(세로 0.32m)이,
  반대쪽엔 드레스룸 붙박이 옷장(`Furn_Wardrobe_W5`, 세로 1m 이상)이 거의 바로 붙어있어서, 걸림 없는 순수 바닥
  틈이 세로로 **0.2m**뿐이었다(옷장 끝 Z=8.80, 계단 시작 Z=9.00). 문을 그 틈(Z=8.90)에 딱 맞춰 세우면 **닫혀
  있을 때는** 안 겹치지만, 여닫이문 특성상 조금이라도 돌아가면 부채꼴로 폭이 넓어져서 각도를 아무리 줄여도(8도
  까지 줄여봐도) 계단 디딤판과 살짝 겹치는 걸 확인함 — 이 지점 자체가 여닫이문이 완전히 안 겹치게 설 수 있는
  공간이 아니었음. 옷장 쪽으로 열면 옷장(눈에 더 띄는 큰 가구)과 겹치고, 계단 쪽으로 열면 디딤판 모서리(작고
  바닥에 붙어 눈에 덜 띔)와 살짝 겹치므로, **계단 쪽으로 여는 방향을 선택**하고 다른 집 문들과 똑같이 90도로
  열리게 둠(각도를 억지로 줄여도 안 겹치는 지점이 없으니, 차라리 다른 문들과 똑같이 자연스럽게 열리는 쪽을
  택함). **이 틈 자체가 구조적으로 좁은 것이라 좌표 조정만으로는 완전히 해결이 안 됨 — 필요하면 옷장
  (`Furn_Wardrobe_W5`)을 살짝 옮기거나 문을 더 작게 다시 만드는 걸 고려할 수 있음**(이번엔 가구 배치를 임의로
  바꾸지 않기로 하고 문 쪽만 조정함).
- 검증: Play 모드 `StartHost()`에서 GIM-007 완료 전(`Door_Attic`/`Door_Master_Dressing` 둘 다 잠김) →
  `Gim007NetSync`의 private `authority` 리플렉션으로 양쪽 손잡이를 잡히고 `Tick()`+`ReportAs(...,true)`를 반복해
  `IsComplete=True`까지 진행 → 두 문 다 `locked=True(열린 채 고정), IsOpen=True`로 정상 전환 확인. `Tools/Network/
  Validate Scene NetworkObjects` 문제 없음(이 문은 순수 `MonoBehaviour`라 네트워크 오브젝트 대상 아님). Play 모드
  종료 후 두 문 다 기본값(잠김/닫힘)으로 깨끗이 복구되는 것 확인하고 저장.
- **세션 중 참고**: 테스트 와중에 사용자가 직접 git으로 LGH 브랜치를 Develop에 머지함(csproj 충돌 포함) — 저장
  시점에 `git diff`가 전부 "추가"뿐이고 "삭제"가 하나도 없는 것으로 그 머지 내용과 이번 작업이 서로 안 깨졌는지
  확인함.
- **한계**: 계단 쪽으로 열 때 디딤판과 살짝 겹치는 건 수치로 이미 확인된 사실이고(완전히 못 없앰), 실제로 눈에
  얼마나 거슬리는지는 스크린샷 도구가 불안정해 직접 못 봤다 — 다음에 직접 보시고 거슬리면 알려주시면 옷장을
  옮기거나 문을 다시 만드는 식으로 다시 손보겠음.

## 다락 접근 형태를 계단 → 사다리로 재변경 (2026-10-26, 열 번째 라운드)
사용자가 "다락문(다락 접근)의 형태는 계단이 아니라 사다리로 가져오라"고 요청 — 직전 라운드에서 만든
`Door_Attic`(GIM-007로 잠기는 문)은 그대로 두고, 그 문 너머의 **오르는 방식만** 계단에서 사다리로 바꾸는 것.
이전에 되돌렸던 "다락 사다리" 라운드와 겉보기엔 비슷하지만, **이번엔 열쇠(`Item_AtticKey`)·뚜껑문
(`AtticTrapdoor`)을 전혀 가져오지 않았다** — 그때 되돌린 이유가 바로 이 열쇠가 공유 프리팹(`StarLockBox_LGH`)을
건드리는 것이었는데, 이제 잠금 자체를 `Door_Attic`(GIM-007)이 담당하므로 사다리 쪽엔 잠금 장치가 아예 필요
없어졌기 때문.

- LGH.unity를 다시 additive로 열어(읽기 전용, 끝에 저장 없이 닫음) `AtticLadder_LGH`를 확인한 결과 구조는
  이전과 동일: 기존 계단 전체 구간(Z 9.0~13.4)을 평평한 바닥으로 메우는 `FloorPatch_Attic`/`CeilingPatch_F2`,
  그 안쪽 끝(Z 13.4~14.6)에 작은 사각 구멍을 내는 `HatchTrim_N/S/W/E`, 그 구멍을 관통해 수직으로 오르는
  `Ladder`(레일 2개 + 가로대 15개) + `LGH.Ladder` 컴포넌트가 붙은 `ClimbZone`. **`AtticTrapdoor`(뚜껑문+열쇠
  하드웨어)만 복제본에서 빼고** `Assets/Objects/LGH/AtticLadder_LGH.prefab`로 다시 저장함(이전엔 이 프리팹
  자체를 삭제했었어서 새로 만듦).
- Develop에 이 프리팹을 씬 최상위에 인스턴스화하고, 기존 `AtticStair`(계단)는 다시 비활성화함(삭제 안 함).
- `Door_Attic`은 계단 입구(Z≈8.9)에 그대로 남아있고, 새로 메운 평평한 바닥(Z 9.0~13.4)과 사다리 구멍
  (Z 13.4~14.6)은 전부 그 문 안쪽이라 — 여전히 이 문 하나만 지나면 나머지는 다 열려 있는 구조. 복도가 이 문
  외에 다른 길이 없는 것도 다시 확인함(벽이 쭉 이어져 있어 이 문이 유일한 길목).
- 검증: Play 모드 `StartHost()`에서 `ClimbZone`의 `LGH.Ladder.Interact()`를 직접 호출해 `FirstPersonController.
  Climbing`이 사다리로 바뀌는 것, `StopClimb()`으로 다시 풀리는 것 확인. `Door_Attic`은 지난 라운드에서 이미
  검증된 그대로라 이번엔 재확인만(코드·위치 안 건드림). `Tools/Network/Validate Scene NetworkObjects` 문제 없음.
  저장 후 `git diff`에 의도한 프리팹 1개(`AtticLadder_LGH.prefab`, 신규) + 씬 변경(인스턴스 추가, AtticStair
  비활성화)만 있는지 확인.
- **한계**: 사다리를 오르내리는 실제 손맛(E로 잡기, W/S로 오르내리기)과 새로 메운 바닥·사다리 구멍이 방 안에서
  자연스러워 보이는지는 스크린샷 도구 문제로 직접 못 봤다 — 다음 플레이 때 확인 필요.

## Door_Attic·다락 펜스 비활성화 (2026-10-26, 열한 번째 라운드)
사용자가 실제로 보고 "드레스룸 옆에 남은 문짝(`Door_Attic`)이 어색하다"고 보고 — 지난 라운드에서 이미 기록해둔
"계단 쪽으로 열 때 디딤판과 살짝 겹친다"는 한계가 실제로 눈에 거슬린 것으로 보임. 또한 "다락으로 이어지던
계단이 없어졌으니 그 계단 난간(펜스)도 필요 없어졌다"고 지적.

- `Door_Attic`(지난 라운드에 새로 만든, GIM-007로 잠기던 문)과 다락의 계단 난간 `Rail_HatchE`/`Rail_HatchN`/
  `Rail_HatchW`(이전 열린 계단 구간을 보호하던 난간, 지금은 그 구간이 평평한 바닥으로 메워져 있어 보호할
  대상이 없음) 전부 **비활성화**함(삭제 안 함, 기존 관례 그대로).
- **주의**: `Door_Attic`을 비활성화하면서 콜라이더도 같이 꺼지므로, **지금은 GIM-007을 깨지 않아도 다락으로
  바로 걸어 들어갈 수 있는 상태**가 됨(막던 문 자체가 없어졌으니). `Gim007Config.secretDoors`의 `Door_Attic`
  항목 자체는 그대로 남겨둠(문이 나중에 다시 생기면 바로 작동하도록) — 비활성 오브젝트도 `FindObjectsByType(...
  Include)`로는 찾아지므로 경고 없이 조용히 "잠긴 채 숨어있는" 상태로만 남고 실제 동작엔 영향 없음.
  **다락을 다시 잠그려면 이 문을 대체할 다른 형태(문 디자인을 다시 만들거나 위치를 바꾸는 등)가 필요함 — 사용자
  확인 후 진행하겠음.**
- 검증: `Tools/Network/Validate Scene NetworkObjects` 문제 없음. 저장 후 `git diff`로 두 가지만 바뀐 것
  확인(`Door_Attic` 및 난간 3개의 `m_IsActive: 1→0`, 그 외 변경 없음). `ProjectSettings/GraphicsSettings.asset`
  등 일부 URP 설정 파일이 매번 "수정됨"으로 뜨는 건 실제 내용 diff가 비어있는 줄바꿈 표기 차이뿐 — 매번 확인하고
  있고 실제 변경 아님.

## 게스트에서 호스트가 든 물건이 안 보이던 버그 (2026-10-28) — NGO 함정 기록
- 원인: `NetworkManager.SpawnManager.GetPlayerNetworkObject(다른 사람 ID)`는 **서버에서만** 동작한다. 게스트에서는 에러 로그를 남기고 `null`을 돌려줘서, `HoldableNetworkSync.Apply`(왼손 물건 붙이기)와 `PotionCraftSync.BuildHeldVisual`(약품 재료 들기)이 호스트 플레이어를 못 찾고 조용히 끝났다. 호스트 혼자 테스트하거나 "호스트가 게스트 것을 볼 때"는 서버라서 멀쩡해 보였다.
- 수정: 모든 컴퓨터에 있는 `LGH.NetworkPlayer.All`에서 `OwnerClientId`로 찾는다(두 파일 모두). **다른 플레이어의 오브젝트를 찾을 때는 앞으로도 `GetPlayerNetworkObject`를 쓰지 말 것.** 약품 재료도 이제 상대 화면에서 아바타 왼손에 붙는다.
- 검증 방법(재사용 가능): 임시 게스트 봇이 든 테스트 빌드(`-guestbot` 인자로 스스로 `StartClient`, 호스트=에디터 `StartHost`, 127.0.0.1:7777)로 실제 두 번째 접속을 만들어 로그/스크린샷으로 확인했다. Relay 없이 직접 접속이 되므로 이 방식으로 "에디터 1개로는 못 보던" 게스트 쪽 동작을 볼 수 있다. 빌드는 `Builds/`(gitignore)에 만들고 끝나면 지울 것.

## LCW 사운드 연결 (2026-10-28)
`Assets/Sounds/LCW`의 효과음/엠비언스 61개를 상황에 맞게 연결함(LGH/KJH 폴더 사운드는 쓰지 않음). 구조:
- **`LcwSoundBank`**(`Assets/Resources/KSM/LcwSoundBank.asset`): 파일을 "상황 이름"(doorOpen, footstep, pianoDo …)으로 묶은 목록. 소리를 바꾸려면 이 에셋의 칸만 바꾸면 되고, 처음/재생성은 `Tools/KSM/Audio/Build LCW Sound Bank`(재실행 가능). 칸↔파일 짝은 `Assets/Scripts/KSM/Audio/Editor/LcwSoundBankBuilder.cs` 표 한 곳에만 있다. 긴 소리(엠비언스, 미로 배경음, 대기실 브금)는 자동으로 Streaming 로드.
- **`LcwSfx`**: 재생 도구(3D 풀 24개, 최대 거리에서 0이 되는 감쇠 곡선). **`LcwSoundDirector`**: 게임이 시작될 때 스스로 생기는 로컬 전용 오브젝트(씬/프리팹에 둘 필요 없음)가 상황을 지켜보다 소리를 낸다. 원칙: **상대 컴퓨터의 소리는 상대 컴퓨터가 낸다** — 문 열림/잠금 풀림/장치 상태처럼 모두에게 동기화되는 변화를 각자 보고 각자 재생(그래서 한 명이 문을 열면 둘 다 그 위치에서 듣는다). 플레이 단계가 아니거나 플레이 시작 후 1.5초 동안은 상태만 기억하고 소리를 내지 않는다(방 초기화·접속 시 소리가 와르르 나는 것 방지).
- **Director가 직접 내는 것**: 대기실 브금(타이틀 화면+대기실에서 한 곡이 끊기지 않고 이어지고 도입부부터 페이드 아웃, 일시정지로 위치 유지), 문 열림/닫힘/잠금 풀림(열쇠 문=열쇠 소리, 현관문=마지막 현관문 소리), 서랍, 발소리(움직인 거리로 한 걸음씩, 나+상대), 엠비언스(플레이 중), GIM-01 초상화 회전·고정(퍼즐이 풀릴 때 정답 액자들)·열쇠 등장, 인벤토리 줍기(읽을 수 있는 것=종이, 그 외=작은 금속), 쪽지 읽기, 공용 들기(인형/배경카드/화분 집기, `HoldableNetworkSync.ItemTaken` 이벤트), GIM-03 장치(`PotionStation.GetState()` 변화 감지 + 손에 든 물병 + 도르래 오르내림 + 제조 시작 시 조합 소리), GIM-04 누름판 눌림/풀림, GIM-05(미로 안 배경음, 시간 초과·재성장·완료 시 철창), GIM-07 가구 밀림(손잡이 0번=L, 1번=R), GIM-08 별 상자·망원경(들여다볼 때와 뗄 때 한 번씩). 한 줄 훅: 잠긴 문 시도(`PlayerInteractor` → `PlayDoorLocked`), 별 상자 누름(`StarLockBox.ApplyFeedback` → `PlayStarLockPress`, 번호키누르는소리1), GIM-07 키를 맞게 누름(`Gim007PushPlayer` → `PlayMinigamePress`).
- **KJH 컴포넌트의 소리 칸에 채운 것**(`Tools/KSM/Audio/Assign LCW Sounds To Scene`, 머지로 씬 값이 날아가면 다시 실행): GIM-02 건반 5음(해·구름·달·꽃·별 = 도·레·미·파·솔), 열쇠 꽂기 3곳, 태엽 손잡이(오르골), 인형 자리, 층별 스피커(무대·반주 오답=공통 오답 소리, 무대 정답·공연 시작·장면 성공=정답 전구 소리); GIM-04 선반 4곳 내려놓기와 오답; GIM-05 놋쇠 버튼·오답·패널 버튼 불빛(덤불 열릴 때)·덤불 8개·미로 오답; GIM-07 발판과 라운드 실패(미니게임 틀림); GIM-03 `PotionCatalog` 에셋(펑 터짐=공통 오답 소리, 최종 혼합 성공=조합 소리). **공통 오답 소리 `오답입력효과음`은 모든 오답에 공통 적용**(GIM-07만 자체 틀림 소리). GIM-07 발판의 문 소리 칸에는 무음(`Assets/Sounds/KSM/Silence_0.1s.wav`)을 넣었다 — 문이 실제로 열릴 때 Director가 한 번 내므로 중복 방지(칸을 비우면 코드 임시 소리가 난다).
- 보류(미적용): 번호키누르는소리2, 숫자자물쇠돌리기1, 촛불소리1. 소리 칸이 비어 있는 이벤트(GIM-02 완료·배경카드 자리, GIM-05 완료·미로 종소리, GIM-07 끌기·숨소리 등)는 KJH 코드의 임시 소리(`Gim002Tones` 등)가 그대로 난다. 어떤 LCW 소리를 쓸지 정해지면 `LcwSoundSceneAssigner`에 칸 한 줄을 추가하면 된다.
- 주의: `PlayerInventory`는 KJH 쪽 같은 이름 클래스와 겹쳐서 반드시 `LGH.PlayerInventory`로 쓸 것(새 코드에서 또 밟기 쉬움).

## 층별 지도 액자 (2026-10-29, Develop 씬)
- 1층 현관 홀(갤러리 쪽 벽, 위치 47.845, 1.65, 11.8)과 2층 계단 홀(드레스룸 쪽 벽, 40.155, 5.55, 4.4)에 `FloorMapBoard_1F/2F`를 배치함. 사용자가 이번 작업에 한해 Develop 씬 직접 배치를 허락함.
- 구성: VARCO `Prop_PortraitFrame`을 원래 방향(위쪽 걸이 문양이 위)으로 세우고, 가로 1.84배·세로 1.1배로 늘린 액자(1.35×1.10m)와, 앞면 그림 자리(메시를 레이캐스트해 잼, 원본 0.54×0.70m → 늘린 뒤 1.00×0.77m)에 붙인 지도 Quad. 처음엔 액자를 가로로 눕혔는데 문양이 옆으로 가서 사용자 요청으로 바꿈(같은 날). 가로 배율은 지도 비율(1.296)에 맞춘 값(세로 배율 × 1.672)이라 문양도 가로로 늘어나 있다. 평소에도 액자 안에 지도가 보이고(URP Lit + 약한 발광 0.1), 바라보고 E를 누르면 화면 가운데로 확대된다(0.25초). 닫기는 E/Esc/Q/우클릭. 보는 동안 내 이동만 멈추는 방식은 `Gim004Journal`과 같다.
- 지도 그림(2026-10-29 교체): 사용자가 준 낡은 종이 느낌 지도 이미지 그대로(`Assets/Images/KJH/FloorMap/FloorMap_1F/2F_Board.png`, 1536×1152, 빨간 점 = 현재 위치 표시로 남김). 그림 비율(1.333)과 액자 그림 자리(1.296)가 약 3% 달라 벽에서는 가로가 살짝 좁게 보이고, 확대 화면은 원래 비율로 나온다. 가져오기 설정에서 NPOT 크기 조정을 꺼 둠 — 켜면 정사각형으로 늘어나 확대 화면이 찌그러진다.
- 스크립트: `Assets/Scripts/KJH/FloorMap/FloorMapBoard.cs`(namespace `FloorMap`). 도구: `Tools/KJH/FloorMap/Build Prefabs`(그림 설정·머티리얼·프리팹 재생성, 씬 인스턴스에 자동 반영), `Tools/KJH/FloorMap/Place In Open Scene`(활성 씬에 같은 이름을 지우고 다시 배치). 그림을 바꾸려면 Board PNG만 교체하고 Build Prefabs를 실행한다.
- 검증: Play 모드에서 `StartHost()`로 두 액자를 띄워 봄. 카메라 레이가 액자에 맞고 안내 문구 "[E] 지도 보기"가 뜨는 것, `Interact()`로 열고 닫기, 이동 잠금·복구, 확대 화면 캡처, 콘솔 에러 0을 확인. 실제 E키 입력은 직접 플레이로 확인이 필요하다.

## 사용 가능한 Unity 전용 스킬
이 환경에는 `unity:` 접두사의 스킬들(unity-cli, ui, urp-postprocessing, physics-3d-collision 등)이 이미 연결되어 있습니다. 관련 작업을 할 때는 먼저 해당 스킬을 확인하세요.

## 감정표현 손(EmoteHand) — 촛불 든 오른손 대신 쓰는 몸짓 (2026-10-27)
LHJ님이 손가락 리깅이 들어간 `Hand_Wave_Rigged.fbx`를 올려서, "촛불을 쥐고 있어 못 움직이는 오른손 대신 감정표현(브이·따봉·인사·가리키기)을 할 수 있게 해달라"는 요청으로 구현함. 팀 결정: 애니메이션은 일단 제가 Unity에서 직접 키프레임(임시, 나중에 LHJ님 애니메이션으로 교체 예정), 상대 플레이어 화면에도 보여야 함(동기화 필요).

- **기존 시스템을 그대로 재사용**: `CoopDemo.CharacterAvatarModel`에 이미 있던 `AttachHandCandle`/`AttachLeftHandItem`(촛불·왼손 아이템을 "내 화면은 1인칭 그대로, 상대 화면은 3D 아바타 손에 옮겨 붙이기") 패턴을 그대로 복제해 `AttachFaceItem(Transform)`을 추가함 — 머리(`head`) 뼈 기준 소켓. `CharacterAvatar.OnNetworkSpawn()`의 기존 "`!IsOwner`면 촛불을 모델 손에 옮겨 붙인다" 블록 옆에 한 줄 추가해 얼굴 앞에도 옮겨 붙임.
- 새 컴포넌트 `Assets/Scripts/KSM/Avatar/EmoteHandView.cs`: `NetworkPlayer_LGH.prefab`의 `Main Camera` 밑에 있는 "EmoteHand"(Hand_Wave_Rigged 인스턴스 + Animator) 오브젝트에 붙어서 `Play(int gesture)`로 트리거를 걸고 일정 시간(2초) 후 자동으로 숨긴다. 평소엔 숨겨져 있다가(`Awake()`에서 자기 자신을 비활성화) 몸짓을 쓸 때만 나타남 — HeldCandle(항상 보임)과 달리 이쪽은 "쓸 때만" 보이는 게 다른 점.
- `CharacterAvatar.cs`에 `NetworkVariable<byte> emoteSignal`(interactCount와 같은 패턴: 아래 4비트=몸짓 번호, 위 4비트=카운터— 같은 몸짓을 또 눌러도 값이 바뀌게) + 공개 메서드 `RequestEmote(int gesture)` 추가 — 호출하면 내 화면엔 바로 재생하고 `emoteSignal.Value`를 바꿔서 상대 화면에도 같은 몸짓이 재생되게 한다(`OnEmoteSignalChanged`).
- **입력 방식(2026-10-27 후속, 숫자키 → 마우스 가운데 버튼 도넛 메뉴로 교체)**: 새 로컬 전용 컴포넌트 `Assets/Scripts/KSM/Avatar/EmoteWheelUI.cs` — 마우스 가운데 버튼을 누르고 있으면 화면 가운데에 도넛 모양 메뉴(인사/브이/따봉/가리키기 4조각, 텍스처는 코드로 그 자리에서 생성)가 뜨고, 마우스를 조각 쪽으로 움직였다가 떼면 그 몸짓이 `CharacterAvatar.RequestEmote()`로 재생된다(가운데 근처에서 떼면 취소). 누르는 동안은 `FirstPersonController.LookEnabled=false`로 시점 회전만 멈추고(이동은 그대로 됨) `Cursor.lockState`는 계속 Locked를 유지한 채 마우스 델타만 따로 누적해서 쓴다 — 커서를 풀면 `PlayerInteractor` 등 다른 "커서 풀림=일시정지" 판정과 겹쳐버리기 때문. `FirstPersonController`/`PlayerInteractor`/`Camera`/`AudioListener`와 똑같이 `NetworkPlayer.ownerOnly` 배열에 등록해서 상대 화면에서는 아예 꺼져 있다(`EmoteHandBuilder.BuildAll()`이 자동으로 등록).
- **임시 애니메이션 제작**: `Assets/Scripts/KSM/Avatar/Editor/EmoteHandBuilder.cs`(`Tools/KSM/EmoteHand/Build All`, 재실행 가능, AvatarBuilder와 같은 패턴 — `SetCurve(path, typeof(Transform), "localRotation.x/y/z/w", ...)`로 쿼터니언 커브를 직접 굽는다)가 `Assets/Animations/KSM/EmoteHand/`에 클립 5개(Idle/Wave/Peace/ThumbsUp/Point) + Animator Controller를 만들고, 플레이어 프리팹의 Main Camera 밑에 "EmoteHand" 오브젝트(Hand_Wave_Rigged 인스턴스 자체에 Animator를 직접 붙임 — 중간에 빈 껍데기를 하나 더 두면 클립의 뼈 경로가 어긋나서 손가락이 하나도 안 움직이는 버그가 났었음, 꼭 Animator가 붙은 오브젝트 = FBX 루트 자체로 할 것)를 배치한다.
  - 손가락을 구부리는 축은 이 프로젝트 전체(`CharacterAvatarModel.ApplyPose`의 팔꿈치·무릎 등)에서 쓰는 "관절 바로 위 뼈의 월드 right축으로 구부린다" 관례를 그대로 따름 — 브이(검지·중지 펴고 나머지 접음)·따봉(엄지만 펴고 나머지 접음)·가리키기(검지만 펴고 나머지 접음)는 이 방식으로 수치 검증까지 마침(Play 모드에서 본 뼈 각도를 직접 확인 — 접은 손가락은 또렷하게 각도가 바뀌고 편 손가락은 원래 각도 그대로임을 확인).
  - **인사(Wave)만 추정치**: 손가락은 원래 펼쳐진 기본 자세 그대로 두고 손목(`Hand` 뼈)만 좌우로 흔드는데, 정확히 어느 축으로 돌려야 "좌우로 흔드는" 모양이 나오는지는 스크린샷 도구 없이 확신할 수 없어서 `EmoteHandBuilder.cs`의 `waveAxisIsUp` 상수로 추정해서 넣어둠 — **실제로 보고 옆이 아니라 위아래로 까딱이면 이 상수를 뒤집어서 재빌드하면 됨**.
- **검증**: Play 모드 `StartHost()`로 확인 — `EmoteHandView`가 평소엔 비활성 상태이다가 `Play()` 호출 시 활성화+`holdSeconds` 후 자동 비활성화, Animator가 Idle→ThumbsUp 트리거 전환 정상 동작, 전환 후 해당 손가락 뼈들의 로컬 회전이 실제로 크게 바뀌고(엄지는 그대로) 유지되는 것까지 뼈 각도 직접 확인. `CharacterAvatarModel.AttachFaceItem()`도 예외 없이 소켓 생성+재부모화 확인. `emoteSignal`의 비트 패킹(몸짓 4비트+카운터 4비트)도 직접 값 대입으로 확인. `Tools/Network/Validate Scene NetworkObjects` 문제 없음, 콘솔 에러 0건.
- **검증(도넛 메뉴)**: Play 모드에서 리플렉션으로 `Open()`(→`LookEnabled=false` 확인)→가상 커서 값을 4방향(상/우/하/좌)으로 직접 넣어 `IndexOf()`가 각각 인사/브이/따봉/가리키기로 정확히 매핑되는 것(OnGUI가 그리는 위치와 일치하도록 각도 계산 일부러 검산함) 확인→오른쪽(브이)에서 `Close(true)` 호출 시 실제로 `EmoteHandView`가 활성화되는 것, 가운데 근처(데드존 안)에서 떼면 `emoteSignal`이 전혀 안 바뀌는 것(취소 확인)→`Close()` 후 `LookEnabled`가 다시 true로 복구되는 것까지 확인. `NetworkPlayer.ownerOnly` 배열에 FirstPersonController/PlayerInteractor/Camera/AudioListener와 나란히 등록된 것도 확인. 콘솔 에러 0건.
- **검증 한계**: 전부 호스트 1개 프로세스에서 메서드/리플렉션으로 확인한 것 — 실제 마우스 가운데 버튼을 누르고 떼는 입력 경로(`simulate_key`/포인터 시뮬레이션이 Game 뷰 포커스 문제로 불가, 기존에도 반복된 한계), 도넛 메뉴가 실제로 눈에 보기 좋게 그려지는지(크기·위치·반투명 정도), 그리고 가장 중요한 "실제로 내 화면엔 1인칭으로 보이고 상대 화면엔 상대 캐릭터 얼굴 앞에 보이는지"는 **다음 플레이테스트에서 꼭 확인 필요**(얼굴 앞 간격 `CharacterAvatarModel.faceItemOffset`(기본 0,-0.05,0.30), 도넛 반지름 `EmoteWheelUI.radius`(기본 110)도 그때 같이 눈으로 보고 다듬을 것). 인사(Wave) 동작의 손목 축 확인 요청은 여전히 유효함(위 항목 참고).

## 감정표현 손 — 크기·위치·축 버그 수정 + 엿 날리기 추가 (2026-10-27, 후속)
실제로 플레이해보니 "손바닥이 아래를 향한다", "시야를 가린다"는 피드백을 받아 재조사함. 이번엔 밝은 곳에
복제본을 두고 스크린샷으로 직접 확인(기존 "스크린샷 도구가 안 될 때가 있다" 문제는 **씬이 어두워서**였고,
밝은 테스트 환경에서는 `capture_scene_view`/`capture_game_view` 둘 다 정상 작동함 — 확인됨).

**진짜 원인 — "시야를 가리는 문제"는 손 크기 버그였음**: `Hand_Wave_Rigged.fbx`를 scale=1로 그대로 쓰면
실제 폭이 약 **1.2m**(사람 손의 6~7배)나 되는 거대한 손이었다 — 밝은 곳 단독 테스트에서는 카메라를 멀리
두고 봐서 못 알아챘는데, 실제 게임 카메라(FOV 60, 손이 얼굴 바로 앞 0.4~0.6m 거리)에서는 화면 절반을
가리는 수준이었다(스크린샷으로 직접 확인). **scale을 0.22로 낮춰 실제 손 크기(폭 약 18cm)로 맞춤.**
위치도 `(0.10,-0.22,0.55)`(얼굴 정면)에서 `(-0.20,-0.05,0.42)`(왼쪽 위, 시선 중앙은 가리지 않는 자리)로 옮김.
- **손목 흔들기(Wave) 축도 틀렸었음**: `EmoteHandBuilder.BuildAll()`이 클립을 구울 때 쓰는 임시 인스턴스
  (`inst`)에 실제 게임에서 적용하는 180도 Y회전을 걸지 않고 있어서, `hand.forward`/`hand.right` 같은 월드축
  계산이 실제 게임과 **다른 좌표계**에서 이뤄지고 있었다(손가락 구부리기는 어느 쪽이든 비슷해 보여서 못
  알아챘지만, 손목 흔들기는 축이 완전히 달라져서 거의 안 움직이는 것처럼 보였음). `HolderRotation` 상수를
  만들어 빌드 시점의 `inst`와 실제 플레이어 프리팹의 `EmoteHand` 둘 다 반드시 같은 회전을 쓰도록 고침.
  축 자체도 `hand.up`(옆으로 고개 돌리듯 — 아님) 대신 `hand.forward`(손목을 좌우로 까딱이는 진짜 손 흔들기)로
  교체 — 밝은 곳에서 ±80도 과장 테스트로 두 축의 차이를 직접 눈으로 비교해 확정함.
- **따봉(ThumbsUp)**: 엄지를 가만히 두면 접힌 네 손가락 옆에 완전히 파묻혀 안 보였음(스크린샷으로 확인) —
  엄지만 `hand.forward` 축으로 45도 더 돌려 옆으로 삐져나오게 고침(`PointThumbUp()`). 그래도 이 모델 자체가
  엄지가 짧고 단순한 뭉툭한 모양이라 다른 네 몸짓만큼 또렷하게 "엄지척"으로 안 읽힐 수 있음(모델 한계, 코드
  버그 아님) — 더 또렷하게 하려면 LHJ님 쪽에서 엄지 부분 모델링 보강이 필요할 수 있음.
- **엿 날리기(MiddleFinger) 추가**: 요청대로 5번째 몸짓 추가 — 엄지·검지·약지·새끼 접고 중지만 폄. 스크린샷으로
  확인한 5개 중 가장 또렷하게 읽힘.
- 도넛 메뉴(`EmoteWheelUI.cs`)도 4조각→5조각(90도→72도 간격)으로 일반화.

**검증**: Play 모드에서 실제 사용자 흐름 그대로(`TitleMenuUI.OnStartClicked()` → 호스트 시작 → `GameFlowSync`
Stage를 Playing으로 강제 → 낮 시간대로 강제) 재현한 뒤, 5개 몸짓 전부 `Screenshots/emote_*.png`로 실제
게임 화면을 캡처해 확인함 — 인사·브이·가리키기·엿은 또렷하게 읽힘, 따봉은 위 한계대로 다소 약함. 모두 왼쪽
위에 적당한 크기로 떠서 중앙 시야(계단 쪽)는 가리지 않는 것 확인.
**검증 한계**: 여전히 호스트 1개 프로세스 — 실제 상대방 화면(캐릭터 얼굴 앞 3인칭)에서 어떻게 보이는지,
그리고 실제 마우스 가운데 버튼 입력 경로는 다음 2인 플레이테스트에서 확인 필요.

**→ 같은 날 후속 — 손가락 몸짓 전부 제거, 인사만 유지**: 실제로 플레이해보니 코드로 관절을 돌려 구부린
손가락들이 부자연스럽게 일그러진다는 피드백. 팀 결정: 브이/따봉/가리키기/엿은 팀원이 Blender로 직접
애니메이션을 만들기로 하고, 그때까지는 손가락을 구부리지 않는 인사(손목만 흔듦)만 남김.
`EmoteHandBuilder.cs`가 이제 `Emote_Idle`/`Emote_Wave` 클립 2개만 만들고(재실행 시 나머지 4개 클립 파일은
자동 삭제), Animator Controller도 Wave 트리거 하나만 가짐. `EmoteHandView.cs`/`EmoteWheelUI.cs`의 몸짓
목록도 배열 하나(`GestureHash`/`Labels`)로 줄임 — **나중에 Blender 클립이 오면 그 배열들에 다시 추가하고
해당 .anim 파일을 같은 이름으로 `Assets/Animations/KSM/EmoteHand/`에 넣은 뒤 Build All을 다시 돌리면 됨**
(관절을 코드로 다시 구부리지 말 것 — 이번에 되돌린 이유가 바로 그 방식의 결과물 품질 문제였음).

**→ 2026-10-29: LHJ님 Blender 손 5종으로 교체(KJH 브랜치)**: LHJ 씬의 `HandEmote`(숫자키, 로컬 전용 테스트)에 있던 손 5종(`Assets/VARCO 3D/LHJ/Hands/Hand_*_Rigged.fbx` + 각 `_Anim.controller`, `Hand_White_Mat`)을 기존 네트워크 이모트에 연결함 — 도넛 메뉴가 인사/브이/중지/가리키기/엄지척 5칸. `EmoteHand`는 이제 빈 홀더이고 그 밑에 손 5개가 꺼진 채 들어 있어 `EmoteHandView.Play(i)`가 i번 손만 켠다(켜지는 순간 Swing 동작이 처음부터 재생, 3초 뒤 숨김). 손별 turnY·크기·좌우반전은 `EmoteHandBuilder.Gestures` 표에 **LHJ 씬 값 그대로** 들어 있다(사용자 요청으로 방향 보정 안 함 — 1인칭에서 가리키기는 손끝이 정면을 향한 덩어리, 엄지척은 옆모습으로 보이는 걸 확인했음, 보정하려면 표의 turnY만 바꾸고 Build All). 코드로 만들던 옛 클립 폴더 `Assets/Animations/KSM/EmoteHand/`는 삭제. 재구성은 `Tools/KSM/EmoteHand/Build All`(Develop.unity는 수정하지 않음, 플레이어 프리팹만 — Develop은 이 브랜치를 머지하면 자동 반영). **상대 화면 위치·방향 수정(같은 날)**: 상대 화면에서는 손이 머리 뼈 소켓에 붙으면서 로컬 회전이 초기화돼(1인칭의 Y180이 사라지고 머리 뼈 축도 제각각) 방향이 틀리고 몸 가운데에 파묻혀 보였음(2인 플레이 스크린샷). `CharacterAvatarModel.AttachFaceItem`→`AttachEmoteHand`로 바꿔 **왼손 위**(`emoteHandOffset` (-0.08,0.18,0.10), 모델 좌표)에 붙이고, 소켓은 플레이어가 바라보는 Y 방향으로 세우되 손의 로컬 회전(1인칭 Y180)·크기는 그대로 유지 → 월드에서 1인칭과 똑같은 방향이라 마주 선 상대는 정반대 면을 본다(인사=손바닥, 브이=손등). 옛 필드 `faceItemOffset/faceItemUpright`는 이름을 바꿔서 Avatar_Blue/Orange 프리팹의 옛 저장값은 무시됨. 검증: 호스트에서 내 아바타를 강제로 보이게 하고 정면에 임시 카메라를 둬서 캡처 — 왼손 위 위치, 인사 손바닥/브이 손등 확인, 1인칭 방향과의 각도 차 0도. 실제 2인 플레이로 최종 확인 필요.

## 병합 충돌 재발 방지 — UnityYAMLMerge 설정 (2026-10-27)
`LGH`·`KJH` 브랜치를 Develop에 머지하는 과정에서, 충돌을 실제로 해결하지 않고 **Git 충돌 마커(`<<<<<<<`/`=======`/`>>>>>>>`)가 그대로 박힌 채 커밋**된 적이 있었다(이미 원격에 푸시까지 된 뒤 발견). 영향받은 파일이 40개가 넘었음 — 컴파일이 깨지는 .cs 파일들, GUID가 마커째로 깨진 .meta 파일, `Develop.unity`를 포함한 씬·프리팹 YAML까지. 아마 머지 도구(VS/Rider/디폴트 Git)가 충돌을 띄웠는데 실제로 고르지 않고 그대로 스테이징·커밋한 것으로 보인다.

**원인**: `.unity`/`.prefab`/`.asset`/`.mat` 같은 Unity 파일은 사실 YAML인데, 평범한 텍스트 머지 도구는 이걸 줄 단위로만 비교해서 조금만 겹쳐도 충돌 마커를 박아버린다. Unity 에디터 안에 있는 `UnityYAMLMerge`라는 전용 도구를 쓰면 오브젝트 단위로 이해해서 실제로 안 겹치는 변경은 알아서 합쳐준다 — 이번 복구도 이 도구로 했다(격리된 git worktree에서 두 머지를 처음부터 다시 수행, `bd19c97` 커밋으로 정정).

**앞으로 이런 일이 재발하지 않도록, 팀원 각자 한 번만 설정하면 됨**:
1. 최신 Develop을 받으면 저장소 루트에 `.gitattributes`·`.gitconfig-unitymerge`·`Tools/git/unity-merge.sh`가 이미 들어있다.
2. 터미널(Git Bash)에서 저장소 루트 기준으로 딱 한 번만 실행:
   ```
   git config --local include.path ../.gitconfig-unitymerge
   ```
3. 끝. 이제 `git pull`/`git merge`가 씬·프리팹·머티리얼·애니메이션·애니메이터 컨트롤러 충돌을 만나면 자동으로 `UnityYAMLMerge`를 거친다. `Tools/git/unity-merge.sh`가 `C:/Program Files/Unity/Hub/Editor/<버전>/Editor/Data/Tools/UnityYAMLMerge.exe`를 자동으로 찾으므로, Unity Hub로 설치했다면 버전이 달라도 따로 손댈 것 없음.

**그래도 충돌 마커가 보이면(.cs 파일 등 텍스트 코드)**: 이건 UnityYAMLMerge 대상이 아니라 원래대로 수동 확인이 필요하다 — 절대 아무 쪽이나 골라서 그대로 커밋하지 말고, 양쪽 diff(`git show :2:<파일>` = 내 쪽, `git show :3:<파일>` = 상대 쪽)를 읽고 의미를 이해한 뒤 합칠 것. 커밋하기 전에 `grep -rn "^<<<<<<< " .`로 충돌 마커가 하나도 안 남았는지 항상 확인.

## 다음에 결정해야 할 것
1. 캐릭터 조작 방식 vs 디오라마 고정 시점 (프로토타입 필요)
2. 팀 공통 코딩 컨벤션, 공용 에셋/스크립트 배치 규칙
