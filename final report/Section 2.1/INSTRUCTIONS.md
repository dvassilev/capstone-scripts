# Measuring the metrics for Section 2 (Project Quality)

All commands are for **PowerShell on Windows**. Run them one at a time.
Measured commit: `897341241f2bfb2439fe0367f4c7c9ccd29a3828` (10 Oct 2026, latest `master` at the time of writing).

## 0. One-time setup

### 0.1 Clone the repository (yes, a local clone is needed)
The tools analyse the source files on disk, the churn metric needs the git history,
and coverage has to run the test suite. A ZIP download is not enough (no history).

```powershell
cd $HOME\Downloads
git clone https://github.com/yt-dlp/yt-dlp.git yt-dlp-quality
cd yt-dlp-quality
git checkout 897341241f2bfb2439fe0367f4c7c9ccd29a3828
```
`git checkout` of a commit puts you in "detached HEAD" state - that is expected.

### 0.2 Create a virtual environment and install the tools
A virtual environment keeps the tools separate from the rest of your Python.

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
```
If PowerShell refuses with "running scripts is disabled", run this once and try again:
```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```
Your prompt now starts with `(.venv)`. Then install:

```powershell
python -m pip install --upgrade pip
python -m pip install radon==6.0.1 complexipy==8.0.1 coverage==7.16.2
python devscripts/install_deps.py --include-group test
```
The last line installs yt-dlp's own dependencies plus `pytest` (the project's official helper).

Check the versions (write them in the report):
```powershell
python -m pip list | Select-String "radon|complexipy|coverage|pytest"
```

### 0.3 Get the helper script
Copy `quality_metrics.py` (from `Downloads\ProjectQuality`) somewhere, e.g. keep it there.
All commands below are run **from the `yt-dlp-quality` folder** with the venv active.

---

## M1 - Size of classes and modules (Modularity)
```powershell
python $HOME\Downloads\ProjectQuality\quality_metrics.py size
```
Prints the 10 largest classes (lines and number of methods) and the 8 largest files.

## M2 - Coupling: fan-in / fan-out (Modularity, Modifiability)
```powershell
python $HOME\Downloads\ProjectQuality\quality_metrics.py coupling
```
Fan-in = how many yt-dlp modules import a module; fan-out = how many it imports.

## M3 - Complexity (Analysability)
Cyclomatic complexity (McCabe), only functions ranked D or worse (CC >= 21):
```powershell
python -m radon cc yt_dlp -s -n D
```
Average for the core vs. the extractors:
```powershell
python -m radon cc yt_dlp -a -s -e "yt_dlp/extractor/*" | Select-Object -Last 2
python -m radon cc yt_dlp/extractor -a | Select-Object -Last 2
```
Cognitive complexity (SonarSource definition), saved to CSV:
```powershell
complexipy yt_dlp --output-format csv --output cognitive.csv -q
```
Open `cognitive.csv` in Excel and sort by the last column.

## M4 - Code churn and hotspots (Modifiability)
```powershell
python $HOME\Downloads\ProjectQuality\quality_metrics.py churn
```
Counts, per file, the commits and changed lines from 1 Sep 2025 to 31 Aug 2026
(same period as Sections 6/7), and multiplies commits by the file's highest
cyclomatic complexity (= hotspot score).

## M5 - Test coverage (Testability) and test pass rate (Faultlessness)
Runs the same core tests as the project's CI (`-m "not download"`), under coverage.
Takes several minutes.
```powershell
python -m coverage run --branch --source=yt_dlp -m pytest -Werror --tb=line -q -m "not download" --junitxml=core_tests.xml -p no:cacheprovider
python -m coverage report --skip-covered --sort=cover | Select-Object -Last 3
python -m coverage report --include="yt_dlp/extractor/*" | Select-Object -Last 1
python -m coverage report --omit="yt_dlp/extractor/*" | Select-Object -Last 1
python -m coverage html
```
`coverage html` creates `htmlcov\index.html` - open it in a browser to see per-file coverage.
The last line of the pytest output gives passed / failed / skipped (= test pass rate).

## M6 - Defect density (Faultlessness)
Number of issues labelled `bug` (core bugs) and `site-bug` (extractor bugs) opened in the period.
Open these two links in a browser; the number of results is shown at the top:

- https://github.com/yt-dlp/yt-dlp/issues?q=is%3Aissue%20label%3Abug%20created%3A2025-09-01..2026-08-31
- https://github.com/yt-dlp/yt-dlp/issues?q=is%3Aissue%20label%3Asite-bug%20created%3A2025-09-01..2026-08-31

Size in source lines (SLOC) for core and extractors:
```powershell
python -m radon raw yt_dlp -s -e "yt_dlp/extractor/*" | Select-Object -Last 12
python -m radon raw yt_dlp/extractor -s | Select-Object -Last 12
```
Defect density = bug issues / (SLOC / 1000).

---

## What to record for each metric (reproducibility)
For the footnote/appendix: tool + version, the exact command above, the commit hash
`8973412`, and the date you ran it.
