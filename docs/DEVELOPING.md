# Developing Aetherstream

Engineering notes for anyone working on the code, human or otherwise. The README is for people who
install the plugin; this is everything that was taken out of it so it could be read.

## Dependencies

Everything third-party, what it is for, and where it comes from. **Bundled** ships in the release
zip; **external** has to be on the user's machine; **server** runs on the party host only.

### Bundled with the plugin

| Dependency | Version | Licence | Used for |
| --- | --- | --- | --- |
| [libvlc](https://www.videolan.org/vlc/libvlc.html) (`VideoLAN.LibVLC.Windows`) | 3.0.21 | LGPL 2.1+ (some plugins GPL) | All decoding: HLS, DASH, RTMP, files, every codec. This is ~90 MB of the zip and the reason it is that size. |
| [LibVLCSharp](https://github.com/videolan/libvlcsharp) | 3.9.4 | LGPL 2.1 | .NET bindings to libvlc — the video and audio callbacks the framebuffer comes through. |
| [NAudio](https://github.com/naudio/NAudio) / `NAudio.Wasapi` | 2.2.1 / 2.3.0 | MIT | Audio output. Dalamud has no audio API, so the plugin opens its own shared-mode WASAPI render stream. |
| [VT323](https://fonts.google.com/specimen/VT323) | Google Fonts, 2011 | SIL OFL 1.1 | The display face — headings, the on-screen display, the app names. `Fonts\OFL.txt` ships beside it, as the licence requires. |
| [ImageSharp](https://github.com/SixLabors/ImageSharp) 3.1 | Six Labors | Six Labors Split License (Apache 2.0 terms for open source) | Decodes venue banners, which are WebP, into pixels for the venues channel. |
| Bossa Antigua, Lobby Time, Backbay Lounge, Airport Lounge, Deuces | [Kevin MacLeod](https://incompetech.com), incompetech.com | CC BY 4.0 | The music under the guide and weather channels, re-encoded at 112 kbps. `music\CREDITS.txt` ships beside them; the Music app can swap in your own folder, a Plex playlist, a radio station or a podcast. |
| [Dalamud](https://github.com/goatcorp/Dalamud) (`Dalamud.NET.Sdk`) | 15.0.0 / API level 15 | AGPL 3.0 | The plugin host: ImGui, textures, the game object table, logging. Not in the zip — every user already has it. |

libvlc's own plugin set is shipped unpruned. It is the safest choice — pruning it is what produced
the "no Opus decoder" theory that turned out to be wrong — at the cost of the download.

### External — on the user's machine

| Dependency | Needed for | Install | Licence |
| --- | --- | --- | --- |
| [yt-dlp](https://github.com/yt-dlp/yt-dlp) | YouTube, Kick and most other sites | `winget install --id yt-dlp.yt-dlp --exact`, then restart the game | Unlicense |
| [Deno](https://deno.com/) | **YouTube specifically** — yt-dlp solves YouTube's JavaScript challenges with an external runtime, and without one it warns, drops its preferred formats and hands back what is left | `winget install --id DenoLand.Deno --exact`, then restart the game | MIT |
| [ffmpeg](https://ffmpeg.org/) | Broadcasting to a party (not watching one) | `winget install --id Gyan.FFmpeg --exact` | LGPL 2.1+ / GPL 2+ depending on build |

Both are looked up on `PATH` at the moment they are needed. yt-dlp is also found in the plugin's
config folder, or wherever **Setup**'s file picker is pointed — so a copy downloaded by
hand to the Desktop works, once, for good. (Not beside the plugin DLL: Dalamud installs each version into its
own numbered folder, so anything left there vanishes on the next update.) Neither is downloaded by the plugin, deliberately —
a plugin that fetches executables is not something to ask people to trust.

### Data sources

| Source | What | Notes |
| --- | --- | --- |
| [iptv-org](https://github.com/iptv-org/iptv) | The default live TV playlist (`index.m3u`) | Volunteer-maintained index of publicly available streams. Cached locally for 12 hours; any other extended M3U can be used instead or alongside it. |
| [Pluto TV](https://pluto.tv/) on demand | The Pluto app: free films and series | A session from Pluto's boot endpoint, the shelves and a search from its services, a series' seasons and episodes, and playback as an HLS playlist from its stitcher's v2 path with Pluto's own ad breaks in the stream, carried by the plugin's loopback relay because the stitcher wants the session token as a header on every playlist. No account; what is on depends on your country. Pluto's linear channels are in Live TV through iptv-org. |
| [Jellyfin](https://jellyfin.org/) and [Emby](https://emby.media/) | The Jellyfin or Emby app: your libraries, continue watching, a search, series into episodes with the rest queued | The two share Emby's API. A name and password go to your server once for a token, which is what is kept; the file streams as it sits on disk, so anything the decoder handles plays without the server transcoding. |
| [Red Bull TV](https://www.redbull.com/int-en/tv) | The Red Bull app: films, documentaries, shows and replays | A session token from its API, the discover page's shelves, each collection on its own, a search, and a film's HLS playlist straight from its media service. Free, worldwide, no account. |
| [NASA+](https://plus.nasa.gov/) | The NASA app: documentaries, series, launches, Earth from orbit | NASA+ is a WordPress site, so its videos are posts on the public REST API, each naming a plain HLS address, with topics as a taxonomy and a search. Free, worldwide, no account. |
| [TED](https://www.ted.com/) | The TED app: the newest talks, TED's playlists, a search; a pasted talk address | TED's open GraphQL endpoint lists talks and playlists and searches; a talk's player data names its HLS manifest, which is resolved here since yt-dlp's TED support is broken. Free, worldwide. |
| [Dailymotion](https://www.dailymotion.com/) | The Dailymotion app: trending, channels, a search | Dailymotion's open API lists and searches; a video plays through yt-dlp, because the manifest host turns away any client whose TLS handshake is not a browser's. |
| [South Park Studios](https://www.southparkstudios.com/) | The South Park app: every season, episodes as stills | Paramount's own site for the show, free with ads. The season list is read from a season page, a season's episodes from the site's paging API with title, number, still, blurb and whether the site has it locked; an episode plays through yt-dlp. |
| [PBS](https://www.pbs.org/) | The PBS app: NOVA, Nature, FRONTLINE and a dozen more | PBS keeps no open catalogue service, so each show's episodes page is read for its episode cards: title, still, length, description. A video plays through yt-dlp. Passport titles, which need a member login, are left off the shelf. Some titles are only for viewers in the United States. |
| Twitch | The Twitch app | Who is live at the top, in a game, or by a search, through the same GraphQL endpoint the resolver uses for playback tokens; who you follow through the session borrowed from the browser set for YouTube, read once through yt-dlp's cookie handling and kept in memory for half an hour. |
| YouTube, through yt-dlp | The YouTube app | Search, a playlist or channel link, and with a signed-in browser's cookies set under Setup, Sources, your own feeds: recommended, subscriptions, watch later, history. Listed flat by yt-dlp with thumbnails; a pick plays and the rest of the list follows it. |
| Local files | The Local app | Your Videos folder and any folders you add, scanned for the usual formats; a picture with the file's name, or a poster.jpg in its folder, is the tile. Played as a file URI. |
| The orchestrion | The Orchestrion app: the game's own music | The Orchestrion tables for the rolls and their files; the Ogg Vorbis stream is pulled out of each roll's SCD once and kept beside the config, then plays like any track. Which rolls the character has comes from the player state. |
| [Radio Browser](https://www.radio-browser.info/) | The Radio app: internet radio stations | A community directory of some thirty thousand stations with stream addresses, logos and tags, on public mirrors. A station plays as the music under the drawn channels; the Radio channel shows its logo and what the stream says is on. |
| Apple's podcast directory, and RSS | The Podcasts app | Shows are found by name through Apple's free search, or a feed address is pasted in; episodes are read from the feed's RSS and play as the music, in order from the one chosen. |
| [Internet Archive](https://archive.org/details/movies) | The Archive app: public domain and freely licensed films | Searched through the advanced search API by collection and title, most downloaded first; a film's best MP4 is picked from its metadata and played straight from archive.org's download links, which redirect to a storage node and support ranges. No account. Quality varies with the transfer. |
| [PaissaDB](https://zhu.codes/paissa) | Open housing plots for the housing channel | The game never lists open plots; PaissaDB is crowd-sourced from players running PaissaHouse who open a ward. Fetched every five minutes while the channel is up, for the character's current world. Every listing shows how long ago it was last seen. |
| [iptv-org API](https://github.com/iptv-org/api) | Other addresses for a channel whose listed one has died (`streams.json`) | The playlist lists one address per channel and the free services move theirs without notice; when a lineup channel dies, the index's other addresses for the same channel id are probed and the first that answers stands in for it, with the channel still listed under its own number. Read once per session, only when something dies. |
| [Plex](https://www.plex.tv/) | Your own library, via `plex.tv/link` sign-in | The token is stored locally and only ever sent to your own server. |
| [Final Fantasy Wiki](https://finalfantasy.fandom.com/) renders | The story hour's Twelve, as sprites | Square Enix's official renders of the Twelve, pixelated to ninety-six rows by `tools/make_twelve.py` and bundled as `data/twelve.json.gz`. Fan use, inside a plugin for their own game. |
| [Carbuncle Plushy fish tracker](https://github.com/icykoneko/ff14-fish-tracker-app) | The fishing channel's windows: weather, preceding weather, hours, bait | MIT, by icykoneko. Trimmed to the fish with a window by `tools/make_fishdata.py` and bundled as `data/fish.json.gz`; nothing is fetched at run time. |

### Server — the party host only

Lives in `deploy\`; nothing here runs on a viewer's machine.

| Dependency | Version | Licence | Role |
| --- | --- | --- | --- |
| [MediaMTX](https://github.com/bluenviron/mediamtx) | `bluenviron/mediamtx` (Docker) | MIT | Takes the host's SRT push and serves it as HLS. Publish authorisation is delegated to the party service. |
| [Caddy](https://caddyserver.com/) | on the host | Apache 2.0 | TLS and routing. Serves HLS on an **HTTP/1.1-only** listener — libvlc 3 cannot fetch HLS over HTTP/2. |
| Party service | `python:3.12-alpine` (Docker), standard library only | — | Groups, codes, membership, and the MediaMTX auth callback. No third-party Python packages. |

## Layout

| Project | Depends on | Purpose |
| --- | --- | --- |
| `Aetherstream.Core` | nothing | The contracts: `IFrameSource`, `FramePipeline`, `StereoRingBuffer`, `IStreamResolver`. Copied from Memoria where noted — keep them byte-compatible. |
| `Aetherstream.Playback` | Core, LibVLCSharp | Decode, resolution, the M3U parser, the HLS relay, known screens. **No UI dependencies.** |
| `Aetherstream.PoC` | Core, Playback, NAudio | Throwaway WinForms harness. Its GDI blit exists only to prove pixels flow through our buffer. |
| `Aetherstream.Plugin` | Core, Playback, NAudio | The Dalamud plugin: overlay panel, surface painting, audio. |

## The window

The window is a remote control and a television. The remote is the left column and never
changes: a display, power, mute and Home, a number pad that dials pinned channels, channel and
volume rockers with the guide and weather keys between them, transport, subtitles, audio and the
gear, and a line at the foot saying what is on. Beside it, the picture in a bezel and a home
screen: what is on, then a grid of apps — one per source or panel, each with a status line —
and an app opened takes the screen with a Home button in its corner. Folding the window leaves
the remote alone. The remote sits outside the apps deliberately: pausing, or dialling a channel,
should never mean navigating away from what you were looking at. It is built to sit beside Memoria and read
as its sibling: the same near-black shell, the same rule that the display face (VT323, SIL OFL,
shipped in `Fonts\`) is for short labels only, never for a path or an error.

| File | Holds |
| --- | --- |
| `UI\Theme.cs` | The palette, the shell styling, the glass panel, the display font. Every colour decision lives here. |
| `UI\Ui.cs` | Shared widgets; its colour names forward to `Theme` so older panels keep working. |
| `UI\DisplayFont.cs` | VT323 at 20 px and 40 px. The face is drawn on a 20-pixel grid and only looks right at multiples of it. |
| `UI\ControlWindow.cs` | The shell: hand-drawn title bar, folding, the remote column beside the television. Pushes the theme in `PreDraw`, pops it in `PostDraw`, and catches everything in `Draw` so a throw cannot leave the style stack unbalanced for every other plugin's window. |
| `UI\Screen.cs` | The picture, the on-screen display, the scrub strip, the signal states, the LED. |
| `UI\ChannelDial.cs` | Channel numbers (pin order), stepping, last-channel memory, which channels are known dead. |
| `UI\UiContext.cs` | What every panel needs, so a panel's constructor stays one argument long. |
| `UI\PosterCard.cs` | One clickable tile, drawn by hand so the whole tile is the hit target. |
| `UI\PlexArt.cs` | Poster and logo textures, fetched once and kept. |
| `UI\RemoteWidget.cs` | The remote: display, dialling, rockers, transport, the menus for subtitles and audio. |
| `UI\HomeScreen.cs` | The apps: the grid, the now-on card, and the router that shows one app at a time. |
| `UI\Tabs\*.cs` | The apps' contents: the link box, each library shelf, Live TV, the drawn channels, music, share, setup. |

Two things worth knowing before editing it:

- **Poster textures are ours; the video texture is not.** Nothing outside our own draw lists ever
  sees a poster, so unlike the video texture they can be released — but only after a frame delay
  (`PlexArt.RetireFrames`), because a draw list built this frame is submitted after `Draw` returns.
- **Wrapping a row of chips needs `GetItemRectMax().X`, not `GetCursorPosX()`.** After a button the
  cursor has already moved to the next line, so it cannot answer "does the next one still fit".

## Painting on a real surface

The overlay panel is drawn in ImGui after the game has finished its frame, so it has no depth: it
paints over walls and over your character, and nothing can fix that from where it draws. Painting on
a surface hands our texture to the game's own renderer instead, and everything works because the
game does not know or care that the texture is video.

Two kinds of surface, found in different places:

- **Model materials.** A furnishing's model → `ModelResourceHandle` → material handles → texture
  handles → `Kernel.Texture`. Swap its `D3D11ShaderResourceView`.
- **Effect (VFX) textures.** Some furnishings draw their screen as an effect, not a surface — the
  Everkeep Monitor's only model is its *base*. Those sample `.atex` textures, located by walking
  every loaded resource (`ResourceManager.ResourceGraph`) and matching on the furnishing's number.
  `ApricotTextureResourceHandle` ends at the same `Kernel.Texture`, so the swap is identical.

### Effects blend additively — put a black wall behind them

An additively blended panel computes *background + picture*, so whatever is behind it is added into
the image: a patterned wall washes the picture out, and no amount of alpha fixes it because alpha is
not what is being honoured. **Recolour the actual in-game wall behind the screen to black** and it
contributes zero, so the picture reads almost true. This is the single biggest quality win for a
VFX-based screen, and it is done in the game, not in code.

Also available for effects: the `Fit` sliders (a VFX panel often fades out toward its edges — shrink
the picture into the solid region), `Brightness`, and painting a second texture flat white as a mask.

## Things that will bite you again

- **Never free a GPU object the game has ever seen.** Every crash this produced was a game material
  still pointing at a view whose last reference we had dropped, faulting inside `nvwgf2umx.dll` on a
  driver thread with none of our frames on the stack. `SurfaceBinding` tracks every painted slot and
  **never releases** its reference; textures and the LibVLC instance are deliberately leaked on
  unload. Leaked megabytes beat a crash to desktop.
- **Furniture is not in `ActiveLayout`.** That holds the building (`bg/…/bgparts`). Furnishings live
  in the *other* loaded layouts (`LayoutWorld.LoadedLayouts`), as **shared groups** whose children
  hold the models — a container has no graphics of its own, so filtering on "has graphics" discards
  every furnishing in the room.
- **A furnishing's object-table entry has no draw object.** It is a targeting proxy; the renderable
  instance is the layout's, linked by `HousingObject.LayoutId`.
- **`ImGui.EndChild()` must be called unconditionally**, even when `BeginChild` returns false.
  Skipping it unbalances ImGui's stack and produces malformed draw data — which the GPU driver
  faults on, looking exactly like a graphics bug.
- **Validate every game pointer before dereferencing** (`SafeMemory`, via `VirtualQuery`). Arrays
  like `ModelResourceHandle.MaterialResourceHandles` carry no count.
- **Never read a field twice when a background task replaces it.** `if (items.Count > 0) … items[0]`
  counts one list and indexes another if a Plex listing lands between the two reads. Snapshot the
  reference into a local and use that for the whole method. The same shape — checking state, then
  mutating it, then reading it again — is what crashed the breadcrumb: `GoUp()` emptied the trail
  and the next line indexed it, with the guard one line too late.
- **State that Draw reads is replaced, not edited in place.** `trail.Clear()` from a worker thread
  while the render thread walks it is a crash waiting for a slow server. Assigning a fresh list is
  atomic; the reader keeps the old one for the rest of its frame.
- **A cache budget must count exactly what it is budgeting.** Gating on total entries while
  evicting only *loaded* ones let a few hundred failed fetches ask for more evictions than there
  were textures — throwing out every poster on screen and refetching them, every frame, forever.
- **`ImGui.IsItemHovered()` is false for anything inside `BeginDisabled`.** So the tooltips that
  explain why a control is greyed out are precisely the ones that never appear. Pass
  `ImGuiHoveredFlags.AllowWhenDisabled`.

## Releasing

```
./release.sh 0.2.10 --notes notes.md
```

One command, and it refuses to cut a release from anything but a clean, pushed `main`. It bumps the
version in the `.csproj`, the plugin manifest and both `repo.json` copies; builds with the exit code
checked directly; packages `bin\Debug` flat (`Aetherstream.dll` and `Aetherstream.json` at the root,
`Fonts\`, `images\` and `libvlc\` beside them) into a zip *outside* the tree; asserts the version,
author and icon inside that zip; commits, tags, pushes and publishes the release; then updates the
mirrors and tells GitHub Pages to build, waiting until the fallback copy actually serves the new
version. `--dry-run` does everything up to the commit and puts the bumps back.

Every step is there because its absence once shipped something wrong. Piping `dotnet build` into
`grep` masked a failed compile and shipped the previous binaries under a new version; a zip in the
working tree ended up in history; Pages twice stopped building on its own and the fallback mirror
fell three releases behind. The dev-plugin folder gets the fresh build too (`--no-dev` to skip), so
"testing a stale build" cannot happen either.

The first install on another machine (2026-08-30) confirmed the flat zip layout works with
`Core.Initialize` resolving `libvlc\win-x64` relative to `AssemblyLocation`; the fiddling that day
was `raw.githubusercontent.com` failing on that machine, which is why the manifest is mirrored.

## Live TV, and why channels used to die at twenty seconds

Channels come from any extended M3U (`M3uPlaylist`); `#EXTVLCOPT:` lines are literally libvlc
options, and the user agent and referrer they carry travel on `ResolvedStream.HttpHeaders` — several
hundred channels in the public list refuse to serve without them, which is why a channel is started
directly rather than sent back through the resolver chain as a bare string.

Some portals answer their advertised URL with a redirect to a tokenised playlist that is valid for
a few seconds. libvlc resolves the redirect once and refreshes the tokenised URL forever, so the
first refresh — and every one after — gets HTTP 509. It plays out the segments it has and stops,
about twenty seconds in. Plain VLC does the same. `HlsRelay` is a loopback HTTP server that
re-resolves the original URL behind a stable address, renames segments by media sequence and
proxies the bytes; the decoder never sees a token. It is used automatically, once, when a relayable
stream stalls. Three things it had to get right that a first draft did not:

- `HttpClient` refuses to auto-follow an HTTPS→HTTP redirect, and that downgrade is exactly what
  these portals do. Redirects are followed by hand.
- The client *does* auto-follow same-scheme hops, so the final address must come from
  `response.RequestMessage.RequestUri`, not from the loop's own bookkeeping — or every segment path
  resolves against the wrong host and the origin answers 403.
- `#EXT-X-KEY` and `#EXT-X-MAP` URIs live on the same expiring host and must be proxied too.

It speaks HTTP over a raw socket rather than `HttpListener`, which on Windows needs an
administrative URL reservation — not something to ask of someone installing a plugin.

**Stall detection had never fired** before this: it accepted progress from position as well as
delivered audio, and libvlc's clock keeps running on a starved live stream. Delivered audio is the
authority whenever there is an audio track. libvlc's own log is now forwarded at warning and above
(`Plugin.VlcLog.cs`), rate-limited and with the routine start-up messages filtered, so the next
stream failure names itself instead of being reproduced in a desktop harness.

## The guide channel

The list button on the remote puts up a listings channel in the style of the 90s TV Guide
Channel: whatever is playing shrinks into the top-left corner, the time and what is on sit
beside it, and a grid of everything you could be watching scrolls up underneath on its own —
pinned channels by number, live parties, then recent films and videos. Channel up and down
still work while it is up, and the current row is highlighted. With nothing playing the test
card takes the corner.

It is drawn straight into the frame buffer, so it shows in the window and on a painted
furnishing alike and costs nothing while it is off. The text comes from `images/guidefont.a8.gz`,
VT323 rasterised at build time by `tools/make_guidefont.py` (needs Pillow); re-run that after
changing the size in the script. Glyphs are ASCII only — names are folded to plain letters
before drawing.

## The weather channel

The cloud button beside the guide puts up Local on the 8s for Eorzea: the zone you are standing
in, what the sky is doing and when it changes, the next five weather periods, and a crawl of
every outdoor zone underneath. Indoors, the district you are in stands in for the sky.

Nothing is fetched. Eorzea's weather is a function of the clock — every 1400 real seconds a
number from 0 to 99 is drawn from the time and looked up in the zone's odds table — and both
the tables and the zone list are read from the game's own data through Dalamud, so a new
expansion's zones appear on their own. The maths lives in `Weather/EorzeaWeather.cs`; the icons
are 16x16 pixel art in `Video/WeatherChannel.cs`.

## Dead channels

Public playlists are full of channels that no longer answer, or answer with a black picture.
Two things find them. A link check asks each lineup channel for its headers in the background,
four at a time, once per lineup per session or from the "Check the lineup's links" button in
Setup, and marks the ones that fail. While a channel plays, the session samples the picture's
brightness once a second; black for fifteen seconds with frames still arriving marks it too, as
does a stall nothing could fix. Marks last a day, survive restarts, and clear themselves when a
channel plays properly. The guide, the lineup and channel up and down skip marked channels;
pinned ones stay on the guide reading OFF AIR. "Forgive them" in Setup clears the list.

## The drawn channels

The Aetherstream app is a dial of everything the set can draw for itself. Each is one class
implementing `IFrameChannel` in `Video\`, handed a snapshot by the plugin when it needs data:

| Channel | What it shows | Where the data comes from |
| --- | --- | --- |
| Guide | The listings grid, picture in the corner: the numbered lineup (pins, then the country and group chosen in Setup, defaulting to the character's region), what is playing and what is queued laid on the timeline from their lengths, live channels' programmes from XMLTV guides, recents with where you left off | The playlist's own guide plus the region's community guides (epgshare), or a URL set per playlist in the Live TV app; cached twice a day and parsed for the lineup's first forty channels, matched by id then by name |
| Weather | Local on the 8s, with rare-weather and special alerts | The game's weather tables; the live sky via ClientStructs |
| Clock | Eorzea time, the calendar date, the moon, the sun's arc | The clock |
| News | Lodestone headlines, one story at a time, maintenance windows in local time with a countdown | lodestonenews.com, every fifteen minutes, cached in the config folder |
| Market | The watch list (kept in Setup) priced, a chart of the featured item, a ticker | Universalis, every ten minutes, for the world you are on |
| Gathering | Timed nodes as a departures board: up now, then soonest, with real-time countdowns | The game's gathering tables and the clock |
| Fishing | The fish you still need that have a window: up now with time left, then the soonest to open, each with spot, zone, weather and hours, bait or intuition; folklore fish in gold, your current zone marked; the next four boats along the top | Windows from the Carbuncle Plushy fish tracker's data (MIT), bundled; spots and zones from the game's tables, the forecast from the weather code, your log through ClientStructs. Caught fish hidden unless Setup says otherwise |
| Venues | Who is open in your region tonight (every datacenter of it) and who opens within a day, one venue featured with its banner and description, a crawl of everyone open | ffxivvenues.com's public API, every ten minutes, filtered to your region by the game's datacenter table; banners decoded with ImageSharp. SFW only by default, toggled in Setup |
| Almanac | Daily, Grand Company and weekly resets, the next ocean fishing boat and both routes' destinations, the Jumbo Cactpot draw for your region, retainers out on ventures; the right column turns pages between today's roulettes, a to-do list (leve and allied society allowances, custom deliveries, Wondrous Tails, the squadron, Fashion Report) and the estate's submersibles and airships | The clock; the rest through ClientStructs. Vessels are read while you stand in the workshop and remembered after |
| Horoscope | A Sharlayan reading a day for those born under your guardian: the arcana drawn (the six, with the meanings the astrologians give them), the moon in and whose it is, the sect by the Eorzean hour, a line each for the deity, the arcana, the moon and the sect, and a piece of counsel; a lucky number and the deity's colour | The guardian from the player state, or one chosen in Setup; the calendar from the clock; the lines are written in the setting's own vocabulary and seeded by the day and the deity, so everyone under one god reads the same card |
| Kitchen | A cooking show on a set: tiled wall, a window on the real sky, shelves, a counter with a board, a stove and a plate stand, and a Namazu chef who walks between them. Every episode a real Culinarian recipe: titles, the mise en place laid along the counter, each ingredient chopped in close-up and tossed into the pot, the pot stirred and steaming through the method, the dish plated, a hand-off to the next episode with credits. Hard cuts between wide and close-up, lower-thirds for the patter, an audience that reacts, a LIVE bug | The Recipe and Item tables and the game's icon textures through Lumina; crystals left off; the patter is written; the schedule runs from the wall clock so everyone sees the same episode |
| Wildlife | A moogle in a bush hat and a khaki vest, Stevie Mogwyn, and a creature every episode, from the hunting log or the hunt boards (every B, A and S rank, by zone): the approach through its country, a close-up with a fact card, the moment he gets far too close and has to run, the sign-off; backdrops by the creature's region, the sky by your hour, a next-week card, and a bit between episodes (field notes, a snack, the injury tally, a viewer's letter, a chase). A hunt mark gets a WANTED poster on the titles and, since the game has no portrait of it, is drawn as a silhouette in the grass with eyes lit by rank | The MonsterNoteTarget table for the log's creatures, zones and portraits; NotoriousMonster and the territories' hunt boards for the marks; TerritoryType for regions; the narration is written |
| Stories | The Tonberry's Lantern: tales of the Twelve, two for each god, six pages apiece, in which the gods are the ones doing things, told from a chair by the fire by a tonberry with a book on his knee and a lantern in his hand. Each page is read from the chair and then shown as a plate in a shadow play: the country in cut paper, the mortals as cutouts, and the god in colour from the game's own render, acting the page's beat: arriving, working their element, striking, meeting another of the Twelve, blessing, departing. A shuffled round gives every god a turn before any gets a second | Written, inspired by the setting's own lore, on what the game says of each god: domain, element, symbol, city, moon and the few kinships everyone knows. Nothing is quoted from the game's text. The renders are Square Enix's, pixelated by `tools/make_twelve.py` |
| Shopping | The Eorzean Shopping Network: your market watch, one item every half minute on a turntable, the lowest price on the board big, the average struck through, how many sell a day, the trend, stickers, a countdown, a ticker of the rest, and a goblin host in a headset who wants you to call now | Your market watch through Universalis; the item's icon from the game; the pitches are written |
| Radio | Aether FM: the set's own jukebox on screen: the track, what is next, track N of M, a sleeve generated from the title, a spinning record, VU meters, a spectrum and a waveform that move to the sound | The jukebox, and a tap on its audio callback |
| Housing | Loporrit Estates: the open plots on your world, one every half minute: district, ward and plot, size, price, first-come or lottery with the phase and how many have entered, who may buy, and how long ago somebody actually looked; a house of the district's kind drawn on its plot with the sky by your hour, and a Loporrit estate agent, Listingway, who is from the moon and has read a great deal about houses | [PaissaDB](https://paissadb.zhu.codes), fed by players running PaissaHouse who open a ward; the pitches are written |
| Painting | The Kupo of Painting: Pom Ross, a moogle in a beret, paints a vista from the Sightseeing Log in front of you: the sky in broad strokes first, then the shapes, then the detail in sweeps, with quiet talk about paint; when it is done the card says whether it is in your log, the hours it can be seen, and what the log asks you to do there | The Adventure table for the vistas, their pictures, words and hours; your log from the game's player state; the painter's lines are written |
| Forecast | The Forecast with Nimbly: a pixie in a raincoat beside a board of every outdoor zone, region by region, the sky now and for the next three bells with the real times, the current row's word and how rare it is, and a rare-weather watch along the bottom for the fishers and gatherers | The game's weather tables through the clock, like the weather channel; the pixie's lines are written |
| Saucer | Saucer Tonight with Ko Bi: a kobold under marquee lights runs the Gold Saucer's numbers like a game show: your MGP, the Jumbo Cactpot draw for your region, the next GATE, the Fashion Report window, and the card of the hour, a Triple Triad card you do not have yet with its art, numbers, stars and where the game says it comes from | Your MGP and cards from the game's inventory and UI state; the card tables; the schedules are the clock |
| Sea News | Sea News with Wavv: a sahagin at a half-drowned desk reads the ocean fishing schedule like the shipping forecast: the next boat on both routes with a countdown, where each is bound and at what hour, the departures board, and the conditions on the coasts, over a sea drawn for the next voyage's time of day | The voyage schedule the fishing board computes; the coasts' weather from the tables |
| Allagans | Ancient Allagans: Doctor Sproutlington, a mandragora with a great deal of hair, takes a real vista, creature or item and proves over ninety seconds that it was the Allagans, or the Ascians, or the moon: a corkboard with the subject's sepia photo pinned to three related photos by red string, three pieces of evidence, an expert who agrees, a question he is only asking, and the stamp | The Sightseeing Log, the hunting log and the item table for the subjects and their pictures; every conclusion is written |
| Cinema | Aetherstream Cinema: a whole film every five minutes, written from a seed: a genre in rotation (a Starlight romance about coming home, a science fiction about a signal, an action about a crystal and a tram), a title, two leads and a third from the trailer cast, nine scenes with their own country, hour and weather, subtitles, explosions, a ship, snow, hearts, then the credits and a card for the next film | The scenery painter and the cast; every word is written |
| *Breaks* | Every five minutes a show stops for forty-eight seconds of adverts: a promo for another show, an item off the market board with a slogan written for it, a trailer for a film that does not exist, a sponsor's card from one of the hosts' side businesses, and a bumper each side. The info and ambience channels are left alone | The channel list, the item table, and the scenery painter; every word is written |
| Scrambled | Channel 99: whatever is on, torn sideways in rolling bands with the colour smeared and the odd frame of static, easing for a moment every nine seconds so you almost make it out; every half minute a blue slate asks you to contact your cable operator | Procedural, over the current picture or the test card. There is nothing under it that was not already on |
| Aquarium, Fireplace, Starfield, Plasma, Mystify, 3D Maze, Pipes | Something on, nothing to read | Procedural; the heavy ones draw at a quarter size and scale up. The maze is a raycaster with the smileys that flip the world and the rat; Pipes is a 3D grid drawn back to front, teapot included |

The weather channel's alerts come in two kinds. Rare weather — a one-in-ten chance or less for
that zone — is scanned across every outdoor zone for the next six periods and shown on the
"Coming up" line, which is what fishers wait on. A special alert turns the header red when the
sky where you stand differs from what the tables say it should be: that only happens when the
game forces it, which is a FATE boss bringing its own weather — Tension over the Shroud is Odin,
Royal Levin in the Forelands is Coeurlregina, Quicklevin in the Lochs is Ixion, Hyperelectricity
in Azys Lla is Proto Ultima. It has to be seen from that zone; a set in a housing ward cannot
know what the Shroud's sky is doing.

## Mirror

`Video\WindowCapture.cs` captures another window with Windows Graphics Capture, the API OBS
uses for game capture, through raw COM vtables rather than the WinRT projections, so nothing
extra ships. It runs on its own thread with its own Direct3D device: frame pool, capture
session, copy each frame to a staging texture, map it, convert BGRA to RGBA, keep the newest.
`MirrorChannel` is an ordinary drawn channel that scales that frame into the picture, so it
shows on the furnishing and in the window with no change to the session. The capture stops
itself a few seconds after the channel is no longer up. Two things that cost time: the CPU
access flag for a staging texture is `0x20000` (read), not `0x10000` (write), and every
interface's methods start at slot 6, after IInspectable's three. Protected video captures as
black by design.

## Channel music

The guide and the weather play music underneath, the way the real ones did. The Music app's
"Channel music" section picks the source: the bundled tracks (five Kevin MacLeod lounge pieces,
CC BY 4.0, in `music\`), a folder of your own (mp3, flac, ogg, m4a, wav, opus, subfolders
included), or an audio playlist on the Plex server you are signed in to. It is shuffled, looped,
placed in the room like the picture's own sound. The weather covers the picture, so it takes the
sound too; the guide keeps the picture in its corner, so a film playing keeps its own sound and the
music only fills in when nothing is on. `Playback\Jukebox.cs` is the second, audio-only decoder that plays it.

## Running the harness

```
Aetherstream.PoC.exe <url-or-twitch-channel> [flags]
```

| Flag | Effect |
| --- | --- |
| *(none)* | Video only. Audio is muted by default — see below. |
| `--audio` | Enable audio through the callback → ring → WASAPI path. |
| `--probe-audio` | Run the full decode and ring path with **no output device**, so the audio diagnostics can be read without anything reaching the speakers. |
| `--vlc-audio` | Let libvlc own the output device instead of our callback path. |
| `--test-pattern` | Animated gradient, no network. Proves the display path alone. |
| `--prove-buffer` | Draw a magenta border into the span the source just filled. If it appears on screen, the pixels demonstrably came through our buffer. |
| `--software` | Disable `d3d11va` hardware decode. |
| `--verbose` | Write libvlc's log to `aetherstream.log` beside the exe. |

Audio is opt-in because a wrong audio format does not fail quietly — it plays full-scale noise.
`--probe-audio` exists so the format can be verified without anyone having to listen to it.

## Sources

Resolution goes through a chain (`StreamResolvers.For`), so no service is wired into the app:

1. **Party code** — six characters, resolved by the party service.
2. **Plex** (`plex:` sources) — your own server, behind your own token.
3. **Direct media URL** (`.m3u8`, `.mpd`, `.mp4`, …) — played as-is.
4. **yt-dlp** — anything it supports. Found beside the plugin, beside the game, or on `PATH`;
   requires `winget install --id yt-dlp.yt-dlp --exact` and a game restart.
5. **Built-in Twitch** — the fallback that still works with nothing installed.

A resolution failure reaches the screen as `NO PICTURE` with the first line of the reason; the
whole reason is in the Dalamud log. It used to go only to the log, which on a machine without
yt-dlp made a YouTube link look like nothing happened.

Verified working: Twitch live, YouTube live, YouTube VOD, direct HLS, Plex, live TV.

Ordinary YouTube videos work too, provided yt-dlp is reasonably current. They earlier did not, and
the reason is worth knowing: `-f best` means *best **muxed*** stream, and YouTube has stopped
publishing muxed formats — the fallback it produced was a client-bound URL that returned HTTP 403 to
anyone else. The selector is now `b/bv*+ba`, which takes separate video and audio and hands the
audio to libvlc as `:input-slave=`. If YouTube ever breaks again, `yt-dlp -U` is the first thing to
try; that cat-and-mouse is exactly the work yt-dlp exists to absorb.

## Plex: what the bundled libvlc can and cannot decode

Measured against a real library rather than assumed, because two plausible theories were wrong
first (a pruned plugin set, and a missing resampler — neither was true).

**Only one combination fails: multichannel Opus.** The bundled libavcodec has no Opus decoder at
all (`avcodec: codec not found (Opus Audio)`), so VLC falls back to its standalone `opus` plugin,
which cannot parse a multichannel channel-mapping and reports `cannot read Opus header`. Stereo
Opus decodes fine through that same plugin.

Everything else tested works, including the ones that look riskier: 8-channel E-AC3, 6-channel
AC3 / DTS / DTS-HD MA / FLAC / PCM, and AAC. TrueHD decodes but logs
`too low audio sample frequency (0)` — unresolved, and rare enough to leave.

So `PlexResolver` reads the audio streams and, when the track libvlc would open is one it cannot
decode, names a usable one with `:audio-track=N`. Direct play is preserved; the cost is losing
whatever language or channel layout that track had. Only if *no* track is decodable does it fall
back to asking the server to transcode.

### Two related bugs found alongside it

- **`TranscodeUrl` returned HTTP 400**, so the quality setting had never worked. Plex builds the
  transcode profile from `X-Plex-Product` / `Version` / `Platform` / `Device` / `Model` and rejects
  the request outright when they are absent — `X-Plex-Client-Identifier` alone is not enough.
- **`directStream=0` forced a full re-encode.** `directStream=1` lets the server remux and convert
  only what must be converted, which on a seedbox is the difference between working and melting.

### TV browses one level at a time

A show lists its **seasons**, a season lists its **episodes**. Both are `/children`; the old code
called `/allLeaves` on a show, which flattened a long-running series into several hundred tiles
with nothing to tell one season from another. A show with a single season — most anime — skips
straight to its episodes, because clicking through "Season 1" never carries information.

Numbers are kept as numbers on `PlexLibrary.Item` (`Index`, `ParentIndex`) and formatted as `S03E09`
at the point of display. Folding them into the title, as before, produced `9. The Lake Effect` —
indistinguishable from every other ninth episode once seasons were flattened.

Two details that are easy to get wrong:

- **Plex prepends a synthetic "All episodes" row** to a show's children. It has no `ratingKey`, so
  it is dropped on read and offered as a button instead, which can say how many episodes it means.
- **An episode's artwork is a 16:9 still, not a 2:3 poster.** The grid switches card shape when it
  is showing episodes; drawing stills into poster slots wastes most of each tile.

Playback names are built from `grandparentTitle` + `SxxEyy` + title, so the transport and the
history do not fill up with entries called "Episode 1".

### A missing file looks exactly like a broken plugin

Plex keeps the database row when a file disappears, so the library still lists the episode and the
part URL still resolves — to a 404, which surfaces as "nothing happens". Resolution now asks with
`checkFiles=1` and reports `exists`/`accessible` as a real error instead.

## Things that cost real time, so they are written down

- **libvlc silently ignores `SetAudioFormat("FL32", …)`** in this build and keeps decoding the
  stream's own format, which we then read as float. It does not error; it produces peaks of 3.4e38
  and NaNs — full-scale noise. `S16N` is honoured, so the conversion to float happens in our
  callback. `VlcStreamSource.AudioPeak` and `AudioBadSamples` exist to catch a regression: real
  audio sits inside [-1, 1].
- **Shared-mode WASAPI does no conversion.** The output format must be the endpoint's own
  `MixFormat`, adopted verbatim. Asking for stereo on an 8-channel endpoint lays each stereo pair
  across an eight-slot frame and plays it four times too fast. `sampleRate` is a required
  constructor parameter for exactly this reason — there is no safe default to assume.
- **`Bitmap.Width`/`Height` are GDI+ P/Invokes, not fields.** Using one as an inner-loop bound cost
  a native call per pixel: 22 ms a frame at 720p, versus 0.8 ms once hoisted.
- **A WinForms timer of 16 ms means 30 fps**, not 60 — the 15.6 ms system tick rounds it up to two
  ticks. 15 fits in one.
- **Redirecting a child's stderr without draining it deadlocks the child** once the pipe fills.
  Read both pipes concurrently.
- **libvlc 3 has no generic HTTP header option** — only `:http-user-agent` and `:http-referrer`.
- **`yt-dlp -f best` means "best muxed"** and now fails on YouTube, which publishes none. Use
  `b/bv*+ba` and pass the audio URL via `:input-slave=`.
- **"Build succeeded" does not mean the plugin was rebuilt.** `Aetherstream.Plugin` was missing from
  the solution entirely, so a solution build compiled everything except the thing being deployed —
  and incremental builds have since been seen to skip it too. After building, compare the
  timestamps of the DLLs in `devPlugins\Aetherstream` against `bin\Debug`. Testing a stale build
  reads exactly like a fix that did not work.

## Phase 2 notes

`Aetherstream.Playback` is deliberately free of UI dependencies, static state, and UI-thread
assumptions. Phase 2 writes a thin adapter implementing Memoria's `IFrameSource` over
`VlcStreamSource` and bridges the audio ring — Memoria's is mono, ours is stereo, so that
conversion is the one open decision. The libvlc native payload (~60–90 MB) needs pruning before
plugin distribution.

