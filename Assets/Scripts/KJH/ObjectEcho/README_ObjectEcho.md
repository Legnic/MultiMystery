# ObjectEcho (임시 이름) — 사물에 손을 얹으면 소리가 들리는 능력

플레이어가 사물을 바라보고 **E**를 누르면 → 시점이 사물 쪽으로 돌아가고 손이 얹힘 → 시야가 좁아짐(비네팅·FOV·채도·환경음)
→ 화면이 완전히 어두워짐(시각 정보 차단) → 그 사물의 소리 + 화면 정가운데 잉크 펜 파형(청각에 집중)
→ 어둠이 걷히고 모든 것이 원래대로 돌아오며 완료 이벤트 발생.

## 파일 구성

| 파일 | 역할 |
|---|---|
| `ObjectEcho/ObjectEchoTarget.cs` | 대상 사물 설정 (HandTarget, FocusPoint, 소리, 1회용 여부, 안내 문구, 완료 UnityEvent). `IInteractable` 구현 |
| `ObjectEcho/ObjectEchoController.cs` | 연출 전체를 단계(상태)로 진행. 모든 수치를 Inspector에서 조정. Player에 붙임 |
| `ObjectEcho/IHandReach.cs` | 손 동작 인터페이스 (뻗기/거두기/완료 알림). **IK 교체 안내 주석 포함** |
| `ObjectEcho/PlaceholderHandReach.cs` | 임시 손 (카메라 아래 단순 도형, 베지어 곡선 이동) |
| `ObjectEcho/Editor/ObjectEchoSetupMenu.cs` | `Tools/KJH/ObjectEcho/...` 설정 메뉴 |
| `SoundCue/SoundCue.cs` | 연출용 소리 재생 + 스펙트럼 표시 묶음 (다른 연출에서도 재사용 가능) |
| `SoundCue/SpectrumOverlay.cs` | 스펙트럼 파형 (0.5초 페이드인 / 소리 끝난 뒤 1초 페이드아웃). 위치는 RectTransform으로 정함 (ObjectEcho는 화면 정가운데) |
| `SoundCue/InkWaveformGraphic.cs` | 잉크 펜 선 그리기 (번짐·펜 압력·끝 가늘어짐) |

기존 공용 코드 재사용: `IInteractable`, `IFocusable`, `IModalInteraction`, `InteractionController`(조준 감지·E/Esc·차단), `InteractionPromptUI`(안내 문구).
`PlayerMovement`에는 시점 각도를 설정하는 `SetLookAngles()`/`Yaw`/`Pitch`만 추가했습니다.

## 1. Unity 에디터 설정 (한 번만)

> 가장 빠른 방법: **Tools > KJH > ObjectEcho > Setup Player And UI** 를 누르면 아래 1~4가 자동으로 됩니다.
> 이미 있는 것은 건드리지 않으니 여러 번 눌러도 안전합니다. 아래는 수동으로 하거나 확인할 때의 절차입니다.

### 1-1. AudioMixer (환경음 줄이기)
1. Project 창 `Assets/Sounds/KJH` 에서 우클릭 > Create > **Audio Mixer** → 이름 `KJH_AudioMixer`.
2. 더블클릭해서 Audio Mixer 창을 열고, Groups 옆 **+** 로 Master 아래에 `Ambient`, `Echo` 그룹을 만듭니다.
3. `Ambient` 그룹 선택 → Inspector의 **Volume** 글자에 우클릭 > **Expose 'Volume (of Ambient)' to script**.
4. Audio Mixer 창 오른쪽 위 **Exposed Parameters** 를 펼쳐서 이름을 `AmbientVolume` 으로 바꿉니다.
5. 씬의 **환경음 AudioSource 전부**의 Output을 `Ambient` 그룹으로 지정합니다. (안 하면 환경음이 줄지 않습니다)
6. Player > SoundCue 의 AudioSource Output과 SoundCue의 Output Group은 `Echo` 로 둡니다. (능력 소리는 줄면 안 되므로)
7. Player의 ObjectEchoController > **Ambient Mixer** 에 `KJH_AudioMixer` 를 연결합니다.

### 1-2. Volume (비네팅 / 채도)
- **아무것도 안 해도 됩니다.** ObjectEchoController가 실행 시 전용 Global Volume(우선순위 20)을 자동으로 만들고,
  Inspector의 비네팅 강도·부드러움·색, 채도 값을 그 Volume에 적용합니다. 원본 프로필 에셋은 수정되지 않습니다.
- 직접 만든 Volume을 쓰고 싶다면: 빈 오브젝트 + Volume 컴포넌트(**Is Global** 체크, Priority는 다른 Volume보다 높게, Weight 0)를 만들고
  ObjectEchoController > **Echo Volume** 칸에 연결합니다. 프로필 안의 값은 실행 중 Inspector 값으로 덮어씌워집니다.
