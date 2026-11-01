"""Assemble the F06 publication from scientific rerun 41302009289, offline.

Supported inputs are the extracted candidate and public pod artifacts of attempt
1, the C5 summary of run 41232286130, the phase-status file of the first dispatch
41292426657, and this checkout's git objects for the commits named below. Run from
any working directory:

    python3 -I docs/operations/evidence/issue-240-f06-verdict/make_results.py \
        --candidates DIR --pods DIR --c5-summary FILE --first-dispatch-status FILE \
        [--check] [--tables]

Only results/F06/2026-11-01.json is written. --check compares its exact bytes and
also requires the report results/F06/2026-11-01.md to contain the tables that
--tables prints, so no table row is typed by hand. It follows the B08 and E01
assembler in this repository's issue-240 verdict pull requests. It is not a second
scientific validator: the verifier's complete-valid projections stay authoritative,
and the 12 candidates and their judge receipts are preserved unchanged,
re-serialized with indentation. The closed decision rules below are transcribed
from the pinned scenarios/F06/expected.yaml and only confirm that the verifier's
proposed assessment follows from the candidates.

Failure conditions: a pinned input hash differs or an input is missing; the
configuration matrix differs from scenarios/F06/scenario.yaml; candidate identity,
status, determinism, outcome class, diagnostics or repeat projection differ from
the expected shape; a judge receipt does not bind the candidate hash or its pod
name and UID; a Job pod is missing, duplicated, restarted, non-zero, on another
service account or image digest, or its collection has gaps; phase-status.json
does not show F06 selected alone, every Job complete, a complete permission audit
with its first submission consumed, and every candidate accepted and
evidence-valid; the access-check projection is not complete, does not hash to the
audited projection, or its entries differ from the four intended grants; a
pod-log capture disagrees with its candidate on delivery, read record, key, value,
settings or error; a seeded location differs between a seed, its reads and the
configuration that reads it; a negative configuration records an acknowledged
send or a read record; the derived assessment differs from the verifier's; the
C5 summary is not complete and passing or its totals do not add up; the six
configured C5 targets are not compared between the two commits; the first
dispatch's F06 client stages are not all not_attempted; or --check output differs.

Only approved projections and provenance are read from pod JSON and captures; raw
pod documents, token-exchange log lines and statistics are never copied.
Regression example: copy the candidates directory, append a newline to
F06-C0.json and use it with --check: the pinned SHA-256 check fails before any
result can be written. The original inputs stay intact.
"""

import argparse
import base64
import collections
import hashlib
import json
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[4]
REPO = "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet"
RUN = REPO + "/actions/runs/41302009289"
RUN_ID = "probe-41302009289-1"
SOURCE = "961413f3fc81ec1b58b3fecc7f9e5122a8a21010"
CANDIDATE_ARTIFACT = RUN + "/artifacts/13082448520"
POD_ARTIFACT = RUN + "/artifacts/13082618179"
BATCH_JOB = RUN + "/job/124834736570"
STOP_JOB = RUN + "/job/124847230706"
FIRST_RUN = REPO + "/actions/runs/41292426657"
FIRST_POD_ARTIFACT = FIRST_RUN + "/artifacts/13078313354"
FIRST_RUN_ID = "probe-41292426657-1"
C5_RUN = REPO + "/actions/runs/41232286130"
C5_SUMMARY_ARTIFACT = C5_RUN + "/artifacts/13057507952"
C5_COMMIT = "2dc9715e3de176f7e1cd55eee9c251e43c29c746"
C5_CONFIG = "tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json"
DATE = "2026-11-01"

