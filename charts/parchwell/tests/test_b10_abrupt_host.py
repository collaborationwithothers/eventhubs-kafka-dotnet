#!/usr/bin/env python3
"""Published-host admission and the mandatory production-byte Python bridge."""
import sys

from test_chart import ROOT, with_case
from test_b10_abrupt_chart import diagnostic_cases
from test_loadgen_host import bounded, rendered_environment, host_environment


def require_file(path, reason):
    assert path.is_file(), reason + ':' + str(path)


def b10_abrupt_host_acceptance(td, publish):
    binary = publish / 'Parchwell.LoadGen.dll'
    require_file(binary, 'b10_abrupt_host_binary_missing')
    command = ['dotnet', str(binary), '--validate-b10-abrupt-configuration']
    environments = []
    for name, values in zip(('b10-abrupt-first', 'b10-abrupt-successor'), diagnostic_cases(), strict=True):
        source = td / (name + '.yaml')
        with_case(values, source)
        rendered = rendered_environment(source, td, name)
        require_file(td / (name + '.rendered.yaml'), 'b10_abrupt_render_missing')
        environments.append(rendered)
        output = td / (name + '.host.out')
        proc = bounded(60, command, output, host_environment(rendered, td))
        require_file(output, 'b10_abrupt_host_output_missing')
        assert proc.returncode == 0, (name, proc.returncode, proc.stderr)
        assert output.read_bytes() == b'{"b10_abrupt_configuration_valid":true}\n', output.read_bytes()
    first, successor = environments
    invalid = (
        ('b10-abrupt-first-missing-role', first, 'Role', None),
        ('b10-abrupt-first-unknown', first, 'UnsupportedSetting', 'forbidden'),
        ('b10-abrupt-first-repeat', first, 'Repeat', '2'),
        ('b10-abrupt-successor-missing-predecessor', successor, 'PredecessorBindingPath', None),
        ('b10-abrupt-successor-unknown', successor, 'UnsupportedSetting', 'forbidden'),
    )
    for name, original, key, value in invalid:
        rendered = dict(original)
        if value is None:
            del rendered['B10Abrupt__' + key]
        else:
            rendered['B10Abrupt__' + key] = value
        output = td / (name + '.host.out')
        proc = bounded(60, command, output, host_environment(rendered, td))
        require_file(output, 'b10_abrupt_host_output_missing')
        assert proc.returncode == 1 and 'b10_abrupt_configuration_invalid' in proc.stderr, (name, proc.returncode, proc.stderr)
    output = td / 'b10-abrupt-build.out'
    proc = bounded(600, ['dotnet', 'build', 'tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj',
                         '--configuration', 'Release', '-p:RestoreLockedMode=true'], output)
    require_file(output, 'b10_abrupt_build_output_missing')
    assert proc.returncode == 0, output.read_text()[-4000:] + proc.stderr[-4000:]
    assembly = ROOT / 'tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll'
    require_file(assembly, 'loadgen_test_assembly_missing')
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
    print('b10 abrupt host checks passed: 2 rendered environments, 5 rejected inputs, production-byte bridge')
