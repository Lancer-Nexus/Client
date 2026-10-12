import contextlib
import importlib.util
import io
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


spec = importlib.util.spec_from_file_location(
    "verify_overlay", Path(__file__).resolve().parents[1] / "verify-lancer-nexus-overlay.py"
)
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)


class OverlayVerificationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        subprocess.run(["git", "init", "-q", str(self.root)], check=True)
        (self.root / "engine.cs").write_text("old\n")
        subprocess.run(["git", "-C", str(self.root), "add", "engine.cs"], check=True)
        subprocess.run(
            ["git", "-C", str(self.root), "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid",
             "commit", "-qm", "baseline"], check=True
        )
        patches = self.root / "patches"
        patches.mkdir()
        (patches / "change.patch").write_text(
            "--- a/engine.cs\n+++ b/engine.cs\n@@ -1 +1 @@\n-old\n+new\n"
        )
        (patches / "series").write_text("client change.patch\n")

    def tearDown(self):
        self.temporary.cleanup()

    def verify(self):
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            return verifier.verify(self.root)

    def test_applied_series_matches(self):
        (self.root / "engine.cs").write_text("new\n")
        self.assertEqual(0, self.verify())

    def test_reverted_overlay_is_rejected(self):
        self.assertEqual(1, self.verify())

    def test_windows_line_endings_are_accepted(self):
        (self.root / "engine.cs").write_bytes(b"new\r\n")
        self.assertEqual(0, self.verify())

    def test_pinned_baseline_is_used_after_head_overlay_changes(self):
        baseline = subprocess.run(
            ["git", "-C", str(self.root), "rev-parse", "HEAD"], check=True,
            text=True, stdout=subprocess.PIPE
        ).stdout.strip()
        (self.root / "patches/base-client-commit").write_text(baseline + "\n")
        (self.root / "engine.cs").write_text("new\n")
        self.assertEqual(0, self.verify())

    def test_later_patch_can_change_earlier_patch_output(self):
        (self.root / "patches/second.patch").write_text(
            "--- a/engine.cs\n+++ b/engine.cs\n@@ -1 +1 @@\n-new\n+final\n"
        )
        (self.root / "patches/series").write_text("client change.patch\nclient second.patch\n")
        (self.root / "engine.cs").write_text("final\n")
        self.assertEqual(0, self.verify())

    def test_unreconstructable_patch_is_rejected(self):
        (self.root / "patches/change.patch").write_text(
            "--- a/engine.cs\n+++ b/engine.cs\n@@ -1 +1 @@\n-missing\n+new\n"
        )
        with self.assertRaises(ValueError):
            self.verify()

    def copy_applier(self):
        scripts = self.root / "scripts"
        scripts.mkdir()
        original = Path(__file__).resolve().parents[1]
        for name in ("apply-lancer-nexus-patches.sh", "verify-lancer-nexus-overlay.py"):
            shutil.copyfile(original / name, scripts / name)
        return scripts / "apply-lancer-nexus-patches.sh"

    def test_record_current_preserves_marker_when_overlay_is_incomplete(self):
        applier = self.copy_applier()
        marker = self.root / ".git/lancer-nexus-patches-state"
        marker.write_text("previous-verified-state\n")
        result = subprocess.run(["bash", str(applier), "--record-current"], capture_output=True)
        self.assertNotEqual(0, result.returncode)
        self.assertEqual("previous-verified-state\n", marker.read_text())

    def test_record_current_accepts_complete_overlay(self):
        applier = self.copy_applier()
        (self.root / "engine.cs").write_text("new\n")
        result = subprocess.run(["bash", str(applier), "--record-current"], capture_output=True)
        self.assertEqual(0, result.returncode, result.stderr.decode())
        self.assertTrue((self.root / ".git/lancer-nexus-patches-state").is_file())

    def test_normal_applier_records_a_complete_existing_overlay(self):
        applier = self.copy_applier()
        (self.root / "engine.cs").write_text("new\n")
        result = subprocess.run(["bash", str(applier)], capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("Recorded verified complete overlay state", result.stdout)
        self.assertTrue((self.root / ".git/lancer-nexus-patches-state").is_file())

    def test_normal_applier_still_applies_patches_to_the_baseline(self):
        applier = self.copy_applier()
        result = subprocess.run(["bash", str(applier)], capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("new\n", (self.root / "engine.cs").read_text())
        self.assertIn("Applied change.patch", result.stdout)

    def test_native_target_is_applied_and_drift_is_rejected(self):
        native = self.root / "extern/blurgtext"
        native.mkdir(parents=True)
        subprocess.run(["git", "init", "-q", str(native)], check=True)
        (native / "renderer.c").write_text("old\n")
        subprocess.run(["git", "-C", str(native), "add", "renderer.c"], check=True)
        subprocess.run(
            ["git", "-C", str(native), "-c", "user.name=Fixture", "-c",
             "user.email=fixture@example.invalid", "commit", "-qm", "native baseline"],
            check=True,
        )
        baseline = subprocess.check_output(
            ["git", "-C", str(native), "rev-parse", "HEAD"], text=True
        ).strip()
        (self.root / "patches/base-blurgtext-commit").write_text(baseline + "\n")
        (self.root / "patches/native.patch").write_text(
            "--- a/renderer.c\n+++ b/renderer.c\n@@ -1 +1 @@\n-old\n+new\n"
        )
        (self.root / "patches/series").write_text(
            "client change.patch\nblurgtext native.patch\n"
        )
        applier = self.copy_applier()
        result = subprocess.run(["bash", str(applier)], capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("new\n", (native / "renderer.c").read_text())
        self.assertEqual(0, self.verify())
        (native / "renderer.c").write_text("unexpected drift\n")
        self.assertEqual(1, self.verify())


if __name__ == "__main__":
    unittest.main()
