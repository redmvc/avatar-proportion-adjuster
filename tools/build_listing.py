"""Builds the VCC listing and package zips from this repository's git history.

    python tools/build_listing.py [--out _site]

A version exists from the first commit on the current branch whose package.json carries it, and
its zip holds the package as of that commit. So bumping the version and pushing is the whole
release, published versions never change, and a change without a bump isn't published.
Needs the full history (fetch-depth: 0 in Actions).
"""
import argparse
import datetime
import hashlib
import io
import json
import subprocess
import tarfile
import zipfile
from pathlib import Path

PACKAGE_DIR = "Packages/red.proportion-adjuster"
BASE_URL = "https://redmvc.github.io/avatar-proportion-adjuster"
LISTING = {
    "name": "Proportion Adjuster",
    "id": "io.github.redmvc.avatar-proportion-adjuster",
    "url": f"{BASE_URL}/index.json",
    "author": "Red",
}


def git(*args):
    return subprocess.run(["git", *args], check=True, capture_output=True).stdout


def manifest_at(commit):
    result = subprocess.run(["git", "show", f"{commit}:{PACKAGE_DIR}/package.json"], capture_output=True)
    return json.loads(result.stdout) if result.returncode == 0 else None


# The same commit always gives the same bytes: sorted entries and the commit's own timestamp.
def build_zip(commit):
    seconds = int(git("show", "-s", "--format=%ct", commit))
    stamp = datetime.datetime.fromtimestamp(seconds, datetime.timezone.utc).timetuple()[:6]
    archive = tarfile.open(fileobj=io.BytesIO(git("archive", "--format=tar", commit, "--", PACKAGE_DIR)))
    members = sorted((m for m in archive.getmembers() if m.isfile()), key=lambda m: m.name)
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w", zipfile.ZIP_DEFLATED) as package:
        for member in members:
            info = zipfile.ZipInfo(member.name[len(PACKAGE_DIR) + 1:], stamp)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.create_system = 3
            info.external_attr = 0o644 << 16
            package.writestr(info, archive.extractfile(member).read())
    return buffer.getvalue()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", type=Path, default=Path("_site"))
    out = parser.parse_args().out
    out.mkdir(parents=True, exist_ok=True)

    versions, name, last = {}, None, None
    for commit in git("log", "--first-parent", "--reverse", "--format=%H").decode().split():
        manifest = manifest_at(commit)
        if not manifest:
            continue
        name = manifest["name"]
        if manifest["version"] in versions:
            continue
        data = build_zip(commit)
        file_name = f"{name}-{manifest['version']}.zip"
        (out / file_name).write_bytes(data)
        versions[manifest["version"]] = {
            **manifest,
            "url": f"{BASE_URL}/{file_name}",
            "zipSHA256": hashlib.sha256(data).hexdigest(),
        }
        last = commit
        print(f"{manifest['version']}: {commit[:8]} {file_name}")

    listing = {**LISTING, "packages": {name: {"versions": versions}}}
    (out / "index.json").write_text(json.dumps(listing, indent=2) + "\n", encoding="utf-8")
    print(f"{out / 'index.json'}: {len(versions)} versions")

    if git("rev-parse", f"HEAD:{PACKAGE_DIR}") != git("rev-parse", f"{last}:{PACKAGE_DIR}"):
        print(f"::warning::{PACKAGE_DIR} changed since {list(versions)[-1]} without a version bump, so those changes aren't published.")


if __name__ == "__main__":
    main()
