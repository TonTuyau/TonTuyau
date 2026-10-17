@youtube @api
Feature: The YouTube cookie
  TonTuyau reads YouTube signed in with the cookie the browser extension sends (PUT /api/extension/youtube-cookie, a
  browserExtension key or an admin's session): only its youtube.com cookies are kept, in PostgreSQL, and it's checked
  at once by reading the account's Liked videos with yt-dlp. Its content is never shown. The admin's YouTube page
  (GET /api/admin/youtube) shows the cookie's state and the tools' versions; an admin checks it again or removes it
  (contract v1.8, TonTuyau 1.5). yt-dlp is a fake here: it answers by the cookie's value.

  Scenario: The extension sends a cookie that works; only its youtube.com cookies are kept
    Given the API key "Firefox" exists
    When the extension sends the YouTube cookie "SIGNED_IN" with the API key "Firefox"
    Then the response status is 200
    And "state" is "works"
    And "receivedBy" is "Firefox (API key)"
    And "reason" is null
    And the answer doesn't hold the cookie's content
    And the database holds only the youtube.com cookie "SIGNED_IN"
    When I send GET "/api/extension/youtube-cookie" with the API key "Firefox"
    Then "cookie.state" is "works"
    And the answer doesn't hold the cookie's content

  Scenario: The admin's YouTube page shows the cookie and the tools, never the cookie's content
    Given the API key "Firefox" exists
    And the extension sent the YouTube cookie "SIGNED_IN" with the API key "Firefox"
    When I request "/api/admin/youtube" with my session
    Then "cookie.state" is "works"
    And "cookie.receivedBy" is "Firefox (API key)"
    And "ytdlp" is "2026.08.19"
    And "deno" is "2.9.7"
    And the answer doesn't hold the cookie's content

  Scenario: A cookie YouTube doesn't take is refused, with its reason
    When I log in as the admin
    And I send the YouTube cookie "EXPIRED" with my session
    Then "state" is "refused"
    And "reason" is "The playlist does not exist."
    And "receivedBy" is "admin"

  Scenario: A bot check tells nothing: the cookie stays unchecked, with the reason
    When I log in as the admin
    And I send the YouTube cookie "BOT_CHECK" with my session
    Then "state" is "unchecked"
    And "reason" mentions "not a bot"

  Scenario Outline: A file without a youtube.com cookie is refused
    When I log in as the admin
    And I send the raw cookie file "<file>" with my session
    Then the answer is a problem 400 about "cookies"

    Examples:
      | file                                     |
      |                                          |
      | LOGIN_INFO=SIGNED_IN; PREF=f6=40000000   |

  Scenario: A new cookie replaces the old one
    When I log in as the admin
    And I send the YouTube cookie "EXPIRED" with my session
    And I send the YouTube cookie "SIGNED_IN" with my session
    Then "state" is "works"
    And the database holds only the youtube.com cookie "SIGNED_IN"

  Scenario: The admin checks it again, then removes it
    When I log in as the admin
    And I send the YouTube cookie "SIGNED_IN" with my session
    And I check the YouTube cookie with my session
    Then the response status is 200
    And "state" is "works"
    When I remove the YouTube cookie with my session
    Then the response status is 204
    When I request "/api/admin/youtube" with my session
    Then "cookie" is null
    When I check the YouTube cookie with my session
    Then the answer is a problem 404
    When I remove the YouTube cookie with my session
    Then the answer is a problem 404

  Scenario: Without a cookie, the extension and the page say there's none
    Given the API key "Firefox" exists
    When I send GET "/api/extension/youtube-cookie" with the API key "Firefox"
    Then "cookie" is null
    When I request "/api/admin/youtube" with my session
    Then "cookie" is null

  Scenario: A key can't see the admin's page, check or remove the cookie
    Given the API key "Firefox" exists
    When I send GET "/api/admin/youtube" with the API key "Firefox"
    Then the answer is a problem 403
    When I send POST "/api/admin/youtube/cookie/check" with the API key "Firefox"
    Then the answer is a problem 403
    When I send DELETE "/api/admin/youtube/cookie" with the API key "Firefox"
    Then the answer is a problem 403

  Scenario: Nobody logged in can't send or see the cookie
    When I send the YouTube cookie "SIGNED_IN" with my session
    Then the answer is a problem 401
    When I request "/api/admin/youtube" with my session
    Then the answer is a problem 401
    When I request "/api/admin/youtube/settings" with my session
    Then the answer is a problem 401
