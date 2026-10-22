import { createHash } from 'node:crypto';
import { readdir, readFile, stat, writeFile } from 'node:fs/promises';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const directory = dirname(fileURLToPath(import.meta.url));
const commands = new Map();
const readJson = async name => JSON.parse(await readFile(join(directory, name), 'utf8'));
const recorded = await readJson('raw-manifest.json');
for (const entry of recorded.files) commands.set(entry.path.replace(/^raw\//, ''), entry.producer);
for (const [name, prefix] of [
  ['terraform-producers.json', 'terraform'],
  ['aks-binding-producers.json', 'aks-binding'],
  ['local-producers.json', 'local'],
  ['resume-producers.json', 'resume'],
  ['review-fix-producers.json', 'review-fixes'],
  ['review-4-producers.json', 'review-4-fixes'],
  ['review-5-producers.json', 'review-5-fixes'],
]) {
  const receipt = await readJson(name);
  for (const [file, command] of Object.entries(receipt.commands)) {
    commands.set(`${prefix}/${file}`, `${command} [source ${receipt.source_commit}; ${receipt.source_status}]`);
  }
}
commands.set('metadata/chart-deployment-source-inventory-post-execution.json',
  'python /workspace/scratch/write-chart-producers.py; byte-preserving move of chart-deployment-source-inventory-post-execution.json to raw/metadata/ [source 4a0acc5d886a12cafdfdeafbe1e71f4d0802bf55; post-execution dirty-source inventory, not historical execution provenance]');

const files = [];
async function visit(path) {
  for (const item of (await readdir(path, { withFileTypes: true })).sort((a, b) => a.name.localeCompare(b.name, 'en'))) {
    const absolute = join(path, item.name);
    if (item.isSymbolicLink()) throw new Error(`Symlink in raw evidence: ${absolute}`);
    if (item.isDirectory()) {
      await visit(absolute);
      continue;
    }
    const key = relative(join(directory, 'raw'), absolute).replaceAll('\\', '/');
    const producer = commands.get(key);
    if (!producer) throw new Error(`Producer not recorded: ${key}`);
    files.push({
      path: `raw/${key}`,
      producer,
      size_bytes: (await stat(absolute)).size,
      sha256: createHash('sha256').update(await readFile(absolute)).digest('hex'),
    });
  }
}
await visit(join(directory, 'raw'));
files.sort((a, b) => a.path.localeCompare(b.path, 'en'));
await writeFile(join(directory, 'raw-manifest.json'), `${JSON.stringify({ files }, null, 2)}\n`);
