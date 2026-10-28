@elasticsearch @sync @api @youtube
Feature: Purging a channel's videos, refreshing them, and downloading them again with other settings
  A purge (2.3.0, the task purgeVideos) deletes videos of one channel from TonTuyau: some of them, or all of them. Each
  goes with its file, its thumbnail, its comments and its failed download; the channel and its subscription stay.
  Nothing is asked of YouTube: the channel's next sync reads its tabs whole and downloads them again. Its files only
  (filesOnly): each video stays, its metadata with it, its syncs blocked and out of the library's lists until it's
  unblocked on the channel's page. The videos picked on a channel's
  page are refreshed (refreshVideos) or downloaded again (redownloadVideos), the new file replacing the old one. Those
  downloads, and "Download everything" of one channel, take yt-dlp's format and the retry without the cookie this once,
  the YouTube page's settings left as they are. yt-dlp is a fake here.

  Background:
    Given TubeArchivist has the channel "alpha" with 2 videos
    And TubeArchivist is subscribed to the channel "alpha"
    And the backend is ready
    And a first import has run
    And I logged in as the admin
    And there's no pause between videos

  Scenario: The videos picked are purged, files and all; the others stay
    Given YouTube has the video "alpha-04": the title "À purger", 3 views
    When I download "alpha-04___" with my session
    Then the latest run ends succeeded within 30 seconds
    And the media folder has the file of the video "alpha-04" with 4096 bytes
    When I purge the videos "alpha-04, alpha-01" of the channel "alpha" with my session
    Then the response status is 202
    And "task" is "purgeVideos"
    And "target" is "UCalpha_________________"
    And the latest run ends succeeded within 30 seconds
    And the latest run's "counts.purged" is "2" within 5 seconds
    And the latest run's notes mention "2 videos purged"
    And TonTuyau doesn't have the video "alpha-04"
    And TonTuyau doesn't have the video "alpha-01"
    And TonTuyau has the video "alpha-02"
    And the index doesn't have the video "alpha-04"
    And the media folder doesn't have the file of the video "alpha-04"

  Scenario: The whole channel is purged, then its full sync downloads its videos again
    When I purge the channel "alpha" with my session
    Then the response status is 202
    And the latest run ends succeeded within 30 seconds
    And the latest run's "counts.purged" is "2" within 5 seconds
    And TonTuyau doesn't have the video "alpha-01"
    And TonTuyau doesn't have the video "alpha-02"
    Given YouTube has the videos "alpha-01"
    And YouTube lists "alpha-01" on the videos tab of the channel "alpha"
    When I download everything of the channel "alpha" with my session
    Then the latest run ends succeeded within 60 seconds
    And the media folder has the file of the video "alpha-01" with 4096 bytes

  Scenario: Purged whole, the videos are downloaded again by the channel's next sync, an incremental one
    Given YouTube has the videos "alpha-01, alpha-02"
    And YouTube lists "alpha-02, alpha-01" on the videos tab of the channel "alpha"
    When I start the YouTube sync with my session
    Then the latest run ends succeeded within 60 seconds
    When I purge the videos "alpha-01" of the channel "alpha" with my session
    Then the latest run ends succeeded within 30 seconds
    And the latest run's notes mention "The channel's next sync downloads them again."
    When I start the YouTube sync with my session
    Then the latest run ends succeeded within 60 seconds
    And the media folder has the file of the video "alpha-01" with 4096 bytes

  Scenario: Their files only: the videos stay, their syncs blocked and out of the library's lists until unblocked
    Given YouTube has the video "alpha-04": the title "Crème brûlée", 3 views
    When I download "alpha-04___" with my session
    Then the latest run ends succeeded within 30 seconds
    When I ask "purgeVideos" for the videos "alpha-04" of the channel "alpha" with "filesOnly=true" with my session
    Then the response status is 202
    And the latest run ends succeeded within 30 seconds
    And the latest run's notes mention "Their files only"
    And TonTuyau has the video "alpha-04"
    And the media folder doesn't have the file of the video "alpha-04"
    And the index doesn't have the video "alpha-04"
    When I send GET "/api/videos/alpha-04___" with my session
    Then the response status is 404
    When I send GET "/api/admin/channels/UCalpha_________________/blocked-videos" with my session
    Then the response status is 200
    And "[0].video.id" is "alpha-04___"
    And "[0].syncBlocked" is "true"
    When I send GET "/api/admin/channels/UCalpha_________________/videos" with my session
    Then "items[0].video.id" is "alpha-04___"
    And "items[0].syncBlocked" is "true"
    And "items[1].syncBlocked" is "false"
    When I send GET "/api/admin/channels/UCalpha_________________/videos?size=1" with my session
    Then "items" has 2 items
    And "total" is "3"
    When I request the next page of "/api/admin/channels/UCalpha_________________/videos?size=1" with my session
    Then "items" has 1 item
    And "items[0].syncBlocked" is "false"
    And "total" is "3"
    When I send GET "/api/admin/channels/UCalpha_________________/videos?blocked=true" with my session
    Then "items" has 1 item
    And "total" is "1"
    When I send GET "/api/admin/channels/UCalpha_________________/videos?q=CREME" with my session
    Then "items" has 1 item
    And "items[0].video.id" is "alpha-04___"
    When I ask "purgeVideos" for the videos "alpha-04" of the channel "alpha" with "filesOnly=true" with my session
    Then the latest run ends succeeded within 30 seconds
    And the latest run's "counts.purged" is "0" within 5 seconds
    When I send GET "/api/admin/channels/UCalpha_________________" with my session
    Then "syncBlocked" is "1"
    Given YouTube lists "alpha-04, alpha-02, alpha-01" on the videos tab of the channel "alpha"
    When I download everything of the channel "alpha" with my session
    Then the latest run ends succeeded within 60 seconds
    And the media folder doesn't have the file of the video "alpha-04"
    When I send DELETE "/api/admin/channels/UCalpha_________________/blocked-videos?videos=alpha-04___" with my session
    Then the response status is 204
    When I send GET "/api/videos/alpha-04___" with my session
    Then the response status is 200
    And "mediaAvailable" is "false"
    When I send GET "/api/admin/channels/UCalpha_________________/blocked-videos" with my session
    Then "" has 0 items

  Scenario: Downloading a video whose syncs are blocked again unblocks it
    When I ask "purgeVideos" for the videos "alpha-01" of the channel "alpha" with "filesOnly=true" with my session
    Then the latest run ends succeeded within 30 seconds
    Given YouTube has the videos "alpha-01"
    When I ask "redownloadVideos" for the videos "alpha-01" of the channel "alpha" with my session
    Then the latest run ends succeeded within 30 seconds
    And the media folder has the file of the video "alpha-01" with 4096 bytes
    When I send GET "/api/videos/alpha-01___" with my session
    Then the response status is 200

  Scenario: On the command line, a channel is purged whole
    When I run "tontuyau youtube purge UCalpha_________________"
    Then the command exits with code 0
    And the output mentions "purged    2 videos"
    And TonTuyau doesn't have the video "alpha-01"

  Scenario: A video of another channel, or one not in the library, isn't purged
    When I purge the videos "beta-01, alpha-09" of the channel "alpha" with my session
    Then the response status is 202
    And the latest run ends succeeded within 30 seconds
    And the latest run's "counts.purged" is "0" within 5 seconds
    And TonTuyau has the video "alpha-01"

  Scenario Outline: A purge the API doesn't take is a 400
    When I send POST "<path>" with my session
    Then the response status is 400

    Examples:
      | path                                                                                  |
      | /api/admin/tasks/purgeVideos/runs                                                     |
      | /api/admin/tasks/purgeVideos/runs?target=nope                                         |
      | /api/admin/tasks/purgeVideos/runs?target=UCalpha_________________&videos=nope         |
      | /api/admin/tasks/purgeVideos/runs?target=UCalpha_________________&videos=,            |
      | /api/admin/tasks/refreshChannel/runs?target=UCalpha_________________&videos=alpha-01___ |
      | /api/admin/tasks/refreshVideos/runs?target=UCalpha_________________                   |
      | /api/admin/tasks/redownloadVideos/runs?target=UCalpha_________________                |
      | /api/admin/tasks/redownloadVideos/runs?videos=alpha-01___                             |
      | /api/admin/tasks/refreshVideos/runs?target=UCalpha_________________&videos=alpha-01___&format=best |
      | /api/admin/tasks/refreshVideos/runs?target=UCalpha_________________&videos=alpha-01___&filesOnly=true |

  Scenario: The videos picked are refreshed on YouTube
    Given YouTube has the video "alpha-01": the title "Renommée", 7 views
    And YouTube has the video "alpha-02": the title "Elle aussi", 8 views
    When I ask "refreshVideos" for the videos "alpha-01, alpha-02" of the channel "alpha" with my session
    Then the response status is 202
    And "task" is "refreshVideos"
    And the latest run ends succeeded within 30 seconds
    And TonTuyau shows the video "alpha-01" with the title "Renommée"
    And TonTuyau shows the video "alpha-02" with the title "Elle aussi"

  Scenario: The videos picked are downloaded again with a format, their new file in place of the old one
    Given YouTube has the videos "alpha-01"
    When I ask "redownloadVideos" for the videos "alpha-01" of the channel "alpha" with "format=398%2B251" with my session
    Then the response status is 202
    And "task" is "redownloadVideos"
    And the latest run ends succeeded within 30 seconds
    And yt-dlp was asked for the format "398+251" for the video "alpha-01"
    And the media folder has the file of the video "alpha-01" with 4096 bytes
    And the latest run's notes mention "is downloaded again"
    And TonTuyau has the video "alpha-01"

  Scenario: A video that can't be downloaded again keeps its file, the others are
    Given YouTube has the videos "alpha-01"
    And YouTube says of the video "alpha-02": "ERROR: [youtube] alpha-02___: Video unavailable"
    When I ask "redownloadVideos" for the videos "alpha-01, alpha-02" of the channel "alpha" with my session
    Then the latest run ends succeeded within 30 seconds
    And the latest run's "counts.downloadFailed" is "1" within 5 seconds
    And the latest run's notes mention "not downloaded again"
    And TonTuyau has the video "alpha-02"
    And the media folder has the file of the video "alpha-01" with 4096 bytes

  Scenario: A channel the library doesn't have isn't purged
    When I purge the channel "nobody" with my session
    Then the response status is 409
    And "detail" mentions "No channel"

  Scenario: "Download everything" with a format: its downloads take it, this once
    Given YouTube has the videos "alpha-03"
    And YouTube lists "alpha-03, alpha-02, alpha-01" on the videos tab of the channel "alpha"
    When I download everything of the channel "alpha" with "format=398%2B251" with my session
    Then the response status is 202
    And the latest run ends succeeded within 60 seconds
    And yt-dlp was asked for the format "398+251" for the video "alpha-03"

  Scenario: "Download everything" without the retry without the cookie: a format matching none isn't downloaded
    Given YouTube, signed in, says of the video "alpha-05": "ERROR: [youtube] alpha-05___: Requested format is not available. Use --list-formats for a list of available formats"
    And I sent the YouTube cookie "SIGNED_IN" with my session
    And YouTube lists "alpha-05, alpha-02, alpha-01" on the videos tab of the channel "alpha"
    When I download everything of the channel "alpha" with "retryWithoutCookie=false" with my session
    Then the response status is 202
    And the latest run ends succeeded within 60 seconds
    And TonTuyau doesn't have the video "alpha-05"
    And the latest run's notes mention "(alpha-05___) not downloaded: Requested format is not available"

  Scenario Outline: Download settings go with "Download everything" of one channel only
    When I send POST "<path>" with my session
    Then the response status is 400

    Examples:
      | path                                                                     |
      | /api/admin/tasks/youTubeFullSync/runs?format=best                        |
      | /api/admin/tasks/youTubeFullSync/runs?retryWithoutCookie=false           |
      | /api/admin/tasks/youTubeSync/runs?retryWithoutCookie=true                |
      | /api/admin/tasks/downloadVideo/runs?target=alpha-09___&retryWithoutCookie=true |
