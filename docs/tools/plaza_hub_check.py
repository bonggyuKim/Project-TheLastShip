"""중앙 광장 허브 배치 - 검산 스크립트 (폐기됨, 2026-08-11).

이 스크립트는 씬/코드를 읽지 않고 docs/central-plaza-hub-layout-v1.md §2.2 좌표표를
손으로 다시 입력해 둔 것이었다. 정본이 바뀔 때마다(에어록 홀 폐지, 조종석 좌표 개편)
이 사본이 낡은 채로 남아 재실행 시 옛 수치를 재확산시키는 사고가 두 번 났다.

진짜 정본은 코드 자체를 재는 LastShiftPlazaLayoutTests(EditMode)다.
좌표 정합·SIMUL_ZONES·이탈 거리·발자국 합 전부 그 테스트 스위트가 검증한다 -
docs/central-plaza-hub-layout-v1.md 부록의 "검사" 열을 참고할 것.

이 파일은 손 계산 사본을 다시 만들지 않기 위해 의도적으로 비웠다.
새 좌표 검산이 필요하면 Unity EditMode 테스트를 추가/실행할 것 - 이 스크립트를
되살리지 말 것.
"""

raise SystemExit(
    "plaza_hub_check.py는 폐기됐다. LastShiftPlazaLayoutTests(EditMode)를 실행할 것 — "
    "docs/central-plaza-hub-layout-v1.md 부록 참고."
)
