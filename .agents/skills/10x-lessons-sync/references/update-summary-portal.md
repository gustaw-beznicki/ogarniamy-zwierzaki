# Update the existing team-summary portal

Read this after a real lesson sync when an existing portal is available. This is agent-authored synthesis from local lesson prose; the downloader itself does not summarise or translate content.

## Reconcile sources

- Inspect repository instructions and Git status. Work in the current checkout; do not move other worktrees or overwrite unrelated edits. Never edit archived changes.
- Read the portal implementation and its embedded data before editing. In the established portal, `portal-data` contains Polish modules and lessons, `portal-en-data` their English equivalents, `overview-en-data` translated stage routes, and `ui-translations` interface text. The Polish overview is defined in the runtime. Preserve the actual schema if it has evolved.
- Compare lesson coverage against local Markdown and `.10x-lessons.json` using source URLs/references and filenames, not titles alone. Include earlier verified local lessons missing from the portal, not only this run's downloads. Do not fold another course into this portal simply because it was downloaded nearby.
- For this portal, retain Foundations and every locally available 10xDevs 4 module. The introductory link to Foundations is not another substantive lesson. Identify module/lesson numbers from source evidence; export order is not a module number.
- Read the full missing lessons before summarising. If a previously covered lesson's source is absent locally, retain its existing summary rather than deleting coverage. Never invent locked or future content.

## Update content and maps

- Match the existing concise style: actionable principles, concrete example and completion evidence, common pitfall, skill activation conditions, inputs, outputs and usable commands. Remove marketing, administrative digressions and redundant narration. Distinguish course claims from illustrative examples.
- Keep technical identifiers, executable command names, artifact paths and source links stable. Mark local workflow additions and course previews explicitly. A repository name, prompt, technique or announced skill is not an available slash skill.
- Update the Polish and English versions together. Preserve lesson IDs and equivalent coverage, examples, skill contracts and caveats. Translate new interface text too; extend the current localisation mechanism rather than replacing the design. Keep source URLs pointing to actual originals even when the portal language is English.
- Extend module decision trees where the new material changes routing. Route nodes must link to real lesson/skill targets. Add a module tab for a newly available module; preserve the clickable node-and-edge diagrams and their detail inspector, without reintroducing removed flow views.
- Update the default whole-course work-map tab when new material adds or changes a project stage, decision or skill. Preserve expandable stages, entry points for a new idea/existing repository/working product, and recurring delivery loops. Do not equate course order with a mandatory lifecycle or force every skill into a chain.
- Preserve the self-contained offline HTML, PL/EN switch, stable anchors, readable responsive typography, keyboard interactions and saved language preference. Update coverage counts, module ranges, source/export dates and descriptions wherever they appear, including translated text and accessibility labels. Determine counts from actual substantive lessons, not a previous fixed total.

## Verify and report

- Parse every changed embedded JSON block and syntax-check the runtime without executing browser-only code. Check Polish/English lesson IDs, order, source links, skill names and preview/local-addition flags for parity. The translated overview routes must match the Polish routes and choices in the positional schema currently used.
- Check unique lesson/skill IDs and every internal routing target, including overview links; distinguish lesson anchors from section and navigation anchors.
- Use available DOM or browser tooling to exercise new module/stage branches, skill links and language switching, including preservation of selected view and open lessons. Check desktop and narrow-screen diagram labels for clipping. Do not claim browser or visual verification if only data/syntax checks were available.
- Confirm that a no-new-content reconciliation leaves an already-current portal unchanged. If a source or translation is ambiguous, keep the existing content and report the unresolved gap; do not publish an English or Polish version with silently missing lessons.
- Report download counts, the updated portal path, added lesson/module identifiers, map changes, PL/EN parity and verification results. Clearly separate complete updates from download or validation failures. Do not commit, push or deploy automatically.
