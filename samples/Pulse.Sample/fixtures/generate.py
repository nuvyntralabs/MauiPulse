#!/usr/bin/env python3
"""Regenerate checked-in Pulse.Sample fixture databases."""
import json
import os
import sqlite3

root = os.path.dirname(os.path.abspath(__file__))
os.makedirs(os.path.join(root, "maui-diagnostics"), exist_ok=True)


def replace(path):
    if os.path.exists(path):
        os.remove(path)
    return sqlite3.connect(path)


job = replace(os.path.join(root, "plugin.maui.jobqueue.db3"))
job.execute(
    """CREATE TABLE Jobs (
  Id TEXT PRIMARY KEY,
  JobType TEXT,
  Status INTEGER,
  NextAttemptAtUtc TEXT,
  LastError TEXT
)"""
)
job.executemany(
    "INSERT INTO Jobs VALUES (?,?,?,?,?)",
    [
        ("1", "sync.push", 0, "2020-01-01T00:00:00.0000000+00:00", None),
        ("2", "report.email", 0, "2099-01-01T00:00:00.0000000+00:00", None),
        ("3", "invoice.send", 1, None, None),
        ("4", "webhook.fanout", 2, None, None),
        ("5", "cleanup.old", 3, "2026-09-19T16:00:00.0000000+00:00", "timeout"),
        ("6", "payment.retry", 4, None, "card declined"),
        ("7", "unused.job", 5, None, None),
    ],
)
job.commit()
job.close()

retry = replace(os.path.join(root, "plugin.maui.retryqueue.db3"))
retry.execute(
    """CREATE TABLE Operations (
  Id TEXT PRIMARY KEY,
  OperationName TEXT,
  Status INTEGER,
  NextAttemptAtUtc TEXT,
  LastError TEXT
)"""
)
retry.executemany(
    "INSERT INTO Operations VALUES (?,?,?,?,?)",
    [
        ("1", "payment.retry", 3, "2099-01-01T00:00:00.0000000+00:00", "card declined"),
        ("2", "token.refresh", 0, "2020-01-01T00:00:00.0000000+00:00", None),
        ("3", "receipt.upload", 4, None, "410 gone"),
    ],
)
retry.commit()
retry.close()

sync = replace(os.path.join(root, "offlinesync.db3"))
sync.execute("CREATE TABLE SyncChangeRecord (Id TEXT PRIMARY KEY, Collection TEXT, EntityId TEXT)")
sync.execute(
    """CREATE TABLE SyncDocumentRecord (
  Id TEXT PRIMARY KEY,
  Collection TEXT,
  EntityId TEXT,
  SyncStateValue INTEGER
)"""
)
sync.executemany("INSERT INTO SyncChangeRecord VALUES (?,?,?)", [("c1", "visits", "184"), ("c2", "visits", "185")])
sync.executemany(
    "INSERT INTO SyncDocumentRecord VALUES (?,?,?,?)",
    [("d1", "visits", "184", 4), ("d2", "visits", "185", 0), ("d3", "patients", "9", 5)],
)
sync.commit()
sync.close()

open(os.path.join(root, "maui-diagnostics", "anr.txt"), "w").write("main thread blocked 6s\n")
open(os.path.join(root, "maui-diagnostics", "timeline.json"), "w").write(
    json.dumps({"events": [{"name": "AnrDetected", "at": "2026-09-19T16:00:00Z"}]}, indent=2) + "\n"
)
open(os.path.join(root, "logcat.txt"), "w").write("this file must be ignored by maui-pulse incident\n")
print("fixtures ready")
