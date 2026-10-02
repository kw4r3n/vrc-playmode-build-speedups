# リリース手順

1. `package.json` の `version` と `CHANGELOG.md` を更新してコミットする。
2. `vX.Y.Z` のタグを付けて、`main` とタグを push する。
3. `Assets/Kw4r3n/VRCPlayModeBuildSpeedups/` を `VRCPlayModeBuildSpeedups_vX.Y.Z.unitypackage` として書き出す。
4. GitHub の Release `vX.Y.Z` を作り、unitypackage を添付する。
5. unitypackage を `VRCPlayModeBuildSpeedups_vX.Y.Z.zip` に固めて BOOTH にアップロードし、**すべてのバリエーション**に付けて、古いファイルを外す。
6. BOOTH の配布ファイルが新しい版になったか確かめ、商品ページをバックアップする（非公開の booth-shop で）。

   ```sh
   cd ~/Work/Tools/booth-shop
   uv run booth-shop check            # 全バリエーションが OK になるまで 5 を直す
   uv run booth-shop backup --commit
   ```
