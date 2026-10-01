# CI/CD guide: GitHub Actions + Azure DevOps → NuGet

How `Ovm.Cqrs`, `Ovm.Cqrs.Logging` and `Ovm.Cqrs.EfCore` get built, tested and published.

## The big picture

```
                    ┌──────────────────────── GitHub: olvitmor/ovm-cqrs ────────────────────────┐
                    │                                                                            │
  pull request ───► │  GitHub Actions  ci.yml        build → test → pack  (artifacts only)       │
                    │                                                                            │
  push to main ───► │  Azure Pipelines (via GitHub App)                                          │
                    │     azure-pipelines.yml        build → test → pack → push ──► Azure Artifacts feed "ovm"
                    │                                                    (prereleases, e.g. 0.1.1-alpha.0.3)
                    │                                                                            │
  push tag v* ────► │  GitHub Actions  release.yml   build → test → pack → push ──► nuget.org (public)
                    │                                                       └────► GitHub Release
                    └────────────────────────────────────────────────────────────────────────────┘
```

| Platform        | Trigger              | Publishes to           | Auth                                   |
|-----------------|----------------------|------------------------|----------------------------------------|
| GitHub Actions  | PR, push to `main`   | nothing (artifacts)    | –                                      |
| GitHub Actions  | tag `v*`             | nuget.org + GH Release | **Trusted Publishing** (OIDC, no secret) |
| Azure Pipelines | push to `main`       | Azure Artifacts `ovm`  | Pipeline identity (`NuGetAuthenticate`) |

### How versions are produced

The version comes from git tags via **MinVer**. No file in the repo contains a version.

| Git state                                   | Version produced     |
|---------------------------------------------|----------------------|
| No tags yet, 5 commits                      | `0.0.0-alpha.0.5`    |
| HEAD is tagged `v0.1.0`                     | `0.1.0`              |
| 3 commits after `v0.1.0`                    | `0.1.1-alpha.0.3`    |
| HEAD is tagged `v0.2.0-beta.1`              | `0.2.0-beta.1`       |

> ⚠️ **MinVer needs the full git history and tags.** Every checkout must be non-shallow
> (`fetch-depth: 0` in GitHub Actions, `fetchDepth: 0` + `fetchTags: true` in Azure Pipelines).
> If you forget, every build silently becomes `0.0.0-alpha.0.N`.

To release, run:

```bash
git tag v0.1.0
git push origin v0.1.0
```

---

## Part 1: One-time setup

Do these in order. Steps marked 🖱️ are done in a web portal; steps marked ⌨️ are done in the CLI.

### 1.1 Install the CLIs ⌨️

```bash
brew install gh azure-cli
az extension add --name azure-devops
gh auth login
az login
```

### 1.2 nuget.org account 🖱️

1. Sign in at <https://www.nuget.org> with a Microsoft account, and pick a **username** (profile name).
   You'll need this username in the release workflow; it is not your email address.
2. Turn on 2FA.
3. **Create the Trusted Publishing policy:** avatar → **Trusted Publishing** → **Create**.

   | Field             | Value         |
   |-------------------|---------------|
   | Repository owner  | `olvitmor`    |
   | Repository        | `ovm-cqrs`    |
   | Workflow file     | `release.yml` (file name only, no path) |
   | Environment       | `nuget` (optional but recommended, see 1.3) |

   With this policy, a run of `release.yml` in that repo can swap its GitHub OIDC token for a
   **one-hour** nuget.org API key. You never create or store an API key.

   > If the very first push of a brand-new package ID is rejected under Trusted Publishing,
   > publish `0.1.0` once with a short-lived classic API key (scope: *Push new packages*,
   > glob `Ovm.Cqrs*`, 1-day expiry), then delete the key.

4. **After the first release**, reserve the ID prefix `Ovm.` so nobody else can publish `Ovm.*`
   packages and yours get the verified ✓ badge. See
   <https://learn.microsoft.com/nuget/nuget-org/id-prefix-reservation> (request via account@nuget.org).

### 1.3 GitHub repository settings 🖱️

1. **Environment** (Settings → Environments → New → `nuget`):
   - Add yourself as a *required reviewer*. Each nuget.org publish then waits for your
     click, which is a useful safety net while you learn.
   - Deployment tags: allow only `v*`.
2. **Branch protection** for `main` (Settings → Rules → Rulesets):
   - Require a pull request.
   - Require the status check `build` (from `ci.yml`) to pass.
3. Actions → General → Workflow permissions: leave the default (*read*). Workflows ask for
   extra permissions explicitly.

### 1.4 Azure DevOps organisation + project 🖱️

