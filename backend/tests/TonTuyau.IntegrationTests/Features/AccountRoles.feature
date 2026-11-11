@elasticsearch @api
Feature: Accounts and roles
  2.3.0: an admin does everything, a user watches the library (no admin page nor admin API). The admin makes local
  accounts on the accounts page, changes their role or password, disables or deletes them; the last enabled local
  admin stays, and nobody disables nor deletes their own account. Each account changes its own password (My account).
  A session is checked against its account every 5 minutes: an account disabled, deleted, or whose role or password
  changed since, is logged out then. The accounts made before 2.3.0 are local admins. Each browser keeps its session
  (a client address gets 5 logins a minute).

  Background:
    Given the clock shows "2026-10-26T08:00:00Z"
    # Its indices made first: the library's pages are asked for below.
    And the backend is ready
    And I logged in as the admin

  Scenario: Who is logged in says the role and where the account is from
    When I ask who is logged in
    Then the answer has exactly the fields "username, displayName, role, source"
    And "username" is "admin"
    And "role" is "admin"
    And "source" is "local"
    And "displayName" is null

  Scenario: An admin makes a user; the user watches, and is refused the admin pages
    When I send POST "/api/admin/accounts" with my session and '{"username": "camille", "password": "camille-password", "role": "user"}'
    Then the response status is 201
    And the answer has exactly the fields "id, username, displayName, role, source, disabled, createdAt, lastLoginAt"
    And "role" is "user"
    And "source" is "local"
    And "disabled" is "false"
    And "lastLoginAt" is null
    When I log out
    And I log in as "camille" with "camille-password"
    Then the response status is 204
    When I ask who is logged in
    Then "role" is "user"
    When I request "/api/home" with my session
    Then the response status is 200
    When I request "/api/admin/dashboard" with my session
    Then the answer is a problem 403
    When I request "/api/admin/accounts" with my session
    Then the answer is a problem 403
    When I request "/api/admin/api-keys" with my session
    Then the answer is a problem 403

  Scenario: The accounts by name; a name taken whatever the case, a short password, an unknown role are refused
    When I send POST "/api/admin/accounts" with my session and '{"username": "Zoé", "password": "zoe-password", "role": "admin"}'
    Then the response status is 201
    When I send POST "/api/admin/accounts" with my session and '{"username": "zoé", "password": "zoe-password", "role": "user"}'
    Then the answer is a problem 409
    When I send POST "/api/admin/accounts" with my session and '{"username": "lucas", "password": "short", "role": "user"}'
    Then the answer is a problem 400 about "password"
    When I send POST "/api/admin/accounts" with my session and '{"username": "lucas", "password": "lucas-password", "role": "owner"}'
    Then the answer is a problem 400 about "role"
    When I request "/api/admin/accounts" with my session
    Then "" has 2 items
    And "[0].username" is "admin"
    And "[0].role" is "admin"
    And "[1].username" is "Zoé"

  Scenario: The last enabled local admin stays; nobody disables nor deletes their own account
    When I request "/api/admin/accounts" with my session
    Then "[0].username" is "admin"
    When I send PUT "/api/admin/accounts/{account:admin}" with my session and '{"role": "user"}'
    Then the answer is a problem 409
    And the problem says "“admin” is the last enabled local admin: it stays, to log in when the directory can't."
    When I send PUT "/api/admin/accounts/{account:admin}" with my session and '{"disabled": true}'
    Then the answer is a problem 409
    When I send DELETE "/api/admin/accounts/{account:admin}" with my session
    Then the answer is a problem 409
    And the problem says "Your own account can't be deleted."

  Scenario: Disabled, a user can't log in, and its session ends at its next check; enabled again, it logs in
    Given there is the user "camille" with the password "camille-password"
    And I keep this session as "admin"
    When I log in as "camille" with "camille-password"
    And I keep this session as "camille"
    And I go back to the session "admin"
    And I send PUT "/api/admin/accounts/{account:camille}" with my session and '{"disabled": true}'
    Then the response status is 200
    And "disabled" is "true"
    When I go back to the session "camille"
    And I ask who is logged in
    Then I am logged in as "camille"
    When the clock moves on 6 minutes
    And I ask who is logged in
    Then nobody is logged in
    When I log in as "camille" with "camille-password"
    Then the login is refused
    When I go back to the session "admin"
    And I send PUT "/api/admin/accounts/{account:camille}" with my session and '{"disabled": false}'
    And I log in as "camille" with "camille-password"
    Then the response status is 204

  Scenario: A new password or role from the admin ends the account's sessions at their next check
    Given there is the user "camille" with the password "camille-password"
    And I keep this session as "admin"
    When I log in as "camille" with "camille-password"
    And I keep this session as "camille"
    And I go back to the session "admin"
    And I send PUT "/api/admin/accounts/{account:camille}" with my session and '{"password": "new-camille-password", "role": "admin"}'
    Then "role" is "admin"
    When I go back to the session "camille"
    And the clock moves on 6 minutes
    And I ask who is logged in
    Then nobody is logged in
    When I log in as "camille" with "camille-password"
    Then the login is refused
    When I log in as "camille" with "new-camille-password"
    And I request "/api/admin/dashboard" with my session
    Then the response status is 200

  Scenario: Deleted, its session ends at its next check
    Given there is the user "camille" with the password "camille-password"
    And I keep this session as "admin"
    When I log in as "camille" with "camille-password"
    And I keep this session as "camille"
    And I go back to the session "admin"
    And I send DELETE "/api/admin/accounts/{account:camille}" with my session
    Then the response status is 204
    When I go back to the session "camille"
    And the clock moves on 6 minutes
    And I ask who is logged in
    Then nobody is logged in

  Scenario: One changes their own password with the current one; this session goes on, the others end
    Given there is the user "camille" with the password "camille-password"
    When I log in as "camille" with "camille-password"
    And I keep this session as "other browser"
    And I log in as "camille" with "camille-password"
    And I send PUT "/api/auth/me/password" with my session and '{"current": "wrong", "password": "new-camille-password"}'
    Then the answer is a problem 400 about "current"
    When I send PUT "/api/auth/me/password" with my session and '{"current": "camille-password", "password": "new-camille-password"}'
    Then the response status is 204
    When the clock moves on 6 minutes
    And I ask who is logged in
    Then I am logged in as "camille"
    When I go back to the session "other browser"
    And I ask who is logged in
    Then nobody is logged in
    When I log in as "camille" with "new-camille-password"
    Then the response status is 204

  Scenario: Nobody logged in can't change a password
    When I log out
    And I send PUT "/api/auth/me/password" with my session and '{"current": "integration-admin-password", "password": "new-password"}'
    Then the answer is a problem 401