PINS = {
    "candidates": {
        "F06-C0-judge-completion.json": "f233ccadcf174ce693afea72cf4d4b5299b7762cb663d0ef7a83b662201323bb",
        "F06-C0.json": "de0ebec7779d42b0faee3f0f6d9cf175350ce981f59cad4e919a5aa46ca1573d",
        "F06-C1-judge-completion.json": "157b0cbff5aa1a60add37e9ef1a1981e464ec13750b5b4b5e38200d7150c1491",
        "F06-C1.json": "d641e2b5c877e6917a1f63e0e7ba36727e3636e1f0d286ba4b92fa8fa3544d2f",
        "F06-C10-judge-completion.json": "9e18735f7f4eb93fd733bb01fb7c8b76cb74b518c4032bb7a78979c5c40b44e2",
        "F06-C10.json": "bea75022ef71f8373788778eb1c8bef927ce30785a208beaacb1f6857b2542e0",
        "F06-C11-judge-completion.json": "d6ee99aaa13792281d49ac5cf1d5977026c6d8c616f5c8eeb7ea188669957e56",
        "F06-C11.json": "1f5f36f22574c974c4b3dfd9db1b04b1e0b5d9359216ea7e2597038e1ba3a0ec",
        "F06-C2-judge-completion.json": "47d06f5d23255ea28de812b3f9b050a18f465aeeaf2a757e6b13ee0c92c8212a",
        "F06-C2.json": "499bf0a363a947ccf9f7856219a00192ffea9d521d2ff7db971d9b57b12e0fdf",
        "F06-C3-judge-completion.json": "282dd3d284dee7e899e96b3d43a5bc29c8adb476393fdbb9a40eae31156c9384",
        "F06-C3.json": "99916a72d860049c522badff6f3a8f63b4df22f364a4004aaacf6616ec66d02c",
        "F06-C4-judge-completion.json": "3820eca12e15269961964da6d5cbd2ce912cfc660f126f51ec2e2247f3ae7b93",
        "F06-C4.json": "ec99f914966c4d6f3fff767c5a975a0a23b0419d36897d1c024a923d91af0967",
        "F06-C5-judge-completion.json": "e2b36fb7a96649bd5f3f8168c7095b1cc844d2b29dd2ce6c7e73389f4a82b41e",
        "F06-C5.json": "c656862904a753fe2a97e7ce7b3acc4183566829a7acfc75437f3f08ef1b3bf1",
        "F06-C6-judge-completion.json": "8db7e28dd4d1135edabe687402bda556a24deb7373d3fe50c7a1470bf17cef2f",
        "F06-C6.json": "7890c73347f65974451f36ec9b643c43b8b1bb874b8b3688d409b8bc37dad760",
        "F06-C7-judge-completion.json": "5d5e14bbd859182e6ef4a6dbe3d547d0caaf19a6fd8f412f8e9b81ab28b7ce8e",
        "F06-C7.json": "95039dfc3821af81cdb79558e2c99f487b75e757914d60dda5a0002bf0e048d7",
        "F06-C8-judge-completion.json": "96505255ddc368a4a8d34ff14eaea4043dacb611e6abb875073e291e3ec335aa",
        "F06-C8.json": "cfa11ea421a295048f4d6c6f207edad803cccc1c6e82b90aa30d5723e2e495af",
        "F06-C9-judge-completion.json": "131d0183ffe335b06aecbde1da96268c4f1225707b0140ca344e27bfe3e3c964",
        "F06-C9.json": "f7616b71c6d4968751c26f686bad651195f84ac7786327d18fa7832af274c7a9"
    },
    "pods": {
        "probe-41302009289-1-f06-c0-action/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c0-action/pods.json": "ddf40cc62114f471e9800f605e7de75b9ab826f091c7efedc017511e13955d7e",
        "probe-41302009289-1-f06-c0-action/probe-41302009289-1-f06-c0-action-5rnfz.containers.loadgen.log": "bd20b37ee1550b046b15404fb735e4cb569900379e9f40d33da48bf329794b3d",
        "probe-41302009289-1-f06-c0-judge/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c0-judge/pods.json": "100684c00a9c7f932050b2c6f6e7243b53fe452befd8f07cd0d417ac6640454b",
        "probe-41302009289-1-f06-c0-judge/probe-41302009289-1-f06-c0-judge-jkdmb.containers.verifier.log": "d11c3669f5a9fa3b5f26c474ff5515df5bf957c290e45dd2d06a43adacdddead",
        "probe-41302009289-1-f06-c0-read/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c0-read/pods.json": "63377b376ebf6d3ade0040fca78a2c93791af8db7b381d11401071f77361683b",
        "probe-41302009289-1-f06-c0-read/probe-41302009289-1-f06-c0-read-vd9kb.containers.probe.log": "721403e8c8524f72f35ad31cb9968b1dfa2e9b3c47873c6d8fa63ed5d2042147",
        "probe-41302009289-1-f06-c1-action/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c1-action/pods.json": "8f338f62fa9937ad98a9f21f5dd0a4933f46ecf1e1540726b737f86607c86717",
        "probe-41302009289-1-f06-c1-action/probe-41302009289-1-f06-c1-action-jkffm.containers.probe.log": "176196af507c270ea7d9f8d7f5659e51be5cb6590d1949dbbcb13ec2d746ae82",
        "probe-41302009289-1-f06-c1-judge/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c1-judge/pods.json": "f29e49113d0398721d9a9ed0ffc030a05df657bc939184251b89ee46a68c6ad9",
        "probe-41302009289-1-f06-c1-judge/probe-41302009289-1-f06-c1-judge-rv6rx.containers.verifier.log": "7f2cf4da28a2fa093a67a521628bc44b78dfbadcdb522a35dd54dd3596d6fceb",
        "probe-41302009289-1-f06-c1-read/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c1-read/pods.json": "d871ddc0823ef321389e293152bfaef4a849af5f6dab05df21b1f51a73927cb7",
        "probe-41302009289-1-f06-c1-read/probe-41302009289-1-f06-c1-read-sbs22.containers.probe.log": "f0c077780488fb5c2b9e4a4ddf341b326f0b068cc29c6bca570bdba2e54e4e95",
        "probe-41302009289-1-f06-c10-action/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c10-action/pods.json": "ffe2cc764d5c8128456b3e39a50fad256fae106765af964a8ffe1ca92b964ab0",
        "probe-41302009289-1-f06-c10-action/probe-41302009289-1-f06-c10-action-xk4l8.containers.enrichment-consumer.log": "6de72304ed451211cee43cad734773cef6dabb1cba9189c24db54f1ee9c31d78",
        "probe-41302009289-1-f06-c10-judge/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c10-judge/pods.json": "37bb6b02a24b470df98bf526ed92628468207b7749f56019d5de31c3c50bd03a",
        "probe-41302009289-1-f06-c10-judge/probe-41302009289-1-f06-c10-judge-bh9w7.containers.verifier.log": "c2d0fd41c47d5c702c4bcafa1e0d3520ebbe69741dc41bdb65831c36423b4b3c",
        "probe-41302009289-1-f06-c11-action/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c11-action/pods.json": "bded8905dd1fa9231c36b70554e954c295dd16507f6432a84efd7d85eca11c00",
        "probe-41302009289-1-f06-c11-action/probe-41302009289-1-f06-c11-action-5rczh.containers.enrichment-consumer.log": "4dca387c568f3e9d505f54ec855e590a81b853052b085e5ce93096b6645bc08a",
        "probe-41302009289-1-f06-c11-judge/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c11-judge/pods.json": "7f7e21f2d462da07193a7a145c5d70e95d07a8a8497b17d53172c0d81d11e274",
        "probe-41302009289-1-f06-c11-judge/probe-41302009289-1-f06-c11-judge-j7rtl.containers.verifier.log": "50dc265b0271bdb947e4e6378f55022d059188a6dec648bbc26114a47e03d731",
        "probe-41302009289-1-f06-c2-action/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c2-action/pods.json": "d635ad1f80a910515b1339fcefd94e7ef0ea0de731d9a91286c0bbfb1db82b75",
        "probe-41302009289-1-f06-c2-action/probe-41302009289-1-f06-c2-action-vjk97.containers.loadgen.log": "a6968590c5535ffd5ee989fe9e5bf957004788652ae80bd6149497eee04dfda4",
        "probe-41302009289-1-f06-c2-judge/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c2-judge/pods.json": "28d3b8a7fa3a9e2845b57034926ae5353df1f0f59cbee84e5b383d38ad39c15a",
        "probe-41302009289-1-f06-c2-judge/probe-41302009289-1-f06-c2-judge-2gd6t.containers.verifier.log": "0cc7acb1abe21c2039a7e0f53ef2d2c9f34ecb344f6e0f36a33f7727ac3902c3",
        "probe-41302009289-1-f06-c2-read/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c2-read/pods.json": "166fb403d40d0d91c10ada6d93d4443568b6ba870fb20bb9607487c20fbee76f",
        "probe-41302009289-1-f06-c2-read/probe-41302009289-1-f06-c2-read-sjq9l.containers.probe.log": "552706e31827fb9ea80cebc582052b231b092f01ee678948c6fc3dcb8778488a",
        "probe-41302009289-1-f06-c3-action/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c3-action/pods.json": "b397d3b4e88fd3646c9753fcc9c9e8d36214c1efd3c647a95c1098163f651993",
        "probe-41302009289-1-f06-c3-action/probe-41302009289-1-f06-c3-action-ltvvl.containers.probe.log": "4402c8000960467aa887433b109f86e597727adbd8466e160ceda6bd4977af08",
        "probe-41302009289-1-f06-c3-judge/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c3-judge/pods.json": "e5bb00a2e6451e2a6e6c4d0313558bc55cf4dd327afaea4ccf6af665f85090d3",
        "probe-41302009289-1-f06-c3-judge/probe-41302009289-1-f06-c3-judge-brcvj.containers.verifier.log": "2b2aec2599e164061a82369953fa08780c46f1102949c5722d97ba288164602b",
        "probe-41302009289-1-f06-c3-read/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c3-read/pods.json": "ca93a3ba5b9a67f0c1196a7ef2d241122c7888ac04835b7798222366c4efe33f",
        "probe-41302009289-1-f06-c3-read/probe-41302009289-1-f06-c3-read-sbp6n.containers.probe.log": "ee5078be4be510c9690c68975420c21a6b5c18981eb875eb8e4deaf5a803bcbe",
        "probe-41302009289-1-f06-c4-action/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c4-action/pods.json": "8f32986d49f8cc3f7a687fde7bee13008668839740382e1ea6f7b3fc4ac0af53",
        "probe-41302009289-1-f06-c4-action/probe-41302009289-1-f06-c4-action-w5swb.containers.loadgen.log": "9a2e811a3b09fcfe7593fdfdfed9814f2281ca58938afea602a60468caacd8fd",
        "probe-41302009289-1-f06-c4-judge/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c4-judge/pods.json": "e4dd0f605b353d2a85b46462c569ec1219ef3951d6a703c6b0ecfe5e81b540af",
        "probe-41302009289-1-f06-c4-judge/probe-41302009289-1-f06-c4-judge-n24bt.containers.verifier.log": "513ec9ffe29ce03a62267596b1e1b076f69239f49d2629ef27dcfa4080d4623d",
        "probe-41302009289-1-f06-c4-read/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c4-read/pods.json": "0de0e748010c6d050343bcc95fc7af589043cd27b806568d72d031f6f8547098",
        "probe-41302009289-1-f06-c4-read/probe-41302009289-1-f06-c4-read-dtlbw.containers.probe.log": "0eebfbc332da86ce9a905d44dbbfe54fc410a66c92c2926d7add759fd6af5bb9",
        "probe-41302009289-1-f06-c5-action/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c5-action/pods.json": "d5f294af70461fbc54244f92798f3e25e46061fcddc0b4e2634d4c727b4a8e79",
        "probe-41302009289-1-f06-c5-action/probe-41302009289-1-f06-c5-action-bgs58.containers.probe.log": "264174df0258b5628ab2eaab478593d8273e64768a402d4959c032e4d66839c8",
        "probe-41302009289-1-f06-c5-judge/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c5-judge/pods.json": "f8692efda98209438910eba026c2c9d70a1e7f0a54b3bec8f423ce361c350446",
        "probe-41302009289-1-f06-c5-judge/probe-41302009289-1-f06-c5-judge-tbrkm.containers.verifier.log": "118f3576389cc09dfc3bfcf905b26ce6b74b8bc37178d4ff6952447fd7699424",
        "probe-41302009289-1-f06-c5-read/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c5-read/pods.json": "3349723b8f94738bf7a710ea05bb07b29980e81683dbd6256f82defd1134d456",
        "probe-41302009289-1-f06-c5-read/probe-41302009289-1-f06-c5-read-gdrmn.containers.probe.log": "13c7c8417fea0d9cf8ae14398be68fbe4e28b8b8270c177a509d99b4b66641fb",
        "probe-41302009289-1-f06-c6-action/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c6-action/pods.json": "8c2590fbccdb59fc30b1575f6f47d060c87289cefeeda8a1804b8b9e4fba729c",
        "probe-41302009289-1-f06-c6-action/probe-41302009289-1-f06-c6-action-cssr4.containers.probe.log": "a15e764e20228b80c781a3160602f361f0a4306aa5583a0c42276ce8d38ad6fe",
        "probe-41302009289-1-f06-c6-judge/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c6-judge/pods.json": "67105d5f157a92b4fe962f489d2f3cbb2f8aad5e90bda18b9d111d010252038e",
        "probe-41302009289-1-f06-c6-judge/probe-41302009289-1-f06-c6-judge-gl684.containers.verifier.log": "3dda5af31ec053c85ae924c5ff1a2f1a121efabadd34cd72a1c19346c9f0c4f7",
        "probe-41302009289-1-f06-c6-seed/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c6-seed/pods.json": "430ac2745d1d4ba370fe56fa657df6156d816a4542f0f5666435d226c9b76595",
        "probe-41302009289-1-f06-c6-seed/probe-41302009289-1-f06-c6-seed-ksf79.containers.loadgen.log": "9aa26fb3c6927aab7b4ce42a00182a64f4acf7ac9029f5ba6d5851d1924c551f",
        "probe-41302009289-1-f06-c7-action/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c7-action/pods.json": "c15a35f1e595b3da2c801837aa6c87f0f8816803d58574f1654fa2d400826295",
        "probe-41302009289-1-f06-c7-action/probe-41302009289-1-f06-c7-action-rqhqk.containers.probe.log": "255f9a717a7ca5fd54f0f2ce132037102602c665e13ecffd9616cd8d20108f84",
        "probe-41302009289-1-f06-c7-judge/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c7-judge/pods.json": "4e1a29a728eb3df24a6b9f33c0cddf1781be85488f1ac371ea6bd790d817d7f2",
        "probe-41302009289-1-f06-c7-judge/probe-41302009289-1-f06-c7-judge-rlxdc.containers.verifier.log": "28d9b00b05142ddb7f79040f2ee80a71596f11af44ab273ab2cc30d0ca80078c",
        "probe-41302009289-1-f06-c7-seed/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c7-seed/pods.json": "b248974a8fba0386938e2b50141cbdcf441363e8363bc28a1eeb10524097f177",
        "probe-41302009289-1-f06-c7-seed/probe-41302009289-1-f06-c7-seed-mfg7n.containers.enrichment-consumer.log": "5632a43b886d0bacff0f5c82ba95ff81ef659b05429782f8aa81b2eafcf4b248",
        "probe-41302009289-1-f06-c8-action/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c8-action/pods.json": "17e8731561f3cc4991511eb2c104b06935dc1e4fb904047f2a4548f80bb4be03",
        "probe-41302009289-1-f06-c8-action/probe-41302009289-1-f06-c8-action-nq85d.containers.probe.log": "1552bca34d717146a365bc70de2ad5bbff0a493316c21152e2a8104b86bfef78",
        "probe-41302009289-1-f06-c8-judge/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c8-judge/pods.json": "dbd539317309f07d5b5cb08abfb311789d33ce415c6b71aee23c8950af2525d3",
        "probe-41302009289-1-f06-c8-judge/probe-41302009289-1-f06-c8-judge-zcznb.containers.verifier.log": "6e53f13a248054023845a738350efe0ef794f16307070093626fe56337e64cf2",
        "probe-41302009289-1-f06-c8-seed/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c8-seed/pods.json": "b95bd53a24be0171c44607f87a5de1aae977f1195c893d7c01b2a556587a6a7c",
        "probe-41302009289-1-f06-c8-seed/probe-41302009289-1-f06-c8-seed-zb4f9.containers.enrichment-consumer.log": "f78e97e02255ea1b151c4c2a9ee70a5355f7f5d668afa8242cbd3c60b1a7f7e7",
        "probe-41302009289-1-f06-c9-action/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c9-action/pods.json": "cf7c53cf1daf465ba92b6ee9088d6eb02c70cbc29fa8de9d17cd4c1c09618404",
        "probe-41302009289-1-f06-c9-action/probe-41302009289-1-f06-c9-action-ch87r.containers.enrichment-consumer.log": "5fa795436e19c562e68e92bc60e20a5c6433fc325d6a8a9f7d11f8e70a8dc5c3",
        "probe-41302009289-1-f06-c9-judge/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
        "probe-41302009289-1-f06-c9-judge/pods.json": "e391afeb802218fe0d204749a833abac362d6df8a6f79c36c2782e1ae36a7c4e",
        "probe-41302009289-1-f06-c9-judge/probe-41302009289-1-f06-c9-judge-mj9l2.containers.verifier.log": "efb475abe817ab66c98897955833555def892f6480c6cb8f839fbd01576f2e75"
    },
    "top": {
        "phase-status.json": "890f9e6f8beedc18bb6391522a890352acb59462ac84648f9f27eb02d19b91cc",
        "probe-v2-access-check.json": "f2ffab1ceb198902be7c6b8a4577bc251bb34ab81be2e263c05760a923d04684",
        "probe-v2-batch.json": "e4b86b096f68d974f58fa44e6ce857ce7914c49eafb16a3819447ba4cd6f5d5c"
    },
    "sources": {
        "scenarios/F06/scenario.yaml": "3ca313515ec897c487a71c86e7e6bb2d3dbfdc6c63577726929b3509428133b0",
        "scenarios/F06/expected.yaml": "0327312ce5edccdab8c9ea9662661189786a85ad434a39b772be15f471e6cc9b",
        "infra/cluster/aks.tf": "969762e923eae813525207d50f5689de9fbed20c0c4c2c274367bcb54462a8a2",
        "infra/ephemeral/eventhubs.tf": "ce85f8b68fdad25ba052185b55d3518ce64b687b641728540a17bad273bed49d",
        "infra/ephemeral/identity.tf": "867ab14760fe5f6212adb9f2756e3384f8334ca331c7210ce7b59aa92cfb2b8c"
    },
    "c5_summary": "3f1ba194816bab19b31b900c9084839d2f8250359b115bfe2df0304f505c6bec",
    "first_dispatch_phase_status": "90bc97993bf439904ffc58782e2f05f0774c3c26b6014c63eabe392e890ea7b1"
}

