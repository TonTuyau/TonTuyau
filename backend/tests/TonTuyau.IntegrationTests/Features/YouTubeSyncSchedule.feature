@elasticsearch @sync @api @youtube
Feature: The incremental YouTube sync, every few hours
  The incremental YouTube sync runs by itself (2.0, B5) every few hours, 6 by default, a YouTube setting (0: by hand
  only): asked for that long after the last one was, at once when none ever ran, while TonTuyau is subscribed to a
  channel. The backend looks every minute. The Tasks page says when it runs next.

  Background:
    Given the clock shows "2026-10-20T08:00:00Z"
    And TubeArchivist has the channel "alpha" with 2 videos
    And TubeArchivist is subscribed to the channel "alpha"
    And the backend is ready
    And a first import has run
    And I logged in as the admin

  Scenario: It runs at once the first time, then 6 hours after the last one
    When the clock moves on 1 minutes
    Then the latest run's "task" is "youTubeSync" within 10 seconds
    And the latest run ends succeeded within 30 seconds
    And the latest run's "trigger" is "scheduler" within 5 seconds
    When I request "/api/admin/tasks" with my session
    Then "tasks[1].task" is "youTubeSync"
    And "tasks[1].next" is "2026-10-20T14:01:00Z"
    And "tasks[2].next" is null
    When the clock moves on 300 minutes
    And the clock moves on 1 minutes
    Then the history has 1 run of "youTubeSync" for 3 seconds
    When the clock moves on 60 minutes
    Then the history has 2 runs of "youTubeSync" within 10 seconds
    And the latest run ends succeeded within 30 seconds

  Scenario: At 0 hours it runs by hand only
    When I send the YouTube settings '{"comments": "off", "syncEveryHours": 0}' with my session
    Then the response status is 200
    And "syncEveryHours" is "0"
    When the clock moves on 1 minutes
    Then the history has 0 runs of "youTubeSync" for 3 seconds
    When I request "/api/admin/tasks" with my session
    Then "tasks[1].next" is null

  Scenario Outline: The interval is from 0 to 168 hours
    When I send the YouTube settings '{"comments": "off", "syncEveryHours": <hours>}' with my session
    Then the response status is 400

    Examples:
      | hours |
      | -1    |
      | 169   |
