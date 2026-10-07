@elasticsearch
Feature: Sync lock and state
  Only one sync runs at a time, across every process (the scheduler in the backend and the CLI in
  `docker exec`). The lock is a document in Elasticsearch with a lease its holder keeps renewing,
  so the lock of a crashed holder can be taken over once its lease has run out.

  Background:
    Given the backend is ready

  Scenario: A second sync can't take the lock while the first one holds it
    Given a sync holds the lock
    When another sync tries to take the lock
    Then it doesn't get the lock
    And the lock still belongs to the first sync

  Scenario: Once released, the lock can be taken again
    Given a sync holds the lock
    When the first sync releases the lock
    And another sync tries to take the lock
    Then it gets the lock

  Scenario: The lock of a crashed sync is taken over once its lease has expired
    Given a sync crashed while holding the lock, and its lease expired a minute ago
    When another sync tries to take the lock
    Then it gets the lock

  Scenario: The lock of a crashed sync is respected while its lease runs
    Given a sync crashed while holding the lock, and its lease runs for another 5 minutes
    When another sync tries to take the lock
    Then it doesn't get the lock

  Scenario: The holder keeps its lease alive
    Given leases last 2 seconds and are renewed every 500 ms
    And a sync holds the lock
    When 3 seconds pass
    Then the lock still belongs to the first sync
    And another sync still can't take the lock

  Scenario: A sync whose lock was taken from it is told to stop
    Given leases last 30 seconds and are renewed every 500 ms
    And a sync holds the lock
    When another process overwrites the lock
    Then the first sync is told within 5 seconds that it lost the lock

  Scenario: The checkpoint survives a restart
    When the checkpoint is saved with watermark 1790867581 and a full run requested
    And the backend restarts
    Then the backend becomes ready
    And the checkpoint has watermark 1790867581 and a full run requested

  Scenario: Run history lists the newest runs first and knows the latest finished full run
    When these runs are saved:
      | id    | mode        | outcome   | started minutes ago |
      | run-a | full        | succeeded | 300                 |
      | run-b | incremental | succeeded | 120                 |
      | run-c | full        | failed    | 60                  |
      | run-d | full        | running   | 1                   |
    Then the recent runs are "run-d, run-c, run-b, run-a"
    And the latest finished full run is "run-c"

  Scenario: Art waiting to be copied is remembered
    When the art "/cache/videos/a/aaaaaaaaaaa.jpg, /cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_thumb.jpg" is pending
    Then the pending art is "/cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_thumb.jpg, /cache/videos/a/aaaaaaaaaaa.jpg"
