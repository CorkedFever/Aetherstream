# Aetherstream

A television in Final Fantasy XIV. Live streams, your Plex library and thousands of live TV channels,
painted **onto a real in-game furnishing** so the game lights and occludes the picture like anything
else in the room — and party groups so a few friends can watch the same thing together.

## Installing

1. In Dalamud settings → *Experimental* → *Custom Plugin Repositories*, add
   `https://raw.githubusercontent.com/CorkedFever/Aetherstream/main/repo.json` and save.
2. Open the plugin installer, search for **Aetherstream**, install it.
3. `/aether` opens the window.

### What it needs

| To do this | You need | Notes |
| --- | --- | --- |
| Twitch, Plex, live TV, direct stream URLs | nothing | works out of the box |
| **YouTube**, Kick and most other sites | **yt-dlp** | `winget install yt-dlp` in a terminal, then **restart the game** — it reads `PATH` when it starts, so an install made while it is running is invisible until you relaunch. If YouTube stops working later, `yt-dlp -U` is the first thing to try. |
| **YouTube** in particular | **Deno** (a JavaScript runtime) | `winget install DenoLand.Deno`, then restart the game. yt-dlp needs it to solve YouTube's challenges; without it YouTube half-works at best. |
| Broadcasting to a party | **ffmpeg** on `PATH` | `winget install ffmpeg`. Watching a party needs nothing. |

If a YouTube link does nothing, the screen says why: `NO PICTURE — yt-dlp is not installed…`.

### First five minutes

- **Screen tab → Known screens → Everkeep Monitor.** Stand next to your monitor and click it; the
  picture lands on the nearest one. Recolour the wall behind it black in-game — the panel is an
  additive effect, and a black wall is the single biggest quality win.
- **Live TV.** Right-click a channel to give it a number; channel ▲/▼ and last-channel on the remote
  step through those. Plenty of channels in a public list are dead at any given moment — if one does
  nothing, try another. Add your own list (an ErsatzTV or Tunarr server, say) from the playlist
  picker.
- **Share.** Make a party, send the six-character code. Whoever pastes it into the Watch tab sees
  what you broadcast.

## Dependencies

Everything third-party, what it is for, and where it comes from. **Bundled** ships in the release
zip; **external** has to be on the user's machine; **server** runs on the party host only.

### Bundled with the plugin

