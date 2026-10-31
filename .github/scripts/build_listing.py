"""Build a VPM listing (index.json) from this repository's GitHub releases.

Each published release must carry the package zip and package.json as assets
(the Release workflow uploads both). Usage: build_listing.py <output dir>
"""
import hashlib
import json
import os
import sys
import urllib.request

LISTING_ID = "com.sou2000sw.vpm"
LISTING_NAME = "sou2000sw"
AUTHOR = "sou2000sw"

API = os.environ.get("GITHUB_API_URL", "https://api.github.com")


def fetch(url, accept="application/vnd.github+json"):
    headers = {"Accept": accept, "User-Agent": "build-listing"}
    token = os.environ.get("GH_TOKEN")
    if token:
        headers["Authorization"] = f"Bearer {token}"
    with urllib.request.urlopen(urllib.request.Request(url, headers=headers)) as res:
        return res.read()


def releases(repo):
    page = 1
    while True:
        batch = json.loads(fetch(f"{API}/repos/{repo}/releases?per_page=100&page={page}"))
        if not batch:
            return
        yield from batch
        page += 1


def asset_bytes(asset):
    # The API asset URL with octet-stream works for private repos too; it redirects to the file
    return fetch(asset["url"], accept="application/octet-stream")


def build(repo):
    owner, name = repo.split("/")
    packages = {}
    for release in releases(repo):
        if release["draft"] or release["prerelease"]:
            continue
        assets = {a["name"]: a for a in release["assets"]}
        zips = [a for n, a in assets.items() if n.endswith(".zip")]
        if "package.json" not in assets or len(zips) != 1:
            print(f"skip {release['tag_name']}: needs package.json and exactly one zip", file=sys.stderr)
            continue
        manifest = json.loads(asset_bytes(assets["package.json"]))
        zip_asset = zips[0]
        manifest["url"] = zip_asset["browser_download_url"]
        manifest["zipSHA256"] = hashlib.sha256(asset_bytes(zip_asset)).hexdigest()
        versions = packages.setdefault(manifest["name"], {"versions": {}})["versions"]
        versions[manifest["version"]] = manifest
    return {
        "name": LISTING_NAME,
        "id": LISTING_ID,
        "url": f"https://{owner.lower()}.github.io/{name}/index.json",
        "author": AUTHOR,
        "packages": packages,
    }


def main():
    out_dir = sys.argv[1]
    listing = build(os.environ["GITHUB_REPOSITORY"])
    if not listing["packages"]:
        sys.exit("no releases with package.json and zip found")
    os.makedirs(out_dir, exist_ok=True)
    with open(os.path.join(out_dir, "index.json"), "w", encoding="utf-8") as f:
        json.dump(listing, f, ensure_ascii=False, indent=2)
    print(json.dumps({n: list(p["versions"]) for n, p in listing["packages"].items()}))


if __name__ == "__main__":
    main()
