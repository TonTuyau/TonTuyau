Feature: Logging in
  Accounts live in PostgreSQL; the first start creates the admin. Logging in gives a session cookie (HttpOnly,
  SameSite=Strict, Secure): 8 hours renewed while in use, or 30 days with "Stay logged in". Every refusal looks the
  same, and a client address gets 5 attempts a minute.

  # ---------------------------------------------------------------- the admin account

  Scenario: The first start creates the admin, later starts never again
    Given the setting "TONTUYAU_ADMIN_USER" is "boss"
    And the setting "TONTUYAU_ADMIN_PASSWORD" is "first-password"
    When I log in as "boss" with "first-password"
    Then the response status is 204
    And the database has 1 account
    Given the setting "TONTUYAU_ADMIN_PASSWORD" is "second-password"
    When the backend restarts
    And I log in as "boss" with "second-password"
    Then the login is refused
    And the database has 1 account

  Scenario: Once the admin exists, the backend starts without its password
    When I log in as the admin
    Then the response status is 204
    Given the setting "TONTUYAU_ADMIN_PASSWORD" is empty
    When the backend restarts
    And I log in as the admin
    Then the response status is 204

  Scenario: Without the admin's password, the first start stops with an error saying so
    Given the setting "TONTUYAU_ADMIN_PASSWORD" is empty
    When the backend starts
    Then it fails to start with an error about "TONTUYAU_ADMIN_PASSWORD"

  # ---------------------------------------------------------------- logging in and out

  Scenario: Logging in gives a session cookie, and the backend knows who it is
    When I ask who is logged in
    Then nobody is logged in
    And the answer is a problem 401
    And the account "admin" has no last login time
    When I log in as the admin
    Then the response status is 204
    And the session cookie is HttpOnly, SameSite=Strict and Secure
    And the session cookie ends with the browser
    And the account "admin" has a last login time
    When I ask who is logged in
    Then I am logged in as "admin"
    And the header "Cache-Control" is "no-store, no-cache"

  Scenario: "Stay logged in" keeps the cookie for 30 days
    When I log in as "admin" with "integration-admin-password", staying logged in
    Then the response status is 204
    And the session cookie lasts 30 days

  Scenario Outline: Every refused login looks the same
    When I log in as "<username>" with "<password>"
    Then the login is refused
    When I ask who is logged in
    Then nobody is logged in

    Examples:
      | username | password                   |
      | admin    | wrong-password             |
      | Admin    | integration-admin-password |
      | nobody   | integration-admin-password |
      | nobody   |                            |
      |          |                            |

  Scenario: Logging out
    When I log in as the admin
    And I log out
    Then the response status is 204
    When I ask who is logged in
    Then nobody is logged in

  Scenario: A restart keeps everyone logged in (the cookies' keys are in PostgreSQL)
    When I log in as the admin
    And the backend restarts
    And I ask who is logged in
    Then I am logged in as "admin"

  Scenario: Over plain HTTP the cookie can be set without Secure
    Given the setting "TONTUYAU_COOKIE_SECURE" is "false"
    When I log in as the admin
    Then the session cookie isn't Secure

  # ---------------------------------------------------------------- abuse

  Scenario: Five attempts a minute per client address, then 429
    When I try a wrong password 5 times from "203.0.113.1"
    And I log in as the admin from "203.0.113.1"
    Then the answer is a problem 429
    # The front server adds the address it saw last: what the client wrote before it doesn't count.
    When I log in as the admin from "203.0.113.1, 203.0.113.2"
    Then the response status is 204

  Scenario: The login takes JSON only
    When I send the login "username=admin&password=integration-admin-password" as "application/x-www-form-urlencoded"
    Then the answer is a problem 415

  # The login's body limit (4 KB, 413 above) is Kestrel's: the in-memory test server doesn't apply it.
