import {execFileSync} from 'node:child_process';
import {readFileSync} from 'node:fs';
import {dirname, resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
import {remark} from 'remark';
import {engine} from 'unified-engine';
import {VFile} from 'vfile';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const lock = JSON.parse(readFileSync(resolve(root, 'skills-lock.json'), 'utf8'));
const excluded = Object.keys(lock.skills).flatMap(name =>
  [`.agents/skills/${name}/`, `.claude/skills/${name}/`]);
const files = execFileSync('git', ['ls-files', '-z'], {cwd: root, encoding: 'utf8'})
  .split('\0').filter(path => /\.md$/i.test(path) && !excluded.some(prefix => path.startsWith(prefix)));
if (files.length === 0) throw new Error('No tracked Markdown files found');

engine({
  processor: remark, cwd: root,
  files: files.map(path => new VFile({cwd: root, path: resolve(root, path)})),
  rcPath: resolve(root, '.remarkrc.mjs'),
  detectConfig: false, detectIgnore: false,
  out: false, output: false, frail: false,
}, (error, code) => {
  if (error) throw error;
  process.exitCode = code;
  if (code === 0) console.log('Documentation scan completed. Warnings are advisory; external URLs were not checked.');
});
