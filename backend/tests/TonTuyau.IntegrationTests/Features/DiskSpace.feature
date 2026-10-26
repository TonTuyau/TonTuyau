@elasticsearch @sync @api @youtube
Feature: The downloads wait for disk space
  2.2.0: under the YouTube page's threshold of free space on the videos folder's disk (50 GB by default; 0: never),
  the downloads don't run, as its switch says by default: the YouTube syncs, "Download a video" (LaPompe's too) and
  "Try again" can't start, and say why on the Tasks page. A sync going on looks before each download, and goes on hold
  once under it: it tries again by itself after the cool-down, or with Resume now. The refreshes don't download: they
  still run. Switched off, the downloads go on, and the dashboard only warns. The disk is a fake here, filled by the
  downloads; yt-dlp is a fake too.

  Background:
    Given TubeArchivist has the channel "alpha" with 2 videos
    And TubeArchivist is subscribed to the channel "alpha"
    And the backend is ready
    And a first import has run
    And I logged in as the admin
    And the API key "Firefox" exists
    And there's no pause between videos

  Scenario: Under the threshold, the downloads can't start, and say why; the refreshes still run
    Given the videos folder's disk has 40 GB free of 1000 GB
    When I start the YouTube sync with my session
    Then the response status is 409
    And "detail" is "Under 50 GB free on the videos folder's disk (40 GB): the downloads wait until space is freed (the YouTube page sets it)."
    When I start the full YouTube sync with my session
    Then the response status is 409
    When I download "omega-01___" with my session
    Then the response status is 409
    And "detail" mentions "Under 50 GB free"
    When I send POST "/api/extension/videos/omega-01___" with the API key "Firefox"
    Then the response status is 409
    When I try the failed downloads of the channel "alpha" again with my session
    Then the response status is 409
    And "detail" mentions "Under 50 GB free"
    When I run "tontuyau youtube download omega-01___"
    Then the command exits with code 4
    And the output mentions "Under 50 GB free"
    When I request "/api/admin/tasks" with my session
    Then "tasks[1].task" is "youTubeSync"
    And "tasks[1].unavailable" mentions "Under 50 GB free"
    And "tasks[2].unavailable" mentions "Under 50 GB free"
    And "tasks[3].unavailable" is null
    Given YouTube has the video "alpha-01": the title "Toujours là", 12 views
    When I refresh the video "alpha-01" with my session
    Then the response status is 202
    And the latest run ends succeeded within 20 seconds

  Scenario: A sync going on holds once the downloads take the disk under the threshold, and goes on once space is freed
    Given the videos folder's disk has 50 GB and 3000 bytes free
    And YouTube has the videos "alpha-05, alpha-04"
    And YouTube lists "alpha-05, alpha-04, alpha-02" on the videos tab of the channel "alpha"
    When I start the YouTube sync with my session
    Then the response status is 202
    And the latest run is onHold within 60 seconds
    And the media folder has the file of the video "alpha-05" with 4096 bytes
    And TonTuyau doesn't have the video "alpha-04"
    And the latest run's "left" is "1" within 5 seconds
    And the latest run's notes mention "1 left to read: Under 50 GB free on the videos folder's disk (49.9 GB)"
    # 2.3.0: held for disk space, it lets the queue go on (a refresh downloads nothing).
    When I request "/api/admin/tasks" with my session
    Then "onHold[0].heldFor" is "diskSpace"
    And "queueWaitsFor" is null
    Given YouTube has the video "alpha-01": the title "Un", 1 views
    When I refresh the video "alpha-01" with my session
    Then the run of the video "alpha-01" is succeeded within 20 seconds
    Given the videos folder's disk has 900 GB free of 1000 GB
    When I resume the run on hold with my session
    Then the latest run of "youTubeSync" is succeeded within 60 seconds
    And the media folder has the file of the video "alpha-04" with 4096 bytes

  Scenario: Switched off, or at a threshold of 0, the downloads run under it
    Given the videos folder's disk has 40 GB free of 1000 GB
    When I send the YouTube settings '{"comments": "off", "holdBelowMinFree": false}' with my session
    Then the response status is 200
    And "minFreeGb" is "50"
    And "holdBelowMinFree" is "false"
    When I request "/api/admin/tasks" with my session
    Then "tasks[1].unavailable" is null
    Given YouTube has the videos "alpha-05"
    And YouTube lists "alpha-05, alpha-02" on the videos tab of the channel "alpha"
    When I start the YouTube sync with my session
    Then the response status is 202
    And the latest run ends succeeded within 60 seconds
    And the media folder has the file of the video "alpha-05" with 4096 bytes
    When I send the YouTube settings '{"comments": "off", "holdBelowMinFree": true, "minFreeGb": 0}' with my session
    Then "minFreeGb" is "0"
    And "holdBelowMinFree" is "true"
    When I request "/api/admin/tasks" with my session
    Then "tasks[1].unavailable" is null

  Scenario: A disk that doesn't say its free space never holds the downloads
    Given the videos folder's disk doesn't say its free space
    When I request "/api/admin/tasks" with my session
    Then "tasks[1].unavailable" is null

  Scenario Outline: A threshold out of range is refused
    When I send the YouTube settings '{"comments": "off", "minFreeGb": <value>}' with my session
    Then the answer is a problem 400 about "minFreeGb"

    Examples:
      | value   |
      | -1      |
      | 1000001 |
