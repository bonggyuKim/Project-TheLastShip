# LAST SHIFT 라임 외계인 서브디비전 프리뷰

## 방향과 결과

정본 `LastShiftLimeAlien_UnityExport_LeftToeFixed.blend`의 머리·배 중심 실루엣과
눈·입의 판독성을 유지하면서, Catmull-Clark 서브디비전이 주는 젤리형 표면을
두 단계로 비교했다. 프리뷰 생성 과정은 정본 메시, 웨이트, 모디파이어를 바꾸지
않으며 Unity FBX도 재수출하지 않는다.

| 단계 | 정점 | 삼각형 | 육안 결과 | 판정 |
| --- | ---: | ---: | --- | --- |
| Base | 5,711 | 11,382 | 현재 정본 | 비교 기준 |
| Jelly L1 | 31,304 | 62,532 | 머리·배 전이가 부드러워지고 큰 실루엣 유지 | 제작 후보 |
| Jelly L2 | 125,138 | 250,128 | 목·배·팔다리에 물결형 요철이 더 드러남 | 표면 참고 전용 |

L1도 기본 대비 약 5.5배의 삼각형이므로 즉시 런타임에 적용하지 않는다. 실제
채택 시에는 필요한 부위의 토폴로지 정리 또는 베이크 결과로 같은 인상을 내고,
런타임 비용 검증을 별도 수행해야 한다. L2는 약 22배라 실루엣 상한 참고용이다.

## 산출물

- Blender 비교 씬:
  `ArtSource/Characters/LastShiftLimeAlien/LastShiftLimeAlien_SubdivisionPreview.blend`
- 정면 비교: `docs/art/evidence/last-shift-lime-alien-subdivision-preview/lime-alien-subdivision-front.png`
- 사선 비교: `docs/art/evidence/last-shift-lime-alien-subdivision-preview/lime-alien-subdivision-oblique.png`
- 메시 수치와 정본 무변경 digest: `docs/art/evidence/last-shift-lime-alien-subdivision-preview/report.json`
- 재현 도구: `Tools/art/render_lime_alien_subdivision_preview.py`

재생성 명령:

```powershell
& 'D:\blender\blender.exe' --background `
  'ArtSource\Characters\LastShiftLimeAlien\LastShiftLimeAlien_UnityExport_LeftToeFixed.blend' `
  --python 'Tools\art\render_lime_alien_subdivision_preview.py' -- `
  --output-dir 'docs\art\evidence\last-shift-lime-alien-subdivision-preview' `
  --blend-output 'ArtSource\Characters\LastShiftLimeAlien\LastShiftLimeAlien_SubdivisionPreview.blend'
```

## 육안 확인 포인트

- 정면에서 L1의 머리 외곽, 목, 배 곡률이 Base보다 부드럽되 캐릭터 비율이
  달라 보이지 않는지 확인한다.
- 사선에서 눈 테두리와 입 구멍이 뭉개지지 않는지 확인한다.
- L2의 목·배 사선 요철은 추가 세분화가 원형 토폴로지 문제를 해결하지 못한다는
  경고 신호로 본다.
- 추후 L1 인상을 게임에 반영할 때는 실제 카메라 거리와 애니메이션 극단 자세에서
  눈·입 판독성과 목/무릎 변형을 Unity에서 다시 확인해야 한다.
