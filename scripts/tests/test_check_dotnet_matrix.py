"""Tests for scripts/check-dotnet-matrix.py. Negative tests change one thing in a valid fixture, except T14 and T24, which combine changes on purpose."""
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest

CHECKER = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "check-dotnet-matrix.py")
PROJECTS = [
    "Parchwell.ArchitectureTests",
    "Parchwell.Enrichment.Tests",
    "Parchwell.Kafka.Tests",
    "Parchwell.LoadGen.Tests",
    "Parchwell.Scenarios.Tests",
    "Parchwell.Verifier.Tests",
]
PREFIX = "check-dotnet-matrix: "
USAGE = "usage: check-dotnet-matrix.py <reports directory> <shard file> <job>=<result>..."
SUCCESS_LINE = PREFIX + "6 test projects passed with validated reports: " + ", ".join(PROJECTS)
GOOD_JOBS = ["dotnet-tests=success", "worker-images=success"]
INVENTORY = "".join(
    f"/w/tests/Parchwell.{x}.Tests/Parchwell.{x}.Tests.csproj\n"
    for x in ("Enrichment", "Kafka", "LoadGen", "Scenarios", "Verifier")
)


VERIFIER = "Parchwell.Verifier.Tests"
S1 = VERIFIER + "--s1"
REST = VERIFIER + "--rest"
SHARD_FILE = VERIFIER + "\ts1\t" + VERIFIER + ".A\n"
METHODS = [VERIFIER + ".A.M1", VERIFIER + ".B.M2"]


def results_xml(*tests):
    """An xUnit-shaped report holding one <test> element per (type, method) pair."""
    lines = "".join(f'<test type="{t}" method="{m}" name="{t}.{m}" />' for t, m in tests)
    return f"<assemblies><assembly><collection>{lines}</collection></assembly></assemblies>"


def summary(project, shard=None):
    return {
        "schema_version": 1,
        "tier": "all",
        "projects": [project],
        "shard": shard,
        "exit_code": 0,
        "outcome": "partial_passed",
        "failed_stages": [],
        "stages": [
            {"label": f"{project} tests", "elapsed_s": 1, "start_utc": "2026-11-13T00:00:00Z",
             "end_utc": "2026-11-13T00:00:01Z", "exit_code": 0},
            {"label": f"{project} report", "elapsed_s": 1, "start_utc": "2026-11-13T00:00:01Z",
             "end_utc": "2026-11-13T00:00:02Z", "exit_code": 0},
            {"label": f"{project} test list", "elapsed_s": 1, "start_utc": "2026-11-13T00:00:02Z",
             "end_utc": "2026-11-13T00:00:03Z", "exit_code": 0},
        ],
    }


