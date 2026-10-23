# Budget arithmetic command

The one-off budget calculation executed `python3 /tmp/issue140/budget-arithmetic.py` from the repository root. The source below is the exact temporary script body extracted from approved Revision 2; its output is retained in [raw/budget-arithmetic.txt](raw/budget-arithmetic.txt).

```python
from pathlib import Path
import re
expected = {'probe': (14310, 14400, 90), 'full-C0': (12960, 14400, 1440),
            'full-C3': (12960, 14400, 1440), 'sweep': (3090, 3600, 510)}
for name in ('scenario', 'sweeper'):
    installed = Path(f'.github/workflows/{name}.yml').read_bytes()
    assert installed == Path(f'docs/operations/workflows/{name}.yml.txt').read_bytes()
    text = installed.decode().split('\njobs:\n', 1)[1]
    jobs = list(re.finditer(r'^  ([\w-]+):\n', text, re.M))
    for i, match in enumerate(jobs):
        job = match[1]
        body = text[match.end():jobs[i+1].start() if i+1 < len(jobs) else len(text)]
        shell = sum(map(int, re.findall(r'^\s+timeout (\d+) ', body, re.M)))
        # The fixed loops were read on main and are unchanged by this PR.
        nested = 60 + 2*(600+60) if job == 'probe' else 2*60 if name == 'scenario' else 0
        cap = int(re.search(r'timeout-minutes: (\d+)', body)[1]) * 60
        total = shell + nested
        assert (total, cap, cap-total) == expected[job]
        assert total < cap
        print(job, 'shell', shell, 'nested', nested, 'total', total, 'cap', cap, 'remainder', cap-total)
measured = 13*60+31 + 2*60+59
print('measured_seconds', measured, 'destroy_margin_seconds', 1050-measured,
      'phase_work_seconds', 9750-300-1050, 'cleanup_reserve_seconds', 900+300+1050)
```