# Read from the batch job log of run 41302009289 (plan and apply of the ephemeral
# layer, then its destroy) and the job metadata; the log itself is not a pinned input.
ENVIRONMENT = {
    "region": "uksouth",
    "event_hubs_sku": "Premium",
    "pu_count": 1,
    "partition_count_planned": {"document-events": 32, "document-state": 32, "document-events-dlq": 4},
    "node_size": "Standard_D4ds_v5",
    "node_size_evidence": "inferred_from_pinned_configuration_and_no_change_cluster_plan",
    "run_created_utc": "2026-11-01T11:28:08Z",
    "batch_job_started_utc": "2026-11-01T11:29:45Z",
    "cluster_plan_no_changes_utc": "2026-11-01T11:37:50Z",
    "ephemeral_plan_utc": "2026-11-01T11:38:27Z",
    "permission_check_complete_log_utc": "2026-11-01T11:41:29Z",
    "ephemeral_destroy_plan_utc": "2026-11-01T12:21:53Z",
    "ephemeral_state_empty_log_utc": "2026-11-01T12:23:42Z",
    "batch_job_completed_utc": "2026-11-01T12:23:47Z",
    "batch_stop_started_utc": "2026-11-01T12:23:52Z",
    "aks_stopped_log_utc": "2026-11-01T12:24:41Z",
    "sources": [
        BATCH_JOB,
        STOP_JOB,
        REPO + "/blob/" + SOURCE + "/infra/cluster/aks.tf",
        REPO + "/blob/" + SOURCE + "/infra/ephemeral/eventhubs.tf",
    ],
}

POSITIVE = ("C0", "C1", "C3", "C5", "C6", "C7", "C8", "C9")
NEGATIVE = ("C2", "C4", "C10", "C11")
CONFIGS = tuple("C%d" % n for n in range(12))
TOPICS = {"events": "document-events", "state": "document-state", "dlq": "document-events-dlq"}

# Transcribed from scenarios/F06/scenario.yaml (cross-checked below). "reads" names
# the configuration whose seed this configuration reads.
MATRIX = {
    "C0": dict(action="produce_one", account="loadgen", topic="document-events", seed=None),
    "C1": dict(action="produce_one", account="probe", topic="document-events", seed=None),
    "C2": dict(action="produce_one", account="loadgen", topic="document-state", seed=None),
    "C3": dict(action="produce_one", account="probe", topic="document-state", seed=None),
    "C4": dict(action="produce_one", account="loadgen", topic="document-events-dlq", seed=None),
    "C5": dict(action="produce_one", account="probe", topic="document-events-dlq", seed=None),
    "C6": dict(action="assign_consume", account="probe", topic="document-events", seed=("C6", "loadgen")),
    "C7": dict(action="assign_consume", account="probe", topic="document-state", seed=("C7", "enrichment-consumer")),
    "C8": dict(action="assign_consume", account="probe", topic="document-events-dlq", seed=("C8", "enrichment-consumer")),
    "C9": dict(action="assign_consume", account="enrichment-consumer", topic="document-events", seed=("C6", None)),
    "C10": dict(action="assign_consume", account="enrichment-consumer", topic="document-state", seed=("C7", None)),
    "C11": dict(action="assign_consume", account="enrichment-consumer", topic="document-events-dlq", seed=("C8", None)),
}
SUCCESS = {"result": "success", "error_code": None, "is_fatal": None, "origin": None}
ERROR29 = {"result": "error", "error_code": 29, "is_fatal": False, "origin": "broker"}

