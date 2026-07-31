const fs = require('fs');
const path = require('path');

const PATCH_FILE = 'patch.txt';
const BASE_DIR = path.join(__dirname, '..', 'Translations', 'jp');


/**
 * Example Patch:
 *
--- Checking locale: jp ---
  hunter.json: Missing 4 key(s)
    - hunter.distance_shard ("エーテライトまでの距離")
    - hunter.elapsed ("経過時間")
    - hunter.export.label ("経路をjson形式としてエクスポート")
    - hunter.export.tooltip ("現在または最後の経路をjson形式にしてクリップボードに出力します")
  modules.carrots.json: Missing 4 key(s)
    - modules.carrots.config.repeat_carrot_hunt.label ("繰り返す")
    - modules.carrots.config.repeat_carrot_hunt.tooltip ("最終ノードに到達後に再度探索を開始します")
    - modules.carrots.panel.hunt.distance_node ("次のにんじんまでの距離")
    - modules.carrots.panel.hunt.title ("にんじんハンター")
  modules.critical_encounters.json: Missing 3 key(s)
    - modules.critical_encounters.config.log_spawn.label ("出現/消滅時にチャットメッセージを表示")
    - modules.critical_encounters.config.log_spawn.tooltip ("クリティカルエンカウントが出現、または消滅したときにエコーメッセージを表示します")
    - modules.critical_encounters.config.title ("クリティカルエンカウント設定")
  modules.data.json: Missing 2 key(s)
    - modules.data.config.explanation ("このモジュールはモンスターの位置情報をクラウドソースし、コミュニティと共有します\n- これは任意機能です\n- 送信されるのはモンスター名、位置、およびそのモンスターの一意な識別子のみです")
    - modules.data.config.title ("情報共有設定")
  modules.fates.json: Missing 2 key(s)
    - modules.fates.config.log_spawn.label ("出現/消滅時にチャットメッセージを表示")
    - modules.fates.config.log_spawn.tooltip ("F.A.T.E. が出現、または消滅したときにエコーメッセージを表示します")
  modules.pathfinder.json: Missing 10 key(s)
    - modules.pathfinder.config.detection_range.label ("オブジェクト検出範囲")
    - modules.pathfinder.config.detection_range.tooltip ("ノードが存在しないと見なすために必要な距離です\nこの値はサーバーがGameObjectインスタンスを送信するタイミングによって変わる場合があります\n値を小さくするとより信頼性は高まりますが、探索に時間がかかります")
    - modules.pathfinder.config.max_level.label ("最大エリアレベル")
    - modules.pathfinder.config.max_level.tooltip ("このレベル以下のモブがいる場所までを探索対象とします")
    - modules.pathfinder.config.return_cost.label ("デミデジョンのコスト")
    - modules.pathfinder.config.return_cost.tooltip ("デミデジョンで拠点に戻る際のコスト設定です")
    - modules.pathfinder.config.teleport_cost.label ("転送網のコスト")
    - modules.pathfinder.config.teleport_cost.tooltip ("転送網を使用して他の地点へ向かう際のコスト設定です")
    - modules.pathfinder.config.text ("これは宝探しやにんじん探しの機能に関係します\nコストは経路の総距離（ヤルム単位）と比較されます")
    - modules.pathfinder.config.title ("経路検索設定")
  modules.treasure.json: Missing 1 key(s)
    - modules.treasure.panel.hunt.distance_node ("次の宝箱までの距離")
 */

function parse_patch(patchText) {
  const patch = {};
  const lines = patchText.split('\n');
  let currentFile = null;

  for (const line of lines) {
    const fileHeader = line.match(/^\s{2}([a-zA-Z0-9_.-]+): Missing \d+ key/);
    const entryLine = line.match(/^\s*-\s*([a-zA-Z0-9_.]+)\s*\("([^"]+)"\)\s*$/);

    if (fileHeader) {
      currentFile = fileHeader[1];
      patch[currentFile] = {};
    } else if (entryLine && currentFile) {
      const keyPath = entryLine[1];
      const translation = entryLine[2].replace(/\\n/g, '\n');
      patch[currentFile][keyPath] = translation;
    }
  }

  return patch;
}

function set_nested_value(obj, keyPath, value) {
  const keys = keyPath.split('.');
  let current = obj;

  for (let i = 0; i < keys.length - 1; i++) {
    const k = keys[i];
    if (typeof current[k] !== 'object' || current[k] === null) {
      current[k] = {};
    }
    current = current[k];
  }

  current[keys[keys.length - 1]] = value;
}

function apply_patch(patch) {
  for (const [filename, changes] of Object.entries(patch)) {
    const filePath = path.join(BASE_DIR, filename);

    let json = {};
    if (fs.existsSync(filePath)) {
      try {
        json = JSON.parse(fs.readFileSync(filePath, 'utf8'));
      } catch (e) {
        console.error(`Failed to parse ${filename}: ${e.message}`);
        continue;
      }
    } else {
      console.warn(`File not found: ${filename}, creating new one`);
    }

    console.log(`\nPatching: ${filename}`);
    for (const [key, value] of Object.entries(changes)) {
      console.log(`  → ${key} = "${value}"`);
      set_nested_value(json, key, value);
    }

    fs.writeFileSync(filePath, JSON.stringify(json, null, 2), 'utf8');
    console.log(`Saved: ${filename}`);
  }
}

const patchText = fs.readFileSync(PATCH_FILE, 'utf8');
const patchData = parse_patch(patchText);
apply_patch(patchData);
