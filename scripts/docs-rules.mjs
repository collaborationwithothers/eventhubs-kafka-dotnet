import {readFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {visit, SKIP} from 'unist-util-visit';

const blueprintPath = 'docs/blueprint.md';

function text(node) {
  if (node.type === 'text') return node.value;
  // Keep non-text leaves as separators: dropping code or images can make
  // a cell or link label look exact, or join a declaration prefix together.
  return node.children ? node.children.map(text).join('') : '\0';
}

function leadingText(node) {
  if (!node) return '';
  return node.type === 'text' ? node.value : leadingText(node.children?.[0]);
}

function excludedLinkText(node, file) {
  // Parsed autolinks have no opening label bracket in the source. Their
  // destinations may add a scheme that is absent from the visible label.
  return node.type === 'link' && (text(node) === node.url
    || String(file).charAt(node.position.start.offset) !== '[');
}

function definitions(tree, includeAdrs = true) {
  const sources = new Map();
  const adrs = new Map();
  const labels = new Set();
  const duplicates = [];
  function add(map, id, node, declaration = node) {
    if (map.has(id)) duplicates.push({id, node});
    map.set(id, node);
    labels.add(declaration);
  }
  visit(tree, node => {
    if (includeAdrs && node.type === 'heading' && node.depth === 3) {
      const match = /^ADR-\d{3}(?![\p{L}\p{M}\p{N}_])/u.exec(text(node));
      if (match && leadingText(node).startsWith(match[0])) add(adrs, match[0], node);
    }
    if (node.type === 'tableRow') {
      const cell = node.children[0];
      const id = text(cell).trim();
      if (/^S\d+$/.test(id) && leadingText(cell).trim() === id) add(sources, id, cell, node);
    }
    if (node.type === 'listItem') {
      const paragraph = node.children[0];
      if (paragraph?.type === 'paragraph') {
        const match = /^(S\d+):/.exec(text(paragraph));
        if (match && leadingText(paragraph).startsWith(match[1])) add(sources, match[1], paragraph);
      }
    }
  });
  return {sources, adrs, labels, duplicates};
}

export default function repositoryRules() {
  const processor = this;
  return (tree, file) => {
    const isBlueprint = resolve(file.path) === resolve(file.cwd, blueprintPath);
    const local = definitions(tree, isBlueprint);
    const blueprint = isBlueprint ? local : definitions(processor.parse(
      readFileSync(resolve(file.cwd, blueprintPath), 'utf8')));
    const sources = local.sources.size ? local.sources : blueprint.sources;
    const report = (message, node, rule) => file.message(message, node, `docs:${rule}`);

    for (const {id, node} of local.duplicates) report(`Duplicate definition: ${id}`, node, 'duplicate-id');

    visit(tree, node => {
      if (excludedLinkText(node, file)) return SKIP;
      const value = node.type === 'text' ? node.value : node.alt;
      if (!value) return;
      if ([...value].some(c => c.codePointAt(0) > 127 && /\p{P}/u.test(c))) {
        report('Use ASCII punctuation in prose', node, 'punctuation');
      }
    });
    visit(tree, node => {
      // Definition labels are declarations, not references. URLs and code are
      // separate Markdown nodes, so only ordinary text is scanned here.
      if (local.labels.has(node)) return SKIP;
      if (excludedLinkText(node, file)) return SKIP;
      if (node.type !== 'text') return;
      for (const match of node.value.matchAll(/(?<![\p{L}\p{M}\p{N}_])(?:ADR-\d{3}|S\d+)(?![\p{L}\p{M}\p{N}_])/gu)) {
        const id = match[0];
        const known = id.startsWith('ADR-') ? blueprint.adrs : sources;
        if (!known.has(id)) report(`Undefined identifier: ${id}`, node, 'undefined-id');
      }
    });
  };
}