# The intended positive data-grant table of the approved plan (Revision 7, "Supported
# audit and interpretation"). Only true cells are listed; every other cell is false.
INTENDED_GRANTS = (
    ("loadgen", "document-events", "send"),
    ("enrichment-consumer", "document-events", "receive"),
    ("enrichment-consumer", "document-state", "send"),
    ("enrichment-consumer", "document-events-dlq", "send"),
)
ALL_ACCOUNT_TARGETS = tuple((a, t) for a in ("loadgen", "enrichment-consumer")
                            for t in ("document-events", "document-state", "document-events-dlq"))

ERROR_CODE_29 = {
    "name": "TOPIC_AUTHORIZATION_FAILED",
    "librdkafka_enum": "RD_KAFKA_RESP_ERR_TOPIC_AUTHORIZATION_FAILED",
    "librdkafka_description": "Broker: Topic authorization failed",
    "dotnet_enum": "ErrorCode.TopicAuthorizationFailed",
    "sources": [
        "https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka.h#L494-L495",
        "https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka.c#L578-L579",
        "https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ErrorCode.cs#L505-L508",
    ],
}


def fail(message):
    raise ValueError(message)


def require(condition, message):
    if not condition:
        fail(message)


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def pinned_bytes(path, digest):
    require(path.is_file(), "missing input " + str(path))
    data = path.read_bytes()
    require(sha256(data) == digest, str(path) + " does not match its pinned SHA-256")
    return data


def git(*args):
    return subprocess.check_output(("git", "-C", str(ROOT)) + args, stderr=subprocess.STDOUT)


def load_json(path, digest):
    return json.loads(pinned_bytes(path, digest).decode("utf-8"))


def b64(value):
    return None if value is None else base64.b64decode(value).decode("ascii")


def capture_line(path, digest):
    text = pinned_bytes(path, digest).decode("utf-8")
    lines = [l for l in text.splitlines() if l.startswith("PARCHWELL_PROBE_V2_CAPTURE ")]
    require(len(lines) == 1, str(path) + " does not hold exactly one capture line")
    return json.loads(lines[0][len("PARCHWELL_PROBE_V2_CAPTURE "):])


def stages_of(config):
    n = int(config[1:])
    if n <= 5:
        return ("action", "read")
    if n <= 8:
        return ("seed", "action")
    return ("action",)


def job_name(config, stage):
    return "%s-f06-%s-%s" % (RUN_ID, config.lower(), stage)


def check_matrix():
    """The MATRIX above equals scenarios/F06/scenario.yaml at the run source and in this checkout."""
    import yaml
    for text in (git("show", SOURCE + ":scenarios/F06/scenario.yaml"), (ROOT / "scenarios/F06/scenario.yaml").read_bytes()):
        doc = yaml.safe_load(text)
        require(doc["id"] == "F06" and doc["repeats"] == 5, "scenario identity or repeats differ")
        cfgs = doc["probe_v2"]["configurations"]
        require(sorted(cfgs) == sorted(CONFIGS), "scenario configurations differ")
        for config, want in MATRIX.items():
            got = cfgs[config]
            require(got["action"] == want["action"] and got["service_account"] == want["account"]
                    and got["topic"] == want["topic"] and got["partition_mode"] == "explicit"
                    and got["partition"] == 0 and got["value_kind"] == "marker",
                    "scenario %s differs from the transcribed matrix" % config)
            require(got["role"] == ("LoadGenProducer" if want["action"] == "produce_one" else "VerifierConsumer"),
                    "scenario role of %s differs" % config)
            require(got["key_rule"] == ("f06_unique" if want["action"] == "produce_one" else "f06_seed"), "key rule differs")
            require(got["window_ms"] == (330000 if want["action"] == "produce_one" else 120000), "window differs")
            require(got["read_rule"] == ("delivery_location" if want["action"] == "produce_one" else "seeded_location"),
                    "read rule differs")
    return {c: {"action": m["action"], "service_account": m["account"], "topic": m["topic"]} for c, m in MATRIX.items()}


def pod_info(pods_dir, job, role):
    prefix = job + "/"
    members = {k: v for k, v in PINS["pods"].items() if k.startswith(prefix)}
    require(len(members) == 3, job + " does not have exactly three pinned members")
    status = load_json(pods_dir / (prefix + "collection-status.json"), members[prefix + "collection-status.json"])
    require(status == {"complete": True, "gaps": []}, job + " pod collection is incomplete")
    doc = load_json(pods_dir / (prefix + "pods.json"), members[prefix + "pods.json"])
    require(len(doc["items"]) == 1, job + " does not have exactly one pod")
    pod = doc["items"][0]
    statuses = pod["status"]["containerStatuses"]
    require(len(statuses) == 1 and statuses[0]["name"] == role, job + " container differs from " + role)
    term = statuses[0]["state"]["terminated"]
    require(term["exitCode"] == 0 and term["reason"] == "Completed" and statuses[0]["restartCount"] == 0,
            job + " pod did not complete once with exit 0")
    log_names = [k for k in members if k.endswith(".log")]
    require(len(log_names) == 1, job + " does not have exactly one pinned log")
    name = pod["metadata"]["name"]
    require(log_names[0] == prefix + name + ".containers." + role + ".log", job + " log name differs from its pod")
    return {
        "job": job,
        "pod": name,
        "pod_uid": pod["metadata"]["uid"],
        "service_account": pod["spec"]["serviceAccountName"],
        "container": role,
        "container_started_at": term["startedAt"],
        "container_finished_at": term["finishedAt"],
        "exit_code": term["exitCode"],
        "image_digest": statuses[0]["imageID"].split("@", 1)[1],
        "pod_log_member": log_names[0],
        "pod_json_member": prefix + "pods.json",
        "_log": pods_dir / log_names[0],
        "_log_hash": members[log_names[0]],
    }


def derive(candidates):
    """Closed rules transcribed from scenarios/F06/expected.yaml (see PINS['sources'])."""
    if any(c["security_finding"] for c in candidates.values()):
        return None, None, "security_finding_requires_hari"
    for config in CONFIGS:
        if candidates[config]["status"] != "complete_valid":
            return None, None, "incomplete_configuration"
    for config in CONFIGS:
        require(candidates[config]["deterministic"] is True, "F06 %s is not deterministic" % config)
    if len({json.dumps(r["outcome_class"], sort_keys=True) for c in candidates.values() for r in c["repeats"]
            if False}) > 1:
        fail("unreachable")
    for config in CONFIGS:
        if candidates[config]["outcome_class"].get("origin") == "construction":
            return "untestable", "undecided", "construction_origin"
    for config in POSITIVE:
        if candidates[config]["outcome_class"]["result"] != "success":
            return "untestable", "undecided", "failed_control"
    return "undocumented", "holds", None


