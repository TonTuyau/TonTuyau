@elasticsearch @sync
Feature: The backend's runs
  The backend starts its runs in the background (the scheduled ones; in 1.4 the Tasks page's and the command line's
  too): one at a time, each known by its id while it goes on, cancellable by that id, canceled when the backend stops.

  Background:
    Given TubeArchivist has the channel "alpha" with 5 videos
    And the backend is ready
    And a first sync has run
    And TubeArchivist downloads 15 new videos in "alpha"

  Scenario: A run started in the backend goes on by itself, known by its id, then ends
    Given TubeArchivist holds its answer to page 2 until told
    When an incremental sync starts in the backend
    Then the backend says the started run is going on
    When TubeArchivist answers
    And the started run ends
    Then the started run succeeded
    And no run is going on in the backend

  Scenario: Canceled by its id: it ends canceled, and its log says who asked
    Given the run going on in the backend is canceled by "admin" right before TubeArchivist answers page 2
    When an incremental sync starts in the backend
    And the started run ends
    Then the started run was canceled
    And no run is going on in the backend
    And the last run's log has, in order, lines with:
      | level       | text                |
      | warning     | canceled by admin   |
      | information | finished: Canceled  |

  Scenario: Canceling a run that isn't going on in this backend does nothing
    When the run "run-20260101T000000Z-abcdef" is canceled in the backend
    Then the backend says that run isn't going on here

  Scenario: Another run holds the lock: the start is refused
    Given another process holds the sync lock
    When an incremental sync starts in the backend
    Then it is refused because "already running"

  # The scenario's end stops the backend once more: stopping again is harmless (a CI run once failed on it).
  Scenario: Once the runs are stopped, none starts any more
    When the backend's runs are stopped
    And an incremental sync starts in the backend
    Then it is refused because "stopping"

  Scenario: The backend stopping cancels the run going on, and its log is written to the end
    Given the backend stops right before TubeArchivist answers page 2
    When an incremental sync starts in the backend
    And the started run ends
    Then the started run was canceled
    When the backend restarts and is ready
    Then the stored started run is canceled
    And the last run's log has, in order, lines with:
      | level       | text                              |
      | information | started: Incremental by Scheduler |
      | information | finished: Canceled                |
