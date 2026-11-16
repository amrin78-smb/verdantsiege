#!/usr/bin/env bash
# Regenerates all derived content (settings, materials, fonts, UI sprites, prefabs from Blender models,
# audio, data, scenes) and validates it. Log: Logs/setup.log
# Run Tools/Blender/build_all.sh first when models changed.
set -u
cd "$(dirname "$0")/.."
Tools/unity_batch.sh VerdantSiege.EditorTools.ProjectSetup.RunBatch setup
code=$?
grep -E "\[VS |error CS|Exception" Logs/setup.log | grep -v "^UnityEngine\|^  at " | head -80
echo "SETUP exit=$code"
exit $code