def verify_candidates(cand_dir, audit_sha):
    candidates, receipts = {}, {}
    for config in CONFIGS:
        name = "F06-%s.json" % config
        data = pinned_bytes(cand_dir / name, PINS["candidates"][name])
        cand = json.loads(data.decode("utf-8"))
        rname = "F06-%s-judge-completion.json" % config
        receipt = load_json(cand_dir / rname, PINS["candidates"][rname])
        want_class = SUCCESS if config in POSITIVE else ERROR29
        for key, value in (("schema_version", 2), ("mode", "single-action-v2"), ("synthetic", True),
                           ("run_id", RUN_ID), ("scenario_id", "F06"), ("configuration_id", config),
                           ("source_commit", SOURCE), ("managed_version", "2.16.0.0"),
                           ("native_version", "2.16.0"), ("status", "complete_valid"),
                           ("deterministic", True), ("adr012_counts", "not_applicable"),
                           ("diagnostics", ["full_experiment", "permission_audit_sha256:" + audit_sha]),
                           ("security_finding", False)):
            require(cand[key] == value, "%s %s differs: %r" % (name, key, cand[key]))
        require(cand["outcome_class"] == want_class, name + " outcome class differs")
        require(sorted(cand["image_digests"]) == ["loadgen", "verifier"], name + " image digest keys differ")
        require([r["repeat"] for r in cand["repeats"]] == [1, 2, 3, 4, 5], name + " repeat ordinals differ")
        for repeat in cand["repeats"]:
            require(repeat["status"] == "complete_valid" and repeat["reasons"] == [], name + " repeat is not complete_valid")
            require(repeat["outcome_class"] == want_class, name + " repeat outcome class differs")
        require(receipt["candidate_sha256"] == sha256(data), rname + " does not bind the candidate bytes")
        for key, value in (("schema_version", 2), ("mode", "single-action-v2-judge"), ("run_id", RUN_ID),
                           ("scenario_id", "F06"), ("configuration_id", config), ("source_commit", SOURCE),
                           ("handler_exit_code", 0), ("cleanup_completed", True)):
            require(receipt[key] == value, "%s %s differs" % (rname, key))
        require(receipt["candidate_name"] == "%s/single-action-v2/F06/%s/candidate.json" % (RUN_ID, config),
                rname + " candidate name differs")
        candidates[config], receipts[config] = cand, receipt
    require(len({json.dumps(c["image_digests"], sort_keys=True) for c in candidates.values()}) == 1, "image digests differ")
    for config in CONFIGS[:-1]:
        require(candidates[config]["item_assessment"] is None, "nonfinal candidate %s carries an assessment" % config)
    return candidates, receipts


def verify_phase_status(pods_dir, candidates, receipts):
    status = load_json(pods_dir / "phase-status.json", PINS["top"]["phase-status.json"])
    require(status["run_id"] == RUN_ID and status["phase"] == "single-action", "phase status identity differs")
    require(status["safety_stop"] is False and status["not_selected"] == ["B08", "E01"] and status["candidate"] is None,
            "phase status safety or selection differs")
    audit = status["permission_audit"]
    require(audit["status"] == "complete" and audit["run_id"] == RUN_ID and audit["first_submission_consumed"] is True
            and audit["clock"] == "monotonic" and "blocked" not in audit, "phase status permission audit is not complete")
    modes = status["modes"]
    require(all(m["item"] == "F06" for m in modes), "phase status holds another item")
    require(len(modes) == 33, "phase status does not hold 33 Jobs")
    jobs = {}
    for config in CONFIGS:
        for stage in stages_of(config) + ("judge",):
            rows = [m for m in modes if m["configuration"] == config and m["stage"] == stage]
            require(len(rows) == 1 and rows[0]["outcome"] == "complete", "F06 %s %s is not complete" % (config, stage))
            name = job_name(config, stage)
            require(rows[0]["job"] == name, "phase status Job name differs")
            if stage == "judge":
                role = "verifier"
            else:
                slots = 0 if (config in ("C2", "C4") and stage == "read") else 5
                require(rows[0]["capture_complete"] is True and rows[0]["admitted_client_slots"] == slots,
                        "F06 %s %s capture or admitted slots differ" % (config, stage))
                if stage == "seed":
                    role = MATRIX[config]["seed"][1]
                elif stage == "read":
                    role = "probe"
                else:
                    role = MATRIX[config]["account"]
                require(rows[0]["role"] == role, "phase status role of %s differs" % name)
            info = pod_info(pods_dir, name, role)
            digest = candidates[config]["image_digests"]["verifier" if stage == "judge" else "loadgen"]
            require(info["image_digest"] == digest, name + " ran another image digest")
            require(info["service_account"] == role, name + " ran as another service account")
            jobs[(config, stage)] = info
        judge = jobs[(config, "judge")]
        require(receipts[config]["pod_name"] == judge["pod"] and receipts[config]["pod_uid"] == judge["pod_uid"]
                and receipts[config]["job_name"] == judge["job"], "judge receipt does not bind its pod")
    require(len(jobs) == 33, "job count differs")
    entries = status["candidates"]
    require(len(entries) == 12 and [c["configuration"] for c in entries] == list(CONFIGS)
            and all(c["item"] == "F06" and c["accepted"] is True and c["evidence_valid"] is True and c["smoke_valid"] is False
                    and c["candidate"] == "F06-%s.json" % c["configuration"]
                    and c["judge_completion"] == "F06-%s-judge-completion.json" % c["configuration"] for c in entries),
            "phase status does not accept all 12 candidates")
    batch = load_json(pods_dir / "probe-v2-batch.json", PINS["top"]["probe-v2-batch.json"])
    require(batch["selected_items"] == ["F06"] and batch["run_id"] == RUN_ID and batch["source_commit"] == SOURCE
            and batch["jobs"] == 33 and batch["evidence_passed"] is True and batch["mode"] == "full_experiment",
            "batch summary differs")
    return jobs, status, batch


def verify_audit(pods_dir, status):
    raw = pinned_bytes(pods_dir / "probe-v2-access-check.json", PINS["top"]["probe-v2-access-check.json"])
    audit = status["permission_audit"]
    require(sha256(raw) == audit["projection_sha256"], "access-check projection does not hash to the audited projection")
    doc = json.loads(raw.decode("utf-8"))
    require(doc["schema_version"] == 1 and doc["mode"] == "azure_rbac_configuration_check" and doc["run_id"] == RUN_ID
            and doc["source_commit"] == SOURCE and doc["status"] == "complete" and doc["reasons"] == []
            and doc["unsupported"] == [], "access-check projection is not complete")
    require(doc["observed"] == {"requests": 6, "api_versions": {"arm": "2022-04-01", "graph": "v1.0"}}, "access-check requests differ")
    require(doc["coverage"]["targets"] == ["document-events", "document-events-dlq", "document-state"], "access-check targets differ")
    cov = doc["coverage"]["accounts"]
    require(sorted(cov) == ["enrichment-consumer", "loadgen"], "access-check accounts differ")
    for account, c in cov.items():
        require(c["membership_complete"] is True and c["groups"] == [] and c["directory_roles"] == []
                and len(c["membership_pages"]) == 1 and len(c["assignment_collections"]) == 1
                and c["assignment_collections"][0]["subject"] == account and c["assignment_collections"][0]["complete"] is True
                and len(c["assignment_collections"][0]["pages"]) == 1, "access-check coverage of %s is incomplete" % account)
    require(len(doc["coverage"]["definitions"]) == 2, "access-check definition count differs")
    got = sorted((e["account"], e["target"], e["operation"]) for e in doc["entries"])
    require(got == sorted(INTENDED_GRANTS), "access-check entries differ from the intended grant table")
    for e in doc["entries"]:
        require(e["subject"] == e["account"] and e["scope_class"] == "event_hub"
                and e["role"] == {"send": "sender", "receive": "receiver"}[e["operation"]], "access-check entry shape differs")
    aliases = {e["definition_alias"] for e in doc["entries"]}
    require(aliases == {d["alias"] for d in doc["coverage"]["definitions"]}, "access-check definitions differ from the entries")
    require(len({e["assignment_alias"] for e in doc["entries"]}) == 4, "access-check assignments are not four distinct")
    grid = []
    for account, target in ALL_ACCOUNT_TARGETS:
        grid.append({"account": account, "target": target,
                     "send": (account, target, "send") in INTENDED_GRANTS,
                     "receive": (account, target, "receive") in INTENDED_GRANTS})
    return {
        "status": doc["status"], "reasons": doc["reasons"], "requests": doc["observed"]["requests"],
        "api_versions": doc["observed"]["api_versions"], "projection_sha256": audit["projection_sha256"],
        "boot_identity_clock": audit["clock"], "first_submission_consumed": audit["first_submission_consumed"],
        "submission_age_ms": audit["submission_age_ms"], "source_limit_first_admission_age_ms": doc["source_limits"]["first_admission_age_ms"],
        "started_unix_ms": doc["started_unix_ms"], "completed_unix_ms": doc["completed_unix_ms"],
        "audited_accounts": ["enrichment-consumer", "loadgen"], "audited_targets": doc["coverage"]["targets"],
        "group_memberships": {a: 0 for a in cov}, "directory_roles": {a: 0 for a in cov},
        "entries": [{k: e[k] for k in ("account", "target", "operation", "role", "scope_class")} for e in
                    sorted(doc["entries"], key=lambda e: (e["account"], e["target"], e["operation"]))],
        "unsupported": doc["unsupported"], "intended_grants_true_cells": grid,
        "not_audited": "the probe identity (namespace-level Sender and Receiver exception) and the verifier; deny assignments, SAS and other authorization mechanisms",
    }


