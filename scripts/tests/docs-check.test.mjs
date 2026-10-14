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

test('image descriptions are checked for punctuation in direct and reference images', t => {
  const {run} = fixture(t, {
    'image.png': '',
    'README.md': '# Readme\n\n![Use “quoted” text](image.png)\n\n![Use ‘quoted’ text][picture]\n\n![José uses 20 °C](image.png)\n\n[picture]: image.png\n',
  });
  const result = run();
  assert.equal(result.status, 0, result.output);
  assert.equal((result.output.match(/Use ASCII punctuation in prose/g) ?? []).length, 2, result.output);
});

test('only autolinks and exactly matching link labels are excluded from prose scans', t => {
  const {run} = fixture(t, {
    'README.md': '# Readme\n\n[www.example.com/S999](https://www.example.com/S999)\n\n[https://example.com/S998](https://example.com/S998)\n\nwww.example.com/S997\n',
  });
  const result = run();
  assert.equal(result.status, 0, result.output);
  assert.match(result.output, /Undefined identifier: S999/);
  assert.doesNotMatch(result.output, /Undefined identifier: S998|Undefined identifier: S997/);
});

test('source descriptions are declarations in both tables and lists but still check punctuation', t => {
  const {run} = fixture(t, {
    'README.md': '# Readme\n\nS1, S2 and S997.\n\n| ID | Source |\n|---|---|\n| S1 | Supersedes S999; “quote” |\n\n- S2: Supersedes S998.\n',
  });
  const result = run();
  assert.equal(result.status, 0, result.output);
  assert.match(result.output, /Undefined identifier: S997/);
  assert.match(result.output, /Use ASCII punctuation in prose/);
  assert.doesNotMatch(result.output, /Undefined identifier: S999|Undefined identifier: S998/);
});

test('split definition identifiers do not resolve references, while whole formatted identifiers do', t => {
  const {run} = fixture(t, {
    'docs/blueprint.md': blueprint + '\n### **ADR-**888: Split\n\n### **ADR-889**: Whole\n',
    'README.md': '# Readme\n\nS880, S881, S882, S883, ADR-888 and ADR-889.\n\n| ID | Source |\n|---|---|\n| **S**880 | Split |\n| **S882** | Whole |\n\n- **S**881: Split.\n- **S883**: Whole.\n',
  });
  const result = run();
  assert.equal(result.status, 0, result.output);
  for (const id of ['S880', 'S881', 'ADR-888']) assert.ok(result.output.includes(`Undefined identifier: ${id}`), result.output);
  for (const id of ['S882', 'S883', 'ADR-889']) assert.ok(!result.output.includes(`Undefined identifier: ${id}`), result.output);
});

for (const cell of ['S999 ![icon](icon.png)', 'S999 ![icon][picture]', 'S999 `extra`',
  '`S999`', '**S999 ![icon](icon.png)**', 'S999 extra']) {
  test(`source ID cell ${cell} does not define a source`, t => {
    const {run} = fixture(t, {
      'icon.png': '',
      'README.md': `# Reproduction\n\nS999.\n\n| ID | Source |\n|---|---|\n| ${cell} | source |\n\n[picture]: icon.png\n`,
    });
    const result = run();
    assert.equal(result.status, 0, result.output);
    assert.match(result.output, /3:1[^\n]*Undefined identifier: S999/, result.output);
  });
}

for (const cell of ['S999', '**S999**', '*S999*', '~~S999~~', '***S999***',
  '[S999](https://example.invalid)']) {
  test(`whole source ID cell ${cell} resolves its reference`, t => {
    const {run} = fixture(t, {
      'README.md': `# Sources\n\nS999.\n\n| ID | Source |\n|---|---|\n| ${cell} | source |\n`,
    });
    const result = run();
    assert.equal(result.status, 0, result.output);
    assert.doesNotMatch(result.output, /\bwarning\b|\berror\b/i);
  });
}

test('ordinary undefined source references are reported without a definition', t => {
  const {run} = fixture(t, {'README.md': '# Readme\n\nS999.\n'});
  const result = run();
  assert.equal(result.status, 0, result.output);
  assert.match(result.output, /3:1[^\n]*Undefined identifier: S999/, result.output);
});

test('non-text content in link labels cannot make them equal their destinations', t => {
  const {run} = fixture(t, {
    'icon.png': '',
    'README.md': '# Readme\n\n[https://example.invalid/S999![icon](icon.png)](https://example.invalid/S999)\n\n[https://example.invalid/S998`extra`](https://example.invalid/S998)\n',
  });
  const result = run();
  assert.equal(result.status, 0, result.output);
  assert.match(result.output, /Undefined identifier: S999/);
  assert.match(result.output, /Undefined identifier: S998/);
});

test('non-text content separates ADR heading text without hiding a valid prefix', t => {
  const {run} = fixture(t, {
    'icon.png': '',
    'docs/blueprint.md': blueprint + '\n### ADR-888`title`9\n\n### **ADR-889**![icon](../icon.png)alpha\n\n### ![icon](../icon.png)ADR-887: Not a declaration\n',
    'README.md': '# Readme\n\nADR-888, ADR-889 and ADR-887.\n',
  });
  const result = run();
  assert.equal(result.status, 0, result.output);
  assert.match(result.output, /Undefined identifier: ADR-887/);
  assert.doesNotMatch(result.output, /Undefined identifier: ADR-888|Undefined identifier: ADR-889/);
});

test('non-text content cannot create a source list declaration prefix', t => {
  const {run} = fixture(t, {
    'icon.png': '',
    'README.md': '# Sources\n\nS999 and S998.\n\n- S999`extra`: source.\n- S998![icon](icon.png): source.\n',
  });
  const result = run();
  assert.equal(result.status, 0, result.output);
  assert.match(result.output, /3:1[^\n]*Undefined identifier: S999/, result.output);
  assert.match(result.output, /3:1[^\n]*Undefined identifier: S998/, result.output);
});

test('Unicode word characters do not delimit identifier references or ADR declarations', t => {
  const {run} = fixture(t, {
    'docs/blueprint.md': blueprint + '\n### ADR-001α: Not a duplicate\n\n### ADR-881α: Not a declaration\n',
    'README.md': '# Readme\n\nS999é éS999 ADR-999α αADR-999 S999\u0301 \u0301S999 ADR-999\u0301 \u0301ADR-999 S999２ ２S999 ADR-999２ ２ADR-999 x_S999 S999_x x_ADR-999 ADR-999_x XS999 S999X ADR-999X XADR-999 ADR-9999\n\n(S882), [ADR-882], S883/ADR-883 and ADR-881.\n',
  });
  const result = run();
  assert.equal(result.status, 0, result.output);
  const undefinedIds = [...result.output.matchAll(/Undefined identifier: ([\w-]+)/g)].map(match => match[1]).sort();
  assert.deepEqual(undefinedIds, ['ADR-881', 'ADR-882', 'ADR-883', 'S882', 'S883'], result.output);
  assert.doesNotMatch(result.output, /Duplicate definition/);
});
