@api
Feature: API keys
  A program uses an API key instead of logging in: it sends "Authorization: Bearer tt_…". A key has a kind, and may
  call only the endpoints that take its kind; every other endpoint (the admin's) refuses keys. In 1.5 the only kind is
  browserExtension (TonTuyau's browser extension: /api/extension/…). Keys have unique names, are shared by every
  admin, and are managed under /api/admin/api-keys by a logged-in admin. The secret is given once, at creation: only
  its hash is kept (contract v1.8, TonTuyau 1.5). No Elasticsearch here: a key is refused before any search runs.

  # ---------------------------------------------------------------- managing keys

  Scenario: Creating a key gives its secret once, then only its name and prefix
    When I log in as the admin
    And I create the API key "Firefox, laptop"
    Then the response status is 201
    And the new key's secret is "tt_" and 40 random characters
    And "key.name" is "Firefox, laptop"
    And "key.kind" is "browserExtension"
    And "key.enabled" is "true"
    And "key.createdBy" is "admin"
    And "key.lastUsedAt" is null
    And the API keys are "Firefox, laptop"
    And no answer about API keys holds a secret
    And the database keeps only the hash of the API key "Firefox, laptop"

  Scenario: The latest key comes first
    When I log in as the admin
    And I create the API key "Firefox, laptop"
    And I create the API key "Chrome, work"
    Then the API keys are "Chrome, work | Firefox, laptop"

  Scenario: Names are unique whatever the case
    Given the API key "Firefox" exists
    When I create the API key "firefox"
    Then the answer is a problem 409
    And the problem says "A key is already called “firefox”."
    And the API keys are "Firefox"

  Scenario Outline: A name is 1 to 60 characters, on one line
    When I log in as the admin
    And I create the API key "<name>"
    Then the answer is a problem 400 about "name"

    Examples:
      | name                                                          |
      |                                                               |
      | aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa |
      | two\nlines                                                    |

  Scenario: A name is trimmed
    When I log in as the admin
    And I create the API key "  Firefox  "
    Then "key.name" is "Firefox"

  Scenario Outline: A key has a known kind
    When I log in as the admin
    And I create the API key "Firefox" of kind "<kind>"
    Then the answer is a problem 400 about "kind"

    Examples:
      | kind     |
      |          |
      | admin    |
      | instance |

  Scenario: Nobody logged in can't see or make keys
    When I list the API keys with my session
    Then the answer is a problem 401
    When I create the API key "Firefox"
    Then the answer is a problem 401

  # ---------------------------------------------------------------- what a key may do

  Scenario: A browser extension's key opens the extension's endpoints, named after itself, and its use is noted
    Given the API key "Firefox, laptop" exists
    When I send GET "/api/extension/connection" with the API key "Firefox, laptop"
    Then the response status is 200
    And "name" is "Firefox, laptop (API key)"
    And the API key "Firefox, laptop" is enabled, last used now

  Scenario Outline: A key never opens the admin's endpoints
    Given the API key "Firefox" exists
    When I send <method> "<path>" with the API key "Firefox"
    Then the answer is a problem 403
    And the problem says "This API key's kind doesn't allow this."

    Examples:
      | method | path                              |
      | GET    | /api/admin/tasks                  |
      | GET    | /api/admin/dashboard              |
      | POST   | /api/admin/tasks/import/runs      |
      | GET    | /api/admin/api-keys               |
      | POST   | /api/admin/api-keys/1/disable     |

  Scenario: An admin's session opens the extension's endpoints too
    When I log in as the admin
    And I request "/api/extension/connection" with my session
    Then "name" is "admin"

  Scenario: The extension's endpoints need a key or a session
    When I request "/api/extension/connection" with my session
    Then the answer is a problem 401

  Scenario Outline: An unknown or malformed key is refused
    When I send GET "/api/extension/connection" with the bearer "<bearer>"
    Then the answer is a problem 401
    And the problem says "The API key is unknown or disabled."
    And the header "WWW-Authenticate" is "Bearer"

    Examples:
      | bearer                                      |
      | tt_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA |
      | tt_short                                    |
      | not-a-key                                   |

  Scenario: A disabled key is refused until it's enabled again
    Given the API key "Firefox" exists
    When I disable the API key "Firefox"
    Then "enabled" is "false"
    When I send GET "/api/extension/connection" with the API key "Firefox"
    Then the answer is a problem 401
    And the API key "Firefox" is disabled, last used never
    When I enable the API key "Firefox"
    And I send GET "/api/extension/connection" with the API key "Firefox"
    Then the response status is 200

  Scenario: A deleted key is refused for good
    Given the API key "Firefox" exists
    When I delete the API key "Firefox"
    Then the response status is 204
    And the API keys are ""
    When I send GET "/api/extension/connection" with the API key "Firefox"
    Then the answer is a problem 401

  Scenario: Deleting a key that isn't there
    When I log in as the admin
    And I delete the API key 42
    Then the answer is a problem 404

  Scenario: The session still works beside the keys
    Given the API key "Firefox" exists
    When I ask who is logged in
    Then I am logged in as "admin"