def verify_captures(jobs, candidates):
    run = RUN_ID
    caps = {}
    for (config, stage), info in sorted(jobs.items()):
        if stage == "judge":
            continue
        cap = capture_line(info["_log"], info["_log_hash"])
        for key, value in (("run_id", RUN_ID), ("scenario_id", "F06"), ("configuration_id", config), ("stage", stage),
                           ("source_commit", SOURCE), ("job_name", info["job"]), ("pod_name", info["pod"]),
                           ("pod_uid", info["pod_uid"]), ("service_account", info["service_account"]),
                           ("handler_exit_code", 0), ("cleanup_completed", True), ("host_cleanup_completed", True),
                           ("managed_version", "2.16.0.0"), ("native_version", "2.16.0")):
            require(cap[key] == value, "F06 %s %s capture %s differs" % (config, stage, key))
        require([r["repeat"] for r in cap["repeats"]] == [1, 2, 3, 4, 5], "capture ordinals differ")
        caps[(config, stage)] = cap

    def produce_checks(config, stage, repeat, topic, key, success):
        require(repeat["admission"] == "required" and repeat["action"] == "produce_one" and repeat["terminal"] is True,
                "%s %s repeat admission differs" % (config, stage))
        rq = repeat["requested"]
        require(rq["topic"] == topic and rq["partition_mode"] == "explicit" and rq["partition"] == 0
                and b64(rq["key_base64"]) == key and rq["value_base64"] == rq["key_base64"], "%s %s request differs" % (config, stage))
        require(repeat["settings"]["partitioner"] == "consistent_random", "%s %s partitioner differs" % (config, stage))
        if success:
            d = repeat["delivery"]
            require(repeat["result"] == "success" and repeat["error"] is None and d["error"] is None
                    and d["persistence_status"] == "Persisted" and d["topic"] == topic and d["partition"] == 0,
                    "%s %s delivery differs" % (config, stage))
        else:
            e = repeat["error"]
            require(repeat["result"] == "error" and repeat["delivery"] is None and repeat["read_observation"]["record"] is None
                    and (e["code"], e["is_fatal"], e["origin"]) == (29, False, "broker"), "%s %s error differs" % (config, stage))

    def consume_checks(config, repeat, loc, ok):
        require(repeat["admission"] == "required" and repeat["action"] == "assign_consume" and repeat["terminal"] is True,
                "%s consume admission differs" % config)
        rq = repeat["requested"]
        require((rq["topic"], rq["partition_mode"], rq["partition"], rq["offset"]) == (loc["topic"], "explicit", 0, loc["offset"])
                and rq["key_base64"] == loc["key_base64"] and rq["value_base64"] == loc["value_base64"],
                "%s consume did not request the delivered location" % config)
        require(repeat["settings"]["group.id"] == "parchwell-verifier" and repeat["settings"]["auto.offset.reset"] == "error"
                and repeat["settings"]["enable.auto.commit"] == "false", "%s consumer settings differ" % config)
        if ok:
            rec = repeat["read_observation"]["record"]
            require(repeat["result"] == "success" and repeat["error"] is None and rec is not None
                    and (rec["topic"], rec["partition"], rec["offset"]) == (loc["topic"], 0, loc["offset"])
                    and rec["key_base64"] == loc["key_base64"] and rec["value_base64"] == loc["value_base64"],
                    "%s read record is not the delivered location, key and value" % config)
        else:
            e = repeat["error"]
            require(repeat["result"] == "error" and repeat["read_observation"]["record"] is None
                    and repeat["read_observation"]["records_seen"] == 0 and repeat["delivery"] is None
                    and (e["code"], e["is_fatal"], e["origin"]) == (29, False, "broker"), "%s consume error differs" % config)

    locations = {}
    for n in range(1, 6):
        for config in ("C0", "C1", "C2", "C3", "C4", "C5"):
            act, rd = caps[(config, "action")]["repeats"][n - 1], caps[(config, "read")]["repeats"][n - 1]
            key = "F06:%s:%s:r%d" % (config, run, n)
            success = config in POSITIVE
            produce_checks(config, "action", act, MATRIX[config]["topic"], key, success)
            if success:
                d = act["delivery"]
                loc = {"topic": d["topic"], "offset": d["offset"], "key_base64": act["requested"]["key_base64"],
                       "value_base64": act["requested"]["value_base64"]}
                consume_checks(config, rd, loc, True)
            else:
                require(rd["admission"] == "not_applicable_no_delivery" and rd["client_id"] is None and rd["requested"] is None
                        and rd["read_observation"]["record"] is None and rd["result"] is None and rd["delivery"] is None,
                        "%s read stage was not skipped for want of a delivery" % config)
        for config in ("C6", "C7", "C8"):
            seed = caps[(config, "seed")]["repeats"][n - 1]
            topic = MATRIX[config]["topic"]
            produce_checks(config, "seed", seed, topic, "f06-seed:%s:%s:r%d" % (run, topic, n), True)
            d = seed["delivery"]
            locations[(config, n)] = {"topic": d["topic"], "offset": d["offset"], "key_base64": seed["requested"]["key_base64"],
                                      "value_base64": seed["requested"]["value_base64"]}
            consume_checks(config, caps[(config, "action")]["repeats"][n - 1], locations[(config, n)], True)
        for config in ("C9", "C10", "C11"):
            seed_config = MATRIX[config]["seed"][0]
            consume_checks(config, caps[(config, "action")]["repeats"][n - 1], locations[(seed_config, n)], config == "C9")
    for config in CONFIGS:
        for n in range(1, 6):
            for stage, cap in ((s, caps[(config, s)]) for s in stages_of(config)):
                r = cap["repeats"][n - 1]
                if r["admission"] == "required":
                    require(r["client_id"] == "%s-r%d" % (jobs[(config, stage)]["pod"], n), "capture client binding differs")
    # Candidate agreement with the captures.
    for config in CONFIGS:
        cand = candidates[config]
        action = caps[(config, "action")]
        for i, repeat in enumerate(cand["repeats"]):
            a = action["repeats"][i]
            so = repeat["semantic_outcome"]
            if config in POSITIVE:
                if MATRIX[config]["action"] == "produce_one":
                    rd = caps[(config, "read")]["repeats"][i]
                    require(repeat["delivery"] == a["delivery"] and repeat["read_observation"]["record"] == rd["read_observation"]["record"],
                            "%s candidate delivery or read differs from the captures" % config)
                    require(so["delivered_partition"] == 0 and so["consumed_partition"] == 0 and so["value_match"] is True
                            and so["key_base64"] == a["requested"]["key_base64"], "%s semantic outcome differs" % config)
                else:
                    require(repeat["delivery"] is None and repeat["read_observation"]["record"] == a["read_observation"]["record"],
                            "%s candidate read differs from the capture" % config)
                    require(so["delivered_partition"] is None and so["consumed_partition"] == 0 and so["value_match"] is True
                            and so["key_base64"] == a["requested"]["key_base64"], "%s semantic outcome differs" % config)
                require(so["value_kind"] == "bytes", "%s value kind differs" % config)
            else:
                require(repeat["delivery"] is None and repeat["read_observation"]["record"] is None
                        and all(v is None for v in so.values()), "%s candidate carries a delivery, record or semantic outcome" % config)
                require((a["error"]["code"], a["error"]["is_fatal"], a["error"]["origin"]) ==
                        (repeat["outcome_class"]["error_code"], repeat["outcome_class"]["is_fatal"], repeat["outcome_class"]["origin"]),
                        "%s candidate error differs from the capture" % config)
        if MATRIX[config]["seed"]:
            seed_config = MATRIX[config]["seed"][0]
            for i, repeat in enumerate(cand["repeats"]):
                ref = repeat["seed_reference"]
                require(ref is not None and ref["configuration_id"] == seed_config and ref["stage"] == "seed"
                        and ref["job_name"] == jobs[(seed_config, "seed")]["job"] and ref["pod_uid"] == jobs[(seed_config, "seed")]["pod_uid"],
                        "%s seed reference differs" % config)
        else:
            require(all(r["seed_reference"] is None for r in cand["repeats"]), "%s carries a seed reference" % config)
    # Negative configurations: no acknowledged send and no read record, in any repeat.
    for config in NEGATIVE:
        require(all(r["delivery"] is None and r["read_observation"]["record"] is None for r in candidates[config]["repeats"]),
                "%s shows an acknowledged send or a read record" % config)
    return caps, locations


