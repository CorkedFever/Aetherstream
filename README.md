# Aetherstream

Live stream playback decoded into a raw RGBA framebuffer, so the picture can be drawn anywhere — a
desktop window, an overlay panel in FFXIV, or **onto a real in-game surface**, where the game
renders it with correct depth, lighting and occlusion.

## Layout

| Project | Depends on | Purpose |
| --- | --- | --- |
| `Aetherstream.Core` | nothing | The contracts: `IFrameSource`, `FramePipeline`, `StereoRingBuffer`, `IStreamResolver`. Copied from Memoria where noted — keep them byte-compatible. |
| `Aetherstream.Playback` | Core, LibVLCSharp | Decode and resolution. **No UI dependencies.** |
| `Aetherstream.PoC` | Core, Playback, NAudio | Throwaway WinForms harness. Its GDI blit exists only to prove pixels flow through our buffer. |
| `Aetherstream.Plugin` | Core, Playback, NAudio | The Dalamud plugin: overlay panel, surface painting, audio. |

## The window

A transport bar that is always visible, and everything else behind a tab. The bar sits outside the
tab bar deliberately: pausing should never mean navigating away from what you were doing, and the
state of the picture is the one thing worth seeing from every panel.

| File | Holds |
| --- | --- |
| `UI\Ui.cs` | The palette and the shared widgets. Every colour decision lives here, not at the call sites. |
| `UI\UiContext.cs` | What every panel needs, so a panel's constructor stays one argument long. |
| `UI\NowPlayingBar.cs` | Title, scrub, transport. Fixed height, so the tabs below never jump as playback state changes. |
| `UI\PosterCard.cs` | One clickable tile, drawn by hand so the whole tile is the hit target. |
| `UI\PlexArt.cs` | Poster textures, fetched once and kept. |
| `UI\Tabs\*.cs` | Watch, Library, Screen, Sound, Setup. |

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

## Running

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

1. **Direct media URL** (`.m3u8`, `.mpd`, `.mp4`, …) — played as-is.
2. **yt-dlp** — anything it supports. Requires `winget install yt-dlp`.
3. **Built-in Twitch** — the fallback that still works with nothing installed.

Verified working: Twitch live, YouTube live, YouTube VOD, direct HLS, Plex.

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
