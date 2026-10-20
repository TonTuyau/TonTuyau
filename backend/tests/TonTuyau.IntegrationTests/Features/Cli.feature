@elasticsearch @sync
Feature: Command line
  `tontuyau` inside the backend container runs one command and exits. Since 1.4 it asks the backend to (through its
  Unix socket; here its in-memory server) and prints the run's log as it comes, then the report.
  Exit codes: 0 done, 1 the run failed or was canceled, 2 usage error, 3 another run is going on, 4 refused.

  Background:
    Given TubeArchivist has the channel "alpha" with 5 videos
    And the backend is ready

  Scenario: An import from the command line
    When I run "tontuyau import"
    Then the command exits with code 0
    And the output mentions "import by cli  SUCCEEDED"
    And the output mentions "  read      5 videos / 1 channels"
    And the output mentions "  store     new 5, already there 0"
    And TonTuyau has 5 videos and 1 channel

  # One plain line per update, terminal or not (QA16-1).
  Scenario: An import from the command line prints its progress, then the report
    Given the media share has every video's file except "alpha-03"
    When I run "tontuyau import"
    Then the command exits with code 0
    And the progress shows the phases "channels, videos, media check" before the report
    And the output mentions "] channels      1 / 1 read"
    And the output mentions "] videos        1 / 1 channels · 5 videos"
    And the output mentions "] media check   5 files checked · 1 missing"
    And the output mentions "import by cli  SUCCEEDED"

  Scenario: Status shows the lock, the indices and the last runs
    Given a first import has run
    When I run "tontuyau status"
    Then the command exits with code 0
    And the output mentions "Lock           : free"
    And the output mentions "Index videos"
    And the output mentions "SUCCEEDED"

  # 1.4: the command line asks the backend to run it, and prints the run's log as it comes.
  Scenario: A run from the command line is the backend's: in its log, the Tasks page sees it
    When I run "tontuyau import"
    Then the command exits with code 0
    And the output mentions "started: Import by Cli."
    And the output mentions "finished: Succeeded."
    And the last run's log has, in order, lines with:
      | level       | text                       |
      | information | started: Import by Cli     |
      | information | finished: Succeeded        |

  Scenario: Ctrl-C cancels the run in the backend, and the report says so
    Given TubeArchivist downloads 15 new videos in "alpha"
    And Ctrl-C is pressed right before TubeArchivist answers page 2
    When I run "tontuyau import"
    Then the command exits with code 1
    And the output mentions "Ctrl-C: canceling the run"
    And the output mentions "warning: Run run-"
    And the output mentions ": canceled by the command line."
    And the output mentions "CANCELED"

  Scenario: Another run is going on: the command queues it, and follows it once it starts
    Given another process holds the run lock
    And the other process lets go of the run lock in 2 seconds
    When I run "tontuyau import"
    Then the command exits with code 0
    And the output mentions "queued: Import by Cli"
    And the output mentions "SUCCEEDED"

  Scenario: A run that fails exits with 1 and says why
    Given TubeArchivist answers 503 to everything
    When I run "tontuyau import"
    Then the command exits with code 1
    And the output mentions "error:"

  Scenario: Without TubeArchivist's settings, the import is refused
    Given TubeArchivist isn't set up
    When I run "tontuyau import"
    Then the command exits with code 4
    And the output mentions "TubeArchivist isn't set up"

  Scenario: Rebuilding the index from the command line
    Given a first import has run
    When I run "tontuyau index rebuild"
    Then the command exits with code 0
    And reads and writes of "videos" both use generation 2
