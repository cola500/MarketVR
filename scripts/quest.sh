#!/usr/bin/env bash
#
# scripts/quest.sh — build the MarketVR Quest APK and/or install + launch it on a connected Quest.
# Copied from Magic Tiles (modelled on CampfireVR's build-quest.sh). Never uninstalls anything: install is `adb install -r`
# of this app's own package id, so other apps on the headset are untouched.
#
# Usage:
#   scripts/quest.sh build           # Unity batchmode build (the project must NOT be open in the Editor). Archives it as
#                                    #   Builds/archive/lilla-fruktstandet_<datum>_<tid>_<sha>[-dirty].apk + .json
#   scripts/quest.sh install <id|apk> # adb install -r of an archived build (its manifest must match), never while the app
#                                    #   is in front (someone may be playing), then the installed build id is verified
#   scripts/quest.sh devices         # connected headsets: serial, USB or wifi, hardware serial
#   scripts/quest.sh wifi            # wireless ADB: headset on USB once -> adb tcpip 5555 + connect; the address is kept
#   scripts/quest.sh connect [ip]    # reconnect over wifi (the kept address); after a headset restart: USB + wifi again
#   scripts/quest.sh disconnect      # drop the wifi connection
#                                    # Every adb call names one device (adb -s). Same headset on USB and wifi: USB is used.
#                                    #   Several headsets: QUEST_DEVICE=<serial|ip:5555> scripts/quest.sh …
#   scripts/quest.sh builds          # list archived builds (nothing is ever deleted)
#   scripts/quest.sh installed       # versionName (contains the build id) of the app on the headset, read-only
#   scripts/quest.sh launch          # start the app on the headset
#   scripts/quest.sh all             # build + install + launch
#   scripts/quest.sh log             # follow Unity's logcat output from the headset
#   scripts/quest.sh perf [seconds]  # read-only: the system's VrApi frame stats while the app is in front (default 60 s):
#                                    #   FPS min/mean, stale frames, CPU/GPU levels; the raw lines in UnityProject/Logs/
#   scripts/quest.sh scene           # regenerate the scene and materials (SceneSetup)
#   scripts/quest.sh snapshots       # the explicit Snapshots renders (graphics), watched; PNGs in UnityProject/Logs/shots/
#   scripts/quest.sh test            # EditMode + PlayMode tests with progress, results in UnityProject/Logs/; stops a hung
#                                    #   run (TEST_STALL s without a finished test, TEST_TIMEOUT s in all); TEST_FILTER=<regex>

set -euo pipefail

UNITY_VERSION="${UNITY_VERSION:-6000.4.7f1}"
UNITY="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
SDK="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/PlaybackEngines/AndroidPlayer/SDK"
ADB="${ADB:-${SDK}/platform-tools/adb}" # overridable (scripts/test/quest-install.test.sh uses a fake)
WIFI_PORT=5555
DEVICE=""
LAST_ARCHIVED=""
AAPT2="$(ls -d "${SDK}/build-tools/"*/aapt2 2>/dev/null | tail -1)"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WIFI_FILE="${QUEST_WIFI_FILE:-${ROOT}/scripts/.quest-wifi}" # git-ignored: the headset's last wifi address
PROJECT="${ROOT}/UnityProject"
APK="${PROJECT}/Builds/LillaFruktstandet-quest.apk"
ARCHIVE="${PROJECT}/Builds/archive"
PACKAGE_ID="com.lillafruktstandet.prototype"
BUILD_TZ="Europe/Stockholm"

# "package versionCode versionName" as the APK itself declares them.
badging() {
    "$AAPT2" dump badging "$1" 2>/dev/null | sed -nE "s/^package: name='([^']*)' versionCode='([^']*)' versionName='([^']*)'.*/\1 \2 \3/p"
}

json_field() { sed -nE "s/.*\"$2\": \"?([^\",]*)\"?,?$/\1/p" "$1" | head -1; }

