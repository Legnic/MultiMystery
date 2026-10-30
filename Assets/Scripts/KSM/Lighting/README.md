# Master 방 조명 실험

이 도구는 아직 Unity에서 컴파일/실행하거나 베이크한 상태가 아니다. 클라우드에 Unity Editor가 없어 로컬 에디터에서 아래 검증이 필요하다. 씬과 조명 설정은 메뉴를 실행할 때만 변경한다.

1. `Develop`의 `Assets/Scenes/Develop.unity`를 열고 Play 모드를 종료한다.
2. `Tools > KSM > Lighting > Master Room`을 연다.
3. Master 방의 월드 범위(min `20, 3.9, 3`, max `36, 7.5, 16`)와 가구 위치가 현재 배치에 맞는지 확인한다. 도구는 GIM007 가구가 이 범위 안에 있어야 실행된다.
4. `1. Prepare Master Room`을 누른다. Hierarchy의 `MasterRoomLighting`에서 보조광 위치, Light Probe 배치, LocalVolume 영역을 Scene 뷰로 확인한다. 보조광은 방 중앙 천장 근처에 배치하는 실험용 고정 광원이다. 실제 조명기구 위치에 맞춰 조정한다.
5. `2. Bake Current Scene`을 누르고 Lighting/Console에서 완료와 오류를 확인한다. Unity 베이크는 **현재 씬 전체**에 수행된다. Master 방만 별도의 베이크 결과로 분리하는 기능은 아니다. 기존 다른 방의 static 플래그는 지우지 않으며, 경계에 걸친 공유 벽·바닥도 베이크에 참여할 수 있다.
6. Console의 UV 중첩/UV 누락 경고, Lighting 창의 라이트맵, Light Probe 결과를 확인한다. UV가 없는 모델은 해당 모델의 Import Settings에서 Generate Lightmap UVs를 설정하고 다시 베이크해야 한다.
7. 실제 호스트/참가 게임에서 방 진입/이탈, 가구의 시작/중간/끝 위치, 가구를 미는 캐릭터, 비밀문 열기, 낮/밤, 촛불 이동을 확인한다. 가구 시작 위치에 그림자가 구워져 남지 않는지 확인한다.
8. 과도한 밝기/흰색 날림/검은 영역을 비교하며 방 전용 VolumeProfile의 ACES와 노출을 조정한다. 노출 기본값 `+0.75 EV`는 검증 전 시작값이다. `RoomExposureOptions.baseExposureEV`도 같은 기준값으로 맞춘다. 게임 밝기 옵션의 EV 보정은 여기에 더해진다.
9. 결과가 괜찮으면 씬을 저장하고, 전용 설정/프로필/라이트맵/LightingData 및 플레이어 프리팹 변경을 함께 검토한다.

설정 내용:

- Master 영역의 고정 건물 표면/장식: Contribute GI 및 Lightmaps. 문/서랍/WorldItem/Rigidbody와 GIM007 루트는 자동 선택에서 제외한다.
- `GIM007_MasterBedroom/Furniture/Body`: Contribute GI와 Static Batching 해제. Light Probe 보간과 가구를 따라 움직이는 중심 Anchor를 사용한다. 원본 GIM007 프리팹을 덮어쓰지 않고 Develop 인스턴스에 적용한다.
- 일반 LightProbeGroup: 약 2m 간격, 바닥에서 0.3/1.2/2.3/3.1m 높이. 콜라이더 안에 들어가는 위치는 제외한다. 현재 URP의 일반 Light Probe 모드를 유지하며 APV로 변경하지 않는다.
- 고정 Mixed Point 보조광: 간접광만 베이크하고 직접광/가구 그림자는 실시간으로 처리한다. 기존 태양/달/촛불의 타입과 밝기는 바꾸지 않는다. 베이크된 고정 간접광은 낮/밤에 따라 다시 계산되지 않는다.
- 씬 LightingSettings: Progressive CPU, 20 texels/m, 2048 atlas, direct 64 / indirect 512 / environment 128 samples, 4 bounces. 기존 라이팅 설정 대신 전용 에셋을 할당한다.
- Master 방만 적용되는 Local Volume: ACES, 기준 노출 +0.75 EV, 경계 1m 블렌드. 기존 전역 프로필 에셋은 수정하지 않는다.
- 씬 카메라와 등록된 네트워크 스폰 플레이어 프리팹 카메라에서 Post Processing을 켜고 Default 레이어 Volume을 허용한다. 기존 전역 프로필의 효과도 이 카메라에 적용될 수 있으므로 방 밖 화면도 확인한다.

Prepare는 재실행 시 같은 이름의 조명/프로브/볼륨과 전용 에셋을 재사용한다. 조명기구를 수동 조정한 뒤 Prepare를 다시 실행하면 도구의 시작값으로 돌아간다. Bake는 현재 설정을 그대로 사용한다. 에디터 창을 닫아도 도구가 시작한 베이크가 자동 취소되지는 않으며 Lighting 창에서 상태를 확인할 수 있다.
