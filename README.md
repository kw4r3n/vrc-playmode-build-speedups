# VRC Play Mode Build Speedups

[日本語](#日本語) | [English](#english)

## 日本語

NDMFとVRCFuryを使ったVRChatアバターで、プレイモード突入時のビルドを短くするエディタ拡張です。どちらもビルド用に複製されたアバターだけを変更し、元のPrefabやシーンには触れません。

| ツール | 内容 | 効果の例 |
| --- | --- | --- |
| Skip NDMF Optimizing In Play Mode | プレイモードでNDMFの最適化フェーズを省く | 7.4秒→6.2秒 |
| Clear Root Animator Controller On Build | ビルド時にルートAnimatorのコントローラーを外す | 32.5秒→29.8秒 |

効果は、VRCFuryとGoGoLocoを使ったアバター1体での実測です。

### 導入

このリポジトリを、Unityプロジェクトの `Packages/io.github.kw4r3n.vrc-playmode-build-speedups` に置いてください(VPMリスティングは未公開)。

```bash
git clone https://github.com/kw4r3n/vrc-playmode-build-speedups Packages/io.github.kw4r3n.vrc-playmode-build-speedups
```

どちらのツールも、導入した時点で有効になります。`Tools` メニューの各項目で個別にオフにできます。

### Skip NDMF Optimizing In Play Mode

VRCFuryは各レイヤーのアニメーターコントローラーを作り直すため、NDMFは最適化フェーズの前にそれらを全部複製し直します。最適化系のツールを使っていないアバターでは、プレイモード中にこのフェーズがすることは未使用オブジェクトの削除(Modular AvatarのGC)だけです。そこで、最適化フェーズの直前でNDMFのビルドを終えます。

次の場合は、従来どおり最適化フェーズを実行します。

- アップロード時(プレイモード以外)
- 次のツールのコンポーネントがアバターにある: Avatar Optimizer(AAO)、TexTransTool、lilxyzw系ツール、Avatar Compressor、VRCQuestTools
- NDMF内部の構造が想定と違う場合(警告を出して通常どおり実行)

違いは、普段なら削除される未使用オブジェクトが残ることだけです。アニメーター、パラメータ、メニューは変わりません。

NDMFの内部APIをリフレクションで呼んでいます。NDMF 1.14.8で確認しました。

### Clear Root Animator Controller On Build

VRChatとGesture ManagerはルートのAnimatorのコントローラーを使いません。それでも設定されていると、VRCFuryがそこに生成したFXを入れ、NDMFがそれを別のコントローラーとしてもう一度複製します。ビルド用の複製でだけコントローラーを外し、この複製を省きます。アップロード時も有効です。

### あわせて読むもの

- [Linux版Unityの CreateInstance/Instantiate が遅い問題の回避](https://github.com/kw4r3n/unity-linux-stackcache) — Linuxではこちらの効果が最も大きい(約15秒→約6.3秒)
- [ThiccWaterのビルドを速くする修正手順](Documentation~/ThiccWater.md)

### 確認した環境

Unity 2022.3.22f1、VRChat SDK 3.10.5、NDMF 1.14.8、Modular Avatar 1.18.7、VRCFury 1.1430.0

## English

Editor tools that shorten the build when entering play mode for VRChat avatars that use NDMF and VRCFury. Both change only the avatar copy made for the build, never the original prefab or scene.

| Tool | What it does | Example effect |
| --- | --- | --- |
| Skip NDMF Optimizing In Play Mode | Skips NDMF's optimizing phase in play mode | 7.4 s → 6.2 s |
| Clear Root Animator Controller On Build | Removes the root Animator's controller during the build | 32.5 s → 29.8 s |

Effects were measured on one avatar that uses VRCFury and GoGoLoco.

### Install

Put this repository in your Unity project at `Packages/io.github.kw4r3n.vrc-playmode-build-speedups` (there is no VPM listing yet).

```bash
git clone https://github.com/kw4r3n/vrc-playmode-build-speedups Packages/io.github.kw4r3n.vrc-playmode-build-speedups
```

Both tools are on once installed. Turn either off from its item in the `Tools` menu.

### Skip NDMF Optimizing In Play Mode

VRCFury rebuilds every playable-layer controller, so NDMF clones all of them again before its optimizing phase. For an avatar without optimizer tools, the only thing that phase does in play mode is remove unused objects (Modular Avatar's GC). So this ends the NDMF build just before the optimizing phase.

The optimizing phase still runs:

- on upload (outside play mode)
- when the avatar has components from Avatar Optimizer (AAO), TexTransTool, lilxyzw tools, Avatar Compressor or VRCQuestTools
- when NDMF's internals don't look as expected (it logs a warning and runs normally)

The only difference is that unused objects which would normally be removed stay. Animators, parameters and menus are unchanged.

It calls NDMF internals through reflection. Tested with NDMF 1.14.8.

### Clear Root Animator Controller On Build

VRChat and Gesture Manager ignore the root Animator's controller. When one is set anyway, VRCFury puts its generated FX there, and NDMF clones it again as a separate controller. Removing the controller from the build copy skips that clone. It also applies to uploads.

### See also

- [Fix for slow CreateInstance/Instantiate in the Linux Unity Editor](https://github.com/kw4r3n/unity-linux-stackcache) — on Linux this has by far the largest effect (about 15 s → 6.3 s)
- [How to speed up the ThiccWater build](Documentation~/ThiccWater.md)

### Tested with

Unity 2022.3.22f1, VRChat SDK 3.10.5, NDMF 1.14.8, Modular Avatar 1.18.7, VRCFury 1.1430.0
