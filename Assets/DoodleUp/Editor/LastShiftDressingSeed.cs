using System.Collections.Generic;
using System.IO;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 드레싱 에셋 부트스트랩. <b>일회성 도구다</b> — 소품 목록이 Editor 코드에 하드코딩돼
    /// 있던 시절의 값을 데이터로 한 번 옮기려고 있다. 옮긴 뒤에는 art 가 Inspector 에서
    /// 에셋을 고치고, 이 파일은 다시 안 돈다.
    ///
    /// <b>씬 빌드는 이걸 절대 안 부른다.</b> 부르면 art 가 에셋에 넣은 값이 매 빌드마다
    /// 코드 값으로 되돌아가고, 그 순간 정본이 다시 코드가 된다 — 이 카드가 없애려던 상태
    /// 그대로다. 메뉴에서 사람이 눌러야만 돈다.
    ///
    /// 에셋이 실수로 지워졌을 때 되살리는 용도로도 남겨 둔다. 그때는 art 의 편집분이
    /// 사라지므로, 덮어쓰기 전에 확인을 받는다.
    /// </summary>
    public static class LastShiftDressingSeed
    {
        private const string MaterialFolder = "Assets/DoodleUp/Materials";

        [MenuItem("Last Shift/SP-02A/드레싱 에셋 부트스트랩 (덮어씀)")]
        public static void SeedWithConfirmation()
        {
            var existing = AssetDatabase.LoadAssetAtPath<LastShiftDressingSet>(LastShiftDressingSet.AssetPath);
            if (existing != null && !EditorUtility.DisplayDialog(
                    "드레싱 에셋 덮어쓰기",
                    $"{LastShiftDressingSet.AssetPath} 를 코드에 든 초기값으로 되돌린다.\n\n" +
                    $"연결된 프리팹 참조 {CountLinkedPrefabs(existing)}개가 전부 끊긴다. " +
                    "Inspector 에서 편집한 재질·좌표도 사라진다.\n\n" +
                    "슬롯만 늘리려는 것이라면 이게 아니라 '드레싱 슬롯 동기화 (연결 보존)' 이다.",
                    "덮어쓴다", "취소"))
                return;

            Seed();
        }

        /// <summary>
        /// 최초 이관용 CLI 진입점. 확인 대화 없이 덮어쓰므로 <b>부트스트랩 때만</b> 쓴다.
        /// <c>-executeMethod DoodleUp.Editor.LastShiftDressingSeed.SeedForAutomation</c>
        /// </summary>
        public static void SeedForAutomation() => Seed();

        /// <summary>
        /// 코드에 새로 생긴 슬롯만 에셋에 덧붙인다. <b>기존 항목은 손대지 않는다</b> —
        /// art 가 채운 프리팹·재질 참조가 그대로 남는다.
        ///
        /// <see cref="Seed"/> 와 갈라 두는 이유가 이것이다. 시드는 목록을 통째로 되돌리므로
        /// 지금 실행하면 art 가 연결한 프리팹 참조가 전부 사라진다. 슬롯이 늘어날 때 필요한
        /// 것은 되돌리기가 아니라 덧붙이기이고, 그건 시드와 다른 동작이다.
        ///
        /// 같은 자리(<c>space</c> + <c>id</c>)가 이미 있으면 건너뛴다. 코드 쪽 좌표가 바뀐
        /// 경우에도 덮지 않는다 — 그때는 무엇을 정본으로 볼지가 판단이라 자동으로 정하지 않고
        /// 로그로만 알린다.
        /// </summary>
        [MenuItem("Last Shift/SP-02A/드레싱 슬롯 동기화 (연결 보존)")]
        public static void SyncMissingProps()
        {
            var set = AssetDatabase.LoadAssetAtPath<LastShiftDressingSet>(LastShiftDressingSet.AssetPath);
            if (set == null)
            {
                Debug.LogError($"[LAST_SHIFT_DRESSING_SYNC] path={LastShiftDressingSet.AssetPath} " +
                               "result=FAIL reason=asset-missing — 먼저 부트스트랩을 돌린다");
                return;
            }

            var existing = new List<LastShiftDressingProp>(set.Props);
            var known = new HashSet<string>();
            foreach (var prop in existing)
                if (prop != null) known.Add(KeyOf(prop));

            var added = 0;
            var drifted = 0;
            foreach (var candidate in BuildInitialProps())
            {
                if (known.Add(KeyOf(candidate)))
                {
                    existing.Add(candidate);
                    added++;
                }
                else
                {
                    drifted++;
                }
            }

            if (added == 0)
            {
                Debug.Log($"[LAST_SHIFT_DRESSING_SYNC] added=0 kept={existing.Count} result=PASS");
                return;
            }

            set.ReplaceAll(existing);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(LastShiftDressingSet.AssetPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[LAST_SHIFT_DRESSING_SYNC] added={added} kept={existing.Count - added} " +
                      $"untouched={drifted} total={existing.Count} result=PASS");
        }

        /// <summary><c>-executeMethod DoodleUp.Editor.LastShiftDressingSeed.SyncForAutomation</c></summary>
        public static void SyncForAutomation() => SyncMissingProps();

        /// <summary>
        /// <b>공간이 없어진 소품만</b> 에셋에서 뺀다. 나머지는 한 줄도 안 건드린다 —
        /// art 가 채운 프리팹·재질 참조가 그대로 남는다.
        ///
        /// <b>M-2 가 이 진입점을 요구했다.</b> 고정 구획이 열하나에서 하나로 줄면서
        /// (<c>docs/core-four-rooms-and-hull-schematic-v1.md</c> §2.4) 에셋에 구워진 소품
        /// 대다수가 <b>표 밖 구획</b>을 가리키게 됐고, <see cref="LastShiftDressingSpaces.BoundsOf"/>
        /// 가 그 값을 <see cref="LastShiftCompartments.Of"/> 에 넣는 순간 터진다. 상부 회랑
        /// 소품도 같은 처지다 — 종류 <c>5</c> 가 enum 에서 없어져 조용히 에어록으로 읽힌다.
        ///
        /// <b><see cref="Seed"/> 도 <see cref="SyncMissingProps"/> 도 이 일을 못 한다.</b>
        /// 시드는 목록을 통째로 되돌려 연결을 전부 끊고, 동기화는 덧붙이기만 한다. 이관에
        /// 필요한 것은 <b>빼기</b>이고, 그건 둘 다와 다른 동작이다.
        ///
        /// <c>-executeMethod DoodleUp.Editor.LastShiftDressingSeed.PruneForAutomation</c>
        /// </summary>
        [MenuItem("Last Shift/SP-02A/드레싱 고아 소품 제거 (연결 보존)")]
        public static void PruneOrphanedProps()
        {
            var set = AssetDatabase.LoadAssetAtPath<LastShiftDressingSet>(LastShiftDressingSet.AssetPath);
            if (set == null)
            {
                Debug.LogError($"[LAST_SHIFT_DRESSING_PRUNE] path={LastShiftDressingSet.AssetPath} " +
                               "result=FAIL reason=asset-missing");
                return;
            }

            // 코드가 지금 세우는 구획 소품의 키. <b>구획 소품은 이 목록에 있어야만 남는다.</b>
            // 근거는 §2.3 이 예고한 enum 재번호다 — 옛 <c>Observatory = 0</c> 자리에 지금
            // <c>Quarters = 0</c> 이 앉았으므로, 인덱스만 보면 관측실 소품이 <b>숙소 소품으로
            // 조용히 되살아난다</b>. 실제로 그 상태에서 별자리도(StarChart)가 숙소 문을
            // 0.05m 로 막아 C5 를 물렸다.
            var codeSideKeys = new HashSet<string>();
            foreach (var candidate in BuildInitialProps())
                if (candidate.space.kind == LastShiftDressingSpaceKind.Compartment)
                    codeSideKeys.Add(KeyOf(candidate));

            var kept = new List<LastShiftDressingProp>();
            var dropped = new List<string>();
            foreach (var prop in set.Props)
            {
                if (prop == null) continue;

                var alive = SpaceStillExists(prop.space) &&
                            (prop.space.kind != LastShiftDressingSpaceKind.Compartment ||
                             codeSideKeys.Contains(KeyOf(prop)));

                if (alive) kept.Add(prop);
                else dropped.Add($"{prop.space}/{prop.id}");
            }

            if (dropped.Count == 0)
            {
                Debug.Log($"[LAST_SHIFT_DRESSING_PRUNE] dropped=0 kept={kept.Count} result=PASS");
                return;
            }

            set.ReplaceAll(kept);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(LastShiftDressingSet.AssetPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[LAST_SHIFT_DRESSING_PRUNE] dropped={dropped.Count} kept={kept.Count} " +
                      $"result=PASS removed={string.Join(",", dropped)}");
        }

        /// <summary><c>-executeMethod DoodleUp.Editor.LastShiftDressingSeed.PruneForAutomation</c></summary>
        public static void PruneForAutomation() => PruneOrphanedProps();

        /// <summary>
        /// 이 소품이 가리키는 공간이 아직 배에 있는가. <b>좌표를 안 묻는다</b> —
        /// 그 물음이 곧 터지는 자리라(<see cref="LastShiftCompartments.Of"/>) 여기서는
        /// 종류와 인덱스만 본다.
        /// </summary>
        private static bool SpaceStillExists(LastShiftDressingSpace space) => space.kind switch
        {
            LastShiftDressingSpaceKind.Zone => (int)space.zone < LastShiftZoneAtlas.ZoneCount,
            LastShiftDressingSpaceKind.Compartment =>
                (int)space.compartment >= 0 && (int)space.compartment < LastShiftCompartments.FixedCount,
            LastShiftDressingSpaceKind.Passage => space.passage is >= 0 and <= 1,
            LastShiftDressingSpaceKind.BypassRun => true,
            LastShiftDressingSpaceKind.AirlockBranch => true,
            // 없어진 종류(폐지된 상부 회랑의 `5`)가 여기로 온다. 기본 갈래를 true 로 두면
            // 그 소품이 조용히 에어록 소품으로 되살아난다.
            _ => false
        };

        private static int CountLinkedPrefabs(LastShiftDressingSet set)
        {
            var linked = 0;
            foreach (var prop in set.Props)
                if (prop != null && prop.prefab != null) linked++;
            return linked;
        }

        private static string KeyOf(LastShiftDressingProp prop) =>
            $"{prop.space.kind}/{prop.space.zone}/{prop.space.compartment}/" +
            $"{prop.space.passage}/{prop.id}";

        /// <summary>에셋을 만들고(또는 덮어쓰고) 저장한다.</summary>
        public static LastShiftDressingSet Seed()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LastShiftDressingSet.AssetPath)!);
            var set = AssetDatabase.LoadAssetAtPath<LastShiftDressingSet>(LastShiftDressingSet.AssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<LastShiftDressingSet>();
                AssetDatabase.CreateAsset(set, LastShiftDressingSet.AssetPath);
            }

            set.ReplaceAll(BuildInitialProps());
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(LastShiftDressingSet.AssetPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[LAST_SHIFT_DRESSING_SEED] path={LastShiftDressingSet.AssetPath} props={set.Props.Count} result=PASS");
            return set;
        }

        private static Material Mat(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");

        private static List<LastShiftDressingProp> BuildInitialProps()
        {
            var props = new List<LastShiftDressingProp>();
            var fixture = Mat("LS_Fixture");
            var hazard = Mat("LS_Hazard");
            var lane = Mat("LS_Lane");

            AddCompartmentProps(props, fixture);
            AddStateCues(props);
            AddBypassProps(props, lane, hazard);
            AddCeilingLamps(props);
            return props;
        }

        // ── 구획 ─────────────────────────────────────────────────────────────────────

        private static void AddCompartmentProps(List<LastShiftDressingProp> props, Material fixture)
        {
            // 고정 구획만이다. 드레싱은 enum 으로 키를 잡는다(LastShiftDressingSpace.Of) —
            // 모듈에는 줄 enum 값이 없으므로 여기 섞이면 키가 없는 방이 들어온다.
            foreach (var spec in LastShiftCompartments.FixedSpecs)
            {
                var compartment = spec.Compartment;
                var space = LastShiftDressingSpace.Of(compartment);
                var tint = Mat($"LS_Tint_{compartment}");

                // 문 앞에서 방 안쪽으로 뻗는 바닥 띠. 문을 통과하는 순간 발밑 색이 바뀌어
                // "다른 공간으로 넘어왔다" 가 천장 높이 말고 하나 더 생긴다. 띠는 방 중심이
                // 아니라 문 중심을 지난다 — 둘이 어긋난 구획(의무실)에서 방 중심에 깔면
                // 띠가 문을 안 지나 유도선 노릇을 못 한다. 그래서 이 항목만 미터 앵커다.
                var alongX = spec.DoorPlane == LastShiftDoorPlane.AlongX;
                props.Add(new LastShiftDressingProp
                {
                    id = "FloorBand",
                    space = space,
                    anchorMode = LastShiftDressingAnchorMode.MetersFromSpaceCenter,
                    anchor = alongX
                        ? new Vector2(0f, spec.DoorCenter - spec.CenterZ)
                        : new Vector2(spec.DoorCenter - spec.CenterX, 0f),
                    size = alongX
                        ? new Vector3(spec.LengthX - 0.3f, 0.03f, 0.5f)
                        : new Vector3(0.5f, 0.03f, spec.WidthZ - 0.3f),
                    bottomY = 0.001f,
                    material = tint
                });

                // 숙소가 이 배에 남는 유일한 고정 방이고, 폐지된 화장실·휴게실을 흡수한다
                // (맵 개편 §3.2 — "침상 + 위생 + 휴게가 한 방"). 프리팹 셋이 하나로 줄어드는
                // 것이 그 결정의 아트 쪽 이득이고, 여기서는 소품 세 벌을 한 공간에 합친다.
                //
                // 발자국이 4x6 이라 예전 숙소와 같으므로 침상 배치는 그대로 두고, 위생·휴게를
                // 남은 x 양 끝에 붙인다. 문이 x=MinX 한가운데(z=0)이므로 z ~ 0 의 x 동선은
                // 비워 둔다 — 여기 소품을 놓으면 배에 하나뿐인 고정 방의 문이 막힌다.
                foreach (var (bunk, uz) in new[] { ("Port", -0.75f), ("Starboard", 0.75f) })
                {
                    Add(props, space, $"Bunk_{bunk}_Lower", 0.55f, uz, 2.2f, 0.25f, 0.9f, fixture, 0.45f);
                    Add(props, space, $"Bunk_{bunk}_Upper", 0.55f, uz, 2.2f, 0.25f, 0.9f, fixture, 1.55f);
                }

                Add(props, space, "Lockers", 1f, 0f, 0.45f, 1.9f, 1.6f, tint);

                // 위생 — 흡수된 화장실. 문 쪽 좌현 구석이라 들어서면서 바로 보인다.
                Add(props, space, "Basin", -0.85f, -1f, 0.9f, 0.9f, 0.5f, fixture);
                Add(props, space, "Stall", -0.85f, 1f, 1.0f, 2.0f, 0.9f, tint);

                // 휴게 — 흡수된 휴게실. 넷이 마주 앉는 자리 하나다. 이 배에서 유일하게
                // 일 안 하는 소품이고, 그 정서가 숙소 안에 남아야 §3.2 의 흡수가 성립한다.
                Add(props, space, "Table", -0.1f, 0f, 1.0f, 0.75f, 1.0f, fixture);
                Add(props, space, "Bench_Port", -0.1f, -0.6f, 1.2f, 0.45f, 0.4f, tint);
                Add(props, space, "Bench_Starboard", -0.1f, 0.6f, 1.2f, 0.45f, 0.4f, tint);
            }
        }

        private static LastShiftDressingProp Add(List<LastShiftDressingProp> props,
            LastShiftDressingSpace space, string id, float ux, float uz,
            float sizeX, float sizeY, float sizeZ, Material material, float bottomY = 0f)
        {
            var prop = new LastShiftDressingProp
            {
                id = id,
                space = space,
                anchorMode = LastShiftDressingAnchorMode.UnitOfSpace,
                anchor = new Vector2(ux, uz),
                size = new Vector3(sizeX, sizeY, sizeZ),
                bottomY = bottomY,
                material = material
            };
            props.Add(prop);
            return prop;
        }

        // ── 상태 단서 ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 냉각실·전력실 상태 단서. 좌표는 <see cref="LastShiftDressing.StateCues"/> 에 있던
        /// 값 그대로다 — 이 값들이 §19.7 안전대와의 여유가 곧 설계라 단위좌표로 바꾸지 않고
        /// 미터 앵커로 옮긴다. 방 중심 기준이므로 전장이 바뀌어도 방을 따라간다.
        /// </summary>
        private static void AddStateCues(List<LastShiftDressingProp> props)
        {
            foreach (var cue in LastShiftDressing.StateCues)
            {
                var material = Mat(cue.Kind == LastShiftStateCue.Frost ? "LS_Frost" : "LS_Scorch");
                props.Add(new LastShiftDressingProp
                {
                    id = cue.Name,
                    space = LastShiftDressingSpace.Of(cue.Room),
                    anchorMode = LastShiftDressingAnchorMode.MetersFromSpaceCenter,
                    anchor = new Vector2(cue.OffsetX, cue.CenterZ),
                    size = cue.Size,
                    bottomY = cue.CenterY - cue.Size.y * 0.5f,
                    material = material,
                    semantics = LastShiftDressingSemantics.StateResponsive,
                    justification = "상태 연동 이펙트가 붙을 자리. 지금은 정적 판이다."
                });
            }
        }

        // ── 우회 통로 ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 갑판 하부 우회 통로. 관 안에 두는 것은 바닥 유도띠 두 줄뿐이다 — 단면이 웅크림
        /// 높이 그대로라 벽에 뭘 붙이면 통행 폭이 눈으로 좁아진다.
        /// </summary>
        private static void AddBypassProps(List<LastShiftDressingProp> props, Material lane, Material hazard)
        {
            const float section = LastShiftBypassDuct.Section;
            const float half = section * 0.5f;
            var run = LastShiftDressingSpace.OfBypassRun();
            var bounds = LastShiftDressingSpaces.BoundsOf(run);
            var runMinX = LastShiftBypassDuct.ForeShaftX - half;
            var runMaxX = LastShiftBypassDuct.AftShaftX + half;

            props.Add(new LastShiftDressingProp
            {
                id = "DuctLane_Run",
                space = run,
                anchorMode = LastShiftDressingAnchorMode.MetersFromSpaceCenter,
                anchor = new Vector2(0f, LastShiftBypassDuct.RunZ - bounds.CenterZ),
                size = new Vector3(runMaxX - runMinX - section * 2f, 0.03f, 0.30f),
                bottomY = 0.005f,
                material = lane,
                semantics = LastShiftDressingSemantics.LightSource,
                // 0.8 -> 0.7. 바닥 띠 둘이 C4 예산 2.0 중 1.6 을 쓰고 있어 천장 등이
                // 한 개도 안 들어갔다. planning 이 예산 인상 대신 띠를 낮추기로 정했고
                // (합 1.6 -> 1.4), 남은 0.6 이 LSDress_Lamp_Duct 한 개다.
                lightIntensity = 0.7f
            });

            // 선수 다리. 승강구 자리는 비운다 — 단(Step)이 서 있어 띠가 묻히고, 비어 있는
            // 것 자체가 "여기가 오르내리는 자리" 라는 표시다.
            var legMinZ = LastShiftBypassDuct.ForeShaftZ - half;
            var legMaxZ = LastShiftBypassDuct.RunZ - half;
            props.Add(new LastShiftDressingProp
            {
                id = "DuctLane_Leg",
                space = run,
                anchorMode = LastShiftDressingAnchorMode.MetersFromSpaceCenter,
                anchor = new Vector2(LastShiftBypassDuct.ForeShaftX - bounds.CenterX,
                    (legMinZ + legMaxZ) * 0.5f + half * 0.5f - bounds.CenterZ),
                size = new Vector3(0.30f, 0.03f, legMaxZ - legMinZ - half),
                bottomY = 0.005f,
                material = lane,
                semantics = LastShiftDressingSemantics.LightSource,
                lightIntensity = 0.7f
            });

            // 에어록 경고 띠. 바깥 해치는 배 밑면이라 진공으로 나가는 자리다 — 배 안에서
            // 유일하게 선체 밖과 맞닿은 문이고, 그 사실이 색으로 서 있어야 한다.
            const float airlockSize = LastShiftBypassDuct.AirlockSize;
            var airlock = LastShiftDressingSpace.OfAirlock();
            var stripe = 0;
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                props.Add(new LastShiftDressingProp
                {
                    id = $"AirlockStripe_{stripe++}",
                    space = airlock,
                    anchorMode = LastShiftDressingAnchorMode.MetersFromSpaceCenter,
                    anchor = new Vector2(sx * airlockSize * 0.5f, sz * airlockSize * 0.5f),
                    size = new Vector3(0.16f, airlockSize * 0.9f, 0.16f),
                    bottomY = airlockSize * 0.05f,
                    material = hazard
                });
        }

        // ── 천장 등기구 ───────────────────────────────────────────────────────────────

        private const string DressingPrefabFolder = "Assets/DoodleUp/Prefabs/Dressing";

        /// <summary>등기구 프리팹. 없으면 <c>null</c> 이고 그 자리는 박스 폴백으로 선다.</summary>
        private static GameObject LampPrefab(string suffix) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{DressingPrefabFolder}/LSDress_Lamp_{suffix}.prefab");

        /// <summary>
        /// 천장 등. <b>이 슬롯이 배 조명의 정본이다</b> — 예전에는 빌더가 씬에 맨 점광원을
        /// 직접 세웠고(<c>CreateZoneLights</c>), 그래서 art 가 만든 등기구 프리팹이 붙을 자리가
        /// 없었다. 자리·개수는 여기서 정하고 <b>밝기·색·반경 실값은 프리팹에 박혀 있다</b>
        /// (art `last-shift-dressing-assets-v1.md` §3.3). 코드에 숫자를 옮겨 적으면 정본이 둘이 된다.
        ///
        /// <paramref name="props"/> 에 붙는 <c>lightIntensity</c> 만 프리팹에서 읽어 복사한다 —
        /// 이건 표시용 실값이 아니라 §3.4 밝기 합계 검사가 보는 집계 칸이다.
        /// </summary>
        private static void AddCeilingLamps(List<LastShiftDressingProp> props)
        {
            // 구역 등. <b>개수는 구역 길이가 아니라 방 길이에서 뽑는다</b> — 드레싱 공간
            // Zone 의 경계는 RoomMinX~RoomMaxX(방)이고 통로는 별도 공간이다. 예전 빌더는
            // 구역 길이(방+통로)로 간격을 잘라 등 하나가 통로에 떨어졌는데, 슬롯으로 옮기면
            // 그 등이 방 경계 밖이라 R1_Bounds 에 걸린다. 통로는 아래에서 따로 단다.
            foreach (LastShiftZone zone in System.Enum.GetValues(typeof(LastShiftZone)))
                AddSpacedLamps(props, LastShiftDressingSpace.Of(zone), LampPrefab(zone.ToString()));

            // 통로. 6m 짜리 방 사이 구간이라 양쪽 방 등의 range 7 로는 가운데가 처진다.
            // 색은 통로 중심이 속한 구역을 따른다 — 통로는 경계(±7)를 걸치므로 절반씩
            // 나뉘지만, 압력상 어느 구역인지는 중심이 정한다. 조명색이 구역 표시를 겸하는
            // 이상(§CreateLighting) 두 색을 섞으면 그 표시가 흐려진다.
            for (var passage = 0; passage <= 1; passage++)
            {
                var centerX = LastShiftShipDimensions.PassageCenterX(passage);
                var zone = LastShiftZoneAtlas.Resolve(new Vector3(centerX, 0f, 0f));
                AddSpacedLamps(props, LastShiftDressingSpace.OfPassage(passage), LampPrefab(zone.ToString()));
            }

            // 드나들 수 있는 구획만 등을 단다. 잠긴 구획은 들어갈 수 없으므로 등이 낭비고,
            // 잠긴 문틈으로 빛이 새면 §17.7 이 미결로 남긴 "차폐 수준" 을 코드가 먼저 정해 버린다.
            foreach (var spec in LastShiftCompartments.FixedSpecs)
            {
                if (!spec.IsPassable) continue;
                AddLamp(props, LastShiftDressingSpace.Of(spec.Compartment), "Lamp",
                    Vector2.zero, RoomLampSize, LampPrefab(spec.Compartment.ToString()));
            }

            AddLamp(props, LastShiftDressingSpace.OfAirlock(), "Lamp",
                Vector2.zero, RoomLampSize, LampPrefab("Airlock"));

            // 우회 통로 등은 하나다. art §3.4 는 셋(합 1.8)을 전제했지만 C4 예산 2.0 을
            // 바닥 띠 둘이 먼저 쓰고 있어 셋은 들어가지 않는다. planning 이 예산 인상 대신
            // 띠를 1.6 -> 1.4 로 낮추고 등 하나만 두기로 정했다 — "듬성듬성한 어둠이
            // 의도에 맞다". 합계는 1.4 + 0.6 = 2.0 으로 예산에 딱 맞는다.
            //
            // 자리는 관의 가로 구간 중앙이다. 하나뿐이라 간격 규칙(AddSpacedLamps)이
            // 의미가 없고, 가운데에 둬야 양 끝이 대칭으로 어두워진다.
            var run = LastShiftDressingSpace.OfBypassRun();
            var runBounds = LastShiftDressingSpaces.BoundsOf(run);
            AddLamp(props, run, "Lamp",
                new Vector2(0f, LastShiftBypassDuct.RunZ - runBounds.CenterZ),
                DuctLampSize, LampPrefab("Duct"));
        }

        /// <summary>관 등기구 외형 치수. 방 등보다 작다 — 관 단면이 좁다.</summary>
        private static readonly Vector3 DuctLampSize = new(0.30f, 0.09f, 0.20f);

        /// <summary>
        /// 공간 길이에서 등 개수를 뽑아 x 축으로 고르게 배치한다. 간격 <c>5.5</c> 는 점광원
        /// range 7 이 서로 겹쳐 사이가 처지지 않는 값이다(예전 빌더가 쓰던 값 그대로).
        /// </summary>
        private static void AddSpacedLamps(List<LastShiftDressingProp> props,
            LastShiftDressingSpace space, GameObject prefab)
        {
            const float lampSpacing = 5.5f;
            var bounds = LastShiftDressingSpaces.BoundsOf(space);
            var length = bounds.MaxX - bounds.MinX;
            var count = Mathf.Max(1, Mathf.RoundToInt(length / lampSpacing));
            for (var index = 0; index < count; index++)
            {
                var offsetX = (index - (count - 1) * 0.5f) * lampSpacing;
                AddLamp(props, space, count > 1 ? $"Lamp_{index}" : "Lamp",
                    new Vector2(offsetX, 0f), RoomLampSize, prefab);
            }
        }

        /// <summary>등기구 외형 치수. 스케일이 아니라 경계 검사용이다(§ <c>LastShiftDressingProp.size</c>).</summary>
        private static readonly Vector3 RoomLampSize = new(1.6f, 0.16f, 0.34f);

        /// <summary>
        /// 천장면에 밀착시킨다. <c>bottomY</c> 가 밑면 기준이므로 공간 높이에서 등기구 두께를
        /// 뺀 값이 곧 밑면 높이다 — 공간마다 천장이 다르므로(구역 3.2 / 구획 3.0 / 관은 더 낮다)
        /// 상수를 적지 않고 그 공간의 실제 높이에서 뽑는다.
        /// </summary>
        private static LastShiftDressingProp AddLamp(List<LastShiftDressingProp> props,
            LastShiftDressingSpace space, string id, Vector2 anchor, Vector3 size, GameObject prefab)
        {
            var bounds = LastShiftDressingSpaces.BoundsOf(space);
            var prop = new LastShiftDressingProp
            {
                id = id,
                space = space,
                anchorMode = LastShiftDressingAnchorMode.MetersFromSpaceCenter,
                anchor = anchor,
                size = size,
                bottomY = bounds.CeilingY - bounds.FloorY - size.y,
                prefab = prefab,
                semantics = LastShiftDressingSemantics.LightSource,
                lightIntensity = LampIntensityOf(prefab)
            };
            props.Add(prop);
            return prop;
        }

        private static float LampIntensityOf(GameObject prefab)
        {
            if (prefab == null) return 0f;
            var light = prefab.GetComponentInChildren<Light>(true);
            return light != null ? light.intensity : 0f;
        }
    }
}
