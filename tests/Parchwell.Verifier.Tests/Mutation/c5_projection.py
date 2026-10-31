"""Suppress control branches outside a shard without changing its mutation population."""

from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import tarfile


POLICY = "excluded-source-pragmas-v1"
PREFIX = b"/* Stryker disable all : This source is outside the assigned C5 shard. */ "
VERIFIER = "src/Parchwell.Verifier/"
MAX_FILES = 4096
MAX_BYTES = 268435456
MAX_VERIFIER_FILES = 128
FIELDS = {"policy", "target", "original_sha256", "before_sha256", "after_sha256", "disabled"}


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def file_bytes(path: Path) -> bytes:
    return os.fsencode(os.readlink(path)) if path.is_symlink() else path.read_bytes()


def prefixed(data: bytes) -> bytes:
    return b"\xef\xbb\xbf" + PREFIX + data[3:] if data.startswith(b"\xef\xbb\xbf") else PREFIX + data


def original(data: bytes) -> bytes:
    offset = 3 if data.startswith(b"\xef\xbb\xbf") else 0
    if data[offset:offset + len(PREFIX)] != PREFIX:
        raise ValueError("missing projection pragma")
    return data[:offset] + data[offset + len(PREFIX):]


def source_names(names) -> list[str]:
    return sorted(name for name in names if name.startswith(VERIFIER) and name.endswith(".cs"))


def capture(root: Path, names: list[str]) -> dict[str, str]:
    # New source outside generated build directories must not enter the copied project.
    expected = {name for name in names if name.endswith(".cs") and name.split("/")[0] in ("src", "tests")}
    actual = {path.relative_to(root).as_posix() for folder in ("src", "tests")
              for path in (root / folder).rglob("*.cs")
              if not {"bin", "obj"}.intersection(path.relative_to(root).parts)}
    if actual != expected:
        raise ValueError("projection compile-source inventory changed")
    return {name: digest(file_bytes(root / name)) for name in names}


def prepare(repository: Path, root: Path, target: str, retained: Path) -> dict:
    names = subprocess.check_output(["git", "-C", str(repository), "ls-files", "-z"]).decode().split("\0")
    names = sorted(name for name in names if name)
    if len(names) > MAX_FILES or sum((repository / name).lstat().st_size for name in names) > MAX_BYTES:
        raise ValueError("projection export bound exceeded")
    sources = source_names(names)
    assigned = VERIFIER + target
    if not sources or len(sources) > MAX_VERIFIER_FILES or assigned not in sources:
        raise ValueError("projection target/source inventory invalid")
    if any((repository / name).is_symlink() for name in sources):
        raise ValueError("verifier source symlink is unsupported")
    archive = root.parent / "source.tar"
    with archive.open("xb") as stream:
        subprocess.run(["git", "-C", str(repository), "archive", "HEAD"], stdout=stream, check=True, timeout=60)
    with tarfile.open(archive) as stream:
        stream.extractall(root, filter="data")
    archive.unlink()
    hashes = capture(root, names)
    if hashes != {name: digest(file_bytes(repository / name)) for name in names}:
        raise ValueError("projection differs from measured checkout")
    disabled = [name for name in sources if name != assigned]
    for name in disabled:
        path = root / name
        data = path.read_bytes()
        if re.search(rb"Stryker\s+(disable|restore)", data, flags=re.IGNORECASE):
            raise ValueError("existing directive in excluded source")
        path.write_bytes(prefixed(data))
    retained.mkdir()
    for name in sources:
        destination = retained / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(root / name, destination)
    return {"policy": POLICY, "target": target, "original_sha256": hashes,
            "before_sha256": capture(root, names), "after_sha256": None, "disabled": disabled}


def finish(root: Path, manifest: dict, output: Path) -> None:
    try:
        manifest["after_sha256"] = capture(root, list(manifest["original_sha256"]))
    except (OSError, ValueError):
        manifest["after_sha256"] = None
    with output.open("x") as stream:
        json.dump(manifest, stream, indent=2)
        stream.write("\n")


def validate(manifest: dict, retained: Path, target: str, sources: dict) -> str:
    if not isinstance(manifest, dict) or set(manifest) != FIELDS or manifest["policy"] != POLICY or manifest["target"] != target:
        raise ValueError("invalid projection policy/target")
    hashes = manifest["original_sha256"]
    if not isinstance(hashes, dict) or not hashes or len(hashes) > MAX_FILES:
        raise ValueError("invalid projection inventory")
    for name, value in hashes.items():
        if not isinstance(name, str) or not name or "\\" in name or PurePosixPath(name).is_absolute() or ".." in PurePosixPath(name).parts or not isinstance(value, str) or re.fullmatch("[0-9a-f]{64}", value) is None:
            raise ValueError("invalid projection source/hash")
    files = source_names(hashes)
    assigned = VERIFIER + target
    disabled = [name for name in files if name != assigned]
    before = manifest["before_sha256"]
    if not files or len(files) > MAX_VERIFIER_FILES or assigned not in files or manifest["disabled"] != disabled or not isinstance(before, dict) or set(before) != set(hashes) or before != manifest["after_sha256"]:
        raise ValueError("projection scope/drift")
    if any(hashes.get(name) != value for name, value in sources.items()):
        raise ValueError("projection original-source mismatch")
    for name, value in hashes.items():
        if name in files:
            data = (retained / name).read_bytes()
            restored = original(data) if name in disabled else data
            if digest(restored) != value or digest(data) != before[name]:
                raise ValueError("projection source transformation mismatch")
            if name in disabled and re.search(rb"Stryker\s+(disable|restore)", restored, flags=re.IGNORECASE):
                raise ValueError("existing directive in retained excluded source")
        elif before[name] != value:
            raise ValueError("projection changed a test or other input")
    return hashes[assigned]
