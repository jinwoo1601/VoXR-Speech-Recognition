# light-lane bindings

## release
<!-- Interview: How does this project publish a release — which repository or registry receives it, what is the release branch called, what command or workflow publishes it, which file states the version and which other files pin it, where do the release notes come from, what versioning rule applies, and what evidence says the release is good? -->
- Repository: GitHub `jinwoo1601/VoXR-Speech-Recognition`, public; consumers install by git URL pinned to a tag (`https://github.com/jinwoo1601/VoXR-Speech-Recognition.git#vX.Y.Z`), with no git credentials needed
- Release branch: `chore/release-X.Y.Z`, cut from an up-to-date `main`, merged to `main` by a pull request the human merges; the Action then cuts its own `release/X.Y.Z` branch, which is never hand-made or deleted unprompted
- Publish procedure: `.github/workflows/release.yml`, triggered by `gh workflow run release.yml -f version=X.Y.Z` only once the bump is on `main`, then read by `gh run list --workflow=release.yml -L 1 --json databaseId,status,conclusion,url` and `gh run watch <id> --exit-status`; it strips the dev-only `NativeBridge~/` and `Tests~/` and the workflow layer (`Planning~/`, `.claude/`, `CLAUDE.md`, `CLAUDE.md.meta`, `memory/`, `memory.meta`), tags `vX.Y.Z` on the stripped commit and creates the GitHub release; it refuses a version mismatch, a missing CHANGELOG section, a stale install pin, a missing `.so`, and an existing tag or release branch; never hand-tag — tags are immutable, so a botched release bumps to the next patch
- Version file: `package.json` `"version"`; the install pins `#vX.Y.Z` in `README.md` and `Documentation~/getting-started.md`, found by `git grep -nE 'VoXR-Speech-Recognition\.git#v[0-9]+\.[0-9]+\.[0-9]+' -- '*.md' ':!CHANGELOG.md'`; the `README.md` version table lists milestones only and is optional
- Release notes: `CHANGELOG.md`, Keep-a-Changelog: the `## [Unreleased]` entries move under `## [X.Y.Z] - <UTC date>`, an empty `## [Unreleased]` left on top; the Action takes the release notes from that section
- Versioning rule: the version the human names; this project does not follow strict semantic versioning (breaking changes have shipped as minors), so a version that looks wrong is said once, then used
- Release evidence: before the cut: the `verification` binding's EditMode and PlayMode runs green from results files this run wrote, and — where `NativeBridge~/` changed since the last tag — its arm64 build, desktop harness and the rebuilt `.so` committed; on-device Quest checks reported as deferred, never claimed; after: `git ls-tree --name-only vX.Y.Z` lacks `NativeBridge~`, `Tests~`, `Planning~`, `.claude`, `CLAUDE.md`, `CLAUDE.md.meta`, `memory` and `memory.meta` and holds `Runtime`, `Documentation~` and `Samples~`, `git ls-tree -r --name-only vX.Y.Z | grep libvosk-bridge.so` finds the plugin, and `gh release view vX.Y.Z` shows the CHANGELOG notes

## issues
<!-- Interview: Where does this project track the issues light-lane work takes up — which tracker, how one issue and its thread are read and the open issues listed with their labels, how findings and questions are posted where an issue lives, and how a changeset names the issue it closes — or does the human name each task, with no tracker? -->
- Tracker: GitHub Issues on `jinwoo1601/VoXR-Speech-Recognition`; an issue labelled `blocked`, `wontfix`, `question`, `needs-input` or `duplicate`, or with a `light-<n>-*` branch already on `origin`, is not picked
- Read: `gh issue view <n> --json title,body,labels,assignees,comments`; `gh issue list --state open --limit 100 --json number,title,createdAt,labels,assignees,url`
- Comment: `gh issue comment <n> --body-file <file>`; an issue left waiting on the human also gets the `needs-input` label by `gh api repos/jinwoo1601/VoXR-Speech-Recognition/issues/<n>/labels -f "labels[]=needs-input"`
- Link: `Closes #<n>` in the pull request body

## classes
<!-- Interview: Which changesets may this project's light lane review as trivial or prose, and how large may a deferral batch grow — at most how many changed lines, summed over its files, and how many files a trivial changeset touches, which path globs every file of a prose changeset must match, and at most how many items one deferral batch holds — or is a slot answered `none`, which reviews every changeset it would admit at slim? -->
- Trivial bound: 20 lines, 1 file — provisional → the tuning pass
- Prose paths: `Documentation~/**`, `Planning~/**`, `memory/**`, `README.md`, `CHANGELOG.md`, `KNOWN_LIMITATIONS.md`
- Batch cap: 5 items — provisional → the tuning pass
