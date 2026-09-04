# STF-1488 fixture inventory

Source: `MaxMind.Db.Test/TestData/MaxMind-DB/test-data/README.md` at submodule
commit `363086b7d90650100e91f954937794c6a090c2a0` (upstream `origin/main`).

Filenames below are copied verbatim from the README. Mapped to the `lessons.md`
§9 list of 12 fixture shapes.

1. Pointer fan-out, conventional IPv4 database:
   `MaxMind-DB-test-pointer-decoder-dos.mmdb` Depth-40 pointer fan-out. An
   unprotected decoder performs 2\*\*40 leaf decodes from 451 bytes. Rejected by
   a depth or value-count limit.

2. Pointer fan-out, conventional IPv6 database:
   `MaxMind-DB-test-pointer-decoder-dos-ipv6.mmdb` Same fan-out shape, in a
   conventional IPv6 database that maps the whole address space to the data
   entry. Rejected by a depth or value-count limit.

3. Amplification through repeated byte-sequence targets — already covered
   (existing test, not a new fixture from this list).

4. Amplification through repeated string targets — already covered (existing
   test, not a new fixture from this list).

5. Worst-case payload amplification within the value limit — already covered
   (existing test, not a new fixture from this list).

6. Exactly 65,536 values: `MaxMind-DB-test-decoder-value-limit.mmdb` Decodes to
   exactly 65,536 values. Expected result per README: accept. Tests that the
   value-count limit is inclusive, not off-by-one on the low side.

7. One value over 65,536: `MaxMind-DB-test-decoder-value-limit-over.mmdb`
   Decodes to 65,537 values. Expected result per README: reject. Tests the
   value-count limit boundary from the high side.

8. Pointer-heavy value-count case:
   `MaxMind-DB-test-decoder-value-limit-pointer-heavy.mmdb` 65,535 values
   reached through a depth-15 pointer fan-out. Expected result per README:
   accept. Tests that a value-count limit correctly counts values reached via
   pointers, not just inline values, while staying under the limit.

9. Exactly 2 MiB expanded payload — already covered (existing test, not a new
   fixture from this list).

10. One byte over 2 MiB expanded payload — already covered (existing test, not a
    new fixture from this list).

11. Amplified metadata rejected on open: Payload variant:
    `MaxMind-DB-test-metadata-payload-limit.mmdb` (already covered by an
    existing test). The metadata alone materializes 2,228,190 bytes; expected
    result per README: reject at open. Value-count variant: NOT PRESENT
    upstream. The README's boundary-fixture table lists only a metadata
    payload-limit file, no metadata value-count file. `ls` of `test-data/`
    confirms no other `metadata` fixture beyond
    `MaxMind-DB-test-metadata-payload-limit.mmdb` and
    `MaxMind-DB-test-metadata-pointers.mmdb` (the latter is unrelated: it is not
    listed in the denial-of-service or boundary tables).

12. Path lookup — not applicable. This reader has no path-selection API. (The
    upstream fixture `MaxMind-DB-test-decode-path-shared-budget.mmdb` exists but
    tests a path API this reader does not have.)

## Verification notes

- The brief's `ls | grep -Ei "dos|limit|amplif|pointer|depth|value"` command
  misses `MaxMind-DB-test-decode-path-shared-budget.mmdb` (item 12, N/A) and
  `MaxMind-DB-test-decoder.mmdb` / `MaxMind-DB-test-pointer-decoder.mmdb`
  (pre-existing static test fixtures, unrelated to the resource-limit work).
  Confirmed by cross-checking the full `test-data/` listing against the README's
  two fixture tables.
- The "Reader Resource Limits" specification section
  (`MaxMind-DB-spec.md#reader-resource-limits`) IS PRESENT at this commit, with
  both the 512-depth and 65,536-value recommendations. `maxmind/MaxMind-DB#282`
  has landed (it is the merge commit at the checked-out HEAD).
