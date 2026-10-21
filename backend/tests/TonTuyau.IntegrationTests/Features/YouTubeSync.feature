@elasticsearch @sync @api @youtube
Feature: The YouTube syncs
  TonTuyau downloads its subscribed channels' videos itself (2.0, B5). The incremental sync lists each subscribed
  channel's ticked tabs, newest first, down to the first video the library has, and downloads what's newer; a channel
  never synced ("Add a channel") is listed whole. The full sync, by hand only, lists every tab whole and downloads what
  the library doesn't have, and the files missing of the videos it has. Streams still to come aren't downloaded; a
  video that can't be is noted, and the others go on. YouTube refusing puts the sync on hold, from where it was.
  yt-dlp is a fake here; TubeArchivist's subscription to "alpha" came with the import, as synced.

  Background:
    Given TubeArchivist has the channel "alpha" with 2 videos
    And TubeArchivist is subscribed to the channel "alpha"
    And the backend is ready
    And a first import has run
    And I logged in as the admin
    And there's no pause between videos

  Scenario: The incremental sync downloads what's newer than the first video the library has
    Given YouTube has the videos "alpha-05, alpha-04, alpha-03"
    And YouTube lists "alpha-05, alpha-04, alpha-02, alpha-03" on the videos tab of the channel "alpha"
    When I start the YouTube sync with my session
    Then the response status is 202
    And "task" is "youTubeSync"
    And the latest run ends succeeded within 60 seconds
    And the media folder has the file of the video "alpha-05" with 4096 bytes
    And the media folder has the file of the video "alpha-04" with 4096 bytes
    And TonTuyau doesn't have the video "alpha-03"
    And the latest run's "counts.created" is "2" within 5 seconds
    And the latest run's "counts.channelsRead" is "1" within 5 seconds
    And the latest run's notes mention ": 2 videos to download."

  Scenario: Another channel's videos listed on a subscribed channel's tab are downloaded once, not at every sync (alpha.5)
    Given YouTube has the videos "beta-02, beta-01"
    And YouTube lists "beta-02, beta-01, alpha-02" on the videos tab of the channel "alpha"
    When I start the YouTube sync with my session
    Then the latest run ends succeeded within 60 seconds
    And the media folder has the file of the video "beta-02" with 4096 bytes
    And the media folder has the file of the video "beta-01" with 4096 bytes
    When yt-dlp's downloads are forgotten
    And I start the YouTube sync with my session
    Then the latest run ends succeeded within 60 seconds
    And yt-dlp didn't download the video "beta-02"
    And yt-dlp didn't download the video "beta-01"
    When I start the full YouTube sync with my session
    Then the latest run ends succeeded within 60 seconds
    And yt-dlp didn't download the video "beta-02"

  Scenario: A stream still to come isn't downloaded, and a video that can't be is noted while the others go on
    Given YouTube has the videos "alpha-04"
    And YouTube says of the video "alpha-05": "ERROR: [youtube] alpha-05___: Join this channel to get access to members-only content"
    And YouTube lists "alpha-06 (upcoming), alpha-05, alpha-04, alpha-02" on the streams tab of the channel "alpha"
    When I start the YouTube sync with my session
    Then the latest run ends succeeded within 60 seconds
    And the media folder has the file of the video "alpha-04" with 4096 bytes
    And TonTuyau doesn't have the video "alpha-05"
    And TonTuyau doesn't have the video "alpha-06"
    And the latest run's "counts.downloadFailed" is "1" within 5 seconds
    And the latest run's notes mention "(alpha-05___) not downloaded"

  Scenario: A failed download is recorded with why; failed 3 times, the incremental sync leaves it, the full one doesn't
    Given YouTube says of the video "alpha-05": "ERROR: [youtube] alpha-05___: Requested format is not available. Use --list-formats for a list of available formats"
    And YouTube lists "alpha-05, alpha-02" on the videos tab of the channel "alpha"
    When I start the YouTube sync with my session
    Then the latest run ends succeeded within 60 seconds
    And the latest run's notes mention "Video alpha-05" and "(alpha-05___) not downloaded: Requested format is not available"
    When I request the failed downloads of the channel "alpha" with my session
    Then the response status is 200
    And "" has 1 item
    And "[0].videoId" is "alpha-05___"
    And "[0].title" is "Video alpha-05"
    And "[0].tries" is "1"
    And "[0].skipped" is "false"
    And "[0].reason" mentions "Requested format is not available"
    And "[0].youTubeUrl" is "https://www.youtube.com/watch?v=alpha-05___"
    When I start the YouTube sync with my session
    Then the latest run ends succeeded within 60 seconds
    When I start the YouTube sync with my session
    Then the latest run ends succeeded within 60 seconds
    And the latest run's notes mention "Failed 3 times: the syncs leave it now."
    When I start the YouTube sync with my session
    Then the latest run ends succeeded within 60 seconds
    And the latest run's "counts.downloadFailed" is "0" within 5 seconds
    When I start the full YouTube sync with my session
    Then the latest run ends succeeded within 60 seconds
    When I request the failed downloads of the channel "alpha" with my session
    Then "[0].tries" is "4"
    And "[0].skipped" is "true"

  Scenario: Failed downloads are tried again, one or all of them, and leave the list once downloaded
    Given YouTube says of the video "alpha-05": "ERROR: [youtube] alpha-05___: Requested format is not available"
    And YouTube says of the video "alpha-04": "ERROR: [youtube] alpha-04___: Requested format is not available"
    And YouTube lists "alpha-05, alpha-04, alpha-02" on the videos tab of the channel "alpha"
    When I start the YouTube sync with my session
    Then the latest run ends succeeded within 60 seconds
    Given YouTube has the videos "alpha-05, alpha-04"
    When YouTube no longer refuses the video "alpha-05"
    And I download "alpha-05___" with my session
    Then the latest run ends succeeded within 60 seconds
    When I request the failed downloads of the channel "alpha" with my session
    Then "" has 1 item
    And "[0].videoId" is "alpha-04___"
    When YouTube no longer refuses the video "alpha-04"
    And I try the failed downloads of the channel "alpha" again with my session
    Then the response status is 202
    And "task" is "retryDownloads"
    And the latest run ends succeeded within 60 seconds
    And the media folder has the file of the video "alpha-04" with 4096 bytes
    When I request the failed downloads of the channel "alpha" with my session
    Then "" has 0 items
    When I try the failed downloads of the channel "alpha" again with my session
    Then the response status is 409

  Scenario: A channel never synced is listed whole, then only down to what the library has
    Given YouTube has the channel "omega": the name "Omega", 500 subscribers
    When I add the channel "omega" with the tabs "videos" with my session
    Then the latest run ends succeeded within 30 seconds
    Given YouTube has the videos "omega-03, omega-02, omega-01"
    And YouTube lists "omega-03, omega-02, omega-01" on the videos tab of the channel "omega"
    When I start the YouTube sync with my session
    Then the latest run ends succeeded within 60 seconds
    And the media folder has the file of the video "omega-01" with 4096 bytes
    And the latest run's "counts.created" is "3" within 5 seconds
    Given YouTube has the videos "omega-04"
    And YouTube lists "omega-04, omega-03, omega-02, omega-01" on the videos tab of the channel "omega"
    When I start the YouTube sync with my session
    Then the latest run ends succeeded within 60 seconds
    And the media folder has the file of the video "omega-04" with 4096 bytes
    And the latest run's "counts.created" is "1" within 5 seconds

  Scenario: The full sync lists every tab whole, and downloads the files missing too
    Given YouTube has the videos "alpha-03, alpha-01"
    And the file of the video "alpha-01" was found missing
    And YouTube lists "alpha-02, alpha-03, alpha-01" on the videos tab of the channel "alpha"
    When I start the full YouTube sync with my session
    Then the response status is 202
    And "task" is "youTubeFullSync"
    And the latest run ends succeeded within 60 seconds
    And the media folder has the file of the video "alpha-03" with 4096 bytes
    And the media folder has the file of the video "alpha-01" with 4096 bytes
    And the video "alpha-01" isn't flagged as missing its file
    And the latest run's "counts.created" is "1" within 5 seconds
    And the latest run's "counts.updated" is "1" within 5 seconds

  Scenario: "Download everything" of one channel is its full sync alone
    Given YouTube has the channel "omega": the name "Omega", 500 subscribers
    When I add the channel "omega" with the tabs "videos" with my session
    Then the latest run ends succeeded within 30 seconds
    Given YouTube has the videos "alpha-03, omega-01"
    And YouTube lists "alpha-02, alpha-03, alpha-01" on the videos tab of the channel "alpha"
    And YouTube lists "omega-01" on the videos tab of the channel "omega"
    When I download everything of the channel "alpha" with my session
    Then the response status is 202
    And "task" is "youTubeFullSync"
    And "target" is "UCalpha_________________"
    And the latest run ends succeeded within 60 seconds
    And the media folder has the file of the video "alpha-03" with 4096 bytes
    And TonTuyau doesn't have the video "omega-01"
    And the latest run's "counts.channelsRead" is "1" within 5 seconds

  Scenario: "Download everything" needs the channel subscribed to, and a sync of one channel is the full one
    When I download everything of the channel "nobody" with my session
    Then the response status is 409
    And "detail" mentions "subscribe to it first"
    When I send POST "/api/admin/tasks/youTubeFullSync/runs?target=nope" with my session
    Then the response status is 400
    When I send POST "/api/admin/tasks/youTubeSync/runs?target=UCalpha_________________" with my session
    Then the response status is 400

  Scenario: A tab that isn't ticked isn't listed
    Given YouTube has the videos "alpha-07"
    And YouTube lists "alpha-07" on the shorts tab of the channel "alpha"
    When I subscribe to the channel "alpha" with '{"videos":true,"shorts":false,"streams":true}' with my session
    And I start the YouTube sync with my session
    Then the latest run ends succeeded within 60 seconds
    And TonTuyau doesn't have the video "alpha-07"

  Scenario: A tab YouTube doesn't answer is noted, and the sync goes on with the others
    Given YouTube has the videos "alpha-04"
    And YouTube says of the videos tab of the channel "alpha": "ERROR: [youtube:tab] Unable to download API page: HTTP Error 503"
    And YouTube lists "alpha-04, alpha-02" on the streams tab of the channel "alpha"
    When I start the YouTube sync with my session
    Then the latest run ends succeeded within 60 seconds
    And the media folder has the file of the video "alpha-04" with 4096 bytes
    And the latest run's "counts.checkFailed" is "1" within 5 seconds
    And the latest run's notes mention "its videos weren't listed"

  Scenario: YouTube refusing puts the sync on hold, and Resume now goes on from there
    Given YouTube has the videos "alpha-09"
    And YouTube says of the video "alpha-08": "ERROR: [youtube] alpha-08___: Sign in to confirm you’re not a bot"
    And YouTube lists "alpha-09, alpha-08, alpha-02" on the videos tab of the channel "alpha"
    When I start the YouTube sync with my session
    Then the latest run is onHold within 60 seconds
    And the media folder has the file of the video "alpha-09" with 4096 bytes
    And the latest run's "left" is "1" within 5 seconds
    Given YouTube has the videos "alpha-08"
    When YouTube no longer refuses the video "alpha-08"
    And I resume the latest run with my session
    Then the latest run ends succeeded within 60 seconds
    And the media folder has the file of the video "alpha-08" with 4096 bytes

  Scenario: On the command line, the sync is a run like the others
    Given YouTube has the videos "alpha-04"
    And YouTube lists "alpha-04, alpha-02" on the videos tab of the channel "alpha"
    When I run "tontuyau youtube sync"
    Then the command exits with code 0
    And the output mentions "download  1 new, 0 files back, 0 not downloaded"
