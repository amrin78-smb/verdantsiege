#!/usr/bin/env bash
# Regenerates every Verdant Siege model (FBX + .blend + previews + icons) and validates them.
# Usage: Tools/Blender/build_all.sh        (VS_NO_RENDER=1 skips previews; VS_SAMPLES=n sets Cycles samples)
set -u
cd "$(dirname "$0")/../.."
B="${BLENDER:-blender}"
fail=0
for s in sunspore bulwarkbloom glowcap drifter skitter platedbrute heartroot props roster_defenders roster_enemies rootmaw; do
  echo "== model_$s.py"
  "$B" -b --factory-startup --python "Tools/Blender/model_$s.py" 2>&1 | grep -E "^\[VS\] [A-Za-z]+ +[0-9]+ tris|self-check failed|Error:|Traceback" || true
  [ "${PIPESTATUS[0]}" -ne 0 ] && { echo "FAILED: model_$s.py"; fail=1; }
done
echo "== branding"
"$B" -b --factory-startup --python Tools/Blender/branding.py -- icon logo 2>&1 | grep -E "^\[VS\]|Error:|Traceback" || true
python3 Tools/Blender/branding_compose.py || fail=1
"$B" -b --factory-startup --python Tools/Blender/check_models.py 2>&1 | grep -E "^(CHECK|SUMMARY|  )" || fail=1
python3 Tools/Blender/contact_sheet.py Art/Previews/_contact_sheet.png >/dev/null 2>&1 || true
[ $fail -eq 0 ] && echo "BUILD_ALL: PASS" || echo "BUILD_ALL: FAIL"
exit $fail
