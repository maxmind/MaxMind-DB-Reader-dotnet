## Releasing

1. Create a release branch from main.
2. Update `releasenotes.md` with the version and today's date.
3. Run `./dev-bin/release.sh`.
4. Approve the release in the GitHub Actions workflow (requires `nuget` environment approval).
