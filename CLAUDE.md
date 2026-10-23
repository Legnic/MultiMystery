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
- 임시 숫자 글자는 깊이 검사 셰이더 `Gim005_WorldText`를 쓴다(기본 TextMesh 글꼴 머티리얼은 URP에서 벽 너머로 비쳐 보임). 글꼴 텍스처는 실행 중에 만들어지므로 에디터(비실행)에서는 숫자가 안 보이는 게 정상.
- 검증 한계: `StartHost()` 로컬 호스트로 입구 통과→타이머, 오답(-15초·3초 잠금), 진짜 길 개방, 시간 초과(흐려짐→시작점 복귀→재배정), 중앙 버튼→완료→철창 비활성화까지 확인(콘솔 GIM-005 에러 0). E키는 Game 뷰 포커스 문제로 `PlayerInteractor`가 조준한 대상의 `Interact()`를 직접 호출해 확인. **두 클라이언트 동시 테스트와 실제 걸어서 통과는 직접 플레이로 확인 필요.**

## 온실 화분 퍼즐 GIM-004 (2026-10-22, KJH 씬)
- 명세: `GIM-004_온실화분퍼즐v2.md`(이채원). 1층 온실 `F1_Conservatory`(x 55~68, z -16~-9)에 중앙 선반 + 화분 7개(A~G), 2층 서고 `F2_Library/ReadingTable` 위에 온실 기록일지. 정답 D-E-A-F(선반 정면 기준 왼쪽부터, `Gim004Config.rules.answer`에서 변경 가능).
- 결정(2026-10-22): 정원문 = `Door_Courtyard_L/R`(남쪽 복도→정원 미로 입구 양문), 정원 관리실 문 = `Door_MaidRoom`. 두 문은 완료 전까지 런타임에 잠그고 완료 시 연다. GIM-003 완료 전 온실·서고 문 잠금은 **기본 꺼짐**(`Gim004Config.lockEntryUntilPotionCraft`로 켤 수 있음). 기록일지는 E로 펼치고 들고 나갈 수 없음. 선반은 뒤쪽·양옆 난간, 정면이 온실 입구(서쪽)를 봄.
- LGH 맵 프리팹은 건드리지 않음: 문은 `Gim004Controller`가 이름으로 찾아(`completeDoors`, 부모 경로에 `Ravenwood_LGH` 포함) 매 프레임 상태에 맞게 잠그고, 완료 시 호스트가 `DoorNetworkSync.RequestInteract`로 연다.
- 스크립트 `Assets/Scripts/KJH/Gim004/`(네임스페이스 `Gim004`). 판정은 순수 C# `Core/Gim004Logic.cs`(asmdef `Gim004.Core`) + EditMode 테스트 23개(배치 기록 4문장의 해가 D-E-A-F 하나뿐인지도 검사). 네트워크는 GIM-005와 같은 구조(`IGim004Net`/`LocalGim004Net`/`Gim004NetSync`), `Gim004Sync.prefab`은 `DefaultNetworkPrefabs.asset`에 등록됨. 화분은 공용 들기 시스템(`HoldableItem`), 손은 동기화 안 함.
- 재생성: `Tools/KJH/GIM-004/Build All`(재실행 가능). **기능/겉모습 분리**: `Assets/Prefabs/KJH/Gim004/Visuals/`의 `Gim004_PotVisual_A~G`, `Gim004_ShelfVisual`, `Gim004_PotBenchVisual`, `Gim004_JournalVisual`은 "없을 때만" 만들어짐 → 모델 교체는 이 프리팹 안만 바꾸고 Build All 재실행(화분 콜라이더 재조정 + 기록일지 사진 재촬영). 임시 모델로 되돌리기는 `Rebuild Placeholder Visuals`. `Data/Gim004Config.asset`도 없을 때만 생성.
- 기록일지 사진 1~4(F·E·D·A)는 `Tools/KJH/GIM-004/Capture Journal Photos`가 실제 화분 겉모습 프리팹을 찍어 `Data/Photos`에 만든다(명세서 "사진은 게임 속 모델을 촬영" 원칙). 모델을 바꾸면 사진도 반드시 다시 찍을 것.
- **Develop 적용 방법**: `GIM004_Conservatory.prefab`(61.5, 0, -12.5), `GIM004_Journal.prefab`(62.55, 4.665, 3.5, 회전 y 180), `Gim004Sync.prefab`(최상위)을 프리팹 인스턴스로 두면 끝(Develop의 Ravenwood_LGH도 같은 좌표). 놓은 뒤 `Tools/Network/Validate Scene NetworkObjects`.
- 검증 한계: Play 모드 `StartHost()` 로컬 호스트에서 `Interact()` 직접 호출로 오답(D-F-A-E → 4개 원위치, 오답 1)·교체·빼기·정답(완료, 두 양문 잠금 해제+열림, 선반 잠금)·기록일지 열고 닫기까지 확인(콘솔 GIM-004 에러 0). **실제 E키 입력, 두 클라이언트 동시 플레이, 1층/2층 안내 문구·실패음 분리는 직접 플레이로 확인 필요.**

