# Contributing

- Keep browser concerns in `apps/web` and application logic in `services/api`.
- Code, comments and documentation are written in English; Polish text lives only in translation files.
- Never commit credentials or connection strings with passwords.
- One type per file: each class, record or enum has its own file named after it.
- NuGet versions are managed centrally in [`Directory.Packages.props`](Directory.Packages.props): reference packages in a `.csproj` without `Version`, and add or bump versions only there.
- Coding-agent guidance is in [`AGENTS.md`](AGENTS.md); the AI-assisted workflow is described in [`docs/10xdevs-agent-workflow.md`](docs/10xdevs-agent-workflow.md).

The project is developed as part of the 10xDevs course; course milestones are tagged `m<module>l<lesson>` (for example `m1l1`).
