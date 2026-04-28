# convert-silent-catches.ps1 — Phase 1.2 일괄 변환 (패턴 A + D)
# 사용: pwsh ./scripts/convert-silent-catches.ps1 [-DryRun]
#
# 패턴 A: Main.LogDebug($"<prefix> error: {ex.Message}");
#   → Main.LogError(ex, $"<prefix> error");
#
# 패턴 D: Main.LogDebug($"<prefix>: {ex.Message}");  (error 단어 없음)
#   → Main.LogError(ex, $"<prefix>");
#
# 변환 대상 제외: bin/, obj/, .git/, Tools/, Main.cs (LogError 정의 자체)
# 이미 변환된 곳(Main.LogError(ex,) 은 자동 스킵 (정규식이 매칭 안 됨)

param([switch]$DryRun)

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

try {
    $files = Get-ChildItem -Recurse -Include *.cs -Path . `
        | Where-Object { $_.FullName -notmatch '\\(bin|obj|\.git|Tools)\\' } `
        | Where-Object { $_.Name -ne 'Main.cs' }

    $totalFiles = 0
    $totalConversions = 0

    foreach ($f in $files) {
        $content = Get-Content $f.FullName -Raw
        if ($null -eq $content) { continue }
        $original = $content

        # 패턴 A: error: {ex.Message}
        # 캡처는 lazy + \s* 가 trailing space 를 흡수하므로 replacement 에 명시적 공백 필요
        $content = [regex]::Replace($content,
            'Main\.LogDebug\(\$"(\[[^\]]+\][^"]*?)\s*error:\s*\{ex\.Message\}"\);',
            'Main.LogError(ex, $"$1 error");')

        # 패턴 D: 단순 prefix + ": {ex.Message}" (error 단어 없음)
        $content = [regex]::Replace($content,
            'Main\.LogDebug\(\$"(\[[^\]]+\][^"]*?):\s*\{ex\.Message\}"\);',
            'Main.LogError(ex, $"$1");')

        if ($content -ne $original) {
            $totalFiles++
            $convsBefore = ([regex]::Matches($original, 'LogDebug.*ex\.Message')).Count
            $convsAfter  = ([regex]::Matches($content,  'LogDebug.*ex\.Message')).Count
            $convs = $convsBefore - $convsAfter
            $totalConversions += $convs
            $relPath = $f.FullName.Substring($root.Length+1)
            Write-Host "$relPath`: $convs conversions"

            if (-not $DryRun) {
                Set-Content -Path $f.FullName -Value $content -NoNewline -Encoding UTF8
            }
        }
    }

    Write-Host ""
    Write-Host "Total: $totalFiles files, $totalConversions conversions"
    if ($DryRun) { Write-Host "(dry run — no files modified)" }
}
finally {
    Pop-Location
}
