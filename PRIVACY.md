# TonTuyau's privacy notes

_Last updated: 2026-10-22 (2.0.0-alpha.8)._

TonTuyau is software you install on your own server. It has no server of its own: whoever runs a TonTuyau decides
what goes in it and who can reach it. This page says what TonTuyau itself stores, where it connects, and what it never
does, so that you, running it, can tell the people who use it.

For LaPompe, TonTuyau's browser extension, see [its own privacy policy](https://github.com/TonTuyau/LaPompe/blob/main/PRIVACY.md).

## In short

- **Its makers receive nothing.** No telemetry, no analytics, no crash reports, no "phone home", no ads.
- **Watching leaves no trace.** No account is needed to browse, and TonTuyau keeps no watch history, no views, no
  searches, and no log of who opened what.
- **Viewers' browsers talk to your TonTuyau only.** Every image and video comes from your server; a page never loads
  anything from YouTube, Google or anyone else.
- **Your server talks to YouTube and GitHub**, for the downloads and for yt-dlp, and to TubeArchivist if you import from
  it. Nowhere else.

## People who browse

| What | Where it stays |
|---|---|
| Display choices: the theme, the language, the volume, the player's size, how Shorts open, the sidebar | the visitor's own browser (`localStorage`); never sent to the server |
| Nothing else | no cookie until someone logs in, no history, no tracking |

The backend writes **no access log**. Its log names a visitor's address in one case only: a **refused login**
("Refused a login from …"), to spot password guessing. The login attempts per address are counted in memory, for one
minute.

The front server's Content-Security-Policy only lets a page load from the server itself (see the README's
[Security](README.md#security)): no third-party scripts, fonts, images or frames.

## The admin account

| What | How it's kept |
|---|---|
| The account: its name, when it was made, when it last logged in | PostgreSQL; the password as an Argon2id hash only, never as such |
| The session | a `tontuyau_session` cookie (HttpOnly, SameSite=Strict, Secure), 8 hours, or 30 days with "Stay logged in" |
| API keys (for LaPompe, scripts) | PostgreSQL: their name, kind, who made them, when they were last used, and a hash of the secret, never the secret itself, which is shown once |

## The YouTube cookie

When LaPompe sends your youtube.com cookies, TonTuyau uses them to read YouTube signed in as you.

- They're kept in **PostgreSQL, as sent**, with when and from which key they came, and what YouTube answered when they
  were checked. Guard the database and its backups accordingly: it holds a YouTube sign-in.
- They're used for **YouTube only**: each yt-dlp run gets them in a file of its own, readable by the backend only, and
  deleted when the run ends.
- They're **never written to a log** and never shown back, not even to the admin.
- **Remove it** on the YouTube page deletes them; TonTuyau then reads YouTube signed out.

## What it keeps from YouTube

The library: the videos and their files, the channels, their images, and what YouTube says of them (titles,
descriptions, views, chapters…). Optionally (the YouTube page's comments setting, off by default), **the comments**,
with their authors' public YouTube names, ids and pictures.

All of it is shown to **anyone who can reach your TonTuyau**, since browsing needs no login. If you open it beyond your
home, that includes other people's comments: decide with that in mind.

## Where your server connects

| To | When | Why |
|---|---|---|
| YouTube (`youtube.com` and its video servers, through yt-dlp) | the syncs, downloads and refreshes you start or schedule | to read and download videos and channels; signed in when there's a cookie |
| YouTube's image servers (`i.ytimg.com`, `yt3.googleusercontent.com`, `yt3.ggpht.com`) | with the above | to copy the videos' and channels' images to your server |
| GitHub (`api.github.com`, `github.com`) | at startup, then once a day | to find the yt-dlp version chosen on the YouTube page, and download it (checked against its published checksums) |
| Your TubeArchivist | at startup, only while `TONTUYAU_TA_URL` is set | the import, read-only |

The requests to GitHub and to YouTube's image servers say they come from TonTuyau (`TonTuyau
(+https://github.com/TonTuyau/TonTuyau)`); yt-dlp's requests to YouTube are yt-dlp's own.

**Link previews**, only when you set `TONTUYAU_PUBLIC_URL`: when someone shares a TonTuyau link in Discord, Messenger
or the like, that app's servers fetch the page and its preview from your TonTuyau, as any visitor would.

## How long

Until you delete it. The library, the runs and their logs, the accounts and the keys stay in PostgreSQL; the files and
images stay in their folders. Removing the YouTube cookie or deleting an API key erases it at once. The containers'
own logs follow Docker's log settings.

## Questions

Open an issue on [GitHub](https://github.com/TonTuyau/TonTuyau/issues); for a security problem, report it privately
through the repository's **Security → Report a vulnerability**.
