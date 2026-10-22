@elasticsearch @sync @api @youtube
Feature: Adding to TonTuyau from the browser extension
  The browser extension's "Add to TonTuyau" (2.0, B7), with a browserExtension API key. Its connection test says
  TonTuyau takes it (canAdd). For a YouTube video or channel, it asks where it stands (absent, fileMissing, waiting,
  inTonTuyau) and adds it: the same runs as the Tasks page's "Download a video" and "Add a channel", through the queue,
  started by the key (trigger "extension"); a channel gets the YouTube page's default tabs. Adding one already waiting
  gives its run, not a second one. yt-dlp is a fake here.

  Background:
    Given TubeArchivist has the channel "alpha" with 2 videos
    And TubeArchivist is subscribed to the channel "alpha"
    And the backend is ready
    And a first import has run
    And the API key "Firefox" exists
    And there's no pause between videos

  Scenario: The connection test says TonTuyau takes "Add to TonTuyau"
    When I send GET "/api/extension/connection" with the API key "Firefox"
    Then the response status is 200
    And "canAdd" is "true"

  Scenario: A video is added, waits its turn, then is in TonTuyau; adding it again while it waits gives the same run
    Given YouTube has the video "omega-01": the title "Par l'extension", 5 views
    And another process holds the run lock
    When I send GET "/api/extension/videos/omega-01___" with the API key "Firefox"
    Then the response status is 200
    And "state" is "absent"
    And "runId" is null
    When I send POST "/api/extension/videos/omega-01___" with the API key "Firefox"
    Then the response status is 202
    And "state" is "waiting"
    And "runId" is the latest run's id
    When I send POST "/api/extension/videos/omega-01___" with the API key "Firefox"
    Then the response status is 202
    And "runId" is the latest run's id
    And the history has 1 run of "downloadVideo" within 1 seconds
    When I send GET "/api/extension/videos/omega-01___" with the API key "Firefox"
    Then "state" is "waiting"
    Given the other process lets go of the run lock in 1 seconds
    Then the latest run ends succeeded within 60 seconds
    And the latest run's "trigger" is "extension" within 5 seconds
    And the latest run's "startedBy" is "Firefox (API key)" within 5 seconds
    And the media folder has the file of the video "omega-01" with 4096 bytes
    When I send GET "/api/extension/videos/omega-01___" with the API key "Firefox"
    Then "state" is "inTonTuyau"
    When I send POST "/api/extension/videos/omega-01___" with the API key "Firefox"
    Then the response status is 409

  Scenario: A video of the library without its file can be added: its file is downloaded
    Given the file of the video "alpha-02" was found missing
    When I send GET "/api/extension/videos/alpha-02___" with the API key "Firefox"
    Then "state" is "fileMissing"
    When I send GET "/api/extension/videos/alpha-01___" with the API key "Firefox"
    Then "state" is "inTonTuyau"

  Scenario: A channel is added with the YouTube page's default tabs, then is in TonTuyau
    Given YouTube has the channel "omega": the name "Omega", 500 subscribers
    And I logged in as the admin
    When I send the YouTube settings '{"comments": "off", "defaultTabs": {"videos": true, "shorts": false, "streams": true}}' with my session
    And I send GET "/api/extension/channels/UComega_________________" with the API key "Firefox"
    Then "state" is "absent"
    When I send POST "/api/extension/channels/UComega_________________" with the API key "Firefox"
    Then the response status is 202
    And "state" is "waiting"
    And the latest run ends succeeded within 30 seconds
    And TonTuyau is subscribed to the channel "omega": "videos,streams", from tonTuyau
    When I send GET "/api/extension/channels/UComega_________________" with the API key "Firefox"
    Then "state" is "inTonTuyau"
    When I send POST "/api/extension/channels/UComega_________________" with the API key "Firefox"
    Then the response status is 409
    When I send GET "/api/extension/channels/UCalpha_________________" with the API key "Firefox"
    Then "state" is "inTonTuyau"

  Scenario Outline: What isn't a video or channel id is refused
    When I send <method> "<path>" with the API key "Firefox"
    Then the response status is 400

    Examples:
      | method | path                                        |
      | GET    | /api/extension/videos/nope                  |
      | POST   | /api/extension/videos/UCalpha_________________ |
      | GET    | /api/extension/channels/alpha-01___         |
      | POST   | /api/extension/channels/@omega              |
