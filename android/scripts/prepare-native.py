"""Fetch pinned upstream binaries; verify all extracted native-library hashes."""
import hashlib
import json
import pathlib
import urllib.request
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
SOURCES = {
    "afreerdp-3.32.1-arm64.apk": (
        "https://f-droid.org/repo/com.freerdp.afreerdp_3320103.apk",
        "520403bf41dec2e4599ddd22ab2bd7597f64867ec422c5e2fa46da259cac668e",
    ),
    "EasyTierCompose-1.0.9-arm64.apk": (
        "https://github.com/ECSDevs/EasyTierCompose/releases/download/v1.0.9/EasyTier-1.0.9-arm64-v8a-d627c1d.apk",
        "7d38361c30b85ede60e309b67f5cc4969a4ff4512fddc0d138eb2d49276b80f2",
    ),
}
manifest = json.loads((ROOT / "third_party/native-manifest.json").read_text())
destination = ROOT / "app/src/main/jniLibs/arm64-v8a"
destination.mkdir(parents=True, exist_ok=True)
cache = ROOT / "build/native-cache"


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


for source, (url, expected) in SOURCES.items():
    entries = [row for row in manifest if row["source"] == source]
    if all((destination / row["name"]).exists() and digest(destination / row["name"]) == row["sha256"] for row in entries):
        print(f"Verified existing native libraries from {source}")
        continue
    cache.mkdir(parents=True, exist_ok=True)
    archive = cache / source
    if not archive.exists() or digest(archive) != expected:
        temporary = archive.with_suffix(".part")
        print(f"Downloading {source}", flush=True)
        with urllib.request.urlopen(url, timeout=90) as response, temporary.open("wb") as target:
            while chunk := response.read(1024 * 1024):
                target.write(chunk)
        if digest(temporary) != expected:
            raise RuntimeError(f"Source hash mismatch: {source}")
        temporary.replace(archive)
    with zipfile.ZipFile(archive) as package:
        for row in entries:
            name = row["name"]
            if pathlib.PurePosixPath(name).name != name:
                raise RuntimeError("Unsafe library name")
            data = package.read("lib/arm64-v8a/" + name)
            if hashlib.sha256(data).hexdigest() != row["sha256"]:
                raise RuntimeError(f"Native library hash mismatch: {name}")
            (destination / name).write_bytes(data)
    print(f"Prepared {len(entries)} libraries from {source}")

