#!/usr/bin/env bash
set -Eeuo pipefail

client_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
series="$client_dir/patches/series"
[[ -f "$series" ]] || exit 0

if [[ $# -ne 0 ]]; then
    if [[ $# -eq 1 && "$1" == --record-current ]]; then
        record_current=true
    else
        printf 'Usage: %s [--record-current]\n' "$0" >&2
        exit 2
    fi
else
    record_current=false
fi

declare -a patch_targets=()
declare -a patch_files=()
declare -a patch_names=()
declare -a targets=()

while IFS=' ' read -r target patch_name || [[ -n "${target:-}" ]]; do
    [[ -z "${target:-}" || "$target" == \#* ]] && continue
    case "$target" in
        client) target_dir="$client_dir" ;;
        protocol) target_dir="$client_dir/Protocol" ;;
        *) printf 'Unknown patch target in %s: %s\n' "$series" "$target" >&2; exit 1 ;;
    esac
    patch_file="$client_dir/patches/$patch_name"
    [[ -f "$patch_file" ]] || { printf 'Patch file is missing: %s\n' "$patch_file" >&2; exit 1; }
    [[ -d "$target_dir/.git" || -f "$target_dir/.git" ]] || {
        printf 'Git checkout for patch target is missing: %s\n' "$target_dir" >&2
        exit 1
    }
    if [[ ! " ${targets[*]} " =~ " ${target} " ]]; then
        targets+=("$target")
    fi
    patch_targets+=("$target")
    patch_files+=("$patch_file")
    patch_names+=("$patch_name")
done < "$series"

target_fingerprint() {
    local target_name="$1"
    local target_root="$2"
    local index patch path
    local -a paths=()
    local -A seen=()

    for ((index = 0; index < ${#patch_files[@]}; index++)); do
        [[ "${patch_targets[index]}" == "$target_name" ]] || continue
        while IFS= read -r path; do
            [[ -n "$path" && -z "${seen[$path]+x}" ]] || continue
            seen["$path"]=1
            paths+=("$path")
        done < <(awk '$1 == "---" && $2 ~ /^a\// { print substr($2, 3) }
                     $1 == "+++" && $2 ~ /^b\// { print substr($2, 3) }' "${patch_files[index]}")
    done

    {
        git -C "$target_root" rev-parse HEAD
        sha256sum "$series"
        for ((index = 0; index < ${#patch_files[@]}; index++)); do
            [[ "${patch_targets[index]}" == "$target_name" ]] || continue
            sha256sum "${patch_files[index]}"
        done
        for path in "${paths[@]}"; do
            if [[ -f "$target_root/$path" ]]; then
                sha256sum "$target_root/$path"
            else
                printf 'MISSING  %s\n' "$path"
            fi
        done
    } | sha256sum | cut -d ' ' -f 1
}

declare -a pending_targets=()
for target in "${targets[@]}"; do
    case "$target" in
        client) target_dir="$client_dir" ;;
        protocol) target_dir="$client_dir/Protocol" ;;
    esac
    marker="$(git -C "$target_dir" rev-parse --absolute-git-dir)/lancer-nexus-patches-state"
    fingerprint="$(target_fingerprint "$target" "$target_dir")"

    if [[ "$record_current" == true ]]; then
        printf '%s\n' "$fingerprint" > "$marker"
        printf 'Recorded %s as applied. Use this only after applying and validating the full patch stack.\n' "$target"
        continue
    fi

    if [[ -f "$marker" ]] && [[ "$(cat "$marker")" == "$fingerprint" ]]; then
        printf 'Verified applied patch state for %s.\n' "$target"
        continue
    fi
    pending_targets+=("$target")
done

if [[ "$record_current" == true ]]; then
    exit 0
fi

for ((index = 0; index < ${#patch_files[@]}; index++)); do
    target="${patch_targets[index]}"
    [[ " ${pending_targets[*]} " == *" $target "* ]] || continue
    case "$target" in
        client) target_dir="$client_dir" ;;
        protocol) target_dir="$client_dir/Protocol" ;;
    esac

    if git -C "$target_dir" apply --check "${patch_files[index]}" 2>/dev/null; then
        git -C "$target_dir" apply "${patch_files[index]}"
        printf 'Applied %s\n' "${patch_names[index]}"
    elif git -C "$target_dir" apply --reverse --check "${patch_files[index]}"; then
        printf 'Already applied %s\n' "${patch_names[index]}"
    else
        printf 'Patch no longer applies cleanly: %s\n' "${patch_names[index]}" >&2
        exit 1
    fi
done

for target in "${pending_targets[@]}"; do
    case "$target" in
        client) target_dir="$client_dir" ;;
        protocol) target_dir="$client_dir/Protocol" ;;
    esac
    marker="$(git -C "$target_dir" rev-parse --absolute-git-dir)/lancer-nexus-patches-state"
    target_fingerprint "$target" "$target_dir" > "$marker"
done
