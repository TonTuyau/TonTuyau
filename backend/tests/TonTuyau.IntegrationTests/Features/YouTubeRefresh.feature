@elasticsearch @sync @api @youtube
Feature: Refreshing videos and channels on YouTube
  An admin (or the command line) reads a video, a channel, or a channel and each of its videos on YouTube (1.5): a run
  like the import, in the history and the log. What YouTube answers becomes TonTuyau's facts, then the index shows them:
  found, its metadata wins over TubeArchivist's; gone (removed, private, terminated), it's flagged; no answer to trust
  (the network, a timeout), it's to retry, and nothing TonTuyau confirmed before changes; YouTube refusing (a bot check,
  too many requests) puts the run on hold (YouTubeHold.feature). yt-dlp is a fake here.

  Background:
    Given TubeArchivist has the channel "alpha" with 3 videos
    And the backend is ready
    And a first import has run
    And I logged in as the admin

  Scenario: A video found on YouTube takes YouTube's metadata
    Given YouTube has the video "alpha-01": the title "Le vrai titre", 1234 views
    When I refresh the video "alpha-01" with my session
    Then the response status is 202
    And "task" is "refreshVideo"
    And "target" is "alpha-01___"
    And the latest run ends succeeded within 20 seconds
    And the stored YouTube state of the video "alpha-01" is "on YouTube"
    When I request the latest run's "" with my session
    Then "counts.foundOnYouTube" is "1"
    And "counts.updated" is "1"
    When I request the video "alpha-01" of TubeArchivist
    Then "title" is "Le vrai titre"
    And "views" is "1234"
    # Its channel's name is the channel's: a refresh of the channel reads it.
    And "channel.name" is "alpha"
    And "active" is "true"

  Scenario: A video YouTube says is removed is flagged
    Given YouTube says of the video "alpha-02": "ERROR: [youtube] alpha-02___: Video unavailable. This video has been removed by the uploader"
    When I refresh the video "alpha-02" with my session
    Then the latest run ends succeeded within 20 seconds
    And the stored YouTube state of the video "alpha-02" is "gone"
    When I request the video "alpha-02" of TubeArchivist
    Then "active" is "false"

  Scenario: No answer to trust: to retry, never gone
    Given YouTube says of the video "alpha-02": "ERROR: [youtube] alpha-02___: Unable to download API page: <urlopen error timed out>"
    When I refresh the video "alpha-02" with my session
    Then the latest run ends succeeded within 20 seconds
    And the stored YouTube state of the video "alpha-02" is "retry later"
    When I request the latest run's "" with my session
    Then "counts.checkFailed" is "1"
    When I request the video "alpha-02" of TubeArchivist
    Then "active" is "true"

  Scenario: A failed check changes nothing TonTuyau confirmed
    Given TonTuyau refreshed the video "alpha-02": the title "Confirmé", 7 views
    And YouTube says of the video "alpha-02": "ERROR: [youtube] alpha-02___: Unable to download webpage: HTTP Error 503: Service Unavailable"
    When I refresh the video "alpha-02" with my session
    Then the latest run ends succeeded within 20 seconds
    And the stored YouTube state of the video "alpha-02" is "on YouTube"

  Scenario: A channel and its videos
    Given there's no pause between videos
    And YouTube has the channel "alpha": the name "Alpha officiel", 5000 subscribers
    And YouTube has the video "alpha-01": the title "Premier", 10 views
    And YouTube says of the video "alpha-02": "ERROR: [youtube] alpha-02___: Private video. Sign in if you've been granted access to this video"
    When I refresh the channel "alpha" and its videos with my session
    Then the latest run ends succeeded within 30 seconds
    When I request the latest run's "" with my session
    Then "counts.channelsRead" is "1"
    And "counts.videosRead" is "3"
    And "counts.foundOnYouTube" is "1"
    And "counts.goneFromYouTube" is "0"
    And "counts.privateOnYouTube" is "1"
    And "counts.checkFailed" is "1"
    And "notes[0]" is "The channel: on YouTube."
    When I request the channel "alpha" of TubeArchivist
    Then "name" is "Alpha officiel"
    And "subscribers" is "5000"
    When I request the video "alpha-01" of TubeArchivist
    Then "title" is "Premier"
    And the stored YouTube state of the video "alpha-02" is "private"
    And the stored YouTube state of the video "alpha-03" is "retry later"

  Scenario: The run shows what it read so far, while it goes on
    Given YouTube has the channel "alpha": the name "Alpha officiel", 5000 subscribers
    And YouTube says of the video "alpha-01": "ERROR: [youtube] alpha-01___: Video unavailable. This video has been removed by the uploader"
    And YouTube says of the video "alpha-02": "ERROR: [youtube] alpha-02___: Video unavailable. This video has been removed by the uploader"
    And YouTube says of the video "alpha-03": "ERROR: [youtube] alpha-03___: Video unavailable. This video has been removed by the uploader"
    And the pause between videos is 60 seconds
    When I refresh the channel "alpha" and its videos with my session
    Then the latest run's "counts.goneFromYouTube" is "1" within 20 seconds
    And "state" is "running"
    And "counts.videosRead" is "1"
    When I cancel the latest run with my session
    Then the latest run ends canceled within 20 seconds

  Scenario: An unlisted video is on YouTube, with its own state
    Given YouTube has the unlisted video "alpha-01": the title "Par lien", 5 views
    When I refresh the video "alpha-01" with my session
    Then the latest run ends succeeded within 20 seconds
    And the stored YouTube state of the video "alpha-01" is "unlisted"
    When I request the latest run's "" with my session
    Then "counts.foundOnYouTube" is "1"
    And "counts.unlistedOnYouTube" is "1"
    When I request "/api/videos/alpha-01___" with my session
    Then "youTubeState" is "unlisted"
    And "title" is "Par lien"
    And "active" is "true"

  Scenario: A video made private is private, and read again at the next refresh
    Given YouTube says of the video "alpha-02": "ERROR: [youtube] alpha-02___: Private video. Sign in if you've been granted access to this video"
    When I refresh the video "alpha-02" with my session
    Then the latest run ends succeeded within 20 seconds
    And the stored YouTube state of the video "alpha-02" is "private"
    When I request the latest run's "" with my session
    Then "counts.privateOnYouTube" is "1"
    And "counts.goneFromYouTube" is "0"
    When I request "/api/videos/alpha-02___" with my session
    Then "youTubeState" is "private"
    And "active" is "true"
    Given YouTube has the video "alpha-02": the title "De retour", 3 views
    When I refresh the video "alpha-02" with my session
    Then the latest run ends succeeded within 20 seconds
    And the stored YouTube state of the video "alpha-02" is "on YouTube"

  Scenario: A gone video isn't refreshed any more: refused alone, skipped with its channel
    Given TonTuyau found the video "alpha-02" gone from YouTube
    And there's no pause between videos
    When I refresh the video "alpha-02" with my session
    Then the response status is 409
    And "detail" mentions "is gone from YouTube: it isn't refreshed any more."
    Given YouTube has the channel "alpha": the name "Alpha officiel", 5000 subscribers
    And YouTube has the video "alpha-01": the title "Un", 1 views
    And YouTube has the video "alpha-03": the title "Trois", 3 views
    When I refresh the channel "alpha" and its videos with my session
    Then the latest run ends succeeded within 30 seconds
    And PostgreSQL has the video "alpha-02" as "gone"
    When I request the latest run's "" with my session
    Then "counts.videosRead" is "2"
    And "counts.foundOnYouTube" is "2"
    And "notes[0]" is "1 gone video skipped for the refresh: it isn't read any more."
    And the last run's log has, in order, lines with:
      | level       | text                                                  |
      | information | Gone video, skipped for the refresh: alpha-02___ (     |

  Scenario: Refreshing a video records its channel, not checked: its badge still follows TubeArchivist
    Given YouTube has the video "alpha-01": the title "Le vrai titre", 1234 views
    When I refresh the video "alpha-01" with my session
    Then the latest run ends succeeded within 20 seconds
    And PostgreSQL has the channel "alpha" as "not checked"
    When I request the channel "alpha" of TubeArchivist
    Then "active" is "true"

  Scenario: Comments aren't read by default (the YouTube page's setting); chapters are
    Given YouTube has the video "alpha-01" with 2 comments and 3 chapters
    When I refresh the video "alpha-01" with my session
    Then the latest run ends succeeded within 20 seconds
    And PostgreSQL has no comments of the video "alpha-01", never read
    And PostgreSQL has the chapters of the video "alpha-01": "0-30 Part 1 | 30-60 Part 2 | 60-90 Part 3"

  Scenario: The YouTube settings, off by default, changed by an admin
    When I request "/api/admin/youtube/settings" with my session
    Then the response status is 200
    And the answer has exactly the fields "comments, commentsMax, pauseSeconds, holdMinutes, syncEveryMinutes, downloadFormat, defaultTabs, changedBy, changedAt"
    And "comments" is "off"
    And "commentsMax" is "500"
    And "pauseSeconds" is "10"
    And "holdMinutes" is "60"
    And "changedBy" is null
    When I send the YouTube settings '{"comments": "top", "commentsMax": 250000}' with my session
    Then the response status is 200
    And "comments" is "top"
    And "commentsMax" is "250000"
    And "changedBy" is "admin"
    When I send the YouTube settings '{"comments": "all", "pauseSeconds": 4, "holdMinutes": 15}' with my session
    Then "comments" is "all"
    And "commentsMax" is "250000"
    And "pauseSeconds" is "4"
    And "holdMinutes" is "15"
    When I request "/api/admin/youtube/settings" with my session
    Then "comments" is "all"

  Scenario Outline: A setting missing or wrong is refused
    When I send the YouTube settings '<body>' with my session
    Then the response status is 400

    Examples:
      | body                                       |
      | {}                                         |
      | {"comments": "yes"}                        |
      | {"comments": "top", "commentsMax": 0}      |
      | {"comments": "top", "commentsMax": -3}     |
      | {"comments": true}                         |
      | {"comments": "off", "pauseSeconds": -1}    |
      | {"comments": "off", "holdMinutes": 0}      |

  Scenario: A video's comments (answers included) and chapters are kept, replaced at each refresh
    Given reading comments is on
    And YouTube has the video "alpha-01" with 2 comments and 3 chapters
    When I refresh the video "alpha-01" with my session
    Then the latest run ends succeeded within 20 seconds
    And PostgreSQL has the comments of the video "alpha-01": "Comment 1 by @viewer1 | ↳Reply 1 by @alpha | Comment 2 by @viewer2 | ↳Reply 2 by @alpha"
    And PostgreSQL has the chapters of the video "alpha-01": "0-30 Part 1 | 30-60 Part 2 | 60-90 Part 3"
    When I request the latest run's "" with my session
    Then "counts.commentsRead" is "4"
    Given YouTube has the video "alpha-01" with 1 comments and 0 chapters
    When I refresh the video "alpha-01" with my session
    Then the latest run ends succeeded within 20 seconds
    And PostgreSQL has the comments of the video "alpha-01": "Comment 1 by @viewer1 | ↳Reply 1 by @alpha"
    And PostgreSQL has the chapters of the video "alpha-01": ""

  Scenario: Comments yt-dlp couldn't read: those read before stay
    Given reading comments is on
    And YouTube has the video "alpha-01" with 1 comments and 0 chapters
    When I refresh the video "alpha-01" with my session
    Then the latest run ends succeeded within 20 seconds
    Given YouTube has the video "alpha-01": the title "Sans commentaires lus", 10 views
    When I refresh the video "alpha-01" with my session
    Then the latest run ends succeeded within 20 seconds
    And PostgreSQL has the comments of the video "alpha-01": "Comment 1 by @viewer1 | ↳Reply 1 by @alpha"

  Scenario: A channel alone leaves its videos as they are, and counts its own answer
    Given YouTube has the channel "alpha": the name "Alpha officiel", 5000 subscribers
    When I refresh the channel "alpha" with my session
    Then the latest run ends succeeded within 20 seconds
    When I request the latest run's "" with my session
    Then "counts.foundOnYouTube" is "1"
    And "notes" has 0 items
    And the stored YouTube state of the video "alpha-01" is "not checked"

  Scenario: Only what the library has, with its target
    When I send POST "/api/admin/tasks/refreshVideo/runs?target=zzzzzzzzzzz" with my session
    Then the response status is 409
    When I send POST "/api/admin/tasks/refreshChannel/runs?target=not-a-channel" with my session
    Then the response status is 409
    When I send POST "/api/admin/tasks/refreshVideo/runs" with my session
    Then the response status is 400
    When I send POST "/api/admin/tasks/import/runs?target=alpha-01___" with my session
    Then the response status is 400

  Scenario: From the command line
    Given YouTube has the video "alpha-01": the title "Le vrai titre", 1234 views
    When I run "tontuyau youtube video alpha-01___"
    Then the command exits with code 0
    And the output mentions "youtube   1 found (0 unlisted), 0 gone,"
    When I run "tontuyau youtube channel nope"
    Then the command exits with code 4

  Scenario: The run history shows each refresh under its own task
    Given YouTube has the video "alpha-01": the title "Le vrai titre", 1234 views
    When I refresh the video "alpha-01" with my session
    Then the latest run ends succeeded within 20 seconds
    When I request "/api/admin/runs?task=refreshVideo" with my session
    Then "total" is "1"
    And "items[0].task" is "refreshVideo"
    When I request "/api/admin/runs?task=refreshChannel" with my session
    Then "total" is "0"
    When I request "/api/admin/runs?task=indexRebuild" with my session
    Then "total" is "0"
