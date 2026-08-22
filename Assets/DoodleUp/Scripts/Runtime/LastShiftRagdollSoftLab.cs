using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// <b>손으로 얹은</b> 래그돌을 플레이에서 직접 건드려 보는 조작기.
    ///
    /// <b>왜 <see cref="LastShiftRagdollLab"/> 을 못 쓰나.</b> 그쪽은
    /// <c>[RequireComponent(typeof(LastShiftRagdoll))]</c> 이고 <c>Awake</c> 에서 <c>Rebuild</c> 를
    /// 부른다. <see cref="LastShiftRagdoll"/> 은 <see cref="LastShiftRagdollRig"/> 표를 보고
    /// <b>프록시 바디·조인트·콜라이더를 제 손으로 다시 만든다</b> — 이 프리팹에 얹으면 부위별로
    /// 손으로 잡아 둔 콜라이더 모양과 <c>c0672ad5</c> 에서 재도출한 관절 한계가 통째로 날아간다.
    /// 그래서 조작만 떼어 온다. <b>리지드바디·조인트·콜라이더는 하나도 안 건드린다.</b>
    ///
    /// <b>왜 필요했나.</b> 랩 씬(<c>LAST_SHIFT_RAGDOLL_LAB</c>)에는 스크립트가 붙은 오브젝트가
    /// <b>하나도 없었다</b>. Play 를 눌러도 승무원이 중력에 정착하는 것 말고는 아무 일도 안 일어나고,
    /// 씬 뷰에서 끌어 봐도 프로젝트가 <c>autoSyncTransforms = 0</c> 이라 그 대입이 PhysX 로 안 넘어간다.
    /// 2026-08-22 사용자 보고 "래그돌인데 넘어뜨려도 아무 변화가 없다" 가 이것이다 —
    /// 물리는 돌고 있었고 <b>미는 수단이 없었다</b>.
    ///
    /// <b>무엇으로 미나.</b> 임펄스는 전부 <see cref="LastShiftRagdollTuning.Comic"/> 값을 쓴다.
    /// 이 프리팹의 총 질량(62kg)이 <see cref="LastShiftRagdollRig.TotalMass"/> 와 같아서 그대로 옮겨진다.
    ///
    /// <b>잠든 바디를 먼저 깨운다.</b> 오래 놔둔 래그돌은 슬립에 들어가고, 잠든 바디에 준 힘은
    /// 그 프레임에 버려진다. 모든 조작이 <see cref="WakeAll"/> 로 시작하는 이유다.
    /// (2026-08-22 실측으로 4초 정착 시점의 잠든 바디는 <c>0/15</c> 였다 — 슬립은 이번 보고의
    /// 원인이 아니었고, 그래도 손으로 깨우는 것은 공짜라 남긴다.)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftRagdollSoftLab : MonoBehaviour
    {
        [Tooltip("충격을 밀어 넣는 수평 방향. 씬의 통로 방향과 맞춰 둔다.")]
        [SerializeField] private Vector3 impactHeading = Vector3.back;

        [Tooltip("운석 폭심(승무원 기준 상대 좌표).")]
        [SerializeField] private Vector3 blastOrigin = new Vector3(0f, 0.2f, 1.6f);

        [Tooltip("마우스로 잡아끌 때의 스프링 세기(가속도). 올리면 뻣뻣해진다.")]
        [SerializeField] private float grabStrength = 60f;

        [Tooltip("잡아끌 때의 감쇠. 낮추면 손끝에서 출렁인다.")]
        [SerializeField] private float grabDamping = 8f;

        [Tooltip("화면 좌상단에 조작키와 물리 상태를 띄운다. 랩에서 눈으로 확인하려고 둔 것이다.")]
        [SerializeField] private bool showHud = true;

        [Tooltip("카메라가 승무원을 따라간다. 끄면 고정 카메라가 된다.")]
        [SerializeField] private bool followCamera = true;

        /// <summary>
        /// 밀 때 온몸에 주는 속도 변화(m/s). <c>LastShiftRagdollCollapseProbe</c> 의 <c>shove</c>
        /// 시나리오·낙하 검사가 쓰는 값과 같다.
        /// </summary>
        public const float ShoveSpeed = 2f;

        private readonly List<Rigidbody> _bodies = new List<Rigidbody>();
        private readonly List<Transform> _pose = new List<Transform>();
        private Vector3[] _restPositions = System.Array.Empty<Vector3>();
        private Quaternion[] _restRotations = System.Array.Empty<Quaternion>();
        private Vector3 _restRootPosition;
        private Quaternion _restRootRotation;

        private Rigidbody _grabbed;
        private Vector3 _grabLocalPoint;
        private float _grabDepth;
        private Vector3 _grabTarget;

        /// <summary>마지막으로 준 조작 이름. HUD 와 검사가 같이 읽는다.</summary>
        public string LastAction { get; private set; } = "-";

        /// <summary>잡아끄는 중인 부위 이름. 안 잡고 있으면 <c>null</c>.</summary>
        public string GrabbedName => _grabbed != null ? _grabbed.name : null;

        private void Awake()
        {
            _bodies.Clear();
            _bodies.AddRange(GetComponentsInChildren<Rigidbody>(true));

            _pose.Clear();
            _pose.AddRange(GetComponentsInChildren<Transform>(true));
            _restPositions = new Vector3[_pose.Count];
            _restRotations = new Quaternion[_pose.Count];
            for (var i = 0; i < _pose.Count; i++)
            {
                _restPositions[i] = _pose[i].localPosition;
                _restRotations[i] = _pose[i].localRotation;
            }

            _restRootPosition = transform.position;
            _restRootRotation = transform.rotation;
        }

        private void Update()
        {
            ReadKeyboard();
            ReadMouse();
        }

        /// <summary>
        /// 카메라를 승무원에 물린다. <b>고정 카메라로는 밀린 몸을 못 본다</b> —
        /// <see cref="LastShiftRagdollLab.CameraOffset"/> 주석에 같은 실측이 적혀 있고
        /// (밀린 뒤 2.7초에 승무원이 화면 폭의 3%), 2026-08-22 증거 촬영에서도 바디체크 한 번에
        /// 승무원이 뷰포트 밖(-5.76)으로 나갔다. 프레이밍 계산은 그쪽 것을 그대로 쓴다 —
        /// 두 랩이 다른 구도로 찍히면 비교가 안 된다.
        ///
        /// <b>물리 뒤에 놓는다.</b> <see cref="LateUpdate"/> 라 이 프레임의 최종 자세를 보고 따라간다.
        /// </summary>
        private void LateUpdate()
        {
            if (!followCamera) return;
            var pelvis = Find(LastShiftRagdollRig.PelvisBoneName);
            if (pelvis == null) return;
            LastShiftRagdollLab.FrameSubject(Camera.main, pelvis.transform.position);
        }

        private void FixedUpdate()
        {
            if (_grabbed == null) return;

            // 스프링으로 끈다. 트랜스폼을 직접 옮기면 autoSyncTransforms = 0 이라 PhysX 가 안 읽고
            // 다음 스텝에 제 포즈로 덮어쓴다 — 사용자가 씬 뷰에서 끌었을 때 겪은 것이 그것이다.
            var hand = _grabbed.transform.TransformPoint(_grabLocalPoint);
            var pull = (_grabTarget - hand) * grabStrength - _grabbed.linearVelocity * grabDamping;
            _grabbed.AddForce(pull, ForceMode.Acceleration);
        }

        private void ReadKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame) BodyCheck();
            if (keyboard.hKey.wasPressedThisFrame) HeadFlick();
            if (keyboard.bKey.wasPressedThisFrame) Blast();
            if (keyboard.rKey.wasPressedThisFrame) ResetToRestPose();
        }

        private void ReadMouse()
        {
            var mouse = Mouse.current;
            var camera = Camera.main;
            if (mouse == null || camera == null) return;

            if (mouse.leftButton.wasPressedThisFrame) TryGrab(camera, mouse.position.ReadValue());

            if (_grabbed != null)
            {
                var screen = mouse.position.ReadValue();
                _grabTarget = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, _grabDepth));
                WakeAll();
            }

            if (mouse.leftButton.wasReleasedThisFrame) _grabbed = null;
        }

        private void TryGrab(Camera camera, Vector2 screenPoint)
        {
            var ray = camera.ScreenPointToRay(new Vector3(screenPoint.x, screenPoint.y, 0f));
            if (!UnityEngine.Physics.Raycast(ray, out var hit, 100f)) return;

            var body = hit.rigidbody;
            if (body == null || !_bodies.Contains(body)) return;

            _grabbed = body;
            _grabLocalPoint = body.transform.InverseTransformPoint(hit.point);
            _grabDepth = camera.WorldToScreenPoint(hit.point).z;
            _grabTarget = hit.point;
            WakeAll();
            LastAction = "잡음 " + body.name;
        }

        /// <summary>
        /// 밀어 넘어뜨린다. 스페이스 키.
        ///
        /// <b>두 겹이다.</b> 온몸에 같은 속도 변화를 주고(=사람이 통째로 밀린다) 가슴에만 임펄스를
        /// 더 얹는다(=상체가 먼저 넘어가며 회전이 생긴다). 가슴만 때리면 이미 누워 있는 승무원은
        /// 상체만 움찔하고 만다 — 실측으로 골반이 <c>0.075m</c> 밖에 안 움직였다. 같은 이유를
        /// <see cref="LastShiftRagdoll.ApplyVelocityChange"/> 주석이 이미 적어 뒀다:
        /// "밀쳐지는 건 부위가 아니라 사람이다".
        ///
        /// 속도값은 새로 만든 것이 아니라 <b>이 랩이 이미 쓰던 밀침 시나리오와 같은 값</b>이다 —
        /// <c>LastShiftRagdollCollapseProbe</c> 의 <c>shove</c> 케이스와 낙하 검사가 모든 바디에
        /// <c>2 m/s</c> 를 준다. 검사와 손으로 미는 것이 같은 세기여야 검사가 검사다.
        /// </summary>
        public void BodyCheck()
        {
            var tuning = LastShiftRagdollTuning.Comic();
            var heading = tuning.ImpactDirection(impactHeading);
            WakeAll();

            for (var i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                if (body == null || body.isKinematic) continue;
                body.AddForce(heading * ShoveSpeed, ForceMode.VelocityChange);
            }

            Impulse(LastShiftRagdollRig.ChestBoneName, heading * tuning.BodyCheckSnapImpulse);
            LastAction = "바디체크(전신+가슴)";
        }

        /// <summary>머리만 톡 친다. H 키.</summary>
        public void HeadFlick()
        {
            var tuning = LastShiftRagdollTuning.Comic();
            Impulse(LastShiftRagdollRig.HeadBoneName,
                tuning.ImpactDirection(impactHeading) * tuning.HeadFlickImpulse);
            LastAction = "헤드플릭(머리)";
        }

        /// <summary>폭심에서 온몸을 한꺼번에 민다. B 키.</summary>
        public void Blast()
        {
            var tuning = LastShiftRagdollTuning.Comic();
            var origin = transform.position + blastOrigin;
            WakeAll();

            for (var i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                if (body == null || body.isKinematic) continue;
                body.AddExplosionForce(tuning.BlastImpulse, origin, tuning.BlastRadius,
                    0.2f, ForceMode.Impulse);
            }

            LastAction = "블라스트";
        }

        /// <summary>
        /// 정지 포즈로 되돌린다. R 키.
        ///
        /// <b>순서가 있다.</b> 트랜스폼을 되돌린 뒤 <see cref="UnityEngine.Physics.SyncTransforms"/> 를
        /// 손으로 부른다 — 프로젝트가 <c>autoSyncTransforms = 0</c> 이라 안 부르면 PhysX 는 옛 포즈를
        /// 그대로 들고 있다가 다음 스텝에 도로 덮어쓴다.
        /// </summary>
        public void ResetToRestPose()
        {
            for (var i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                if (body == null) continue;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            transform.SetPositionAndRotation(_restRootPosition, _restRootRotation);
            for (var i = 0; i < _pose.Count; i++)
            {
                if (_pose[i] == null || _pose[i] == transform) continue;
                _pose[i].localPosition = _restPositions[i];
                _pose[i].localRotation = _restRotations[i];
            }

            UnityEngine.Physics.SyncTransforms();
            WakeAll();
            _grabbed = null;
            LastAction = "정지 포즈 복귀";
        }

        /// <summary>부위 하나에 충격을 준다. 이름은 <see cref="LastShiftRagdollRig"/> 의 뼈 이름이다.</summary>
        public void Impulse(string boneName, Vector3 impulse)
        {
            var body = Find(boneName);
            if (body == null) return;
            WakeAll();
            body.AddForce(impulse, ForceMode.Impulse);
        }

        /// <summary>
        /// 잠든 바디를 전부 깨운다. <b>임펄스보다 먼저 불러야 한다</b> —
        /// 잠든 바디에 준 힘은 그 프레임에 통째로 버려진다.
        /// </summary>
        public void WakeAll()
        {
            for (var i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                if (body == null || body.isKinematic) continue;
                body.WakeUp();
            }
        }

        /// <summary>잠들어 있는 바디 수. HUD 와 검사가 "물리가 살아 있나" 를 이것으로 본다.</summary>
        public int SleepingBodies()
        {
            var count = 0;
            for (var i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                if (body == null || body.isKinematic) continue;
                if (body.IsSleeping()) count++;
            }

            return count;
        }

        private Rigidbody Find(string boneName)
        {
            for (var i = 0; i < _bodies.Count; i++)
                if (_bodies[i] != null && _bodies[i].name == boneName) return _bodies[i];
            return null;
        }

        private void OnGUI()
        {
            if (!showHud) return;

            var pelvis = Find(LastShiftRagdollRig.PelvisBoneName);
            var speed = pelvis != null ? pelvis.linearVelocity.magnitude : 0f;
            var text = "[래그돌 랩] 좌클릭 드래그=부위 잡아끌기 · Space=바디체크 · H=헤드플릭 · B=블라스트 · R=정지 포즈\n"
                       + "바디 " + _bodies.Count + " · 잠듦 " + SleepingBodies()
                       + " · 골반 속도 " + speed.ToString("F2") + " m/s · 마지막 조작 " + LastAction;
            GUI.Label(new Rect(12f, 12f, 900f, 44f), text);
        }

        /// <summary>
        /// 선택했을 때 부위별 콜라이더를 전부 그린다.
        ///
        /// <b>왜 있나.</b> 콜라이더는 <c>DEF-</c> 뼈가 아니라 그 밑의 <c>*_Col</c> 자식에 달려 있어서,
        /// 뼈를 클릭하면 인스펙터에 아무것도 안 나온다. 2026-08-22 사용자 보고
        /// "콜라이더 설정이 안 보인다" 가 이것이다 — 지워진 것이 아니라 <b>한 층 밑에 있다</b>.
        /// 루트만 선택하면 열다섯 개가 한 번에 보이게 둔다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 1f, 0.6f, 0.9f);
            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                var shape = collider.transform;
                Gizmos.matrix = Matrix4x4.TRS(shape.position, shape.rotation, shape.lossyScale);

                switch (collider)
                {
                    case BoxCollider box:
                        Gizmos.DrawWireCube(box.center, box.size);
                        break;
                    case SphereCollider sphere:
                        Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                        break;
                    case CapsuleCollider capsule:
                        DrawWireCapsule(capsule);
                        break;
                }
            }

            Gizmos.matrix = Matrix4x4.identity;
        }

        /// <summary>캡슐은 기즈모 기본 도형이 없다. 양 끝 구와 이은 선으로 대신한다.</summary>
        private static void DrawWireCapsule(CapsuleCollider capsule)
        {
            var axis = capsule.direction == 0 ? Vector3.right
                : capsule.direction == 1 ? Vector3.up : Vector3.forward;
            var half = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
            var a = capsule.center + axis * half;
            var b = capsule.center - axis * half;

            Gizmos.DrawWireSphere(a, capsule.radius);
            Gizmos.DrawWireSphere(b, capsule.radius);

            var side = capsule.direction == 1 ? Vector3.right : Vector3.up;
            var other = Vector3.Cross(axis, side).normalized;
            for (var i = 0; i < 4; i++)
            {
                var offset = (i == 0 ? side : i == 1 ? -side : i == 2 ? other : -other) * capsule.radius;
                Gizmos.DrawLine(a + offset, b + offset);
            }
        }
    }
}
