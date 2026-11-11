@elasticsearch @api
Feature: A library for its accounts only
  2.3.0: the admin's accounts page sets who can watch: everyone who reaches TonTuyau (the default), or the logged-in
  accounts only. Then, without an account's session, the browsing API, the pictures, the files and streams and the
  OpenAPI document answer 401, and the page shows the login page (it asks /api/auth/access, open to anyone). Logging
  in, the admin API (an admin's session), the browser extension's (its key) and /health stay as they are. A change
  applies at once.

  Background:
    Given the channels:
      | key      | name      |
      | tomcooks | Tom Cooks |
    And the videos:
      | key    | title      | channel  |
      | huiles | Les huiles | tomcooks |
    And the file of the video "huiles" is on the media share with 1000 bytes

  Scenario: Open by default: anyone watches
    When I request "/api/auth/access"
    Then the answer has exactly the fields "accountsOnly, passwordResetUrl"
    And "accountsOnly" is "false"
    When I request "/api/videos/{video:huiles}"
    Then the response status is 200
    When I request "/media/{video:huiles}"
    Then the response status is 200

  Scenario Outline: For its accounts only, the library answers 401 without a session: <what>
    Given the library is for its accounts only
    When I request "<path>"
    Then the answer is a problem 401
    And the problem says "This TonTuyau is for its accounts: log in to watch."

    Examples:
      | what             | path                                |
      | the home page    | /api/home                           |
      | a video          | /api/videos/{video:huiles}          |
      | a channel        | /api/channels/{channel:tomcooks}    |
      | the search       | /api/search?q=huiles                |
      | a thumbnail      | /art/videos/{video:huiles}          |
      | a file           | /media/{video:huiles}               |
      | a stream         | /media/{video:huiles}/hls/x/index.m3u8 |
      | the API document | /openapi/v1.json                    |

  Scenario: For its accounts only, logging in, saying it's closed and the health stay open
    Given the library is for its accounts only
    When I request "/api/auth/access"
    Then "accountsOnly" is "true"
    When I ask who is logged in
    Then nobody is logged in
    And the problem says "Nobody is logged in."
    When I request "/health"
    Then the response status is 200
    When I log in as the admin
    Then the response status is 204

  Scenario: A user's session watches, the pictures and files too; an API key doesn't, but still does its own
    Given there is the user "camille" with the password "camille-password"
    And the API key "Firefox" exists
    And the library is for its accounts only
    When I log in as "camille" with "camille-password"
    And I request "/api/videos/{video:huiles}" with my session
    Then the response status is 200
    When I request "/art/videos/{video:huiles}" with my session
    Then the response status is 200
    When I request "/media/{video:huiles}" with my session
    Then the response status is 200
    When I send GET "/api/videos/{video:huiles}" with the API key "Firefox"
    Then the answer is a problem 401
    When I send GET "/api/extension/connection" with the API key "Firefox"
    Then the response status is 200

  Scenario: The admin sets it, at once, saying who and when; a user can't
    Given there is the user "camille" with the password "camille-password"
    When I log in as the admin
    And I request "/api/admin/settings/access" with my session
    Then 'accountsOnly' is 'false'
    And "changedBy" is null
    When I send PUT "/api/admin/settings/access" with my session and '{"accountsOnly": true}'
    Then the response status is 200
    And the answer has exactly the fields "accountsOnly, changedBy, changedAt"
    And "changedBy" is "admin"
    And "changedAt" is not null
    When I request "/api/videos/{video:huiles}"
    Then the answer is a problem 401
    When I send PUT "/api/admin/settings/access" with my session and '{}'
    Then the answer is a problem 400 about "accountsOnly"
    When I send PUT "/api/admin/settings/access" with my session and '{"accountsOnly": false}'
    And I request "/api/videos/{video:huiles}"
    Then the response status is 200
    When I log in as "camille" with "camille-password"
    And I send PUT "/api/admin/settings/access" with my session and '{"accountsOnly": true}'
    Then the answer is a problem 403