1. Go to <https://dev.azure.com> → **Create new organization** (e.g. `olvitmor`).
2. Create a **private** project `ovm-cqrs`. Under Project settings → Overview, turn off
   Boards, Repos and Test Plans; keep **Pipelines** and **Artifacts**.
3. **Request the free hosted parallel job:** <https://aka.ms/azpipelines-parallelism-request>.
   New organisations get **0** Microsoft-hosted jobs until the request is approved, which
   usually takes 2–3 business days. Pipelines queue forever until then, so do this first.

### 1.5 Azure Artifacts feed 🖱️ / ⌨️

1. Artifacts → **Create Feed**:
   - Name: `ovm`
   - Visibility: members of the organisation
   - **Upstream sources: ✅ Include packages from common public sources**
   - Scope: **Project** (`ovm-cqrs`)
2. Feed settings → **Permissions** → make sure **`ovm-cqrs Build Service (olvitmor)`** has the
   role **Feed Publisher (Contributor)**. Without it, the push step fails with 403.

The feed URL (you'll need it in several places) is:

```
https://pkgs.dev.azure.com/olvitmor/ovm-cqrs/_packaging/ovm/nuget/v3/index.json
```

> **Upstream shielding:** once `Ovm.Cqrs` is pushed to the feed, the feed stops pulling
> `Ovm.Cqrs` from nuget.org upstream (this protects against dependency confusion). That's
> fine here, because the feed receives every `main` build, including tagged ones.

### 1.6 Connect Azure Pipelines to GitHub 🖱️

1. Pipelines → **New pipeline** → **GitHub** → authorise → install the **Azure Pipelines
   GitHub App** on `olvitmor/ovm-cqrs` only (not on all repositories).
2. Choose **Existing Azure Pipelines YAML file** → `/azure-pipelines.yml`.
3. Rename the pipeline to `ovm-cqrs-prerelease`.

With the GitHub App, run status shows up on the GitHub commit next to the GitHub Actions checks.

---

## Part 2: Files in the repo

### 2.1 `build.sh`: the single source of truth

Both platforms call this script, so the build logic lives in one place and the YAML only
handles triggers, environment and publishing.

```bash
#!/usr/bin/env bash
set -euo pipefail

ARTIFACTS="${1:-./artifacts}"
CONFIG=Release

dotnet restore
dotnet build --configuration "$CONFIG" --no-restore -warnaserror
dotnet test  --configuration "$CONFIG" --no-build
dotnet pack  --configuration "$CONFIG" --no-build --output "$ARTIFACTS"
```

`chmod +x build.sh`. Locally, `./build.sh` does exactly what CI does. That needs Docker running,
because the EfCore tests use Testcontainers.

### 2.2 `Directory.Build.props`: CI-relevant bits

```xml
<PropertyGroup>
  <MinVerTagPrefix>v</MinVerTagPrefix>
  <!-- Deterministic paths in PDBs when running on any CI -->
  <ContinuousIntegrationBuild Condition="'$(CI)' == 'true' or '$(TF_BUILD)' == 'true'">true</ContinuousIntegrationBuild>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
</PropertyGroup>
```

GitHub Actions sets `CI=true`; Azure Pipelines sets `TF_BUILD=True`.

### 2.3 `.github/workflows/ci.yml`

```yaml
name: ci

on:
  pull_request:
    branches: [main]
  push:
    branches: [main]

permissions:
  contents: read

jobs:
  build:
    runs-on: ubuntu-latest          # has Docker → Testcontainers works
    steps:
      - uses: actions/checkout@v5
        with:
          fetch-depth: 0            # MinVer needs history + tags

      - uses: actions/setup-dotnet@v5
        with:
          global-json-file: global.json

      - run: ./build.sh ./artifacts

      - uses: actions/upload-artifact@v4
        with:
          name: packages
          path: artifacts/*
```

### 2.4 `.github/workflows/release.yml`

```yaml
name: release

on:
  push:
    tags: ['v*']

jobs:
  release:
    runs-on: ubuntu-latest
    environment: nuget              # must match the Trusted Publishing policy (if set)
    permissions:
      id-token: write               # ← required for Trusted Publishing (OIDC)
      contents: write               # ← required to create the GitHub Release
    steps:
      - uses: actions/checkout@v5
        with:
          fetch-depth: 0

      - uses: actions/setup-dotnet@v5
        with:
          global-json-file: global.json

      - run: ./build.sh ./artifacts

      - name: NuGet login (OIDC → short-lived API key)
        id: nuget
        uses: NuGet/login@v1
        with:
          user: <your-nuget.org-username>

      - name: Push to nuget.org
        run: >
          dotnet nuget push "artifacts/*.nupkg"
          --api-key "${{ steps.nuget.outputs.NUGET_API_KEY }}"
          --source https://api.nuget.org/v3/index.json
          --skip-duplicate
        # matching .snupkg files are pushed automatically alongside each .nupkg

      - name: GitHub Release
        env:
          GH_TOKEN: ${{ github.token }}
        run: >
          gh release create "$GITHUB_REF_NAME"
          --generate-notes
          ${{ contains(github.ref_name, '-') && '--prerelease' || '' }}
          artifacts/*.nupkg artifacts/*.snupkg
```

### 2.5 `azure-pipelines.yml`

```yaml
trigger:
  branches:
    include: [main]
  # tags are NOT built: releases belong to GitHub Actions

pr: none                            # PRs are validated by GitHub Actions ci.yml

pool:
  vmImage: ubuntu-latest            # has Docker → Testcontainers works

variables:
  feedUrl: https://pkgs.dev.azure.com/olvitmor/ovm-cqrs/_packaging/ovm/nuget/v3/index.json

steps:
  - checkout: self
    fetchDepth: 0                   # MinVer: full history…
    fetchTags: true                 # …and tags

  - task: UseDotNet@2
    inputs:
      useGlobalJson: true

  - script: ./build.sh $(Build.ArtifactStagingDirectory)
    displayName: Build, test, pack

  - task: NuGetAuthenticate@1       # gives the pipeline identity access to the feed
    displayName: Authenticate to Azure Artifacts

  - script: >
      dotnet nuget push "$(Build.ArtifactStagingDirectory)/*.nupkg"
      --source $(feedUrl)
      --api-key az
      --skip-duplicate
    displayName: Push prerelease to feed "ovm"
    # .snupkg is not supported by Azure Artifacts; it is ignored there.

  - publish: $(Build.ArtifactStagingDirectory)
    artifact: packages
```

`--api-key az` is a placeholder. Azure Artifacts ignores the value, but `dotnet nuget push`
requires one; the real credentials come from `NuGetAuthenticate@1`.

---

## Part 3: Day-to-day

### Ship a change

1. Open a branch and a pull request, and wait for `ci / build` to go green → merge.
2. Azure Pipelines builds `main`, and a few minutes later `0.1.1-alpha.0.N` appears in the `ovm` feed.
3. Try it in another project (see Part 4).

### Cut a release

```bash
git switch main && git pull
git tag v0.1.1            # or v0.2.0-beta.1 for a public prerelease
git push origin v0.1.1
```

→ the `release` workflow waits for your approval (environment `nuget`) → pushes to nuget.org →
creates a GitHub Release. Packages appear on nuget.org after indexing, usually 5–15 minutes.

### Undo a bad release

You **cannot delete** a version from nuget.org; you can only **unlist** it (nuget.org → Manage
package → Listing). Fix the problem and ship the next patch version. Never reuse a version number.

---

## Part 4: Consuming the Azure feed from your other projects

`nuget.config` next to the consuming solution:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <!-- upstream to nuget.org is enabled, so this one feed serves everything -->
    <add key="ovm" value="https://pkgs.dev.azure.com/olvitmor/ovm-cqrs/_packaging/ovm/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

Authenticate once per machine:

```bash
sh -c "$(curl -fsSL https://aka.ms/install-artifacts-credprovider.sh)"
dotnet restore --interactive        # opens a device-code login the first time
```

Then reference a prerelease explicitly, for example `dotnet add package Ovm.Cqrs --prerelease`.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Version is `0.0.0-alpha.0.N` on CI but correct locally | Shallow clone | `fetch-depth: 0` / `fetchDepth: 0` + `fetchTags: true` |
| Azure pipeline stuck in *Queued* | No hosted parallel job granted yet | Wait for the parallelism request (1.4 step 3) |
| Azure push: `403 Forbidden` | Build service lacks feed role | Feed settings → Permissions → *Feed Publisher (Contributor)* (1.5) |
| Azure push: `409 Conflict` | Version already in feed | Expected for re-runs; `--skip-duplicate` handles it |
| `NuGet/login` fails: no matching policy | Owner/repo/workflow file/environment differ from the policy | Fix the policy on nuget.org, or the `environment:` in `release.yml` |
| `NuGet/login` fails: missing OIDC token | `id-token: write` missing | Add it under the job's `permissions` |
| nuget.org push: `403` for a new package ID | Trusted Publishing can't create the ID | One-time classic key (see 1.2 note) |
| Testcontainers: `Docker is not running` | Self-hosted agent or local machine without Docker | Use `ubuntu-latest` hosted agents / start Docker Desktop |

> Action versions (`@v5`, `@v4`, `@v1`) were current when this guide was written; check the
> actions' release pages and bump the major versions when you set this up.
