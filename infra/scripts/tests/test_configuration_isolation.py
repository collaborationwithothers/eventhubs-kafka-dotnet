"""Offline fixtures for the namespace lifecycle. No Azure commands are run."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SOURCE = Path(__file__).resolve().parents[1] / "lifecycle" / "configuration-isolation.py"
spec = importlib.util.spec_from_file_location("configuration_isolation", SOURCE)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def change(address, actions, mode="managed"):
    return {"address": address, "mode": mode, "change": {"actions": actions}}


def valid_plan(extra):
    return {"resource_changes": [change(address, ["no-op"]) for address in module.SHARED_ADDRESSES] + extra}


def output(enabled, name="eh-old"):
    prefix = "/subscriptions/synthetic/resourceGroups/ephemeral/providers/"
    account = prefix + "Microsoft.Storage/storageAccounts/account"
    life = {"eventhubs_enabled": enabled, "storage_account_resource_id": account, "blob_private_endpoint_resource_id": prefix + "Microsoft.Network/privateEndpoints/blob", "container_resource_ids": {x: account + "/blobServices/default/containers/" + x for x in ("ledger", "verifier", "claim_check", "single_action")}, "workspace_resource_id": prefix + "Microsoft.OperationalInsights/workspaces/law", "blob_diagnostic_target_resource_id": account + "/blobServices/default", "namespace_resource_id": prefix + "Microsoft.EventHub/namespaces/" + name if enabled else None, "eventhubs_private_endpoint_resource_id": prefix + "Microsoft.Network/privateEndpoints/pe-" + name if enabled else None}
    data = {"configuration_lifecycle": {"value": life}, "blob": {"value": {"account_url": "https://account.blob.core.windows.net", "containers": {"ledger": "ledger"}}}, "private_endpoint_ips": {"value": {"blob": "10.0.0.1", "eventhubs": "10.0.0.2" if enabled else None}}}
    if enabled:
        data["eventhubs"] = {"value": {"namespace_name": name, "bootstrap_servers": name + ".servicebus.windows.net:9093"}}
        data["probe_v2_role_scope"] = {"value": {}}
    return data


class SyntheticLifecycle:
    def __init__(self, path, results):
        self.private = path
        self.results = list(results)
        self.calls = []
        self.variables = path / "inputs.tfvars.json"
        self.variables.write_text('{}')

    def backend(self, root):
        return None, self.variables, "key", "other"

    def read_json(self, path):
        return json.loads(path.read_text())

    def run(self, args, seconds, output=None, **kwargs):
        self.calls.append(args)
        result = self.results.pop(0)
        if output is not None:
            output.write_text(result.get("stdout", ""))
        return result.get("status", 0), result.get("stderr", b"")


class ConfigurationIsolationTests(unittest.TestCase):
    def test_namespace_name_vectors(self):
        repo = "collaborationwithothers/eventhubs-kafka-dotnet"
        self.assertEqual("ehp-8651ab1d805edb095360115f", module.namespace_name(repo, "43686555201", "1", "C0"))
        self.assertEqual("ehp-39836678d093a66a4994757a", module.namespace_name(repo, "43686555201", "1", "C1"))
        self.assertNotEqual(module.namespace_name(repo, "43686555201", "1", "C0"), module.namespace_name(repo, "43686555201", "2", "C0"))
        for invalid in (("bad repository", "1", "1", "C0"), (repo, "0", "1", "C0"), (repo, "1", "x", "C0"), (repo, "1", "1", "C2")):
            with self.subTest(invalid=invalid), self.assertRaises(module.TransitionError):
                module.namespace_name(*invalid)

    def test_transition_rejects_shared_changes(self):
        for address in ('module.storage.azapi_resource.this', 'module.storage.module.containers["ledger"].azapi_resource.this', 'module.storage.module.private_endpoints["blob"].azapi_resource.this', 'module.storage.module.private_endpoints["blob"].azapi_resource.private_dns_zone_group[0]', 'azurerm_role_assignment.workload["loadgen_ledger_contributor"]', 'azurerm_unexpected.resource'):
            for actions in (["create"], ["update"], ["delete"], ["delete", "create"], ["create", "delete"]):
                with self.subTest(address=address, actions=actions), self.assertRaises(module.TransitionError) as caught:
                    module.validate_plan(valid_plan([change(address, actions)]) if address == "azurerm_unexpected.resource" else {"resource_changes": [change(a, actions if a == address else ["no-op"]) for a in module.SHARED_ADDRESSES]})
                self.assertEqual((2, "namespace_transition_plan_rejected"), (caught.exception.code, str(caught.exception)))
        for invalid in ({}, {"resource_changes": []}, {"resource_changes": [{"address": "x"}]}):
            with self.assertRaises(module.TransitionError):
                module.validate_plan(invalid)
        module.validate_plan(valid_plan([change('module.eventhubs[0].azurerm_eventhub_namespace.this[0]', ["delete"]), change('data.azurerm_private_endpoint_connection.blob', ["read"], "data")]))

    def test_transition_rejects_probe_namespace_roles(self):
        for name in (None, "eh-new"):
            with self.subTest(name=name), tempfile.TemporaryDirectory() as directory:
                lifecycle = SyntheticLifecycle(Path(directory), [])
                lifecycle.variables.write_text('{"probe_namespace_data_roles": true}')
                with patch.dict('os.environ', {"PARCHWELL_RUN_ID": "run-id"}), self.assertRaises(module.TransitionError) as caught:
                    module.transition(lifecycle, name)
                self.assertEqual((2, "namespace_transition_input_rejected"), (caught.exception.code, str(caught.exception)))
                self.assertEqual([], lifecycle.calls)

    def test_off_readback_is_not_absence_on_error(self):
        for state, arm in (("", {}), ("azurerm_resource_group.ephemeral\nmodule.storage.account[0]\n", {"status": 1, "stderr": b"ERROR: (AuthorizationFailed) forbidden"}), ("azurerm_resource_group.ephemeral\nmodule.storage.account[0]\n", {"status": 124}), ('azurerm_resource_group.ephemeral\nmodule.storage.account[0]\nmodule.eventhubs[0].azurerm_eventhub_namespace.this[0]\n', {}), ('azurerm_resource_group.ephemeral\nmodule.storage.account[0]\nmodule.eventhubs[0].azurerm_private_endpoint.this[0]\n', {})):
            with self.subTest(state=state, arm=arm), tempfile.TemporaryDirectory() as directory:
                path = Path(directory)
                (path / "ephemeral-output.json").write_text(json.dumps(output(True)))
                plan = json.dumps(valid_plan([change('module.eventhubs[0].azurerm_eventhub_namespace.this[0]', ["delete"])]))
                lifecycle = SyntheticLifecycle(path, [{}, {"stdout": plan}, {}, {"stdout": json.dumps(output(False))}, {"stdout": state}, arm])
                with patch.dict('os.environ', {"PARCHWELL_RUN_ID": "run-id"}), self.assertRaises(module.TransitionError) as caught:
                    module.transition(lifecycle)
                self.assertEqual((1, "namespace_removal_unconfirmed"), (caught.exception.code, str(caught.exception)))
                self.assertFalse((path / "namespace-transition.json").exists())

    def test_off_404_confirms_and_rejected_plan_never_applies(self):
        for broken in (False, True):
            with self.subTest(broken=broken), tempfile.TemporaryDirectory() as directory:
                path = Path(directory)
                (path / "ephemeral-output.json").write_text(json.dumps(output(True)))
                plan = valid_plan([change('module.eventhubs[0].azurerm_eventhub_namespace.this[0]', ["delete"])])
                if broken:
                    plan["resource_changes"][0]["change"]["actions"] = ["update"]
                absence = {"status": 1, "stderr": b"ERROR: (ResourceNotFound) absent"}
                state = "azurerm_resource_group.ephemeral\nmodule.storage.azapi_resource.this\n"
                lifecycle = SyntheticLifecycle(path, [{}, {"stdout": json.dumps(plan)}, {}, {"stdout": json.dumps(output(False))}, {"stdout": state}, absence, absence])
                with patch.dict('os.environ', {"PARCHWELL_RUN_ID": "run-id"}):
                    if broken:
                        with self.assertRaises(module.TransitionError) as caught:
                            module.transition(lifecycle)
                        self.assertEqual((2, "namespace_transition_plan_rejected"), (caught.exception.code, str(caught.exception)))
                        self.assertFalse(any("apply" in call for call in lifecycle.calls))
                    else:
                        module.transition(lifecycle)
                        self.assertTrue((path / "namespace-transition.json").exists())

    def test_invalid_arm_id_rejected_before_apply(self):
        for key in ("namespace_resource_id", "eventhubs_private_endpoint_resource_id", "storage_account_resource_id", "blob_private_endpoint_resource_id", "workspace_resource_id"):
            with self.subTest(key=key), tempfile.TemporaryDirectory() as directory:
                path = Path(directory)
                active = output(True)
                active["configuration_lifecycle"]["value"][key] = "/ambiguous"
                (path / "ephemeral-output.json").write_text(json.dumps(active))
                lifecycle = SyntheticLifecycle(path, [])
                with patch.dict('os.environ', {"PARCHWELL_RUN_ID": "run-id"}), self.assertRaises(module.TransitionError):
                    module.transition(lifecycle)
                self.assertEqual([], lifecycle.calls)

    def test_activation_refreshes_only_eventhubs(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            old, off, new = output(True), output(False), output(True, "eh-new")
            (path / "ephemeral-output.json").write_text(json.dumps(off))
            (path / "namespace-transition.json").write_text(json.dumps({"initial": old, "old_ids": [old["configuration_lifecycle"]["value"]["namespace_resource_id"], old["configuration_lifecycle"]["value"]["eventhubs_private_endpoint_resource_id"]]}))
            absent = {"status": 1, "stderr": b"ERROR: (ResourceNotFound) gone"}
            plan = json.dumps(valid_plan([change('module.eventhubs[0].azurerm_eventhub_namespace.this[0]', ["create"])]))
            lifecycle = SyntheticLifecycle(path, [{"stdout": "azurerm_resource_group.ephemeral\nmodule.storage.account[0]\n"}, absent, absent, {}, {"stdout": plan}, {}, {"stdout": json.dumps(new)}, {"stdout": "10.0.0.2 STREAM\n"}])
            with patch.dict('os.environ', {"PARCHWELL_RUN_ID": "run-id"}):
                module.transition(lifecycle, "eh-new")
            self.assertIn("-var=eventhub_private_endpoint_name=pe-eh-new", lifecycle.calls[3])
            self.assertEqual(new, json.loads((path / "ephemeral-output.json").read_text()))
            self.assertFalse((path / "namespace-transition.json").exists())


if __name__ == "__main__":
    unittest.main()
