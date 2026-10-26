#!/usr/bin/env bash
# Publishes the LoadGen and Verifier container images with the repository's
# locked linux-x64 restore, then starts each image once with --image-smoke.
# A base image that lacks a shared framework the app needs fails here, before
# any cloud run (issue #179). Needs .NET SDK 10.0.402 and a running Docker
# daemon; takes no arguments. Rules: docs/engineering/dotnet-verification.md,
# "Worker image smoke".
set -euo pipefail

repository_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repository_root"
sdk_version=$(dotnet --version)
if [[ "$sdk_version" != "10.0.402" ]]; then
    echo "Expected .NET SDK 10.0.402; selected $sdk_version." >&2
    exit 1
fi
if ! docker version --format '{{.Server.Version}}' >/dev/null; then
    echo "Docker daemon is not reachable." >&2
    exit 1
fi

work=$(mktemp -d "${TMPDIR:-/tmp}/parchwell-worker-images.XXXXXXXX")
tag="smoke-$$"
containers=()
images=()
cleanup() {
    for container in "${containers[@]}"; do docker rm --force "$container" >/dev/null 2>&1 || true; done
    for image in "${images[@]}"; do docker image rm --force "$image" >/dev/null 2>&1 || true; done
    echo "Worker image smoke logs: $work"
}
trap cleanup EXIT

expected='{"native_loaded":true,"scenario_files":3,"uid_expected":1654}'
for project in LoadGen Verifier; do
    worker=${project,,}
    archive="$work/$worker.tar.gz"
    if ! dotnet publish "src/Parchwell.$project/Parchwell.$project.csproj" --configuration Release -r linux-x64 --self-contained false \
        -t:PublishContainer -p:RestoreLockedMode=true -p:NuGetLockFilePath=packages.linux-x64.lock.json \
        "-p:ContainerImageTag=$tag" "-p:ContainerArchiveOutputPath=$archive" >"$work/$worker-publish.log" 2>&1; then
        echo "$project image publish failed." >&2
        tail -n 80 "$work/$worker-publish.log" >&2
        exit 1
    fi
    if ! docker load --input "$archive" >"$work/$worker-load.log" 2>&1; then
        echo "$project image load failed." >&2
        cat "$work/$worker-load.log" >&2
        exit 1
    fi
    image="parchwell/$worker:$tag"
    images+=("$image")
    container=$(docker create --network none --read-only --user 1654:1654 --cap-drop ALL \
        --security-opt no-new-privileges "$image" --image-smoke)
    containers+=("$container")
    docker start --attach "$container" >"$work/$worker-smoke.stdout" 2>"$work/$worker-smoke.stderr" || true
    exit_code=$(docker container inspect --format '{{.State.ExitCode}}' "$container")
    stdout=$(<"$work/$worker-smoke.stdout")
    if [[ "$exit_code" != "0" || "$stdout" != "$expected" ]]; then
        echo "$project image smoke failed: exit $exit_code." >&2
        cat "$work/$worker-smoke.stdout" "$work/$worker-smoke.stderr" >&2
        exit 1
    fi
    echo "$project image smoke: passed"
done
