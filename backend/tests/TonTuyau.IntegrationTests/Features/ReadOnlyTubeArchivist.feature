Feature: TubeArchivist is never modified
  TonTuyau only reads from TubeArchivist. Whatever code asks for, a request that could
  change TubeArchivist's data never leaves TonTuyau.

  Background:
    Given a fake TubeArchivist
    And the backend is configured to use it

  Scenario Outline: Write requests are blocked before they leave TonTuyau
    When TonTuyau's TubeArchivist client is asked to send a <method> request
    Then the request is refused as a read-only violation
    And the fake TubeArchivist received no request

    Examples:
      | method |
      | POST   |
      | PUT    |
      | PATCH  |
      | DELETE |

  Scenario: Reads carry the API token
    When TonTuyau pings TubeArchivist
    Then TubeArchivist's version is "v0.5.12"
    And the fake TubeArchivist received 1 GET request to "/api/ping/"
    And every request carried the API token

  Scenario: A failing TubeArchivist is retried, then reported
    Given the fake TubeArchivist answers 503 to every request
    When TonTuyau pings TubeArchivist
    Then TonTuyau reports that TubeArchivist is failing
    And the fake TubeArchivist received 4 GET requests to "/api/ping/"

  Scenario: Retries stay read-only
    Given the fake TubeArchivist answers 503 to every request
    When TonTuyau pings TubeArchivist
    Then every request the fake TubeArchivist received was a GET

  Scenario: A refused token is reported without revealing it
    Given the fake TubeArchivist refuses the token
    When TonTuyau pings TubeArchivist
    Then TonTuyau reports an authentication failure
    And the error does not reveal the TubeArchivist token
