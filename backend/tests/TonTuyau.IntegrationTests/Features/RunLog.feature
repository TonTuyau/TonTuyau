@elasticsearch @sync
Feature: The runs' logs
  Every line a run logs (its start, its progress lines, warnings, errors, its end) is in the backend's log as always,
  and since 1.4 in that run's log in PostgreSQL too, for the Tasks page to show: TonTuyau's own lines from Information
  up, the warnings and errors of everything else, nothing logged outside the run.

  Background:
    Given TubeArchivist has the channel "alpha" with 5 videos
    And the backend is ready

  Scenario: A sync's log: its start, its progress, its end, in order
    Given a first sync has run
    And TubeArchivist downloads 25 new videos in "alpha"
    When an incremental sync runs
    Then the run succeeded
    And the last run's log has, in order, lines with:
      | level       | text                              |
      | information | started: Incremental by Scheduler |
      | information | new videos                        |
      | information | finished: Succeeded               |
    And every line of the last run's log is that run's
    And no line of the last run's log is about an HTTP request

  Scenario: A failed sync's log says why
    Given a first sync has run
    And TubeArchivist answers 503 to everything
    When an incremental sync runs
    Then the run failed
    And the last run's log has, in order, lines with:
      | level       | text                              |
      | information | started: Incremental by Scheduler |
      | warning     | failed:                           |
      | information | finished: Failed                  |

  Scenario: Applying deletions has a log of its own
    Given the deletion limit is 1
    And a first sync has run
    And TubeArchivist deletes 2 videos of "alpha"
    And a full sync has run
    When the deletions of that run are applied
    Then the last run's log has, in order, lines with:
      | level       | text                                  |
      | information | started: ApplyDeletions by Cli        |
      | information | deletions                             |
      | information | finished: Succeeded                   |
