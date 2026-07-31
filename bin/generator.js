const fs = require("fs");
const path = require("path");

class Generator {
    constructor({ stubRoot, outputRoot }) {
        this.stubRoot = stubRoot;
        this.outputRoot = outputRoot;
    }

    static pascalCase(str) {
        return str.replace(/(^\w|_\w)/g, (m) =>
            m.replace("_", "").toUpperCase(),
        );
    }

    generate(type, file, replacements) {
        const stubPath = path.join(this.stubRoot, `${type}.stub`);

        if (!fs.existsSync(path.dirname(stubPath))) {
            throw new Error(
                `❌ Stub directory not found: ${path.dirname(stubPath)}`,
            );
        }

        const outputPath = path.join(this.outputRoot, file);
        fs.mkdirSync(this.outputRoot, { recursive: true });

        const stub = fs.readFileSync(stubPath, "utf-8");
        const output = this.replacePlaceholders(stub, replacements);

        fs.writeFileSync(outputPath, output);
        console.log(`✅ Created: ${outputPath}`);
    }

    replacePlaceholders(content, replacements) {
        return Object.entries(replacements).reduce((acc, [key, value]) => {
            const regex = new RegExp(`{{${key}}}`, "g");
            return acc.replace(regex, value);
        }, content);
    }
}

module.exports = Generator;
