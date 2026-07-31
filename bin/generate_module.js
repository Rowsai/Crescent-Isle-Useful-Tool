const Generator = require("./generator");
const path = require("path");

const [name] = process.argv.slice(2);

if (!name) {
    console.error("❌ Usage: node generate_module.js <name>");
    process.exit(1);
}

const generator = new Generator({
    stubRoot: path.join(__dirname, "/../stubs"),
    outputRoot: path.join(
        __dirname,
        "/../CrescentIsleUsefulTool",
        "Modules",
        name,
    ),
});

generator.generate("module/module", `${name}Module.cs`, { name });
generator.generate("module/config", `${name}Config.cs`, { name });
generator.generate("module/panel", `Panel.cs`, { name });