## 함께 미는 안방 가구 GIM-007 (2026-10-23, KJH 씬)
- 명세: `GIM-007_함께 미는 안방 가구.md`(김진호). 2층 안방 `F2_Master`(x 20~36, z 3~16, 바닥 y=3.9). 행동 동기화형: 두 사람이 서랍장 뒷면 손잡이 2개를 각각 잡고, 라운드마다 각자 화면에만 뜨는 W/A/S/D를 제한 시간 안에 둘 다 맞히면 한 칸 전진. 안방 가운데(x 29)에서 동쪽으로 8칸×0.5m 밀어 드레스룸 문 앞 무게 발판(x 33, z 12.9)에 올리면 완료.
- 결정(2026-10-23): 비밀문 = 기존 드레스룸 양문 `Door_Master_Dressing`(+짝 `_B`)을 이름으로 찾아 완료 전 잠금, 완료 시 열고 **열린 채로 잠가 고정**(`keepDoorOpenAfterComplete`). 사람이 발판에 올라가면 덜컥거리기만 하고 문은 안 열림(우회 방지). 오입력도 실패(`rules.wrongKeyFails`). 임시 가구 = 서랍장. 수치는 명세서 제안값(8회/1.5초/숨고르기 2초) — 전부 `Gim007Config.asset`에서 조절.
- 입력 지연 처리: 각 컴퓨터가 **자기 화면에 키가 뜬 순간부터** 제한 시간을 재서 성공/실패를 보고하고, 호스트는 제한 시간 + `latencyGraceSeconds`(0.35초)까지 기다린 뒤 안 온 쪽을 시간 초과로 처리. 손잡이 점유는 RPC 발신자 ID로 판정(먼저 도착한 쪽), 접속이 끊기면 호스트가 그 사람 손잡이를 놓는다(가구 위치 유지).
- 스크립트 `Assets/Scripts/KJH/Gim007/`(네임스페이스 `Gim007`). 판정은 순수 C# `Core/Gim007Logic.cs`(asmdef `Gim007.Core`) + EditMode 테스트 24개. 네트워크는 GIM-004/005와 같은 구조(`IGim007Net`/`LocalGim007Net`/`Gim007NetSync`), `Gim007Sync.prefab`은 `DefaultNetworkPrefabs.asset`에 등록됨. `Gim007PushPlayer`(로컬 전용, 프리팹 루트에 1개)가 잡는 동안 플레이어를 손잡이 뒤에 세우고 시점 고정·키 UI(OnGUI)·E로 손 떼기를 담당 — LGH 플레이어 프리팹 수정 없음.
- **이동 잠금 주의**: LGH `PistolLoader`가 매 프레임 `FirstPersonController.SpeedMultiplier`를 1로 되돌린다(`PistolLoader.cs:190`). 그래서 `IPlayerLock.SpeedMultiplier=0`만으로는 이동이 안 멈춘다 — GIM-007은 `LateUpdate`에서 플레이어를 손잡이 자리로 다시 옮겨 고정한다. 다른 기믹에서 이동 잠금이 안 먹으면 이 원인을 먼저 의심할 것.
- 재생성: `Tools/KJH/GIM-007/Build All`(재실행 가능). **기능/겉모습 분리**: `Assets/Prefabs/KJH/Gim007/Visuals/`의 `Gim007_FurnitureVisual`(피벗 바닥 가운데, +Z = 미는 방향/발판 쪽, -Z = 잡는 뒷면), `Gim007_PlateVisual`(내려앉을 부분 이름 `PlateTop`), `Gim007_ScratchVisual`(바닥 긁힌 자국)은 "없을 때만" 만들어짐 → 모델 교체는 이 프리팹 안만 바꾸고 Build All 재실행(가구 콜라이더·손잡이 위치가 새 모델 크기에 맞춰 다시 잡힘). 임시 모델로 되돌리기는 `Rebuild Placeholder Visuals`. 경로는 프리팹의 `Furniture/PathStart`·`PathEnd` 위치, 소리는 각 컴포넌트 AudioClip 칸(비우면 `Gim007Sounds` 임시 소리).
- 혼자 테스트: `GIM007_MasterBedroom`의 `Gim007Controller`에서 `Debug Fake Partner`를 켜면 네트워크 없이 Play할 때 가짜 동료가 반대쪽을 잡고 자동 입력한다(네트워크 게임 중에는 동작 안 함).
- **Develop 적용 방법**: `GIM007_MasterBedroom.prefab`(31, 3.9, 12.9, 회전 y 90), `Gim007Sync.prefab`(최상위)을 프리팹 인스턴스로 두면 끝(Develop의 Ravenwood_LGH도 같은 좌표·같은 문 이름). 놓은 뒤 `Tools/Network/Validate Scene NetworkObjects`. Develop에서 다른 기믹이 `Door_Master_Dressing`을 잠그거나 열고 있으면 충돌하니 먼저 확인.
- 검증 한계: Play 모드 `StartHost()` 로컬 호스트에서 확인함 — 핸들 조준 안내 문구, 혼자 잡기("꿈쩍도 하지 않는다", 손잡이 뒤 고정·시점 고정), 상대(클라이언트 ID 1을 판정 엔진에 직접 넣어 흉내) 잡기 → 라운드 성공·오입력 실패·로컬 시간 초과 실패, 도중 손 놓기(취소·칸 유지·"상대가 손을 놓았다"), 다시 잡고 완료(가구 발판 위, 발판 내려앉음, 자동 손 떼기, 양문 열림+고정, 완료 문구), 발판 위 사람 덜컥거림(문 잠김 유지), 이미 잡힌 손잡이 안내, 접속 끊김 해제, `Validate Scene NetworkObjects` 0건, 콘솔 GIM-007 에러 0. **실제 W/A/S/D·E 키 입력(Game 뷰 포커스 문제로 `simulate_key` 불가), 두 클라이언트 동시 플레이·지연 체감, 수치 튜닝은 직접 플레이로 확인 필요.**

