#!/usr/bin/env bash
set -euo pipefail
if [[ $# != 2 || $1 != --output ]]; then echo 'Usage: verify-a01-deployment.sh --output <directory>' >&2; exit 2; fi
if [[ ${PARCHWELL_DEPLOYMENT_BOUNDED:-} != 1 ]]; then
  exec timeout 600 env PARCHWELL_DEPLOYMENT_BOUNDED=1 "$0" "$@"
fi
repository_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repository_root"
output=$(realpath -m -- "$2")
mkdir -p "$output/wire-fixtures" "$output/tools" "$output/images" "$output/tests"
export PARCHWELL_WIRE_FIXTURE_DIRECTORY="$output/wire-fixtures"
source_commit=$(git rev-parse HEAD)
export PARCHWELL_SOURCE_COMMIT="$source_commit"
# Use the managed local daemon. Preserve registry authentication configuration.
unset DOCKER_HOST DOCKER_CONTEXT DOCKER_TLS DOCKER_TLS_VERIFY DOCKER_CERT_PATH
export DOCKER_HOST=unix:///var/run/docker.sock
mark_assertion() {
  python3 - "$output/assertions.json" "$1" <<'PYMARK'
import json,pathlib,sys
p=pathlib.Path(sys.argv[1]); j=json.loads(p.read_text()) if p.exists() else {'schema_version':1,'synthetic':True,'azure_calls':0,'assertions':{}}
j['assertions'][sys.argv[2]]=True; p.write_text(json.dumps(j,indent=2)+'\n')
PYMARK
}
stage=wire_fixtures
trap 'status=$?; python3 - "$output/assertions.json" "$stage" "$status" <<'"'"'PY'"'"'
import json,pathlib,sys
p=pathlib.Path(sys.argv[1]); j=json.loads(p.read_text()) if p.exists() else {"schema_version":1,"synthetic":True,"azure_calls":0,"assertions":{}}
j["result"]="passed" if sys.argv[3]=="0" else "failed"; j["terminal_stage"]=sys.argv[2]; j["exit_code"]=int(sys.argv[3]); p.write_text(json.dumps(j,indent=2)+"\n")
PY
' EXIT
dotnet test --project tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj --configuration Release --no-build \
  --minimum-expected-tests 1 --results-directory "$output/tests" -- \
  --filter-class '*WorkerWireFixtureTests' --report-xunit-xml --report-xunit-xml-filename results.xml >"$output/tests/worker-wire.log" 2>&1
python3 - "$output/tests/results.xml" "$output/wire-fixtures/wire-manifest.json" <<'PY'
import pathlib,sys,xml.etree.ElementTree as ET
root=ET.parse(sys.argv[1]).getroot(); assemblies=root.findall('.//assembly')
assert assemblies and sum(int(a.get('passed','0')) for a in assemblies)>0
assert all(int(a.get('failed','0'))==0 and int(a.get('errors','0'))==0 for a in assemblies)
assert pathlib.Path(sys.argv[2]).is_file()
PY
mark_assertion real_worker_wire_fixture_test_passed
stage=images
[[ $(dotnet --version) == 10.0.402 ]]
dotnet --info >"$output/tools/dotnet.txt"
docker info >"$output/tools/docker-info.txt"
python3 -c 'import yaml; print(yaml.__version__)' >"$output/tools/pyyaml.txt"
for worker in loadgen verifier; do
  if [[ $worker == loadgen ]]; then project=LoadGen; else project=Verifier; fi
  archive="$output/images/$worker.tar.gz"
  dotnet publish "src/Parchwell.$project/Parchwell.$project.csproj" --configuration Release -r linux-x64 --self-contained false \
    -t:PublishContainer -p:RestoreLockedMode=true -p:NuGetLockFilePath=packages.linux-x64.lock.json "-p:ContainerImageTag=$source_commit" "-p:ContainerArchiveOutputPath=$archive" \
    >"$output/images/$worker-publish.log" 2>&1
  sha256sum "$archive" >"$output/images/$worker-archive.sha256"
  docker load --input "$archive" >"$output/images/$worker-load.log" 2>&1
  image="parchwell/$worker:$source_commit"
  docker image inspect "$image" >"$output/images/$worker-inspect.json"
  smoke_id=$(docker create --network none --read-only --user 1654:1654 --cap-drop ALL --security-opt no-new-privileges "$image" --image-smoke)
  docker start --attach "$smoke_id" >"$output/images/$worker-smoke.json" 2>"$output/images/$worker-smoke.stderr"
  docker container inspect --format '{"user":{{json .Config.User}},"network_mode":{{json .HostConfig.NetworkMode}},"read_only":{{json .HostConfig.ReadonlyRootfs}},"cap_drop":{{json .HostConfig.CapDrop}},"security_opt":{{json .HostConfig.SecurityOpt}},"exit_code":{{.State.ExitCode}}}' \
    "$smoke_id" >"$output/images/$worker-container-inspect.json"
  python3 - "$output/images/$worker-inspect.json" "$output/images/$worker-smoke.json" "$output/images/$worker-container-inspect.json" <<'PY'
import json,sys
inspection=json.load(open(sys.argv[1]))[0]; smoke=json.load(open(sys.argv[2])); container=json.load(open(sys.argv[3]))
assert str(inspection['Config']['User']) in ('1654','1654:1654')
assert smoke=={'native_loaded':True,'scenario_files':2,'uid_expected':1654}
assert inspection['Os']=='linux' and inspection['Architecture']=='amd64'
assert container['user']=='1654:1654' and container['network_mode']=='none' and container['read_only'] is True and container['exit_code']==0
assert container['cap_drop']==['ALL'] and 'no-new-privileges' in container['security_opt']
PY
  docker rm "$smoke_id" >"$output/images/$worker-container-cleanup.txt"
  mark_assertion "${worker}_locked_archive_published"
  mark_assertion "${worker}_native_network_none_readonly_uid1654_smoke"
done
# Retain the exact official base index and resolved linux/amd64 manifest/config digests.
curl --fail --silent --show-error --max-time 60 --max-filesize 1048576 \
  -H 'Accept: application/vnd.oci.image.index.v1+json,application/vnd.docker.distribution.manifest.list.v2+json' \
  'https://mcr.microsoft.com/v2/dotnet/runtime/manifests/sha256:939555726cae4c916d212323ac3c0792996dcac2614f00e82cc40fcf059e1af3' -o "$output/tools/base-index.json"
base_manifest=$(python3 - "$output/tools/base-index.json" <<'PYBASE'
import hashlib,json,pathlib,sys
b=pathlib.Path(sys.argv[1]).read_bytes()
assert hashlib.sha256(b).hexdigest()=='939555726cae4c916d212323ac3c0792996dcac2614f00e82cc40fcf059e1af3'
m=[m for m in json.loads(b)['manifests'] if m.get('platform')=={'architecture':'amd64','os':'linux'}]
assert len(m)==1
print(m[0]['digest'])
PYBASE
)
curl --fail --silent --show-error --max-time 60 --max-filesize 1048576 \
  -H 'Accept: application/vnd.oci.image.manifest.v1+json,application/vnd.docker.distribution.manifest.v2+json' \
  "https://mcr.microsoft.com/v2/dotnet/runtime/manifests/$base_manifest" -o "$output/tools/base-manifest.json"
python3 - "$output/tools/base-manifest.json" "$base_manifest" "$output/tools/base-receipt.json" <<'PYBASE'
import hashlib,json,pathlib,sys
b=pathlib.Path(sys.argv[1]).read_bytes(); assert 'sha256:'+hashlib.sha256(b).hexdigest()==sys.argv[2]
pathlib.Path(sys.argv[3]).write_text(json.dumps({'manifest_list_digest':'sha256:939555726cae4c916d212323ac3c0792996dcac2614f00e82cc40fcf059e1af3','linux_amd64_manifest_digest':sys.argv[2],'linux_amd64_config_digest':json.loads(b)['config']['digest']},indent=2)+'\n')
PYBASE
mark_assertion base_image_amd64_manifest_config_verified
stage=verified_helm
helm_archive=${PARCHWELL_HELM_ARCHIVE:-}
if [[ -z $helm_archive || ! -f $helm_archive ]]; then
  echo 'PARCHWELL_HELM_ARCHIVE must name official helm-v4.3.1-linux-amd64.tar.gz; no unverified fallback.' >&2
  exit 1
fi
python3 - "$helm_archive" "$output/tools/helm.json" <<'PY'
import hashlib,json,pathlib,sys
actual=hashlib.sha256(pathlib.Path(sys.argv[1]).read_bytes()).hexdigest()
expected='0403a88ae20913d11b10a2467b50087721fea7581736da6b9d3992f5357fe54b'
receipt={'version':'v4.3.1','source_url':'https://get.helm.sh/helm-v4.3.1-linux-amd64.tar.gz','checksum_source':'https://api.github.com/repos/helm/helm/releases/tags/v4.3.1','expected_sha256':expected,'actual_sha256':actual,'verified':actual==expected}
pathlib.Path(sys.argv[2]).write_text(json.dumps(receipt,indent=2)+'\n')
assert receipt['verified'], 'official Helm archive SHA-256 mismatch'
PY
tar -xzf "$helm_archive" -C "$output/tools" linux-amd64/helm
export PATH="$output/tools/linux-amd64:$PATH"
[[ $(helm version --short) == v4.3.1+* ]]
helm version >"$output/tools/helm-version.txt"
mark_assertion official_helm_archive_verified
stage=generate
python3 - "$output" "$source_commit" "$repository_root" <<'PY'
import json,pathlib,sys
out=pathlib.Path(sys.argv[1]); source=sys.argv[2]; repo=pathlib.Path(sys.argv[3])
# Documentation address ranges and example.invalid are deliberately synthetic fixtures.
for configuration in ('C0','C3'):
 for mode in ('loadgen','start-line','verify'):
  d=out/configuration/mode; d.mkdir(parents=True,exist_ok=True)
  inp={'schema_version':1,'synthetic':True,'source_commit':source,'phase':configuration,'run_id':'synthetic-'+configuration.lower()+'-r1','producer_pod_name':'synthetic-producer-pod','mode':mode,'scenario_root':str(repo),'helm_binary_file':str(out/'tools/linux-amd64/helm'),'limits_file':str(repo/'docs/operations/a01-runtime-limits.json'),'expected_broker_names':['sasl_ssl://broker.example.invalid:9093/0'],'images':{role:{'source_commit':source,'digest':'sha256:'+('a' if role=='loadgen' else 'b')*64} for role in ('loadgen','verifier')},'persistent_outputs':{'acr':{'login_server':'registry.example.invalid'}},'ephemeral_outputs':{'eventhubs':{'namespace_name':'synthetic-ns'},'blob':{'account_url':'https://storage.example.invalid','containers':{'ledger':'ledger','verifier':'verifier'}},'private_endpoint_ips':{'eventhubs':'203.0.113.1','blob':'203.0.113.2'},'workload_identity':{'namespace':'parchwell','service_accounts':{role:{'name':role,'client_id':'synthetic-'+role,'subject':'system:serviceaccount:parchwell:'+role} for role in ('loadgen','enrichment-consumer','verifier')}}}}
  (d/'input.json').write_text(json.dumps(inp,indent=2)+'\n')
PY
for configuration in C0 C3; do
  for mode in loadgen start-line verify; do
    target="$output/$configuration/$mode"
    dotnet run --project src/Parchwell.Runner/Parchwell.Runner.csproj --configuration Release --no-build -- \
      generate --input "$target/input.json" --output "$target" >"$target/generate.log" 2>&1
    stage=helm_render
    helm lint charts/parchwell -f "$target/generated-values.yaml" -f "$target/runtime-values.yaml" >"$target/helm-lint.log" 2>&1
    helm template parchwell charts/parchwell -f "$target/generated-values.yaml" -f "$target/runtime-values.yaml" >"$target/rendered.yaml" 2>"$target/helm-template.stderr"
  done
done
stage=assert_rendered
python3 scripts/verification/assert-a01-deployment.py "$output"
stage=complete
