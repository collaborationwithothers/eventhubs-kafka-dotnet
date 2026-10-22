from pathlib import Path
import urllib.request, hashlib, json
out = Path('/tmp/kql-type-114-source')
sources = [
    ('union-operator.md', 'MicrosoftDocs/dataexplorer-docs', '85351b30116da41b75c5a4e8a5b7f5730d81ab08', 'data-explorer/kusto/query/union-operator.md'),
    ('long.md', 'MicrosoftDocs/dataexplorer-docs', '85351b30116da41b75c5a4e8a5b7f5730d81ab08', 'data-explorer/kusto/query/scalar-data-types/long.md'),
    ('tolong-function.md', 'MicrosoftDocs/dataexplorer-docs', 'ac19a887fb20eab5c08ec7742478621be92c56fd', 'data-explorer/kusto/query/tolong-function.md'),
    ('azmsdiagnosticerrorlogs.md', 'MicrosoftDocs/azure-monitor-docs', '6c8e97c9e8b8e57c19e3b319bda5188e4ceba029', 'articles/azure-monitor/reference/tables/azmsdiagnosticerrorlogs.md'),
    ('azmskafkausererrorlogs.md', 'MicrosoftDocs/azure-monitor-docs', '6c8e97c9e8b8e57c19e3b319bda5188e4ceba029', 'articles/azure-monitor/reference/tables/azmskafkausererrorlogs.md'),
]
for name, repo, sha, path in sources:
    url = f'https://raw.githubusercontent.com/{repo}/{sha}/{path}'
    with urllib.request.urlopen(url, timeout=30) as response:
        data = response.read()
    (out / name).write_bytes(data)
    print(json.dumps({'path': str(out / name), 'url': url, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}))
