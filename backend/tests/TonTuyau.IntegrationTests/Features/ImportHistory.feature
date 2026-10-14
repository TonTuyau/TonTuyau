@elasticsearch @api
Feature: The admin dashboard's import history
  The new videos of the finished sync runs (never an index rebuild, which writes every video again, nor a run that
  only applied a deletion list), over a period:
  the last day a bar per sync (an empty one too), the last 7 or 30 days a bar per day, the weeks (from Monday) of the last 365 days, or
  every month since the first import. Days are those of the sync's time zone (Europe/Paris here: UTC+2 in summer,
  UTC+1 in winter). Every step has its bar, an empty one too. Runs of 1.1 didn't record the types.

  Background:
    Given the clock shows "2026-10-14T10:00:00Z"
    And the sync runs:
      | started              | outcome   | new   | videos | shorts | streams | rebuild |
      | 2025-03-10T07:00:00Z | succeeded | 40000 |        |        |         | false   |
      | 2025-11-03T07:00:00Z | succeeded | 5     | 5      | 0      | 0       | false   |
      | 2026-09-20T07:00:00Z | succeeded | 3     | 2      | 1      | 0       | false   |
      | 2026-10-04T01:00:00Z | succeeded | 40003 | 40000  | 3      | 0       | true    |
      | 2026-10-09T07:00:00Z | failed    | 2     | 1      | 1      | 0       | false   |
      | 2026-10-13T21:30:00Z | succeeded | 4     | 3      | 0      | 1       | false   |
      | 2026-10-14T08:00:00Z | succeeded | 6     | 3      | 2      | 1       | false   |
      | 2026-10-14T09:00:00Z | running   | 2     | 2      | 0      | 0       | false   |
      | 2026-10-14T09:30:00Z | succeeded | 0     | 0      | 0      | 0       | false   |
    And a run applied a deletion list at "2026-10-14T09:45:00Z"

  # 1.2.1: every sync of the last day has its bar, an empty one for a sync that brought nothing.
  Scenario: The last day: a bar per sync of the last 24 hours, an empty one too
    When I log in as the admin
    And I request "/api/admin/dashboard/imports?period=day" with my session
    Then the response status is 200
    And the header "Cache-Control" is "no-store, no-cache"
    And the answer has exactly the fields "step, timeZone, total, bars"
    And the import history is by "run" in "Europe/Paris": 3 bars, 10 new videos
    And "bars[2].start" is "2026-10-14T09:30:00Z"
    And "bars[2].total" is "0"
    And its bars with new videos are:
      | start                | total | videos | shorts | streams | notRecorded |
      | 2026-10-13T21:30:00Z | 4     | 3      | 0      | 1       | 0           |
      | 2026-10-14T08:00:00Z | 6     | 3      | 2      | 1       | 0           |

  Scenario: The last week: a bar per day in Paris (23:30 on the 13th is the 13th)
    When I log in as the admin
    And I request "/api/admin/dashboard/imports?period=week" with my session
    Then the import history is by "day" in "Europe/Paris": 7 bars, 12 new videos
    And "bars[0].start" is "2026-10-07T22:00:00Z"
    And its bars with new videos are:
      | start                | total | videos | shorts | streams | notRecorded |
      | 2026-10-08T22:00:00Z | 2     | 1      | 1      | 0       | 0           |
      | 2026-10-12T22:00:00Z | 4     | 3      | 0      | 1       | 0           |
      | 2026-10-13T22:00:00Z | 6     | 3      | 2      | 1       | 0           |

  Scenario Outline: The last month, the default: a bar per day of the last 30 days
    When I log in as the admin
    And I request "/api/admin/dashboard/imports<query>" with my session
    Then the import history is by "day" in "Europe/Paris": 30 bars, 15 new videos
    And "bars[0].start" is "2026-09-14T22:00:00Z"
    And its bars with new videos are:
      | start                | total | videos | shorts | streams | notRecorded |
      | 2026-09-19T22:00:00Z | 3     | 2      | 1      | 0       | 0           |
      | 2026-10-08T22:00:00Z | 2     | 1      | 1      | 0       | 0           |
      | 2026-10-12T22:00:00Z | 4     | 3      | 0      | 1       | 0           |
      | 2026-10-13T22:00:00Z | 6     | 3      | 2      | 1       | 0           |

    Examples:
      | query         |
      |               |
      | ?period=month |

  Scenario: The last year: a bar per week from Monday, in winter time too
    When I log in as the admin
    And I request "/api/admin/dashboard/imports?period=year" with my session
    Then the import history is by "week" in "Europe/Paris": 53 bars, 20 new videos
    And "bars[0].start" is "2025-10-12T22:00:00Z"
    And its bars with new videos are:
      | start                | total | videos | shorts | streams | notRecorded |
      | 2025-11-02T23:00:00Z | 5     | 5      | 0      | 0       | 0           |
      | 2026-09-13T22:00:00Z | 3     | 2      | 1      | 0       | 0           |
      | 2026-10-04T22:00:00Z | 2     | 1      | 1      | 0       | 0           |
      | 2026-10-11T22:00:00Z | 10    | 6      | 2      | 2       | 0           |

  Scenario Outline: All: a bar per month since the first import, the runs of 1.1 without their types
    Given the dashboard reads the runs <page> at a time
    When I log in as the admin
    And I request "/api/admin/dashboard/imports?period=all" with my session
    Then the import history is by "month" in "Europe/Paris": 20 bars, 40020 new videos
    And "bars[0].start" is "2025-02-28T23:00:00Z"
    And its bars with new videos are:
      | start                | total | videos | shorts | streams | notRecorded |
      | 2025-02-28T23:00:00Z | 40000 | 0      | 0      | 0       | 40000       |
      | 2025-10-31T23:00:00Z | 5     | 5      | 0      | 0       | 0           |
      | 2026-08-31T22:00:00Z | 3     | 2      | 1      | 0       | 0           |
      | 2026-09-30T22:00:00Z | 12    | 7      | 3      | 2       | 0           |

    Examples: the runs read in one search, or page after page (search_after)
      | page  |
      | 10000 |
      | 2     |

  Scenario: An unknown period is refused
    When I log in as the admin
    And I request "/api/admin/dashboard/imports?period=hour" with my session
    Then the answer is a problem 400 about "period"
    When I log out
    And I request "/api/admin/dashboard/imports" with my session
    Then the answer is a problem 401
