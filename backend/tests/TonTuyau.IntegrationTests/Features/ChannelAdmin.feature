@elasticsearch @api
Feature: A channel's admin page
  GET /api/admin/channels/{id} and /api/admin/channels/{id}/videos (contract v1.12, TonTuyau 1.5): the channel and its
  videos as TonTuyau knows them on YouTube (not checked, on YouTube, retry later, gone), latest published first, by
  state or by words. Refreshing them is a task. An admin's session only.

  Background:
    Given the channels:
      | key      | name      | youtube    |
      | tomcooks | Tom Cooks | on YouTube |
      | zeste    | Zeste     |            |
    And the videos:
      | key     | title                      | channel  | type  | published  | youtube     |
      | huiles  | Les huiles de cuisson      | tomcooks | video | 2026-09-01 | on YouTube  |
      | poulet  | Le poulet rôti du dimanche | tomcooks | video | 2026-09-03 | gone        |
      | morteau | La saucisse de Morteau     | tomcooks | short | 2026-09-05 | retry later |
      | creme   | Crème brûlée               | tomcooks | video | 2026-09-07 |             |

  Scenario: Nobody logged in gets a 401
    When I request "/api/admin/channels/{channel:tomcooks}" with my session
    Then the answer is a problem 401
    When I request "/api/admin/channels/{channel:tomcooks}/videos" with my session
    Then the answer is a problem 401

  Scenario: The channel, its state on YouTube, how many of its videos are in each state
    When I log in as the admin
    And I request "/api/admin/channels/{channel:tomcooks}" with my session
    Then the response status is 200
    And the header "Cache-Control" is "no-store, no-cache"
    And the answer has exactly the fields "channel, youTube, videos, subscription"
    And "channel.name" is "Tom Cooks"
    And "channel.videoCounts.videos" is "3"
    And "youTube.state" is "onYouTube"
    And 'videos' is '{"notChecked":1,"onYouTube":1,"unlisted":0,"private":0,"retryLater":1,"gone":1}'

  Scenario: Its videos, latest published first, each with its state on YouTube
    When I log in as the admin
    And I request "/api/admin/channels/{channel:tomcooks}/videos" with my session
    Then the response status is 200
    And "total" is "4"
    And "items" has 4 items
    And "items[0].video.id" is "{video:creme}"
    And "items[0].youTube.state" is "notChecked"
    And "items[0].youTube.checkedAt" is null
    And "items[2].video.id" is "{video:poulet}"
    And "items[2].youTube.state" is "gone"
    And "items[2].youTube.reason" is "Video unavailable"
    And "items[2].video.active" is "false"

  Scenario Outline: By state, or by words of the title
    When I log in as the admin
    And I request "/api/admin/channels/{channel:tomcooks}/videos?<query>" with my session
    Then "items" has <count> item
    And "items[0].video.id" is "{video:<first>}"

    Examples:
      | query            | count | first   |
      | state=gone       | 1     | poulet  |
      | state=retryLater | 1     | morteau |
      | state=notChecked | 1     | creme   |
      | q=poulet         | 1     | poulet  |
      | q=cr             | 1     | creme   |

  Scenario: Page by page
    When I log in as the admin
    And I request "/api/admin/channels/{channel:tomcooks}/videos?size=3" with my session
    Then "items" has 3 items
    When I request the next page of "/api/admin/channels/{channel:tomcooks}/videos?size=3" with my session
    Then "items" has 1 item
    And "items[0].video.id" is "{video:huiles}"

  Scenario Outline: A channel that isn't there, or a request that isn't right
    When I log in as the admin
    And I request "<path>" with my session
    Then the answer is a problem <status>

    Examples:
      | path                                                        | status |
      | /api/admin/channels/UCnope_________________0                | 404    |
      | /api/admin/channels/UCnope_________________0/videos         | 404    |
      | /api/admin/channels/not-a-channel                           | 400    |
      | /api/admin/channels/{channel:tomcooks}/videos?state=shiny   | 400    |
      | /api/admin/channels/{channel:tomcooks}/videos?size=0        | 400    |
