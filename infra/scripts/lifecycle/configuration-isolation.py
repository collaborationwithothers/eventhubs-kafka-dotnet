"""Private, fail-closed namespace transitions for the ephemeral root."""
import hashlib
import ipaddress
import json
import os
from pathlib import Path
import re
import sys
import time


class TransitionError(Exception):
    def __init__(self, reason, code):
        super().__init__(reason)
        self.code = code


def reject(reason="namespace_transition_input_rejected", code=2):
    raise TransitionError(reason, code)


def namespace_name(repository, run, attempt, phase):
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository, flags=re.ASCII):
        reject()
    if not re.fullmatch(r"[1-9][0-9]*", run, flags=re.ASCII) or not re.fullmatch(r"[1-9][0-9]*", attempt, flags=re.ASCII):
        reject()
    if phase not in ("probe", "C0", "C1", "C3"):
        reject()
    return "ehp-" + hashlib.sha256("\n".join((repository, run, attempt, phase)).encode("ascii")).hexdigest()[:24]


EH_ROLES = frozenset(("probe_events_receiver", "loadgen_events_sender", "enrichment_events_receiver", "enrichment_state_sender", "enrichment_dlq_sender", "verifier_events_receiver"))
BLOB_ROLES = frozenset(("probe_single_action_contributor", "verifier_single_action_reader", "loadgen_ledger_contributor", "enrichment_claim_check_contributor", "verifier_ledger_reader", "verifier_results_contributor"))


SHARED_ADDRESSES = frozenset({
    "azurerm_resource_group.ephemeral",
    "module.storage.azapi_resource.this",
    'module.storage.module.private_endpoints["blob"].azapi_resource.this',
    'module.storage.module.private_endpoints["blob"].azapi_resource.private_dns_zone_group[0]',
    'module.storage.module.diagnostic_setting_blob.azapi_resource.this["law"]',
    *(f'module.storage.module.containers["{key}"].azapi_resource.this' for key in ("ledger", "verifier", "claim_check", "single_action")),
    *(f'azurerm_role_assignment.workload["{key}"]' for key in BLOB_ROLES),
})


def role_address(address, roles):
    match = re.fullmatch(r'azurerm_role_assignment\.workload\["([a-z_]+)"\]', address)
    return bool(match and match.group(1) in roles)


def validate_plan(plan, activate=False):
    try:
        changes = plan["resource_changes"]
        if not isinstance(changes, list) or not changes:
            raise ValueError()
        seen = set()
        for item in changes:
            address, mode = item["address"], item["mode"]
            actions = item["change"]["actions"]
            if not isinstance(address, str) or address in seen or not isinstance(actions, list):
                raise ValueError()
            seen.add(address)
            if mode == "data":
                if actions not in (["read"], ["no-op"]):
                    raise ValueError()
                continue
            if mode != "managed":
                raise ValueError()
            eh = (address.startswith("module.eventhubs[0].") or address in ("azurerm_eventhub.state[0]", "azurerm_monitor_diagnostic_setting.eventhubs[0]") or role_address(address, EH_ROLES))
            shared = address in SHARED_ADDRESSES
            if eh:
                if actions not in (["no-op"], ["create"] if activate else ["delete"]):
                    raise ValueError()
            elif not shared or actions != ["no-op"]:
                raise ValueError()
        if not SHARED_ADDRESSES.issubset(seen):
            raise ValueError()
    except (KeyError, TypeError, ValueError):
        reject("namespace_transition_plan_rejected")


SHARED_KEYS = ("storage_account_resource_id", "blob_private_endpoint_resource_id", "container_resource_ids", "workspace_resource_id", "blob_diagnostic_target_resource_id")
ALL_KEYS = frozenset((*SHARED_KEYS, "eventhubs_enabled", "namespace_resource_id", "eventhubs_private_endpoint_resource_id"))


