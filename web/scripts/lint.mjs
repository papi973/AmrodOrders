// Collects src/**/*.ts(x) and passes them to oxlint explicitly.
// Works around oxlint's directory scan finding 0 files in this setup.
import { readdirSync } from "node:fs";
import { join } from "node:path";
import { spawnSync } from "node:child_process";

function collect(dir) {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) return collect(path);
    return /\.(ts|tsx)$/.test(entry.name) && !entry.name.endsWith(".d.ts") ? [path] : [];
  });
}

const files = collect("src");
console.log(`Linting ${files.length} files`);

const result = spawnSync("npx", ["oxlint", ...files], {
  stdio: "inherit",
  shell: process.platform === "win32",
});
process.exit(result.status ?? 1);