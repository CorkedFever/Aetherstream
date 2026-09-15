# Working on Aetherstream

Read [docs/DEVELOPING.md](docs/DEVELOPING.md) before touching the code. It holds the layout, the
window's structure, how painting on a furnishing works, the release process, and a long list of
things that have crashed the game or shipped broken before. The README is for users and should
stay short and plain.

## Rules that are not in the code

- **Do not release or push unless asked.** Committing locally is fine. "Push" means cut a release
  with `./release.sh <version> --notes <file>`, which itself pushes.
- **The author is CorkedFever everywhere**: commits, manifests, releases. No other name goes into
  the repository or its history.
- **Never commit secrets or zips.** `api.env`, `mediamtx.yml`, `Caddyfile`, `party.env` and any
  `.zip` stay out of the tree.
- **Nothing to do with Dalamud or this plugin goes on luna** (the business box). The Aetherstream
  server is meteor.
- **Session tokens from browsers** (YouTube, Twitch) are never printed or written to disk beyond
  the transient cookie jar yt-dlp reads.

## Habits that save time

- Gate the build on its own output: `OUT=$(dotnet build …); if grep -q "Build succeeded"`. Piping
  the build into grep once shipped a release with the previous binaries.
- After a build, copy `Aetherstream.dll`, `Aetherstream.Playback.dll` and `Aetherstream.Core.dll`
  from `src/Aetherstream.Plugin/bin/Debug` into `%AppData%\XIVLauncher\devPlugins\Aetherstream`,
  or the in-game test is of a stale build.
- The preview harness in the scratchpad reflects over the plugin DLL to render drawn channels and
  exercise the service clients without the game. Rebuild it after the plugin.
- New sources follow one shape: a client class in `Aetherstream.Playback`, a tab in
  `UI/Tabs`, glue in a `Plugin.*.cs` partial, an icon in `UI/HomeScreen.cs`, a row in the
  dependency table in `docs/DEVELOPING.md`, a line on the site.
- Free services only. Anything DRM-protected or deliberately obfuscated is declined, and said so.
