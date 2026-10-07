Feature: Service health
  The backend exposes a health endpoint so Docker and operators can tell it is alive,
  and it refuses to start when its configuration is broken.

  Scenario: A correctly configured backend reports healthy
    Given the backend is configured with valid settings
    When I request "/health"
    Then the response status is 200
    And the response body is "Healthy"
    And the response carries TonTuyau's version in "X-TonTuyau-Version"

  Scenario Outline: The backend refuses to start when a required setting is missing
    Given the backend is configured with valid settings
    But the setting "<setting>" is empty
    When the backend starts
    Then it fails to start with an error about "<setting>"
    And the error does not reveal the TubeArchivist token

    Examples:
      | setting             |
      | TONTUYAU_TA_URL      |
      | TONTUYAU_TA_TOKEN    |
      | TONTUYAU_ES_URL      |
      | TONTUYAU_ES_PASSWORD |

  Scenario: The backend refuses to start with an invalid schedule
    Given the backend is configured with valid settings
    But the setting "TONTUYAU_SYNC_FULL_CRON" is "every saturday"
    When the backend starts
    Then it fails to start with an error about "TONTUYAU_SYNC_FULL_CRON"
