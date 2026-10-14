# Documentation checks

Run `npm ci --ignore-scripts` and `npm run check:docs` before requesting a documentation review. The command reports a bounded set of local mistakes; warnings are advisory. A completed scan does not establish that factual claims are correct or that every possible documentation error was checked.

## Contract

The command scans tracked files with a `.md` extension, ignoring case, except third-party skill directories listed in `skills-lock.json`. Stage a new document before running the command. Markdown is parsed by remark with its GitHub-flavoured Markdown plugin; we do not maintain a Markdown parser.

| Check | Supported input | Finding |
|---|---|---|
| Punctuation | Parsed prose text, including headings and table cells | A non-ASCII character in Unicode's punctuation category |
| Local links | Standard Markdown links, images and link definitions | A local target file or heading does not exist, as reported by remark-validate-links |
| Source definitions | A table row whose first cell is exactly `S` followed by digits, or a list item starting `S` followed by digits and a colon | A source ID is defined more than once in a document |
| Source references | A complete `S` followed by digits in a prose text node | No matching source definition in the document, or in the blueprint when the document defines no sources |
| ADR definitions | A level-three heading in `docs/blueprint.md` starting with `ADR-` and three digits | An ADR ID is defined more than once |
| ADR references | A complete `ADR-` followed by three digits in a prose text node | No matching heading in the local blueprint |

Source IDs are exact: leading zeros are significant. Identifiers must be contiguous within a text node; do not split one identifier across emphasis or other inline markup. Formatting around an entire identifier is supported.

Code blocks, inline code, HTML, URL destinations and displayed URL text are excluded from identifier and punctuation checks. Source definition cells and complete source definition list paragraphs are declarations, not reference sites; their descriptions are not checked for identifier references. ADR heading declarations are also excluded from identifier-reference scans. Punctuation is still checked in declarations. Non-ASCII letters in names and unit symbols are allowed.

Links are checked through unified-engine, the engine used by the remark CLI, so that headings in other Markdown files are included. External URLs are not requested or validated. Plain-text references such as `file.md "Heading"` are not links and are not checked. Undefined Markdown reference labels and HTML links are outside this version's contract.

## Boundaries

This version does not check dates, unused source definitions, retired-source semantics, ADR body structure, issue/PR numbers or state, other branches, ADR reservations, or the truth of claims. It neither allocates ADR numbers nor coordinates merges. Use normal review for these responsibilities.

The checkout is the input. After installing the locked dependencies, the check needs neither network access nor GitHub credentials. It does not edit files. A nonzero exit means the scan could not complete; warnings alone exit successfully. Tests run separately with `npm run test:docs`.

## Rollout and review

The `docs-advisory` workflow runs the same command and the regression tests. It is not a required status check. A warning is visible in the Actions log without blocking a push or merge; installation, test and execution failures remain failures.

Assess findings against this contract. Correct a defect within the supported inputs, record a justified follow-up on the issue, or identify an unsupported input. New capabilities require a scope decision. Do not grow the parser or interpret more prose merely to clear another review comment.

Before proposing a blocking check, record examples of mistakes caught and valid documents rejected during normal use in [issue #42](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/42). Hari decides whether that evidence justifies making it required. Current acceptance tests are regression evidence, not proof of future review savings.

The initial scan reports `S43 is unused.` in the research document because that document has no S43 definition. That sentence describes an unused number rather than citing a source: it is a known false positive of the explicit token contract. It stays visible during the advisory trial; no special English-language exception is added to the checker.

## Evidence and dependencies

[PR #43](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/43) preserves the superseded implementation and review examples. Its punctuation, undefined-identifier and Markdown-context examples informed the regression tests; its custom parser and online coordination code are not carried over.

Direct tool versions and all transitive resolutions are pinned in `package.json` and `package-lock.json`. The installed upstream documentation describes the parser, CLI and local-link behaviour:

- [remark](https://github.com/remarkjs/remark/blob/5017a27db024db6feec85a3e1e19f8d78a485680/packages/remark/readme.md): Markdown parsing.
- [unified-engine](https://github.com/unifiedjs/unified-engine/blob/8c7633c5d082089eec4db58cf4f02b7f3be3c68b/readme.md): advisory warnings, configuration and cross-file processing.
- [remark-gfm](https://github.com/remarkjs/remark-gfm/blob/109972e8a773bf5dac1d6d2da0776557f36971aa/readme.md): GitHub-flavoured Markdown parsing, including tables.
- [remark-validate-links](https://github.com/remarkjs/remark-validate-links/blob/22799a03c5412aab5dd24e70158b1b71ff6ac971/readme.md): local files and headings; cross-file heading checking requires the CLI.
- [unist-util-visit](https://github.com/syntax-tree/unist-util-visit/blob/5d601df684ca7341646d6b57eb0f20fdfe277bc2/readme.md): traversal of parsed nodes.