| Dependency | Version | Licence | Used for |
| --- | --- | --- | --- |
| [libvlc](https://www.videolan.org/vlc/libvlc.html) (`VideoLAN.LibVLC.Windows`) | 3.0.21 | LGPL 2.1+ (some plugins GPL) | All decoding: HLS, DASH, RTMP, files, every codec. This is ~90 MB of the zip and the reason it is that size. |
| [LibVLCSharp](https://github.com/videolan/libvlcsharp) | 3.9.4 | LGPL 2.1 | .NET bindings to libvlc — the video and audio callbacks the framebuffer comes through. |
| [NAudio](https://github.com/naudio/NAudio) / `NAudio.Wasapi` | 2.2.1 / 2.3.0 | MIT | Audio output. Dalamud has no audio API, so the plugin opens its own shared-mode WASAPI render stream. |
| [VT323](https://fonts.google.com/specimen/VT323) | Google Fonts, 2011 | SIL OFL 1.1 | The display face — headings, the on-screen display, the input strip. `Fonts\OFL.txt` ships beside it, as the licence requires. |
| [Dalamud](https://github.com/goatcorp/Dalamud) (`Dalamud.NET.Sdk`) | 15.0.0 / API level 15 | AGPL 3.0 | The plugin host: ImGui, textures, the game object table, logging. Not in the zip — every user already has it. |

libvlc's own plugin set is shipped unpruned. It is the safest choice — pruning it is what produced
the "no Opus decoder" theory that turned out to be wrong — at the cost of the download.

### External — on the user's machine

| Dependency | Needed for | Install | Licence |
| --- | --- | --- | --- |
| [yt-dlp](https://github.com/yt-dlp/yt-dlp) | YouTube, Kick and most other sites | `winget install yt-dlp`, then restart the game | Unlicense |
| [Deno](https://deno.com/) | **YouTube specifically** — yt-dlp solves YouTube's JavaScript challenges with an external runtime, and without one it warns, drops its preferred formats and hands back what is left | `winget install DenoLand.Deno`, then restart the game | MIT |
| [ffmpeg](https://ffmpeg.org/) | Broadcasting to a party (not watching one) | `winget install ffmpeg` | LGPL 2.1+ / GPL 2+ depending on build |

Both are looked up on `PATH` at the moment they are needed. yt-dlp is also found in the plugin's
config folder, or wherever the **Setup tab** is pointed — so a copy downloaded by hand to the
Desktop works, once, for good. (Not beside the plugin DLL: Dalamud installs each version into its
own numbered folder, so anything left there vanishes on the next update.) Neither is downloaded by the plugin, deliberately —
a plugin that fetches executables is not something to ask people to trust.

### Data sources

| Source | What | Notes |
| --- | --- | --- |
| [iptv-org](https://github.com/iptv-org/iptv) | The default live TV playlist (`index.m3u`) | Volunteer-maintained index of publicly available streams. Cached locally for 12 hours; any other extended M3U can be used instead or alongside it. |
| [Plex](https://www.plex.tv/) | Your own library, via `plex.tv/link` sign-in | The token is stored locally and only ever sent to your own server. |

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

The window is a television: the live picture in a bezel at the top, a remote under it, a strip of
inputs, and whichever input is selected filling the rest. The screen and remote sit outside the
inputs deliberately — pausing should never mean navigating away from what you were doing, and the
picture is the one thing worth seeing from every panel. It is built to sit beside Memoria and read
as its sibling: the same near-black shell, the same rule that the display face (VT323, SIL OFL,
shipped in `Fonts\`) is for short labels only, never for a path or an error.

| File | Holds |
| --- | --- |
| `UI\Theme.cs` | The palette, the shell styling, the glass panel, the display font. Every colour decision lives here. |
| `UI\Ui.cs` | Shared widgets; its colour names forward to `Theme` so older panels keep working. |
| `UI\DisplayFont.cs` | VT323 at 20 px and 40 px. The face is drawn on a 20-pixel grid and only looks right at multiples of it. |
| `UI\ControlWindow.cs` | The shell: hand-drawn title bar, folding, the input strip. Pushes the theme in `PreDraw`, pops it in `PostDraw`, and catches everything in `Draw` so a throw cannot leave the style stack unbalanced for every other plugin's window. |
| `UI\Screen.cs` | The picture, the on-screen display, the scrub strip, the signal states, the LED. |
| `UI\Remote.cs` | Transport, channel up/down, last channel, mute. |
| `UI\ChannelDial.cs` | Channel numbers (pin order), stepping, last-channel memory, which channels are known dead. |
| `UI\UiContext.cs` | What every panel needs, so a panel's constructor stays one argument long. |
| `UI\PosterCard.cs` | One clickable tile, drawn by hand so the whole tile is the hit target. |
| `UI\PlexArt.cs` | Poster and logo textures, fetched once and kept. |
| `UI\Tabs\*.cs` | Watch, Library, Live TV, Screen, Sound, Share, Setup. |

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

The zip is `bin\Debug` of the plugin project, flat: `Aetherstream.dll` and `Aetherstream.json` at
the root with `Fonts\` and `libvlc\` beside them. Dalamud extracts it as-is, and
`Core.Initialize` finds `libvlc\win-x64` relative to `AssemblyLocation` in both the installer and
dev-plugin layouts — confirmed by the first install on another machine (2026-08-30), which took
fiddling for a different reason: `raw.githubusercontent.com` was failing on that machine for
several repos at once, which is why `repo.json` is also mirrored from luna. Bump the version in the
`.csproj`, `Aetherstream.json` and `repo.json` (both `AssemblyVersion` fields and the three download
links), tag, publish the release with the zip, then push the mirror.

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
   requires `winget install yt-dlp` and a game restart.
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
