#!/usr/bin/env bash
set -euo pipefail
if [[ $# != 2 ]]; then
  echo 'Usage: fetch-service-tags.sh <https://download.microsoft.com/...json> <output.json>' >&2
  exit 2
fi
source_url=$1
output_file=$2
python3 - "$source_url" <<'PY'
import sys, urllib.parse
u=urllib.parse.urlsplit(sys.argv[1])
if u.scheme != 'https' or u.hostname != 'download.microsoft.com' or u.username or u.password or u.port not in (None,443) or u.query or u.fragment or not u.path.endswith('.json'):
    raise SystemExit('An explicit official Microsoft downloadable JSON URL is required')
PY
task_temp=$(mktemp -d)
trap 'rm -rf "$task_temp"' EXIT
# A single bounded transfer, without retries. Deployment never calls this script.
curl --fail --silent --show-error --proto '=https' --max-time 60 --max-filesize 16777216 \
  "$source_url" --output "$task_temp/source.json"
python3 - "$source_url" "$task_temp/source.json" "$output_file" <<'PY'
import datetime, hashlib, ipaddress, json, pathlib, sys
url,path,destination=sys.argv[1:]
b=pathlib.Path(path).read_bytes()
if len(b)>16777216: raise SystemExit('service_tags_document_exceeds_byte_limit')
def unique(items):
    result={}
    for key,value in items:
        if key in result: raise ValueError('duplicate JSON property')
        result[key]=value
    return result
root=json.loads(b, object_pairs_hook=unique)
if type(root.get('changeNumber')) is not int: raise SystemExit('root_change_number_required')
tags={}
for name in ('AzureActiveDirectory','AzureMonitor'):
    matches=[t for t in root['values'] if t.get('name')==name]
    if len(matches)!=1: raise SystemExit('exactly_one_global_service_tag_required:'+name)
    props=matches[0]['properties']; raw=props['addressPrefixes']
    if type(props.get('changeNumber')) is not int: raise SystemExit('tag_change_number_required')
    if not isinstance(raw,list) or not raw or len(raw)>4096: raise SystemExit('service_tag_prefix_count_invalid')
    if any(not isinstance(p,str) for p in raw): raise SystemExit('service_tag_prefix_type_invalid')
    for p in raw:
        network=ipaddress.ip_network(p,strict=True)
        if network.prefixlen==0: raise SystemExit('unrestricted_cidr_forbidden')
    prefixes=sorted(set(raw))
    tags[name]={'change_number':props['changeNumber'],'address_prefixes':prefixes}
result={'schema_version':1,'source_url':url,'retrieved_utc':datetime.datetime.now(datetime.timezone.utc).isoformat().replace('+00:00','Z'),
        'source_sha256':hashlib.sha256(b).hexdigest(),'change_number':root['changeNumber'],'tags':tags}
p=pathlib.Path(destination); p.parent.mkdir(parents=True,exist_ok=True)
temp=p.with_name(p.name+'.tmp'); temp.write_text(json.dumps(result,indent=2)+'\n'); temp.replace(p)
PY
