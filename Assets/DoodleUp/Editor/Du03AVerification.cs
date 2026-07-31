using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Editor
{
    public static class Du03AVerification
    {
        private const string DefaultRawPath = "DU03A_Runtime_Raw.csv";
        private const string ReportPath = "DU03A_Verification_Report.txt";
        private const int ExpectedColumns = 50;

        public static void RunFromRaw()
        {
            var rawPath = Environment.GetEnvironmentVariable("DU03A_RUNTIME_RAW_PATH");
            if (string.IsNullOrWhiteSpace(rawPath)) rawPath = DefaultRawPath;
            if (!File.Exists(rawPath)) throw new FileNotFoundException("DU-03A runtime raw evidence was not found.", rawPath);

            var allLines = File.ReadAllLines(rawPath);
            if (allLines.Length < 2) throw new InvalidDataException("DU-03A raw has no scenario rows.");
            var header = allLines[0].Split(',');
            if (header.Length != ExpectedColumns) throw new InvalidDataException($"Expected {ExpectedColumns} columns, got {header.Length}.");
            var rows = allLines.Skip(1)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => ParseRow(header, line))
                .ToDictionary(row => row.Text("scenario"), StringComparer.Ordinal);
            var lines = new List<string>
            {
                "DU-03A REV1 RUNTIME VERIFICATION",
                $"unity={Application.unityVersion}",
                $"raw={Path.GetFullPath(rawPath)}",
                $"rawSha256={ComputeSha256(rawPath)}"
            };

            Verify(rows, lines, "short_cancel", CanonicalCancelled);
            Verify(rows, lines, "pending_confirm", row =>
                row.Text("state_after") == "Idle"
                && row.Text("terminal_state") == "Committed"
                && row.Int("pending_colliders") == 0
                && row.Int("final_committed_count") == 1
                && row.Int("collider_count") == row.Int("segment_count") - row.Int("degenerate_skipped")
                && row.Int("collider_count") > 0
                && row.Int("capsule_direction") == 1
                && Approximately(row.Float("capsule_radius"), 0.14f)
                && Approximately(row.Float("capsule_height"), row.Float("expected_capsule_height"))
                && row.Bool("capsule_center_zero") && row.Bool("capsule_non_trigger")
                && row.Bool("root_scale_one") && row.Bool("child_scale_one")
                && row.Bool("midpoint_aligned") && row.Bool("y_axis_aligned")
                && row.Float("max_shared_endpoint_gap") <= 0.000001f
                && row.Int("render_frame") > 0
                && row.Long("late_update_sequence") > 0
                && row.Text("sample_phase") == "LATE_UPDATE"
                && row.Int("candidate_count_this_frame") == 1
                && row.Text("event_order") == "CANDIDATE>RELEASE");
            Verify(rows, lines, "pending_cancel", CanonicalCancelled);
            Verify(rows, lines, "reach_atomic", InvalidAtomic);
            Verify(rows, lines, "ink_atomic", row => InvalidAtomic(row) && row.Text("reason") == "InkInvalid");
            Verify(rows, lines, "r_reset_pending", CanonicalReset);
            Verify(rows, lines, "invalid_release_under_min", row => InvalidAtomic(row) && CanonicalCancelled(row));
            Verify(rows, lines, "invalid_release_over_min", row => InvalidAtomic(row)
                && row.Text("state_after") == "Pending" && row.Float("final_pending_reserved") >= 0.20f);
            Verify(rows, lines, "drawing_cancel", CanonicalCancelled);
            Verify(rows, lines, "pending_new_draw_reject", row => row.Text("state_after") == "Pending"
                && row.Text("event_order") == "PRESS_REJECTED" && row.Bool("atomic_unchanged"));
            Verify(rows, lines, "out_of_state_confirm", row => row.Text("state_after") == "Idle"
                && row.Int("final_committed_count") == 0 && row.Int("collider_count") == 0
                && row.Text("event_order") == "CONFIRM_REJECTED" && row.Bool("atomic_unchanged"));
            Verify(rows, lines, "confirm_release_same_frame", row => row.Text("state_after") == "Pending"
                && row.Text("event_order") == "CONFIRM_REJECTED>CANDIDATE>RELEASE"
                && row.Bool("accepted_appended") && row.Int("final_committed_count") == 0);
            Verify(rows, lines, "mode_parity_aim", row => row.Text("mode") == "Aim");
            Verify(rows, lines, "mode_parity_trajectory", row => row.Text("mode") == "Trajectory");

            VerifyModeParity(rows["mode_parity_aim"], rows["mode_parity_trajectory"]);
            foreach (var row in rows.Values)
            {
                if (!Approximately(row.Float("final_ledger_total"), 5f))
                    throw new InvalidDataException($"Ledger total failed: {row.Text("scenario")}");
                if (row.Text("result") != "PASS")
                    throw new InvalidDataException($"Runtime result failed: {row.Text("scenario")}");
            }

            if (rows.Count != 14) throw new InvalidDataException($"Expected 14 DU-03A scenarios, got {rows.Count}.");
            lines.Add("ledgerFormula=final_available+final_drawing_reserved+final_pending_reserved+final_committed_charged=5.000000");
            lines.Add("scenarios=14");
            lines.Add("result=PASS");
            File.WriteAllLines(ReportPath, lines);
            AssetDatabase.Refresh();
            Debug.Log($"[DU03A_VERIFY] raw={Path.GetFullPath(rawPath)} report={Path.GetFullPath(ReportPath)} scenarios={rows.Count} result=PASS");
        }

        private static bool CanonicalCancelled(Row row)
        {
            return row.Text("state_after") == "Idle"
                && row.Text("terminal_state") == "Cancelled"
                && Approximately(row.Float("final_available"), 5f)
                && Approximately(row.Float("final_drawing_reserved"), 0f)
                && Approximately(row.Float("final_pending_reserved"), 0f)
                && Approximately(row.Float("final_committed_charged"), 0f)
                && row.Int("final_accepted_points") == 0
                && row.Int("final_pending_count") == 0
                && row.Int("final_committed_count") == 0;
        }

        private static bool CanonicalReset(Row row)
        {
            return row.Text("state_after") == "Idle"
                && row.Text("terminal_state") == "Idle"
                && Approximately(row.Float("final_available"), 5f)
                && Approximately(row.Float("final_drawing_reserved"), 0f)
                && Approximately(row.Float("final_pending_reserved"), 0f)
                && Approximately(row.Float("final_committed_charged"), 0f)
                && row.Int("final_accepted_points") == 0
                && row.Int("final_pending_count") == 0
                && row.Int("final_committed_count") == 0;
        }

        private static bool InvalidAtomic(Row row)
        {
            return !row.Bool("candidate_valid")
                && !row.Bool("accepted_appended")
                && row.Bool("atomic_unchanged")
                && row.Int("candidate_points_before") == row.Int("candidate_points_after")
                && Approximately(row.Float("candidate_length_before"), row.Float("candidate_length_after"))
                && Approximately(row.Float("candidate_available_before"), row.Float("candidate_available_after"))
                && Approximately(row.Float("candidate_drawing_before"), row.Float("candidate_drawing_after"))
                && Approximately(row.Float("candidate_pending_before"), row.Float("candidate_pending_after"));
        }

        private static void VerifyModeParity(Row aim, Row trajectory)
        {
            var fields = new[]
            {
                "state_after", "candidate_points_after", "candidate_length_after",
                "candidate_available_after", "candidate_drawing_after", "candidate_pending_after",
                "final_available", "final_drawing_reserved", "final_pending_reserved",
                "final_committed_charged", "final_ledger_total", "charged_length", "simplified_points"
            };
            foreach (var field in fields)
            {
                if (aim.Text(field) != trajectory.Text(field))
                    throw new InvalidDataException($"Aim/Trajectory parity failed: {field}");
            }
        }

        private static void Verify(Dictionary<string, Row> rows, ICollection<string> lines,
            string scenario, Func<Row, bool> contract)
        {
            if (!rows.TryGetValue(scenario, out var row))
                throw new InvalidDataException($"Missing DU-03A scenario: {scenario}");
            var passed = contract(row) && row.Text("result") == "PASS";
            lines.Add(FormattableString.Invariant(
                $"scenario={scenario} mode={row.Text("mode")} state={row.Text("state_after")} finalAvailable={row.Float("final_available"):F6} finalDrawing={row.Float("final_drawing_reserved"):F6} finalPending={row.Float("final_pending_reserved"):F6} finalCommitted={row.Float("final_committed_charged"):F6} ledgerTotal={row.Float("final_ledger_total"):F6} colliders={row.Int("collider_count")} phase={row.Text("sample_phase")} order={row.Text("event_order")} result={(passed ? "PASS" : "FAIL")}"));
            if (!passed) throw new InvalidDataException($"DU-03A runtime scenario failed: {scenario}");
        }

        private static Row ParseRow(string[] header, string line)
        {
            var cells = line.Split(',');
            if (cells.Length != header.Length)
                throw new InvalidDataException($"Expected {header.Length} columns, got {cells.Length}: {line}");
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < header.Length; index++) values.Add(header[index], cells[index]);
            return new Row(values);
        }

        private static bool Approximately(float left, float right) => Mathf.Abs(left - right) <= 0.0001f;

        private static string ComputeSha256(string path)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private sealed class Row
        {
            private readonly Dictionary<string, string> values;

            public Row(Dictionary<string, string> values) => this.values = values;
            public string Text(string name) => values[name];
            public bool Bool(string name) => bool.TryParse(values[name], out var parsed) && parsed;
            public int Int(string name) => int.Parse(values[name], CultureInfo.InvariantCulture);
            public long Long(string name) => long.Parse(values[name], CultureInfo.InvariantCulture);
            public float Float(string name) => float.Parse(values[name], CultureInfo.InvariantCulture);
        }
    }
}