build() {
    # The build id is decided before Unity runs, so it describes the sources that get built: Stockholm time + short
    # commit, and "-dirty" when the working copy has uncommitted changes (tracked or untracked; .gitignore respected).
    local sha commit dirty stamp iso id name
    commit="$(git -C "$ROOT" rev-parse HEAD)"
    sha="$(git -C "$ROOT" rev-parse --short=7 HEAD)"
    if [[ -n "$(git -C "$ROOT" status --porcelain)" ]]; then dirty=true; else dirty=false; fi
    stamp="$(TZ="$BUILD_TZ" date +%Y-%m-%d_%H%M%S)"
    iso="$(TZ="$BUILD_TZ" date +%Y-%m-%dT%H:%M:%S%z | sed -E 's/([+-][0-9]{2})([0-9]{2})$/\1:\2/')"
    id="${stamp}_${sha}"
    [[ $dirty == true ]] && id="${id}-dirty"
    name="lilla-fruktstandet_${id}"
    if [[ -e "${ARCHIVE}/${name}.apk" || -e "${ARCHIVE}/${name}.json" ]]; then
        echo "[build] ${name} already exists; not overwriting it" >&2
        exit 6
    fi
    [[ $dirty == true ]] && echo "[build] NOTE: uncommitted changes in the working copy; this build is marked -dirty"

    echo "[build] ${id} · Unity ${UNITY_VERSION} → ${APK#"${ROOT}/"} (close the Editor first)"
    FRUKTSTAND_BUILD_ID="$id" FRUKTSTAND_BUILT_AT="$iso" FRUKTSTAND_GIT_COMMIT="$commit" FRUKTSTAND_GIT_DIRTY="$dirty" \
        "$UNITY" -batchmode -nographics -projectPath "$PROJECT" -buildTarget Android \
        -executeMethod Fruktstand.EditorTools.QuestBuild.Build -logFile "${PROJECT}/Logs/quest-build.log" \
        || { echo "[build] failed, see UnityProject/Logs/quest-build.log (nothing archived)" >&2; exit 3; }
    [[ -f "$APK" ]] || { echo "[build] no APK produced (nothing archived)" >&2; exit 3; }

    local pkg code vname hash unity
    read -r pkg code vname <<<"$(badging "$APK")"
    hash="$(shasum -a 256 "$APK" | cut -d' ' -f1)"
    unity="$(sed -nE 's/^m_EditorVersion: (.*)/\1/p' "${PROJECT}/ProjectSettings/ProjectVersion.txt")"
    mkdir -p "$ARCHIVE"
    # noclobber + read-only: an archived build is never overwritten, and nothing here ever deletes one.
    ( set -o noclobber; cat "$APK" > "${ARCHIVE}/${name}.apk" ) || { echo "[build] could not archive ${name}.apk" >&2; exit 6; }
    ( set -o noclobber; cat > "${ARCHIVE}/${name}.json" ) <<JSON || { echo "[build] could not write ${name}.json" >&2; exit 6; }
{
  "buildId": "${id}",
  "builtAt": "${iso}",
  "timeZone": "${BUILD_TZ}",
  "gitCommit": "${commit}",
  "gitDirty": ${dirty},
  "unityVersion": "${unity}",
  "applicationId": "${pkg}",
  "versionName": "${vname}",
  "versionCode": ${code:-null},
  "sha256": "${hash}"
}
JSON
    chmod a-w "${ARCHIVE}/${name}.apk" "${ARCHIVE}/${name}.json"
    if [[ $dirty == true ]]; then
        # The commit alone can't reproduce a dirty build: keep the sources it was built from (tracked + untracked,
        # .gitignore respected; small, no Library/Builds).
        ( cd "$ROOT" && git ls-files -co --exclude-standard -z | xargs -0 tar -czf "${ARCHIVE}/${name}.source.tar.gz" ) \
            && chmod a-w "${ARCHIVE}/${name}.source.tar.gz" \
            && echo "[build] sources of this dirty build: ${ARCHIVE#"${ROOT}/"}/${name}.source.tar.gz"
    fi
    cp -f "${ARCHIVE}/${name}.json" "${APK%.apk}.json" # describes the "latest" copy next to it
    echo "[build] OK · $(du -h "$APK" | cut -f1) · ${pkg} · versionCode ${code} · versionName ${vname}"
    echo "[build] archived: ${ARCHIVE#"${ROOT}/"}/${name}.apk (+ .json)"
    LAST_ARCHIVED="${ARCHIVE}/${name}.apk"
}

# Which build an APK is: its manifest (same name, .json) when the hash matches, otherwise only what the APK declares.
describe() {
    local apk="$1" manifest="${1%.apk}.json" hash
    hash="$(shasum -a 256 "$apk" | cut -d' ' -f1)"
    if [[ -f "$manifest" ]] && [[ "$(json_field "$manifest" sha256)" == "$hash" ]]; then
        echo "[install] build $(json_field "$manifest" buildId) · built $(json_field "$manifest" builtAt) · commit $(json_field "$manifest" gitCommit | cut -c1-10) · local changes: $(json_field "$manifest" gitDirty)"
    else
        echo "[install] unknown build: no matching manifest next to the APK · sha256 ${hash:0:12}"
    fi
    echo "[install] APK declares: $(badging "$apk" | awk '{print $1 " · versionCode " $2 " · versionName " $3}')"
}

