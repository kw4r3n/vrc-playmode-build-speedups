# ThiccWater のビルドを速くする修正手順 / Speeding up the ThiccWater build

[日本語](#日本語) | [English](#english)

## 日本語

ThiccWater(PleasureArcade、確認したバージョン 2.0)は有料アセットのため、修正済みのコードはここに含めていません。以下の手順で、各自のプロジェクトのファイルを直してください。修正前に、プロジェクトのバックアップかgitのコミットを取ってください。

ThiccWaterはビルドのたびに、アセットの保存と再インポートをエミッターやクリップごとに繰り返しています。保存とインポートは最後に1回で十分です。

| 修正 | 対象ファイル | 効果(実測) |
| --- | --- | --- |
| 1. クリップのコピーをメモリ上で行う | `Scripts/AnimationClipEditor.cs` | 1クリップあたり約1秒 |
| 2. 途中の保存をやめる | `Scripts/AnimatorCloner.cs`、`Resources/EmitterColor.cs`、`Editor/ThiccWaterCompiler.cs` | 4.6秒→3.2秒 |
| 3. 生成アセットのインポートをまとめる | `Editor/ThiccWaterCompiler.cs`、`Scripts/AnimationClipEditor.cs` | 約1.6秒 |

パスはすべて `Assets/PleasureArcade/ThiccWater/` からの相対パスです。

### 1. クリップのコピーをメモリ上で行う

`AnimationClipEditor.RetargetAnimation` は、`AssetDatabase.CopyAsset` でクリップをコピーしてから書き換えています。`CopyAsset` は1回ごとに完全なインポートを走らせ、全パッケージのアセットポストプロセッサーも呼ばれます。

- 先頭の `AssetDatabase.CopyAsset(oldPath, newPath);` と、その次の `LoadAssetAtPath` の行を、次の2行に置き換える。

  ```csharp
  var anim = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AnimationClip>(oldPath));
  anim.name = System.IO.Path.GetFileNameWithoutExtension(newPath);
  ```

- メソッド末尾の `AssetDatabase.SaveAssets();` を `AssetDatabase.CreateAsset(anim, newPath);` に置き換える。

同じファイルの `RegroupAnimation` は使われていないので、そのままで構いません。

### 2. 途中の保存をやめる

`ThiccWaterCompiler.OnPreprocessAvatar` は最後に `AssetDatabase.SaveAssets()` を呼ぶので、途中の保存は不要です。途中で保存すると、そのたびに変更済みのアセット全部(VRCFuryが作った大きなFXコントローラーを含む)が書き出され、再インポートされます。

次の行を削除します。

- `Scripts/AnimatorCloner.cs`: `EditorUtility.SetDirty(mainController);` の直後にある `AssetDatabase.SaveAssets();` と `AssetDatabase.Refresh();`。`SetDirty` は残す。
- `Resources/EmitterColor.cs`: `SaveMaterialAsset` 内の `AssetDatabase.CreateAsset(...)` の直後にある `AssetDatabase.SaveAssets();`。
- `Editor/ThiccWaterCompiler.cs`:
    - `GetFXController` 内、FXコントローラーが無いときの分岐の末尾にある `AssetDatabase.SaveAssets();` と `AssetDatabase.Refresh();`。
    - `GetMenuControlFromPath` の `return existingControl;` の直前にある `AssetDatabase.SaveAssets();`。

`OnPreprocessAvatar` の最後の `AssetDatabase.SaveAssets();` は残します。

### 3. 生成アセットのインポートをまとめる

エミッターの生成処理全体を `AssetDatabase.StartAssetEditing()` と `StopAssetEditing()` で囲み、生成したクリップ・メニュー・マテリアルを1回でインポートします。修正2を先に済ませてください。

囲んでいる間は、新しく作ったアセットを `LoadAssetAtPath` で読み戻せません。そのため、読み戻している2か所も直します。

1. `Scripts/AnimationClipEditor.cs` の `RetargetAnimation` の戻り値を `void` から `AnimationClip` に変え、末尾の `CreateAsset` の後に `return anim;` を足す。
2. `Editor/ThiccWaterCompiler.cs` で `RetargetAnimation(...)` を呼んでいる箇所を、戻り値をそのまま `state.state.motion` に入れる形にし、直後の `LoadAssetAtPath<AnimationClip>(newMotionPath)` の行を削除する。

    ```csharp
    state.state.motion = AnimationClipEditor.RetargetAnimation(motionPath, newMotionPath, emitterType.shape.name, emitterObjects.ToArray(), _avatar.transform);
    ```

3. `OnPreprocessAvatar` の中で、`TW.InitGeneratedAssetFolder();` の次の `foreach (var emitter in emitters)` から、最後の `Object.Instantiate(TW.resources.depthProvider, ...)` までを、新しいメソッド `BuildEmitters` に移す。そのメソッドは最後に `return true;` を返す(途中の `return false;` はそのまま)。
4. 移した部分の代わりに、`OnPreprocessAvatar` に次を書く。

    ```csharp
    // Batch the generated clips, menus and materials into one import instead of one per CreateAsset
    bool built;
    AssetDatabase.StartAssetEditing();
    try
    {
        built = BuildEmitters(_vrcCloneObject, emitters);
    }
    finally
    {
        AssetDatabase.StopAssetEditing();
    }
    if (!built) return false;

    AssetDatabase.SaveAssets();

    return true;
    ```

    新しいメソッドの宣言は次のとおり。

    ```csharp
    private bool BuildEmitters(GameObject _vrcCloneObject, ThiccWaterEmitter[] emitters)
    ```

5. `GetFXController` の、FXコントローラーが無いときの分岐では、空のコントローラーを `CopyAsset` した直後に `LoadAssetAtPath` で読み戻しています。ここだけは一時的に囲みを外します。`CopyAsset` の行の直前に `AssetDatabase.StopAssetEditing();` を、`LoadAssetAtPath<AnimatorController>(fxPath)` の行の直後に `AssetDatabase.StartAssetEditing();` を足す。

`StartAssetEditing` の囲みが閉じないまま例外で抜けると、Unityを再起動するまでアセットのインポートが止まります。必ず `try`/`finally` で囲んでください。

### 確認方法

- 修正の前後で、プレイモード中のアバターのFXレイヤー数・パラメータ数・Expression Parametersのコストが同じであること。
- `Assets/PleasureArcade/ThiccWater/GeneratedAssets/` に、修正前と同じ数の `.anim` と `.asset` が作られること。
- コンソールにエラーが出ないこと。

## English

ThiccWater (PleasureArcade, tested with version 2.0) is a paid asset, so no patched code is included here. Follow these steps to change the files in your own project. Back up the project or commit to git first.

On every build, ThiccWater saves and reimports assets once per emitter or clip. Saving and importing once at the end is enough.

| Change | Files | Measured effect |
| --- | --- | --- |
| 1. Copy clips in memory | `Scripts/AnimationClipEditor.cs` | about 1 s per clip |
| 2. Stop saving midway | `Scripts/AnimatorCloner.cs`, `Resources/EmitterColor.cs`, `Editor/ThiccWaterCompiler.cs` | 4.6 s → 3.2 s |
| 3. Batch the generated assets into one import | `Editor/ThiccWaterCompiler.cs`, `Scripts/AnimationClipEditor.cs` | about 1.6 s |

All paths are relative to `Assets/PleasureArcade/ThiccWater/`.

### 1. Copy clips in memory

`AnimationClipEditor.RetargetAnimation` copies the clip with `AssetDatabase.CopyAsset` and then edits it. Each `CopyAsset` runs a full import, including every package's asset postprocessors.

- Replace the leading `AssetDatabase.CopyAsset(oldPath, newPath);` and the `LoadAssetAtPath` line after it with:

  ```csharp
  var anim = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AnimationClip>(oldPath));
  anim.name = System.IO.Path.GetFileNameWithoutExtension(newPath);
  ```

- Replace the `AssetDatabase.SaveAssets();` at the end of the method with `AssetDatabase.CreateAsset(anim, newPath);`.

`RegroupAnimation` in the same file is unused and can stay as it is.

### 2. Stop saving midway

`ThiccWaterCompiler.OnPreprocessAvatar` calls `AssetDatabase.SaveAssets()` at the end, so the saves in between are unnecessary. Each one writes out and reimports every dirty asset, including the large FX controller VRCFury generates.

Delete these lines:

- `Scripts/AnimatorCloner.cs`: the `AssetDatabase.SaveAssets();` and `AssetDatabase.Refresh();` right after `EditorUtility.SetDirty(mainController);`. Keep the `SetDirty`.
- `Resources/EmitterColor.cs`: the `AssetDatabase.SaveAssets();` right after `AssetDatabase.CreateAsset(...)` in `SaveMaterialAsset`.
- `Editor/ThiccWaterCompiler.cs`:
    - in `GetFXController`, the `AssetDatabase.SaveAssets();` and `AssetDatabase.Refresh();` at the end of the branch taken when there is no FX controller.
    - in `GetMenuControlFromPath`, the `AssetDatabase.SaveAssets();` right before `return existingControl;`.

Keep the final `AssetDatabase.SaveAssets();` in `OnPreprocessAvatar`.

### 3. Batch the generated assets into one import

Wrap the whole emitter build in `AssetDatabase.StartAssetEditing()` and `StopAssetEditing()` so the generated clips, menus and materials import once. Do change 2 first.

While the batch is open, newly created assets cannot be loaded back with `LoadAssetAtPath`, so fix the two places that do that.

1. In `Scripts/AnimationClipEditor.cs`, change `RetargetAnimation`'s return type from `void` to `AnimationClip`, and add `return anim;` after the final `CreateAsset`.
2. In `Editor/ThiccWaterCompiler.cs`, assign the result of the `RetargetAnimation(...)` call straight to `state.state.motion`, and delete the `LoadAssetAtPath<AnimationClip>(newMotionPath)` line after it.

    ```csharp
    state.state.motion = AnimationClipEditor.RetargetAnimation(motionPath, newMotionPath, emitterType.shape.name, emitterObjects.ToArray(), _avatar.transform);
    ```

3. In `OnPreprocessAvatar`, move everything from the `foreach (var emitter in emitters)` after `TW.InitGeneratedAssetFolder();` through the final `Object.Instantiate(TW.resources.depthProvider, ...)` into a new method `BuildEmitters`. Make it end with `return true;` (keep the `return false;` lines inside).
4. In place of the moved code, put this in `OnPreprocessAvatar`:

    ```csharp
    // Batch the generated clips, menus and materials into one import instead of one per CreateAsset
    bool built;
    AssetDatabase.StartAssetEditing();
    try
    {
        built = BuildEmitters(_vrcCloneObject, emitters);
    }
    finally
    {
        AssetDatabase.StopAssetEditing();
    }
    if (!built) return false;

    AssetDatabase.SaveAssets();

    return true;
    ```

    The new method is declared as:

    ```csharp
    private bool BuildEmitters(GameObject _vrcCloneObject, ThiccWaterEmitter[] emitters)
    ```

5. In `GetFXController`, the branch taken when there is no FX controller copies the empty controller with `CopyAsset` and immediately loads it back with `LoadAssetAtPath`. Pause the batch around just that: add `AssetDatabase.StopAssetEditing();` right before the `CopyAsset` line, and `AssetDatabase.StartAssetEditing();` right after the `LoadAssetAtPath<AnimatorController>(fxPath)` line.

If an exception leaves a `StartAssetEditing` batch open, asset imports stay stopped until Unity restarts. Always use the `try`/`finally`.

### Checking the result

- The avatar in play mode has the same FX layer count, parameter count and Expression Parameters cost before and after the change.
- `Assets/PleasureArcade/ThiccWater/GeneratedAssets/` gets the same number of `.anim` and `.asset` files as before.
- No errors in the console.
