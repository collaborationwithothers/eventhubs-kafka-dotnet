# B07 publication receipt

This publication applies the approved batching-delay rule to the existing synthetic run. It preserves the candidate observations and records the accepted latest mutation measurement.

## Execution card

- Deliverable: [report](../../../../results/B07/2026-11-04.md), [structured publication](../../../../results/B07/2026-11-04.json) and [shared item state](../../../../items/381/state.md).
- Authorization: [Revision 1 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/381#issuecomment-6639775016), [standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/381#issuecomment-6639861834) with no blocking or apply-in-implementation finding, [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/381), then [adopted authority](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/381#issuecomment-6639454561). The [tool comment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/381#issuecomment-6639469262) names Codex. The shared state records both approved plans and their checks from the implementation authority snapshot at 2026-11-04T19:20:59Z.
- Source commit: `aca33589a3c69056dcbad3677698a53da7487bf8`; implementation base: `3f4b29e79883ec520eaed78bbf4390b3b7abeb5b`.
- Mandatory gates and earliest publication checkpoint: Enforced checks 1-5 in the plan, latest applicable mutation acceptance and cold review before a non-backup push or verdict pull request. Final-head CI, hosted Codex review and fresh Claude review remain required. Hari merges this verdict; this pull request does not close the shared issue. Each item has an independent verdict pull request targeting main.
- Review budget: zero comprehensive and zero focused passes started at authoring. The cumulative limits and exhaustion action are [pull-request rule 8](../../../agents/pull-requests.md). The root implementation session owns reviews and records complete reports and dispositions in the pull request self-review record; none has been started or claimed here.
- Work bounds: the approved two candidates, ten repeats and 32 phase-status Jobs, named workflow metadata/logs, historical and latest mutation summaries. Temporary validation authoring used one initial pass and one consolidated correction pass to bind the newly preserved run-kind log, candidate identities to loadgen pods, and missing termination fields. One correction pass remains. Changed-file documentation warnings allow 20 dispositions. At exhaustion, push -wip and hand off under the plan; no additional run or investigation is authorized.
- Predicate map: not applicable; no runtime verifier predicate changes. The temporary publication helper applies the existing approved rule and checks the closed publication contract.
- Uncertainty: node size is configured at the source commit, not independently measured. Callback latency is not wire latency, and equality of batch count and request count does not independently prove batch-window coverage.

## Inputs and reproducibility

The [candidate artifact](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41866017867/artifacts/13353901810) and [pod artifact](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41866017867/artifacts/13352834167) are the bounded scenario inputs. The JSON records artifact expiry, candidate member hashes and exact pod JSON/log member paths. The historical [mutation summary artifact](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41866011149/artifacts/13349781411) is distinct from latest applicable acceptance. Raw pod files, logs and command output remain outside git.

Candidate byte hashes:

- `B07-C0-candidate.json`: `343c131ee39fcb362cf292bfad9a53a07249aca4bd42352a58367a754232eb7a`.
- `B07-C1-candidate.json`: `de4e844d2a74432f05a6d9db77244480369b91fc655a580cb737cc74fc35f289`.

Extract the complete source block below to `/tmp/issue-381/verify-b07-publication.py`. It includes generation, validation and the complete report text; no external helper is needed. Run from the repository root with the named candidates in `candidates/`, expanded pod artifact in `pods/`, historical summary in `mutation/summary.json`, and saved run/artifact/job REST responses in `<run-id>-run.json`, `<run-id>-artifacts.json` and `<run-id>-jobs.json` under `/tmp/issue-381`. The sanitized batch log excerpts are `infrastructure-log-evidence.txt` and `run-kind-log-evidence.txt`: job 126703352594 lines 4808-4833 record Premium/capacity 1/uksouth, while lines 55 and 57 record the ledger-batch scenario and batch run kind. These excerpts contain no connection strings or identity identifiers. On acceptance, place the latest summary in `latest-mutation/summary.json` and refresh `41892353617-run.json`.

The source-pinned scenario, expected outcome, profile and verifier contract were read and are linked in the report/JSON. The official Microsoft configuration page, cached during planning and read again on 2026-11-04, contains the unlabelled troubleshooting advice "Reduce linger.ms value (use 0 for lowest latency)"; its higher-throughput recommendation appears in the Java table. The current publication adds no claim about replication or guaranteed durability.

## Verification commands and results

```sh
python /tmp/issue-381/verify-b07-publication.py --evidence /tmp/issue-381 --result results/B07/2026-11-04.json --report results/B07/2026-11-04.md --write
python /tmp/issue-381/verify-b07-publication.py --evidence /tmp/issue-381 --result results/B07/2026-11-04.json --report results/B07/2026-11-04.md
python -m json.tool results/B07/2026-11-04.json >/dev/null
npm run check:docs
git diff --check
git diff --name-only origin/main
```

Rendering writes only the two approved publication files and never certifies acceptance. Validation compares full selected dictionaries, exact ordinals, authorized phase sums, hashes, ratios, medians and manuscript text with the source-derived rendering. It binds phase-status Jobs to pod ownership, candidate producer identities, container terminations and artifact members. Smoke takes precedence, then missing/incomplete configuration, then missing p50 or strict comparisons. For the named candidates it asserts `9155 < 12504` and the recorded reference observations; descriptive medians never decide the verdict. Latest mutation acceptance requires its successful completed Actions run and summary booleans, measured commit ancestry, and coverage of the latest main change to any currently configured target.

Source identity, projection, arithmetic, pod binding, manuscript, four-path scope and latest mutation acceptance passed; the checker printed `B07 publication checks passed`. The accepted [mutation summary](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41892353617/artifacts/13360514326) identifies `3f4b29e79883ec520eaed78bbf4390b3b7abeb5b`, with `measurement_complete=true`, `break_passed=true` and no incomplete reasons. That commit includes the scenario source and the latest configured-target change; main is unchanged since it. JSON parsing, working-tree and staged whitespace checks, full receipt-source equality and byte-identical shared state checks passed. Documentation checks completed with 8 warnings, all in unchanged files and outside this publication scope. No runtime code changed, so no runtime tests were added or repeated.

Cold-review mode: comprehensive, against base `3f4b29e79883ec520eaed78bbf4390b3b7abeb5b`. The frozen input commit is identified by the full SHA in the supplied review-diff filename; the completed report and cumulative counts belong in the pull request self-review record. Final-head CI, hosted Codex review, fresh Claude review and Hari's verdict merge remain outstanding.

## Full authored script

```python
#!/usr/bin/env python3
"""Bounded B07 publication rendering and source comparison; no runtime changes."""
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
MEASURES = 'latency_p50_us produce_requests messages_per_request batch_count batch_max_messages'.split()
FILES = {'results/B07/2026-11-04.json', 'results/B07/2026-11-04.md', 'docs/operations/evidence/issue-381-b07/receipt.md', 'items/381/state.md'}


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
        b, counts = r.get('batching_recorded_evidence') or {}, r.get('counts') or {}
        row = {'repeat': r['repeat']}
        if counts.get('ok') is not None and b.get('produce_requests') is not None and b['produce_requests'] > 0:
            row['messages_per_request'] = counts['ok'] / b['produce_requests']
        else:
            gaps.append(f'repeat {r["repeat"]}: messages_per_request unavailable')
        if b.get('batch_count') is not None and b.get('produce_requests') is not None:
            row['batch_count_matches_produce_requests'] = b['batch_count'] == b['produce_requests']
        else:
            gaps.append(f'repeat {r["repeat"]}: batch/request comparison unavailable')
        rows.append(row)
    medians = {}
    for key in MEASURES:
        values = [d[key] if key in d else (r.get('batching_recorded_evidence') or {}).get(key)
                  for r, d in zip(candidate['repeats'], rows)]
        if all(v is not None for v in values):
            medians[key] = statistics.median(values)
        else:
            gaps.append(f'{key}: five-repeat median unavailable')
    return {'repeats': rows, 'medians': medians, 'gaps': gaps}


def assess(candidates, run_kind):
    # Preserve the expected outcome's precedence before applying strict p50 separation.
    if run_kind == 'smoke':
        return None, 'smoke_only', None, {}
    assert run_kind == 'batch'
    if any(candidates.get(c) is None or candidates[c].get('status') != 'complete_valid' for c in ['C0', 'C1']):
        return None, 'incomplete_configuration', None, {}
    comparison = {}
    for config in ['C0', 'C1']:
        repeats = candidates[config].get('repeats', [])
        if [r['repeat'] for r in repeats] != list(range(1, 6)) or any(r['status'] != 'complete_valid' for r in repeats):
            return 'untestable', None, 'inconclusive', {}
        values = [(r.get('batching_recorded_evidence') or {}).get('latency_p50_us') for r in repeats]
        if any(v is None for v in values):
            return 'untestable', None, 'inconclusive', {}
        comparison[f'{config}_p50_us'] = values
        comparison[f'{config}_min_p50_us'] = min(values)
        comparison[f'{config}_max_p50_us'] = max(values)
    c0, c1 = comparison['C0_p50_us'], comparison['C1_p50_us']
    below, above = max(c1) < min(c0), min(c1) > max(c0)
    comparison.update(all_C1_below_C0=below, all_C0_below_C1=above)
    return ('confirmed' if below else 'contradicted' if above else 'untestable', None,
            'holds' if below else 'contradicted' if above else 'inconclusive', comparison)


def mutation(evidence, run_id, directory):
    run = read(evidence / f'{run_id}-run.json')
    summary_path = evidence / directory / 'summary.json'
    s = read(summary_path)
    assert run['id'] == run_id and run['head_branch'] == 'main'
    assert run['event'] == 'workflow_dispatch' and run['path'] == '.github/workflows/verifier-c5-mutation.yml'
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
    result = {'schema_version': 1, 'scenario_id': 'B07', 'synthetic': True,
              'verdict': None, 'hypothesis_assessment': {},
              'provenance': {'source_commit': SOURCE, 'run_id': RUN, 'attempt': 1,
                  'run_url': run['html_url'], 'status': run['status'], 'conclusion': run['conclusion'],
                  'artifacts': [artifact(a, RUN) for a in [ca, pa]], 'candidate_members': [],
                  'sources': {k: pinned(p) for k, p in {
                      'scenario': 'scenarios/B07/scenario.yaml', 'expected': 'scenarios/B07/expected.yaml',
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
    for config, reorder in [('C0', 1), ('C1', 18)]:
        p = e / 'candidates' / f'B07-{config}-candidate.json'
        c = read(p)
        assert [c[k] for k in ['scenario_id', 'configuration_id', 'phase', 'source_commit', 'run_id']] == [
            'B07', config, config, SOURCE, f'ledger-{RUN}-1-b07']
        assert c['schema_version'] == 1 and c['synthetic'] is True
        assert c['status'] == 'complete_valid' and c['reasons'] == []
        assert [r['repeat'] for r in c['repeats']] == list(range(1, 6))
        assert all(r['status'] == 'complete_valid' and r['reasons'] == [] for r in c['repeats'])
        for k in COUNTS:
            assert c['counts'][k] == sum(r['counts'][k] for r in c['repeats'])
        assert c['counts'] == dict(zip(COUNTS, [930000, 0, 0, 0, 0, reorder]))
        result['provenance']['candidate_members'].append({'member': p.name, 'sha256': sha(p)})
        result['candidate_projections'][config] = {**select(c, TOP), 'repeats': [select(r, REPEAT) for r in c['repeats']]}
        result['derived'][config] = derived(c)
    assert result['candidate_projections']['C0']['versions'] == result['candidate_projections']['C1']['versions']
    phase = read(e / 'pods/phase-status.json')
    assert phase['run_id'] == f'ledger-{RUN}-1' and phase['phase'] == 'ledger-batch'
    modes = [m for m in phase['modes'] if m['item'] == 'B07']
    expected = {(c, r, mode) for c in ['C0', 'C1'] for r in range(1, 6) for mode in ['start-line', 'loadgen', 'verify']}
    expected |= {(c, None, 'judge') for c in ['C0', 'C1']}
    assert len(modes) == 32 and {(m['configuration'], m['repeat'], m['mode']) for m in modes} == expected
    for m in modes:
        job = m['job']
        suffix = f'-r{m["repeat"]}' if m['repeat'] is not None else ''
        assert job == f'ledger-{RUN}-1-b07-{m["configuration"].lower()}{suffix}-{m["mode"]}'
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
            if m['mode'] == 'loadgen':
                repeat = result['candidate_projections'][m['configuration']]['repeats'][m['repeat'] - 1]
                assert repeat['identity'] == pod_name
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
                if any(k not in term for k in ['reason', 'startedAt', 'finishedAt']):
                    result['pod_findings'].append(f'{job}: missing termination reason or timestamps')
                if term.get('exitCode') != 0:
                    result['pod_findings'].append(f'{job}: nonzero or missing container exit: {term.get("exitCode")}')
                if not (e / 'pods' / log_member).is_file():
                    result['pod_findings'].append(f'{job}: missing {log_member}')
    kind_log = (e / 'run-kind-log-evidence.txt').read_text()
    assert 'PARCHWELL_SCENARIO: ledger-batch' in kind_log and 'PARCHWELL_RUN_KIND: batch' in kind_log
    run_kind = 'batch'
    result['provenance']['run_kind'] = run_kind
    result['provenance']['run_kind_basis'] = f'{REPO}/actions/runs/{RUN}/job/126703352594'
    result['provenance']['run_kind_detail'] = 'Batch job log lines 55 and 57: ledger-batch scenario and batch run kind.'
    verdict, reason, hypothesis, comparison = assess(result['candidate_projections'], run_kind)
    assert verdict == 'confirmed' and reason is None and hypothesis == 'holds'
    assert comparison['C0_p50_us'] == [12504, 12771, 12764, 12679, 12904]
    assert comparison['C1_p50_us'] == [9020, 9145, 9069, 9077, 9155]
    assert comparison['C1_max_p50_us'] == 9155 < comparison['C0_min_p50_us'] == 12504
    assert comparison['C1_min_p50_us'] == 9020 and comparison['C0_max_p50_us'] == 12904
    result['verdict'] = verdict
    result['hypothesis_assessment'] = {'H_lowest_latency': hypothesis, 'H_batching': 'recorded_only', 'H_tail': 'recorded_only'}
    result['latency_comparison'] = comparison
    for config, p50, requests, batch_max in [('C0', 12764, 49379, 10), ('C1', 9077, 181711, 5)]:
        medians = result['derived'][config]['medians']
        assert medians['latency_p50_us'] == p50
        assert medians['produce_requests'] == medians['batch_count'] == requests
        assert medians['batch_max_messages'] == batch_max
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
    parts = ['# B07: batching-delay comparison, 2026-11-04',
        "The B07 batching-delay comparison confirms the approved lowest-latency hypothesis for this run. Every repeat with linger.ms set to 0 had a lower p50 application callback latency than every repeat with linger.ms set to 5. Batching and tail latency are recorded at the synthetic profile's offered rate, which does not establish maximum throughput or wire latency.",
        f"The [approved rule]({sources['expected']}) gives `confirmed` when every C1 p50 is below every C0 p50, after smoke and incomplete-configuration precedence. `H_batching` and `H_tail` are recorded only. The [scenario]({sources['scenario']}) uses C0 baseline `linger.ms=5` and C1 `LoadGenProducer.linger.ms=0`, with other settings unchanged and no fault injected. The [profile]({sources['profile']}) fixes the offered rates.",
        "Microsoft's [Kafka configuration page](https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations), read 2026-11-04, gives the unlabelled troubleshooting advice \"Reduce linger.ms value (use 0 for lowest latency)\". The Java table's higher-throughput recommendation does not govern this client under the [baseline decision](" + pinned('docs/blueprint.md') + "#adr-015-the-baseline-is-microsofts-guidance-every-setting-the-harness-depends-on-explicit-and-tagged-preview-features-never-enter-it).",
        '## Run and environment', table(['Field', 'Value'], [
            ['Run', f'[{RUN}, attempt {p["attempt"]}]({p["run_url"]})'], ['Source commit', f'`{SOURCE}`'], ['Run kind', p['run_kind']],
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
            [[c, r['repeat'], *[(r.get('batching_recorded_evidence') or {}).get(k) for k in ['delivered_latency_count', 'latency_min_us', 'latency_p50_us', 'latency_p95_us', 'latency_max_us', 'produce_requests', 'batch_count', 'batch_max_messages']], d.get('messages_per_request'), d.get('batch_count_matches_produce_requests')]
             for c in cs for r, d in zip(cs[c]['repeats'], ds[c]['repeats'])]),
        'Five-repeat medians, computed with Python `statistics.median`:',
        table(['Configuration', *MEASURES], [[c, *[ds[c]['medians'].get(k) for k in MEASURES]] for c in cs]),
        f"Strict separation: `max(C1 p50) = {x['latency_comparison']['C1_max_p50_us']} us < min(C0 p50) = {x['latency_comparison']['C0_min_p50_us']} us`, so `H_lowest_latency` holds and the verdict is `confirmed`. The reverse comparison, `min(C1 p50) = {x['latency_comparison']['C1_min_p50_us']} us > max(C0 p50) = {x['latency_comparison']['C0_max_p50_us']} us`, is false. Medians are descriptive and do not determine the verdict.",
        'Batch count equals Produce requests in every observed repeat. Equality does not independently prove complete batch-window coverage. No missing optional measurement is replaced with zero.',
        'Measurement gaps: ' + ('; '.join(g for c in ds for g in ds[c]['gaps']) or 'none in the displayed fields.'),
        '## Sampled broker connections',
        'These are LoadGenProducer statistics counts. Each is a lower bound from sampled reports, not a complete inventory of simultaneous physical connections.',
        table(['Configuration', 'Repeat', 'Maximum', 'First post-drain', 'Lower bound'],
              [[c, r['repeat'], *[(r.get('broker_count') or {}).get(k) for k in ['maximum', 'first_post_drain', 'lower_bound']]] for c in cs for r in cs[c]['repeats']]),
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
        f"Recorded reorders were C0={cs['C0']['counts']['reorder']} and C1={cs['C1']['counts']['reorder']}. They remain findings in the evidence, not a reason to alter the expected outcome. The fixed offered rate does not measure maximum throughput. There is no separately published configuration-fidelity measurement. The [verifier contract]({sources['verifier_contract']}#c5-phase-blob-and-receipt-contract) defines the scope of the checked observations."]
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
        print('B07 draft rendered; publication acceptance not certified')
        return
    actual = read(args.result)
    assert actual == expected, 'publication differs from selected evidence or derived values'
    assert args.report.read_text() == report(actual), 'report tables, links or prose differ from publication rendering'
    changed = set(git('diff', '--name-only', BASE).splitlines()) | set(git('ls-files', '--others', '--exclude-standard').splitlines())
    assert changed == FILES, f'unexpected publication scope: {changed}'
    subprocess.run(['git', 'diff', '--check'], check=True)
    print('B07 source, arithmetic, pod, manuscript and scope checks passed')
    acceptance(actual)
    print('B07 publication checks passed')


if __name__ == '__main__':
    main()

```
