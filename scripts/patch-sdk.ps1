<#
    Patches known defects in the cloned Context Plugins SDKs.

    The SDKs are consumed from a source clone (neither is on NuGet), so these are applied
    to sdk/ after cloning and must be re-applied if you re-clone. Each patch is idempotent.

    Run:  pwsh scripts/patch-sdk.ps1

    PATCH 1 — nytimes: ArticleSearchArticle.PrintPage is typed int? but the live API
    returns a string ("3"), and print_section likewise ("BU"). Every Article Search
    response therefore fails System.Text.Json deserialization with

        JsonException: The JSON value could not be converted to System.Nullable`1[System.Int32].
        Path: $.response.docs[0].print_page

    which makes the Search controller — its only operation — unusable as generated.
    Recorded as FINDINGS.md Finding 8.
#>

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$applied = 0
$skipped = 0

function Set-Patch {
    param([string]$RelativePath, [string]$From, [string]$To, [string]$Label)

    $path = Join-Path $repoRoot $RelativePath
    if (-not (Test-Path $path)) {
        Write-Host "  MISSING  $RelativePath - clone the SDKs first (see README)" -ForegroundColor Yellow
        return
    }

    $text = Get-Content $path -Raw
    if ($text.Contains($To)) {
        Write-Host "  already  $Label"
        $script:skipped++
        return
    }
    if (-not $text.Contains($From)) {
        Write-Host "  NOT FOUND  $Label - the SDK may have been regenerated; re-check the defect" -ForegroundColor Yellow
        return
    }

    Set-Content -Path $path -Value $text.Replace($From, $To) -NoNewline
    Write-Host "  patched  $Label" -ForegroundColor Green
    $script:applied++
}

Write-Host "Patching cloned SDKs..."

Set-Patch -RelativePath 'sdk/nytimes-csharp-sdk/Models/ArticleSearchArticle.cs' `
          -From 'public int? PrintPage { get; init; }' `
          -To   'public string? PrintPage { get; init; }' `
          -Label 'nytimes ArticleSearchArticle.PrintPage int? -> string?'

# PATCH 2 — nytimes: Response1.Meta is bound to the wire name "meta", but Article Search
# returns "metadata". The property therefore always deserializes to null and hit counts are
# permanently unavailable. Recorded as FINDINGS.md Finding 9.
Set-Patch -RelativePath 'sdk/nytimes-csharp-sdk/Models/Response1.cs' `
          -From '[JsonPropertyName("meta")]' `
          -To   '[JsonPropertyName("metadata")]' `
          -Label 'nytimes Response1.Meta wire name meta -> metadata'

Write-Host ""
Write-Host "$applied applied, $skipped already present."
