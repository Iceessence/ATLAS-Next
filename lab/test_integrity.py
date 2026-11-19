import json
import tempfile
import unittest
from pathlib import Path
from corpus import ROOT, ORIGINS, digest, features, rows

from outcomes import PUBLIC, load_suite, score, sha


class Integrity(unittest.TestCase):
    def test_unsupported_provenance_is_rejected(self):
        spec=json.loads((ROOT/"lab/train.json").read_text())
        with tempfile.TemporaryDirectory() as work:
            path=Path(work)/"corpus.json"
            for origin in (ORIGINS-{"rule_label"})|{"unclassified"}:
                spec["origin"]=origin
                path.write_text(json.dumps(spec))
                with self.subTest(origin=origin), self.assertRaises(ValueError):
                    rows(path)

    def test_split_identity(self):
        groups=[]
        shapes=[]
        for name in ("train","validation","holdout"):
            dataset=rows(ROOT/"lab"/f"{name}.json")
            self.assertTrue(all(row["origin"]=="rule_label" and row["source_sha256"] for row in dataset))
            groups.append({row["family"] for row in dataset})
            shapes.append({row["prompt"].replace(row["family"],"{topic}") for row in dataset})
        for partition in (groups,shapes):
            for i,group in enumerate(partition):
                for other in partition[i+1:]:
                    self.assertFalse(group & other)

    def test_code_baseline_keeps_multiline_oracle_independent(self):
        from code_navigation import FILES, local_search, oracle
        self.assertEqual(len({row[0] for row in FILES}), 2)
        source = "# 🧭\r\ndef probe(\r\n    x: str,\r\n):\r\n    return x\r\n"
        location, signature = oracle(source, "probe")
        self.assertEqual(location, dict(line=2,start=10,length=5,text="probe"))
        self.assertEqual(signature["text"], "def probe(\r\n    x: str,\r\n):")
        self.assertNotEqual(local_search(source,"probe")[1],signature)
        self.assertEqual(oracle(source, "missing"), [None,None])

    def test_conversational_coding_comparator_and_generic_oracle(self):
        from code_conversation import CASES, CODE, gold, search_turn
        ctrl_names = [cid for cid, names, _ in CASES if all(n in CODE for n in names)]
        self.assertGreaterEqual(len(ctrl_names), 6)
        self.assertEqual(search_turn(CODE, "Where is it called?")[0], ("unsupported", []))
        self.assertEqual(search_turn(CODE, "Where is Metrics.strict(3 parameters) defined?")[0], ("unsupported", []))
        exp = gold(CODE, [("Repo.cs", 5, "Load")])
        self.assertEqual(exp[0]["text"], "Load")

    def test_ledger_rejects_mismatched_and_swapped_requests_with_matching_report_hash(self):
        import copy, code_conversation_host as h
        report = json.loads((ROOT / "docs/evidence/next-008/code-conversation.json").read_bytes())
        ledger = json.loads((ROOT / "docs/evidence/next-008/eligible-outcomes.json").read_bytes())

        def with_matching_hash(rep, led):
            rep_bytes = (json.dumps(rep, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
            led["source_report_sha256"] = sha(rep_bytes)

        # 1. Altered request in both report and ledger with matching report SHA-256
        rep1 = copy.deepcopy(report)
        led1 = copy.deepcopy(ledger)
        fake_q = "Where is Unrelated.Fake() defined?"
        for r in (rep1["trials"][0]["runs"], led1["observations"][0]["runs"]):
            r[0]["turns"][0]["delivered"]["request"] = fake_q
        with_matching_hash(rep1, led1)
        with self.assertRaises(ValueError):
            h.validate_report(rep1)
        with self.assertRaises(ValueError):
            h.validate(led1)

        # 2. Swapped turn requests in a multi-turn conversation with matching report SHA-256
        rep2 = copy.deepcopy(report)
        led2 = copy.deepcopy(ledger)
        for r in (rep2["trials"][1]["runs"], led2["observations"][1]["runs"]):
            t0 = r[0]["turns"][0]["delivered"]["request"]
            t1 = r[0]["turns"][1]["delivered"]["request"]
            r[0]["turns"][0]["delivered"]["request"] = t1
            r[0]["turns"][1]["delivered"]["request"] = t0
        with_matching_hash(rep2, led2)
        with self.assertRaises(ValueError):
            h.validate_report(rep2)
        with self.assertRaises(ValueError):
            h.validate(led2)

        # 3. Altered report summary count on constructed trial with matching report SHA-256
        rep3 = copy.deepcopy(report)
        led3 = copy.deepcopy(ledger)
        rep3["trials"][-1]["host_complete"] = False
        rep3["conversations"]["host"] = "13/14"
        with_matching_hash(rep3, led3)
        with self.assertRaises(ValueError):
            h.validate_report(rep3)

    def test_qualified_comparator_pins_scope_and_overloads(self):
        from code_depth import CONTROL, CASES, FIXTURE, gold, search
        cases=[row for row in CASES if row[0]==CONTROL]
        self.assertTrue(any(row[3]=="ambiguous" for row in cases))
        for _,symbol,follow,loc,detail in cases:
            self.assertEqual(search(FIXTURE,symbol,follow),[gold(FIXTURE,loc),gold(FIXTURE,detail)])
        self.assertEqual(search(FIXTURE,"Gamma.Scan","Show its documentation."),["unsupported","unsupported"])

    def test_outcome_suite_provenance_and_digest(self):
        cases, _ = load_suite(PUBLIC)
        self.assertTrue(all(c["origin"] == "author_fixture" for c in cases))
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            suite = root / "cases.json"
            manifest = root / "manifest.json"
            for origin in ("model_label", "observed_project_outcome"):
                case = dict(cases[0], origin=origin, outcome_receipt={"file":"result.txt","sha256":"0"*64})
                (root / "result.txt").write_text("fixture")
                suite.write_text(json.dumps([case]))
                manifest.write_text(json.dumps({"schema":1,"file":suite.name,"sha256":sha(suite.read_bytes())}))
                with self.assertRaises(ValueError):
                    load_suite(manifest)
            suite.write_text("[]")
            with self.assertRaises(ValueError):
                load_suite(manifest)

    def test_correct_text_with_wrong_span_or_verdict_fails_outcome(self):
        case = load_suite(PUBLIC)[0][0]
        expected = case["expect"]
        work = {"state":"completed", "result":expected["answer"], "sourceIntegrity":"passed",
                "answerRelevance":"supported", "proposal":{"qualification":"shadow"},
                "researchReview":{"verdict":"unsupported"},
                "evidence":{"id":"doc-test","version":1,"digest":sha(case["documents"][0].encode()),
                            "span":{"path":"source-0.md","line":expected["line"],"length":expected["length"],"start":0}}}
        result = score(case, {"answer":work, "ids":["doc-test"]})
        self.assertTrue(result["answer"])
        self.assertFalse(result["span"])
        self.assertFalse(result["verdict"])

    def test_project_measurement_and_family_boundaries(self):
        from project_cases import cases, validate_receipt
        from project_audit import PROJECTS, DATA
        splits = {name: cases(name) for name in PROJECTS}
        for name, rows_for_split in splits.items():
            for other, other_rows in splits.items():
                if other != name:
                    for key in ("family","question"):
                        self.assertFalse({r[key] for r in rows_for_split} & {r[key] for r in other_rows})
            for row in rows_for_split:
                ev = row["outcome_receipt"]
                self.assertEqual(ev["sha256"], sha((DATA/name/ev["file"]).read_bytes()))
                expected = row["expect"]
                raw = row["documents"][expected["document"]].encode("utf-16-le")
                a = 2*expected["start"]
                self.assertEqual(raw[a:a+2*expected["length"]].decode("utf-16-le"), expected["answer"])
        receipt=json.loads((DATA/"train"/"numpy.json").read_bytes())
        for key,value in [("origin","author_fixture"),("origin","rule_label"),("custody","custodian")]:
            with self.assertRaises(ValueError):
                validate_receipt(dict(receipt,**{key:value}), "numpy")

    def test_blind_comparison_refuses_public_or_existing_destinations(self):
        from project_specialist import blind
        with tempfile.TemporaryDirectory() as work:
            output=Path(work)/"report.json"
            with self.assertRaises(ValueError):
                blind(PUBLIC,output)
            output.write_text("preserve")
            with self.assertRaises(ValueError):
                blind(Path(work)/"manifest.json",output)
            self.assertEqual(output.read_text(),"preserve")

    def test_claim_key_features_respect_import_syntax(self):
        from project_specialist import claim_keys, features
        sample = "\ufeff# Notes\n~~~\nOther flag: red\n~~~\nValid through: 2030-03-02\n- Active flag is blue."
        self.assertEqual(list(claim_keys(sample)), ["Active flag"])
        question = "What is the active flag?"
        self.assertEqual(features(question, sample, {})[1:], features(question, "Active flag: blue", {})[1:])

    def test_project_model_is_lab_only(self):
        from project_audit import DATA
        from project_specialist import CHECKPOINT, features, predict
        import numpy as np
        model = json.loads(CHECKPOINT.read_bytes())
        self.assertEqual(model["qualification"],"shadow")
        self.assertEqual(model["source_pins"],{s:sha((DATA/s/"pins.json").read_bytes()) for s in ("train","validation")})
        self.assertFalse((ROOT/"assets"/CHECKPOINT.name).exists())
        x = np.array([features("Name the restore flag.","Restore flag: active",model["idf"])])
        for policy in model["policies"].values():
            self.assertTrue(np.all((predict(x,policy["weights"])>=0) & (predict(x,policy["weights"])<=1)))

    def test_checkpoint_and_lineage(self):
        manifest=json.loads((ROOT/"assets/native.manifest.json").read_text())
        self.assertEqual(digest(ROOT/"assets/native.json"),manifest["checkpoint_sha256"])
        self.assertEqual(digest(ROOT/"lab/train.json"),manifest["train_sha256"])
        self.assertEqual(digest(ROOT/"lab/validation.json"),manifest["validation_sha256"])
        self.assertEqual("shadow",manifest["qualification"])
        self.assertEqual(len(features("hello")),256)

    def test_python_header_grammar_against_cpython_oracle(self):
        import ast, subprocess
        from importlib.metadata import distribution
        exe = ROOT / "checks/bin/Release/net10.0-windows/AtlasNext.Checks.exe"
        self.assertTrue(exe.is_file(), f"Missing Release checks binary: {exe}")

        def probe(files, turns):
            with tempfile.TemporaryDirectory() as work:
                proc = subprocess.run(
                    [str(exe), "--conversation-probe", work],
                    input=json.dumps({"turns": turns, "files": files}),
                    encoding="utf-8", capture_output=True, check=True, timeout=60,
                )
            return json.loads(proc.stdout)["runs"][0]["turns"]

        valid_src = (
            "class OracleValid(Base, metaclass=Meta):\n"
            "    def nums(self, a=0, b=0x_1, c=0b10_10, d=0o7_7, e=1.e2, f=1j, g=.5) -> int | str:\n"
            "        return a\n"
            "    def cont(self, x: int) -> \\\n"
            "        int | str \\\n"
            "        :\n"
            "        return x\n"
            "    def lam(self, fn: lambda a=1: a = 2) -> lambda b, c=0: b + c:\n"
            "        return fn\n"
        )
        tree = ast.parse(valid_src)
        cls_node = tree.body[0]
        methods = {n.name: n for n in cls_node.body if isinstance(n, ast.FunctionDef)}

        def_cls = probe({"valid.py": valid_src}, ["Where is OracleValid defined?"])
        self.assertEqual(def_cls[0]["delivered"]["state"], "completed")
        self.assertEqual(def_cls[0]["delivered"]["result"], "OracleValid")

        for mname, arity, expected_ret in [
            ("nums", 8, ast.get_source_segment(valid_src, methods["nums"].returns)),
            ("cont", 2, ast.get_source_segment(valid_src, methods["cont"].returns)),
            ("lam", 2, ast.get_source_segment(valid_src, methods["lam"].returns)),
        ]:
            t_par = probe({"valid.py": valid_src}, [f"Where is OracleValid.{mname}({arity} parameters) defined?", "What are its parameters?"])
            self.assertEqual(t_par[0]["delivered"]["state"], "completed", mname)
            self.assertEqual(t_par[1]["delivered"]["state"], "completed", mname)
            self.assertEqual(t_par[1]["delivered"]["result"], f"({valid_src.split(f'def {mname}(', 1)[1].split(')', 1)[0]})")
            t_ret = probe({"valid.py": valid_src}, [f"Where is OracleValid.{mname} defined?", "What is its return type?"])
            self.assertEqual(t_ret[0]["delivered"]["state"], "completed", mname)
            self.assertEqual(t_ret[1]["delivered"]["state"], "completed", mname)
            self.assertEqual(t_ret[1]["delivered"]["result"], expected_ret)

        bad_headers = [
            ("num_lead_def", "def bad(self, x=01): pass"),
            ("num_lead_ret", "def bad(self, x) -> 0_1: pass"),
            ("num_alpha_pann", "def bad(self, x: 123abc): pass"),
            ("num_dots_ret", "def bad(self, x) -> 1.2.3: pass"),
            ("num_exp_bad", "def bad(self, x=1e++2): pass"),
            ("op_aug_def", "def bad(self, x=a += 1): pass"),
            ("op_aug_pann", "def bad(self, x: int |= str): pass"),
            ("op_aug_ret", "def bad(self, x) -> int &= str: pass"),
            ("op_tilde_bin", "def bad(self, x=1 ~ 2): pass"),
            ("op_pipe_gap", "def bad(self, x) -> int | | str: pass"),
            ("op_amp_amp", "def bad(self, x=a && b): pass"),
            ("struct_if_no_else", "def bad(self, x) -> a if b: pass"),
            ("struct_walrus_ret", "def bad(self, x) -> a := 1: pass"),
            ("struct_lam_order", "def bad(self, x=lambda a=1, b: a): pass"),
            ("struct_dict_mix", "def bad(self, x={1: 2, 3}): pass"),
            ("struct_bytes_utf", "def bad(self, x=b\"é\"): pass"),
            ("struct_fstr", "def bad(self, x=f\"{1+}\"): pass"),
            ("escape_hex_truncated", r'''def bad(self, x="\x7"): pass'''),
            ("escape_bytes_hex", r'''def bad(self, x=b"\xGG"): pass'''),
            ("escape_unicode_truncated", r'''def bad(self, x: "\u03B") -> int: pass'''),
            ("escape_unicode_range", r'''def bad(self, x) -> "\U00FFFFFF": pass'''),
            ("escape_named_unknown", r'''def bad(self, x) -> "\N{UNKNOWN REVIEW GLYPH}": pass'''),
        ]
        for label, hdr in bad_headers:
            with self.subTest(malformed=label):
                with self.assertRaises(SyntaxError):
                    compile(hdr, f"<{label}>", "exec")
                src = f"class Probe:\n    {hdr}\n"
                for q in ("Where is Probe.bad(2 parameters) defined?", "Where is Probe.bad defined?"):
                    t_par = probe({"bad.py": src}, [q, "What are its parameters?"])
                    self.assertEqual(t_par[0]["delivered"]["state"], "needs clarification")
                    self.assertEqual(t_par[1]["delivered"]["state"], "needs clarification")
                    t_ret = probe({"bad.py": src}, [q, "What is its return type?"])
                    self.assertEqual(t_ret[0]["delivered"]["state"], "needs clarification")
                    self.assertEqual(t_ret[1]["delivered"]["state"], "needs clarification")

        for default, annotation in [
            (r'''"octal \101 and hex \x4f"''', r'''"\u03BB\U0001f642"'''),
            (r'''b"\xA9"''', r'''"\\unchanged"'''),
            (r'''rb"\xGG"''', r'''r"\u03B"'''),
        ]:
            literal = f"class Probe:\n    def value(self, label={default}) -> {annotation}:\n        pass\n"
            parsed = ast.parse(literal)
            compile(parsed, "<valid literal>", "exec")
            turns = ["Where is Probe.value(2 parameters) defined?", "What are its parameters?",
                     "Where is Probe.value defined?", "What is its return type?"]
            result = [turn["delivered"]["result"] for turn in probe({"valid.py": literal}, turns)]
            expected_return = ast.get_source_segment(literal, parsed.body[0].body[0].returns)
            self.assertEqual(result, ["value", f"(self, label={default})", "value", expected_return])

        named_literal = r'''def named() -> "\N{GREEK SMALL LETTER LAMDA}": pass'''
        ast.parse(named_literal)
        named = probe({"named.py": named_literal}, ["Where is named defined?", "What is its return type?"])
        self.assertTrue(all(t["delivered"]["state"] == "needs clarification" for t in named))

        rel_src = Path(distribution("python-dateutil").locate_file("dateutil/relativedelta.py")).read_text(encoding="utf-8")
        rel_turns = probe({"relativedelta.py": rel_src}, ["Where is relativedelta.normalized(1 parameter) defined?", "What are its parameters?"])
        self.assertEqual(rel_turns[0]["delivered"]["state"], "completed")
        self.assertEqual(rel_turns[1]["delivered"]["result"], "(self)")
        try:
            docx_src = Path(distribution("python-docx").locate_file("docx/document.py")).read_text(encoding="utf-8")
        except Exception:
            docx_src = None
        if docx_src is not None:
            docx_turns = probe({"document.py": docx_src}, ["Where is Document.iter_inner_content defined?", "What is its return type?"])
            self.assertEqual(docx_turns[0]["delivered"]["state"], "completed")
            self.assertEqual(docx_turns[1]["delivered"]["result"], "Iterator[Paragraph | Table]")

if __name__=="__main__": unittest.main()
