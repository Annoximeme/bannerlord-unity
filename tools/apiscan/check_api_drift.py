#!/usr/bin/env python3
"""
API-drift gate (docs/ROADMAP.md Phase 1.3, mitigates RISK-05).

Compares the API surface of the pinned assemblies against a committed baseline
and fails if anything is removed. Scoped to exactly the assemblies
src/Coop.GameInterface references (not the full 11-assembly Phase 0 audit set)
because that's what "anything we bind to" means in practice for this project
right now — extend PINNED_ASSEMBLIES as Coop.GameInterface grows.

Usage:
    python3 check_api_drift.py <dir containing the pinned .dlls>
    python3 check_api_drift.py --write-baseline <dir containing the pinned .dlls>

The pinned assemblies come from the same
Bannerlord.ReferenceAssemblies.Core NuGet package Coop.GameInterface.csproj
restores (docs/VERSION_SUPPORT.md §6/§8) — not the real game install, since CI
has no game install to check against. Their surface is byte-identical to the
real install's (verified, docs/VERSION_SUPPORT.md §6 / TD10).
"""
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
BASELINE_DIR = HERE.parent.parent / "docs" / "evidence" / "api-baseline"

# Every TaleWorlds assembly src/Coop.GameInterface currently references.
PINNED_ASSEMBLIES = [
    "TaleWorlds.CampaignSystem.dll",
    "TaleWorlds.Core.dll",
    "TaleWorlds.Library.dll",
    "TaleWorlds.ModuleManager.dll",
    "TaleWorlds.MountAndBlade.dll",
    "TaleWorlds.ObjectSystem.dll",  # MBObjectBase — added Phase 1.6, EngineObjectIdAdapter
    "TaleWorlds.SaveSystem.dll",
]


def surface(dll_path: Path) -> list:
    result = subprocess.run(
        [sys.executable, str(HERE / "cli_meta.py"), "surface", str(dll_path)],
        capture_output=True,
        text=True,
        check=True,
    )
    return sorted(set(result.stdout.splitlines()))


def write_baseline(pinned_dir: Path) -> int:
    BASELINE_DIR.mkdir(parents=True, exist_ok=True)
    for name in PINNED_ASSEMBLIES:
        dll = pinned_dir / name
        if not dll.exists():
            print(f"MISSING PINNED ASSEMBLY: {dll}", file=sys.stderr)
            return 1
        lines = surface(dll)
        out = BASELINE_DIR / f"{name}.surface.txt"
        out.write_text("\n".join(lines) + "\n", encoding="utf-8")
        print(f"wrote {out} ({len(lines)} entries)")
    return 0


def check(pinned_dir: Path) -> int:
    failed = False
    for name in PINNED_ASSEMBLIES:
        dll = pinned_dir / name
        baseline_file = BASELINE_DIR / f"{name}.surface.txt"
        if not dll.exists():
            print(f"MISSING PINNED ASSEMBLY: {dll}", file=sys.stderr)
            failed = True
            continue
        if not baseline_file.exists():
            print(
                f"MISSING BASELINE: {baseline_file} "
                "(run with --write-baseline first)",
                file=sys.stderr,
            )
            failed = True
            continue

        current = set(surface(dll))
        baseline = set(
            line
            for line in baseline_file.read_text(encoding="utf-8").splitlines()
            if line
        )
        removed = sorted(baseline - current)
        added = sorted(current - baseline)

        if removed:
            print(f"=== {name}: {len(removed)} member(s) REMOVED since baseline ===")
            for line in removed:
                print(f"  - {line}")
            failed = True
        if added:
            print(f"=== {name}: {len(added)} member(s) added since baseline (informational) ===")

    if failed:
        print(
            "\nAPI drift check FAILED: something the baseline recorded is gone "
            "from the pinned assemblies. If this is a deliberate version bump, "
            "re-run with --write-baseline and review the diff by hand first."
        )
        return 1

    print("API drift check passed: no removals since baseline.")
    return 0


def main() -> int:
    args = sys.argv[1:]
    write_mode = "--write-baseline" in args
    args = [a for a in args if a != "--write-baseline"]

    if len(args) != 1:
        print(__doc__, file=sys.stderr)
        return 2

    pinned_dir = Path(args[0])
    if write_mode:
        return write_baseline(pinned_dir)
    return check(pinned_dir)


if __name__ == "__main__":
    sys.exit(main())
