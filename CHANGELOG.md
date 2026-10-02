# Changelog

## 1.1.1

- Skip NDMF Optimizing In Play Mode: no longer warns on every play-mode entry when NDMF is not installed or did not build the avatar
- README: install from the unitypackage; list which tools help with NDMF, VRCFury, or both

## 1.1.0

- Skip Optimizers In Play Mode: strips AAO Trace and Optimize, Avatar Compressor and fully hidden AAO Remove Mesh By BlendShape from play-mode builds, so avatars using them also get the NDMF optimizing-phase skip
- Log Play Mode Build Time: logs the play-mode entry time with a per-stage breakdown (off by default)
- Menus moved under Tools > Kw4r3n > VRC Play Mode Build Speedups

## 1.0.0

- Skip NDMF Optimizing In Play Mode
- Clear Root Animator Controller On Build
