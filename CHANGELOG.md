# 4.2.1

- トレジャーハンターで青銅・白銀の宝箱を開封するとき、マウントから降りずに操作するよう変更

# 4.2.0

- マジックポット宝箱到着時のクラッシュ原因だった、終了済みFATEのゲーム内ポインター保持を廃止
- FATE、CE、宝箱、にんじん、敵オブジェクトを安全な管理データへ変換し、操作直前にゲーム内オブジェクトを再取得する方式へ変更
- vnavmesh／Lifestreamが未起動・再読込中でも、自動操作の停止や移動処理からIPC例外を出さないよう保護
- ゲーム内シングルトン、アドオン、インベントリ、アクション呼び出しのNULL・状態確認を追加
- モジュールごとの更新・描画処理を例外分離し、1機能の失敗がプラグイン全体へ波及しないよう強化
- デミデジョン成功後、探究心の対象バフが未付与または設定したしきい値以下なら再付与し、元のサポートジョブへ復帰
- メイン画面に自動操作モード、CE移動、FATE移動の切替と、現在アクティブな操作モード表示を追加

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