# The connected, authorised devices (one serial per line; wifi ones are ip:port).
connected() { "$ADB" devices | awk 'NR>1 && $2=="device" {print $1}'; }
hw_serial() { "$ADB" -s "$1" shell getprop ro.serialno 2>/dev/null | tr -d '\r'; }
is_wifi() { [[ "$1" == *:* ]]; }

# Picks the one headset every adb call goes to (adb -s "$DEVICE"), so USB + wifi to the same headset never gives
# "more than one device". QUEST_DEVICE names it explicitly.
require_device() {
    local list n first serials="" s hw same=true usb=""
    list="$(connected)"
    if [[ -n "${QUEST_DEVICE:-}" ]]; then
        grep -qxF "$QUEST_DEVICE" <<<"$list" || { echo "[adb] QUEST_DEVICE=$QUEST_DEVICE is not connected. Connected: $(echo $list)" >&2; exit 4; }
        DEVICE="$QUEST_DEVICE"
        return
    fi
    n=$(grep -c . <<<"$list" || true)
    if (( n == 0 )); then
        echo "[adb] No authorised Quest connected. USB: plug in USB-C and accept 'Allow USB debugging' in the headset." >&2
        echo "[adb] Wifi: scripts/quest.sh connect (after a headset restart: USB once, then scripts/quest.sh wifi)." >&2
        exit 4
    fi
    first="$(head -1 <<<"$list")"
    if (( n == 1 )); then DEVICE="$first"; return; fi
    while IFS= read -r s; do
        hw="$(hw_serial "$s")"
        [[ -n "$serials" && "$hw" != "$serials" ]] && same=false
        serials="$hw"
        is_wifi "$s" || usb="$s"
    done <<<"$list"
    if [[ $same == true && -n "$usb" ]]; then
        DEVICE="$usb"
        echo "[adb] the same headset is on USB and wifi: using USB ($usb)"
        return
    fi
    echo "[adb] Several headsets are connected: $(echo $list). Choose one: QUEST_DEVICE=<serial> scripts/quest.sh …" >&2
    exit 4
}

adbq() { "$ADB" -s "$DEVICE" "$@"; }

devices() {
    local list s
    list="$(connected)"
    [[ -z "$list" ]] && { echo "[devices] none (USB: plug in and allow debugging; wifi: scripts/quest.sh connect)"; return; }
    while IFS= read -r s; do
        echo "[devices] $s · $(is_wifi "$s" && echo wifi || echo USB) · headset $(hw_serial "$s")"
    done <<<"$list"
}

# Wireless ADB, the documented way (adb tcpip + adb connect): the headset on USB once, on the same wifi as the Mac.
wifi() {
    local usb ip state i
    usb="$(connected | grep -v ':' || true)"
    [[ -n "${QUEST_DEVICE:-}" ]] && usb="$QUEST_DEVICE"
    if [[ -z "$usb" ]]; then echo "[wifi] Connect the headset with USB first (and allow USB debugging), then run this again." >&2; exit 4; fi
    if (( $(grep -c . <<<"$usb") > 1 )); then echo "[wifi] Several headsets on USB: QUEST_DEVICE=<serial> scripts/quest.sh wifi" >&2; exit 4; fi
    ip="$("$ADB" -s "$usb" shell ip -f inet addr show wlan0 2>/dev/null | awk '/inet /{print $2}' | cut -d/ -f1 | head -1 || true)"
    if [[ -z "$ip" ]]; then echo "[wifi] The headset is not on wifi (no address on wlan0). Join the same wifi as the Mac, then try again." >&2; exit 4; fi
    echo "[wifi] headset $usb has wifi address $ip; switching ADB to TCP port $WIFI_PORT"
    "$ADB" -s "$usb" tcpip "$WIFI_PORT" >/dev/null
    for i in 1 2 3 4 5; do
        sleep 1
        "$ADB" connect "$ip:$WIFI_PORT" | grep -q "connected to" && break
    done
    state="$("$ADB" -s "$ip:$WIFI_PORT" get-state 2>/dev/null || true)"
    if [[ "$state" != device ]]; then
        explain_no_connection "$ip" wifi
        exit 4
    fi
    echo "$ip" > "$WIFI_FILE"
    echo "[wifi] connected: $ip:$WIFI_PORT. You can unplug the cable now."
    echo "[wifi] Later: scripts/quest.sh connect. After a headset restart (omstart) wifi debugging is off: USB once + scripts/quest.sh wifi."
}