## 다락 별 상자 보상 — 황금상 + 현관 열쇠 (2026-10-23, KJH 씬)
- 다락의 별 상자 `StarLockBox_LGH`(LGH `Ravenwood_LGH.prefab` 안, 월드 26.09, 9.115, 11.88) 안에 황금상과 현관 열쇠를 둔다. 둘 다 임시 큐브(모델링 예정).
- 결정(2026-10-23): 황금상은 E → **두 사람 모두에게** 획득 연출 → 연출이 끝나면 **집은 사람 인벤토리**에. 현관 열쇠는 E → 바로 인벤토리. 현관 양문 `Door_Front_L/R`은 이 열쇠로 열리고, 열리면 KSM `GameFlowSync.RequestEscapeSuccess()`(탈출 성공 → 엔딩)를 부른다. 새 아이템 `Item_GoldStatue`(`gold_statue`)·`Item_FrontKey`(`key_front`)는 `Assets/Prefabs/KJH/AtticTreasure/Items/`에 있고, LGH `ItemDatabase_LGH` 목록 **끝(6·7번)**에 추가함(기존 번호 유지).
- 스크립트 `Assets/Scripts/KJH/AtticTreasure/`(네임스페이스 `AtticTreasure`). `AtticTreasureSync`(NetworkBehaviour, `AtticTreasureSync.prefab`, DefaultNetworkPrefabs 등록)가 호스트 판정: 황금상 상태(InBox/Cutscene/Taken)와 열쇠 주움 여부를 NetworkVariable로 동기화. 연출 도중 집은 사람이 나가면 황금상은 상자로 돌아가고, 연출 종료 신호가 120초 넘게 안 오면 강제로 지급. 상자가 열려 있을 때만 집을 수 있다(`StarLockBox.IsOpen`을 이름으로 찾은 상자에서 읽음).
- **연출 연결 방법**: `AtticTreasureCutscene`(추상 클래스, `Play(bool isPicker, Action onFinished)`)을 상속해 연출을 만들고 `AtticTreasureSync.prefab`의 Cutscene 칸에 연결. 별도 씬이면 Play에서 Additive로 로드 → 끝나면 언로드 후 `onFinished` 호출. 비어 있으면 임시 연출(`AtticTreasurePlaceholderCutscene`, 2.5초 화면 어두워짐 + 문구).
- 현관문: `AtticTreasureFrontDoor`가 실행 중에 문을 이름으로 찾아 `keyItem`만 넣는다(LGH 문·씬 데이터 수정 없음). 열쇠 확인·잠금 해제·양문 동기화는 LGH `InteractableDoor`/`DoorNetworkSync` 기존 방식 그대로. 판정 담당이 "잠겨 있던 문이 풀리고 열림"을 보면 한 번 `onFrontDoorOpened` + 탈출 성공 요청. KJH 씬엔 GameFlowSync가 없어서 로그만 남김(Develop에는 있음, Playing 단계에서만 받아들임).
- 재생성: `Tools/KJH/AtticTreasure/Build All`(재실행 가능, 아이템 중복 추가 안 함). 겉모습 `Visuals/AtticTreasure_StatueVisual`·`AtticTreasure_KeyVisual`은 없을 때만 생성 → 모델 교체 후 Build All이면 콜라이더가 새 크기에 맞춰짐. 임시 큐브로 되돌리기는 `Rebuild Placeholder Visuals`.
- **LGH 쪽 문제 2건 (2026-10-23 발견, LGH 담당자 확인 필요 — 수정하지 않음)**:
  1. `ItemNetworkSync`(바닥 아이템 줍기/내려놓기/나간 사람 아이템 떨구기)가 LGH 씬의 씬 오브젝트 `DoorSync_LGH`에만 있고 **`DoorSync_LGH.prefab`에는 빠져 있음** → KJH·Develop 씬에서는 WorldItem(놋쇠 열쇠 등)을 아예 주울 수 없고, 인벤토리에서 내려놓기도 안 된다. 그래서 황금상·열쇠 줍기는 `AtticTreasureSync`가 직접 처리하게 만들었다. 내려놓기용 숨은 자리(`*_DropSpot`, WorldItem)는 넣어 뒀으니 ItemNetworkSync가 프리팹에 들어오면 내려놓기도 동작한다.
  2. `FinalDevice_Dial`(`Ravenwood_LGH/TempFurniture/Attic`)이 납작한 원판 모양인데 기본 원기둥의 **CapsuleCollider(0.9m)가 남아 있어서 별 상자 전체를 감싼다** → 실제 플레이에서 상자 안 물건은 물론 **별 자물쇠(LGH 기믹) 자체도 조준이 막힘**. 콜라이더를 원판 크기로 바꾸거나 없애야 한다.
