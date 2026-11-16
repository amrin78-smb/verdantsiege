#!/usr/bin/env bash
# Regenerates every Verdant Siege model (FBX + .blend + previews + icons) and validates them.
# Usage: Tools/Blender/build_all.sh        (VS_NO_RENDER=1 skips previews; VS_SAMPLES=n sets Cycles samples)
set -u
cd "$(dirname "$0")/../.."
B="${BLENDER:-blender}"
fail=0
for s in sunspore bulwarkbloom glowcap drifter skitter platedbrute heartroot props roster_defenders roster_enemies rootmaw fx emberwild; do
  echo "== model_$s.py"
  out=$("$B" -b --factory-startup --python-exit-code 1 --python "Tools/Blender/model_$s.py" 2>&1); rc=$?
  echo "$out" | grep -E "^\[VS\] [A-Za-z]+ +[0-9]+ tris|self-check failed|^  [a-z0-9]|Error:|Traceback"
  [ $rc -ne 0 ] && { echo "FAILED: model_$s.py"; fail=1; }
done
echo "== branding"
"$B" -b --factory-startup --python-exit-code 1 --python Tools/Blender/branding.py -- icon logo > Logs_branding.tmp 2>&1 || fail=1
grep -E "^\[VS\]|Error:|Traceback" Logs_branding.tmp; rm -f Logs_branding.tmp
python3 Tools/Blender/branding_compose.py || fail=1
out=$("$B" -b --factory-startup --python-exit-code 1 --python Tools/Blender/check_models.py 2>&1) || fail=1
echo "$out" | grep -E "^(CHECK|SUMMARY)"
python3 Tools/Blender/contact_sheet.py Art/Previews/_contact_sheet.png >/dev/null 2>&1 || true
[ $fail -eq 0 ] && echo "BUILD_ALL: PASS" || echo "BUILD_ALL: FAIL"
exit $fail
