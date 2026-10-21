@elasticsearch @sync @api @youtube
Feature: Refreshing everything from YouTube
  "Refresh everything" (2.0, B8) reads each channel of the library then its videos on YouTube, the least recently read
  first, at the refreshes' pace (a pause between two reads, a hold when YouTube refuses). What TonTuyau read in the last
  days (7 by default, 0: nothing) is skipped, and what's gone is never read again, so a run started after a stop goes on
  with what's left. yt-dlp is a fake here.

  Background:
    Given TubeArchivist has the channel "alpha" with 2 videos
    And TubeArchivist has the channel "beta" with 1 videos
    And the backend is ready
    And a first import has run
    And I logged in as the admin
    And there's no pause between videos
    And YouTube has the channel "alpha": the name "Alpha", 100 subscribers
    And YouTube has the channel "beta": the name "Beta", 200 subscribers
    And YouTube has the videos "alpha-01, beta-01"

  Scenario: Every channel and video is read, then a run within the days skips them all, one at 0 days reads them again
    Given YouTube has the videos "alpha-02"
    When I refresh everything with my session
    Then the response status is 202
    And "task" is "refreshAll"
    And the latest run ends succeeded within 60 seconds
    And the latest run's "counts.foundOnYouTube" is "5" within 5 seconds
    And the latest run's notes mention "5 channels and videos to read, the least recently read first; 0 skipped"
    And the stored YouTube state of the video "beta-01" is "on YouTube"
    When I request the channel "beta" of TubeArchivist
    Then "name" is "Beta"
    When I refresh everything with my session
    Then the latest run ends succeeded within 60 seconds
    And the latest run's notes mention "0 channels and videos to read, the least recently read first; 5 skipped: gone, or read in the last 7 days."
    When I refresh everything, skipping what was read in the last 0 days, with my session
    Then the latest run ends succeeded within 60 seconds
    And the latest run's "counts.foundOnYouTube" is "5" within 5 seconds

  Scenario: A gone video isn't read again
    Given YouTube says of the video "alpha-02": "ERROR: [youtube] alpha-02___: Video unavailable. This video has been removed by the uploader"
    When I refresh everything with my session
    Then the latest run ends succeeded within 60 seconds
    And the stored YouTube state of the video "alpha-02" is "gone"
    When I refresh everything, skipping what was read in the last 0 days, with my session
    Then the latest run ends succeeded within 60 seconds
    And the latest run's notes mention "4 channels and videos to read, the least recently read first; 1 skipped"

  Scenario: YouTube refusing puts it on hold, and Resume now goes on from there
    Given YouTube says of the video "alpha-02": "ERROR: [youtube] alpha-02___: Sign in to confirm you’re not a bot"
    When I refresh everything with my session
    Then the latest run is onHold within 60 seconds
    Given YouTube has the videos "alpha-02"
    When YouTube no longer refuses the video "alpha-02"
    And I resume the latest run with my session
    Then the latest run ends succeeded within 60 seconds
    And the stored YouTube state of the video "alpha-02" is "on YouTube"

  Scenario Outline: The days are 0 to 365, and only for refreshAll
    When I send POST "<path>" with my session
    Then the response status is 400

    Examples:
      | path                                                    |
      | /api/admin/tasks/refreshAll/runs?days=-1                |
      | /api/admin/tasks/refreshAll/runs?days=366               |
      | /api/admin/tasks/youTubeSync/runs?days=7                |

  Scenario: On the command line, a run like the others
    When I run "tontuyau youtube refresh-all 0"
    Then the command exits with code 0
    And the output mentions "youtube refresh-all by cli  SUCCEEDED"