connect() {
    local ip="${1:-}"
    [[ -z "$ip" && -f "$WIFI_FILE" ]] && ip="$(cat "$WIFI_FILE")"
    [[ -z "$ip" ]] && { echo "[connect] No known wifi address: connect USB once and run scripts/quest.sh wifi." >&2; exit 4; }
    ip="${ip%:*}"
    "$ADB" connect "$ip:$WIFI_PORT" >/dev/null || true
    if [[ "$("$ADB" -s "$ip:$WIFI_PORT" get-state 2>/dev/null || true)" != device ]]; then
        explain_no_connection "$ip" connect
        exit 4
    fi
    echo "$ip" > "$WIFI_FILE"
    echo "[connect] connected: $ip:$WIFI_PORT"
}

# Why adb could not reach the headset. If the port answers to macOS's own nc but adb gets no route, it is macOS's
# Local Network privacy (Sequoia+), which blocks non-Apple programs such as adb until the app that started them may.
explain_no_connection() {
    local ip="$1" what="$2"
    if command -v nc >/dev/null && nc -z -G 2 "$ip" "$WIFI_PORT" 2>/dev/null; then
        echo "[$what] The headset answers on $ip:$WIFI_PORT, but adb is not allowed to reach the local network." >&2
        echo "[$what] macOS: System Settings > Privacy & Security > Local Network: turn it on for the app this runs in" >&2
        echo "[$what]   (Terminal, iTerm, VS Code, Claude …). Then: $ADB kill-server, and run this again." >&2
    elif [[ "$what" == wifi ]]; then
        echo "[$what] Could not connect to $ip:$WIFI_PORT. Same wifi as the Mac? Guest networks often block devices from each other." >&2
    else
        echo "[$what] $ip:$WIFI_PORT does not answer. Wake the headset (put it on or press the power button briefly)." >&2
        echo "[$what] After a restart wifi debugging is off: connect USB once and run scripts/quest.sh wifi." >&2
    fi
}

disconnect() {
    local ip=""
    [[ -f "$WIFI_FILE" ]] && ip="$(cat "$WIFI_FILE")"
    [[ -z "$ip" ]] && { echo "[disconnect] no known wifi address"; return; }
    "$ADB" disconnect "$ip:$WIFI_PORT" || true
}

# True if the app is the one in front on the headset (someone may be playing).
app_in_front() { adbq shell dumpsys activity activities 2>/dev/null | grep -E "topResumedActivity|mResumedActivity" | grep -q "$PACKAGE_ID/"; }

install() {
    local choice="${1:-}" apk manifest want got
    if [[ -z "$choice" ]]; then
        echo "[install] Choose a build: scripts/quest.sh install <build id or archived apk>. Archived builds:" >&2
        builds >&2
        exit 5
    fi
    apk="$choice"
    [[ -f "$apk" ]] || apk="${ARCHIVE}/lilla-fruktstandet_${choice}.apk"
    [[ -f "$apk" ]] || { echo "[install] No archived build '$choice' (scripts/quest.sh builds)" >&2; exit 5; }
    apk="$(cd "$(dirname "$apk")" && pwd)/$(basename "$apk")"
    manifest="${apk%.apk}.json"
    [[ "$(dirname "$apk")" == "$ARCHIVE" && -f "$manifest" ]] || { echo "[install] Only archived builds with their manifest are installed (Builds/archive/*.apk + .json)." >&2; exit 5; }
    [[ "$(badging "$apk" | cut -d' ' -f1)" == "$PACKAGE_ID" ]] || { echo "[install] $apk is not $PACKAGE_ID; refusing" >&2; exit 5; }
    [[ "$(json_field "$manifest" sha256)" == "$(shasum -a 256 "$apk" | cut -d' ' -f1)" ]] || { echo "[install] The APK does not match its manifest (sha256); refusing" >&2; exit 5; }
    describe "$apk"
    require_device
    if app_in_front && [[ "${ALLOW_WHILE_RUNNING:-}" != 1 ]]; then
        echo "[install] The app is in front on the headset: someone may be playing. Not installing (ask first; ALLOW_WHILE_RUNNING=1 overrides)." >&2
        exit 6
    fi
    echo "[install] adb -s $DEVICE install -r ${apk#"${ROOT}/"}"
    adbq install -r "$apk"
    want="$(json_field "$manifest" versionName)"
    got="$(adbq shell dumpsys package "$PACKAGE_ID" | sed -nE 's/^ *versionName=//p' | tr -d '\r' | head -1)"
    if [[ "$got" != "$want" ]]; then
        echo "[install] NOT verified: the headset reports versionName '$got', expected '$want'" >&2
        exit 7
    fi
    echo "[install] verified on $DEVICE: $got"
}

