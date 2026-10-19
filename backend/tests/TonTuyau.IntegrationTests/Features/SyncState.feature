@elasticsearch
Feature: Run lock and state
  Only one run goes on at a time, across every process (the backend and the CLI in `docker exec`). The lock is a
  row in PostgreSQL (2.0) with a lease its holder keeps renewing, so the lock of a crashed holder can be taken over
  once its lease has run out.

  Background:
    Given the backend is ready

  Scenario: A second run can't take the lock while the first one holds it
    Given a run holds the lock
    When another run tries to take the lock
    Then it doesn't get the lock
    And the lock still belongs to the first run

  Scenario: Once released, the lock can be taken again
    Given a run holds the lock
    When the first run releases the lock
    And another run tries to take the lock
    Then it gets the lock

  Scenario: The lock of a crashed run is taken over once its lease has expired
    Given a run crashed while holding the lock, and its lease expired a minute ago
    When another run tries to take the lock
    Then it gets the lock

  Scenario: The lock of a crashed run is respected while its lease runs
    Given a run crashed while holding the lock, and its lease runs for another 5 minutes
    When another run tries to take the lock
    Then it doesn't get the lock

  Scenario: The holder keeps its lease alive
    Given leases last 2 seconds and are renewed every 500 ms
    And a run holds the lock
    When 3 seconds pass
    Then the lock still belongs to the first run
    And another run still can't take the lock

  Scenario: A run whose lock was taken from it is told to stop
    Given leases last 30 seconds and are renewed every 500 ms
    And a run holds the lock
    When another process overwrites the lock
    Then the first run is told within 5 seconds that it lost the lock

  Scenario: Run history lists the newest runs first
    When these runs are saved:
      | id    | mode         | outcome   | started minutes ago |
      | run-a | import       | succeeded | 300                 |
      | run-b | indexRebuild | succeeded | 120                 |
      | run-c | refreshVideo | failed    | 60                  |
      | run-d | import       | running   | 1                   |
    Then the recent runs are "run-d, run-c, run-b, run-a"

  Scenario: Art waiting to be copied is remembered
    When the art "/cache/videos/a/aaaaaaaaaaa.jpg, /cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_thumb.jpg" is pending
    Then the pending art is "/cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_thumb.jpg, /cache/videos/a/aaaaaaaaaaa.jpg"