class CheckerTestCase(unittest.TestCase):
    def setUp(self):
        self.root = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, self.root, ignore_errors=True)
        self.reports = os.path.join(self.root, "reports")
        os.mkdir(self.reports)
        self.shard_file = os.path.join(self.root, "shards.tsv")
        self.write(self.root, "shards.tsv", "# none\n")
        for project in PROJECTS:
            self.add(project, 1)

    def directory(self, project, attempt=1):
        return os.path.join(self.reports, f"dotnet-verification-{project}-attempt-{attempt}")

    def add(self, project, attempt, passed=True, inventory=INVENTORY, shard=None, methods="[]", results="<assemblies />"):
        """Adds the artifact of a unit: the project itself, or with a shard the unit <project>--<shard>."""
        unit = project if shard is None else f"{project}--{shard}"
        directory = self.directory(unit, attempt)
        os.makedirs(directory)
        self.write(directory, "summary.json", json.dumps(summary(project, shard)))
        self.write(directory, "stage-timing.tsv", f"{project} tests\t1\t2026-11-13T00:00:00Z\t2026-11-13T00:00:01Z\n")
        self.write(directory, "results.xml", results)
        self.write(directory, "unit-projects.txt", inventory)
        self.write(directory, "methods.json", methods)
        if passed:
            self.write(directory, "leg-passed", "")

    def write(self, directory, name, text):
        with open(os.path.join(directory, name), "w", newline="") as handle:
            handle.write(text)

    def set_summary(self, project, **changes):
        data = summary(project)
        for key, value in changes.items():
            if value is MISSING:
                del data[key]
            else:
                data[key] = value
        self.write(self.directory(project), "summary.json", json.dumps(data))

    def run_checker(self, jobs=None, reports=None, with_shard_file=True):
        command = [sys.executable, CHECKER, reports or self.reports] + ([self.shard_file] if with_shard_file else []) + (
            GOOD_JOBS if jobs is None else jobs)
        done = subprocess.run(command, capture_output=True, text=True)
        return done.returncode, done.stdout, done.stderr

    def expect_findings(self, *lines, jobs=None):
        code, out, err = self.run_checker(jobs)
        self.assertEqual(1, code, err)
        self.assertEqual("", out)
        self.assertEqual("".join(PREFIX + line + "\n" for line in lines), err)

    def expect_success(self):
        code, out, err = self.run_checker()
        self.assertEqual((0, SUCCESS_LINE + "\n", ""), (code, out, err))


MISSING = object()


class JobResultTests(CheckerTestCase):
    def test_t1_valid_fixture_passes(self):
        self.expect_success()

    def test_t2_worker_images_not_success(self):
        for value in ("failure", "cancelled", "skipped", ""):
            with self.subTest(value=value):
                self.expect_findings(
                    f"job-result: worker-images is '{value}', expected 'success'",
                    jobs=["dotnet-tests=success", f"worker-images={value}"],
                )

    def test_t3_dotnet_tests_failure(self):
        self.expect_findings("job-result: dotnet-tests is 'failure', expected 'success'",
                             jobs=["dotnet-tests=failure", "worker-images=success"])

    def test_t4_usage_errors(self):
        cases = {
            "only the directory": [],
            "directory and shard file only": [],
            "no equals": ["worker-images"],
            "empty job name": ["=success"],
            "repeated job": ["dotnet-tests=success", "dotnet-tests=success"],
        }
        for label, jobs in cases.items():
            with self.subTest(label):
                code, out, err = self.run_checker(jobs, with_shard_file=label != "only the directory")
                self.assertEqual((2, "", USAGE + "\n"), (code, out, err))


class ArtifactDiscoveryTests(CheckerTestCase):
    def test_t5_absent_or_empty_reports_directory(self):
        absent = os.path.join(self.root, "absent")
        empty = os.path.join(self.root, "empty")
        os.mkdir(empty)
        for path in (absent, empty):
            with self.subTest(path=path):
                code, out, err = self.run_checker(reports=path)
                self.assertEqual(1, code)
                self.assertEqual(
                    f"{PREFIX}no-artifacts: {path}\n{PREFIX}missing-project: Parchwell.ArchitectureTests\n", err)

    def test_t6_bad_artifact_names(self):
        def make_file():
            self.write(self.reports, "notes.txt", "x")
            return "notes.txt"

        def make_attempt_zero():
            name = "dotnet-verification-Parchwell.Kafka.Tests-attempt-0"
            os.mkdir(os.path.join(self.reports, name))
            return name

        def make_other():
            os.mkdir(os.path.join(self.reports, "other"))
            return "other"

        for make in (make_file, make_attempt_zero, make_other):
            with self.subTest(make.__name__):
                name = make()
                self.expect_findings(f"bad-artifact-name: {name}")
                path = os.path.join(self.reports, name)
                os.remove(path) if os.path.isfile(path) else os.rmdir(path)

    def test_t7_highest_attempt_wins_when_valid(self):
        self.add("Parchwell.Kafka.Tests", 2)
        os.remove(os.path.join(self.directory("Parchwell.Kafka.Tests", 1), "leg-passed"))
        self.expect_success()

    def test_t8_highest_attempt_failing_is_reported(self):
        self.add("Parchwell.Kafka.Tests", 2, passed=False)
        self.expect_findings("leg-not-passed: Parchwell.Kafka.Tests")

    def test_t9_attempts_compare_numerically(self):
        shutil.rmtree(self.directory("Parchwell.Kafka.Tests", 1))
        self.add("Parchwell.Kafka.Tests", 9)
        self.add("Parchwell.Kafka.Tests", 10, passed=False)
        self.expect_findings("leg-not-passed: Parchwell.Kafka.Tests")


