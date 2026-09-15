<p align="center"><img src="src/Aetherstream.Plugin/images/banner.png" alt="Aetherstream — live from the Lifestream" width="660"></p>

# Aetherstream

A television for Final Fantasy XIV. It puts a picture on a real furnishing in your house, so the
game lights it and walks in front of it like anything else in the room, and it comes with a remote.

**[aetherstream.corkedfever.com](https://aetherstream.corkedfever.com/)** has the install guide and screenshots on one page.

## What it plays

- **Live TV.** Thousands of free channels with a guide, channel numbers you pick, and a remote with a number pad.
- **Your own stuff.** Plex, Jellyfin or Emby, and any folder of videos on your PC.
- **Free services.** Pluto TV, Red Bull TV, PBS, NASA+, TED, Dailymotion and the Internet Archive.
- **Sites.** YouTube (with your subscriptions and watch later if you sign in), Twitch, Kick, and pretty much anything yt-dlp can open.
- **Paste a link.** Any stream URL, a Twitch channel name, a party code.
- **Watch parties.** Make a party, send a six-character code, and a room full of friends sees what you're broadcasting.
- **Radio and podcasts.** Thirty thousand internet radio stations, any podcast feed, and your orchestrion rolls.

## Channels the set draws itself

This is the odd part, and the fun part. Besides playing video, Aetherstream is a little broadcaster
that draws its own channels from the game's data. They show on the furnishing like anything else.

- **Info.** A TV Guide-style listings channel, Eorzean weather with "Local on the 8s", market prices from Universalis, timed gathering nodes, your fishing windows, open venues tonight, housing plots for sale, daily resets, and a horoscope for your guardian deity.
- **Shows.** A cooking show with a Namazu chef making real recipes. A moogle wildlife presenter who gets too close to the creature of the week. A moogle in a beret painting vistas from your sightseeing log. A pixie doing the weather. A kobold running the Gold Saucer numbers like a game show. A sahagin reading the ocean fishing forecast. A mandragora proving everything was the Allagans. A home shopping channel with a goblin host selling things off the market board. A cinema channel that writes a whole film every five minutes. And commercial breaks between them.
- **Stories.** A tonberry by the fire telling tales of the Twelve as a shadow play.
- **Ambience.** An aquarium, a fireplace, and the old screensavers: starfield, pipes, the 3D maze.

## Installing

1. In Dalamud settings, under *Experimental*, add this custom repository and save:
   `https://raw.githubusercontent.com/CorkedFever/Aetherstream/main/repo.json`
2. Install **Aetherstream** from the plugin installer.
3. Type `/aether` to open it.

### A couple of things to install yourself

Most of it works with nothing else installed. For the rest:

| For | Install | Then |
| --- | --- | --- |
| YouTube, Kick, Dailymotion, PBS and most other sites | `winget install --id yt-dlp.yt-dlp --exact` | Restart the game. It only looks for yt-dlp when it starts. |
| YouTube specifically | `winget install --id DenoLand.Deno --exact` | Restart the game. yt-dlp needs it to get past YouTube's checks. |
| Hosting a watch party | `winget install --id Gyan.FFmpeg --exact` | Watching a party needs nothing. |

The plugin never downloads these for you. If a YouTube link does nothing, the screen tells you why.
If YouTube stops working one day, `yt-dlp -U` is the first thing to try.

### The first five minutes

- **Put it on a screen.** Press the gear on the remote, then Screen, then Known screens, and pick the Everkeep Monitor while standing next to yours. Paint the wall behind it black in game. That one change makes the picture look right, because the monitor is an additive effect and anything behind it bleeds through.
- **Give channels numbers.** In Live TV, right-click a channel to pin it. The remote's number pad and the channel up and down keys work on your pins.
- **Try the drawn channels.** The Aetherstream app on the home screen. Press one and it goes up on the set.
- **Host a party.** Watch party, make a party, send the code. Your friends paste it into Paste a link.

## The window

The left side is a remote: a display strip, power, mute and Home, a number pad, channel and
volume rockers, transport, subtitles, and the gear. Fold the window and only the remote stays.

The right side is the television: the picture, one line saying what's on, and a home screen of
apps laid out like a tablet, with Search, Paste a link, Watch party and Setup in a dock along the
bottom. Apps you never use can be hidden under Setup, System.

## Credits and licences

Aetherstream ships with these, and is grateful to all of them.

| | Licence | What it's for |
| --- | --- | --- |
| [libvlc](https://www.videolan.org/vlc/libvlc.html) and [LibVLCSharp](https://github.com/videolan/libvlcsharp) | LGPL 2.1 | All the decoding. This is most of the download. |
| [NAudio](https://github.com/naudio/NAudio) | MIT | Sound output. |
| [ImageSharp](https://github.com/SixLabors/ImageSharp) | Six Labors Split Licence | Decoding images for posters and banners. |
| [VT323](https://fonts.google.com/specimen/VT323) | SIL OFL 1.1 | The display face. |
| Bossa Antigua, Lobby Time, Backbay Lounge, Airport Lounge, Deuces by [Kevin MacLeod](https://incompetech.com) | CC BY 4.0 | The lounge music under the drawn channels. |
| [Dalamud](https://github.com/goatcorp/Dalamud) | AGPL 3.0 | The plugin host. |

Data comes from [iptv-org](https://github.com/iptv-org/iptv) for live TV, [Universalis](https://universalis.app/) for prices,
[PaissaDB](https://zhu.codes/paissa) for housing, [ffxivvenues.com](https://ffxivvenues.com/) for venues, [Radio Browser](https://www.radio-browser.info/) for stations,
the [Carbuncle Plushy fish tracker](https://github.com/icykoneko/ff14-fish-tracker-app) (MIT) for fishing windows, and the
game's own tables through Dalamud for everything else. The free video services are used through their own public endpoints, with no
accounts and nothing bypassed; anything behind DRM or a paywall is left alone on purpose.

Renders of the Twelve are Square Enix's, used as fan art inside a plugin for their own game.

## For developers

Engineering notes, the release process, and the long list of things that have bitten this project
live in [docs/DEVELOPING.md](docs/DEVELOPING.md). Ideas that aren't planned are parked in [IDEAS.md](IDEAS.md).
