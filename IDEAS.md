# Possible ideas

A parking lot, not a plan. Nothing here is promised; things move to the README's changelog when
they ship. Grouped by the kind of thing they are, roughly ordered within each group by how much
they would change the experience.

## Watch-together

- **Sync anchor.** Party viewers each buffer independently and drift apart by seconds. The host
  publishes its position; everyone nudges toward it. Planned, not started.
- **"N watching" on the OSD**, with names.
- **Host handoff.** If the host drops, someone else takes over instead of the stream dying.
- **Shared queue.** Members add to a queue the host plays through.
- **Now-playing in chat.** `/aether share` posts what's on and the party code to party chat.
- **Presence.** Opt-in: what friends are watching shows in the guide, so you can drop in.
- **Vote to skip**, or "pass the remote" so a guest drives for a while.
- **Party watch history**, so "what did we watch last week" is answerable.
- **Emotes and reactions** shown as small icons over the picture for a couple of seconds.
- **Scheduled showings.** A party has a start time, the guide says "movie night, 8pm", and the
  set switches on by itself.
- **Screen share from a friend** via the SRT ingest already on meteor: OBS to your party in one
  URL. Also answers "how do I show a game I'm playing".

## Screens and the room

- **Multiple screens at once**, each with its own source and sound position.
- **Picture-in-picture** on the TV window, for when the painted surface is out of view.
- **Sleep timer.** Off after N minutes, or at the end of the episode.
- **Ambient glow.** Tint the window frame (or nearby geometry) with the average frame colour.
- **Aspect modes** on the remote: fit, fill, stretch, 4:3 pillarbox. Some furnishings are square.
- **Brightness and colour** per screen, so a set in a dark room isn't blinding.
- **Curved or tilted mapping** for surfaces that aren't flat panels.
- **Burn-in screensaver.** After the sleep timer, a bouncing logo or the test card.

## Sources

- **Twitch chat overlay** beside or over the picture.
- **Jellyfin** alongside Plex. Same shape as the Plex resolver.
- **Local files** via the file picker.
- **Bookmarks** that outlive the recents list, with a name and a poster.
- **Podcasts and radio** as audio-only channels with cover art on the screen.
- **YouTube playlist import**, played in order through NextUp.
- **A shared party playlist file** that's just a URL, so a venue can hand out its channel list.
- **A channel from your own library.** Your Plex shows lined up as a 24-hour schedule, so there
  is always something on. Ours, rather than a third-party tool.
- **Karaoke or music mode.** Audio-only sources with a visualiser, for venues.
- **Channel music sources.** *Built: bundled tracks, a folder, or a Plex playlist.* Spotify and the
  other DRM services cannot hand over audio; a "Spotify remote" that starts and pauses the desktop
  app with a channel is the most that is possible there. Jellyfin and Navidrome would be easy adds.

## Guide

- **EPG data** where a Live TV playlist offers XMLTV: what's on now and next, not just names.
- **Favourites row** at the top, and a "last watched" jump.
- **Search** across guide, library and recents in one box.

## Playback

- **Per-source A/V offset**, so a Plex transcode remembers +1000 ms and live TV remembers zero.
- **Playback speed.**
- **Chapters** from the container, with skip buttons on the remote.
- **Screenshot** of the current frame to clipboard or file.
- **Hardware decode** via D3D11 in libvlc, cutting CPU at 1080p.

## Quality of life

- **Keybinds.** Play, pause, mute, next channel without opening the window.
- **Auto-pause in duty**, resume when you're back.
- **Quiet hours.** Sound ducks or mutes during cutscenes and when the chat window is focused.
- **Config export and import**, for a second character or a friend's machine.
- **First-run walkthrough.** Pick a screen, pick a source, hear a sound. Most support questions
  so far were setup, not bugs.
- **Health panel.** Green/amber/red for yt-dlp, Deno, ffmpeg, Plex sign-in, party server, audio
  device. One glance instead of a log.
- **Bug report bundle.** Zips recent vlc log plus config with tokens redacted.
- **Update notes in game**, shown once when a new version lands.

## Retro flavour

- **"PLEASE STAND BY" card** during buffering and stalls.
- **Channel-change static** for a few frames when switching live channels.
- **Startup thunk**, a brief warm-up glow when the set switches on.
- **Remote control rattle**, a tiny animation on button press.
- **Channel idents.** A short bumper card with the channel name on switch.
- **Weather channel.** *Built, with music, rare-weather alerts across all zones, and a special
  alert when the live sky is forced by a FATE boss (Odin, Coeurlregina, Ixion, Proto Ultima).*

## Reach

- **Submit to the official Dalamud repo.** The biggest change to who ever sees it. Expect
  questions about the libvlc payload and launching yt-dlp as a process.
- **A trailer or demo clip** on the site and README.
- **Support link** on the site, if wanted.

## Under the hood

- **Startup hitch** (~110 ms) when playback starts, noticeable mid-fight.
- **Prune libvlc plugins** from the zip. 92 MB, most of it codecs nobody will hit.
- **Replace the party's Python service** with something backed by a real database, if it ever
  hosts more than friends.
- **Tests** around the relay and the resolvers, the parts that break when a service changes.

## Accessibility

