@elasticsearch @sync
Feature: Automatic index rebuild
  After an upgrade whose index definitions changed (QA10 M5-10), the indices rebuild themselves: when the backend
  starts (in the background, users keep the current indices meanwhile), or else at the next import, which rebuilds
  them first. It's a run like the others (same lock, history, log), and since 2.0 it writes the library from
  PostgreSQL: nothing is read from TubeArchivist, which may be down or gone.

  Background:
    Given TubeArchivist has the channel "alpha" with 12 videos
    And the backend is ready
    And a first import has run

  Scenario: Indices from the current definitions: nothing happens at startup
    When the backend restarts and is ready
    Then no run starts by itself
    And reads and writes of "videos" both use generation 1

  Scenario: Changed definitions: the backend rebuilds at startup and switches users to the new indices
    Given the live "videos" index was made by an older version
    When the backend restarts and is ready
    Then an index rebuild started by the backend succeeds
    And reads and writes of "videos" both use generation 2
    And reads and writes of "channels" both use generation 2
    And the "videos" index is reported as up to date
    And TonTuyau has 12 videos and 1 channel

  Scenario: TubeArchivist unreachable: the backend rebuilds at startup all the same
    Given the live "videos" index was made by an older version
    And TubeArchivist answers 503 to everything
    When the backend restarts and is ready
    Then an index rebuild started by the backend succeeds
    And reads and writes of "videos" both use generation 2
    And the "videos" index is reported as up to date
    And TonTuyau has 12 videos

  Scenario: An import while a rebuild is pending rebuilds first, then imports, and says so
    Given the live "videos" index was made by an older version
    And TubeArchivist downloads 2 new videos in "alpha"
    When I run "tontuyau import"
    Then the command exits with code 0
    And the output mentions "Index rebuild pending (the index definitions changed):"
    And the output mentions "this run rebuilt the indices first, then imported."
    And the output mentions "Index rebuild first"
    And reads and writes of "videos" both use generation 2
    And TonTuyau has 14 videos

  Scenario: The status says a rebuild is pending
    Given the live "videos" index was made by an older version
    When I run "tontuyau status"
    Then the command exits with code 0
    And the output mentions "Index rebuild  : pending"
