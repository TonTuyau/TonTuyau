@elasticsearch @sync
Feature: Command line
  `tontuyau` inside the backend container runs one sync command and exits. Since 1.4 it asks the backend to (through
  its Unix socket; here its in-memory server) and prints the run's log as it comes, then the report.
  Exit codes: 0 done, 1 the run failed or was canceled, 2 usage error, 3 another sync is running, 4 refused.

  Background:
    Given TubeArchivist has the channel "alpha" with 5 videos
    And the backend is ready

  Scenario: A full sync from the command line
    When I run "tontuyau sync full"
    Then the command exits with code 0
    And the output mentions "full by cli  SUCCEEDED"
    And the output mentions "  read      5 videos / 1 channels"
    And the output mentions "  store     new 5, changed 0, unchanged 0"
    And the output mentions "  deletions 0 deleted, 0 kept (still in TubeArchivist)"
    And TonTuyau has 5 videos and 1 channel

  # One plain line per update, terminal or not (QA16-1).
  Scenario: A sync from the command line prints its progress, then the report
    Given the media share has every video's file except "alpha-03"
    When I run "tontuyau sync full"
    Then the command exits with code 0
    And the progress shows the phases "channels, videos, media check, deletions" before the report
    And the output mentions "] channels      1 / 1 read"
    And the output mentions "] videos        1 / 1 channels · 5 videos"
    And the output mentions "] media check   5 files checked · 1 missing"
    And the output mentions "] deletions     nothing to delete"
    And the output mentions "full by cli  SUCCEEDED"

  Scenario: Status shows the watermark, the lock, the indices and the last runs
    Given a first sync has run
    When I run "tontuyau sync status"
    Then the command exits with code 0
    And the output mentions "Lock           : free"
    And the output mentions "Index videos"
    And the output mentions "SUCCEEDED"

  Scenario: A note spanning several lines is printed with its lines under the bullet's text
    Given the deletion limit is 1
    And a first sync has run
    And TubeArchivist deletes 2 videos of "alpha"
    When I run "tontuyau sync full"
    Then the output mentions "  - 2 deletions planned > limit 1: nothing deleted."
    And the output mentions "    Check "
    And the output mentions ", then run: tontuyau sync apply-deletions run-"

  Scenario: Applying a blocked list from the command line reports a run of its own
    Given the deletion limit is 1
    And a first sync has run
    And TubeArchivist deletes 2 videos of "alpha"
    And a full sync has run
    When I run "tontuyau sync apply-deletions" for the latest full run
    Then the command exits with code 0
    And the output mentions "apply-deletions by cli  SUCCEEDED"
    And the output mentions "  applies   the list of run-"
    And the output mentions ": 2 of 2 deleted"
    And the output mentions "  deletions 2 deleted, 0 kept (still in TubeArchivist)"

  # 1.4: the command line asks the backend to run it, and prints the run's log as it comes.
  Scenario: A run from the command line is the backend's: in its log, the Tasks page sees it
    When I run "tontuyau sync full"
    Then the command exits with code 0
    And the output mentions "started: Full by Cli."
    And the output mentions "finished: Succeeded."
    And the last run's log has, in order, lines with:
      | level       | text                       |
      | information | started: Full by Cli       |
      | information | finished: Succeeded        |

  Scenario: Ctrl-C cancels the run in the backend, and the report says so
    Given a first sync has run
    And TubeArchivist downloads 15 new videos in "alpha"
    And Ctrl-C is pressed right before TubeArchivist answers page 2
    When I run "tontuyau sync incremental"
    Then the command exits with code 1
    And the output mentions "Ctrl-C: canceling the run"
    And the output mentions "warning: Sync run-"
    And the output mentions ": canceled by the command line."
    And the output mentions "CANCELED"

  Scenario: Another sync is running
    Given another process holds the sync lock
    When I run "tontuyau sync incremental"
    Then the command exits with code 3
    And the output mentions "already running"

  Scenario: A run that fails exits with 1 and says why
    Given TubeArchivist answers 503 to everything
    When I run "tontuyau sync full"
    Then the command exits with code 1
    And the output mentions "error:"

  Scenario: Applying the list of a run that isn't the latest full run is refused
    When I run "tontuyau sync apply-deletions run-20260101T000000Z-abcdef"
    Then the command exits with code 4
    And the output mentions "isn't the latest full run"

  Scenario: A malformed run id is refused before anything is read
    When I run "tontuyau sync apply-deletions ../../etc/passwd"
    Then the command exits with code 4
    And the output mentions "isn't a run id"

  Scenario: Rebuilding the index from the command line
    Given a first sync has run
    When I run "tontuyau index rebuild"
    Then the command exits with code 0
    And reads and writes of "videos" both use generation 2
