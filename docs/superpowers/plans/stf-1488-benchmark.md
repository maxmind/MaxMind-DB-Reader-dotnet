# STF-1488 decode guard benchmark

Measures the cost of the STF-1488 decoder guards on an ordinary lookup.
`origin/main` has none of the guards. The branch tip adds `CheckDepth`,
`CheckContainer`, and `ConsumePayload` in `MaxMind.Db/Decoder.cs`, plus two
extra `ref int` parameters threaded through every `Decode` frame.

This is not the hostile-fixture measurement. A separate number exists for a
crafted 132 KB database with about 33 million pointer follows, at roughly
300 ms. That number tests the guards under attack. This benchmark tests their
cost on a normal, benign lookup, the everyday hot path.

## Commits compared

- Branch tip (`greg/stf-1488`): `20728f4`
- `origin/main`: `4ad3a8c`

## What the benchmark measures

`MaxMind.Db.Benchmark/Program.cs` runs `CityBenchmark`, a `[MemoryDiagnoser]`
BenchmarkDotNet class with 2 methods: `CityMemoryMappedLookup` and
`CityMemoryLookup`. Each opens a `GeoIP2-City` style database (one with
`FileAccessMode.MemoryMapped`, one with `FileAccessMode.Memory`) and calls
`Find<CityResponse>` for every IP address in a list, timing the loop.

The database path comes from the `MAXMIND_BENCHMARK_DB` environment variable.
The IP list comes from `MAXMIND_BENCHMARK_IP_ADDRESSES` (comma-separated), or,
if unset, 1,000 random IPv4 addresses generated with a fixed seed.

### Database substitution

The benchmark needs a full production `GeoIP2-City.mmdb`. That file is not in
the `MaxMind-DB` test-data submodule, which holds only small fixture databases
built for correctness tests. The largest City-shaped, non-broken database in
`test-data` is `GeoIP2-City-Test.mmdb` (22,569 bytes). This benchmark uses that
file in place of a production database. Every number below reflects a lookup
against a 22 KB fixture, not a multi-hundred-megabyte production database.

### IP address substitution

The default random IP list mostly misses in this fixture. `GeoIP2-City-Test.mmdb`
covers only 20 IPv4 networks, against a full 32-bit address space. A first run
with the default random list confirmed the miss rate: 0 B allocated per
operation, meaning the decoder never ran. That run measures search-tree
traversal only, not the guarded decode path, so it is not usable for this
comparison.

To exercise the decode path this benchmark instead sets
`MAXMIND_BENCHMARK_IP_ADDRESSES` to the 20 network-start addresses actually
present in `GeoIP2-City-Test.mmdb` (found by enumerating the database with
`Reader.FindAll`). Every lookup then decodes a full `CityResponse`: nested
dictionaries, strings, and a list of subdivisions. This is the configuration
used for every number below.

## Machine and runtime

- BenchmarkDotNet v0.15.8
- Linux Ubuntu 26.04 LTS, 13th Gen Intel Core i7-13700K, 1 CPU, 24 logical /
  16 physical cores
- .NET SDK 10.0.302
- Runtime: .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
- Target framework: net10.0, Release configuration
- BenchmarkDotNet default job (15 iterations after warmup)

Each configuration ran in its own `git worktree`, outside `/workspace`, built
and run one at a time. Both worktrees pointed at the same physical database
file, so the only variable between runs is the decoder code.

## Results

Two full runs per commit, run sequentially (never concurrently, since a first
concurrent attempt showed 3 to 8 times the StdDev from CPU contention and was
discarded).

### Run 1

| Commit | Method | Mean (us) | Error (us) | StdDev (us) | Allocated |
|---|---|---:|---:|---:|---:|
| `4ad3a8c` (main) | CityMemoryMappedLookup | 39.22 | 0.120 | 0.112 | 89.55 KB |
| `4ad3a8c` (main) | CityMemoryLookup | 38.96 | 0.106 | 0.099 | 89.55 KB |
| `20728f4` (HEAD) | CityMemoryMappedLookup | 42.25 | 0.232 | 0.217 | 89.55 KB |
| `20728f4` (HEAD) | CityMemoryLookup | 41.26 | 0.107 | 0.100 | 89.55 KB |

### Run 2

| Commit | Method | Mean (us) | Error (us) | StdDev (us) | Allocated |
|---|---|---:|---:|---:|---:|
| `4ad3a8c` (main) | CityMemoryMappedLookup | 40.72 | 0.260 | 0.243 | 89.55 KB |
| `4ad3a8c` (main) | CityMemoryLookup | 39.33 | 0.266 | 0.249 | 89.55 KB |
| `20728f4` (HEAD) | CityMemoryMappedLookup | 41.47 | 0.263 | 0.246 | 89.55 KB |
| `20728f4` (HEAD) | CityMemoryLookup | 41.95 | 0.145 | 0.128 | 89.55 KB |

### Averaged across both runs

| Method | main (us) | HEAD (us) | Delta (us) | Delta (%) |
|---|---:|---:|---:|---:|
| CityMemoryMappedLookup | 39.97 | 41.86 | +1.89 | +4.7% |
| CityMemoryLookup | 39.15 | 41.61 | +2.46 | +6.3% |

Every run's Error and StdDev stay under 0.27 us. The delta between main and
HEAD, in both individual runs and the average, is 6 to 12 times larger than
the Error band. The regression is real, not noise.

Allocation is identical at 89.55 KB per operation on both commits. The guards
add counter increments and comparisons, not allocations. This matches the
source: `CheckDepth`, `CheckContainer`, and `ConsumePayload` update `ref int`
counters and throw on a limit, they do not allocate.

## Judgment

The regression is 4.7% to 6.3%, averaged over 2 independent runs. This is
within the "a few percent" range `lessons.md` §12 calls acceptable for a
security fix, and well under the 10% threshold for concern.

Nothing got faster in either run, on either commit, across both metrics. That
rules out the run-to-run swap-of-work failure mode this task specifically
watches for.

The regression is consistent with the guards: a container check and a
pointer check on every container and pointer, a payload check on every
string, and 2 extra `ref int` parameters threaded through every `Decode`
frame. A `CityResponse` decode touches many nested containers and strings, so
the per-frame cost of the added checks and parameters adds up across a
single lookup. Nothing in the change places a check inside an unnecessary
loop, and nothing points at a fixable inlining failure. The cost looks
inherent to the guards, not an implementation defect.

**No further action is needed.** The guards stay as implemented.

## Side note: worktree cleanup

Cleanup ran `git worktree remove --force` on the 2 temporary worktrees this
task created, then `git worktree prune`. The repository already carried 2
other worktree registrations, at `/workspace/.worktrees/greg/eng-3437` and
`/workspace/.worktrees/greg/eng-4329`, both already marked `prunable` because
their recorded paths (under `/home/greg/MaxMind/...`) do not exist in this
environment. Their `.git` file already pointed at that same missing path
before this task started. `git worktree prune` removed those 2 stale
registrations from `git worktree list`. It did not touch the files under
`/workspace/.worktrees/`, which remain in place with unchanged timestamps.
`git worktree list` now shows only `/workspace`.
