#!/bin/bash

# Test Coverage Script for EasyAppDev.Blazor.Store
# This script runs tests with coverage collection and generates an HTML report

set -e

echo "======================================"
echo "Running Tests with Coverage..."
echo "======================================"

# Navigate to solution root
cd "$(dirname "$0")/.."

# Run tests with coverage
dotnet test \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura \
  /p:CoverletOutput=./TestResults/coverage.cobertura.xml \
  /p:Exclude="[*.Tests]*" \
  --configuration Release \
  --verbosity minimal

echo ""
echo "======================================"
echo "Generating HTML Coverage Report..."
echo "======================================"

# Check if reportgenerator is installed
if ! command -v reportgenerator &> /dev/null; then
    echo "Warning: reportgenerator not found!"
    echo "Install it with: dotnet tool install -g dotnet-reportgenerator-globaltool"
    echo ""
    echo "Coverage data saved at: ./tests/EasyAppDev.Blazor.Store.Tests/TestResults/coverage.cobertura.xml"
    exit 0
fi

# Generate HTML report
reportgenerator \
  -reports:./tests/EasyAppDev.Blazor.Store.Tests/TestResults/coverage.cobertura.xml \
  -targetdir:./TestResults/coverage-report \
  -reporttypes:Html

echo ""
echo "======================================"
echo "Coverage Report Generated!"
echo "======================================"
echo "Open: ./TestResults/coverage-report/index.html"
echo ""

# Display coverage summary if available
if [ -f "./TestResults/coverage-report/Summary.txt" ]; then
    echo "Coverage Summary:"
    cat "./TestResults/coverage-report/Summary.txt"
fi
