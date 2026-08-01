# 4.1.0

- 「探究心」を有効にしている場合、4種類の30分バフのうち1つでも切れているか、残り時間が設定値以下になると確実に再付与
- 「探究心」使用後に、変更前のサポートジョブへ復帰する処理を強化
- プレイヤーまたはvnavmeshが移動中の場合、デミデジョンを詠唱しない安全判定を追加
- 画面、設定、コマンド、内部クラス名を「自動操作モード」に統一
- 自動操作モード用コマンドを `/ciut auto`、`/ciutauto`、`/crescentauto` に変更

# 4.0.0

- ソースフォルダ、プロジェクト、アセンブリ、名前空間、内部識別子を `CrescentIsleUsefulTool` に統一
- 配布DLLとマニフェストを新しい内部識別子へ移行
- 旧来の開発用クラス名、キューID、翻訳表記、VS Codeビルドパスを製品名準拠へ整理
- 互換ライブラリをクリーンなローカルソースからビルドする構成へ変更
- 未使用だった外部データ送信モジュールと固定APIキーを削除

# 3.3.0

- マジックポットFATEの実測開始時刻を検知し、30分周期の次回発生予想へ反映
- 財宝誘導バフ、マジカルエリクサー、方向・距離ヒントを利用するマジックポット宝箱探索モードを追加
- 北征編マジックポット宝箱の候補座標を追加
- トレジャーハントを停止した地点から再開できるよう変更
- 取得済みの青銅・白銀宝箱数を追加し、複数の宝箱オブジェクトを個別に追跡
- FATE／CEが妖火の漁村エリア外にある場合、妖火の漁村エーテライトを移動候補から除外

# 0.11.0

- Updated UI to include both a teleport and move to button
- Can no longer click teleport if you are already next to the destination aetheryte
- Updated aethenet shard for Brain Drain
- Added some custom paths for certain fates, so that the path taken to walk to them is more natural

# 0.12.0

- Removed Crowdsourcing module
- Added WindowManager Module
    - This module allow you to configure if the main and config windows open and close on plugin load, enter zone and
      exit zone

# 0.12.1

- Changed labels in WindowManager config slightly
