"""
Helper metrics for Section 2 (Project Quality) of the yt-dlp report.

Run from the root of a yt-dlp clone:
    python <path>/quality_metrics.py size      # M1: class size (methods, LOC)
    python <path>/quality_metrics.py coupling  # M2: module fan-in / fan-out
    python <path>/quality_metrics.py churn     # M4: churn and hotspots (needs radon)

Only the standard library is used, except `churn`, which calls radon for complexity.
"""
import ast
import json
import subprocess
import sys
from collections import Counter, defaultdict
from pathlib import Path

PKG = Path("yt_dlp")
CHURN_SINCE, CHURN_UNTIL = "2025-09-01", "2026-09-01"  # same period as Sections 6/7


def py_files():
    return sorted(p for p in PKG.rglob("*.py") if "__pyinstaller" not in p.parts)


def module_name(path):
    parts = list(path.with_suffix("").parts)
    if parts[-1] == "__init__":
        parts = parts[:-1]
    return ".".join(parts)


# ---------------------------------------------------------------- M1: size
def size():
    rows = []
    for f in py_files():
        tree = ast.parse(f.read_text(encoding="utf-8"))
        for node in tree.body:
            if isinstance(node, ast.ClassDef):
                methods = [n for n in node.body if isinstance(n, (ast.FunctionDef, ast.AsyncFunctionDef))]
                loc = node.end_lineno - node.lineno + 1
                rows.append((loc, len(methods), f.as_posix(), node.name))
    rows.sort(reverse=True)
    print(f"{'class':<38}{'file':<45}{'LOC':>7}{'methods':>9}")
    for loc, m, f, name in rows[:10]:
        print(f"{name:<38}{f:<45}{loc:>7}{m:>9}")

    print("\nLargest modules (physical lines):")
    files = sorted(((len(f.read_text(encoding='utf-8').splitlines()), f.as_posix()) for f in py_files()), reverse=True)
    for n, f in files[:8]:
        print(f"  {n:>6}  {f}")


# ------------------------------------------------------------ M2: coupling
def resolve(mod, node, is_pkg):
    """Absolute module names imported by an Import/ImportFrom node."""
    if isinstance(node, ast.Import):
        return [a.name for a in node.names]
    base = mod.split(".") if is_pkg else mod.split(".")[:-1]
    if node.level:
        base = base[: len(base) - (node.level - 1)]
        prefix = ".".join(base + ([node.module] if node.module else []))
    else:
        prefix = node.module or ""
    if node.module is None:  # from . import a, b
        return [f"{prefix}.{a.name}" for a in node.names]
    return [prefix]


def coupling():
    modules = {module_name(f): f for f in py_files()}
    fan_out = defaultdict(set)
    for mod, f in modules.items():
        tree = ast.parse(f.read_text(encoding="utf-8"))
        for node in ast.walk(tree):
            if isinstance(node, (ast.Import, ast.ImportFrom)):
                for target in resolve(mod, node, f.name == "__init__.py"):
                    # map to the closest known module (e.g. yt_dlp.utils._utils -> itself)
                    while target and target not in modules:
                        target = target.rpartition(".")[0]
                    if target and target.startswith("yt_dlp") and target != mod:
                        fan_out[mod].add(target)
    fan_in = Counter(t for targets in fan_out.values() for t in targets)

    print(f"Modules analysed: {len(modules)}")
    print("\nHighest fan-in (how many yt-dlp modules import it):")
    for m, n in fan_in.most_common(8):
        print(f"  {n:>5}  {m}")
    print("\nHighest fan-out (how many yt-dlp modules it imports), excluding registries:")
    ranked = sorted(fan_out.items(), key=lambda kv: -len(kv[1]))
    for m, t in [kv for kv in ranked if not kv[0].endswith(("_extractors", "extractor", "postprocessor"))][:8]:
        print(f"  {len(t):>5}  {m}")
    ext = [m for m in modules if m.startswith("yt_dlp.extractor.") and m != "yt_dlp.extractor._extractors"]
    uses_utils = sum(1 for m in ext if any(t.startswith("yt_dlp.utils") for t in fan_out[m]))
    print(f"\nExtractor modules importing yt_dlp.utils: {uses_utils} of {len(ext)}")


# ------------------------------------------------- M4: churn and hotspots
def churn():
    log = subprocess.run(
        ["git", "log", f"--since={CHURN_SINCE}", f"--until={CHURN_UNTIL}", "--numstat", "--format=@%H"],
        capture_output=True, text=True, encoding="utf-8", check=True).stdout
    lines_changed, commits = Counter(), Counter()
    for line in log.splitlines():
        parts = line.split("\t")
        if len(parts) == 3 and parts[2].startswith("yt_dlp/") and parts[0] != "-":
            lines_changed[parts[2]] += int(parts[0]) + int(parts[1])
            commits[parts[2]] += 1

    cc = json.loads(subprocess.run([sys.executable, "-m", "radon", "cc", "yt_dlp", "-j"],
                                   capture_output=True, text=True, encoding="utf-8", check=True).stdout)
    max_cc = {f.replace("\\", "/"): max((b["complexity"] for b in blocks), default=0)
              for f, blocks in cc.items() if isinstance(blocks, list)}

    print(f"Period: {CHURN_SINCE} .. {CHURN_UNTIL} (exclusive)")
    print(f"\n{'file':<48}{'commits':>8}{'lines':>8}{'max CC':>8}{'hotspot':>9}")
    hot = sorted(((commits[f] * max_cc.get(f, 0), f) for f in commits), reverse=True)
    for score, f in hot[:10]:
        print(f"{f:<48}{commits[f]:>8}{lines_changed[f]:>8}{max_cc.get(f, 0):>8}{score:>9}")
    core = {f: n for f, n in commits.items() if not f.startswith("yt_dlp/extractor/")}
    print(f"\nFiles changed: {len(commits)} (core: {len(core)}); "
          f"commits touching core files: {sum(core.values())}")


if __name__ == "__main__":
    {"size": size, "coupling": coupling, "churn": churn}[sys.argv[1]]()
