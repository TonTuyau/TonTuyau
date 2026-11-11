@elasticsearch @ldap @api
Feature: Logging in with the LDAP directory
  2.3.0: a name that isn't a local account's logs in from the LDAP directory when it's on: its service account finds
  the name (the UID attribute), the admins filter makes it an admin, the users filter a user, and its password binds
  it. Its row is made at its first login, its role and name read again at each login and session check (a role
  changed, or a filter no longer finding it, ends its session). A name both local and the directory's is the local
  account's only. Its password changes in TonTuyau: the service account changes it, or the account itself where the
  directory refuses the service account. The settings are the admin's LDAP page's, as Jellyfin's plugin takes them,
  with tests that run on their own. The directory here is a real LLDAP, as the user's: eclegv an admin, marie and
  camille users, zoe in no group, boss a user and an LLDAP admin; each password is the name and "-password".

  Background:
    Given the clock shows "2026-10-26T08:00:00Z"

  # ---------------------------------------------------------------- logging in

  Scenario: The users filter makes a user, the admins filter an admin; the row is made at the first login
    Given the LDAP directory is on
    When I log in as "marie" with "marie-password"
    Then the response status is 204
    When I ask who is logged in
    Then "username" is "marie"
    And "displayName" is "Marie Dubois"
    And "role" is "user"
    And "source" is "ldap"
    When I log in as "eclegv" with "eclegv-password"
    And I request "/api/admin/accounts" with my session
    Then the response status is 200
    And "[1].username" is "eclegv"
    And "[1].role" is "admin"
    And "[1].source" is "ldap"
    And "[2].username" is "marie"
    And "[2].displayName" is "Marie Dubois"

  Scenario Outline: Refused: <why>
    Given the LDAP directory is on
    When I log in as "<username>" with "<password>"
    Then the login is refused

    Examples:
      | why                       | username | password        |
      | a wrong password          | marie    | wrong-password  |
      | no filter finds it        | zoe      | zoe-password    |
      | the directory hasn't it   | nobody   | nobody-password |
      | an empty password         | marie    |                 |

  Scenario: A name both local and the directory's is the local account's only
    Given there is the user "camille" with the password "local-camille"
    And the LDAP directory is on
    When I log in as "camille" with "camille-password"
    Then the login is refused
    When I log in as "camille" with "local-camille"
    And I ask who is logged in
    Then "source" is "local"

  Scenario: Off, nobody logs in from the directory; down, nobody does either, and the local admin still does
    Given the LDAP directory is on but doesn't answer
    When I log in as "marie" with "marie-password"
    Then the login is refused
    When I log in as the admin
    Then the response status is 204

  Scenario: A directory account the admin disables can't log in
    Given the LDAP directory is on
    And I logged in as the admin
    And I keep this session as "admin"
    When I log in as "marie" with "marie-password"
    And I go back to the session "admin"
    And I send PUT "/api/admin/accounts/{account:marie}" with my session and '{"disabled": true}'
    Then "disabled" is "true"
    When I send PUT "/api/admin/accounts/{account:marie}" with my session and '{"role": "admin"}'
    Then the answer is a problem 409
    When I log in as "marie" with "marie-password"
    Then the login is refused

  Scenario: Taken out of the group, a directory account is logged out at its next check
    Given the LDAP directory is on
    When I log in as "marie" with "marie-password"
    And "marie" is taken out of the directory's users group
    And I ask who is logged in
    Then I am logged in as "marie"
    When the clock moves on 6 minutes
    And I ask who is logged in
    Then nobody is logged in

  # ---------------------------------------------------------------- passwords

  Scenario: A directory account changes its password: the service account changes it in the directory
    Given the LDAP directory is on
    When I log in as "marie" with "marie-password"
    And I send PUT "/api/auth/me/password" with my session and '{"current": "wrong", "password": "new-marie-password"}'
    Then the answer is a problem 400 about "current"
    When I send PUT "/api/auth/me/password" with my session and '{"current": "marie-password", "password": "new-marie-password"}'
    Then the response status is 204
    And the directory takes "marie"'s password "new-marie-password"
    When I log in as "marie" with "new-marie-password"
    Then the response status is 204

  Scenario: The service account may not change an LLDAP admin's password: the account changes its own
    Given the LDAP directory is on
    When I log in as "boss" with "boss-password"
    And I send PUT "/api/auth/me/password" with my session and '{"current": "boss-password", "password": "new-boss-password"}'
    Then the response status is 204
    And the directory takes "boss"'s password "new-boss-password"

  Scenario: The login page's link for a forgotten password is the directory's, while it's on
    When I request "/api/auth/access"
    Then "passwordResetUrl" is null
    Given the LDAP directory is on
    When I request "/api/auth/access"
    Then "passwordResetUrl" is "http://ldap.example.com:17170/$username/password"

  # ---------------------------------------------------------------- the LDAP page

  Scenario: The settings: checked when on, the service account's password never given back, kept when none is typed
    Given I logged in as the admin
    When I request "/api/admin/settings/ldap" with my session
    Then "enabled" is "false"
    And "uidAttribute" is "uid"
    And "bindPasswordSet" is "false"
    When I send PUT "/api/admin/settings/ldap" with my session and '{"enabled": true, "url": "http://ldap.example.com"}'
    Then the answer is a problem 400 about "url"
    When I save the LDAP settings with my session
    Then the response status is 200
    And the answer has exactly the fields "enabled, url, startTls, caCertificate, bindDn, bindPasswordSet, baseDn, uidAttribute, usernameAttribute, usersFilter, adminsFilter, passwordAttribute, passwordResetUrl, clashes, changedBy, changedAt"
    And "bindPasswordSet" is "true"
    And "changedBy" is "admin"
    When I save the LDAP settings without the service account's password with my session
    Then "bindPasswordSet" is "true"
    When I run the LDAP test "connection" with my session
    Then "connected" is "true"

  Scenario: Each test on its own: the connection, who each filter finds, a name
    Given I logged in as the admin
    And there is the user "camille" with the password "local-camille"
    When I save the LDAP settings with my session
    And I run the LDAP test "connection" with my session
    Then the answer has exactly the fields "connected, error, found, user"
    And "connected" is "true"
    And "found" is null
    When I run the LDAP test "admins" with my session
    Then "found.total" is "1"
    And "found.accounts[0].username" is "eclegv"
    When I run the LDAP test "users" with my session
    Then "found.total" is "4"
    And "found.accounts[1].username" is "camille"
    And "found.accounts[1].clash" is "true"
    When I look up "eclegv" in the LDAP directory with my session
    Then "user.dn" is "uid=eclegv,ou=people,dc=example,dc=com"
    And "user.admin" is "true"
    And "user.role" is "admin"
    When I look up "zoe" in the LDAP directory with my session
    Then "user.role" is null
    And "user.refused" is "neither the users filter nor the admins filter finds it."
    When I run the LDAP test "connection" with the service account's password "wrong" with my session
    Then "connected" is "false"
    And "error" is "the service account's DN or password was refused."
    When I request "/api/admin/settings/ldap" with my session
    Then "clashes" lists "admin, camille"

  Scenario: A local account can't take a name the directory has
    Given the LDAP directory is on
    And I logged in as the admin
    When I send POST "/api/admin/accounts" with my session and '{"username": "Marie", "password": "local-marie", "role": "user"}'
    Then the answer is a problem 409
    And the problem says "The directory has “Marie”: a local account can't have its name too."

  Scenario: A user can't see nor change the LDAP settings
    Given there is the user "lea" with the password "lea-password"
    When I log in as "lea" with "lea-password"
    And I request "/api/admin/settings/ldap" with my session
    Then the answer is a problem 403