builds() {
    local f
    for f in "$ARCHIVE"/*.apk; do
        [[ -e "$f" ]] || { echo "[builds] none yet"; return; }
        echo "$(du -h "$f" | cut -f1)  ${f#"${ROOT}/"}"
    done
}

installed() {
    require_device
    adbq shell dumpsys package "$PACKAGE_ID" | grep -E "versionName|versionCode|lastUpdateTime" | sed -E 's/^ +/[installed] /'
}

launch() {
    require_device
    echo "[launch] $PACKAGE_ID on $DEVICE"
    adbq shell monkey -p "$PACKAGE_ID" -c android.intent.category.LAUNCHER 1 >/dev/null
}

# EPIC-013: frame-rate evidence from the headset, not the Editor. Only reads logcat (the system's VrApi line, once a
# second); the app must already be running in front with someone wearing the headset, otherwise nothing is measured.
perf() {
    require_device
    local secs="${1:-60}" out="${PROJECT}/Logs/perf_$(date +%Y%m%d_%H%M%S).log"
    app_in_front || { echo "[perf] The app is not in front on the headset: start it (scripts/quest.sh launch) and play first." >&2; exit 5; }
    echo "[perf] $(adbq shell dumpsys package "$PACKAGE_ID" | sed -n 's/.*versionName=//p' | head -1) · ${secs} s · $DEVICE"
    adbq logcat -T 1 -s VrApi:I >"$out" 2>/dev/null &
    local pid=$!
    sleep "$secs"
    kill "$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
    app_in_front || echo "[perf] Note: the app left the front during the measurement; the numbers include that." >&2
    grep "FPS=" "$out" | awk -F'[=,/]' '
        { for (i = 1; i <= NF; i++) {
              if ($i ~ /FPS$/) { f = $(i + 1); t = $(i + 2) }
              if ($i ~ /Stale$/) s += $(i + 1)
              if ($i == "GPU") { c = $(i + 1); g = $(i + 2) } # "CPU4/GPU=4/4": the CPU and GPU levels
          }
          n++; sum += f; if (n == 1 || f < min) min = f; if (f < t - 1) low++
          if (c + 0 > cmax) cmax = c + 0; if (g + 0 > gmax) gmax = g + 0 }
        END { if (n == 0) { print "[perf] no VrApi lines (is the headset worn?)"; exit 1 }
              printf "[perf] %d s: FPS mean %.1f, min %d (target %d), %d s below target, stale frames %d, CPU/GPU level max %d/%d\n", n, sum / n, min, t, low, s, cmax, gmax }'
    echo "[perf] raw lines: ${out#"$ROOT"/}"
}

scene() {
    "$UNITY" -batchmode -nographics -projectPath "$PROJECT" -executeMethod Fruktstand.EditorTools.SceneSetup.CreateScene -quit \
        -logFile "${PROJECT}/Logs/scene.log" || { echo "[scene] failed, see UnityProject/Logs/scene.log" >&2; exit 3; }
    echo "[scene] OK"
}

# A PlayMode run can hang after a failed test (seen in EPIC-003 and EPIC-006): each platform gets TEST_TIMEOUT
# seconds (default 600), then Unity is stopped and the run reported as hung instead of waiting silently.
# One Unity test run, watched (docs/TESTING.md): the test assemblies log "[test-progress]" lines
# (Tests/*/TestProgress.cs); this shows them, stops the run when no test has finished for TEST_STALL s (120) or after
# TEST_TIMEOUT s (600), names the test it stopped in, and saves a stack sample of Unity to Logs/<name>-hang.txt first.
# Usage: unity_tests <platform> <name> [extra Unity args...]
unity_tests() {
    local platform="$1" name="$2"; shift 2
    local limit="${TEST_TIMEOUT:-600}" stall="${TEST_STALL:-120}" log="${PROJECT}/Logs/${name}.log" results="${PROJECT}/Logs/${name}-results.xml"
    rm -f "$results" "$log" "${PROJECT}/Logs/${name}-hang.txt"
    "$UNITY" -batchmode "$@" -projectPath "$PROJECT" -runTests -testPlatform "$platform" -testResults "$results" -logFile "$log" &
    local pid=$! waited=0 quiet=0 seen=0 current="" hung=""
    while kill -0 "$pid" 2>/dev/null; do
        sleep 1; waited=$((waited + 1)); quiet=$((quiet + 1))
        local lines n l
        lines=$(grep -a '^\[test-progress\]' "$log" 2>/dev/null | uniq || true) # Unity may write each line twice
        n=$(grep -c . <<<"$lines" || true)
        if (( n > seen )); then
            while IFS= read -r l; do
                case "$l" in
                    "[test-progress] start "*) current="${l#\[test-progress\] start }" ;;
                    "[test-progress] run "*) [[ -z "${TEST_FILTER:-}" ]] && echo "[test] ${name}: ${l#\[test-progress\] run } tests" ;;
                    "[test-progress] Failed "*) printf '\n[test] %s: FAILED %s\n' "$name" "${l#\[test-progress\] Failed }"; quiet=0 ;;
                    *) quiet=0 ;;
                esac
            done < <(tail -n +$((seen + 1)) <<<"$lines")
            seen=$n
            local last
            last=$(grep -E '^\[test-progress\] (Passed|Failed|Skipped|Inconclusive) ' <<<"$lines" | tail -1 | awk '{print $3}' || true)
            [[ -n "$last" ]] && printf '\r[test] %s: %s  ' "$name" "$last"
        fi
        (( seen == 0 )) && quiet=0 # still compiling / loading: only the overall limit applies
        if (( waited >= limit )); then hung="after ${limit}s (TEST_TIMEOUT)"; elif (( quiet >= stall )); then hung="no test finished for ${stall}s (TEST_STALL)"; fi
        if [[ -n "$hung" ]]; then
            sample "$pid" 3 -file "${PROJECT}/Logs/${name}-hang.txt" >/dev/null 2>&1 || true
            kill "$pid" 2>/dev/null || true
            printf '\n[test] %s: HUNG, %s; stopped%s.\n       Log: %s; stack sample: %s\n' "$name" "$hung" \
                "${current:+ in ${current}}" "${log#"${ROOT}/"}" "UnityProject/Logs/${name}-hang.txt" >&2
            return 1
        fi
    done
    echo
    local status=0 line
    wait "$pid" || status=1
    line=$(grep -m1 -o '<test-run [^>]*' "$results" 2>/dev/null || true)
    echo "[test] ${name}: $(sed -E 's/.*total="([0-9]+)".*passed="([0-9]+)".*failed="([0-9]+)".*skipped="([0-9]+)".*/\2 passed, \3 failed, \4 skipped (of \1)/' <<<"$line")"
    return $status
}

