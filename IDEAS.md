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
- **Weather channel.** Eorzea weather and time for the current zone on a Weather-Channel-style
  card, with music.

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

## Picks

If choosing for the next stretch: the sync anchor (watch-together is what gets others to
install it, and drift is what makes it feel broken), the library-as-a-channel (distinctive),
the health panel (stops the next friend bouncing off setup), and the weather channel (for fun).
Keybinds and auto-pause in duty are the ones people would quietly rely on.
