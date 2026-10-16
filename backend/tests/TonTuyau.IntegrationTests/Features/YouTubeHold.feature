@elasticsearch @sync @api @youtube
Feature: A refresh on hold when YouTube refuses
  When YouTube refuses TonTuyau (a bot check, too many requests: 1.5), a refresh stops asking, frees the lock, and
  waits: it's on hold, with what it has left to read. It resumes by itself after the YouTube page's cool-down (60 min by
  default), or at once with Resume now: the same run goes on where it stopped. It can be canceled while on hold. What
  it read before stays; the item refused isn't recorded. The clock is frozen here, and there's no pause between videos.

  Background:
    Given the clock shows "2026-10-16T08:00:00Z"
    And TubeArchivist has the channel "alpha" with 3 videos
    And the backend is ready
    And a first sync has run
    And I logged in as the admin
    And there's no pause between videos
    And the clock moves on 1 minutes

  Scenario: A bot check puts the refresh on hold, and frees the lock
    Given YouTube says of the video "alpha-02": "ERROR: [youtube] alpha-02___: Sign in to confirm you’re not a bot"
    When I refresh the video "alpha-02" with my session
    Then the latest run is onHold within 20 seconds
    And the stored YouTube state of the video "alpha-02" is "not checked"
    When I request the latest run's "" with my session
    Then "state" is "onHold"
    And "resumeAt" is "2026-10-16T09:01:00Z"
    And "left" is "1"
    And "cancellable" is "true"
    And "counts.videosRead" is "0"
    And "notes[0]" mentions "YouTube refused (Sign in to confirm you’re not a bot)"
    When I request "/api/admin/tasks" with my session
    Then "lock" is null
    And "onHold" has 1 items
    And "onHold[0].left" is "1"

  Scenario: Resume now: the same run goes on where it stopped
    Given YouTube has the channel "alpha": the name "Alpha officiel", 5000 subscribers
    And YouTube says of the video "alpha-01": "ERROR: [youtube] alpha-01___: HTTP Error 429: Too Many Requests"
    And YouTube says of the video "alpha-02": "ERROR: [youtube] alpha-02___: HTTP Error 429: Too Many Requests"
    And YouTube says of the video "alpha-03": "ERROR: [youtube] alpha-03___: HTTP Error 429: Too Many Requests"
    When I refresh the channel "alpha" and its videos with my session
    Then the latest run is onHold within 20 seconds
    When I request the latest run's "" with my session
    Then "left" is "3"
    And "notes[0]" is "The channel: on YouTube."
    Given YouTube has the video "alpha-01": the title "Un", 1 views
    And YouTube has the video "alpha-02": the title "Deux", 2 views
    And YouTube has the video "alpha-03": the title "Trois", 3 views
    When I resume the latest run with my session
    Then the response status is 202
    And the latest run ends succeeded within 20 seconds
    When I request the latest run's "" with my session
    Then "counts.videosRead" is "3"
    And "counts.foundOnYouTube" is "3"
    And "left" is "0"
    And "resumeAt" is null
    And "notes[2]" is "Resumed by admin."
    When I request "/api/admin/runs?task=refreshChannelVideos" with my session
    Then "total" is "1"
    When I resume the latest run with my session
    Then the response status is 409

  Scenario: Canceled while on hold, it won't resume
    Given YouTube says of the video "alpha-02": "ERROR: [youtube] alpha-02___: Sign in to confirm you’re not a bot"
    When I refresh the video "alpha-02" with my session
    Then the latest run is onHold within 20 seconds
    When I cancel the latest run with my session
    Then the response status is 202
    And "state" is "canceled"
    And "resumeAt" is null
    When I request "/api/admin/tasks" with my session
    Then "onHold" has 0 items
    When the clock moves on 61 minutes
    And I resume the latest run with my session
    Then the response status is 409

  Scenario: It resumes by itself once the cool-down is over
    Given YouTube says of the video "alpha-02": "ERROR: [youtube] alpha-02___: Sign in to confirm you’re not a bot"
    When I refresh the video "alpha-02" with my session
    Then the latest run is onHold within 20 seconds
    Given YouTube has the video "alpha-02": the title "Enfin", 7 views
    When the clock moves on 59 minutes
    Then the latest run is onHold within 2 seconds
    When the clock moves on 2 minutes
    Then the latest run is succeeded within 20 seconds
    And the stored YouTube state of the video "alpha-02" is "on YouTube"
    When I request the latest run's "" with my session
    Then "notes[1]" is "Resumed by itself, after the cool-down."
