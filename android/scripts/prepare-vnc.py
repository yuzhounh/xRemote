"""Prepare the pinned, unmodified AVNC JNI library and its shipped license notices."""
from pathlib import Path
import urllib.request
import zipfile

root = Path(__file__).resolve().parents[1]
library = root / "app/src/main/jniLibs/arm64-v8a/libnative-vnc.so"
notices = root / "third_party"
licenses = ("GPL-3.0.txt", "BSD-libjpeg-turbo.txt", "X11.txt")
if library.exists() and all((notices / ("AVNC-" + name)).exists() for name in licenses):
    print("Using prepared AVNC 3.3.1 JNI library")
else:
    archive = root / "build/native-cache/AVNC-3.3.1.apk"
    archive.parent.mkdir(parents=True, exist_ok=True)
    if not archive.exists():
        urllib.request.urlretrieve("https://github.com/gujjwal00/avnc/releases/download/v3.3.1/AVNC-3.3.1.apk", archive)
    library.parent.mkdir(parents=True, exist_ok=True)
    notices.mkdir(exist_ok=True)
    with zipfile.ZipFile(archive) as package:
        library.write_bytes(package.read("lib/arm64-v8a/libnative-vnc.so"))
        for name in licenses:
            (notices / ("AVNC-" + name)).write_bytes(package.read("assets/license/" + name))
    print("Prepared AVNC 3.3.1 JNI library")
