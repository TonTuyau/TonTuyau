@elasticsearch @sync @api @youtube
Feature: The run queue
  One run at a time (the run lock, across processes). Since 2.0 a task asked for while another run goes on isn't
  refused: it's recorded at once, queued, and starts when its turn comes, first come first served. The Tasks page lists
  the queue, and a queued run can be canceled. While a run is on hold (YouTube refused it, or an admin held it) the
  queue waits with it, and goes on once it resumes or is canceled. yt-dlp is a fake here.

  Background:
    Given TubeArchivist has the channel "alpha" with 5 videos
    And the backend is ready
    And a first import has run
    And I logged in as the admin
    And there's no pause between videos

  Scenario: Asked for while a run goes on, two refreshes wait their turn, then run in the order they came
    Given TubeArchivist downloads 15 new videos in "alpha"
    And TubeArchivist holds its answer to page 2 until told
    And YouTube has the video "alpha-01": the title "Un", 1 views
    And YouTube has the video "alpha-02": the title "Deux", 2 views
    When I send POST "/api/admin/tasks/import/runs" with my session
    Then "state" is "running"
    When I refresh the video "alpha-01" with my session
    Then the response status is 202
    And "state" is "queued"
    And "position" is "1"
    When I refresh the video "alpha-02" with my session
    Then "position" is "2"
    When I request "/api/admin/tasks" with my session
    Then "queue" has 2 items
    And "queue[0].target" is "alpha-01___"
    And "queue[1].target" is "alpha-02___"
    When TubeArchivist answers
    Then the latest run ends succeeded within 40 seconds
    When I request "/api/admin/runs?size=3" with my session
    Then "items[0].target" is "alpha-02___"
    And "items[0].state" is "succeeded"
    And "items[1].target" is "alpha-01___"
    And "items[1].state" is "succeeded"
    And "items[2].task" is "import"
    When I request "/api/admin/tasks" with my session
    Then "queue" has 0 items
    And the stored YouTube state of the video "alpha-01" is "on YouTube"

  Scenario: A run on hold holds the queue; once it's canceled, the queue goes on
    Given YouTube says of the video "alpha-02": "ERROR: [youtube] alpha-02___: Sign in to confirm you’re not a bot"
    And YouTube has the video "alpha-01": the title "Un", 1 views
    When I refresh the video "alpha-02" with my session
    Then the latest run is onHold within 20 seconds
    When I refresh the video "alpha-01" with my session
    Then "state" is "queued"
    When 6 seconds pass
    And I request the latest run's "" with my session
    Then "state" is "queued"
    And "position" is "1"
    When I cancel the run on hold with my session
    Then the response status is 202
    And the latest run ends succeeded within 20 seconds
    And the stored YouTube state of the video "alpha-01" is "on YouTube"