- **Develop 적용 방법**: `AtticTreasure_Contents.prefab`(26.088, 9.115, 11.879, 회전 y 270 — 별 상자와 같은 위치·회전), `AtticTreasureSync.prefab`(최상위)을 프리팹 인스턴스로 두면 끝. 놓은 뒤 `Tools/Network/Validate Scene NetworkObjects`. `ItemDatabase_LGH` 변경(아이템 2개 추가)도 같이 머지돼야 한다.
- 검증 한계: Play 모드 `StartHost()` 로컬 호스트에서 확인 — 상자 닫힘 시 집기 불가, 열면 안내 문구, 황금상 → 연출(임시) → 인벤토리, 열쇠 → 인벤토리, 열쇠 없이 현관문 "현관 열쇠이(가) 필요하다", 열쇠로 양문 잠금 해제·열림, 탈출 요청 지점 도달(KJH엔 GameFlowSync 없어 로그), Validate 0건, 콘솔 에러 0. 위 2번 콜라이더 문제 때문에 E 입력은 `Interact()` 직접 호출로 확인. **두 사람 동시 연출, Develop에서 실제 엔딩 전환은 직접 확인 필요.**

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

## 사용 가능한 Unity 전용 스킬
이 환경에는 `unity:` 접두사의 스킬들(unity-cli, ui, urp-postprocessing, physics-3d-collision 등)이 이미 연결되어 있습니다. 관련 작업을 할 때는 먼저 해당 스킬을 확인하세요.

## 다음에 결정해야 할 것
1. 캐릭터 조작 방식 vs 디오라마 고정 시점 (프로토타입 필요)
2. 팀 공통 코딩 컨벤션, 공용 에셋/스크립트 배치 규칙
