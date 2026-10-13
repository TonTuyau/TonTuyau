@elasticsearch @sync @api
Feature: Starting a task, canceling a run, from the Tasks page
  POST /api/admin/tasks/{task}/runs starts a task now, in the backend's background (202, the run just started);
  POST /api/admin/runs/{id}/cancel asks the run going on to stop (202; it ends canceled soon after). One run at a
  time: 409 while another goes on (contract v1.7, TonTuyau 1.4). Logged-in accounts only.

  Background:
    Given TubeArchivist has the channel "alpha" with 5 videos
    And the backend is ready

  Scenario Outline: Nobody logged in gets a 401
    When I send POST "<path>" with my session
    Then the response status is 401

    Examples:
      | path                                                   |
      | /api/admin/tasks/incremental/runs                      |
      | /api/admin/runs/run-20260101T000000Z-abcdef/cancel     |

  Scenario: A task started from the page runs in the background, started by the account
    Given a first sync has run
    And TubeArchivist downloads 15 new videos in "alpha"
    And TubeArchivist holds its answer to page 2 until told
    When I log in as the admin
    And I send POST "/api/admin/tasks/incremental/runs" with my session
    Then the response status is 202
    And "task" is "incremental"
    And "state" is "running"
    And "trigger" is "admin"
    And "startedBy" is "admin"
    And "cancellable" is "true"
    And the Location header is the run's address
    When TubeArchivist answers
    Then the latest run ends succeeded within 30 seconds

  Scenario: Another run going on: the start is refused
    Given another process holds the sync lock
    When I log in as the admin
    And I send POST "/api/admin/tasks/full/runs" with my session
    Then the response status is 409
    And "detail" mentions "already running"

  Scenario: Not a task
    When I log in as the admin
    And I send POST "/api/admin/tasks/rebuild/runs" with my session
    Then the response status is 400

  Scenario: Applying deletions without a list waiting is refused
    Given a first sync has run
    When I log in as the admin
    And I send POST "/api/admin/tasks/applyDeletions/runs" with my session
    Then the response status is 409
    And "detail" mentions "No deletion list"

  Scenario: Applying deletions applies the list that waits
    Given the deletion limit is 1
    And a first sync has run
    And TubeArchivist deletes 2 videos of "alpha"
    And a full sync has run
    When I log in as the admin
    And I send POST "/api/admin/tasks/applyDeletions/runs" with my session
    Then the response status is 202
    And "task" is "applyDeletions"
    And "appliesTo" is the latest full run's id
    And the latest run ends succeeded within 30 seconds
    And TonTuyau has 3 videos

  Scenario: Canceling the run going on: it ends canceled, and its log says who asked
    Given a first sync has run
    And TubeArchivist downloads 15 new videos in "alpha"
    And TubeArchivist holds its answer to page 2 until told
    When I log in as the admin
    And I send POST "/api/admin/tasks/incremental/runs" with my session
    And I cancel the latest run with my session
    Then the response status is 202
    And "cancellable" is "true"
    When TubeArchivist answers
    Then the latest run ends canceled within 30 seconds
    And the last run's log has, in order, lines with:
      | level       | text                         |
      | information | started: Incremental by Admin |
      | warning     | canceled by admin            |
      | information | finished: Canceled           |

  Scenario: A run that's over can't be canceled
    Given a first sync has run
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
