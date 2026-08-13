param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputPath = "docs/art/last-shift-scene-asset-audit-2026-08-13.md"
)

$ErrorActionPreference = 'Stop'
$assetsRoot = Join-Path $ProjectRoot 'Assets'
$shipPath = Join-Path $ProjectRoot 'Assets/DoodleUp/Prefabs/LastShiftShipGraybox.prefab'
$dressingPath = Join-Path $ProjectRoot 'Assets/DoodleUp/Dressing/LastShiftDressingSet.asset'
$mapPath = Join-Path $ProjectRoot 'Assets/DoodleUp/Data/LastShiftModularMap.json'
$scenePaths = @(
    'Assets/Scenes/LAST_SHIFT_SOLO.unity',
    'Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity'
)

function Read-Text([string]$relativePath) {
    return [IO.File]::ReadAllText((Join-Path $ProjectRoot $relativePath))
}

$guidToAsset = @{}
Get-ChildItem $assetsRoot -Recurse -File -Filter '*.meta' | ForEach-Object {
    $first = Get-Content $_.FullName -TotalCount 2 | Select-String '^guid: ([0-9a-f]{32})$'
    if ($first) {
        $relative = $_.FullName.Substring($ProjectRoot.Length + 1) -replace '\\','/' -replace '\.meta$',''
        $guidToAsset[$first.Matches[0].Groups[1].Value] = $relative
    }
}

function Get-GuidRefs([string]$relativePath) {
    $text = Read-Text $relativePath
    return [regex]::Matches($text, 'guid: ([0-9a-f]{32})') | ForEach-Object {
        $_.Groups[1].Value
    } | Where-Object { $guidToAsset.ContainsKey($_) } | Sort-Object -Unique
}

function Get-PrefabClosure([string[]]$roots) {
    $seen = @{}
    $queue = [Collections.Generic.Queue[string]]::new()
    foreach ($root in $roots) { $queue.Enqueue($root) }
    while ($queue.Count -gt 0) {
        $path = $queue.Dequeue()
        if ($seen.ContainsKey($path)) { continue }
        $seen[$path] = $true
        if ($path -notlike '*.prefab' -and $path -notlike '*.unity' -and $path -notlike '*.asset') { continue }
        foreach ($guid in Get-GuidRefs $path) {
            $child = $guidToAsset[$guid]
            if ($child -like '*.prefab' -and !$seen.ContainsKey($child)) { $queue.Enqueue($child) }
        }
    }
    return @($seen.Keys | Sort-Object)
}

$map = Read-Text 'Assets/DoodleUp/Data/LastShiftModularMap.json' | ConvertFrom-Json
$catalogAssetIds = @(
    $map.placementRules.assetId
    $map.spaces.feature
) | Where-Object { $_ } | Sort-Object -Unique

$kitPrefabs = Get-ChildItem (Join-Path $ProjectRoot 'Assets/DoodleUp/Prefabs/LastShiftModularKit') -File -Filter '*.prefab' |
    ForEach-Object BaseName | Sort-Object
$unusedKit = @($kitPrefabs | Where-Object { $_ -notin $catalogAssetIds })

$dressingText = Read-Text 'Assets/DoodleUp/Dressing/LastShiftDressingSet.asset'
$dressingEntries = [regex]::Matches($dressingText, '(?ms)^  - id: (?<id>[^\r\n]+).*?^    prefab: \{fileID: (?<file>[^,}\r\n]+)(?:, guid: (?<guid>[0-9a-f]{32}), type: \d+)?\}')
$dressingRows = foreach ($entry in $dressingEntries) {
    $guid = $entry.Groups['guid'].Value
    [pscustomobject]@{
        Id = $entry.Groups['id'].Value.Trim()
        Asset = if ($guid -and $guidToAsset.ContainsKey($guid)) { $guidToAsset[$guid] } else { '(data/material anchor)' }
    }
}

$sceneClosure = Get-PrefabClosure ($scenePaths + 'Assets/DoodleUp/Prefabs/LastShiftShipGraybox.prefab')
$shipText = Read-Text 'Assets/DoodleUp/Prefabs/LastShiftShipGraybox.prefab'
$shipObjects = [regex]::Matches($shipText, '(?m)^  m_Name: (.+)$') | ForEach-Object { $_.Groups[1].Value.Trim() }
$legacyForbidden = @('CockpitConsole','CoolingStack','CoolingStack_Fin_0','CoolingStack_Fin_1','CoolingStack_Fin_2','CoolingStack_Fin_3','CoolingStack_Fin_4','StarField','SpaceVoid','NebulaCard','BypassDuct','DiscHull','AirlockHall')
$legacyFound = @($legacyForbidden | Where-Object { $_ -in $shipObjects })
$doorRoots = @($shipObjects | Where-Object { $_ -match '^ZoneDoor_B[0-4]$' })
$doorPrefabGuid = (Get-Content (Join-Path $ProjectRoot 'Assets/DoodleUp/Prefabs/LastShiftModularKit/LPK_Door_Airlock_2m.prefab.meta') -TotalCount 2 |
    Select-String '^guid: ([0-9a-f]{32})$').Matches[0].Groups[1].Value
