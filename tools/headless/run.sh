#!/usr/bin/env bash
# Headless logic check: compiles the pure core + all EditMode test suites with
# mono/mcs and runs them under a minimal NUnit shim. Fast proof on machines
# without the Unity Editor. The authoritative run is GameCI (see docs/ci.md).
set -euo pipefail
cd "$(dirname "$0")/../.."
OUT=tools/headless/bin
mkdir -p "$OUT"
mcs -nologo -nowarn:0414,0219,0169 -out:"$OUT/EmberfallHeadlessTests.exe" \
    -r:System.Web.Extensions.dll \
    -main:Emberfall.Headless.Runner \
    tools/headless/UnityShim.cs \
    tools/headless/NUnitShim.cs \
    Assets/_Project/Scripts/Runtime/Core/*.cs \
    Assets/_Project/Tests/EditMode/*.cs
mono "$OUT/EmberfallHeadlessTests.exe"
