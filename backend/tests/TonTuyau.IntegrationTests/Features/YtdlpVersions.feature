@youtube @api
Feature: Which yt-dlp runs
  The image has a yt-dlp (here 2026.08.19). By default TonTuyau runs the latest official release, checked on GitHub at
  startup and daily, and downloaded when newer; an admin can choose one official release, or one nightly build of the
  last 2 months, instead (GET/PUT /api/admin/youtube/ytdlp, GET /api/admin/youtube/ytdlp/versions). A version is
  downloaded from its GitHub release, checked against the checksums published with it, tried, and switched to between
  tasks only (contract v1.8, TonTuyau 1.5). GitHub is a fake here.

  Background:
    Given GitHub lists the official yt-dlp releases:
      | version    | days ago | linux |
      | 2026.09.30 | 15       | yes   |
      | 2026.08.19 | 57       | yes   |
      | 2022.06.01 | 1600     | no    |
    And GitHub lists the nightly yt-dlp builds:
      | version           | days ago |
      | 2026.10.10.233141 | 4        |
      | 2026.09.01.120000 | 44       |
      | 2026.07.01.120000 | 106      |

  Scenario: By default the latest official release is downloaded, checked and run
    When I log in as the admin
    Then yt-dlp runs "2026.09.30" within 30 seconds
    And "bundled" is "false"
    And "choice.kind" is "latest"
    And "error" is null
    And the tools folder holds yt-dlp "2026.09.30" only
    When I request "/api/admin/youtube" with my session
    Then "ytdlp" is "2026.09.30"

  Scenario: The versions offered: every official release with a Linux program, the nightly builds of 2 months
    When I log in as the admin
    And I request "/api/admin/youtube/ytdlp/versions" with my session
    Then the response status is 200
    And the yt-dlp versions offered are "2026.09.30, 2026.08.19" and the nightly builds "2026.10.10.233141, 2026.09.01.120000"

  Scenario: Choosing the image's version goes back to it, and removes the one downloaded
    When I log in as the admin
    Then yt-dlp runs "2026.09.30" within 30 seconds
    When I choose yt-dlp "official" "2026.08.19" with my session
    Then the response status is 202
    And "choice.kind" is "official"
    And "choice.version" is "2026.08.19"
    And "choice.chosenBy" is "admin"
    And yt-dlp runs "2026.08.19" within 30 seconds
    And "bundled" is "true"
    And the tools folder holds yt-dlp "" only

  Scenario: Choosing a nightly build
    When I log in as the admin
    And I choose yt-dlp "nightly" "2026.10.10.233141" with my session
    Then yt-dlp runs "2026.10.10.233141" within 30 seconds
    And the tools folder holds yt-dlp "2026.10.10.233141" only

  Scenario: A program that doesn't match its checksum is never run
    Given GitHub's checksum of yt-dlp "2026.09.30" is wrong
    When I log in as the admin
    Then yt-dlp's last check failed, mentioning "checksum", within 30 seconds
    And "running" is "2026.08.19"
    And "bundled" is "true"
    And the tools folder holds yt-dlp "" only

  Scenario Outline: Only a version offered can be chosen
    When I log in as the admin
    And I choose yt-dlp "<kind>" "<version>" with my session
    Then the answer is a problem 400 about "<field>"

    Examples:
      | kind    | version           | field   |
      | stable  |                   | kind    |
      | official| 2022.06.01        | version |
      | official|                   | version |
      | nightly | 2026.07.01.120000 | version |
      | nightly | 2026.09.30        | version |

  Scenario: An API key can't see or choose yt-dlp
    Given the API key "Firefox" exists
    When I send GET "/api/admin/youtube/ytdlp" with the API key "Firefox"
    Then the answer is a problem 403
    When I send PUT "/api/admin/youtube/ytdlp" with the API key "Firefox"
    Then the answer is a problem 403

  @elasticsearch @sync
  Scenario: A version chosen while a task runs waits for it to end
    Given TubeArchivist has the channel "alpha" with 5 videos
    And the backend is ready
    And a first import has run
    And TubeArchivist downloads 15 new videos in "alpha"
    And TubeArchivist holds its answer to page 2 until told
    When I log in as the admin
    Then yt-dlp runs "2026.09.30" within 30 seconds
    When I send POST "/api/admin/tasks/import/runs" with my session
    And I choose yt-dlp "nightly" "2026.10.10.233141" with my session
    Then yt-dlp "2026.10.10.233141" waits for the task going on within 30 seconds
    And "running" is "2026.09.30"
    When TubeArchivist answers
    Then the latest run ends succeeded within 30 seconds
    And yt-dlp runs "2026.10.10.233141" within 60 seconds