class RequiredFileTests(CheckerTestCase):
    def test_t10_missing_files(self):
        for name in ("summary.json", "stage-timing.tsv", "results.xml", "methods.json"):
            with self.subTest(name):
                path = os.path.join(self.directory("Parchwell.LoadGen.Tests"), name)
                with open(path) as handle:
                    content = handle.read()
                os.remove(path)
                self.expect_findings(f"missing-file: Parchwell.LoadGen.Tests/{name}")
                self.write(self.directory("Parchwell.LoadGen.Tests"), name, content)

    def test_t11_missing_inventory(self):
        os.remove(os.path.join(self.directory("Parchwell.Verifier.Tests"), "unit-projects.txt"))
        self.expect_findings("missing-file: Parchwell.Verifier.Tests/unit-projects.txt")

    def test_t12_missing_passed_marker(self):
        os.remove(os.path.join(self.directory("Parchwell.Kafka.Tests"), "leg-passed"))
        self.expect_findings("leg-not-passed: Parchwell.Kafka.Tests")


class SummaryTests(CheckerTestCase):
    project = "Parchwell.Scenarios.Tests"

    def test_t13_each_bad_key(self):
        cases = [
            ("schema_version", 2), ("schema_version", True), ("tier", "fast"), ("projects", []),
            ("projects", ["Parchwell.Scenarios.Tests", "Parchwell.Kafka.Tests"]), ("projects", None),
            ("shard", "s1"), ("shard", MISSING),
            ("exit_code", 1), ("exit_code", False), ("outcome", "full_gate_passed"), ("outcome", "failed"),
            ("failed_stages", [{"label": "Warning control", "exit_code": 1}]), ("outcome", MISSING),
        ]
        for key, value in cases:
            with self.subTest(key=key, value=repr(value)):
                self.set_summary(self.project, **{key: value})
                self.expect_findings(f"bad-summary: {self.project}: {key}")

    def test_t14_first_failing_key_only(self):
        self.set_summary(self.project, tier="fast", exit_code=1)
        self.expect_findings(f"bad-summary: {self.project}: tier")

    def test_t15_not_a_json_object(self):
        for text in ("not json", "[]"):
            with self.subTest(text=text):
                self.write(self.directory(self.project), "summary.json", text)
                self.expect_findings(f"bad-summary: {self.project}: not a JSON object")

    def test_t16_report_stage_missing_or_failed(self):
        project = "Parchwell.Enrichment.Tests"
        good = summary(project)["stages"]
        cases = {
            "report removed": good[:1],
            "report exit 1": [good[0], dict(good[1], exit_code=1)],
            "tests removed": good[1:],
            "empty scaffold": [],
            "stages key removed": MISSING,
        }
        for label, stages in cases.items():
            with self.subTest(label):
                self.set_summary(project, stages=stages)
                self.expect_findings(f"report-stage: {project}")

    def test_t17_stage_of_another_project_does_not_count(self):
        project = "Parchwell.Enrichment.Tests"
        stages = summary(project)["stages"]
        stages[1] = dict(stages[1], label="Parchwell.Kafka.Tests report")
        self.set_summary(project, stages=stages)
        self.expect_findings(f"report-stage: {project}")


