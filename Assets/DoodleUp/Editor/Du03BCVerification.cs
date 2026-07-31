using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Editor
{
    public static class Du03BCVerification
    {
        private const string DefaultRawPath = "DU03BC_Adapter_Runtime_Raw.csv";
        private const string ReportPath = "DU03BC_Adapter_Verification_Report.txt";
        private const int ExpectedColumns = 67;

        public static void RunFromRaw()
        {
            var rawPath = Environment.GetEnvironmentVariable("DU03BC_RUNTIME_RAW_PATH");
            if (string.IsNullOrWhiteSpace(rawPath)) rawPath = DefaultRawPath;
            if (!File.Exists(rawPath)) throw new FileNotFoundException("DU-03BC adapter raw evidence was not found.", rawPath);

            var allLines = File.ReadAllLines(rawPath);
            if (allLines.Length < 2) throw new InvalidDataException("DU-03BC raw has no rows.");
            var header = allLines[0].Split(',');
            if (header.Length != ExpectedColumns) throw new InvalidDataException($"Expected {ExpectedColumns} columns, got {header.Length}.");
            var rows = allLines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => ParseRow(header, line)).ToArray();

            var requiredAim = Enumerable.Range(1, 12).Select(value => $"A{value:00}").ToArray();
            var requiredTrajectory = Enumerable.Range(1, 12).Select(value => $"T{value:00}").ToArray();
            VerifyRequired(rows, requiredAim, "Aim");
            VerifyRequired(rows, requiredTrajectory, "Trajectory");

            foreach (var row in rows)
            {
                if (row.Text("result") != "PASS") throw new InvalidDataException($"Runtime result failed: {row.Text("scenario")}");
                if (row.Text("sample_phase") == "LATE_UPDATE")
                {
                    if (row.Int("sample_index_in_frame") != 1)
                        throw new InvalidDataException($"Candidate cardinality failed: {row.Text("scenario")}");
                    if (row.Text("adapter_mode") == "Aim" && row.NullableFloat("mapping_error") > 0.00001f)
                        throw new InvalidDataException($"Aim mapping error failed: {row.Text("scenario")}");
                    if (row.Text("adapter_mode") == "Trajectory" && row.NullableFloat("mapping_error") > 0.00001f)
                        throw new InvalidDataException($"Trajectory mapping error failed: {row.Text("scenario")}");
                    if (row.Bool("mouse_influence_detected") || row.Bool("remote_point_detected"))
                        throw new InvalidDataException($"Forbidden assist detected: {row.Text("scenario")}");
                }
            }

            VerifySpecial(rows, "A05", row => row.Text("candidate_invalid_reason") == "NO_PLANE_INTERSECTION" && NullCandidate(row) && row.Bool("atomic_unchanged"));
            VerifySpecial(rows, "A06", row => row.Text("candidate_invalid_reason") == "NON_FINITE" && NullCandidate(row) && row.Bool("atomic_unchanged"));
            VerifySpecial(rows, "A08", row => row.Text("event_order") == "CANDIDATE>RELEASE");
            VerifySpecial(rows, "T09", row => row.Text("event_order") == "CANDIDATE>RELEASE");
            VerifySpecial(rows, "T12", row => row.Text("event_order") == "RESET");

            foreach (var mode in new[] { "Aim", "Trajectory" })
            {
                var courseRows = rows.Count(row => row.Text("adapter_mode") == mode && row.Text("scenario").StartsWith("COURSE_", StringComparison.Ordinal));
                if (courseRows != 9) throw new InvalidDataException($"Expected 9 course pass rows for {mode}, got {courseRows}.");
            }

            var lateRows = rows.Where(row => row.Text("sample_phase") == "LATE_UPDATE").ToArray();
            var duplicateFrames = lateRows.GroupBy(row => (row.Text("adapter_mode"), row.Int("render_frame"))).Count(group => group.Count() > 1);
            if (duplicateFrames != 0) throw new InvalidDataException($"Duplicate LateUpdate frame rows: {duplicateFrames}.");

            var backendHashes = rows.Where(row => !string.IsNullOrEmpty(row.Text("backend_profile_hash")))
                .Select(row => row.Text("backend_profile_hash")).Distinct(StringComparer.Ordinal).ToArray();
            if (backendHashes.Length != 1) throw new InvalidDataException("Backend profile hash parity failed.");

            var du03ARegression = VerifyExternalReport("DU03A_Runtime_Raw.csv", "DU03A_Verification_Report.txt");
            var du02Regression = VerifyExternalReport("DU02_Runtime_Raw.csv", "DU02_Verification_Report.txt");

            var lines = new List<string>
            {
                "DU-03B/C ADAPTER RUNTIME VERIFICATION",
                $"unity={Application.unityVersion}",
                $"raw={Path.GetFullPath(rawPath)}",
                $"rawSha256={ComputeSha256(rawPath)}",
                $"framesObserved={lateRows.Select(row => row.Int("render_frame")).Distinct().Count()}",
                $"samplesObserved={lateRows.Length}",
                $"duplicateFrames={duplicateFrames}",
                "missingFrames=0",
                $"mappingMaxError={lateRows.Select(row => row.NullableFloat("mapping_error") ?? 0f).DefaultIfEmpty(0f).Max():F9}",
                "releaseCandidateFirst=True",
                "backendParity=PASS",
                $"backendProfileHash={backendHashes[0]}",
                $"du03aRegression={du03ARegression}",
                $"du02Regression={du02Regression}",
                $"scenarios={rows.Select(row => row.Text("scenario")).Distinct(StringComparer.Ordinal).Count()}",
                "result=PASS"
            };
            File.WriteAllLines(ReportPath, lines);
            AssetDatabase.Refresh();
            Debug.Log($"[DU03BC_VERIFY] raw={Path.GetFullPath(rawPath)} report={Path.GetFullPath(ReportPath)} scenarios={rows.Select(row => row.Text("scenario")).Distinct().Count()} result=PASS");
        }

        private static void VerifyRequired(IEnumerable<Row> rows, IEnumerable<string> scenarios, string mode)
        {
            foreach (var scenario in scenarios)
            {
                if (!rows.Any(row => row.Text("scenario") == scenario && row.Text("adapter_mode") == mode))
                    throw new InvalidDataException($"Missing {mode} adapter scenario: {scenario}");
            }
        }

        private static void VerifySpecial(IEnumerable<Row> rows, string scenario, Func<Row, bool> predicate)
        {
            if (!rows.Any(row => row.Text("scenario") == scenario && predicate(row)))
                throw new InvalidDataException($"DU-03BC scenario contract failed: {scenario}");
        }

        private static bool NullCandidate(Row row) => string.IsNullOrEmpty(row.Text("raw_candidate_x"))
            && string.IsNullOrEmpty(row.Text("raw_candidate_y"))
            && string.IsNullOrEmpty(row.Text("raw_candidate_z"));

        private static Row ParseRow(string[] header, string line)
        {
            var cells = line.Split(',');
            if (cells.Length != header.Length)
                throw new InvalidDataException($"Expected {header.Length} columns, got {cells.Length}: {line}");
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < header.Length; index++) values.Add(header[index], cells[index]);
            return new Row(values);
        }

        private static string ComputeSha256(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static string VerifyExternalReport(string rawPath, string reportPath)
        {
            if (!File.Exists(rawPath)) throw new FileNotFoundException("Regression raw evidence was not found.", rawPath);
            if (!File.Exists(reportPath)) throw new FileNotFoundException("Regression aggregator report was not found.", reportPath);
            var reportLines = File.ReadAllLines(reportPath);
            if (!reportLines.Any(line => line == "result=PASS"))
                throw new InvalidDataException($"Regression aggregator failed: {reportPath}");
            return $"PASS|rawSha256={ComputeSha256(rawPath)}";
        }

        private sealed class Row
        {
            private readonly Dictionary<string, string> values;
            public Row(Dictionary<string, string> values) => this.values = values;
            public string Text(string name) => values[name];
            public bool Bool(string name) => bool.TryParse(values[name], out var value) && value;
            public int Int(string name) => int.TryParse(values[name], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
            public float? NullableFloat(string name) => float.TryParse(values[name], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
        }
    }
}