def output_values(data, enabled):
    try:
        life = data["configuration_lifecycle"]["value"]
        ips = data["private_endpoint_ips"]["value"]
        blob = data["blob"]["value"]
        if not isinstance(life, dict) or set(life) != ALL_KEYS or life["eventhubs_enabled"] is not enabled:
            raise ValueError()
        for key in SHARED_KEYS:
            if key != "container_resource_ids" and (not isinstance(life[key], str) or not life[key].startswith("/")):
                raise ValueError()
        containers = life["container_resource_ids"]
        if not isinstance(containers, dict) or set(containers) != {"ledger", "verifier", "claim_check", "single_action"} or any(not isinstance(x, str) or not x.startswith("/") for x in containers.values()):
            raise ValueError()
        if not isinstance(blob, dict) or not isinstance(blob.get("account_url"), str) or not isinstance(blob.get("containers"), dict):
            raise ValueError()
        if not isinstance(ips, dict) or not isinstance(ips.get("blob"), str):
            raise ValueError()
        ipaddress.ip_address(ips["blob"])
        if enabled:
            if not isinstance(data["probe_v2_role_scope"]["value"], dict):
                raise ValueError()
            eh = data["eventhubs"]["value"]
            if not isinstance(eh, dict) or not isinstance(eh.get("namespace_name"), str) or eh.get("bootstrap_servers") != eh["namespace_name"] + ".servicebus.windows.net:9093":
                raise ValueError()
            ipaddress.ip_address(ips["eventhubs"])
            if any(not isinstance(life[k], str) or not life[k].startswith("/") for k in ("namespace_resource_id", "eventhubs_private_endpoint_resource_id")):
                raise ValueError()
        elif ("eventhubs" in data or "probe_v2_role_scope" in data or ips.get("eventhubs") is not None or life["namespace_resource_id"] is not None or life["eventhubs_private_endpoint_resource_id"] is not None):
            raise ValueError()
        prefix = r"/subscriptions/[^/\s]+/resourceGroups/[^/\s]+/providers/"
        def arm(value, suffix):
            return bool(re.fullmatch(prefix + suffix, value, flags=re.IGNORECASE))
        account = life["storage_account_resource_id"]
        if not arm(account, r"Microsoft\.Storage/storageAccounts/[^/\s]+") or not arm(life["blob_private_endpoint_resource_id"], r"Microsoft\.Network/privateEndpoints/[^/\s]+") or not arm(life["workspace_resource_id"], r"Microsoft\.OperationalInsights/workspaces/[^/\s]+"):
            raise ValueError()
        resource_group = account.split("/providers/", 1)[0].casefold() if "/providers/" in account else account.lower().split("/providers/", 1)[0]
        if life["blob_private_endpoint_resource_id"].lower().split("/providers/", 1)[0] != resource_group:
            raise ValueError()
        for value in containers.values():
            if not re.fullmatch(re.escape(account) + r"/blobServices/default/containers/[^/\s]+", value, flags=re.IGNORECASE):
                raise ValueError()
        if enabled:
            if life["namespace_resource_id"].lower().split("/providers/", 1)[0] != resource_group or life["eventhubs_private_endpoint_resource_id"].lower().split("/providers/", 1)[0] != resource_group:
                raise ValueError()
            if not arm(life["namespace_resource_id"], r"Microsoft\.EventHub/namespaces/[^/\s]+") or not arm(life["eventhubs_private_endpoint_resource_id"], r"Microsoft\.Network/privateEndpoints/[^/\s]+") or not life["namespace_resource_id"].casefold().endswith("/namespaces/" + eh["namespace_name"].casefold()) or life["eventhubs_private_endpoint_resource_id"].casefold() == life["blob_private_endpoint_resource_id"].casefold():
                raise ValueError()
        ids = [life["storage_account_resource_id"], life["blob_private_endpoint_resource_id"], life["workspace_resource_id"], *containers.values()]
        if len({x.casefold() for x in ids}) != len(ids) or life["blob_diagnostic_target_resource_id"].casefold() != (life["storage_account_resource_id"] + "/blobServices/default").casefold():
            raise ValueError()
        return life, ips, blob
    except (KeyError, TypeError, ValueError):
        reject("namespace_activation_unconfirmed" if enabled else "namespace_removal_unconfirmed", 1)


def shared_matches(before, after):
    a, aips, ablob = before
    b, bips, bblob = after
    return all(a[k] == b[k] for k in SHARED_KEYS) and aips["blob"] == bips["blob"] and ablob == bblob


