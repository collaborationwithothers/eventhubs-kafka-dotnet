import assert from 'node:assert/strict';
import {execFileSync, spawnSync} from 'node:child_process';
import {copyFileSync, mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {dirname, join, resolve} from 'node:path';
import {fileURLToPath, pathToFileURL} from 'node:url';
import {test} from 'node:test';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const blueprint = `# Blueprint

### ADR-001: Example

Source S1.

| ID | Source |
|---|---|
| S1 | [Source](https://example.invalid) |
`;

function fixture(t, files = {}) {
  const cwd = mkdtempSync(join(tmpdir(), 'docs-check-'));
  t.after(() => rmSync(cwd, {recursive: true, force: true}));
  const contents = {'docs/blueprint.md': blueprint, 'skills-lock.json': '{"skills":{"vendor":{}}}', ...files};
  for (const [path, content] of Object.entries(contents)) {
    mkdirSync(dirname(join(cwd, path)), {recursive: true});
    writeFileSync(join(cwd, path), content);
  }
  mkdirSync(join(cwd, 'scripts'), {recursive: true});
  copyFileSync(join(root, 'scripts/check-docs.mjs'), join(cwd, 'scripts/check-docs.mjs'));
  writeFileSync(join(cwd, '.remarkrc.mjs'), `export {default} from ${JSON.stringify(pathToFileURL(join(root, '.remarkrc.mjs')).href)};\n`);
  symlinkSync(join(root, 'node_modules'), join(cwd, 'node_modules'), 'dir');
  execFileSync('git', ['init', '--quiet'], {cwd});
  execFileSync('git', ['add', '--', ...Object.keys(contents)], {cwd});
  return {
    cwd,
    run() {
      const result = spawnSync(process.execPath, [join(cwd, 'scripts/check-docs.mjs')], {
        cwd, encoding: 'utf8', env: {...process.env, NO_COLOR: '1'},
      });
      assert.ifError(result.error);
      return {status: result.status, output: result.stdout + result.stderr};
    },
  };
}

test('recorded mistake classes are reported in an advisory scan', t => {
  const {run} = fixture(t, {
    'docs/blueprint.md': blueprint + '\n### ADR-001: Duplicate\n\n| ID | Source |\n|---|---|\n| S1 | duplicate |\n',
    'README.md': '# Readme\n\nUse “quotes”. S999 and ADR-999.\n\n[missing](missing.md)\n\n[wrong heading](docs/blueprint.md#absent)\n',
  });
  const result = run();
  assert.equal(result.status, 0, result.output);
  for (const expected of ['punctuation', 'Undefined identifier: S999', 'Undefined identifier: ADR-999',
    'Duplicate definition: S1', 'Duplicate definition: ADR-001', 'missing-file', 'missing-heading']) {
    assert.ok(result.output.includes(expected), result.output);
  }
  assert.match(result.output, /Documentation scan completed/);
});

test('valid prose and Markdown examples do not produce false identifier findings', t => {
  const {run} = fixture(t, {
    'README.md': `# Readme

José uses 20 °C and 5 µs. **S1** and *ADR-001*.

[guide](docs/guide.md#underlined) and [repeat](docs/guide.md#repeat-1).

[S1](https://example.invalid/S999 "ADR-999")

https://example.invalid/S999

www.example.com/S999

<person@S999.example>

\`S999 ADR-999 “example”\`

<!-- S999 ADR-999 “comment” -->

> ~~~markdown
> ### ADR-999: Example
> S999 “example”
> ~~~

    S999 ADR-999 “indented example”

~~~markdown
[not a real link](missing.md)
S999 ADR-999
~~~
`,
    'docs/guide.md': '# Guide\n\nUnderlined\n----------\n\n## Repeat\n\n## Repeat\n',
  });
  const result = run();
  assert.equal(result.status, 0, result.output);
  assert.doesNotMatch(result.output, /\bwarning\b|\berror\b/i);
});

test('document source namespaces are local and exact', t => {
  const {run} = fixture(t, {
    'local.md': '# Sources\n\nS01 is valid, S1 is missing here.\n\n- S01: https://example.invalid\n- S01: duplicate\n',
    'README.md': '# Readme\n\nS1 resolves from the blueprint.\n',
  });
  const result = run();
  assert.equal(result.status, 0, result.output);
  assert.match(result.output, /Undefined identifier: S1\b/);
  assert.match(result.output, /Duplicate definition: S01/);
  assert.doesNotMatch(result.output, /Undefined identifier: S01/);
});

test('discovery excludes pinned skills and untracked documents, but includes owned and uppercase Markdown', t => {
  const f = fixture(t, {
    '.agents/skills/vendor/SKILL.md': 'S888',
    '.claude/skills/vendor/SKILL.md': 'S887',
    '.agents/skills/repo-review/SKILL.md': 'S886',
    'SECURITY.MD': 'S885',
    'docs/[draft].md': 'S883',
  });
  writeFileSync(join(f.cwd, 'untracked.md'), 'S884');
  const result = f.run();
  assert.equal(result.status, 0, result.output);
  assert.match(result.output, /Undefined identifier: S886/);
  assert.match(result.output, /Undefined identifier: S885/);
  assert.match(result.output, /Undefined identifier: S883/);
  assert.doesNotMatch(result.output, /S888|S887|S884/);
});

test('broken configuration fails the scan rather than appearing successful', t => {
  const f = fixture(t);
  writeFileSync(join(f.cwd, '.remarkrc.mjs'), 'throw new Error("fixture configuration failure");');
  const result = f.run();
  assert.notEqual(result.status, 0);
  assert.match(result.output, /fixture configuration failure/);
  assert.doesNotMatch(result.output, /Documentation scan completed/);
});

test('scan is read-only and external links require no successful response', t => {
  const input = '# Readme\n\n[S1](https://does-not-exist.invalid)\n';
  const f = fixture(t, {'README.md': input});
  const result = f.run();
  assert.equal(result.status, 0, result.output);
  assert.doesNotMatch(result.output, /\bwarning\b|\berror\b/i);
  assert.equal(readFileSync(join(f.cwd, 'README.md'), 'utf8'), input);
});