def verify_c5(c5_path):
    summary = load_json(c5_path, PINS["c5_summary"])
    require(summary["commit"] == C5_COMMIT and summary["run_id"] == "41232286130"
            and summary["run_attempt"] == "1", "C5 summary identity differs")
    require(summary["measurement_complete"] is True and summary["incomplete_reasons"] == [] and summary["break_passed"] is True,
            "C5 summary is not complete and passing")
    targets = summary["targets"]
    require(sum(t["numerator"] for t in targets.values()) == summary["numerator"]
            and sum(t["denominator"] for t in targets.values()) == summary["denominator"], "C5 totals do not add up")
    config = json.loads(git("show", C5_COMMIT + ":" + C5_CONFIG))["stryker-config"]
    require(config["thresholds"]["break"] == 60, "C5 break threshold differs")
    mutate = config["mutate"]
    require(sorted(t["target"] for t in targets.values()) == sorted(mutate), "C5 targets differ from the configuration")
    same = {}
    for target in mutate:
        path = "src/Parchwell.Verifier/" + target
        same[target] = git("rev-parse", C5_COMMIT + ":" + path).strip() == git("rev-parse", SOURCE + ":" + path).strip()
    config_same = git("show", C5_COMMIT + ":" + C5_CONFIG) == git("show", SOURCE + ":" + C5_CONFIG)
    require(subprocess.call(("git", "-C", str(ROOT), "merge-base", "--is-ancestor", C5_COMMIT, SOURCE)) == 0,
            "the C5 commit is not an ancestor of the run source")
    return {
        "run": C5_RUN,
        "summary_artifact": C5_SUMMARY_ARTIFACT,
        "commit": summary["commit"],
        "measurement_complete": True,
        "break_passed": True,
        "break_threshold": config["thresholds"]["break"],
        "numerator": summary["numerator"],
        "denominator": summary["denominator"],
        "score_percent_rounded_1dp": round(summary["numerator"] / summary["denominator"] * 100, 1),
        "status_counts": summary["status_counts"],
        "targets": {t["target"]: {"status_counts": t["status_counts"], "numerator": t["numerator"],
                                  "denominator": t["denominator"]} for t in targets.values()},
        "target_source_identical_at_run_source": same,
        "all_targets_identical_at_run_source": all(same.values()),
        "config_identical_at_run_source": config_same,
        "c5_commit_is_ancestor_of_run_source": True,
        "run_source": SOURCE,
    }


def verify_first_dispatch(path):
    status = load_json(path, PINS["first_dispatch_phase_status"])
    require(status["run_id"] == FIRST_RUN_ID, "first dispatch run ID differs")
    f06 = [m for m in status["modes"] if m["item"] == "F06"]
    client = [m for m in f06 if m["stage"] != "judge"]
    require(len(client) == 21 and all(m["outcome"] == "not_attempted" and m["reason"] == "permission_evidence_incomplete"
                                       for m in client), "first dispatch F06 client stages are not all not_attempted")
    counts = collections.Counter(m["stage"] for m in client)
    cands = [c for c in status["candidates"] if c["item"] == "F06"]
    require(len(cands) == 12 and all(c["evidence_valid"] is False for c in cands), "first dispatch F06 candidates differ")
    audit = status["permission_audit"]
    require(audit["status"] == "incomplete" and audit["blocked"] is True and audit["first_submission_consumed"] is False,
            "first dispatch permission audit differs")
    return {
        "workflow_run": FIRST_RUN, "run_id": FIRST_RUN_ID, "pod_artifact": FIRST_POD_ARTIFACT,
        "f06_client_stages_not_attempted": len(client),
        "f06_not_attempted_by_stage": {s: counts[s] for s in ("seed", "action", "read")},
        "f06_not_attempted_reason": "permission_evidence_incomplete",
        "permission_audit_status": audit["status"],
        "f06_client_observations": "none: no F06 client was admitted, so it contributes no observation to any hypothesis",
        "f06_judge_stages_diagnostic_only": len([m for m in f06 if m["stage"] == "judge"]),
        "cause": "audit defect fixed in pull request 288",
        "fix": REPO + "/pull/288",
        "rerun_authorization": REPO + "/issues/240#issuecomment-6557191473",
        "pooled_with_final_run": False,
    }


def build(cand_dir, pods_dir, c5_path, first_path):
    for path, digest in PINS["sources"].items():
        require(sha256(git("show", SOURCE + ":" + path)) == digest, path + " at the run source differs from its pin")
        if path.startswith("scenarios/F06/"):
            pinned_bytes(ROOT / path, digest)
    matrix = check_matrix()
    status0 = load_json(pods_dir / "phase-status.json", PINS["top"]["phase-status.json"])
    audit_sha = status0["permission_audit"]["projection_sha256"]
    candidates, receipts = verify_candidates(cand_dir, audit_sha)
    jobs, status, batch = verify_phase_status(pods_dir, candidates, receipts)
    audit = verify_audit(pods_dir, status)
    caps, locations = verify_captures(jobs, candidates)
    verdict, hypothesis, reason = derive(candidates)
    final = candidates["C11"]["item_assessment"]
    require(final == {"verdict": verdict, "hypotheses": {"H_positive_scope": hypothesis, "H_negative_surface": "recorded_only"},
                      "reason": reason}, "derived assessment differs from the verifier's")
    require(verdict == "undocumented" and hypothesis == "holds" and reason is None, "verdict differs from the proposed one")
    c5 = verify_c5(c5_path)
    first = verify_first_dispatch(first_path)

    per_conf, observed, reasons = {}, {}, {}
    for config in CONFIGS:
        cand = candidates[config]
        clients = []
        for repeat in cand["repeats"]:
            for client in repeat["clients"]:
                if client["client_id"].startswith("%s-f06-%s-" % (RUN_ID, config.lower())):
                    clients.append({"repeat": repeat["repeat"], "stage": client["stage"], "client_id": client["client_id"],
                                    "broker_count": client["broker_count"]})
        per_conf[config] = {
            "definition": matrix[config], "status": cand["status"], "deterministic": cand["deterministic"],
            "outcome_class": cand["outcome_class"], "broker_count_lower_bound": True,
            "broker_count_per_client": clients,
        }
        rows = []
        cap = caps[(config, "action")]
        for repeat, a in zip(cand["repeats"], cap["repeats"]):
            so = repeat["semantic_outcome"]
            row = {"repeat": repeat["repeat"], "status": repeat["status"], "outcome_class": repeat["outcome_class"],
                   "delivered_partition": so["delivered_partition"], "consumed_partition": so["consumed_partition"],
                   "key": b64(a["requested"]["key_base64"]), "value_match": so["value_match"]}
            if a["delivery"]:
                row["delivered_offset"] = a["delivery"]["offset"]
            rec = repeat["read_observation"]["record"]
            row["read_offset"] = rec["offset"] if rec else None
            row["error_reason_text_not_compared"] = a["error"]["reason"] if a["error"] else None
            rows.append(row)
        observed[config] = rows
        counter = collections.Counter(r["error_reason_text_not_compared"] for r in rows if r["error_reason_text_not_compared"])
        if counter:
            reasons[config] = sorted(counter.items())
    summary = [
        "Across 60 complete synthetic repeats in 12 deterministic configurations, the eight positive controls (C0, C1, C3, C5, C6, C7, C8 and C9) succeeded in all five repeats each, and the four configurations outside the narrow grants (C2, C4, C10 and C11) each returned broker error code 29, TOPIC_AUTHORIZATION_FAILED, in all five repeats, so H_positive_scope holds, H_negative_surface is recorded only, and the proposed F06 verdict is undocumented.",
        "The workflow's own permission audit of the loadgen and enrichment-consumer identities completed with exactly the four intended Sender or Receiver grants at event-hub scope and no other supported or unsupported grant, which supports reading the code-29 results as the effect of those scopes without proving it.",
        "Only partition 0 of each of three topics, one native error class per configuration and the two narrow identities were observed, so no claim of universal scope enforcement follows, and the probe identity's namespace-level grants were set by an explicit exception and were not audited.",
    ]
    job_rows = {}
    for (config, stage), info in sorted(jobs.items()):
        job_rows.setdefault(config, {})[stage] = {k: v for k, v in info.items() if not k.startswith("_")}
    publication = {
        "item": "F06",
        "proposed_verdict": verdict,
        "summary": summary,
        "hypotheses": final["hypotheses"],
        "derivation": "decision rules transcribed from scenarios/F06/expected.yaml (SHA-256 %s) into make_results.py; derived verdict equal to the verifier item_assessment of C11 (C0 to C10 carry null, as the rules require)" % PINS["sources"]["scenarios/F06/expected.yaml"],
        "per_configuration": per_conf,
        "observed_per_repeat": observed,
        "error_code_names": {"29": ERROR_CODE_29},
        "error_reason_text_per_configuration_not_compared": {c: [list(x) for x in v] for c, v in reasons.items()},
        "permission_audit": audit,
        "adr012_counts": "not_applicable",
        "run": {
            "workflow_run": RUN, "attempt": 1, "run_id": RUN_ID, "source_commit": SOURCE, "run_conclusion": "success",
            "candidate_artifact": CANDIDATE_ARTIFACT, "pod_artifact": POD_ARTIFACT,
            "batch_job": BATCH_JOB, "batch_stop_job": STOP_JOB, "jobs": job_rows,
        },
        "first_dispatch_provenance": first,
        "versions": {"confluent_kafka_managed": "2.16.0.0", "librdkafka_native": "2.16.0",
                     "image_digests": candidates["C0"]["image_digests"]},
        "environment": ENVIRONMENT,
        "mutation_gate": c5,
        "workload": "synthetic",
    }
    result = {"publication": publication, "candidates": candidates, "judge_receipts": receipts}
    return result, jobs, candidates


