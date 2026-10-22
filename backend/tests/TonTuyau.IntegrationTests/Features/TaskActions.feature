@elasticsearch @sync @api
Feature: Starting a task, canceling a run, from the Tasks page
  POST /api/admin/tasks/{task}/runs starts a task now, in the backend's background (202, the run just started);
  POST /api/admin/runs/{id}/cancel asks the run going on to stop (202; it ends canceled soon after). One run at a
  time; one asked for while another goes on waits its turn in the run queue (2.0). Logged-in accounts only.

  Background:
    Given TubeArchivist has the channel "alpha" with 5 videos
    And the backend is ready

  Scenario Outline: Nobody logged in gets a 401
    When I send POST "<path>" with my session
    Then the response status is 401

    Examples:
      | path                                                   |
      | /api/admin/tasks/import/runs                      |
      | /api/admin/runs/run-20260101T000000Z-abcdef/cancel     |

  Scenario: A task started from the page runs in the background, started by the account
    Given a first import has run
    And TubeArchivist downloads 15 new videos in "alpha"
    And TubeArchivist holds its answer to page 2 until told
    When I log in as the admin
    And I send POST "/api/admin/tasks/import/runs" with my session
    Then the response status is 202
    And "task" is "import"
    And "state" is "running"
    And "trigger" is "admin"
    And "startedBy" is "admin"
    And "cancellable" is "true"
    And the Location header is the run's address
    When TubeArchivist answers
    Then the latest run ends succeeded within 30 seconds

  Scenario: Another run going on: the import waits in the queue, and can be taken out of it
    Given another process holds the run lock
    When I log in as the admin
    And I send POST "/api/admin/tasks/import/runs" with my session
    Then the response status is 202
    And "state" is "queued"
    And "position" is "1"
    And "cancellable" is "true"
    When I request "/api/admin/tasks" with my session
    Then "queue" has 1 item
    And "queue[0].task" is "import"
    When I cancel the latest run with my session
    Then the response status is 202
    And "state" is "canceled"
    And "notes[0]" mentions "while it waited in the queue"
    When I request "/api/admin/tasks" with my session
    Then "queue" has 0 items

  Scenario: Not a task
    When I log in as the admin
    And I send POST "/api/admin/tasks/indexRebuild/runs" with my session
    Then the response status is 400

  Scenario: Without TubeArchivist's settings, the import is refused
    Given TubeArchivist isn't set up
    When I log in as the admin
    And I send POST "/api/admin/tasks/import/runs" with my session
    Then the response status is 409
    And "detail" mentions "TubeArchivist isn't set up"

  Scenario: Canceling the run going on: it ends canceled, and its log says who asked
    Given a first import has run
    And TubeArchivist downloads 15 new videos in "alpha"
    And TubeArchivist holds its answer to page 2 until told
    When I log in as the admin
    And I send POST "/api/admin/tasks/import/runs" with my session
    And I cancel the latest run with my session
    Then the response status is 202
    And "cancellable" is "true"
    When TubeArchivist answers
    Then the latest run ends canceled within 30 seconds
    And the last run's log has, in order, lines with:
      | level       | text                   |
      | information | started: Import by Admin |
      | warning     | canceled by admin      |
      | information | finished: Canceled     |

  Scenario: A run that's over can't be canceled
    Given a first import has run
    When I log in as the admin
    And I cancel the latest run with my session
    Then the response status is 409
    And "detail" mentions "over"

  Scenario: A run going on in another process can't be canceled from here
    Given a run going on in another process, "run-20260101T070000Z-abcdef"
    When I log in as the admin
    And I send POST "/api/admin/runs/run-20260101T070000Z-abcdef/cancel" with my session
    Then the response status is 409
    And "detail" mentions "another process"

  Scenario: Canceling a run that doesn't exist
    When I log in as the admin
    And I send POST "/api/admin/runs/run-20260101T000000Z-abcdef/cancel" with my session
    Then the response status is 404
