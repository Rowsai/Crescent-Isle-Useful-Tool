const fs = require('fs');
const path = require('path');

function load_json(filePath) {
    if (!fs.existsSync(filePath)) return {};
    let content = fs.readFileSync(filePath, 'utf-8');
    if (content.charCodeAt(0) === 0xFEFF) content = content.slice(1);
    return JSON.parse(content);
}

function save_json(filePath, data) {
    fs.mkdirSync(path.dirname(filePath), { recursive: true });
    fs.writeFileSync(filePath, JSON.stringify(data, null, 2) + '\n', 'utf-8');
}

function find_null_paths(obj, prefix = '') {
    let nullPaths = [];
    for (const key in obj) {
        const fullKey = prefix ? `${prefix}.${key}` : key;
        const val = obj[key];
        if (val === null) {
            nullPaths.push(fullKey);
        } else if (val && typeof val === 'object' && !Array.isArray(val)) {
            nullPaths = nullPaths.concat(find_null_paths(val, fullKey));
        }
    }
    return nullPaths;
}

function get_value_by_path(obj, path) {
    return path.split('.').reduce((o, key) => (o && o[key] !== undefined) ? o[key] : undefined, obj);
}

function set_value_by_path(obj, path, value) {
    const parts = path.split('.');
    let current = obj;
    for (let i = 0; i < parts.length - 1; i++) {
        const part = parts[i];
        if (!(part in current)) current[part] = {};
        current = current[part];
    }
    current[parts[parts.length - 1]] = value;
}

function patch_null_fields(baseDir) {
    const legacyJp = load_json(path.join(baseDir, 'Translations', 'fr.json'));
    const jpDir = path.join(baseDir, 'Translations', 'fr');
    const files = fs.readdirSync(jpDir).filter(f => f.endsWith('.json'));

    for (const file of files) {
        console.log(`Processing ${file}...`);
        const moduleName = path.basename(file, '.json');
        const jpFilePath = path.join(jpDir, file);
        const jpData = load_json(jpFilePath);

        const nullPaths = find_null_paths(jpData);
        let updated = false;

        for (const keyPath of nullPaths) {
            const fullKey = `${keyPath}`;
            const legacyVal = get_value_by_path(legacyJp, fullKey);
            if (legacyVal !== undefined && legacyVal !== null) {
                set_value_by_path(jpData, keyPath, legacyVal);
                updated = true;
                console.log(`✓ ${file} → ${keyPath} filled from legacy`);
            }
        }

        if (updated) {
            save_json(jpFilePath, jpData);
        }
    }
}

const baseDir = path.join(__dirname, '..');
patch_null_fields(baseDir);
