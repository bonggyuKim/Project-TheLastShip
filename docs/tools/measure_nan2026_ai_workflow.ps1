[CmdletBinding()]
param(
    [string]$BaseUrl = "http://127.0.0.1:8791",
    [string]$ConfigPath = "D:\.adk\config\agentdesk.yaml",
    [string]$Repository = "bonggyuKim/Project-DoodleUp",
    [string]$TitlePrefix = "[LAST SHIFT]"
)

$ErrorActionPreference = "Stop"

function Get-AgentDeskAuthToken {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "AgentDesk config not found: $Path"
    }

    $tokenLine = Get-Content -LiteralPath $Path |
        Where-Object { $_ -match '^\s*auth_token\s*:' } |
        Select-Object -First 1

    if (-not $tokenLine) {
        throw "server.auth_token was not found in $Path"
    }

    $token = ($tokenLine -replace '^\s*auth_token\s*:\s*', '').Trim().Trim('"').Trim("'")
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "server.auth_token is empty in $Path"
    }

    return $token
}

function Get-MinuteStats {
    param([double[]]$Values)

    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 0) {
        return $null
    }

    $middle = if ($sorted.Count % 2 -eq 1) {
        $sorted[[math]::Floor($sorted.Count / 2)]
    }
    else {
        ($sorted[$sorted.Count / 2 - 1] + $sorted[$sorted.Count / 2]) / 2
    }

    return [ordered]@{
        n          = $sorted.Count
        mean_min   = [math]::Round(($sorted | Measure-Object -Average).Average, 1)
        median_min = [math]::Round($middle, 1)
        p25_min    = [math]::Round($sorted[[math]::Floor(($sorted.Count - 1) * 0.25)], 1)
        p75_min    = [math]::Round($sorted[[math]::Floor(($sorted.Count - 1) * 0.75)], 1)
        p90_min    = [math]::Round($sorted[[math]::Floor(($sorted.Count - 1) * 0.90)], 1)
        min_min    = [math]::Round($sorted[0], 1)
        max_min    = [math]::Round($sorted[-1], 1)
    }
}

$token = Get-AgentDeskAuthToken -Path $ConfigPath
$headers = @{ Authorization = "Bearer $token" }
$cardsResponse = Invoke-RestMethod -Uri "$BaseUrl/api/kanban-cards" -Headers $headers -TimeoutSec 30
$dispatchResponse = Invoke-RestMethod -Uri "$BaseUrl/api/dispatches" -Headers $headers -TimeoutSec 60

$cards = @($cardsResponse.cards | Where-Object {
    $_.repo_id -eq $Repository -and $_.title.StartsWith($TitlePrefix, [System.StringComparison]::Ordinal)
})

$cardIds = @{}
foreach ($card in $cards) {
    $cardIds[$card.id] = $true
}

$dispatches = @($dispatchResponse.dispatches | Where-Object {
    $_.kanban_card_id -and $cardIds.ContainsKey([string]$_.kanban_card_id)
})

$doneCards = @($cards | Where-Object {
    $_.status -eq 'done' -and $_.created_at -and $_.completed_at
})

$leadMinutes = @($doneCards | ForEach-Object {
    (([datetimeoffset]$_.completed_at) - ([datetimeoffset]$_.created_at)).TotalMinutes
})

$workWindowMinutes = @($doneCards | Where-Object { $_.started_at } | ForEach-Object {
    (([datetimeoffset]$_.completed_at) - ([datetimeoffset]$_.started_at)).TotalMinutes
})

$completedImplementation = @($dispatches | Where-Object {
    $_.dispatch_type -eq 'implementation' -and
    $_.status -eq 'completed' -and
    $_.created_at -and
    $_.completed_at
})

$implementationAttempts = @($dispatches | Where-Object {
    $_.dispatch_type -eq 'implementation'
})

$implementationMinutes = @($completedImplementation | ForEach-Object {
    (([datetimeoffset]$_.completed_at) - ([datetimeoffset]$_.created_at)).TotalMinutes
})

$implementationAttemptsByCard = @{}
foreach ($dispatch in $implementationAttempts) {
    $cardId = [string]$dispatch.kanban_card_id
    if (-not $implementationAttemptsByCard.ContainsKey($cardId)) {
        $implementationAttemptsByCard[$cardId] = 0
    }
    $implementationAttemptsByCard[$cardId] += 1
}

$repeatedImplementationCards = @($doneCards | Where-Object {
    $implementationAttemptsByCard.ContainsKey([string]$_.id) -and
    $implementationAttemptsByCard[[string]$_.id] -gt 1
})

$formalReviewCards = @($doneCards | Where-Object { [int]$_.review_round -gt 0 })
$completedPhaseGates = @($dispatches | Where-Object {
    $_.dispatch_type -eq 'phase-gate' -and $_.status -eq 'completed'
})

$statusCounts = [ordered]@{}
foreach ($group in @($cards | Group-Object status | Sort-Object Name)) {
    $statusCounts[$group.Name] = $group.Count
}

$firstCreated = @($cards | Where-Object { $_.created_at } | ForEach-Object {
    [datetimeoffset]$_.created_at
} | Sort-Object | Select-Object -First 1)

$lastCompleted = @($doneCards | ForEach-Object {
    [datetimeoffset]$_.completed_at
} | Sort-Object | Select-Object -Last 1)

$result = [ordered]@{
    generated_at = [datetimeoffset]::UtcNow.ToString('o')
    scope = [ordered]@{
        repository   = $Repository
        title_prefix = $TitlePrefix
        source       = "AgentDesk GET /api/kanban-cards and GET /api/dispatches"
        first_card_created_at = if ($firstCreated.Count) { $firstCreated[0].ToString('o') } else { $null }
        last_card_completed_at = if ($lastCompleted.Count) { $lastCompleted[0].ToString('o') } else { $null }
    }
    cards = [ordered]@{
        total         = $cards.Count
        status_counts = $statusCounts
        completed     = $doneCards.Count
    }
    card_lead_time_minutes = Get-MinuteStats -Values $leadMinutes
    card_work_window_minutes = Get-MinuteStats -Values $workWindowMinutes
    implementation_dispatch_window_minutes = Get-MinuteStats -Values $implementationMinutes
    pipeline_evidence = [ordered]@{
        completed_implementation_dispatches = $completedImplementation.Count
        completed_phase_gates                = $completedPhaseGates.Count
        cards_with_formal_review_round       = $formalReviewCards.Count
        cards_with_repeated_implementation_attempts = $repeatedImplementationCards.Count
        repeated_implementation_attempt_proxy_pct   = if ($doneCards.Count) {
            [math]::Round(100 * $repeatedImplementationCards.Count / $doneCards.Count, 1)
        } else { $null }
    }
    interpretation_limits = @(
        "Lead time and work window are wall-clock intervals; they do not isolate model-active time from tool or queue wait.",
        "Repeated implementation attempts include completed, failed, and cancelled dispatches; they are a rework proxy, not a verified QA-rejection rate.",
        "Human intervention duration is not recorded as a timed field.",
        "Provider usage logs are not card-bound and do not contain an authoritative billed-cost field."
    )
}

$result | ConvertTo-Json -Depth 8
