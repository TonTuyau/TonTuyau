@elasticsearch @sync @api
Feature: The Tasks page's API
  GET /api/admin/tasks, /api/admin/runs, /api/admin/runs/{id} and /api/admin/runs/{id}/log (contract v1.7, TonTuyau
  1.4; v2.0: the TubeArchivist import is the task): the task (how it last ran, or why it can't start), the run history
  a page at a time, a run, its log. Logged-in accounts only.

  Background:
    Given TubeArchivist has the channel "alpha" with 5 videos
    And the backend is ready

  Scenario Outline: Nobody logged in gets a 401
    When I request "<path>" with my session
    Then the response status is 401

    Examples:
      | path                                             |
      | /api/admin/tasks                                 |
      | /api/admin/runs                                  |
      | /api/admin/runs/run-20260101T000000Z-abcdef      |
      | /api/admin/runs/run-20260101T000000Z-abcdef/log  |

  Scenario: The import, before any run
    When I log in as the admin
    And I request "/api/admin/tasks" with my session
    Then the response status is 200
    And the header "Cache-Control" is "no-store, no-cache"
    And the answer has exactly the fields "lock, runningHere, tasks, onHold, queue"
    And "lock" is null
    And "runningHere" is null
    And "tasks" has 4 items
    And "tasks[0].task" is "import"
    And "tasks[0].lastRun" is null
    And "tasks[0].unavailable" is null
    And "tasks[0].next" is null
    And "tasks[1].task" is "youTubeSync"
    And "tasks[2].task" is "youTubeFullSync"
    And "tasks[2].next" is null
    And "tasks[3].task" is "refreshAll"
    And "tasks[3].next" is null
    And "queue" has 0 items

  Scenario: Without TubeArchivist's settings, the import says why it can't start
    Given TubeArchivist isn't set up
    When I log in as the admin
    And I request "/api/admin/tasks" with my session
    Then "tasks[0].task" is "import"
    And "tasks[0].unavailable" mentions "TubeArchivist isn't set up"

  Scenario: The history, latest first, a page at a time, or one task's
    Given a first import has run
    When an index rebuild runs
    And an import runs
    And I log in as the admin
    And I request "/api/admin/runs?size=2" with my session
    Then the response status is 200
    And the answer has exactly the fields "items, nextCursor, total"
    And "total" is "3"
    And "items" has 2 items
    And "items[0].task" is "import"
    And "items[1].task" is "indexRebuild"
    And the next cursor is the id of the page's last run
    When I request the next page of runs with my session
    Then "items" has 1 item
    And "nextCursor" is null
    When I request "/api/admin/runs?task=indexRebuild" with my session
    Then "total" is "1"
    And "items[0].task" is "indexRebuild"

  Scenario Outline: A request the API doesn't take is a 400
    When I log in as the admin
    And I request "<path>" with my session
    Then the response status is 400

    Examples:
      | path                                                       |
      | /api/admin/runs?task=full                                  |
      | /api/admin/runs?before=nope                                |
      | /api/admin/runs?size=0                                     |
      | /api/admin/runs?size=101                                   |
      | /api/admin/runs/run-20260101T000000Z-abcdef/log?after=-1   |
      | /api/admin/runs/run-20260101T000000Z-abcdef/log?size=2001  |

  Scenario: A run, and the ones that don't exist
    Given a first import has run
    When I log in as the admin
    And I request the latest run's "" with my session
    Then the response status is 200
    And the answer has exactly the fields "id, task, trigger, startedBy, state, startedAt, queuedAt, position, finishedAt, error, counts, target, tabs, resumeAt, left, indexRebuild, notes, cancellable"
    And "id" is the latest run's id
    And "state" is "succeeded"
    And "trigger" is "cli"
    And "cancellable" is "false"
    And "counts.created" is "5"
    When I request "/api/admin/runs/run-20260101T000000Z-abcdef" with my session
    Then the response status is 404
    When I request "/api/admin/runs/nope" with my session
    Then the response status is 404

  Scenario: A run's log, oldest line first, complete once the run is over
    Given a first import has run
    When I log in as the admin
    And I request the latest run's "/log" with my session
    Then the response status is 200
    And the answer has exactly the fields "lines, complete"
    # Its lines are written within a second or so: like the page, ask again until it's complete.
    And the log is complete within 15 seconds
    And "lines[0].level" is "information"
    And "lines[0].message" mentions "started"
    When I request the lines after the last one with my session
    Then "lines" has 0 items
    And "complete" is "true"

  Scenario: A run from before 1.4 has no log, and nothing more will come
    Given a run from before 1.4, "run-20260101T070000Z-abcdef"
    When I log in as the admin
    And I request "/api/admin/runs/run-20260101T070000Z-abcdef/log" with my session
    Then "lines" has 0 items
    And "complete" is "true"
