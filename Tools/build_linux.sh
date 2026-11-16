#!/usr/bin/env bash
# Linux desktop test player (used by the autopilot and layout screenshots).
set -u
cd "$(dirname "$0")/.."
Tools/unity_batch.sh VerdantSiege.EditorTools.BuildScript.LinuxBatch linux_build -buildTarget Linux64
code=$?
echo "LINUX BUILD exit=$code"; grep -E "\[VS Build\]|error CS" Logs/linux_build.log | head
exit $code
