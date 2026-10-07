# TonTuyau — Future plan: federation

> 2026-10-05. Your idea, written down; **nothing here is built now**. It comes after the TubeArchivist replacement ([FUTURE_TA_REPLACEMENT.md](FUTURE_TA_REPLACEMENT.md)). Two kinds of federation:

## 1. Private federation: between TonTuyau instances

Several TonTuyau instances (yours, friends', family's) link to each other: from your TonTuyau you can browse and search the libraries of the instances you're federated with, and watch their videos.

## 2. Public federation: one central instance

One central TonTuyau instance handles **login**; a signed-in user can reach the libraries of **all** the TonTuyau instances that joined it.

## What this changes compared with today

| Today | With federation |
|---|---|
| LAN only (Q14), no TLS | instances talk across the internet: HTTPS, exposed endpoints, rate limits |
| no user accounts; only the admin logs in (LDAP `tontuyau_admin`) | instance-to-instance credentials (private); user accounts on the central instance (public) |
| one library, one search index | search across instances: forwarded queries, or a shared catalogue |
| media served from the local share | media streamed from the instance that has it (its bandwidth), or copied |

## Questions to settle when this stage starts

- **Trust (private):** how two instances link: an invitation both admins accept, keys exchanged; how a link is revoked.
- **What is shared:** the whole library or chosen channels; metadata only, or metadata + streaming; downloads allowed or not.
- **Search across instances:** each search forwarded to every linked instance (simple, slower, always fresh) or a catalogue copied between instances (fast, needs syncing, like our TubeArchivist sync).
- **Where the video comes from:** streamed from the owning instance through yours (one origin for the browser) or directly (the browser talks to the other instance).
- **Protocol:** our own API between TonTuyau instances, or a standard such as **ActivityPub** (what PeerTube uses for its federation), which would also let TonTuyau talk to other software.
- **Central instance (public):** who runs it; how instances join and leave; whether it stores anything or only routes; how users are vetted.
- **Legal (public mostly):** the libraries are archives of YouTube videos. Serving them to people outside your household, and above all publicly, is redistribution of other people's content: copyright and YouTube's terms apply, and the rules differ by country. This needs a clear answer (which content may be shared, with whom) before the public federation is built; the private one between people you know carries less risk but isn't free of it either.

## Order

| Stage | Content |
|---|---|
| after M8 | TubeArchivist replacement (LDAP admin, admin UI, own downloader) + TubeArchivist Companion compatibility |
| then | **Private federation** (between TonTuyau instances) |
| then | **Public federation** (central login instance), once the private one and the legal question are settled |
