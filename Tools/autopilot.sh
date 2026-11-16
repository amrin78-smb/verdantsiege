#!/usr/bin/env bash
# Automated play-through of an encounter on the Linux test player at one or more resolutions.
# The in-game Autoplay harness (-vs-autoplay) plays like a simple human: plants by tapping cards/cells,
# taps pickups, takes screenshots, writes a report, then quits.
# Usage: Tools/autopilot.sh [WxH[:inset] ...]   e.g. Tools/autopilot.sh 1920x1080 2400x1080:90 2048x1536
# Env: VS_ENCOUNTER=<id> (default vh_01), VS_STRATEGY=good|none, VS_SPEED=1|2, SKIP_BUILD=1
set -u
cd "$(dirname "$0")/.."
[ "${SKIP_BUILD:-0}" = 1 ] || Tools/build_linux.sh || exit 1
OUT="$PWD/Logs/AutoPilot"; mkdir -p "$OUT"
RES=("$@"); [ ${#RES[@]} -eq 0 ] && RES=(1920x1080)
rc=0
for R in "${RES[@]}"; do
  WH=${R%%:*}; INSET=0; [[ "$R" == *:* ]] && INSET=${R##*:}
  W=${WH%x*}; H=${WH#*x}; TAG="${W}x${H}"; [ "$INSET" != 0 ] && TAG="${TAG}_inset${INSET}"
  ARGS=(-vs-autoplay -vs-shots "$OUT" -vs-tag "$TAG" -vs-safe-inset "$INSET"
        -vs-encounter "${VS_ENCOUNTER:-vh_01}" -vs-strategy "${VS_STRATEGY:-good}" -vs-speed "${VS_SPEED:-2}")
  rm -rf "$HOME/.config/unity3d/Nocvault/Verdant Siege/verdant_siege_save.json"*
  timeout 1200 xvfb-run -a -s "-screen 0 ${W}x${H}x24" Builds/Linux/VerdantSiege.x86_64 -force-glcore -screen-fullscreen 0 \
    -screen-width "$W" -screen-height "$H" "${ARGS[@]}" -logFile "$PWD/Logs/autopilot_${TAG}.log"
  code=$?; [ $code -ne 0 ] && rc=$code
  echo "== $TAG exit=$code"; tail -6 "$OUT/${TAG}_autoplay_report.txt" 2>/dev/null || grep -E "Exception|Error" "Logs/autopilot_${TAG}.log" | head
done
exit $rc
