@elasticsearch @api
Feature: The admin dashboard
  Logged-in accounts only. Every number is counted by Elasticsearch over what users see ("gone from YouTube" is what
  TonTuyau confirmed, 1.5: never TubeArchivist's flag alone). The import history has its own call (ImportHistory.feature).

  Background:
    Given the channels:
      | key      | name      |
      | tomcooks | Tom Cooks |
      | arte     | ARTE      |
      | zeste    | Zeste     |
    And the videos:
      | key     | channel  | type   | duration | active | youtube     |
      | huiles  | tomcooks | video  | 1800     | true   |             |
      | poulet  | tomcooks | video  | 600      | false  | gone        |
      | morteau | tomcooks | short  | 60       | false  | retry later |
      | usa     | arte     | video  | 3600     | false  | gone        |
      | astuce  | arte     | short  | 40       | true   | gone        |
      | debat   | arte     | stream | 3900     | false  |             |

  Scenario: Nobody logged in gets a 401, not the dashboard
    When I request "/api/admin/dashboard"
    Then the answer is a problem 401
    And the problem says "Nobody is logged in."
    When I log in as the admin
    And I log out
    And I request "/api/admin/dashboard" with my session
    Then the answer is a problem 401

  Scenario: The library's totals and play time
    When I log in as the admin
    And I request "/api/admin/dashboard" with my session
    Then the response status is 200
    And the header "Cache-Control" is "no-store, no-cache"
    And the answer has exactly the fields "lastSync, totals, playTime, channels"
    And 'totals' is '{"channels":3,"videos":3,"shorts":2,"streams":1,"gone":3}'
    And 'playTime.totalSeconds' is '10000'
    And the play time by type is:
      | type   | count | seconds | share | averageSeconds |
      | video  | 3     | 6000    | 0.6   | 2000           |
      | short  | 2     | 100     | 0.01  | 50             |
      | stream | 1     | 3900    | 0.39  | 3900           |

  Scenario: Every channel, biggest first, with its picture
    Given the sync recorded the thumbnail of the channel "tomcooks" as 900 px wide with the versions "88, 176"
    When I log in as the admin
    And I request "/api/admin/dashboard" with my session
    Then the dashboard's channels are:
      | name      | videos | shorts | streams | gone | retryLater | notChecked | total |
      | ARTE      | 1      | 1      | 1       | 2    | 1          | 0          | 3     |
      | Tom Cooks | 2      | 1      | 0       | 1    | 1          | 1          | 3     |
      | Zeste     | 0      | 0      | 0       | 0    | 0          | 0          | 0     |
    And "channels[1].id" is "{channel:tomcooks}"
    And "channels[1].thumbnailUrl" is "/art/channels/{channel:tomcooks}/thumb"
    And "channels[1].thumbnails" lists the widths "88, 176, 900"
    And "channels[2].thumbnailUrl" is null

  Scenario: The last sync is when the latest successful sync finished, whether it brought videos or not
    Given the sync runs:
      | started              | outcome   | new | videos | shorts | streams | rebuild |
      | 2026-09-01T07:00:00Z | succeeded | 40  |        |        |         | false   |
      | 2026-09-05T07:00:00Z | succeeded | 0   | 0      | 0      | 0       | false   |
      | 2026-09-06T07:00:00Z | failed    | 3   | 2      | 1      | 0       | false   |
      | 2026-09-07T07:00:00Z | running   | 2   | 2      | 0      | 0       | false   |
    And a run applied a deletion list at "2026-09-08T07:00:00Z"
    When I log in as the admin
    And I request "/api/admin/dashboard" with my session
    Then "lastSync" is "2026-09-05T07:10:00Z"

  Scenario: Without any run, no last sync
    When I log in as the admin
    And I request "/api/admin/dashboard" with my session
    Then "lastSync" is null
