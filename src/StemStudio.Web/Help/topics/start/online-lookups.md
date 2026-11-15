---
id: online-lookups
title: "Online lookups"
area: start
summary: "Pictures and band information come from open data services. They are shown in the app only and can be switched off."
anchors: ["online-lookups"]
keywords: ["online", "offline", "privacy", "musicbrainz", "wikipedia", "cover art archive", "switch off", "disable"]
related: [band-info, artwork, legal-notice]
reviewed: "005"
---

After an import StemStudio looks the song up in open data services: MusicBrainz and the Cover Art Archive for the
recording, album and cover, Wikidata and Wikipedia for the artist. Only the title, artist, album, duration and ISRC
of the song are sent; no audio and nothing about you. Requests are slow on purpose (about one per second) to respect
the services' limits.

Pictures and band texts are shown in the app only. They are never part of an export.

## Switching lookups off

Set `STEMSTUDIO_ONLINE_LOOKUPS=off` for the StemStudio worker (in `docker-compose.yml` or your environment) and
restart it. New songs then get no band information and only the picture of their own source; the band info panel
says that lookups are off. Set it back to `on` and use **Search again** in the panel to look a song up.
