"""Run one repeat of every B06 and B07 configuration in one job and summarise the producer evidence.

The summaries are smoke diagnostics for planning the B06 and B07 judgement; they are not results, and the
verifier owns every judgement.
"""
import json
import math
import re

ITEMS = ("B06", "B07")
CONFIGURATIONS = (("B06", "C0"), ("B06", "C1"), ("B07", "C0"), ("B07", "C1"))
MODE = "ledger-batch"
MODES = ("start-line", "loadgen", "verify")
REPEAT = 1
BLOB_MAXIMUM = 268435456  # per-blob download cap; a larger blob is recorded as a gap, never truncated
LABEL = r"[a-z0-9](?:[a-z0-9-]*[a-z0-9])?"
WINDOW_FIELDS = ("cnt", "sum", "min", "max", "avg", "p50")


class Gap(Exception):
    """A named reason why one configuration's summary is incomplete."""


def strict_json(raw):
    """Decode JSON text or bytes, refusing duplicate keys and non-finite numbers."""
    def pairs(items):
        result = {}
        for key, item in items:
            if key in result:
                raise ValueError("duplicate JSON key")
            result[key] = item
        return result

    def constant(_value):
        raise ValueError("non-finite JSON number")

    if isinstance(raw, bytes):
        raw = raw.decode("utf-8", errors="strict")
    try:
        return json.loads(raw, object_pairs_hook=pairs, parse_constant=constant)
    except RecursionError as error:
        raise ValueError("JSON nesting exceeded") from error


def nearest_rank(ordered, fraction):
    return ordered[max(0, math.ceil(fraction * len(ordered)) - 1)]


class LedgerBatch:
    def __init__(self, phase):
        self.p = phase

    def worker_run(self, item):
        if item not in ITEMS:
            raise ValueError("unsupported ledger item")
        run = self.p.read("phase.json")["run_id"] + "-" + item.lower()
        if len(run) > 29 or not re.fullmatch(LABEL, run):
            raise ValueError("ledger run identity invalid")
        return run

    def run_smoke(self):
        phase = self.p.read("phase.json")
        if phase.get("scenario") != MODE or phase.get("run_kind") != "smoke" or phase.get("items") != list(ITEMS):
            raise ValueError("unsupported ledger batch selection")
        passed = True
        for item, configuration in CONFIGURATIONS:
            records = [self.p.run_mode(mode, REPEAT, item, configuration) for mode in MODES]
            if any(record["outcome"] != "complete" for record in records):
                passed = False
            if not self.summarise(item, configuration, records[1])["complete"]:
                passed = False
        return int(not passed)

    def summarise(self, item, configuration, loadgen_record):
        summary = {"schema_version": 1, "item": item, "configuration": configuration, "repeat": REPEAT,
                   "smoke_diagnostic": True, "complete": False, "reason": None, "statistics": None, "ledger": None}
        try:
            if loadgen_record["outcome"] != "complete":
                raise Gap("loadgen_incomplete")
            pods = strict_json((self.p.artifacts / loadgen_record["job"] / "pods.json").read_bytes())["items"]
            if not isinstance(pods, list) or len(pods) != 1:
                raise Gap("producer_pod_count")
            pod = pods[0]["metadata"]["name"]
            if not isinstance(pod, str) or not re.fullmatch(LABEL, pod):
                raise Gap("producer_pod_name")
            prefix = f"{self.worker_run(item)}/{configuration}/r{REPEAT}/producer/{pod}/publication/"
            content = {}
            for name in ("statistics.jsonl", "ledger.jsonl"):
                target = self.p.private / f"ledger-{item}-{configuration}-{name}"
                data = self.p.download_blob(prefix + name, target, BLOB_MAXIMUM, None, container="ledger")
                if data is None:
                    raise Gap("download_failed:" + name)
                content[name] = data
            summary["statistics"] = self.statistics(content["statistics.jsonl"])
            summary["ledger"] = self.ledger(content["ledger.jsonl"])
            summary["complete"] = True
        except Gap as error:
            summary["reason"] = str(error)
        except (OSError, ValueError, KeyError, TypeError, IndexError, UnicodeError, AttributeError):
            summary["reason"] = "malformed_evidence"
        target = self.p.candidates / f"{item}-{configuration}-smoke-summary.json"
        target.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        state = self.p.state()
        state.setdefault("ledger_smoke", []).append({"item": item, "configuration": configuration,
                                                     "complete": summary["complete"], "reason": summary["reason"]})
        self.p.save_state(state)
        return summary

    @staticmethod
    def statistics(raw):
        rows = []
        for line in raw.splitlines():
            if not line.strip():
                continue
            text = strict_json(line)["raw_json"]
            if not isinstance(text, str):
                raise ValueError("raw_json must be text")
            report = strict_json(text)
            produce = [broker["req"]["Produce"] for broker in report.get("brokers", {}).values()
                       if isinstance(broker.get("req"), dict) and "Produce" in broker["req"]]
            if any(type(value) is not int for value in produce):
                raise ValueError("Produce counter must be an integer")
            batches = {name: {field: topic["batchcnt"].get(field) for field in WINDOW_FIELDS}
                       for name, topic in sorted(report.get("topics", {}).items()) if isinstance(topic.get("batchcnt"), dict)}
            rows.append({"ts": report.get("ts"), "produce_requests": sum(produce) if produce else None, "batchcnt": batches})
        return {"reports": len(rows), "rows": rows}

    @staticmethod
    def ledger(raw):
        rows = delivered = 0
        latencies = []
        for line in raw.splitlines():
            if not line.strip():
                continue
            record = strict_json(line)
            rows += 1
            if record.get("delivered") is True:
                delivered += 1
            latency = record.get("delivery_latency_ms")
            if latency is not None:
                if type(latency) not in (int, float) or not math.isfinite(latency):
                    raise ValueError("delivery latency must be a finite number")
                latencies.append(float(latency))
        latencies.sort()
        result = {"rows": rows, "delivered": delivered, "latency_count": len(latencies)}
        if latencies:
            result.update(latency_min_ms=latencies[0], latency_p50_ms=nearest_rank(latencies, 0.5),
                          latency_p95_ms=nearest_rank(latencies, 0.95), latency_max_ms=latencies[-1])
        return result
