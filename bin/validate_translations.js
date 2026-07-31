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

function flatten_keys(obj, prefix = '') {
    let keys = [];
    for (const key in obj) {
        const fullKey = prefix ? `${prefix}.${key}` : key;
        if (obj[key] !== null && typeof obj[key] === 'object' && !Array.isArray(obj[key])) {
            keys = keys.concat(flatten_keys(obj[key], fullKey));
        } else {
            keys.push(fullKey);
        }
    }
    return keys;
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

function check_translations(basePath) {
    const locales = fs.readdirSync(basePath).filter(f => fs.statSync(path.join(basePath, f)).isDirectory());
    if (!locales.includes('en')) {
        console.error(`Missing base locale folder: en`);
        process.exit(1);
    }

    const baseDir = path.join(basePath, 'en');
    const baseModules = fs.readdirSync(baseDir).filter(f => f.endsWith('.json'));

    const baseLocale = {};
    for (const file of baseModules) {
        const name = path.basename(file, '.json');
        baseLocale[name] = load_json(path.join(baseDir, file));
    }

    for (const locale of locales) {
        if (locale === 'en') continue;

        console.log(`--- Checking locale: ${locale} ---`);
        const localeDir = path.join(basePath, locale);

        for (const moduleName of Object.keys(baseLocale)) {
            const baseData = baseLocale[moduleName];
            const baseKeys = new Set(flatten_keys(baseData));

            const filePath = path.join(localeDir, `${moduleName}.json`);
            let localeData = fs.existsSync(filePath) ? load_json(filePath) : {};

            const missing = [...baseKeys].filter(k => {
                const val = get_value_by_path(localeData, k);
                return val === undefined || val === null;
            });

            const localeKeys = new Set(flatten_keys(localeData));
            const excess = [...localeKeys].filter(k => !baseKeys.has(k));

            if (missing.length === 0 && excess.length === 0) {
                continue;
            }

            if (missing.length > 0) {
                console.log(`  ${moduleName}.json: Missing ${missing.length} key(s)`);
                for (const key of missing.sort()) {
                    const enVal = get_value_by_path(baseData, key);
                    console.log(`    - ${key} (${JSON.stringify(enVal)})`);
                    set_value_by_path(localeData, key, null);
                }
            }

            if (excess.length > 0) {
                console.log(`  ${moduleName}.json: Excess ${excess.length} key(s)`);
                for (const key of excess.sort()) {
                    console.log(`    - ${key}`);
                }
            }

            save_json(filePath, localeData);
        }

        // Check for any extra files in this locale not present in en
        const localeFiles = fs.readdirSync(localeDir).filter(f => f.endsWith('.json'));
        const extraFiles = localeFiles.filter(f => !baseModules.includes(f));
        if (extraFiles.length > 0) {
            console.log(`  Extra files in ${locale}/:`);
            extraFiles.forEach(f => console.log(`    - ${f}`));
        }

        console.log();
    }
}

const basePath = path.join(__dirname, '..', 'Translations');
check_translations(basePath);
