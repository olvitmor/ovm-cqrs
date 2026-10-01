#!/usr/bin/env bash
# Single source of truth for CI (GitHub Actions and Azure Pipelines) and local builds.
set -euo pipefail

ARTIFACTS="${1:-./artifacts}"
CONFIG=Release

dotnet restore
dotnet build --configuration "$CONFIG" --no-restore -warnaserror
dotnet test  --configuration "$CONFIG" --no-build
for project in src/*/*.csproj; do
  dotnet pack "$project" --configuration "$CONFIG" --no-build --output "$ARTIFACTS"
done
