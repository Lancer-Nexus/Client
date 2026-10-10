#!/usr/bin/env python3
"""Compare the applied overlay with a clean reconstruction of the maintained series."""

from pathlib import Path, PurePosixPath
import shlex
import subprocess
import sys
import tempfile


def run(*args, cwd=None):
    return subprocess.run(args, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)


def patch_paths(patch):
    paths = set()
    for line in patch.read_text().splitlines():
        if not line.startswith(("--- ", "+++ ")):
            continue
        path = shlex.split(line)[1]
        if path == "/dev/null":
            continue
        if not path.startswith(("a/", "b/")):
            raise ValueError(f"Unsupported patch path in {patch.name}")
        path = PurePosixPath(path[2:])
        if path.is_absolute() or ".." in path.parts or not path.parts:
            raise ValueError(f"Unsafe patch path in {patch.name}")
        paths.add(path.as_posix())
    return paths


def verify(root):
    def base_ref(target):
        configured = root / "patches" / f"base-{target}-commit"
        reference = configured.read_text().strip() if configured.is_file() else "HEAD"
        repo = root if target == "client" else root / "Protocol"
        resolved = run("git", "-C", str(repo), "rev-parse", "--verify", "--quiet", f"{reference}^{{commit}}")
        if resolved.returncode:
            raise ValueError(f"Patch baseline for {target} is unavailable: {reference}")
        return reference

    targets = {}
    for line in (root / "patches/series").read_text().splitlines():
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        target, filename = line.split()
        if target not in ("client", "protocol"):
            raise ValueError(f"Unknown patch target: {target}")
        patch = root / "patches" / filename
        targets.setdefault(target, []).append((patch, patch_paths(patch)))

    mismatches = []
    checked = 0
    for target, patches in targets.items():
        repo = root if target == "client" else root / "Protocol"
        reference = base_ref(target)
        paths = set().union(*(paths for _, paths in patches))
        with tempfile.TemporaryDirectory(prefix="nexus-overlay-check-") as temporary:
            baseline = Path(temporary)
            for path in sorted(paths):
                original = run("git", "-C", str(repo), "show", f"{reference}:{path}")
                if original.returncode:
                    absent = run("git", "-C", str(repo), "ls-tree", reference, "--", path)
                    if absent.returncode or absent.stdout:
                        raise ValueError(f"Cannot read baseline {reference}: {target}/{path}")
                    continue
                destination = baseline / path
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(original.stdout)
            for patch, _ in patches:
                applied = run("git", "apply", "--ignore-space-change", str(patch), cwd=baseline)
                if applied.returncode:
                    raise ValueError(f"Cannot reconstruct {target} patch {patch.name}:\n"
                                     + applied.stderr.decode(errors="replace"))
            for path in sorted(paths):
                expected = baseline / path
                actual = repo / path
                checked += 1
                if expected.exists() != actual.exists():
                    mismatches.append(f"{target}/{path}")
                elif expected.exists():
                    # Existing Windows line endings are harmless; all other bytes matter.
                    if expected.read_bytes().replace(b"\r\n", b"\n") != actual.read_bytes().replace(b"\r\n", b"\n"):
                        mismatches.append(f"{target}/{path}")
    if mismatches:
        print("Applied overlay differs from the maintained series:", file=sys.stderr)
        print("\n".join(mismatches), file=sys.stderr)
        return 1
    print(f"Verified {checked} overlay files against a clean patch reconstruction.")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(verify(Path(__file__).resolve().parents[1]))
    except (ValueError, OSError) as exception:
        print(f"Overlay verification failed: {exception}", file=sys.stderr)
        sys.exit(1)
