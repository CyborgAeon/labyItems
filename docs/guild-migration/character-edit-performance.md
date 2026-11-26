# Character edit performance

The review and export paths enter the same character ability refresh as the builder. The refresh loads race and class grants, reads the selected specialisation state on the UI thread, resolves guild benefits for the active memberships, and computes and publishes the aggregate. Detailed published-rule expansion stays in ability detail lookup. The public reference services own their catalogue caches, with account-scoped invalidation where the contents depend on the workspace.

| User action | Execution path |
| --- | --- |
| Open builder | Load reference cards, capture the selected class's human life baseline, initialize the active specialisation screen, then resolve character abilities. |
| Select race or class | Update selection immediately, cancel prior selection work, filter compatible options, reload specialisations and resolve the character. |
| Edit subtype or skill | Recalculate the specialisation screen, preserve or confirm dependent selections, update visible spell customisation and run the character refresh. The refresh applies prerequisite feedback using the resolved abilities. |
| Buy an additional skill or choose a multiclass or multirace level | Advancement and their own wizards stage selections in the draft. The multi-path level sliders update existing row highlights and the saved selection determines the subsequent review and battleboard projection. Their loaders use the shared class, race and multi-path catalogues. |
| Edit guild choice | The guild results use the current tier and selections. Refreshing the character applies selected grants with scoped grant identities. Eligibility search and picker projection are distinct from character benefit calculation. |
| Review or export | The wizard syncs the draft and builds summary rows. Saved-character battleboard entry points refresh stale snapshot values before presenting effective abilities. Export builds the workbook from the same draft before rendering the PDF. |

## Verified reductions

The following counts are from tracing one completed operation in the applicable path. They count builder work, excluding the separate review grid's per-level life-scale queries.

| Work unit | Before | After |
| --- | ---: | ---: |
| Effective draft life calculation on startup, race or class selection, or one review sync | 2 | 1 |
| UI dispatches to read selected specialisation grant and alignment state during a worker refresh | 2 | 1 |
| Prerequisite passes after one completed specialisation edit | 2 | 1 |
| Gating callbacks after a completed race or class selection, including the immediate selection update | 3 | 2 |
| Consolidation armour classification calls per sourced base ability | 2 | 1 |

A release-mode .NET 10 probe compared the old list membership scan and new case-insensitive set lookup in isolation. It used 240 input names with 120 distinct values, confirmed identical output order, warmed each implementation for 100 iterations, then alternated two blocks of 5,000 passes per implementation. Stopwatch time and managed allocations on the calling thread were measured over each block.

| Membership check | Warm time per pass | Bytes allocated per pass |
| --- | ---: | ---: |
| List scan with LINQ predicate | 0.0677–0.0879 ms | 23,312 |
| Indexed check | 0.0148–0.0160 ms | 9,568 |

The probe isolates one repeated operation and excludes data loading, other consolidation work, UI scheduling and rendering. Cold and warm catalogue parsing are measured separately in [data-migration.md](data-migration.md). End-to-end mobile timings, physical UI notification counts and measurements for additional skill purchases and multi-path changes were unavailable in this session.

Race and class pipelines now keep cancellation tokens valid when an edit supersedes queued work, and specialisation edits pass their token through prerequisite publication and ability refresh. Grant contributions stay independent through innate rank calculation, so rounding and acquisition-level checks happen before presentation totals. This change uses the existing per-editor synchronization and introduces no global character cache. A complete immutable snapshot of all editor inputs remains future work. Independent callers in the wizard and advancement still have separate lifecycles, so the counts above apply to each traced operation.
