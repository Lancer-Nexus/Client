#!/usr/bin/env bash
set -Eeuo pipefail

client_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
series="$client_dir/patches/series"
[[ -f "$series" ]] || exit 0

declare -A applied_hashes=()
declare -A target_indices=()

marker_for() {
    git -C "$1" rev-parse --git-path lancer-nexus-patches.applied
}

write_marker() {
    local marker="$1" target="$2" patch_name="$3" patch_hash="$4"
    local marker_tmp="${marker}.tmp"
    if [[ -f "$marker" ]]; then
        cat -- "$marker" > "$marker_tmp"
    else
        : > "$marker_tmp"
    fi
    printf '%s %s %s\n' "$target" "$patch_name" "$patch_hash" >> "$marker_tmp"
    mv -- "$marker_tmp" "$marker"
}

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

    patch_hash="$(sha256sum -- "$patch_file" | cut -d ' ' -f 1)"
    marker="$(marker_for "$target_dir")"
    index="${target_indices[$target]:-0}"
    if [[ -f "$marker" ]]; then
        mapfile -t marker_lines < "$marker"
        target_marker_lines=()
        for marker_line in "${marker_lines[@]}"; do
            read -r old_target old_name old_hash <<< "$marker_line"
            [[ "$old_target" == "$target" ]] && target_marker_lines+=("$old_name $old_hash")
        done
        if (( index < ${#target_marker_lines[@]} )); then
            expected="${target_marker_lines[$index]}"
            if [[ "$expected" != "$patch_name $patch_hash" ]]; then
                printf 'Recorded patch state does not match series at %s\n' "$patch_name" >&2
                exit 1
            fi
            printf 'Already applied %s (recorded)\n' "$patch_name"
            target_indices[$target]=$((index + 1))
            continue
        elif (( index != ${#target_marker_lines[@]} )); then
            printf 'Patch series changed after an earlier patch was applied at %s\n' "$patch_name" >&2
            exit 1
        fi
    fi

    if git -C "$target_dir" apply --check "$patch_file" 2>/dev/null; then
        git -C "$target_dir" apply "$patch_file"
        printf 'Applied %s\n' "$patch_name"
    elif git -C "$target_dir" apply --reverse --check "$patch_file"; then
        printf 'Already applied %s\n' "$patch_name"
    else
        printf 'Patch no longer applies cleanly: %s\n' "$patch_name" >&2
        exit 1
    fi
    write_marker "$marker" "$target" "$patch_name" "$patch_hash"
    target_indices[$target]=$((index + 1))
done < "$series"