class InventoryTests(CheckerTestCase):
    def test_t18_inventory_differs(self):
        lines = INVENTORY.splitlines(keepends=True)[1:]
        self.write(self.directory("Parchwell.Verifier.Tests"), "unit-projects.txt", "".join(lines))
        self.expect_findings("inventory-mismatch: Parchwell.Verifier.Tests")

    def test_t19_prefix_and_line_ends_do_not_matter(self):
        text = INVENTORY.replace("/w/", "/other/root/").replace("\n", "\r\n")
        self.write(self.directory("Parchwell.Verifier.Tests"), "unit-projects.txt", text)
        self.expect_success()

    def test_t20_missing_verifier(self):
        shutil.rmtree(self.directory("Parchwell.Verifier.Tests"))
        self.expect_findings("missing-project: Parchwell.Verifier.Tests")

    def test_t21_missing_architecture_tests(self):
        shutil.rmtree(self.directory("Parchwell.ArchitectureTests"))
        self.expect_findings("missing-project: Parchwell.ArchitectureTests")

    def test_t22_extra_project(self):
        self.add("Parchwell.Extra.Tests", 1)
        self.expect_findings("extra-project: Parchwell.Extra.Tests")

    def test_t23_inventory_names_project_without_artifact(self):
        extra = "/w/tests/Parchwell.New.Tests/Parchwell.New.Tests.csproj\n"
        for project in PROJECTS:
            self.write(self.directory(project), "unit-projects.txt", INVENTORY + extra)
        self.expect_findings("missing-project: Parchwell.New.Tests")

    def test_t24_findings_come_in_the_documented_order(self):
        shutil.rmtree(self.directory("Parchwell.Verifier.Tests"))
        os.remove(os.path.join(self.directory("Parchwell.Kafka.Tests"), "leg-passed"))
        self.expect_findings(
            "job-result: worker-images is 'failure', expected 'success'",
            "leg-not-passed: Parchwell.Kafka.Tests",
            "missing-project: Parchwell.Verifier.Tests",
            jobs=["dotnet-tests=success", "worker-images=failure"],
        )


class ShardedFixtureTestCase(CheckerTestCase):
    """Fixture W: the Verifier tests are two units, s1 (class A) and rest (class B)."""

    def setUp(self):
        super().setUp()
        shutil.rmtree(self.directory(VERIFIER))
        methods = json.dumps(METHODS)
        self.add(VERIFIER, 1, shard="s1", methods=methods, results=results_xml((VERIFIER + ".A", "M1")))
        self.add(VERIFIER, 1, shard="rest", methods=methods, results=results_xml((VERIFIER + ".B", "M2")))
        self.write_shard_file(SHARD_FILE)

    def write_shard_file(self, text):
        self.write(self.root, "shards.tsv", text)

    def add_unit(self, unit_project, shard, methods=None, results="<assemblies />"):
        self.add(unit_project, 1, shard=shard, methods=json.dumps(METHODS if methods is None else methods), results=results)

    def write_unit_summary(self, unit, project, unit_shard, **changes):
        data = summary(project, unit_shard)
        for key, value in changes.items():
            if value is MISSING:
                del data[key]
            else:
                data[key] = value
        self.write(self.directory(unit), "summary.json", json.dumps(data))


