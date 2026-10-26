# Issue #202 validation receipt

Implementation model: GPT-6 Luna (`gpt-6-luna`), high effort.

The documented policy uses the existing `verifier-c5-mutation.yml` workflow and aggregator without changing them. No mutation target is changed by this pull request, so no hosted mutation workflow was dispatched. Future target-changing merges require the post-merge run defined in `docs/agents/factory.md`.

Source commit for the final validation commands: `00845be9bbff97df9566dd88e214f606ff6e12dd` (`origin/main`). The repository initially had no `node_modules`; the first `npm run test:docs` and `npm run check:docs` attempts failed with `ERR_MODULE_NOT_FOUND` for `remark`. After `npm ci --no-audit --no-fund` installed the locked dependencies, the checks produced these results:

| Command | Result |
| --- | --- |
| `npm ci --no-audit --no-fund` | Passed; installed 191 packages from the lockfile. `package-lock.json` and `package.json` remained unchanged. npm reported one deprecation warning for `glob@10.5.0`. |
| `npm run test:docs` | Passed: 28 tests, 0 failures. |
| `npm run check:docs` | Exited 0. The three advisory warnings were in unchanged `docs/operations/evidence/issue-124/README.md` and `docs/operations/evidence/issue-140/README.md`; all changed documentation files reported no issues. |
| `git diff --check` | Passed. |
| `rg -n '\]\([^)]*run-c5-sample\.py' --glob '*.md' .` | No active Markdown link to the deleted controller remains. Historical command and hash records are retained as plain text. |

The first two dependency-missing failures are setup evidence, not failures after the lockfile install. No program output or raw test logs are committed.
