#!/bin/bash
set -euo pipefail
trap 'printf "%s:%s: exit %s\n" "${0##*/}" "$LINENO" "$?" >&2' ERR
umask 077
script_directory=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
python3 - "$script_directory" "$@" <<'PY'
import hashlib
import os
from pathlib import Path
import platform
import re
import shutil
import stat
import subprocess
import sys
import tarfile
import tempfile
import zipfile

# SHA-256 provenance and supported artifact names are fixed by the C2 plan.
TOOLS = (
    ('terraform', '1.16.6', 'https://releases.hashicorp.com/terraform/1.16.6/terraform_1.16.6_linux_amd64.zip', '38034b38d03096399a7a1264a14561a8029b2b496fd1f169452656e53968f1b7', 'terraform', ['version'], r'^Terraform v1\.16\.6(?:\s|$)'),
    ('helm', '4.3.1', 'https://get.helm.sh/helm-v4.3.1-linux-amd64.tar.gz', '0403a88ae20913d11b10a2467b50087721fea7581736da6b9d3992f5357fe54b', 'linux-amd64/helm', ['version', '--short'], r'^v4\.3\.1(?:\+|\s|$)'),
    ('actionlint', '1.7.12', 'https://github.com/rhysd/actionlint/releases/download/v1.7.12/actionlint_1.7.12_linux_amd64.tar.gz', '8aca8db96f1b94770f1b0d72b6dddcb1ebb8123cb3712530b08cc387b349a3d8', 'actionlint', ['-version'], r'^1\.7\.12(?:\s|$)'),
    ('shellcheck', '0.11.0', 'https://github.com/koalaman/shellcheck/releases/download/v0.11.0/shellcheck-v0.11.0.linux.x86_64.tar.xz', '8c3be12b05d5c177a04c29e3c78ce89ac86f1595681cab149b65b97c4e227198', 'shellcheck-v0.11.0/shellcheck', ['--version'], r'^version: 0\.11\.0$'),
    ('kubeconform', '0.8.0', 'https://github.com/yannh/kubeconform/releases/download/v0.8.0/kubeconform-linux-amd64.tar.gz', '9bc2bffbf71f261128533edaf912153948b7ff238f9a531ae6d34466ec287883', 'kubeconform', ['-v'], r'^v?0\.8\.0(?:\s|$)'),
    ('tflint', '0.65.0', 'https://github.com/terraform-linters/tflint/releases/download/v0.65.0/tflint_linux_amd64.zip', 'd4c545ed2e29d6338604b586f34f544f380f70f243aa72f7b484713835eaec61', 'tflint', ['--version'], r'^TFLint version 0\.65\.0(?:\s|$)'),
    ('kubectl', '1.36.5', 'https://dl.k8s.io/release/v1.36.5/bin/linux/amd64/kubectl', '33bc88a24c3b09cf55bfd59e6ca977881b61fc3f25ab65bc80c65b430248648e', None, ['version', '--client=true', '--output=json'], r'"gitVersion":\s*"v1\.36\.5"'),
    ('kubelogin', '0.2.21', 'https://github.com/Azure/kubelogin/releases/download/v0.2.21/kubelogin-linux-amd64.zip', '934a2a9618e018cb9fbeb14f3e4c28416fdae815df7706df91500e41d234eb66', 'bin/linux_amd64/kubelogin', ['--version'], r'^git hash: v0\.2\.21/[0-9a-f]{40}$'),
)
WHEEL_URL = 'https://files.pythonhosted.org/packages/8b/9d/b3589d3877982d4f2329302ef98a8026e7f4443c765c46cfecc8858c6b4b/pyyaml-6.0.3-cp312-cp312-manylinux2014_x86_64.manylinux_2_17_x86_64.manylinux_2_28_x86_64.whl'
WHEEL_SHA = 'ba1cc08a7ccde2d2ec775841541641e4548226580ab850948cbfda66a1befcdc'
LIMIT = 128 * 1024 * 1024


def prerequisites(mode):
    if sys.version_info < (3, 10):
        raise ValueError('Python 3.10+ is required')
    if platform.system() != 'Linux' or platform.machine() != 'x86_64':
        raise ValueError('installer supports Linux x86_64 only; supply pinned Mac prerequisites locally')
    if mode == 'ci' and (platform.python_implementation() != 'CPython' or sys.version_info[:2] != (3, 12)):
        raise ValueError('PyYAML wheel requires CPython 3.12')


