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
- **아직 설치 안 됨**: 온라인 2인 협동에 필요한 네트워킹 패키지(Netcode for GameObjects vs Unity Multiplayer Services 중 미결정). 이 결정이 나기 전까지 네트워크 코드를 임의로 추가하지 말고, 필요하면 먼저 질문할 것.
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

## 사용 가능한 Unity 전용 스킬
이 환경에는 `unity:` 접두사의 스킬들(unity-cli, ui, urp-postprocessing, physics-3d-collision 등)이 이미 연결되어 있습니다. 관련 작업을 할 때는 먼저 해당 스킬을 확인하세요.

## 다음에 결정해야 할 것
1. 네트워킹 방식(로컬 2인 vs 온라인, 온라인이면 어떤 패키지)
2. 캐릭터 조작 방식 vs 디오라마 고정 시점 (프로토타입 필요)
3. 팀 공통 코딩 컨벤션, 공용 에셋/스크립트 배치 규칙
