# migrate-logging-categorize.ps1 — Phase 2 폴더별 Main.Log* → Log.<Cat>.<Lvl> 변환
# 사용: pwsh ./scripts/migrate-logging-categorize.ps1 -Folder Analysis -Category Analysis [-DryRun]

param(
    [Parameter(Mandatory)][string]$Folder,
    [Parameter(Mandatory)][string]$Category,
    [switch]$DryRun
)

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

try {
    $targetPath = Join-Path $root $Folder
    if (-not (Test-Path $targetPath)) {
        Write-Error "Folder not found: $Folder"
        exit 1
    }

    $files = Get-ChildItem -Recurse -Include *.cs -Path $targetPath `
        | Where-Object { $_.FullName -notmatch '\\(bin|obj|\.git)\\' }

    $totalFiles = 0
    $totalConversions = 0

    foreach ($f in $files) {
        $content = Get-Content $f.FullName -Raw
        if ($null -eq $content) { continue }
        $original = $content

        # Order matters: LongestSuffix first to avoid Main.Log( matching Main.LogDebug(
        # Main.LogDebug → Log.<Cat>.Debug
        $content = [regex]::Replace($content, 'Main\.LogDebug\(', "Log.$Category.Debug(")
        # Main.LogWarning → Log.<Cat>.Warn
        $content = [regex]::Replace($content, 'Main\.LogWarning\(', "Log.$Category.Warn(")
        # Main.LogError → Log.<Cat>.Error  (handles both string and (ex, string) overloads)
        $content = [regex]::Replace($content, 'Main\.LogError\(', "Log.$Category.Error(")
        # Main.Log( → Log.<Cat>.Info(
        # Now safe — LongestSuffix variants already converted above
        $content = [regex]::Replace($content, 'Main\.Log\(', "Log.$Category.Info(")

        if ($content -ne $original) {
            # using directive 추가: 이미 있으면 skip
            if ($content -notmatch '(?m)^using CompanionAI_v3\.Logging\s*;') {
                # 가장 마지막 using 다음에 삽입
                if ($content -match '(?ms)((?:^using[^\r\n]+\r?\n)+)') {
                    $usings = $matches[1]
                    $newUsings = $usings + "using CompanionAI_v3.Logging;`r`n"
                    $content = $content -replace [regex]::Escape($usings), $newUsings
                } elseif ($content -match '(?m)^namespace\s') {
                    # using 블록 없음 — namespace 직전에 삽입
                    $content = $content -replace '(?m)^(namespace\s)', "using CompanionAI_v3.Logging;`r`n`r`n`$1"
                }
            }

            $totalFiles++
            $convsBefore = ([regex]::Matches($original, 'Main\.Log\w*\(')).Count
            $convsAfter  = ([regex]::Matches($content,  'Main\.Log\w*\(')).Count
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
    Write-Host "Total: $totalFiles files, $totalConversions conversions in $Folder"
    if ($DryRun) { Write-Host "(dry run — no files modified)" }
}
finally {
    Pop-Location
}