def run(helper, seconds, argv, output=None, limit=LIMIT):
    command = [sys.executable, str(helper), str(seconds)]
    if output is not None:
        command += ['--stdout-file', str(output), '--max-bytes', str(limit)]
    subprocess.run(command + ['--'] + [str(arg) for arg in argv], check=True)


def verify(path, digest):
    with path.open('rb') as stream:
        actual = hashlib.file_digest(stream, 'sha256').hexdigest() if sys.version_info >= (3, 11) else hashlib.sha256(stream.read(LIMIT + 1)).hexdigest()
    if path.stat().st_size > LIMIT or actual != digest:
        raise ValueError(f'SHA-256 mismatch or oversized artifact: {path.name}')


def download(helper, url, digest, temporary):
    path = temporary / url.rsplit('/', 1)[-1]
    run(helper, 120, ['curl', '--fail', '--silent', '--show-error', '--location', '--max-time', '120', '--max-filesize', str(LIMIT), url], path)
    verify(path, digest)
    return path


def extract(artifact, member, target):
    if member is None:
        shutil.copyfile(artifact, target)
    elif artifact.suffix == '.zip':
        with zipfile.ZipFile(artifact) as archive:
            matches = [item for item in archive.infolist() if item.filename == member]
            if len(matches) != 1 or matches[0].is_dir():
                raise ValueError(f'missing/duplicate/nonregular archive member: {member}')
            info = matches[0]
            kind = stat.S_IFMT(info.external_attr >> 16)
            if kind not in (0, stat.S_IFREG) or info.file_size > LIMIT:
                raise ValueError(f'nonregular/oversized archive member: {member}')
            with archive.open(info) as source, target.open('wb') as destination:
                shutil.copyfileobj(source, destination)
    else:
        with tarfile.open(artifact) as archive:
            matches = [item for item in archive.getmembers() if item.name == member]
            if len(matches) != 1 or not matches[0].isfile() or matches[0].size > LIMIT:
                raise ValueError(f'missing/duplicate/nonregular/oversized archive member: {member}')
            with archive.extractfile(matches[0]) as source, target.open('wb') as destination:
                shutil.copyfileobj(source, destination)
    target.chmod(0o700)


def check_version(tool, text):
    if not re.search(tool[6], text, re.MULTILINE):
        raise ValueError(f'{tool[0]} did not report pinned version {tool[1]}')
    print(text, end='' if text.endswith('\n') else '\n')


def main():
    if len(sys.argv) != 4 or sys.argv[3] not in ('ci', 'live'):
        raise ValueError('usage: install-tools.sh DEST ci|live')
    mode = sys.argv[3]
    prerequisites(mode)
    for executable in ('curl',):
        if shutil.which(executable) is None:
            raise ValueError(f'missing executable: {executable}')
    destination = Path(sys.argv[2]).resolve()
    destination.mkdir(parents=True, exist_ok=True)
    helper = Path(sys.argv[1]) / 'command.py'
    with tempfile.TemporaryDirectory(prefix='parchwell-tools-') as directory:
        temporary = Path(directory)
        if mode == 'live':
            if shutil.which('az') is None:
                raise ValueError('missing executable: az')
            run(helper, 60, ['az', 'version'])
            run(helper, 60, ['az', 'aks', '--help'])
        selected = [tool for tool in TOOLS if (tool[0] not in ('kubectl', 'kubelogin') if mode == 'ci' else tool[0] in ('terraform', 'helm', 'kubectl', 'kubelogin'))]
        for tool in selected:
            artifact = download(helper, tool[2], tool[3], temporary)
            staged = temporary / ('checked-' + tool[0])
            extract(artifact, tool[4], staged)
            version = temporary / 'version.txt'
            run(helper, 60, [staged] + tool[5], version, 65536)
            check_version(tool, version.read_text())
            shutil.copyfile(staged, destination / tool[0])
            (destination / tool[0]).chmod(0o700)
        if mode == 'ci':
            wheel = download(helper, WHEEL_URL, WHEEL_SHA, temporary)
            run(helper, 120, [sys.executable, '-m', 'venv', destination / 'venv'])
            python = destination / 'venv/bin/python'
            run(helper, 120, [python, '-m', 'pip', 'install', '--no-index', '--no-deps', wheel])
            run(helper, 60, [python, '-c', "import yaml; assert yaml.__version__ == '6.0.3'; print('PyYAML ' + yaml.__version__)"])
    print(f'Installed pinned {mode} tools in {destination}')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, subprocess.CalledProcessError, tarfile.TarError, zipfile.BadZipFile) as error:
        print(f'install-tools: {error}', file=sys.stderr)
        sys.exit(1)
PY
