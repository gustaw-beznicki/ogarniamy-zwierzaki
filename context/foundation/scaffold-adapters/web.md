---
adapter_schema: 1
component_id: web
starter_id: astro
target_dir: apps/web
staging_dir: .bootstrap-scaffold/web
generated_root: ogarniamy-zwierzaki-web
generated_at: 2026-09-19T01:07:23Z
status: smoke-tested
handoff_sha256: dd1f48145984d04f2f38c4da5bb3e6fa26acf2ae88774aa50487f04927c3302d
cli_executable: npm
cli_path: /home/gustawb/.nvm/versions/node/v24.21.0/bin/npm
cli_version: "12.0.2"
latest_stable_version: "5.2.4"
help_sha256: 7ffdc1b92544dd24c26407df86ff2ed7b047348c7f888151ffec19a22454e998
fresh_for_days: 7
---

## Official sources

- [Install Astro](https://docs.astro.build/en/install-and-setup/) — current official installation guide checked on 2026-09-19; establishes `npm create astro`, the Node.js requirement, templates, dependency installation, and the npm argument separator.
- [create-astro on npm](https://www.npmjs.com/package/create-astro) — official registry package checked on 2026-09-19; latest stable generator is `5.2.4` and documents the non-interactive, install, git, AI-files, and template flags.
- [Astro releases](https://github.com/withastro/astro/releases) — official release feed checked on 2026-09-19; latest stable framework release is `7.3.3`.

## Environment discovered

The local runner is `/home/gustawb/.nvm/versions/node/v24.21.0/bin/npm` version `12.0.2`, with Node.js `v24.21.0`. The project generator is not installed globally: npm fetched the pinned `create-astro@5.2.4` package for the isolated smoke test. `npm create astro@5.2.4 -- --help` reported generator version `5.2.4` and confirmed every flag used below. Its normalized help SHA-256 is recorded in the frontmatter. The generated project declared Astro `^7.3.3` and Node.js `>=22.12.0`.

## Naming and paths

- Raw and logical project name: `ogarniamy-zwierzaki-web`
- Invocation working directory: `.bootstrap-scaffold/web`
- Generated source root: `.bootstrap-scaffold/web/ogarniamy-zwierzaki-web`
- Final target directory: `apps/web`

## Scaffold instruction

From `.bootstrap-scaffold/web`, invoke the pinned generator as one process:

```text
npm create astro@5.2.4 ogarniamy-zwierzaki-web -- --template minimal --no-install --no-git --no-ai --yes
```

## Expected result

The generated source root is `.bootstrap-scaffold/web/ogarniamy-zwierzaki-web`. It must contain `package.json`, `package-lock.json`, `astro.config.mjs`, `tsconfig.json`, `src/pages/index.astro`, and `public/`. `package.json` must identify the package as `ogarniamy-zwierzaki-web` and depend on Astro `^7.3.3`. No Git repository may be initialized. Authored source and configuration must not contain `.bootstrap-scaffold`, a temporary-directory suffix, or another staging-only identity.

## Dependency setup

From the generated source root, run dependency installation as a separate process:

```text
npm install
```

## Build or smoke verification

From the generated source root, run:

```text
npm run build
```

Success means exit code `0` and an Astro static build in `dist/`.

## Dependency audit

From the generated source root, run:

```text
npm audit --audit-level=high
```

## Smoke-test evidence

Tested in a fresh `/tmp/10x-scaffold-adapter.XXXXXX` directory that was removed afterward. Help, scaffold with `--no-install`, separate dependency installation, build, and audit each exited `0`. The expected files and package identity were observed, Astro `7.3.3` built one static page, and npm reported zero vulnerabilities. Cleanup exited `0`; the absolute temporary path does not survive.

## Safety notes

The scaffold process downloads and executes the pinned `create-astro@5.2.4` package, while the separate dependency step downloads packages from the npm registry. Both require network and package-cache permission. The generator does not initialize Git, does not install dependencies itself, and skips generator-created AI instruction files. Bootstrapper must still request normal execution approval and run each command only in its stated staging location.
