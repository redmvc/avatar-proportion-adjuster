# Proportion Adjuster

Reshapes a VRChat avatar to your own measurements when it's built, so full-body tracking fits your
body. How to use it is in the [package's README](Packages/red.proportion-adjuster/README.md).

## Install with VCC

In the VRChat Creator Companion, go to **Settings > Packages > Add Repository** and paste:

```
https://redmvc.github.io/avatar-proportion-adjuster/index.json
```

Then add **Proportion Adjuster** to your project under **Manage Project**.

It needs NDMF. If VCC can't find it, add `https://vpm.nadena.dev/vpm.json` as a repository too.

A copy imported from a `.unitypackage` into `Assets/Proportion Adjuster` is removed when you install
1.8.1 or later. Your avatars keep their values. Versions before 1.8.1 don't remove it, and installing
one alongside that copy gives duplicate script errors.

## Releasing

1. Change the package in `Packages/red.proportion-adjuster`.
2. Bump `version` in its `package.json`.
3. Commit and push to `main`.

An Action rebuilds the listing and zips from the git history and publishes them to GitHub Pages. A
version is the package as of the first commit that has that version number, so published versions
never change, and changes without a bump aren't published. Each version's commit is tagged `vX.Y.Z`.

To see what would be published, run `python tools/build_listing.py`, which writes to `_site`.
