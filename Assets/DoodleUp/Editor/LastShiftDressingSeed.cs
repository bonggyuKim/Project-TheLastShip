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

        private static int CountLinkedPrefabs(LastShiftDressingSet set)
        {
            var linked = 0;
            foreach (var prop in set.Props)
                if (prop != null && prop.prefab != null) linked++;
            return linked;
        }

        private static string KeyOf(LastShiftDressingProp prop) =>
            $"{prop.space.kind}/{prop.space.zone}/{prop.space.compartment}/" +
            $"{prop.space.passage}/{prop.space.galleryLeg}/{prop.id}";

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
            var indicator = Mat("LS_ServerIndicator");
            var grow = Mat("LS_GrowLight");

            AddCompartmentProps(props, fixture, hazard, indicator, grow);
            AddStateCues(props);
            AddBypassProps(props, lane, hazard);
            AddCeilingLamps(props);
            AddGalleryProps(props);
            return props;
        }

        // ── 구획 ─────────────────────────────────────────────────────────────────────

        private static void AddCompartmentProps(List<LastShiftDressingProp> props,
            Material fixture, Material hazard, Material indicator, Material grow)
        {
            foreach (var spec in LastShiftCompartments.Specs)
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

                switch (compartment)
                {
                    // 관측실 — 앉아서 밖을 보는 방. 콘솔을 낮게 깔아 시선 높이를 비워 둔다.
                    case LastShiftCompartment.Observatory:
                        Add(props, space, "SightingConsole", 0f, -1f, 2.0f, 0.85f, 0.5f, fixture);
                        Add(props, space, "ObserverSeat", 0f, -0.2f, 0.6f, 0.5f, 0.6f, fixture);
                        Add(props, space, "InstrumentColumn", 1f, 1f, 0.4f, 2.0f, 0.4f, fixture);
                        Add(props, space, "StarChart", -1f, 0.3f, 0.12f, 1.0f, 1.4f, tint, 1.0f);
                        break;

                    // 정비창 — 작업대가 양 현에 붙어 가운데 x 동선을 남긴다. 양 끝 x 면은 둘 다
                    // 문 면이라(선수 쪽 관측실, 선미 쪽 화물칸) 거기에는 아무것도 안 붙인다.
                    case LastShiftCompartment.Workshop:
                        Add(props, space, "Bench_Port", 0f, -1f, 3.0f, 0.9f, 0.7f, fixture);
                        Add(props, space, "Bench_Starboard", 0f, 1f, 3.0f, 0.9f, 0.7f, fixture);
                        Add(props, space, "ToolRack", 0f, -1f, 2.4f, 1.0f, 0.3f, fixture, 1.2f);
                        Add(props, space, "PartsPallet", 0.2f, 0f, 1.0f, 0.35f, 1.0f, tint);
                        break;

                    // 화물칸 — 높이가 다른 스택 넷. 같은 높이로 깔면 바닥 무늬로 보인다.
                    case LastShiftCompartment.CargoBay:
                        Add(props, space, "Crate_0", -0.8f, -0.7f, 1.6f, 1.6f, 1.6f, fixture);
                        Add(props, space, "Crate_1", -0.8f, 0.5f, 1.2f, 2.4f, 1.2f, fixture);
                        Add(props, space, "Crate_2", 0.7f, -0.4f, 2.0f, 1.0f, 1.8f, fixture);
                        Add(props, space, "Crate_3", 0.9f, 0.9f, 1.2f, 1.8f, 1.2f, tint);
                        Add(props, space, "LashRail_Port", -0.5f, -1f, 5.5f, 0.06f, 0.2f, hazard);
                        Add(props, space, "LashRail_Starboard", -0.5f, 1f, 5.5f, 0.06f, 0.2f, hazard);
                        break;

                    // 격납고 — 가운데를 비운다. 발진 구역이 비어 있어야 방이 격납고로 읽힌다.
                    case LastShiftCompartment.Hangar:
                        Add(props, space, "Cradle_Fore", 0f, -0.45f, 4.5f, 0.5f, 0.4f, fixture);
                        Add(props, space, "Cradle_Aft", 0f, 0.45f, 4.5f, 0.5f, 0.4f, fixture);
                        Add(props, space, "LaunchMark_Fore", 0f, -0.75f, 5.0f, 0.03f, 0.18f, hazard);
                        Add(props, space, "LaunchMark_Aft", 0f, 0.75f, 5.0f, 0.03f, 0.18f, hazard);
                        Add(props, space, "HangarRack", -1f, 0f, 0.5f, 2.2f, 4.0f, fixture);
                        Add(props, space, "Gantry", 1f, 0f, 0.4f, 2.6f, 5.0f, tint);
                        break;

                    // 서버통신실 — 랙 네 열. 문 면에서 한 열 물려 세워 들어서는 자리를 비운다.
                    case LastShiftCompartment.ServerRoom:
                        for (var rack = 0; rack < 4; rack++)
                        {
                            var uz = -0.35f + rack * 0.45f;
                            Add(props, space, $"Rack_{rack}", 0f, uz, 2.4f, 2.2f, 0.5f, fixture);
                            var led = Add(props, space, $"RackIndicator_{rack}", -0.55f, uz,
                                0.5f, 0.06f, 0.56f, indicator, 1.7f);
                            led.semantics = LastShiftDressingSemantics.RoomSystemReadout |
                                            LastShiftDressingSemantics.LightSource;
                            led.lightIntensity = 1.1f;
                            led.justification =
                                "통신 상태 표현이지 압력 계기가 아니다 — 브리프 §6.2. " +
                                "선체 상태가 아니라 이 방 자체의 정체를 말한다.";
                        }

                        break;

                    // 화장실 — 폭이 좁고 양 끝이 다 문이라 x 동선(z ≈ 0)을 비워 둬야 한다.
                    case LastShiftCompartment.Lavatory:
                        Add(props, space, "Basin", 0f, -1f, 1.2f, 0.9f, 0.5f, fixture);
                        Add(props, space, "Stall_Fore", 0f, 0.45f, 1.2f, 2.0f, 0.08f, tint);
                        Add(props, space, "Stall_Aft", 0f, 0.85f, 1.2f, 2.0f, 0.08f, tint);
                        break;

                    // 숙소 — 2단 침상 두 조 = 네 자리. 4인 승무원과 수를 맞춘다.
                    case LastShiftCompartment.Quarters:
                        foreach (var (bunk, uz) in new[] { ("Port", -0.75f), ("Starboard", 0.75f) })
                        {
                            Add(props, space, $"Bunk_{bunk}_Lower", 0f, uz, 2.6f, 0.25f, 0.9f, fixture, 0.45f);
                            Add(props, space, $"Bunk_{bunk}_Upper", 0f, uz, 2.6f, 0.25f, 0.9f, fixture, 1.55f);
                        }

                        Add(props, space, "Lockers", 0f, -1f, 2.4f, 1.9f, 0.45f, tint);
                        break;

                    // 휴게실 — 넷이 마주 앉는 자리. 이 배에서 유일하게 일 안 하는 방이다.
                    case LastShiftCompartment.Lounge:
                        Add(props, space, "Table", 0f, 0f, 1.6f, 0.75f, 1.2f, fixture);
                        Add(props, space, "Bench_Port", 0f, -0.75f, 2.0f, 0.45f, 0.5f, tint);
                        Add(props, space, "Bench_Starboard", 0f, 0.75f, 2.0f, 0.45f, 0.5f, tint);
                        Add(props, space, "GalleyCounter", 0f, 1f, 3.0f, 1.0f, 0.6f, fixture);
                        break;

                    // 수경재배·산소재생실 — 그로우 라이트가 이 배에서 가장 밝은 색이다.
                    case LastShiftCompartment.Hydroponics:
                        for (var row = 0; row < 2; row++)
                        {
                            var uz = row == 0 ? -0.72f : 0.72f;
                            for (var tier = 0; tier < 3; tier++)
                            {
                                Add(props, space, $"Tray_{row}_{tier}", 0f, uz, 4.4f, 0.14f, 1.0f, fixture, 0.45f + tier * 0.8f);
                                var growth = Add(props, space, $"Growth_{row}_{tier}", 0f, uz, 4.0f, 0.22f, 0.8f, tint, 0.59f + tier * 0.8f);
                                growth.semantics = LastShiftDressingSemantics.RoomSystemReadout;
                                growth.justification =
                                    "방치 시 잎이 마르는 장기관리 축의 표현이다 — 브리프 §5.3. " +
                                    "압력존 게이지가 아니라 이 방이 원래 갖고 있는 상태다.";
                                var light = Add(props, space, $"GrowLight_{row}_{tier}", 0f, uz, 4.2f, 0.06f, 0.24f, grow, 0.86f + tier * 0.8f);
                                light.semantics = LastShiftDressingSemantics.LightSource;
                                light.lightIntensity = 1.6f;
                            }
                        }

                        break;

                    // 의무실 — 침상 하나. 넷이 타는 배에 침상이 하나라는 것 자체가 정보다.
                    case LastShiftCompartment.MedBay:
                        Add(props, space, "MedBed", 0f, -0.3f, 2.1f, 0.6f, 0.9f, fixture);
                        Add(props, space, "ScannerArch", -0.5f, -0.3f, 0.35f, 2.0f, 1.6f, tint);
                        Add(props, space, "MedCabinet", 1f, 1f, 0.5f, 1.8f, 1.6f, fixture);
                        break;

                    // 구명정 — 좌석 넷과 해치 링. 색이 배에서 여기 하나뿐인 적색이다.
                    default:
                        for (var seat = 0; seat < 4; seat++)
                            Add(props, space, $"PodSeat_{seat}",
                                seat < 2 ? -0.55f : 0.15f, seat % 2 == 0 ? -0.55f : 0.55f,
                                0.7f, 1.0f, 0.7f, fixture);
                        var console = Add(props, space, "PodConsole", 0f, -1f, 2.4f, 1.1f, 0.4f, tint);
                        console.semantics = LastShiftDressingSemantics.RoomSystemReadout;
                        console.justification =
                            "발진 가능 상태(잠금=적/가능=녹)는 이 방 고유 시스템이고 승패조건에 " +
                            "직접 관여한다 — 브리프 §4.3. 플레이어가 알아야 하는 유일한 상태등이다.";
                        Add(props, space, "HatchRing", 1f, 0f, 0.2f, 2.2f, 2.2f, tint);
                        break;
                }
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
            foreach (var spec in LastShiftCompartments.Specs)
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

        // ── 상부 회랑 ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 긴 구간을 뺀 다리 넷. 긴 구간은 다른 넷이 <b>붙는</b> 몸통이라 "입구" 도
        /// "맞은편 벽" 도 자기 자신에 대해서는 없다.
        /// </summary>
        private static IEnumerable<LastShiftGalleryLeg> BranchLegs()
        {
            for (var index = 0; index < LastShiftUpperGallery.LegCount; index++)
                if (index != LastShiftUpperGallery.RunLeg)
                    yield return LastShiftUpperGallery.LegAt(index);
        }

        private static GameObject GalleryPrefab(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{DressingPrefabFolder}/LSDress_{name}.prefab");

        /// <summary>회랑 등기구 외형 치수. 방 등보다 앞뒤로 조금 두껍다(art §4.1).</summary>
        private static readonly Vector3 GalleryLampSize = new(1.60f, 0.16f, 0.38f);

        private static readonly Vector3 GalleryCeilingRunSize = new(2.40f, 0.12f, 0.62f);
        private static readonly Vector3 GalleryTrimBandSize = new(2.44f, 0.10f, 0.03f);
        private static readonly Vector3 GalleryMouthFrameSize = new(2.24f, 3.00f, 0.26f);

        /// <summary>등 간격. art §4.2 가 목표 조도 <c>160 lx</c> 에서 역산한 값이다.</summary>
        private const float GalleryLampSpacing = 5.1f;

        /// <summary>유도띠 밑면 높이(art §4.4). 손 높이 아래라 통행에 안 걸린다.</summary>
        private const float GalleryTrimBottomY = 0.45f;

        /// <summary>
        /// 상부 회랑 드레싱(art <c>last-shift-hull-finish-v1.md</c> §4.4). <b>좌표는 한 줄도
        /// 안 적는다</b> — 전부 <see cref="LastShiftUpperGallery"/> 에서 뽑는다. 회랑은 구획
        /// 표에서 파생하는 공간이라 방이 하나만 움직여도 <c>x</c>·<c>z</c> 가 같이 움직이고,
        /// 리터럴을 적으면 그때 소품만 제자리에 남는다.
        ///
        /// <b>넷 다 프리팹이 정본이다.</b> 여기 있는 치수는 스케일이 아니라 경계 검사용
        /// 바깥 치수이고(art §4.1 표), 밝기 실값은 <see cref="LampIntensityOf"/> 로 프리팹에서
        /// 읽는다.
        ///
        /// 회랑에는 <c>C4</c> 밝기 예산이 안 걸린다 — 그 예산은 "불편해야 하는 길"(우회 통로)
        /// 의 것이고 회랑은 실사용 이면 동선이다(§27.4, art §4.3). 유도띠에 <c>Comfort</c> 가
        /// 붙는 것도 같은 이유로 위반이 아니다.
        /// </summary>
        private static void AddGalleryProps(List<LastShiftDressingProp> props)
        {
            var lamp = GalleryPrefab("Lamp_Gallery");
            var ceilingRun = GalleryPrefab("GalleryCeilingRun");
            var trim = GalleryPrefab("GalleryTrimBand");
            var frame = GalleryPrefab("GalleryMouthFrame");

            var run = LastShiftDressingSpace.OfGalleryRun();
            var runBounds = LastShiftDressingSpaces.BoundsOf(run);
            var runLength = runBounds.MaxX - runBounds.MinX;

            // 긴 구간 천장 등. 개수를 간격에서 뽑으므로 회랑이 길어지면 등도 같이 는다.
            var lampCount = Mathf.Max(1, Mathf.RoundToInt(runLength / GalleryLampSpacing));
            for (var index = 0; index < lampCount; index++)
                AddLamp(props, run, $"Lamp_{index}",
                    new Vector2((index - (lampCount - 1) * 0.5f) * GalleryLampSpacing, 0f),
                    GalleryLampSize, lamp);

            // 천장 배관. 등과 달리 <b>끊기면 안 되는</b> 줄이라 간격이 곧 부재 길이다.
            // 남는 자투리는 양 끝에 반씩 두고(가운데 정렬) 개수를 내림으로 잡는다 —
            // 올림하면 마지막 한 장이 회랑 밖으로 나가 R1_Bounds 에 걸린다.
            const float ceilingRunPitch = 2.40f;
            var ceilingRunCount = Mathf.Max(1, Mathf.FloorToInt(runLength / ceilingRunPitch));
            for (var index = 0; index < ceilingRunCount; index++)
                props.Add(GalleryProp($"CeilingRun_{index}", run,
                    new Vector2((index - (ceilingRunCount - 1) * 0.5f) * ceilingRunPitch, 0.5f),
                    GalleryCeilingRunSize, runBounds.CeilingY - runBounds.FloorY - GalleryCeilingRunSize.y,
                    0f, ceilingRun));

            // 개구부 프레임 여섯. 다섯은 긴 구간에서 보이는 면이고(격납고 종점 + 다리 넷의
            // 입구), 하나는 강하 다리 끝의 구명정 면이다. 프레임의 발광 액센트가 로컬 +z 라
            // 회전이 곧 "어느 쪽에서 보라고 세운 문인가" 다.
            foreach (var leg in BranchLegs())
            {
                // 다리 입구는 긴 구간의 안쪽 벽(z = NearZ)에 뚫린 구멍이다. 프레임을 다리가
                // 아니라 긴 구간에 다는 것은 이 문들이 <b>긴 구간을 걷는 사람</b>에게 갈림길을
                // 알리려고 있기 때문이다(art §4.3) — 다리 안에서는 갈림길이 없다.
                props.Add(GalleryProp($"Mouth_{leg.Name}", run,
                    new Vector2(leg.CenterX - runBounds.CenterX, runBounds.MinZ - runBounds.CenterZ),
                    GalleryMouthFrameSize, 0f, 0f, frame));
            }

            // 격납고 종점. 여기만 문 면이 x 법선이라(§27.4 의 AlongX 분기) 프레임을 돌린다.
            props.Add(GalleryProp("Mouth_Hangar", run,
                new Vector2(runBounds.MinX - runBounds.CenterX, 0f),
                Swizzle(GalleryMouthFrameSize), 0f, 90f, frame));

            // 유도띠. 개구부 맞은편 바깥 벽(z = FarZ)에 붙어 문이 있는 자리를 건너편에서
            // 비춘다. 긴 구간 양 끝 둘은 문이 아니라 <b>구간이 여기서 끝난다</b> 는 표시다.
            foreach (var leg in BranchLegs())
            {
                props.Add(GalleryProp($"Trim_{leg.Name}", run,
                    new Vector2(leg.CenterX - runBounds.CenterX, runBounds.MaxZ - runBounds.CenterZ),
                    GalleryTrimBandSize, GalleryTrimBottomY, 180f, trim,
                    LastShiftDressingSemantics.Comfort));
            }

            var halfBand = GalleryTrimBandSize.x * 0.5f;
            foreach (var (id, x) in new[]
                     {
                         ("Trim_ForeEnd", runBounds.MinX + halfBand),
                         ("Trim_AftEnd", runBounds.MaxX - halfBand)
                     })
                props.Add(GalleryProp(id, run,
                    new Vector2(x - runBounds.CenterX, runBounds.MaxZ - runBounds.CenterZ),
                    GalleryTrimBandSize, GalleryTrimBottomY, 180f, trim,
                    LastShiftDressingSemantics.Comfort));

            AddDescentLegProps(props, lamp, trim, frame);
        }

        /// <summary>
        /// 강하 다리. 긴 구간과 달리 <b>z 로 달린다</b> — 길쭉한 소품은 전부 90° 돌아가고,
        /// 그래서 경계 검사에 넣는 치수도 축을 바꿔 적는다(<see cref="Swizzle"/>).
        /// </summary>
        private static void AddDescentLegProps(List<LastShiftDressingProp> props,
            GameObject lamp, GameObject trim, GameObject frame)
        {
            var descent = LastShiftDressingSpace.OfGallery(LastShiftUpperGallery.DescentLeg);
            var bounds = LastShiftDressingSpaces.BoundsOf(descent);

            // 등 둘. 8m 다리에 5.1m 간격을 그대로 쓰면 하나뿐이라 끝이 처진다 —
            // art §4.4 가 4m 간격 둘로 정했다.
            var index = 0;
            foreach (var offset in new[] { 2f, 6f })
                AddLamp(props, descent, $"Lamp_{index++}",
                    new Vector2(0f, bounds.MinZ + offset - bounds.CenterZ),
                    Swizzle(GalleryLampSize), lamp).eulerAngles = new Vector3(0f, 90f, 0f);

            // 구명정 종점. 문 면은 구명정 우현(z 법선)이라 긴 구간 쪽 입구들과 같은 방향이고,
            // 회전이 없다 — art §4.4 표는 여기 90° 로 적었지만, 그 면은 §27.4 의
            // AlongZ 분기라 돌리면 프레임이 문 면과 직교한다.
            props.Add(GalleryProp("Mouth_EscapePod", descent,
                new Vector2(0f, bounds.MinZ - bounds.CenterZ),
                GalleryMouthFrameSize, 0f, 0f, frame));

            // 유도띠 하나. 다리가 z 로 달리므로 벽은 x 면이고, 띠는 선미 쪽 벽에 붙어
            // 안쪽(-x)을 향한다.
            props.Add(GalleryProp("Trim_Wall", descent,
                new Vector2(bounds.MaxX - bounds.CenterX, 0f),
                Swizzle(GalleryTrimBandSize), GalleryTrimBottomY, 270f, trim,
                LastShiftDressingSemantics.Comfort));
        }

        /// <summary>
        /// <c>y</c> 로 90° 돌린 소품의 경계 검사용 치수. <c>x</c> 와 <c>z</c> 를 맞바꾼다 —
        /// <see cref="LastShiftDressingProp.size"/> 는 선체 축 기준이라 회전을 안 따라간다.
        /// </summary>
        private static Vector3 Swizzle(Vector3 size) => new(size.z, size.y, size.x);

        private static LastShiftDressingProp GalleryProp(string id, LastShiftDressingSpace space,
            Vector2 anchor, Vector3 size, float bottomY, float yaw, GameObject prefab,
            LastShiftDressingSemantics semantics = LastShiftDressingSemantics.None) =>
            new()
            {
                id = id,
                space = space,
                anchorMode = LastShiftDressingAnchorMode.MetersFromSpaceCenter,
                anchor = anchor,
                size = size,
                bottomY = bottomY,
                eulerAngles = new Vector3(0f, yaw, 0f),
                prefab = prefab,
                semantics = semantics
            };
    }
}
