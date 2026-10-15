@elasticsearch @sync @api
Feature: The Tasks page's API
  GET /api/admin/tasks, /api/admin/runs, /api/admin/runs/{id} and /api/admin/runs/{id}/log (contract v1.7, TonTuyau
  1.4): the three tasks (when they run next, how they last ran, the deletion list waiting), the run history a page at
  a time, a run, its log. Logged-in accounts only.

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

  Scenario: The three tasks before any run
    When I log in as the admin
    And I request "/api/admin/tasks" with my session
    Then the response status is 200
    And the header "Cache-Control" is "no-store, no-cache"
    And the answer has exactly the fields "lock, runningHere, tasks"
    And "lock" is null
    And "runningHere" is null
    And "tasks" has 3 items
    And "tasks[0].task" is "incremental"
    And "tasks[0].schedule" is "0 7-23 * * *"
    And "tasks[0].next" is null
    And "tasks[0].lastRun" is null
    And "tasks[1].task" is "full"
    And "tasks[1].schedule" is "0 1 * * 6"
    And "tasks[2].task" is "applyDeletions"
    And "tasks[2].schedule" is null
    And "tasks[2].waiting" is null

  Scenario: When the scheduler runs them next, in the sync's time zone
    Given the clock shows "2026-10-13T12:20:00Z"
    And the scheduler is on with the default schedule
    When I log in as the admin
    And I request "/api/admin/tasks" with my session
    # Paris is UTC+2 until the 25th: the next hour is 15:00 there, the next Saturday 01:00 is Friday 23:00 UTC.
    Then "tasks[0].next" is "2026-10-13T13:00:00Z"
    And "tasks[1].next" is "2026-10-16T23:00:00Z"
    And "tasks[2].next" is null

  Scenario: The deletion list of a full run over the limit waits, until it's applied
    Given the deletion limit is 1
    And a first sync has run
    And TubeArchivist deletes 2 videos of "alpha"
    And a full sync has run
    When I log in as the admin
    And I request "/api/admin/tasks" with my session
    Then "tasks[1].lastRun.state" is "succeeded"
    And "tasks[1].lastRun.deletionsBlocked" is "true"
    And "tasks[2].waiting.count" is "2"
    And "tasks[2].waiting.runId" is the latest full run's id
    And "tasks[2].lastRun" is null
    When the deletions of that run are applied
    And I request "/api/admin/tasks" with my session
    Then "tasks[2].waiting" is null
    And "tasks[2].lastRun.state" is "succeeded"
    And "tasks[2].lastRun.task" is "applyDeletions"
    And "tasks[2].lastRun.appliesTo" is the latest full run's id
    And "tasks[2].lastRun.counts.deleted" is "2"

  Scenario: The history, latest first, a page at a time, or one task's
    Given a first sync has run
    When an incremental sync runs
    And a full sync runs
    And I log in as the admin
    And I request "/api/admin/runs?size=2" with my session
    Then the response status is 200
    And the answer has exactly the fields "items, nextCursor, total"
    And "total" is "3"
    And "items" has 2 items
    And "items[0].task" is "full"
    And "items[1].task" is "incremental"
    And the next cursor is the id of the page's last run
    When I request the next page of runs with my session
    Then "items" has 1 item
    And "nextCursor" is null
    When I request "/api/admin/runs?task=incremental" with my session
    Then "total" is "1"
    And "items[0].task" is "incremental"

  Scenario Outline: A request the API doesn't take is a 400
    When I log in as the admin
    And I request "<path>" with my session
    Then the response status is 400

    Examples:
      | path                                                       |
      | /api/admin/runs?task=rebuild                               |
      | /api/admin/runs?before=nope                                |
      | /api/admin/runs?size=0                                     |
      | /api/admin/runs?size=101                                   |
      | /api/admin/runs/run-20260101T000000Z-abcdef/log?after=-1   |
      | /api/admin/runs/run-20260101T000000Z-abcdef/log?size=2001  |

  Scenario: A run, and the ones that don't exist
    Given a first sync has run
    When I log in as the admin
    And I request the latest run's "" with my session
    Then the response status is 200
    And the answer has exactly the fields "id, task, trigger, startedBy, state, startedAt, finishedAt, error, counts, deletionsPlanned, deletionsBlocked, appliesTo, target, indexRebuild, notes, cancellable"
    And "id" is the latest run's id
    And "state" is "succeeded"
    And "trigger" is "scheduler"
    And "cancellable" is "false"
    And "counts.created" is "5"
    When I request "/api/admin/runs/run-20260101T000000Z-abcdef" with my session
    Then the response status is 404
    When I request "/api/admin/runs/nope" with my session
    Then the response status is 404

  Scenario: A run's log, oldest line first, complete once the run is over
    Given a first sync has run
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
