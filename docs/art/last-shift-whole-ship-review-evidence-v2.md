# LAST SHIFT 전체 검수 증거 v2

기존 날짜 기반 11장 세트는 한 방을 한 각도에서만 보여 주고, 이미지가 어느 소스·수정 상태에서 생성됐는지 기계적으로 확인할 수 없었다. v2는 광장, 조종석, 전력실, 냉각실, 생명유지실, 숙소, 화물 소품군, EVA를 각각 `context`와 `diagnostic` 두 각도로 촬영한다.

Unity 메뉴 `Last Shift/Review/Capture Whole Ship Evidence v2` 또는 `DoodleUp.Editor.LastShiftVisualReviewCapture.CaptureWholeShipEvidenceV2ForAutomation`을 실행한다. 결과는 `docs/art/evidence/last-shift-whole-ship-review-v2/`에 생성된다.

`manifest.json`에는 다음을 함께 기록한다.

- 증거 스키마와 버전, 씬, Unity 버전, UTC 생성 시각
- Git HEAD와 tracked dirty 여부
- 해상도, FOV, near/far clip
- 리베이크·천장·숙소 단일 소스/높이·배전반 분리·침상 방향 수정 상태
- 각 이미지의 공간, 목적, 파일명, 카메라 위치와 주시점

배포 가능한 증거는 `sourceDirty=false`여야 한다. 이미지와 manifest는 같은 실행에서 다시 생성하며, 기존 v1 폴더의 파일과 섞지 않는다.