- 확인할 것: Main Camera의 **Rendering > Post Processing** 이 체크되어 있어야 화면 효과가 보입니다. (KJH 씬은 이미 켜져 있음)

### 1-3. Input Actions
- **새로 추가할 것 없음.** 기존 `InputSystem_Actions` 의 `Player/Interact`(E), `Photo/ClosePhoto`(Esc), `Photo/ToggleInventory`(Tab)를 그대로 씁니다.
- E/Esc는 `InteractionController` 가 받아서, 연출 중에는 ObjectEchoController(IModalInteraction)로 넘겨줍니다.
- 연출 중 차단: 다른 상호작용·사진 촬영 진입(E가 가로채짐), Tab 인벤토리(PhotoInventory가 `IsModalActive` 확인) 모두 기존 구조로 막힙니다.

### 1-4. 손 / 스펙트럼 UI
- `Player/Main Camera/PlaceholderHand` : PlaceholderHandReach + 단순 도형 손. 처음 놓인 위치(화면 밖 오른쪽 아래)가 대기 자리입니다.
- `PhotoUICanvas/EchoBlackout` : 화면 전체를 덮는 암전 덮개(평소 투명). 그리는 순서가 **게임 화면 < EchoBlackout < InteractionPrompt < SpectrumOverlay** 여야 암전 위에 안내 문구와 파형이 보입니다.
  컨트롤러의 `Darken Screen`(켜기/끄기), `Blackout Duration`(1.2초), `Blackout Alpha`(1 = 완전 암전), `Blackout Restore Duration`(1.2초)으로 조절합니다.
- `PhotoUICanvas/SpectrumOverlay` : 화면 정가운데, 1000×260. 선 색은 자식 `InkLine` 의 Color에서 바꿉니다.
- Player의 ObjectEchoController에 Hand Reach / Sound Cue 가 연결되어 있는지 확인합니다.

### 1-5. HandTarget / FocusPoint 배치 요령
- **HandTarget** = 손바닥이 닿는 점. 축 약속:
  - 파란 **Z축** = 손가락 끝 방향
  - 초록 **Y축** = 손등 방향 (= 사물 표면의 바깥쪽)
  - 위치 = 표면 바로 위 (손 모델의 원점이 손바닥 아랫면이라서 표면에 딱 붙습니다)
  - 책상 위: Y는 위, Z는 플레이어 반대쪽. 벽면: Y는 벽 바깥(플레이어 쪽), Z는 위쪽.
- **FocusPoint** = 연출 동안 카메라가 바라볼 점. 손과 사물이 함께 화면 가운데 들어오도록 손 자리 근처에 둡니다. 비우면 HandTarget을 봅니다.
- 대상 오브젝트를 선택하면 씬 뷰에 손바닥 크기 상자(손 자리)와 하늘색 구(응시 지점)가 그려지니 보면서 맞추면 됩니다.
- Scene 뷰 툴바의 회전 기준을 **Local** 로 바꾸면 축 방향 확인이 쉽습니다.

### 1-5. 플레이어 능력 (PlayerAbilities)
- 소리 듣기는 **플레이어 A(SoundEcho 능력)** 전용입니다. Player의 `PlayerAbilities` → Abilities에 SoundEcho가 있어야 합니다.
- 능력이 없는 플레이어에게는 대상의 안내 문구·강조가 뜨지 않고 E에도 반응하지 않습니다 (`ObjectEchoTarget`이 `IRequiresAbility`로 SoundEcho를 요구).
- 같은 방식으로 사진 촬영 지점·Tab 사진 인벤토리는 플레이어 B(PastPhoto) 전용입니다. 단서는 누구나 쓸 수 있습니다.
- 혼자 테스트하는 KJH 씬의 Player는 두 능력(SoundEcho, PastPhoto)을 모두 켜 두었습니다.
- 코드 위치: `Assets/Scripts/KJH/Common/` (`PlayerAbility`, `PlayerAbilities`, `IRequiresAbility`). 공용 폴더 규칙이 정해지면 옮길 예정.

## 2. 새 능력 대상 오브젝트 추가하기

1. 씬에서 사물(모델의 최상위)을 선택합니다.
2. 메뉴 **Tools > KJH > ObjectEcho > Make Object Echo Target (Top Surface)** — 책상 위 물건처럼 위에서 손을 얹는 경우
   또는 **(Front Surface)** — 벽/액자/문처럼 앞에서 손을 대는 경우 (씬 뷰 카메라 쪽을 향한 면에 배치됨).
   → ObjectEchoTarget 추가, 콜라이더 없으면 BoxCollider 추가, 자식 `HandTarget`/`FocusPoint` 자동 생성.