def cell(value):
    return "n/a" if value is None else str(value)


def render_tables(result, jobs, candidates):
    pub = result["publication"]
    out = []
    out.append("<!-- tables:configurations -->")
    out.append("| Configuration | Account | Action | Topic | Expected by the scenario | Outcome in all 5 repeats |")
    out.append("| --- | --- | --- | --- | --- | --- |")
    for config in CONFIGS:
        m = MATRIX[config]
        grant = "send" if m["action"] == "produce_one" else "receive"
        if m["account"] == "probe":
            held = "namespace-level %s (test-only exception)" % grant
        else:
            held = "%s grant at event-hub scope %s" % (grant, "held" if (m["account"], m["topic"], grant) in INTENDED_GRANTS else "not held")
        oc = candidates[config]["outcome_class"]
        outcome = "`success`" if oc["result"] == "success" else "`error`, code %d, fatal %s, origin `%s`" % (
            oc["error_code"], "no" if oc["is_fatal"] is False else "yes", oc["origin"])
        out.append("| %s | `%s` | `%s` | `%s` | %s | %s |" % (config, m["account"], m["action"], m["topic"], held, outcome))
    out.append("<!-- /tables:configurations -->")
    out.append("")
    out.append("<!-- tables:repeat-outcomes -->")
    out.append("| Configuration | Repeat | Status | Result | Error code | Fatal | Origin | Sent offset | Read offset |")
    out.append("| --- | ---: | --- | --- | ---: | --- | --- | ---: | ---: |")
    for config in CONFIGS:
        for repeat, row in zip(candidates[config]["repeats"], pub["observed_per_repeat"][config]):
            oc = repeat["outcome_class"]
            out.append("| %s | %d | `%s` | `%s` | %s | %s | %s | %s | %s |" % (
                config, repeat["repeat"], repeat["status"], oc["result"], cell(oc["error_code"]),
                cell(None if oc["is_fatal"] is None else ("yes" if oc["is_fatal"] else "no")),
                "n/a" if oc["origin"] is None else "`%s`" % oc["origin"],
                cell(row.get("delivered_offset")), cell(row["read_offset"])))
    out.append("<!-- /tables:repeat-outcomes -->")
    out.append("")
    out.append("<!-- tables:error-reasons -->")
    out.append("| Configuration | Distinct reason text (not compared) | Repeats |")
    out.append("| --- | --- | ---: |")
    for config in CONFIGS:
        for text, count in pub["error_reason_text_per_configuration_not_compared"].get(config, []):
            out.append("| %s | `%s` | %d |" % (config, text, count))
    out.append("<!-- /tables:error-reasons -->")
    out.append("")
    out.append("<!-- tables:client-broker-counts -->")
    out.append("| Configuration | Repeat | seed brokers | action brokers | read brokers |")
    out.append("| --- | ---: | ---: | ---: | ---: |")
    for config in CONFIGS:
        counts = {}
        for client in pub["per_configuration"][config]["broker_count_per_client"]:
            counts[(client["repeat"], client["stage"])] = client["broker_count"]
        for n in range(1, 6):
            out.append("| %s | %d | %s | %s | %s |" % (config, n, cell(counts.get((n, "seed"))), cell(counts.get((n, "action"))),
                                                       cell(counts.get((n, "read")))))
    out.append("<!-- /tables:client-broker-counts -->")
    out.append("")
    out.append("<!-- tables:permission-audit -->")
    out.append("| Account | Target | Send grant | Receive grant | Audited entries |")
    out.append("| --- | --- | --- | --- | --- |")
    audit = pub["permission_audit"]
    for g in audit["intended_grants_true_cells"]:
        ents = sorted(e["operation"] for e in audit["entries"] if e["account"] == g["account"] and e["target"] == g["target"])
        out.append("| `%s` | `%s` | %s | %s | %s |" % (g["account"], g["target"], "intended" if g["send"] else "none",
                                                      "intended" if g["receive"] else "none", ", ".join(ents) if ents else "none"))
    out.append("<!-- /tables:permission-audit -->")
    out.append("")
    out.append("<!-- tables:jobs -->")
    out.append("| Configuration | Stage | Job (account) | Container started | Container finished | Pod log member | Pod JSON member |")
    out.append("| --- | --- | --- | --- | --- | --- | --- |")
    for config in CONFIGS:
        for stage in stages_of(config) + ("judge",):
            j = {k: v for k, v in jobs[(config, stage)].items() if not k.startswith("_")}
            out.append("| %s | %s | `%s` (`%s`) | %s | %s | `%s` | `%s` |" % (
                config, stage, j["job"], j["service_account"], j["container_started_at"], j["container_finished_at"],
                j["pod_log_member"], j["pod_json_member"]))
    out.append("<!-- /tables:jobs -->")
    out.append("")
    out.append("<!-- tables:mutation-targets -->")
    out.append("| Target | Killed | Timeout | Survived | NoCoverage | CompileError | RuntimeError | Ignored | Detected / scored | Identical at run source |")
    out.append("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |")
    c5 = pub["mutation_gate"]
    for target, t in c5["targets"].items():
        s = t["status_counts"]
        out.append("| `%s` | %d | %d | %d | %d | %d | %d | %d | %d / %d | %s |" % (
            target, s["Killed"], s["Timeout"], s["Survived"], s["NoCoverage"], s["CompileError"], s["RuntimeError"],
            s["Ignored"], t["numerator"], t["denominator"], "yes" if c5["target_source_identical_at_run_source"][target] else "no"))
    s = c5["status_counts"]
    out.append("| All six targets | %d | %d | %d | %d | %d | %d | %d | %d / %d | %s |" % (
        s["Killed"], s["Timeout"], s["Survived"], s["NoCoverage"], s["CompileError"], s["RuntimeError"], s["Ignored"],
        c5["numerator"], c5["denominator"], "yes" if c5["all_targets_identical_at_run_source"] else "no"))
    out.append("<!-- /tables:mutation-targets -->")
    return "\n".join(out) + "\n"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--candidates", required=True, type=pathlib.Path)
    parser.add_argument("--pods", required=True, type=pathlib.Path)
    parser.add_argument("--c5-summary", required=True, type=pathlib.Path)
    parser.add_argument("--first-dispatch-status", required=True, type=pathlib.Path)
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--tables", action="store_true")
    args = parser.parse_args()
    result, jobs, candidates = build(args.candidates, args.pods, args.c5_summary, args.first_dispatch_status)
    data = (json.dumps(result, indent=2, ensure_ascii=True) + "\n").encode("ascii")
    target = ROOT / "results" / "F06" / (DATE + ".json")
    report = ROOT / "results" / "F06" / (DATE + ".md")
    tables = render_tables(result, jobs, candidates)
    if args.tables:
        sys.stdout.write(tables)
        return
    if args.check:
        require(target.is_file() and target.read_bytes() == data, "published result bytes differ")
        text = report.read_text(encoding="utf-8")
        for block in tables.strip().split("\n\n"):
            require(block in text, "report does not contain the generated table " + block.splitlines()[0])
        print("check passed")
        return
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)
    print("wrote", target.relative_to(ROOT))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, ImportError, subprocess.CalledProcessError) as error:
        print("make_results: " + str(error), file=sys.stderr)
        sys.exit(1)
