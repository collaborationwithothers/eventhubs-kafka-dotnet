#!/usr/bin/env python3
"""Published-host admission and the mandatory production-byte Python bridge."""
from pathlib import PurePosixPath
import sys

import yaml

from test_chart import ROOT, with_case
from test_b10_abrupt_chart import diagnostic_cases
from test_loadgen_host import bounded, rendered_environment, host_environment


def require_file(path, reason):
    assert path.is_file(), reason + ':' + str(path)


def b10_abrupt_host_acceptance(td, publish):
    binary = publish / 'Parchwell.LoadGen.dll'
    require_file(binary, 'b10_abrupt_host_binary_missing')
    command = ['dotnet', str(binary), '--validate-b10-abrupt-configuration']
    output = td / 'b10-abrupt-build.out'
    proc = bounded(600, ['dotnet', 'build', 'tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj',
                         '--configuration', 'Release', '-p:RestoreLockedMode=true'], output)
    require_file(output, 'b10_abrupt_build_output_missing')
    assert proc.returncode == 0, output.read_text()[-4000:] + proc.stderr[-4000:]
    assembly = ROOT / 'tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll'
    require_file(assembly, 'loadgen_test_assembly_missing')
    environments = []
    for name, values in zip(('b10-abrupt-first', 'b10-abrupt-successor'), diagnostic_cases(), strict=True):
        # Preserve the failed deployment's input shapes without copying its identifiers.
        runtime = values['runtime']
        runtime['run_id'] = 'b10-12345678901-1'
        runtime['loadgen_job_name'] = runtime['run_id'] + '-c1-r1-loadgen'
        runtime['b10_abrupt_binding_configmap'] = runtime['run_id'] + '-b10-abrupt-' + ('first' if name == 'b10-abrupt-first' else 'successor')
        runtime['blob']['account_url'] = 'https://syntheticstorage.blob.core.windows.net'
        source = td / (name + '.yaml')
        with_case(values, source)
        rendered = rendered_environment(source, td, name)
        manifest = td / (name + '.rendered.yaml')
        require_file(manifest, 'b10_abrupt_render_missing')
        job = next(obj for obj in yaml.safe_load_all(manifest.read_text()) if obj and obj['kind'] == 'Job')
        container = job['spec']['template']['spec']['containers'][0]
        # Run the real zero-argument guard before Main, without host startup or Azure.
        # An absent command reproduces the SDK's framework-dependent entrypoint.
        selected = container.get('command', ['dotnet', '/app/Parchwell.LoadGen.dll'])
        selected = [str(publish / PurePosixPath(arg).name) if arg.startswith('/app/') else arg
                    for arg in selected]
        guard_env = host_environment({}, td)
        guard_env['DOTNET_STARTUP_HOOKS'] = str(assembly)
        guard_output = td / (name + '.running-apphost.out')
        guard = bounded(60, selected, guard_output, guard_env)
        assert guard.returncode == 0, (name, guard.returncode, guard.stderr)
        assert guard_output.read_bytes() == b'b10_abrupt_running_apphost_checked\n', guard_output.read_bytes()
        assert container['command'] == ['/app/Parchwell.LoadGen']
        assert container['args'] == ['--b10-abrupt-parent' if name == 'b10-abrupt-first' else '--b10-abrupt-successor']
        apphost = publish / PurePosixPath(container['command'][0]).name
        require_file(apphost, 'b10_abrupt_apphost_missing')
        apphost_output = td / (name + '.apphost.out')
        apphost_proc = bounded(60, [str(apphost), '--validate-b10-abrupt-configuration'],
                               apphost_output, host_environment(rendered, td))
        require_file(apphost_output, 'b10_abrupt_apphost_output_missing')
        assert apphost_proc.returncode == 0, (name, apphost_proc.returncode, apphost_proc.stderr)
        assert apphost_output.read_bytes() == b'{"b10_abrupt_configuration_valid":true}\n', apphost_output.read_bytes()
        environments.append(rendered)
        output = td / (name + '.host.out')
        proc = bounded(60, command, output, host_environment(rendered, td))
        require_file(output, 'b10_abrupt_host_output_missing')
        assert proc.returncode == 0, (name, proc.returncode, proc.stderr)
        assert output.read_bytes() == b'{"b10_abrupt_configuration_valid":true}\n', output.read_bytes()
    first, successor = environments
    invalid = (
        ('b10-abrupt-first-missing-role', first, 'Role', None, 'Role'),
        ('b10-abrupt-first-supervisor', first, 'Role', 'supervisor', 'Role'),
        ('b10-abrupt-first-timeout-job', first, 'JobName', first['B10Abrupt__RunId'] + '-c1-r1-loadgen', 'JobName'),
        ('b10-abrupt-first-unknown', first, 'UnsupportedSetting', 'forbidden', 'SettingNameOrDuplicate'),
        ('b10-abrupt-first-repeat', first, 'Repeat', '2', 'DiagnosticIdentity'),
        ('b10-abrupt-successor-missing-predecessor', successor, 'PredecessorBindingPath', None, 'Binding'),
        ('b10-abrupt-successor-unknown', successor, 'UnsupportedSetting', 'forbidden', 'SettingNameOrDuplicate'),
    )
    for name, original, key, value, detail in invalid:
        rendered = dict(original)
        if value is None:
            del rendered['B10Abrupt__' + key]
        else:
            rendered['B10Abrupt__' + key] = value
        output = td / (name + '.host.out')
        proc = bounded(60, command, output, host_environment(rendered, td))
        require_file(output, 'b10_abrupt_host_output_missing')
        assert proc.returncode == 1 and 'b10_abrupt_configuration_invalid' in proc.stderr, (name, proc.returncode, proc.stderr)
        assert 'validation_detail=' + detail in proc.stderr, (name, proc.stderr)
    apphost_env = host_environment({}, td)
    apphost_env['PARCHWELL_B10_ABRUPT_PUBLISHED_APPHOST'] = str(apphost)
    output = td / 'b10-abrupt-apphost-guard.out'
    proc = bounded(120, ['dotnet', str(assembly), '-class',
                         'Parchwell.LoadGen.Tests.Abrupt.B10AbruptPublishedApphostTests'], output, apphost_env)
    require_file(output, 'b10_abrupt_apphost_guard_output_missing')
    assert proc.returncode == 0, output.read_text()[-4000:] + proc.stderr[-4000:]
    export = td / 'b10-abrupt-wire'
    export.mkdir()
    env = host_environment({}, td)
    env['PARCHWELL_B10_ABRUPT_WIRE_OUTPUT'] = str(export)
    output = td / 'b10-abrupt-wire.out'
    proc = bounded(120, ['dotnet', str(assembly), '-class',
                         'Parchwell.LoadGen.Tests.Abrupt.B10AbruptWireProducerTests'], output, env)
    require_file(output, 'b10_abrupt_wire_output_missing')
    assert proc.returncode == 0, output.read_text()[-4000:] + proc.stderr[-4000:]
    require_file(export / 'export.json', 'b10_abrupt_wire_export_missing')
    output = td / 'b10-abrupt-check.out'
    proc = bounded(120, [sys.executable, 'infra/scripts/phase/tests/check_b10_abrupt_producers.py', str(export)], output)
    require_file(output, 'b10_abrupt_check_output_missing')
    assert proc.returncode == 0, proc.stderr
    assert output.read_bytes() == b'b10_abrupt_produced_bytes_checked\n', output.read_bytes()
    print('b10 abrupt host checks passed: 2 rendered environments, 7 rejected inputs, production-byte bridge')