3. HandTarget / FocusPoint 위치·회전을 위 1-5 요령대로 다듬습니다.
4. ObjectEchoTarget Inspector:
   - **Echo Clip** 에 소리 연결, Volume, Spectrum Intensity(선 출렁임 배율)
   - **Spectrum Response** 에 반응 프리셋 연결 (선택): `SoundCue/Presets/` 의 Calm(잔잔) / Normal(보통) / Sensitive(예민).
     비우면 SpectrumOverlay의 기본 반응(= Normal과 같은 값)을 씁니다. 새 프리셋은 Create > KJH > Spectrum Response Profile.
   - **Repeatable** (기본 꺼짐 = 1회만), **Prompt Text** (기본 "손을 얹는다")
   - **On Echo Completed** 에 퍼즐 로직 연결 (예: 서랍 열기, 단서 획득 등). 중간에 중단되면 호출되지 않습니다.
5. 콜라이더가 **Default 레이어**에 있는지, 플레이어 조준 거리(InteractionController > Max Look Distance, 기본 2m) 안에서 닿는지 확인합니다.

수동으로 할 경우: 사물에 ObjectEchoTarget 추가 → 자식 빈 오브젝트 2개(HandTarget, FocusPoint) 만들어 연결 → 콜라이더 확인.

## 3. 리깅 캐릭터로 교체할 때 작업 목록

자세한 단계는 `IHandReach.cs` 상단 주석에 있습니다. 요약:

- [ ] Package Manager에서 **Animation Rigging** 설치
- [ ] 캐릭터에 **Rig Builder** + 자식 **Rig** + 오른팔 **Two Bone IK Constraint** (Root=위팔, Mid=아래팔, Tip=손, Target, Hint) 구성
- [ ] `TwoBoneIKHandReach : MonoBehaviour, IHandReach` 작성
  - ReachTo: IK Target을 손 본 위치에서 HandTarget까지 곡선 이동(PlaceholderHandReach의 베지어 코드 재사용) + Constraint weight 0→1
  - Retract: weight 1→0 (원래 애니메이션 자세로 자연스럽게 복귀)
  - SnapToRest: weight 0 즉시
  - IsMoving / IsExtended 상태 갱신
- [ ] 손 모델의 축이 HandTarget 약속(Z=손가락, Y=손등)과 다르면 회전 보정값으로 맞추기
- [ ] Player > ObjectEchoController > **Hand Reach** 칸을 새 컴포넌트로 교체
- [ ] `Main Camera/PlaceholderHand` 삭제 (임시 손 머티리얼 `PlaceholderHand_Mat` 도 정리)
- [ ] 1인칭 카메라에서 팔이 카메라 근평면(Near Clip 0.05)에 잘리지 않는지, 몸통 모델이 시야를 가리지 않는지 확인
- [ ] 기존 대상들의 HandTarget 높이가 캐릭터 팔 길이로 닿는 범위인지 확인 (너무 멀면 팔이 쭉 펴져 어색함 → 대상 쪽 위치 조정)
- **연출 코드(ObjectEchoController, ObjectEchoTarget)는 수정할 필요 없음**

## 테스트 씬 (KJH.unity / `ObjectEcho_Test`)

- `Echo_Desk_MusicBox` : 책상 위 상자, 윗면에 손 (오르골 소리)
- `Echo_Wall_Portrait` : 벽 액자, 정면에 손 (벽 너머 두드림 소리, 스펙트럼 강도 1.3)
- `Ambient_RoomTone` : 환경음 루프 (Ambient 그룹) — 연출 중 줄어드는지 확인용

**임시 소리 위치**: `Assets/Sounds/KJH/ObjectEcho/Temp_*.wav` (코드로 합성한 테스트용 소리).
실제 소리가 준비되면 같은 폴더(또는 담당자 이니셜 폴더)에 넣고 각 ObjectEchoTarget의 Echo Clip만 바꾸면 됩니다.

## 설계 메모

- **코루틴을 쓴 이유**: 중단(StopCoroutine)이 간단하고, 오브젝트가 꺼지면 자동으로 멈춰 안전하며, 기존 연출 코드(InspectableClue 등)와 같은 방식이라서.
  자세한 내용은 `ObjectEchoController.cs` 상단 주석 참고.
- 시야 효과(비네팅·FOV·채도·환경음)는 0~1 값 하나(`narrowBlend`)로 함께 움직여서, 중간에 중단돼도 "지금 값에서" 되돌리면 됩니다.
- 시점 회전은 연출이 끝난 뒤에도 사물을 바라본 채로 남습니다 (원래 방향으로 되돌리지 않음).
- 네트워크(2인 협동) 미정이라 동기화 코드는 없습니다. 상호작용한 플레이어의 컨트롤러가 연출을 맡는 구조라서,
  나중에 "손 얹은 사람만 소리를 듣는" 정보 비대칭으로 확장하기 쉽습니다.
- 공용 코드 배치 규칙이 정해지면 `SoundCue/` 폴더는 공용 위치로 옮기는 것을 고려하세요.