class ShardedUnitTests(ShardedFixtureTestCase):
    def test_t25_valid_sharded_fixture_passes(self):
        self.expect_success()

    def test_t26_missing_rest_shard(self):
        shutil.rmtree(self.directory(REST))
        self.expect_findings(f"missing-shard: {REST}")

    def test_t27_unsharded_artifact_of_a_sharded_project(self):
        self.add(VERIFIER, 1)
        self.expect_findings(f"unexpected-artifact: {VERIFIER}")

    def test_t28_unexpected_units(self):
        self.add("Parchwell.Scenarios.Tests", 1, shard="rest")
        self.expect_findings("unexpected-artifact: Parchwell.Scenarios.Tests--rest")
        shutil.rmtree(self.directory("Parchwell.Scenarios.Tests--rest"))
        self.add_unit(VERIFIER, "s2", results="<assemblies />")
        self.expect_findings(f"unexpected-artifact: {VERIFIER}--s2")

    def test_t29_rest_runs_a_listed_class(self):
        self.write(self.directory(REST), "results.xml", results_xml((VERIFIER + ".B", "M2"), (VERIFIER + ".A", "M1")))
        self.expect_findings(f"class-outside-shard: {REST}: {VERIFIER}.A",
                             f"method-in-two-shards: {VERIFIER}: {VERIFIER}.A.M1")

    def test_t30_shard_runs_an_unlisted_class(self):
        self.write(self.directory(S1), "results.xml", results_xml((VERIFIER + ".A", "M1"), (VERIFIER + ".B", "M2")))
        self.expect_findings(f"class-outside-shard: {S1}: {VERIFIER}.B",
                             f"method-in-two-shards: {VERIFIER}: {VERIFIER}.B.M2")

    def test_t31_listed_method_runs_nowhere(self):
        for unit in (S1, REST):
            self.write(self.directory(unit), "methods.json", json.dumps(METHODS + [VERIFIER + ".B.M3"]))
        self.expect_findings(f"method-not-run: {VERIFIER}: {VERIFIER}.B.M3")

    def test_t32_listings_differ_between_units(self):
        self.write(self.directory(REST), "methods.json", json.dumps(METHODS + [VERIFIER + ".B.M3"]))
        self.expect_findings(f"methods-mismatch: {VERIFIER}")

    def test_t33_test_missing_from_the_listing(self):
        self.write(self.directory(S1), "results.xml", results_xml((VERIFIER + ".A", "M1"), (VERIFIER + ".A", "M9")))
        self.expect_findings(f"unlisted-test: {S1}: {VERIFIER}.A.M9")

    def test_t34_shard_class_with_no_tests(self):
        self.write_shard_file(SHARD_FILE + VERIFIER + "\ts1\t" + VERIFIER + ".C\n")
        self.expect_findings(f"shard-class-not-run: {S1}: {VERIFIER}.C")

    def test_t35_invalid_listing(self):
        for text in ("not json", "{}", "[1]", '["x","x"]'):
            with self.subTest(text=text):
                self.write(self.directory(S1), "methods.json", text)
                self.expect_findings(f"bad-methods: {S1}")

    def test_t36_results_do_not_parse(self):
        self.write(self.directory(S1), "results.xml", "not xml")
        self.expect_findings(f"bad-results: {S1}")

    def test_t37_summary_shard_value(self):
        cases = [
            (S1, VERIFIER, "s1", {"shard": "rest"}),
            (S1, VERIFIER, "s1", {"shard": None}),
            (S1, VERIFIER, "s1", {"shard": MISSING}),
            ("Parchwell.Scenarios.Tests", "Parchwell.Scenarios.Tests", None, {"shard": "s1"}),
        ]
        for unit, project, shard, changes in cases:
            with self.subTest(unit=unit, changes=repr(changes)):
                self.write_unit_summary(unit, project, shard, **changes)
                self.expect_findings(f"bad-summary: {unit}: shard")
                self.write_unit_summary(unit, project, shard)

    def test_t38_test_list_stage(self):
        project = "Parchwell.Kafka.Tests"
        good = summary(project)["stages"]
        for label, stages in {"stage missing": good[:2], "exit 1": good[:2] + [dict(good[2], exit_code=1)]}.items():
            with self.subTest(label):
                self.set_summary(project, stages=stages)
                self.expect_findings(f"report-stage: {project}")

    def test_t39_missing_listing(self):
        os.remove(os.path.join(self.directory("Parchwell.Kafka.Tests"), "methods.json"))
        self.expect_findings("missing-file: Parchwell.Kafka.Tests/methods.json")
        self.write(self.directory("Parchwell.Kafka.Tests"), "methods.json", "[]")
        os.remove(os.path.join(self.directory(REST), "methods.json"))
        self.expect_findings(f"missing-file: {REST}/methods.json")

    def test_t40_bad_unit_names(self):
        for name in (
            "dotnet-verification-Parchwell.Kafka.Tests--Bad-attempt-1",
            "dotnet-verification-Parchwell.Kafka.Tests--rest--x-attempt-1",
            "dotnet-verification---rest-attempt-1",
        ):
            with self.subTest(name):
                os.mkdir(os.path.join(self.reports, name))
                self.expect_findings(f"bad-artifact-name: {name}")
                os.rmdir(os.path.join(self.reports, name))

    def test_t41_unsharded_project_lists_a_method_that_did_not_run(self):
        self.write(self.directory("Parchwell.Scenarios.Tests"), "methods.json", '["Parchwell.Scenarios.Tests.X.M"]')
        self.expect_findings("method-not-run: Parchwell.Scenarios.Tests: Parchwell.Scenarios.Tests.X.M")

    def test_t42_repeated_method_inside_one_unit(self):
        rows = [(VERIFIER + ".A", "M1")] * 3
        self.write(self.directory(S1), "results.xml", results_xml(*rows))
        self.expect_success()

    def test_t45_findings_come_in_the_documented_order(self):
        os.remove(os.path.join(self.directory(S1), "leg-passed"))
        self.write(self.directory(S1), "results.xml", results_xml((VERIFIER + ".A", "M1"), (VERIFIER + ".B", "M2")))
        self.expect_findings(
            "job-result: worker-images is 'failure', expected 'success'",
            f"leg-not-passed: {S1}",
            f"class-outside-shard: {S1}: {VERIFIER}.B",
            f"method-in-two-shards: {VERIFIER}: {VERIFIER}.B.M2",
            jobs=["dotnet-tests=success", "worker-images=failure"],
        )

    def test_t46_shard_file_with_crlf_line_ends(self):
        self.write_shard_file(SHARD_FILE.replace("\n", "\r\n"))
        self.expect_success()

    def test_t47_inventory_of_a_shard_unit(self):
        lines = INVENTORY.splitlines(keepends=True)[1:]
        self.write(self.directory(REST), "unit-projects.txt", "".join(lines))
        self.expect_findings(f"inventory-mismatch: {REST}")


