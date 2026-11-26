#!/usr/bin/env python3
"""Compare the pre-refactor snapshots and current assets with the same JSON parser."""

import gzip
import json
from pathlib import Path
import statistics
import time
import tracemalloc


REPOSITORY = Path(__file__).resolve().parents[2]
BACKUPS = REPOSITORY / "docs/guild-migration/backups"
RAW = REPOSITORY / "labyItems/Resources/Raw"


def measure(name, raw_files):
    timings = []
    for _ in range(51):
        start = time.perf_counter()
        parsed = [json.loads(raw) for raw in raw_files]
        timings.append((time.perf_counter() - start) * 1000)
        del parsed

    tracemalloc.start()
    parsed = [json.loads(raw) for raw in raw_files]
    try:
        _, peak = tracemalloc.get_traced_memory()
    finally:
        tracemalloc.stop()

    guilds = parsed[0]["guilds"]
    print(name)
    print(f"  Guild records: {len(guilds):,}")
    print(f"  Embedded miracle lists: {sum('MiracleList' in guild for guild in guilds.values())}")
    print(f"  Separate miracle lists: {len(parsed[2]['miracleLists']) if len(parsed) > 2 else 0}")
    print(f"  Ability definitions: {len(parsed[1]['abilities']):,}")
    print(f"  Raw bytes per file: {[len(raw) for raw in raw_files]}")
    print(f"  Raw bytes total: {sum(map(len, raw_files)):,}")
    print(f"  Gzip bytes total: {sum(len(gzip.compress(raw, mtime=0)) for raw in raw_files):,}")
    print(f"  First parse: {timings[0]:.2f} ms")
    print(f"  Median of 50 repeat parses: {statistics.median(timings[1:]):.2f} ms")
    print(f"  Peak parser allocations: {peak:,} B")


if __name__ == "__main__":
    measure("Before (version 2 guild proposal)", [
        gzip.decompress((BACKUPS / "guilds.pre-refactor.json.gz").read_bytes()),
        gzip.decompress((BACKUPS / "abilities.pre-refactor.json.gz").read_bytes()),
    ])
    measure("After", [
        (RAW / "people/guilds.json").read_bytes(),
        (RAW / "specialisation/abilities.json").read_bytes(),
        (RAW / "people/miracleLists.json").read_bytes(),
    ])