run_tests() {
    local status=0 filter=()
    [[ -n "${TEST_FILTER:-}" ]] && filter=(-testFilter "$TEST_FILTER")
    for platform in EditMode PlayMode; do
        unity_tests "$platform" "$platform" -nographics ${filter[@]+"${filter[@]}"} || status=1
    done
    return $status
}

# Off-headset renders (the explicit Snapshots tests need graphics), watched like the tests; PNGs in Logs/shots/.
snapshots() {
    rm -rf "${PROJECT}/Logs/shots"
    local status=0
    TEST_FILTER="${TEST_FILTER:-Snapshots}" unity_tests PlayMode snapshots -testFilter "${TEST_FILTER:-Snapshots}" || status=1
    local f
    for f in "${PROJECT}"/Logs/shots/*.bmp; do [[ -e "$f" ]] && sips -s format png "$f" --out "${f%.bmp}.png" >/dev/null; done
    echo "[snapshots] $(ls "${PROJECT}"/Logs/shots/*.png 2>/dev/null | wc -l | tr -d ' ') images in UnityProject/Logs/shots/"
    return $status
}

case "${1:-}" in
    build) build ;;
    install) install "${2:-}" ;;
    devices) devices ;;
    wifi) wifi ;;
    connect) connect "${2:-}" ;;
    disconnect) disconnect ;;
    builds) builds ;;
    installed) installed ;;
    launch) launch ;;
    all) build; install "$LAST_ARCHIVED"; launch ;;
    scene) scene ;;
    test) run_tests ;;
    snapshots) snapshots ;;
    log) require_device; adbq logcat -s Unity ;;
    perf) perf "${2:-}" ;;
    *) sed -n '2,26p' "$0"; exit 1 ;;
esac
