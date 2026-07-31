using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Editor
{
    public static class Du02Verification
    {
        private const string ReportPath = "DU02_Verification_Report.txt";
        private const string DefaultRawPath = "DU02_Runtime_Raw.csv";
        private const string IdentityRotation = "0.000000|0.000000|0.000000|1.000000";
        private const string ZeroVector = "0.000000|0.000000|0.000000";

        public static void RunFromRaw()
        {
            var rawPath = Environment.GetEnvironmentVariable("DU02_RUNTIME_RAW_PATH");
            if (string.IsNullOrWhiteSpace(rawPath)) rawPath = DefaultRawPath;
            if (!File.Exists(rawPath)) throw new FileNotFoundException("DU-02 runtime raw evidence was not found.", rawPath);

            var rows = File.ReadAllLines(rawPath).Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).Select(ParseRow).ToList();
            var samplingRows = rows.Where(row => row.RecordType == "sampling").ToList();
            var resetRows = rows.Where(row => row.RecordType == "reset").ToList();
            var lines = new List<string>
            {
                "DU-02 REV2 VERIFICATION",
                $"unity={Application.unityVersion}",
                $"raw={Path.GetFullPath(rawPath)}",
                $"rawSha256={ComputeSha256(rawPath)}"
            };

            foreach (var fps in new[] { 30, 60, 144 })
            {
                var matches = samplingRows.Where(row => row.RequestedFps == fps).ToList();
                if (matches.Count != 1) throw new InvalidDataException($"Expected one sampling row for {fps} fps, got {matches.Count}.");
                var row = matches[0];
                var passed = row.ObservedFrames > 0
                    && row.ObservedFrames == row.ObservedSamples
                    && row.DuplicateFrames == 0
                    && row.MissingFrames == 0
                    && row.ElapsedSeconds >= 10d;
                lines.Add(FormattableString.Invariant($"sampling fps={fps} observedFrames={row.ObservedFrames} observedSamples={row.ObservedSamples} duplicate={row.DuplicateFrames} missing={row.MissingFrames} elapsed={row.ElapsedSeconds:F6} resetGeneration={row.ResetGeneration} result={(passed ? "PASS" : "FAIL")}"));
                if (!passed) throw new InvalidDataException($"Runtime sampling evidence failed at {fps} fps.");
            }

            var expectedTasks = new[] { "T1Horizontal", "T2Rising", "T3Bridge" };
            var expectedPaths = new[] { "R_KEY", "LANE_SELECT" };
            foreach (var task in expectedTasks)
            {
                foreach (var path in expectedPaths)
                {
                    var matches = resetRows.Where(row => row.Task == task && row.ResetPath == path).ToList();
                    if (matches.Count != 1) throw new InvalidDataException($"Expected one reset row for {task}/{path}, got {matches.Count}.");
                    var row = matches[0];
                    var fieldsPassed = row.BeforeDiffers
                        && row.AfterEqual
                        && !string.Equals(row.BaselineHash, row.BeforeHash, StringComparison.Ordinal)
                        && string.Equals(row.BaselineHash, row.AfterHash, StringComparison.Ordinal)
                        && row.BaselineRotation == IdentityRotation
                        && row.BeforeRotation != IdentityRotation
                        && row.AfterRotation == IdentityRotation
                        && row.BaselineAngularVelocity == ZeroVector
                        && row.BeforeAngularVelocity != ZeroVector
                        && row.AfterAngularVelocity == ZeroVector
                        && row.BaselinePhase == "Idle"
                        && row.BeforePhase == "ProbePerturbed"
                        && row.AfterPhase == "Idle"
                        && row.RotationRestored
                        && row.AngularVelocityRestored
                        && row.PhaseRestored;
                    lines.Add($"reset task={task} path={path} generation={row.ResetGeneration} baselineHash={row.BaselineHash} beforeHash={row.BeforeHash} afterHash={row.AfterHash} beforeDiffers={row.BeforeDiffers} afterEqual={row.AfterEqual} baselineRotation={row.BaselineRotation} beforeRotation={row.BeforeRotation} afterRotation={row.AfterRotation} baselineAngularVelocity={row.BaselineAngularVelocity} beforeAngularVelocity={row.BeforeAngularVelocity} afterAngularVelocity={row.AfterAngularVelocity} baselinePhase={row.BaselinePhase} beforePhase={row.BeforePhase} afterPhase={row.AfterPhase} rotationRestored={row.RotationRestored} angularVelocityRestored={row.AngularVelocityRestored} phaseRestored={row.PhaseRestored} result={(fieldsPassed ? "PASS" : "FAIL")}");
                    if (!fieldsPassed) throw new InvalidDataException($"Runtime reset evidence failed for {task}/{path}.");
                }
            }

            if (samplingRows.Count != 3 || resetRows.Count != 6)
                throw new InvalidDataException($"Unexpected raw row counts: sampling={samplingRows.Count}, reset={resetRows.Count}.");

            lines.Add("result=PASS");
            File.WriteAllLines(ReportPath, lines);
            AssetDatabase.Refresh();
            Debug.Log($"[DU02_VERIFY] raw={Path.GetFullPath(rawPath)} report={Path.GetFullPath(ReportPath)} samplingRows={samplingRows.Count} resetRows={resetRows.Count} d3r1=PASS result=PASS");
        }

        private static EvidenceRow ParseRow(string line)
        {
            var cells = line.Split(',');
            if (cells.Length != 27) throw new InvalidDataException($"Expected 27 columns, got {cells.Length}: {line}");
            return new EvidenceRow
            {
                RecordType = cells[0],
                RequestedFps = ParseInt(cells[1]),
                ObservedFrames = ParseLong(cells[2]),
                ObservedSamples = ParseLong(cells[3]),
                DuplicateFrames = ParseLong(cells[4]),
                MissingFrames = ParseLong(cells[5]),
                ElapsedSeconds = ParseDouble(cells[6]),
                ResetGeneration = ParseInt(cells[7]),
                Task = cells[8],
                ResetPath = cells[9],
                BaselineHash = cells[10],
                BeforeHash = cells[11],
                AfterHash = cells[12],
                BeforeDiffers = ParseBool(cells[13]),
                AfterEqual = ParseBool(cells[14]),
                BaselineRotation = cells[15],
                BeforeRotation = cells[16],
                AfterRotation = cells[17],
                BaselineAngularVelocity = cells[18],
                BeforeAngularVelocity = cells[19],
                AfterAngularVelocity = cells[20],
                BaselinePhase = cells[21],
                BeforePhase = cells[22],
                AfterPhase = cells[23],
                RotationRestored = ParseBool(cells[24]),
                AngularVelocityRestored = ParseBool(cells[25]),
                PhaseRestored = ParseBool(cells[26])
            };
        }

        private static bool ParseBool(string value) => bool.TryParse(value, out var parsed) && parsed;
        private static int ParseInt(string value) => string.IsNullOrEmpty(value) ? 0 : int.Parse(value, CultureInfo.InvariantCulture);
        private static long ParseLong(string value) => string.IsNullOrEmpty(value) ? 0 : long.Parse(value, CultureInfo.InvariantCulture);
        private static double ParseDouble(string value) => string.IsNullOrEmpty(value) ? 0d : double.Parse(value, CultureInfo.InvariantCulture);

        private static string ComputeSha256(string path)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private sealed class EvidenceRow
        {
            public string RecordType;
            public int RequestedFps;
            public long ObservedFrames;
            public long ObservedSamples;
            public long DuplicateFrames;
            public long MissingFrames;
            public double ElapsedSeconds;
            public int ResetGeneration;
            public string Task;
            public string ResetPath;
            public string BaselineHash;
            public string BeforeHash;
            public string AfterHash;
            public bool BeforeDiffers;
            public bool AfterEqual;
            public string BaselineRotation;
            public string BeforeRotation;
            public string AfterRotation;
            public string BaselineAngularVelocity;
            public string BeforeAngularVelocity;
            public string AfterAngularVelocity;
            public string BaselinePhase;
            public string BeforePhase;
            public string AfterPhase;
            public bool RotationRestored;
            public bool AngularVelocityRestored;
            public bool PhaseRestored;
        }
    }
}