class ShardFileTests(CheckerTestCase):
    """Fixture V with a bad shard file: nothing is sharded, so only the file's lines are reported."""

    def test_t43_bad_shard_file_lines(self):
        a = VERIFIER + ".A"
        cases = [
            ("two fields", f"{VERIFIER}\ts1\n", 1),
            ("rest shard", f"{VERIFIER}\trest\t{a}\n", 1),
            ("uppercase shard", f"{VERIFIER}\tBad\t{a}\n", 1),
            ("class of another project", f"{VERIFIER}\ts1\tOther.X\n", 1),
            ("duplicate pair", f"{VERIFIER}\ts1\t{a}\n{VERIFIER}\ts2\t{a}\n", 2),
            ("unexpected project", "Parchwell.Nope.Tests\ts1\tParchwell.Nope.Tests.A\n", 1),
            ("four fields", f"{VERIFIER}\ts1\t{a}\tx\n", 1),
            ("project outside the pattern", "Verifier.Tests\ts1\tVerifier.Tests.A\n", 1),
            ("class with a space", f"{VERIFIER}\ts1\t{a} B\n", 1),
        ]
        for label, text, line in cases:
            with self.subTest(label):
                self.write(self.root, "shards.tsv", text)
                self.expect_findings(f"bad-shard-file: {self.shard_file}: line {line}")

    def test_t44_missing_shard_file(self):
        os.remove(self.shard_file)
        self.expect_findings(f"bad-shard-file: {self.shard_file}: unreadable")


if __name__ == "__main__":
    unittest.main()