- **Subtitle styling.** Size, outline, position, a dyslexia-friendly font. libvlc exposes these,
  and they matter on a small painted surface.
- **Audio description tracks** treated as a first-class pick in the audio menu.
- **Colourblind-safe OSD colours**, or a high-contrast toggle for the remote.

## Venues and events

- **Venue mode.** Locks the remote to a host, hides the window for guests, shows a schedule card
  between showings. Nightclubs and cafés in housing are a real scene.
- **Announcements.** A text ticker across the bottom of the picture, editable by the host.
- **Intermission card** with a countdown to when the film resumes.
- **Guest join code.** A short code in a macro or the venue's Discord that joins in one command.

## Persistence

- **Continue watching row** at the top of the library, from the resume table.
- **Per-house screen layouts** that remember which furnishing in which house.
- **Cloud config**, optional, through the party server, so friends can share a setup.

## Sound

- **Reverb by room size.** A hall gets a little echo, a bedroom none. Cheap to fake.
- **Occlusion.** Muffle when a wall is between you and the screen; a raycast, or just
  "different room, duck 6 dB".
- **Audio-only mode** for a screen that's out of view: sound follows, picture doesn't render.

## Cross-plugin

- **Memoria bridge.** The Phase 2 note in the README: one decoder shared by both plugins.
- **Discord Rich Presence**: "watching X with N people", optional.
- **IPC** so other plugins can ask what's playing or change the channel.

## Odd but cheap

- **Teletext page.** A 1980s teletext page of Eorzea news, weather and market prices on channel 100.
- **Prevue-style guide channel.** *Built, with music when nothing is on; EPG cells and a promo loop
  still to do.* The 90s TV Guide Channel: a grid of channels and half-hour
  slots scrolling up slowly on the bottom two thirds, the current channel or a promo loop in the
  top third, a weather and party ticker along the bottom, smooth jazz throughout. Rows come from
  the Live TV playlist and the party schedule; cells fill from EPG data where a playlist has it.
  Probably the better first build of the two: it answers "what's on right now" and reuses the
  guide's data, while teletext is the deeper joke.
- **Test card variations** by time of day, or a sign-off at midnight.
- **Remote batteries.** After a thousand presses the remote gets slow until you "change the
  batteries". A joke, behind a toggle.

## More drawn channels

Everything here is data plus the bitmap font, drawn into the frame the way the guide and the
weather are. A new one is one class implementing `IFrameChannel` and a button.

From the game, no network:

- **Eorzea clock.** *Built.* Eorzea time, the calendar date, the moon, the sun's arc.
- **Hunt and FATE board.** What is up in the zone from the object table, like a departures board.
  *Weak in practice: the set lives in a housing ward, where the client sees no FATEs and no marks.
  Only worth it fed from outside — Sonar's IPC if the viewer has it, or a train route a conductor
  pastes into the party — so it sits low on the list.*
- **Almanac.** *Built.* Resets, the ocean fishing boat, the cactpot draw, retainers on
  ventures, and today's roulettes ticked off.
- **Gathering log.** *Built.* Timed nodes as a departures board, with countdowns to each.
- **Zone map.** The current map texture with you and the party as blips.

From the party server:

- **"ON AIR" card.** Title, host, viewer count, and the join code, big.
- **Announcements ticker.** A line the host types, crawling under whatever is playing.
- **Countdown channel.** "Movie night starts in 12:04" with the poster, then it tunes in by itself.

From Plex:

- **Recently added.** A rotating poster wall of the last twenty additions.
- **Continue watching.** The resume list as a channel, with progress bars.
- **Album art channel.** Cover art big while music plays, with a slow colour wash behind.

From the web, cached:

- **Lodestone news.** *Built.* Headlines and patch notes, one every ten seconds.
- **Market watch.** *Built.* Universalis prices for a watch list, with a chart and a ticker.
- **Twitch chat** beside the picture for Twitch sources.
- **RSS reader.** Any feed pasted in, as a news channel with a ticker.

Pure atmosphere:

- **Aquarium.** *Built.* Pixel-art fish drifting across, the odd Namazu.
- **Fireplace** *(built)*, **rain on a window.** Procedural.
- **Starfield, Mystify, Pipes, 3D Maze** *(built)*, **Flying Toasters.** The screensavers.
- **Colour bars with a tone**, the sign-off, "PLEASE STAND BY".
- **Demoscene channel.** *Plasma built*; a rotozoomer and a scrolling greeting to come.
- **Fake ad breaks.** Bumpers for in-game things: "Visit the Gold Saucer".

Interactive, with the remote:

- **Teletext**, with a page dial on the remote.
- **Trivia.** A question card with a countdown; questions from a file; answers in chat.
- **Jukebox channel.** The music list on screen, next and previous on the remote, cover art from Plex.

A page dial on the remote is shared by teletext, trivia and the jukebox, so it is the piece to
build first. Picks: the aquarium for delight, the Eorzea clock for utility, the "ON AIR" card
because it finishes watch-together.

## Picks

If choosing for the next stretch: the sync anchor (watch-together is what gets others to
install it, and drift is what makes it feel broken), the library-as-a-channel (distinctive),
the health panel (stops the next friend bouncing off setup), and the weather channel (for fun).
Keybinds and auto-pause in duty are the ones people would quietly rely on.
