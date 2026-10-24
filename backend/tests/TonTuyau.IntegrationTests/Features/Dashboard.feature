@elasticsearch @api
Feature: The admin dashboard
  Logged-in accounts only. Every number is counted by Elasticsearch over what users see ("gone from YouTube" is what
  TonTuyau confirmed, 1.5: never TubeArchivist's flag alone). The import history has its own call (ImportHistory.feature),
  and so do the channels (alpha.3: a page at a time). The storage (2.2.0): the videos folder's disk (a fake here) and
  the videos' files by type, with the YouTube page's threshold.

  Background:
    Given the channels:
      | key      | name      |
      | tomcooks | Tom Cooks |
      | arte     | ARTE      |
      | zeste    | Zeste     |
    And the videos:
      | key     | channel  | type   | duration | youtube     | size       |
      | huiles  | tomcooks | video  | 1800     |             | 2000000000 |
      | poulet  | tomcooks | video  | 600      | gone        | 500000000  |
      | morteau | tomcooks | short  | 60       | retry later | 40000000   |
      | usa     | arte     | video  | 3600     | gone        | 3000000000 |
      | astuce  | arte     | short  | 40       | gone        | 30000000   |
      | debat   | arte     | stream | 3900     |             | 5000000000 |

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
    And the answer has exactly the fields "lastSync, totals, playTime, storage"
    And 'totals' is '{"channels":3,"videos":3,"shorts":2,"streams":1,"gone":3}'
    And 'playTime.totalSeconds' is '10000'
    And the play time by type is:
      | type   | count | seconds | share | averageSeconds |
      | video  | 3     | 6000    | 0.6   | 2000           |
      | short  | 2     | 100     | 0.01  | 50             |
      | stream | 1     | 3900    | 0.39  | 3900           |

  Scenario: The storage: the videos folder's disk, the videos' files by type, and the threshold
    Given the videos folder's disk has 412 GB free of 1000 GB
    When I log in as the admin
    And I request "/api/admin/dashboard" with my session
    Then 'storage.disk' is '{"total":1000000000000,"free":412000000000}'
    And 'storage.types' is '[{"type":"video","bytes":5500000000,"count":3},{"type":"short","bytes":70000000,"count":2},{"type":"stream","bytes":5000000000,"count":1}]'
    And "storage.minFree" is "50000000000"
    And "storage.holdDownloads" is "true"
    Given the videos folder's disk doesn't say its free space
    When I send the YouTube settings '{"comments": "off", "minFreeGb": 120, "holdBelowMinFree": false}' with my session
    And I request "/api/admin/dashboard" with my session
    Then "storage.disk" is null
    And "storage.minFree" is "120000000000"
    And "storage.holdDownloads" is "false"

  Scenario: Every channel, biggest first, with its picture
    Given the import recorded the thumbnail of the channel "tomcooks" as 900 px wide with the versions "88, 176"
    When I log in as the admin
    And I request "/api/admin/dashboard/channels" with my session
    Then the response status is 200
    And the header "Cache-Control" is "no-store, no-cache"
    And the dashboard's channels are:
      | name      | videos | shorts | streams | gone | retryLater | notChecked | total | size       |
      | ARTE      | 1      | 1      | 1       | 2    | 0          | 1          | 3     | 8030000000 |
      | Tom Cooks | 2      | 1      | 0       | 1    | 1          | 1          | 3     | 2540000000 |
      | Zeste     | 0      | 0      | 0       | 0    | 0          | 0          | 0     | 0          |
    And "total" is "3"
    And "nextCursor" is null
    And "items[1].id" is "{channel:tomcooks}"
    And "items[1].thumbnailUrl" is "/art/channels/{channel:tomcooks}/thumb"
    And "items[1].thumbnails" lists the widths "88, 176, 900"
    And "items[2].thumbnailUrl" is null

  Scenario: The channels a page at a time, found by name, with videos gone, sorted by a column
    When I log in as the admin
    And I request "/api/admin/dashboard/channels?sort=name&size=2" with my session
    Then the dashboard's channels are:
      | name      |
      | ARTE      |
      | Tom Cooks |
    And "total" is "3"
    When I request the next page of "/api/admin/dashboard/channels?sort=name&size=2" with my session
    Then the dashboard's channels are:
      | name  |
      | Zeste |
    When I request "/api/admin/dashboard/channels?q=COOK" with my session
    Then the dashboard's channels are:
      | name      |
      | Tom Cooks |
    When I request "/api/admin/dashboard/channels?gone=true&sort=gone&order=asc" with my session
    Then the dashboard's channels are:
      | name      | gone |
      | Tom Cooks | 1    |
      | ARTE      | 2    |
    When I request "/api/admin/dashboard/channels?sort=size&order=asc" with my session
    Then the dashboard's channels are:
      | name      | size       |
      | Zeste     | 0          |
      | Tom Cooks | 2540000000 |
      | ARTE      | 8030000000 |

  Scenario Outline: An unknown <field> is refused
    When I log in as the admin
    And I request "/api/admin/dashboard/channels?<field>=<value>" with my session
    Then the answer is a problem 400 about "<field>"

    Examples:
      | field  | value |
      | sort   | heavy |
      | order  | up    |
      | size   | 0     |
      | cursor | nope  |

  # alpha.8: an import or a download of TonTuyau's, the latest finished one that added videos (failed after some too).
  Scenario: The last time videos came is when the latest finished run that added some ended, an import or a download
    Given the runs that add videos:
      | task          | started              | outcome   | new | videos | shorts | streams | rebuild |
      | import        | 2026-09-01T07:00:00Z | succeeded | 40  |        |        |         | false   |
      | import        | 2026-09-05T07:00:00Z | succeeded | 0   | 0      | 0      | 0       | false   |
      | youTubeSync   | 2026-09-06T05:00:00Z | succeeded | 2   | 1      | 1      | 0       | false   |
      | downloadVideo | 2026-09-06T07:00:00Z | failed    | 1   | 1      | 0      | 0       | false   |
      | youTubeSync   | 2026-09-06T11:00:00Z | succeeded | 0   | 0      | 0      | 0       | false   |
      | youTubeSync   | 2026-09-07T07:00:00Z | running   | 2   | 2      | 0      | 0       | false   |
    And a refresh ran at "2026-09-08T07:00:00Z"
    When I log in as the admin
    And I request "/api/admin/dashboard" with my session
    Then "lastSync" is "2026-09-06T07:10:00Z"

  Scenario: Without any run, no last import
    When I log in as the admin
    And I request "/api/admin/dashboard" with my session
    Then "lastSync" is null
