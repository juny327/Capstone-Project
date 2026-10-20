현재 Stage1에는 다운로드 에셋을 사용한 소행성 채굴 기지 버전이 적용되어 있다. 구성과 백업 위치는 DownloadedAssetsMap.md를 참고한다. 아래 기본 메시 설명은 초기 Stage1_Orbit 디자인에 해당한다.

# 우주정거장 맵 — Orbit / 07

## 열기

실제 게임의 `Assets/Scenes/Stage1.unity`에 우주정거장 맵을 적용했다. 로비에서 게임을 시작하면 이 맵으로 진입한다. Stage1의 원래 GUID와 빌드 씬 순서를 유지하므로 다음 스테이지 연결은 유지된다. Stage2·Stage3·StageBoss에도 서로 다른 우주 테마가 적용되어 있다. 최신 구성은 [CampaignMaps.md](CampaignMaps.md)를 참고한다.

`Assets/Scenes/Stage1_Orbit.unity`는 별도 디자인 씬으로 남겨두었다. 이전 Stage1은 `Assets/Scenes/MapBackups/Stage1_BeforeOrbit.unity`에 백업했다. 직접 Play용 GameAppManager는 로비에서 진입하면 기존 싱글턴을 사용하고 중복 인스턴스는 제거된다. 로비 StartGame 진입, 플레이어 생성, 연결된 바닥 4,384칸 및 네 구역 NavMesh 경로 4/4를 확인했다.

## 구성

- 70 × 70 바닥, 중앙 원형 도착 구역과 십자형 이동 통로.
- 네 개의 설비 구역과 낮은 엄폐물. 장애물은 MapData의 Wall로 표현하므로 생성 위치 선정과 NavMesh가 같은 지형 정보를 사용한다.
- 금속 패널, 청록색 이동 유도선, 황색 설비 경고등.
- 맵 밖 별과 행성. 배경과 장식에는 충돌체가 없다.
- 장식은 재질별로 메시를 합쳐 오브젝트 수를 줄인다.

## 수정

`OrbitalMapLayout`에서 통로와 장애물 배치를, `OrbitalMapSetup`에서 재질과 장식을 변경할 수 있다. `OrbitalMapTheme`가 붙은 MapGenerator만 우주정거장 배치를 사용한다.

`Tools > Orbit Map > Create Space Stage`는 씬을 처음 만드는 명령이다. 이미 만들어진 씬을 덮어쓰지 않도록 차단한다. 이후 배치는 씬에서 편집할 수 있지만, 실제 충돌 지형 변경은 실행 때 다시 생성되므로 OrbitalMapLayout에도 반영해야 한다.

## 에셋과 프로젝트

이번 초안의 바닥·설비·장식은 Unity 기본 메시와 새로 만든 재질로 구성했다. 외부 유료 에셋이나 별도 다운로드는 추가하지 않았다. 원래 저장소에 포함된 플레이어와 게임 시스템은 그대로 사용한다.

원본 요구 버전은 6000.3.7f1이며, 작업 PC에 설치된 6000.3.24f1로 별도 복제본을 열었다. Pipeline 0.8.0-exp.1은 에디터 연결을 위해 추가했다.
