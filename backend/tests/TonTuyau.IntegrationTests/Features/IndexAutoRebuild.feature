@elasticsearch @sync
Feature: Automatic index rebuild
  After an upgrade whose index definitions changed (QA10 M5-10), the indices rebuild themselves: when the backend
  starts (in the background, users keep the current indices meanwhile), or else at the next sync, which rebuilds
  instead of its normal run until a rebuild succeeds. It's a sync like the others: same lock, TubeArchivist read-only,
  every safety of a full run. With the scheduler off, the backend never starts it by itself.

  Background:
    Given TubeArchivist has the channel "alpha" with 12 videos
    And the backend is ready
    And a first sync has run

  Scenario: Indices from the current definitions: nothing happens at startup
    Given the scheduler is on, with no sync due soon
    When the backend restarts and is ready
    Then no sync starts by itself
    And reads and writes of "videos" both use generation 1

  Scenario: Changed definitions: the backend rebuilds at startup and switches users to the new indices
    Given the live "videos" index was made by an older version
    And the scheduler is on, with no sync due soon
    When the backend restarts and is ready
    Then an index rebuild started by the backend succeeds
    And reads and writes of "videos" both use generation 2
    And reads and writes of "channels" both use generation 2
    And the "videos" index is reported as up to date
    And TonTuyau has 12 videos and 1 channel

  Scenario: TubeArchivist unreachable: users keep the old indices, and the next scheduled sync rebuilds instead
    Given the live "videos" index was made by an older version
    And the scheduler is on, with no sync due soon
    And TubeArchivist answers 503 to everything
    When the backend restarts and is ready
    Then an index rebuild started by the backend fails
    And reads and writes of "videos" both use generation 1
    And the "videos" index is reported as outdated
    And TonTuyau has 12 videos
    When TubeArchivist works again
    And an incremental sync runs
    Then the run succeeded
    And the run rebuilt the indices instead
    And reads and writes of "videos" both use generation 2
    When an incremental sync runs
    Then the run succeeded as an incremental run
    And the run didn't rebuild the indices

  Scenario: With the scheduler off, the backend never starts the rebuild by itself
    Given the live "videos" index was made by an older version
    When the backend restarts and is ready
    Then no sync starts by itself
    And reads and writes of "videos" both use generation 1
    And the "videos" index is reported as outdated

  Scenario: A sync from the command line rebuilds instead while a rebuild is pending, and says so
    Given the live "videos" index was made by an older version
    When I run "tontuyau sync incremental"
    Then the command exits with code 0
    And the output mentions "Index rebuild pending (the index definitions changed):"
    And the output mentions "this run rebuilt the indices instead of the sync asked for."
    And the output mentions "Index rebuild instead of the incremental sync"
    And reads and writes of "videos" both use generation 2

  Scenario: The status says a rebuild is pending
    Given the live "videos" index was made by an older version
    When I run "tontuyau sync status"
    Then the command exits with code 0
    And the output mentions "Index rebuild  : pending"
