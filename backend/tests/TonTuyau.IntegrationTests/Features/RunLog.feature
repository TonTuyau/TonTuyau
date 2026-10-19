@elasticsearch @sync
Feature: The runs' logs
  Every line a run logs (its start, its progress lines, warnings, errors, its end) is in the backend's log as always,
  and since 1.4 in that run's log in PostgreSQL too, for the Tasks page to show: TonTuyau's own lines from Information
  up, the warnings and errors of everything else, nothing logged outside the run.

  Background:
    Given TubeArchivist has the channel "alpha" with 5 videos
    And the backend is ready

  Scenario: An import's log: its start, its progress, its end, in order
    Given TubeArchivist downloads 25 new videos in "alpha"
    When an import runs
    Then the run succeeded
    And the last run's log has, in order, lines with:
      | level       | text                   |
      | information | started: Import by Cli |
      | information | videos                 |
      | information | finished: Succeeded    |
    And every line of the last run's log is that run's
    And no line of the last run's log is about an HTTP request

  Scenario: A failed import's log says why
    Given a first import has run
    And TubeArchivist answers 503 to everything
    When an import runs
    Then the run failed
    And the last run's log has, in order, lines with:
      | level       | text                   |
      | information | started: Import by Cli |
      | warning     | failed:                |
      | information | finished: Failed       |