def transition(lifecycle, name=None):
    """Perform a single off/on transition, writing only private files."""
    try:
        _, variables, _, _ = lifecycle.backend("ephemeral")
        inputs = lifecycle.read_json(variables)
        if not isinstance(inputs, dict) or inputs.get("probe_namespace_data_roles", False) is not False:
            reject()
        if name is not None and (not re.fullmatch(r"[a-z][a-z0-9-]{4,48}[a-z0-9]", name, flags=re.ASCII) or "azure" in name):
            reject()
        run_id = os.environ.get("PARCHWELL_RUN_ID", "")
        if not re.fullmatch(r"[a-z0-9](?:[a-z0-9-]*[a-z0-9])?", run_id) or len(run_id) > 29:
            reject()
        private = lifecycle.private
        output = private / "ephemeral-output.json"
        marker = private / "namespace-transition.json"
        try:
            current = lifecycle.read_json(output)
        except Exception:
            reject("namespace_removal_unconfirmed" if name is None else "namespace_activation_unconfirmed", 1)
        before = output_values(current, name is None)
        env = dict(os.environ, TF_DATA_DIR=str(private / "tfdata-ephemeral"))
        base = ["terraform", "-chdir=infra/ephemeral"]
        end = time.monotonic() + 600
        def command(args, path=None, errors=False, seconds=600, maximum=4 * 1024 * 1024):
            try:
                return lifecycle.run(args, seconds, output=path, maximum=maximum, errors=errors, env=env, end=end, private_capture=True)
            except Exception:
                return 1, b""
        def absent(ids):
            state = private / "namespace-transition-state.txt"
            status, _ = command([*base, "state", "list"], state)
            if status:
                reject("namespace_removal_unconfirmed", 1)
            try:
                lines = state.read_text().splitlines()
            except (OSError, UnicodeError):
                reject("namespace_removal_unconfirmed", 1)
            if (len(lines) != len(set(lines)) or not all(re.fullmatch(r'[A-Za-z0-9_\.\[\]"-]+', x) for x in lines) or "azurerm_resource_group.ephemeral" not in lines or not any(x.startswith("module.storage.") for x in lines)):
                reject("namespace_removal_unconfirmed", 1)
            if any(x.startswith("module.eventhubs[0].") or x in ("azurerm_eventhub.state[0]", "azurerm_monitor_diagnostic_setting.eventhubs[0]") or role_address(x, EH_ROLES) for x in lines):
                reject("namespace_removal_unconfirmed", 1)
            for resource in ids:
                status, error = command(["az", "resource", "show", "--ids", resource, "-o", "json"], private / "namespace-arm-readback.json", True)
                if not status or status in (124, 125) or not re.search(rb"(?m)^ERROR: \((?:ResourceNotFound|ResourceGroupNotFound)\) ", error):
                    reject("namespace_removal_unconfirmed", 1)
        if name is not None:
            try:
                saved = lifecycle.read_json(marker)
            except Exception:
                reject("namespace_removal_unconfirmed", 1)
            if not isinstance(saved, dict) or set(saved) != {"initial", "old_ids"} or not isinstance(saved["old_ids"], list) or len(saved["old_ids"]) != 2 or any(not isinstance(x, str) or not x.startswith("/") for x in saved["old_ids"]):
                reject("namespace_removal_unconfirmed", 1)
            initial = output_values(saved["initial"], True)
            if not shared_matches(initial, before):
                reject("shared_storage_changed", 1)
            if name == saved["initial"]["eventhubs"]["value"]["namespace_name"]:
                reject()
            absent(saved["old_ids"])
        else:
            if marker.exists():
                reject()
            saved = {"initial": current, "old_ids": [before[0]["namespace_resource_id"], before[0]["eventhubs_private_endpoint_resource_id"]]}
        plan = private / "namespace-transition.tfplan"
        args = [*base, "plan", "-input=false", "-lock-timeout=120s", "-var-file=" + str(variables), "-var=run_id=" + run_id, "-var=eventhubs_enabled=" + ("true" if name else "false"), "-out=" + str(plan)]
        if name:
            args += ["-var=eventhub_namespace_name=" + name, "-var=eventhub_private_endpoint_name=pe-" + name]
        if command(args)[0]:
            reject("namespace_transition_plan_rejected", 1)
        plan_json = private / "namespace-transition-plan.json"
        if command([*base, "show", "-json", str(plan)], plan_json)[0]:
            reject("namespace_transition_plan_rejected", 1)
        try:
            plan_data = lifecycle.read_json(plan_json)
        except Exception:
            reject("namespace_transition_plan_rejected")
        validate_plan(plan_data, activate=name is not None)
        if command([*base, "apply", "-input=false", "-lock-timeout=120s", str(plan)])[0]:
            reject("namespace_activation_unconfirmed" if name else "namespace_removal_unconfirmed", 1)
        if command([*base, "output", "-json"], output)[0]:
            reject("namespace_activation_unconfirmed" if name else "namespace_removal_unconfirmed", 1)
        try:
            after_data = lifecycle.read_json(output)
        except Exception:
            reject("namespace_activation_unconfirmed" if name else "namespace_removal_unconfirmed", 1)
        after = output_values(after_data, name is not None)
        if not shared_matches(before, after):
            reject("shared_storage_changed", 1)
        if name is None:
            absent(saved["old_ids"])
            marker.write_text(json.dumps(saved))
            os.chmod(marker, 0o600)
        else:
            eh = after_data["eventhubs"]["value"]
            if eh["namespace_name"] != name or not after[0]["namespace_resource_id"].casefold().endswith("/namespaces/" + name.casefold()) or not after[0]["eventhubs_private_endpoint_resource_id"].casefold().endswith("/privateendpoints/pe-" + name.casefold()):
                reject("namespace_activation_unconfirmed", 1)
            dns = private / "namespace-transition-dns.json"
            status, _ = command([sys.executable, "infra/scripts/phase/phase.py", "resolve", name + ".servicebus.windows.net"], dns, seconds=15, maximum=65536)
            try:
                resolved = lifecycle.read_json(dns) if not status else None
                if not isinstance(resolved, list) or not resolved or any(not isinstance(item, str) for item in resolved):
                    raise ValueError()
                if any(str(ipaddress.ip_address(item)) != item for item in resolved) or set(resolved) != {after[1]["eventhubs"]}:
                    raise ValueError()
            except Exception:
                reject("namespace_activation_unconfirmed", 1)
            marker.unlink()
    except TransitionError:
        raise
    except Exception:
        reject("namespace_transition_input_rejected")


def main(args):
    try:
        if len(args) != 2 or args[0] != "name":
            reject()
        print(namespace_name(os.environ.get("GITHUB_REPOSITORY", ""), os.environ.get("GITHUB_RUN_ID", ""), os.environ.get("GITHUB_RUN_ATTEMPT", ""), args[1]))
        return 0
    except TransitionError as exc:
        print(str(exc), file=sys.stderr)
        return exc.code


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
