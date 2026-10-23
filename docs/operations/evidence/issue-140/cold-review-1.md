# Independent cold review, round 1

A fresh-context, high-effort Codex reviewer applied the repo-review skill to the full authored diff and raw-evidence manifest. The only supplied inputs were those two paths; raw evidence was excluded under AGENTS.md rule 8. Review diff SHA-256: `abbed8495a7b3b27deffc9a91c4edbaee1f32a7dfd6c14a7a6b692315b820d71`.

## Report

P1 docs/operations/evidence/issue-140/raw-manifest.json:6 AGENTS.md "Before opening a pull request", rule 8: producer.command must record the exact command used, but the manifest records a heredoc invocation while the cited raw/budget-arithmetic.txt records `python3 /tmp/issue140/budget-arithmetic.py`. Preserve the actual invocation and retain the script body separately as authored evidence; the arithmetic itself independently reproduces correctly.

Findings: 1 at P0 or P1

Pre-existing (not blocking):

- Historical raw Markdown captures retain the documented advisory link and punctuation warnings.
- TFLint remains unavailable because its signature-verification endpoint is denied; the evidence accurately reports that limitation.

## Disposition

Fixed the budget producer to record the actual invocation. [Budget arithmetic command](budget-arithmetic.md) retains the exact temporary script as authored evidence, without adding a repository checker. Checked the other producer entries for the same defect: downloaded sources now include their original curl output paths and describe the byte-preserving copy into raw evidence. The other validation commands match their recorded invocations. No output bytes, implementation, assertion or acceptance result changed. The next independent review includes this record and the corrected provenance.
