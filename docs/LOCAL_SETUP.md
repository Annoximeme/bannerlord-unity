# Running Claude Code Locally (Windows)

**Why this matters for this project.** The Phase 0 audit ran in a cloud session, which is an isolated Linux VM with **no access to your local drives** — `G:\` is unreachable from it. Several audit items (RISK-02, and investigations B6/B7/B8) need the real installation and, in some cases, a running game. A local session can read `G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord` directly.

Verified against the official setup documentation on 2026-09-16.

---

## 1. Prerequisites

| Requirement | Notes |
|---|---|
| Windows 10 1809+ or Windows Server 2019+ | 4 GB+ RAM, x64 or ARM64 |
| A Claude **Pro, Max, Team, Enterprise or Console** account | The free claude.ai plan does **not** include Claude Code |
| **Git for Windows** | *Optional but recommended* — see §3 |
| Python 3.8+ | Only needed for `tools/apiscan/` |

## 2. Native Windows vs WSL — use Native Windows

| Option | Sandboxing | Verdict for this project |
|---|---|---|
| **Native Windows** | Not supported | ✅ **Use this.** The game and the .NET toolchain are Windows-native, and `G:\` is directly readable. |
| WSL 2 | Supported | ❌ Adds a translation layer over `/mnt/g` and complicates running the Windows .NET build tools. |

## 3. Install Git for Windows first (recommended)

https://git-scm.com/downloads/win

Two reasons:

1. **It gives Claude Code the Bash tool.** Without it, Claude Code falls back to the PowerShell tool. Both work, but the project's tooling and docs are written against bash-style commands.
2. **It installs Git Credential Manager**, which is what makes pushing to GitHub painless (§6).

If Claude Code can't find Git Bash, set it in `settings.json`:

```json
{ "env": { "CLAUDE_CODE_GIT_BASH_PATH": "C:\\Program Files\\Git\\bin\\bash.exe" } }
```

## 4. Install Claude Code

**PowerShell** (prompt shows `PS C:\`):

```powershell
irm https://claude.ai/install.ps1 | iex
```

**CMD** (prompt shows `C:\` with no `PS`):

```batch
curl -fsSL https://claude.ai/install.cmd -o install.cmd && install.cmd && del install.cmd
```

**WinGet** (does not auto-update; run `winget upgrade Anthropic.ClaudeCode` periodically):

```powershell
winget install Anthropic.ClaudeCode
```

**Prefer a GUI?** The Desktop app works without the terminal: https://claude.com/download

Native installs auto-update in the background. You do **not** need Administrator rights.

### Verify

```powershell
claude --version     # prints e.g. 2.1.211 (Claude Code)
claude doctor        # read-only diagnostics: install health, settings validation
```

## 5. Get the project

```powershell
cd C:\dev                         # or wherever you keep projects
git clone https://github.com/Annoximeme/bannerlord-unity.git
cd bannerlord-unity
git checkout claude/inspiring-mayer-mqd8zl
claude
```

`CLAUDE.md` sits at the repository root, so a local session loads the project instructions automatically.

## 6. Pushing to GitHub — yes, this is easy

You own `Annoximeme/bannerlord-unity`, so you already have write access. Nothing Claude-Code-specific is required.

**First push:** Git Credential Manager (bundled with Git for Windows) opens a browser window, you authorise once, and the credential is cached in Windows Credential Manager. Every later push is silent.

```powershell
git push -u origin claude/inspiring-mayer-mqd8zl
```

The branch already exists on the remote, so local commits push straight to it.

**Alternatives:** the GitHub CLI (`winget install GitHub.cli`, then `gh auth login`), or an SSH key if you prefer.

Claude Code can run `git push` for you — it will ask for permission the first time.

> ⚠ **Never commit the game assemblies.** TaleWorlds DLLs are proprietary. `.gitignore` already excludes `*.dll`; leave that rule in place.

## 7. First thing to run locally

```powershell
python tools\apiscan\collect_install_report.py "G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
```

This fills in the version block that Phase 0 could not determine (see `docs/VERSION_SUPPORT.md` §5a). Output lands in `docs/install-report/` as text and JSON — safe to commit.

## 8. What a local session unlocks

| Blocked item | Why the cloud session couldn't do it | Local |
|---|---|---|
| B2 — version pin | No access to `G:\` | ✅ Filesystem read |
| B6 — which mutations bump `Ship.VersionNo` | Reference assemblies have no method bodies | ✅ Real DLLs carry IL |
| B7 — `MapEvent` → naval mission launch path | Same | ✅ |
| B8 — siege stage transitions | Same | ✅ |
| B3 — `GameNetwork`-in-campaign probe (RISK-02) | Needs a running game | ✅ Requires launching Bannerlord |
| B9 — save compat with/without War Sails | Needs a running game | ✅ |

For IL-level work (B7, B8 — B6 is done), a decompiler helps: [ILSpy](https://github.com/icsharpcode/ILSpy) or `ilspycmd`. `tools/apiscan/cli_meta.py` reads type and member metadata but deliberately does not decode method bodies.

`dotnet tool install -g ilspycmd` pulls the latest release, which as of 2026-09-17 fails to install (`DotnetToolSettings.xml was not found in the package`). Pin an older release instead: `dotnet tool install -g ilspycmd --version 8.2.0.7535`. Point it at the real installed assemblies (not the reference-assembly NuGet packages, which have no method bodies), e.g.:

```powershell
ilspycmd -t "TaleWorlds.CampaignSystem.Naval.Ship" -r "G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client" "G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll"
```

**Never commit decompiled output.** It reproduces TaleWorlds' own source with real method bodies — legally a stricter case than the metadata-only surface dumps in `docs/evidence/`. Write findings from it into the docs in our own words instead; `.gitignore` blocks `*.decompiled.cs` and `/decompiled/` as a backstop.

## 9. Notes

- **Sandboxing is unavailable on native Windows**, so Claude Code asks permission for commands more often. Expected, not a fault.
- Keep the same branch (`claude/inspiring-mayer-mqd8zl`) so cloud and local work stay on one history.
- If you work in both places, `git pull` before starting to avoid divergence.
