# VRC Play Mode Build Speedups

[日本語](#日本語) | [English](#english)

## 日本語

NDMF・VRCFury・AAOを使ったVRChatアバターで、プレイモード突入時のビルドを短くするエディタ拡張です。どれもビルド用に複製されたアバターだけを変更し、元のPrefabやシーンには触れません。

| ツール | 内容 | 効果の例 |
| --- | --- | --- |
| Skip Optimizers In Play Mode | プレイモードではAAOのTrace and OptimizeとAvatar Compressorを外す | 14.6秒→6.8秒 |
| Skip NDMF Optimizing In Play Mode | プレイモードでNDMFの最適化フェーズを省く | 7.4秒→6.2秒 |
| Clear Root Animator Controller On Build | ビルド時にルートAnimatorのコントローラーを外す | 32.5秒→29.8秒 |
| Log Play Mode Build Time(おまけ) | プレイモード突入時間と、NDMF・VRCFuryなどの内訳をコンソールに出す | — |

効果は、VRCFury・GoGoLoco・AAOを使ったアバター1体での実測です。

### 効果が出る条件

NDMFは、Modular AvatarやAAOなどが使っている基盤です。アバターにNDMFとVRCFuryのどちらが入っているかで、効果が変わります。

| ツール | NDMFだけ | VRCFuryだけ | 両方 |
| --- | --- | --- | --- |
| Skip Optimizers In Play Mode | ○ ※1 | — | ○ ※1 |
| Skip NDMF Optimizing In Play Mode | △ ※2 | — | ○ |
| Clear Root Animator Controller On Build | ○ ※3 | — | ○(効果大) |
| Log Play Mode Build Time | ○ | ○ | ○ |

- ※1 AAOのTrace and OptimizeかAvatar Compressorを使っている場合だけ効きます。
- ※2 主に省けるのは、VRCFuryが作り直したコントローラーの複製です。NDMFだけのアバターでは、効果は小さいと見込んでいます(未計測)。
- ※3 ルートのAnimatorにコントローラーが設定されている場合だけ効きます。VRCFuryがあると、そこにVRCFuryの生成したFXが入るので、効果が大きくなります。

VRCFuryだけのアバターでは、計測ツール以外は何もしません。

### 導入

[Releases](https://github.com/kw4r3n/vrc-playmode-build-speedups/releases/latest) から `.unitypackage` をダウンロードし、Unityで開いたプロジェクトにインポートしてください。`Assets/Kw4r3n/VRCPlayModeBuildSpeedups/` に入ります。外すときは、このフォルダを削除してください。

計測ツール以外は、導入した時点で有効になります。`Tools > Kw4r3n > VRC Play Mode Build Speedups` の各項目で個別にオン・オフできます。計測ツールは初期状態でオフです。

### Skip Optimizers In Play Mode

プレイモードでの動作確認に、最終品質の最適化は不要です。プレイモードに入るときだけ、ビルド用の複製から次のコンポーネントを外します。アップロード時は外しません。

- AAO の Trace and Optimize
- Avatar Compressor の Texture Compressor
- AAO の Remove Mesh By BlendShape のうち、対象のシェイプキーがすべて100になっているもの(見た目が変わらないもの)

これらが外れると、次の Skip NDMF Optimizing In Play Mode も働くようになります。AAOの他のコンポーネント(Merge Skinned Meshなど)が残っている場合は、最適化フェーズを通常どおり実行します。

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

### Log Play Mode Build Time(おまけ)

オンにすると、プレイモードに入るたびに次のような1行をコンソールに出します。設定やツールの効果を測るときに使ってください。

```
[PlayModeBuildTimer] Entered play mode in 7.4s (avatar build 6.0s: NDMF 0.9s, VRCFury 3.5s, other 1.5s, NDMF optimizing 0.0s, after 0.1s)
```

- NDMF: NDMFの前半(Modular Avatarの統合など)
- VRCFury: VRCFuryのビルド
- other: VRCFuryからNDMF最適化フェーズまでの間に動くツール
- NDMF optimizing: NDMFの最適化フェーズ(AAOなど)

計測を正確にするには、比較する設定を交互に切り替えて(A→B→B→A)複数回測ってください。

### あわせて読むもの

- [Linux版Unityの CreateInstance/Instantiate が遅い問題の回避](https://github.com/kw4r3n/unity-linux-stackcache) — Linuxではこちらの効果が最も大きい(約15秒→約6.3秒)
- [ThiccWaterのビルドを速くする修正手順](Documentation~/ThiccWater.md)

### 確認した環境

Unity 2022.3.22f1、VRChat SDK 3.10.5、NDMF 1.14.8、Modular Avatar 1.18.7、VRCFury 1.1430.0

### AIの利用

原因の調査、コード、このREADMEの作成には AI(Anthropic の Claude Code)を使っています。計測と動作確認は「確認した環境」に書いた環境で、実際に行っています。

## English

Editor tools that shorten the build when entering play mode for VRChat avatars that use NDMF, VRCFury and AAO. All of them change only the avatar copy made for the build, never the original prefab or scene.

| Tool | What it does | Example effect |
| --- | --- | --- |
| Skip Optimizers In Play Mode | Removes AAO Trace and Optimize and Avatar Compressor in play mode | 14.6 s → 6.8 s |
| Skip NDMF Optimizing In Play Mode | Skips NDMF's optimizing phase in play mode | 7.4 s → 6.2 s |
| Clear Root Animator Controller On Build | Removes the root Animator's controller during the build | 32.5 s → 29.8 s |
| Log Play Mode Build Time (bonus) | Logs the play-mode entry time with a breakdown for NDMF, VRCFury and the rest | — |

Effects were measured on one avatar that uses VRCFury, GoGoLoco and AAO.

### When each tool helps

NDMF is the framework under Modular Avatar, AAO and others. The effect depends on whether the avatar uses NDMF, VRCFury, or both.

| Tool | NDMF only | VRCFury only | Both |
| --- | --- | --- | --- |
| Skip Optimizers In Play Mode | Yes *1 | — | Yes *1 |
| Skip NDMF Optimizing In Play Mode | Small *2 | — | Yes |
| Clear Root Animator Controller On Build | Yes *3 | — | Yes (larger) |
| Log Play Mode Build Time | Yes | Yes | Yes |

- *1 Only when the avatar uses AAO Trace and Optimize or Avatar Compressor.
- *2 What it mainly saves is cloning the controllers VRCFury rebuilt again. On an NDMF-only avatar the gain is expected to be small (not measured).
- *3 Only when the root Animator has a controller. With VRCFury, that controller becomes VRCFury's generated FX, so the gain is larger.

On a VRCFury-only avatar, everything except the timer does nothing.

### Install

Download the `.unitypackage` from [Releases](https://github.com/kw4r3n/vrc-playmode-build-speedups/releases/latest) and import it into your project in Unity. It goes to `Assets/Kw4r3n/VRCPlayModeBuildSpeedups/`. To remove it, delete that folder.

Everything except the timer is on once installed. Turn each on or off from `Tools > Kw4r3n > VRC Play Mode Build Speedups`. The timer starts off.

### Skip Optimizers In Play Mode

Play-mode testing doesn't need final-quality optimization. When entering play mode, this removes the following from the build copy. Uploads keep them.

- AAO Trace and Optimize
- Avatar Compressor's Texture Compressor
- AAO Remove Mesh By BlendShape components whose blend shapes are all at 100 (so removing them changes nothing visible)

With these gone, Skip NDMF Optimizing In Play Mode below can work too. If other AAO components remain (Merge Skinned Mesh and so on), the optimizing phase runs as usual.

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

### Log Play Mode Build Time (bonus)

When on, every play-mode entry logs a line like this. Use it to measure the effect of a setting or tool.

```
[PlayModeBuildTimer] Entered play mode in 7.4s (avatar build 6.0s: NDMF 0.9s, VRCFury 3.5s, other 1.5s, NDMF optimizing 0.0s, after 0.1s)
```

- NDMF: NDMF's first half (Modular Avatar merging and so on)
- VRCFury: the VRCFury build
- other: tools that run between VRCFury and NDMF's optimizing phase
- NDMF optimizing: NDMF's optimizing phase (AAO and so on)

For a fair comparison, alternate the settings (A, B, B, A) and measure several times.

### See also

- [Fix for slow CreateInstance/Instantiate in the Linux Unity Editor](https://github.com/kw4r3n/unity-linux-stackcache) — on Linux this has by far the largest effect (about 15 s → 6.3 s)
- [How to speed up the ThiccWater build](Documentation~/ThiccWater.md)

### Tested with

Unity 2022.3.22f1, VRChat SDK 3.10.5, NDMF 1.14.8, Modular Avatar 1.18.7, VRCFury 1.1430.0

### Use of AI

The investigation, the code, and this README were made with AI (Anthropic's Claude Code). Measurements and testing were done for real on the environment listed under "Tested with".
