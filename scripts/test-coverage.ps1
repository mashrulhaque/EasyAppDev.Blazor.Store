# Test Coverage Script for EasyAppDev.Blazor.Store
# This script runs tests with coverage collection and generates an HTML report

$ErrorActionPreference = "Stop"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Running Tests with Coverage..." -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan

# Navigate to solution root
Set-Location $PSScriptRoot\..

# Run tests with coverage
dotnet test `
  /p:CollectCoverage=true `
  /p:CoverletOutputFormat=cobertura `
  /p:CoverletOutput=./TestResults/coverage.cobertura.xml `
  /p:Exclude="[*.Tests]*" `
  --configuration Release `
  --verbosity minimal

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Generating HTML Coverage Report..." -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan

# Check if reportgenerator is installed
$reportGeneratorInstalled = Get-Command reportgenerator -ErrorAction SilentlyContinue

if (-not $reportGeneratorInstalled) {
    Write-Host "Warning: reportgenerator not found!" -ForegroundColor Yellow
    Write-Host "Install it with: dotnet tool install -g dotnet-reportgenerator-globaltool" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Coverage data saved at: ./tests/EasyAppDev.Blazor.Store.Tests/TestResults/coverage.cobertura.xml" -ForegroundColor Green
    exit 0
}

# Generate HTML report
reportgenerator `
  -reports:./tests/EasyAppDev.Blazor.Store.Tests/TestResults/coverage.cobertura.xml `
  -targetdir:./TestResults/coverage-report `
  -reporttypes:Html

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host "Coverage Report Generated!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host "Open: ./TestResults/coverage-report/index.html" -ForegroundColor Green
Write-Host ""

# Display coverage summary if available
if (Test-Path "./TestResults/coverage-report/Summary.txt") {
    Write-Host "Coverage Summary:" -ForegroundColor Cyan
    Get-Content "./TestResults/coverage-report/Summary.txt"
}
