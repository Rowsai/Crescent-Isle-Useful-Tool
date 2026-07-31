# Crescent Isle Useful Tool

FFXIV「蜃気楼の島 クレセントアイル」の南征編・北征編に対応した、探索支援用Dalamudプラグインです。

FATE／クリティカルエンカウント（CE）の監視、マジックポットの発生予想、宝箱・にんじん探索、通貨・経験値の計測、自動移動などを一つの画面にまとめています。

## 主な機能

### 南征編・北征編ダッシュボード

- 現在いるエリアに合わせたタブの自動選択
- 発生中のFATE／CE、進行率、開始・終了予想時間の表示
- デミアートマ、ノート、ソウルシャードなどの報酬情報表示
- 不正モードのON／OFF状態と現在のアクティビティを常時表示
- 青色テーマのカード型UI

### FATE／CE移動支援

- 発生地点に最も近いエーテライトを選択
- Lifestreamとvnavmeshを利用したテレポート・マウント・経路移動
- 北征編CE 15件、FATE 13件を個別に有効／無効化
- CE／FATEの位置へマップフラッグを設定

### 北征編マジックポット

- 「隠されのマジックポット」「飛ばされのマジックポット」を検知
- コンテンツ残り時間を基準に次回発生までの予想時間を常時表示
- 最古時間179分を基準とした初回20分後、その後30分周期の予想
- 不正モードでは通常CE／FATEより優先して移動

### 宝箱・にんじん探索

- 周囲の青銅・白銀の宝箱とにんじんを検知
- 宝箱までの距離・位置を表示
- 取得可能な青銅／白銀の宝箱数と残数を表示
- 南征編は現在地から、北征編は探査隊の北部ベースキャンプから青銅・白銀の宝箱を近い順に巡回
- 北征編の宝箱座標はゲーム内部の配置データから開始時に自動取得し、必要に応じて最寄りの魔導通路を利用
- 宝箱へ接近後に降りて開錠し、現在のプレイヤーには出現していない配置地点を自動でスキップ
- 宝箱数の計測時にすっぴんの「たんきゅうしん」を使用し、完了後は変更前のサポートジョブへ復帰

### 計測・補助機能

- 1時間あたりの銀貨・金貨獲得量
- 1時間あたりの経験値獲得量
- ナレッジバフの一括更新
- Mob Farmerによる周辺エネミーの検知・戦闘支援
- FATE／CEの情報パネルと宝箱・にんじんのレーダー表示

## 不正モード

不正モードは、発生中のアクティビティへの移動と戦闘支援を自動化する任意機能です。

- 優先順位は「マジックポット → CE → 通常FATE」
- 実行画面に検知中のFATE／CE件数、移動状態、依存プラグインの準備状態を表示
- 北征編で対象がない場合は「探査隊の北部ベースキャンプ」のエーテライト付近で待機
- 新しいアクティビティ発生時は、目的地に最も近いエーテライトを経由して移動
- FATE／CE完了後はデミデジョンで拠点へ帰還
- 拠点およびデミデジョン到着地点の周辺では、デミデジョンを実行しない
- VBMまたはBossMod Rebornとの連携、強制ターゲット設定に対応

自動化機能の使用可否や扱いは、利用環境のルールを確認したうえで各自の責任で判断してください。

## 必要なプラグイン

通常の情報表示だけであれば単体で利用できます。自動移動・不正モードを使用する場合は次のプラグインが必要です。

- Lifestream
- vnavmesh

自動移動の開始時にIPCやナビメッシュがまだ準備中の場合は、画面に待機理由を表示し、準備完了後に自動で処理を再開します。トレジャーハント開始時は移動処理の競合を防ぐため、不正モードを自動的にOFFにします。

戦闘AIの自動ON／OFFを利用する場合は、次のいずれかを設定します。

- Boss Mod（VBM）
- BossMod Reborn（BMR）

## コマンド

| コマンド | エイリアス | 内容 |
|---|---|---|
| `/ciut` | `/crescent`, `/crescentisle` | メイン画面を開く |
| `/ciut config` | `/ciutcfg`, `/ciutconfig`, `/crescentconfig` | 設定画面を開く |
| `/ciut illegal [on\|off\|toggle]` | `/ciutillegal`, `/crescentillegal` | 不正モードを操作する |
| `/ciut buff` | `/ciutbuff`, `/crescentbuff` | ナレッジバフを更新する |
| `/ciut tp [pot\|ce\|fate]` | `/ciuttp`, `/crescenttp` | 対象の最寄りエーテライトへ移動する |
| `/ciutcmd flag-active-ce` | `/crescentcmd flag-active-ce` | 受付中のCEへフラッグを設定する |
| `/ciutcmd flag-active-fate` | `/crescentcmd flag-active-fate` | 発生中のFATEへフラッグを設定する |
| `/ciutcmd flag-active-non-pot-fate` | `/crescentcmd flag-active-non-pot-fate` | マジックポット以外のFATEへフラッグを設定する |
| `/ciut language <code>` | ― | 表示言語を変更する |

対応する言語コードは `en`、`de`、`fr`、`jp`、`uwu` です。

## インストール

Dalamudの「設定」→「試験的機能」→「カスタムプラグインリポジトリ」に次のURLを追加してください。

```text
https://raw.githubusercontent.com/Rowsai/Rowsai-Plugins/refs/heads/main/pluginmaster.json
```

プラグイン一覧を更新し、「Crescent Isle Useful Tool」をインストールしてください。

## リンク

- [ソースコード](https://github.com/Rowsai/Crescent-Isle-Useful-Tool)
- [リリース一覧](https://github.com/Rowsai/Crescent-Isle-Useful-Tool/releases)
- [プラグインリポジトリ](https://github.com/Rowsai/Rowsai-Plugins)