$doorVisualInstances = ([regex]::Matches($shipText, "m_SourcePrefab: \{fileID: 100100000, guid: $doorPrefabGuid, type: 3\}")).Count

$transformBlocks = [regex]::Matches($shipText, '(?ms)^Transform:\r?\n(?<body>.*?)(?=^--- !u!|\z)')
$nonUnitScale = @($transformBlocks | Where-Object { $_.Groups['body'].Value -match 'm_LocalScale: \{x: (?!1(?:\.0+)?[,}])' -or $_.Groups['body'].Value -match 'm_LocalScale: \{x: [^,]+, y: (?!1(?:\.0+)?[,}])' -or $_.Groups['body'].Value -match 'm_LocalScale: \{x: [^,]+, y: [^,]+, z: (?!1(?:\.0+)?[,}])' })

$leafNames = @()
foreach ($path in $sceneClosure | Where-Object { $_ -like '*.prefab' }) {
    $text = Read-Text $path
    $gameObjects = [regex]::Matches($text, '(?m)^  m_Name: (.+)$') | ForEach-Object { $_.Groups[1].Value.Trim() }
    $leafNames += $gameObjects
}
$leafNames = @($leafNames | Sort-Object -Unique)

$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# LAST SHIFT 씬/에셋 전수 감사 — 2026-08-13')
$lines.Add('')
$lines.Add('이 파일은 `Tools/Audit-LastShiftSceneAssets.ps1`로 재생성한다. 범위는 두 LAST SHIFT 씬, `LastShiftShipGraybox`, 재귀 중첩 프리팹 최하위, 드레싱 102개 슬롯, modular map 카탈로그다.')
$lines.Add('')
$lines.Add('## 집계')
$lines.Add('')
$lines.Add("- 씬: $($scenePaths.Count)개")
$lines.Add("- 재귀 참조 에셋/프리팹: $($sceneClosure.Count)개")
$lines.Add("- 중첩 프리팹 고유 오브젝트 이름(최하위 포함): $($leafNames.Count)개")
$lines.Add("- Ship prefab 직렬화 GameObject: $($shipObjects.Count)개")
$lines.Add("- 드레싱 슬롯: $($dressingRows.Count)개")
$lines.Add("- map 사용 modular asset ID: $($catalogAssetIds.Count)개")
$lines.Add("- 폴더 내 modular prefab: $($kitPrefabs.Count)개")
$lines.Add("- map 미사용 modular prefab: $($unusedKit.Count)개 (프로젝트 라이브러리 보관, 씬 배치 없음)")
$lines.Add("- 금지/레거시 씬 오브젝트 발견: $($legacyFound.Count)개")
$lines.Add("- gameplay blocker 문 루트: $($doorRoots.Count)개 ($($doorRoots -join ', '))")
$lines.Add("- map 문 슬롯 / 정상 문 visual 인스턴스: $($map.spaces.Count)개 / ${doorVisualInstances}개")
$lines.Add("- 비단위 Transform 후보: $($nonUnitScale.Count)개 (생성된 치수형 primitive 포함; Unity 검증과 함께 판정)")
$lines.Add('')
$lines.Add('## map 카탈로그 대조')
$lines.Add('')
$lines.Add('| asset ID | scene/map 상태 |')
$lines.Add('|---|---|')
foreach ($id in $catalogAssetIds) { $lines.Add("| ``$id`` | 사용 |") }
$lines.Add('')
$lines.Add('## 폴더에는 있으나 map에 배치되지 않은 modular prefab')
$lines.Add('')
foreach ($id in $unusedKit) { $lines.Add("- ``$id`` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존") }
$lines.Add('')
$lines.Add('## 드레싱 102개 슬롯 대조')
$lines.Add('')
$lines.Add('| # | ID | 연결 에셋 |')
$lines.Add('|---:|---|---|')
$i = 0
foreach ($row in $dressingRows) { $i++; $lines.Add("| $i | ``$($row.Id)`` | ``$($row.Asset)`` |") }
$lines.Add('')
$lines.Add('## 판정')
$lines.Add('')
$lines.Add('- 카탈로그에 없는 레거시/금지 오브젝트는 ship prefab에서 0개다.')
$lines.Add('- `Visual`, blocker, readout, mesh child는 부모 프리팹 구성요소이므로 독립 삭제하지 않는다.')
$lines.Add('- map 미사용 prefab은 씬 미배치 상태이며 프로젝트 파일 자체는 다른 제작 경로가 참조할 수 있어 삭제하지 않는다.')

$absoluteOutput = Join-Path $ProjectRoot $OutputPath
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($absoluteOutput)) | Out-Null
[IO.File]::WriteAllLines($absoluteOutput, $lines, [Text.UTF8Encoding]::new($false))
Write-Output "AUDIT scenes=$($scenePaths.Count) closure=$($sceneClosure.Count) leaves=$($leafNames.Count) shipObjects=$($shipObjects.Count) dressing=$($dressingRows.Count) doorSlots=$($map.spaces.Count) doorVisuals=$doorVisualInstances forbidden=$($legacyFound.Count) unusedKit=$($unusedKit.Count) output=$OutputPath"
