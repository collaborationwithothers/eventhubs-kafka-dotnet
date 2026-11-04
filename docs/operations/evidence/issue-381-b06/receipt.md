# B06 publication receipt

This publication applies the approved acknowledgement rule to the existing synthetic run. It preserves the candidate observations and records the latest mutation acceptance as a publication gate.

## Execution card

- Deliverable: [report](../../../../results/B06/2026-11-04.md), [structured publication](../../../../results/B06/2026-11-04.json) and [item state](../../../../items/381/state.md).
- Authorization: [Revision 1 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/381#issuecomment-6639623294), [standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/381#issuecomment-6639711510) with no blocking or apply-in-implementation finding, and [adopted authority](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/381#issuecomment-6639454561). The issue tool comment names Codex; the ready-for-agent event is 2026-11-04T19:03:32Z. Parent items are the shared batch and producer work, linked in the state file.
- Source commit: `aca33589a3c69056dcbad3677698a53da7487bf8`; implementation base: `3f4b29e79883ec520eaed78bbf4390b3b7abeb5b`.
- Mandatory gates and earliest checkpoint: the plan's Enforced checks 1-5, latest applicable mutation acceptance and cold review before a non-backup push or verdict pull request. Final-head CI, hosted Codex review and fresh Claude review remain required. Hari merges; this B06 pull request does not close the shared issue.
- Review budget: zero comprehensive and zero focused passes started at authoring. Use the cumulative limits and exhaustion action in [pull-request rule 8](../../../agents/pull-requests.md). The root implementation session owns reviews and records their complete reports and dispositions in the pull request self-review record; no review has been claimed here.
- Work bounds: the approved plan's two candidates, ten repeats and 32 Jobs. Temporary script authoring used one initial pass and two consolidated correction passes (the initial delimiter correction and the review prose correction); no correction pass remains. Changed-file advisory warnings have the plan's 20-disposition cap. At exhaustion, push -wip and hand off under the plan; no additional run or investigation is authorized.
- Predicate map: not applicable; no verifier predicate changes. This temporary publication checker checks the closed publication contract and does not alter scientific validity.
- Uncertainty: node size is configured at the source commit, not independently measured. Callback latency is not wire latency, batch-window coverage is not independently proved, and durability was not tested.

## Inputs and reproducibility

The [candidate artifact](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41866017867/artifacts/13353901810) and [pod artifact](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41866017867/artifacts/13352834167) are the bounded scenario inputs. The JSON records their expiry, member names, hashes and exact pod member paths. The historical [mutation summary artifact](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41866011149/artifacts/13349781411) is distinct from the latest applicable mutation result. Raw logs, pod JSON and downloaded command output stay outside git.

Candidate byte hashes:

- `B06-C0-candidate.json`: `b8cd500e0d300f8874609e0ec83d9ff38145044cd42e2f5fa4d8441108c83c62`.
- `B06-C1-candidate.json`: `e3f2d5e612e38507a72c3141ed76db587686cdea33e44a034c85a9c95adf2ff7`.

Extract the source block below to `/tmp/issue-381/verify-b06-publication.py`. It is the entire authored generation and validation script, including the report text; there is no unreviewed helper. Run from the repository root with the evidence directory populated as follows: named candidates in `candidates/`; expanded pod artifact in `pods/`; historical summary in `mutation/summary.json`; the run and artifact REST responses in `<run-id>-run.json`, `<run-id>-artifacts.json` and `<run-id>-jobs.json`; and the sanitized batch destroy-state excerpt in `infrastructure-log-evidence.txt`. On success, the latest summary goes in `latest-mutation/summary.json` with its run REST response in `41892353617-run.json`.

The source-pinned scenario, expected outcome, profile and contracts are linked by the JSON/report. Microsoft's configuration page was read from the cached official page on 2026-11-04: the `acks` row is in the Java defaults table; the complete librdkafka section contains no `acks` row. No claim about replication or guaranteed durability is added.

## Verification commands and results

```sh
python /tmp/issue-381/verify-b06-publication.py --evidence /tmp/issue-381 --result results/B06/2026-11-04.json --report results/B06/2026-11-04.md --write
python /tmp/issue-381/verify-b06-publication.py --evidence /tmp/issue-381 --result results/B06/2026-11-04.json --report results/B06/2026-11-04.md
python -m json.tool results/B06/2026-11-04.json >/dev/null
npm run check:docs
git diff --check
git diff --name-only origin/main
```

The rendering command writes only the two approved publication files and does not certify acceptance. Validation compares the complete selected dictionaries, exact repeat ordinals, phase sums, ratios, medians and report rendering against the named sources. Pod records are bound to phase-status Jobs, pod owner references and artifact paths. The final gate checks measured commit ancestry and every current configured mutation target against origin/main; an older pass cannot mask a later target change.

The checker passed source identity, exact projection, arithmetic, pod binding, manuscript, four-path scope and latest mutation acceptance, and printed `B06 publication checks passed`. The accepted [mutation summary](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41892353617/artifacts/13360514326) identifies `3f4b29e79883ec520eaed78bbf4390b3b7abeb5b`, with `measurement_complete=true`, `break_passed=true` and no incomplete reasons. That commit includes the scenario source and the latest configured-target change; main is unchanged since it. JSON parsing, working-tree and staged whitespace checks passed. The path listing contained only the four planned files. Documentation checks completed with 8 warnings, all in unchanged files and outside this publication scope. No runtime code changed, so no runtime tests were added or repeated.

Cold-review mode: focused correction, comparing against `e82ac6df2ebdef25a23bf91609fbc48eee7a5d61`, with the full diff against base `3f4b29e79883ec520eaed78bbf4390b3b7abeb5b` as context. The frozen input commit is identified by the full SHA in the supplied review-diff filename. Scope is the baseline citation, explicit synthetic-data statement, matching renderer prose, review record and shared state catch-up. The latest [Claude comprehensive review](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/384#pullrequestreview-5776151425) is complete; its findings and applicable surrounding text are the correction scope. Cumulative counts including this round are two comprehensive and 2 focused reviews. Completed reports and dispositions belong in the PR description. No further comprehensive review is permitted without Hari's explicit decision; the cumulative focused limit and exhaustion action remain pull-request rule 8. Final-head CI, hosted Codex review, fresh Claude review and Hari's verdict merge remain outstanding for the correction.

Latest mutation acceptance is complete, as recorded above. The remaining gates are reviews and final-head CI before Hari's verdict merge.

## Review corrections

The user requested fixes for the review findings on both publication PRs. The baseline-setting citation now points to the source-pinned catalog line; the scenario link supports the override. Both reports explicitly state that workload data is synthetic. These are prose corrections in the reports and their embedded renderers; candidate projections, numbers, verdicts and decision rules are unchanged.

Verification uses the same commands above after extracting the updated embedded script. The final-head workflow and hosted review results will be recorded in the PR description.

## Full authored script

```python
#!/usr/bin/env python3
"""Bounded B06 publication rendering and source comparison; no runtime changes."""
import argparse
import hashlib
import json
import statistics
import subprocess
from pathlib import Path

REPO = 'https://github.com/collaborationwithothers/eventhubs-kafka-dotnet'
SOURCE = 'aca33589a3c69056dcbad3677698a53da7487bf8'
BASE = '3f4b29e79883ec520eaed78bbf4390b3b7abeb5b'
RUN = 41866017867
LATEST = 41892353617
TOP = 'schema_version synthetic scenario_id configuration_id run_id phase source_commit status gate versions image_digests timings counts reasons'.split()
REPEAT = 'repeat status reasons checks identity counts broker_count batching_recorded_evidence'.split()
COUNTS = 'ok loss phantom reported_failure duplicate reorder'.split()
MEASURES = 'produce_requests messages_per_request batch_count batch_max_messages'.split()
FILES = {'results/B06/2026-11-04.json', 'results/B06/2026-11-04.md', 'docs/operations/evidence/issue-381-b06/receipt.md', 'items/381/state.md'}


def read(path):
    return json.loads(path.read_text())


def select(value, keys):
    return {k: value[k] for k in keys if k in value}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def git(*args):
    return subprocess.check_output(['git', *args], text=True).strip()


def pinned(path):
    return f'{REPO}/blob/{SOURCE}/{path}'


def artifact(a, run):
    return {**select(a, ['id', 'name', 'expires_at', 'expired']),
            'url': f'{REPO}/actions/runs/{run}/artifacts/{a["id"]}'}


def derived(candidate):
    rows, gaps = [], []
    for r in candidate['repeats']:
        b, counts = r.get('batching_recorded_evidence', {}), r.get('counts', {})
        row = {'repeat': r['repeat']}
        if 'ok' in counts and b.get('produce_requests', 0) > 0:
            row['messages_per_request'] = counts['ok'] / b['produce_requests']
        else:
            gaps.append(f'repeat {r["repeat"]}: messages_per_request unavailable')
        if 'batch_count' in b and 'produce_requests' in b:
            row['batch_count_matches_produce_requests'] = b['batch_count'] == b['produce_requests']
        else:
            gaps.append(f'repeat {r["repeat"]}: batch/request comparison unavailable')
        rows.append(row)
    medians = {}
    for key in MEASURES:
        values = [d[key] if key in d else r.get('batching_recorded_evidence', {}).get(key)
                  for r, d in zip(candidate['repeats'], rows)]
        if all(v is not None for v in values):
            medians[key] = statistics.median(values)
        else:
            gaps.append(f'{key}: five-repeat median unavailable')
    return {'repeats': rows, 'medians': medians, 'gaps': gaps}


def mutation(evidence, run_id, directory):
    run = read(evidence / f'{run_id}-run.json')
    summary_path = evidence / directory / 'summary.json'
    s = read(summary_path)
    assert str(s['run_id']) == str(run_id) and s['commit'] == run['head_sha']
    assert str(s['run_attempt']) == str(run['run_attempt'])
    return {'run_id': run_id, 'run_url': run['html_url'], 'source_commit': s['commit'],
            'status': run['status'], 'conclusion': run['conclusion'],
            'measurement_complete': s['measurement_complete'], 'break_passed': s['break_passed'],
            'summary_sha256': sha(summary_path)}


def publication(e):
    run = read(e / f'{RUN}-run.json')
    assert (run['id'], run['head_sha'], run['run_attempt']) == (RUN, SOURCE, 1)
    assert (run['status'], run['conclusion']) == ('completed', 'success')
    jobs = read(e / f'{RUN}-jobs.json')['jobs']
    assert {j['name']: j['conclusion'] for j in jobs} == {
        'batch': 'success', 'batch-stop': 'success', 'probe': 'skipped', 'full-C0': 'skipped', 'full-C3': 'skipped'}
    for name, step in [('batch', 'Destroy ephemeral infrastructure'), ('batch-stop', 'Stop the cluster at dispatch end')]:
        job = next(j for j in jobs if j['name'] == name)
        assert next(s for s in job['steps'] if s['name'] == step)['conclusion'] == 'success'
    arts = read(e / f'{RUN}-artifacts.json')['artifacts']
    ca = next(a for a in arts if a['id'] == 13353901810)
    pa = next(a for a in arts if a['id'] == 13352834167)
    assert ca['name'] == f'ledger-batch-candidates-{RUN}-1'
    assert pa['name'] == f'ledger-batch-pods-{RUN}-1'
    for a in [ca, pa]:
        assert a['workflow_run']['head_sha'] == SOURCE and a['workflow_run']['id'] == RUN
    result = {'schema_version': 1, 'scenario_id': 'B06', 'synthetic': True,
              'verdict': 'undocumented', 'reason': 'not_in_librdkafka_table', 'not_tested': ['D1'],
              'provenance': {'source_commit': SOURCE, 'run_id': RUN, 'attempt': 1,
                  'run_url': run['html_url'], 'status': run['status'], 'conclusion': run['conclusion'],
                  'artifacts': [artifact(a, RUN) for a in [ca, pa]], 'candidate_members': [],
                  'sources': {k: pinned(p) for k, p in {
                      'scenario': 'scenarios/B06/scenario.yaml', 'expected': 'scenarios/B06/expected.yaml',
                      'profile': 'scenarios/profiles/saturation-ramp.yaml',
                      'verifier_contract': 'docs/operations/verifier-contract.md'}.items()},
                  'jobs': [{**select(j, ['id', 'name', 'status', 'conclusion']),
                            'steps': [select(s, ['name', 'status', 'conclusion']) for s in j['steps']]} for j in jobs]},
              'environment': {'sku': 'Premium', 'processing_units': 1, 'region': 'uksouth',
                  'observed_source': f'{REPO}/actions/runs/{RUN}/job/126703352594',
                  'observed_source_detail': 'Destroy-state log lines 4808-4833: Event Hubs namespace sku, capacity and location.',
                  'node_size': {'value': 'Standard_D4ds_v5',
                      'basis': 'configured at source commit; not independently measured',
                      'source': pinned('infra/cluster/aks.tf')}},
              'candidate_projections': {}, 'derived': {}, 'pod_evidence': [], 'pod_findings': []}
    log = (e / 'infrastructure-log-evidence.txt').read_text()
    assert all(v in log for v in ['Premium', 'capacity', '= 1 -> null', 'uksouth'])
    assert 'Standard_D4ds_v5' in git('show', f'{SOURCE}:infra/cluster/aks.tf')
    for config, reorder in [('C0', 62), ('C1', 11)]:
        p = e / 'candidates' / f'B06-{config}-candidate.json'
        c = read(p)
        assert [c[k] for k in ['scenario_id', 'configuration_id', 'phase', 'source_commit', 'run_id']] == [
            'B06', config, config, SOURCE, f'ledger-{RUN}-1-b06']
        assert c['status'] == 'complete_valid' and c['reasons'] == []
        assert [r['repeat'] for r in c['repeats']] == list(range(1, 6))
        assert all(r['status'] == 'complete_valid' and r['reasons'] == [] for r in c['repeats'])
        for k in COUNTS:
            assert c['counts'][k] == sum(r['counts'][k] for r in c['repeats'])
        assert c['counts'] == dict(zip(COUNTS, [930000, 0, 0, 0, 0, reorder]))
        result['provenance']['candidate_members'].append({'member': p.name, 'sha256': sha(p)})
        result['candidate_projections'][config] = {**select(c, TOP), 'repeats': [select(r, REPEAT) for r in c['repeats']]}
        result['derived'][config] = derived(c)
    phase = read(e / 'pods/phase-status.json')
    assert phase['run_id'] == f'ledger-{RUN}-1' and phase['phase'] == 'ledger-batch'
    modes = [m for m in phase['modes'] if m['item'] == 'B06']
    expected = {(c, r, mode) for c in ['C0', 'C1'] for r in range(1, 6) for mode in ['start-line', 'loadgen', 'verify']}
    expected |= {(c, None, 'judge') for c in ['C0', 'C1']}
    assert len(modes) == 32 and {(m['configuration'], m['repeat'], m['mode']) for m in modes} == expected
    for m in modes:
        job = m['job']
        suffix = f'-r{m["repeat"]}' if m['repeat'] is not None else ''
        assert job == f'ledger-{RUN}-1-b06-{m["configuration"].lower()}{suffix}-{m["mode"]}'
        member = f'{job}/pods.json'
        path = e / 'pods' / member
        if not path.exists():
            result['pod_findings'].append(f'{job}: missing {member}')
            continue
        pods = read(path)['items']
        if not pods:
            result['pod_findings'].append(f'{job}: empty pod list')
        for pod in pods:
            assert any(o['kind'] == 'Job' and o['name'] == job for o in pod['metadata']['ownerReferences'])
            pod_name = pod['metadata']['name']
            statuses = pod.get('status', {}).get('containerStatuses', [])
            if not statuses:
                result['pod_findings'].append(f'{job}: missing container status')
            for s in statuses:
                term = select(s.get('state', {}).get('terminated', {}), ['exitCode', 'reason', 'startedAt', 'finishedAt'])
                log_member = f'{job}/{pod_name}.containers.{s["name"]}.log'
                result['pod_evidence'].append({'configuration': m['configuration'], 'repeat': m['repeat'],
                    'mode': m['mode'], 'job': job, 'pod': pod_name, 'container': s['name'],
                    'termination': term, 'pod_json_member': member, 'log_member': log_member,
                    'artifact_url': artifact(pa, RUN)['url']})
                if term.get('exitCode') != 0:
                    result['pod_findings'].append(f'{job}: nonzero or missing container exit: {term.get("exitCode")}')
                if not (e / 'pods' / log_member).is_file():
                    result['pod_findings'].append(f'{job}: missing {log_member}')
    historical = mutation(e, 41866011149, 'mutation')
    assert historical['source_commit'] == SOURCE
    assert historical['measurement_complete'] is True and historical['break_passed'] is True
    assert historical['status'] == 'completed' and historical['conclusion'] == 'success'
    latest = None
    if (e / 'latest-mutation/summary.json').exists():
        observed = mutation(e, LATEST, 'latest-mutation')
        if observed['measurement_complete'] is True and observed['break_passed'] is True and observed['conclusion'] == 'success' and observed['status'] == 'completed':
            latest = observed
    result['mutation_acceptance'] = {'historical_scenario_source': historical, 'latest_applicable': latest,
        'pending_run_url': f'{REPO}/actions/runs/{LATEST}' if latest is None else None}
    return result


def fmt(value):
    if value is None:
        return 'absent'
    if isinstance(value, bool):
        return str(value).lower()
    return f'{value:.6f}' if isinstance(value, float) else str(value)


def table(headers, rows):
    return '\n'.join(['| ' + ' | '.join(headers) + ' |', '| ' + ' | '.join(['---'] * len(headers)) + ' |'] +
                     ['| ' + ' | '.join(fmt(v) for v in row) + ' |' for row in rows])


def report(x):
    p, cs, ds = x['provenance'], x['candidate_projections'], x['derived']
    sources = p['sources']
    parts = ['# B06: acknowledgement comparison, 2026-11-04',
        "The B06 acknowledgement comparison is undocumented for the librdkafka client under the approved decision rule. Both configurations completed five valid repeats, with batching and application callback latency recorded at the synthetic profile's offered rate. Durability was not tested, and these measurements do not establish maximum throughput.",
        "Workload data is synthetic.",
        f"The [approved rule]({sources['expected']}) gives `undocumented` / `not_in_librdkafka_table`, with `H_throughput` and `H_latency` recorded only and `D1` not tested. The [baseline catalog]({pinned('src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs')}#L107) sets C0 `acks=-1`. The [scenario]({sources['scenario']}) sets C1 `LoadGenProducer.acks=1`; it injects no fault. The [profile]({sources['profile']}) fixes the offered rates.",
        "Microsoft's [Kafka configuration page](https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations), read 2026-11-04, places its `acks` guidance in the Java table and does not list `acks` in the librdkafka section. The [baseline decision](" + pinned('docs/blueprint.md') + "#adr-015-the-baseline-is-microsofts-guidance-every-setting-the-harness-depends-on-explicit-and-tagged-preview-features-never-enter-it) keeps Java labels from applying to the librdkafka client.",
        '## Run and environment', table(['Field', 'Value'], [
            ['Run', f'[{RUN}, attempt {p["attempt"]}]({p["run_url"]})'], ['Source commit', f'`{SOURCE}`'],
            *[[k, v] for k, v in cs['C0']['versions'].items()],
            ['Event Hubs SKU / PU / region', f'{x["environment"]["sku"]} / {x["environment"]["processing_units"]} / {x["environment"]["region"]}'],
            ['Node size', 'Standard_D4ds_v5, configured at source commit; not independently measured']]),
        f"The observed SKU, PU count and region come from the [batch destroy-state log]({x['environment']['observed_source']}), lines 4808-4833. Node size comes from [cluster configuration]({x['environment']['node_size']['source']}). Versions are the candidate values common to both configurations. Image digests are retained in the [JSON publication](2026-11-04.json).",
        '## Outcome counts', 'Every listed repeat and configuration is `complete_valid`, with an empty reasons array. Existing verifier/judge status authorizes these counts under the [run-pinned contract](' + sources['verifier_contract'] + '#c5-phase-blob-and-receipt-contract).',
        table(['Configuration', 'Repeat', *COUNTS], [[c, r['repeat'], *[r['counts'].get(k) for k in COUNTS]] for c in cs for r in cs[c]['repeats']]),
        table(['Configuration', *COUNTS], [[c, *[cs[c]['counts'].get(k) for k in COUNTS]] for c in cs]),
        '## Recorded batching and callback latency',
        'Latency is application callback latency, not wire latency. Values below remain in the recorded microsecond unit. Messages/request is `ok / produce_requests`, displayed to six decimal places; the JSON retains the computed value.',
        table(['Configuration', 'Repeat', 'Latency count', 'Minimum us', 'p50 us', 'p95 us', 'Maximum us', 'Produce requests', 'Batch count', 'Batch maximum messages', 'Messages/request', 'Batch count equals requests'],
            [[c, r['repeat'], *[r.get('batching_recorded_evidence', {}).get(k) for k in ['delivered_latency_count', 'latency_min_us', 'latency_p50_us', 'latency_p95_us', 'latency_max_us', 'produce_requests', 'batch_count', 'batch_max_messages']], d.get('messages_per_request'), d.get('batch_count_matches_produce_requests')]
             for c in cs for r, d in zip(cs[c]['repeats'], ds[c]['repeats'])]),
        'Five-repeat medians, computed with Python `statistics.median`:',
        table(['Configuration', *MEASURES], [[c, *[ds[c]['medians'].get(k) for k in MEASURES]] for c in cs]),
        'Batch count equals Produce requests in every observed repeat. Equality does not independently prove complete batch-window coverage. No missing optional measurement is replaced with zero.',
        'Measurement gaps: ' + ('; '.join(g for c in ds for g in ds[c]['gaps']) or 'none in the displayed fields.'),
        '## Sampled broker connections',
        'These are LoadGenProducer statistics counts. Each is a lower bound from sampled reports, not a complete inventory of simultaneous physical connections.',
        table(['Configuration', 'Repeat', 'Maximum', 'First post-drain', 'Lower bound'],
              [[c, r['repeat'], *[r.get('broker_count', {}).get(k) for k in ['maximum', 'first_post_drain', 'lower_bound']]] for c in cs for r in cs[c]['repeats']]),
        '## Timestamps and candidate provenance',
        table(['Configuration', 'Worker started UTC', 'Judge completed UTC', 'Duration s'],
              [[c, *[cs[c]['timings'][k] for k in ['worker_started_utc', 'judge_completed_utc', 'duration_seconds']]] for c in cs]),
        table(['Candidate member', 'SHA-256'], [[f'`{m["member"]}`', f'`{m["sha256"]}`'] for m in p['candidate_members']]),
        table(['Artifact', 'ID', 'Expiry UTC'], [[f'[{a["name"]}]({a["url"]})', a['id'], a['expires_at']] for a in p['artifacts']]),
        '## Pod evidence',
        'Paths below are relative to the [pod artifact](' + p['artifacts'][1]['url'] + '). The JSON records each full Job/pod/container name and artifact URL. For each row, the JSON member is `<Job>/pods.json` and the log member is `<Job>/<pod>.containers.<container>.log`. Logs and exits are supporting evidence; they do not replace authoritative candidate counts.',
        table(['Configuration', 'Repeat', 'Mode', 'Job', 'Pod', 'Container', 'Exit', 'Reason', 'Started UTC', 'Finished UTC'],
              [[r['configuration'], r['repeat'] if r['repeat'] is not None else 'judge', r['mode'], f'`{r["job"]}`', f'`{r["pod"]}`', r['container'], *[r['termination'].get(k) for k in ['exitCode', 'reason', 'startedAt', 'finishedAt']]] for r in x['pod_evidence']]),
        'Pod findings: ' + ('; '.join(x['pod_findings']) or 'none; all indexed containers terminated with exit code 0 and reason Completed.'),
        '## Mutation acceptance and workflow completion']
    for label, m in [('Historical scenario-source pass', x['mutation_acceptance']['historical_scenario_source']), ('Latest applicable acceptance', x['mutation_acceptance']['latest_applicable'])]:
        if m is None:
            parts.append(f"{label}: pending [run {LATEST}]({x['mutation_acceptance']['pending_run_url']}). Publication remains blocked until the latest applicable measurement passes and commit ancestry is verified. The historical pass does not satisfy that gate.")
        else:
            parts.append(f"{label}: [run {m['run_id']}]({m['run_url']}) at `{m['source_commit']}`, Actions `{m['conclusion']}`, `measurement_complete={str(m['measurement_complete']).lower()}`, `break_passed={str(m['break_passed']).lower()}`.")
    parts += [table(['Job', 'Status', 'Conclusion'], [[j['name'], j['status'], j['conclusion']] for j in p['jobs']]),
        'The batch destroy step and the batch-stop dispatch-end cluster stop succeeded. Probe, full-C0 and full-C3 were skipped; their absence is not a successful run of those jobs.',
        '## Findings and limits',
        f"Recorded reorders were C0={cs['C0']['counts']['reorder']} and C1={cs['C1']['counts']['reorder']}. They remain findings in the evidence, not a reason to alter the expected outcome. No durability fault was injected, so zero observed loss does not establish a durability advantage or guarantee. The fixed offered rate does not measure maximum throughput. There is no separately published configuration-fidelity measurement. The [verifier contract]({sources['verifier_contract']}#c5-phase-blob-and-receipt-contract) defines the scope of the checked observations."]
    return '\n\n'.join(parts) + '\n'


def acceptance(x):
    m = x['mutation_acceptance']['latest_applicable']
    assert m is not None, 'latest applicable mutation acceptance is pending'
    measured = m['source_commit']
    subprocess.run(['git', 'merge-base', '--is-ancestor', measured, 'origin/main'], check=True)
    cfg_path = 'tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json'
    cfg = json.loads(git('show', f'origin/main:{cfg_path}'))['stryker-config']
    paths = ['src/Parchwell.Verifier/' + p.removeprefix('**/') for p in cfg['mutate']]
    assert not git('diff', '--name-only', measured, 'origin/main', '--', cfg_path, *paths), 'new configured target changes require newer mutation acceptance'
    last = git('log', '-1', '--format=%H', 'origin/main', '--', cfg_path, *paths)
    subprocess.run(['git', 'merge-base', '--is-ancestor', last, measured], check=True)


def main():
    a = argparse.ArgumentParser()
    a.add_argument('--evidence', required=True, type=Path)
    a.add_argument('--result', required=True, type=Path)
    a.add_argument('--report', required=True, type=Path)
    a.add_argument('--write', action='store_true', help='Render publication files; does not certify acceptance.')
    args = a.parse_args()
    expected = publication(args.evidence)
    if args.write:
        args.result.parent.mkdir(parents=True, exist_ok=True)
        args.result.write_text(json.dumps(expected, indent=2) + '\n')
        args.report.write_text(report(expected))
        print('B06 draft rendered; publication acceptance not certified')
        return
    actual = read(args.result)
    assert actual == expected, 'publication differs from selected evidence or derived values'
    assert args.report.read_text() == report(actual), 'report tables, links or prose differ from publication rendering'
    changed = set(git('diff', '--name-only', BASE).splitlines()) | set(git('ls-files', '--others', '--exclude-standard').splitlines())
    assert changed == FILES, f'unexpected publication scope: {changed}'
    subprocess.run(['git', 'diff', '--check'], check=True)
    print('B06 source, arithmetic, pod, manuscript and scope checks passed')
    acceptance(actual)
    print('B06 publication checks passed')


if __name__ == '__main__':
    main()
```
