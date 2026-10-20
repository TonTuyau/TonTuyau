@elasticsearch @sync @api @youtube
Feature: Subscribing to channels
  TonTuyau subscribes to channels itself (2.0, B4): the YouTube syncs (B5) will download their videos, from the tabs
  ticked (videos, shorts, streams). The import takes TubeArchivist's subscriptions with the channels it adds, the three
  tabs ticked; a channel TonTuyau had keeps its own, so one unsubscribed isn't subscribed again by the next import.
  "Add a channel" (the Tasks page, the command line) reads a channel on YouTube by its id or @handle, adds it when the
  library doesn't have it, and subscribes to it: a run through the queue, on hold when YouTube refuses. A channel's
  admin page shows its subscription, changes its tabs, subscribes or unsubscribes. yt-dlp is a fake here.

  Background:
    Given TubeArchivist has the channel "alpha" with 2 videos
    And TubeArchivist has the channel "beta" with 1 videos
    And TubeArchivist is subscribed to the channel "alpha"
    And the backend is ready
    And a first import has run
    And I logged in as the admin

  # ---------------------------------------------------------------- the import

  Scenario: The import takes TubeArchivist's subscriptions with the channels it adds
    Then TonTuyau is subscribed to the channel "alpha": "videos,shorts,streams", from tubeArchivist
    And TonTuyau isn't subscribed to the channel "beta"

  Scenario: A channel unsubscribed in TonTuyau isn't subscribed again by the next import
    When I unsubscribe from the channel "alpha" with my session
    Then the response status is 204
    When an import runs
    Then the run succeeded
    And the run counted 0 subscriptions
    And TonTuyau isn't subscribed to the channel "alpha"

  # ---------------------------------------------------------------- a channel's admin page

  Scenario: A channel's admin page shows its subscription, and changes its tabs
    When I request the admin page of the channel "alpha" with my session
    Then the response status is 200
    And "subscription.tabs.shorts" is "true"
    And "subscription.origin" is "tubeArchivist"
    When I subscribe to the channel "alpha" with '{"videos":true,"shorts":false,"streams":false}' with my session
    Then the response status is 200
    And "tabs.videos" is "true"
    And "tabs.shorts" is "false"
    And "origin" is "tubeArchivist"
    And TonTuyau is subscribed to the channel "alpha": "videos", from tubeArchivist

  Scenario: A channel of the library is subscribed to from its admin page
    When I request the admin page of the channel "beta" with my session
    Then the response status is 200
    And "subscription" is null
    When I subscribe to the channel "beta" with '{"videos":true,"shorts":true,"streams":false}' with my session
    Then the response status is 200
    And "by" is "admin"
    And "origin" is "tonTuyau"
    And TonTuyau is subscribed to the channel "beta": "videos,shorts", from tonTuyau

  Scenario Outline: A subscription without a tab, or not saying each, is refused
    When I subscribe to the channel "beta" with '<body>' with my session
    Then the response status is 400

    Examples:
      | body                                                |
      | {"videos":false,"shorts":false,"streams":false} |
      | {"videos":true}                                   |

  Scenario: An unknown channel has no subscription to set
    When I subscribe to the channel "nobody" with '{"videos":true,"shorts":true,"streams":true}' with my session
    Then the response status is 404
    When I unsubscribe from the channel "nobody" with my session
    Then the response status is 404

  # ---------------------------------------------------------------- "Add a channel"

  Scenario: A new channel pasted as its @handle link is added and subscribed to, through the queue
    Given YouTube has the channel "omega": the name "Omega", 500 subscribers
    And YouTube has the channel "omega" at the handle "@omega"
    When I add the channel "https://www.youtube.com/@omega/videos" with the tabs "videos,streams" with my session
    Then the response status is 202
    And "task" is "addChannel"
    And "target" is "@omega"
    And "tabs.shorts" is "false"
    And the latest run ends succeeded within 30 seconds
    And the latest run's "target" is "UComega_________________" within 5 seconds
    And the latest run's "counts.subscribed" is "1" within 5 seconds
    And the latest run's notes mention ": videos and streams. Its videos come with the YouTube syncs."
    And TonTuyau is subscribed to the channel "omega": "videos,streams", from tonTuyau
    When I request the admin page of the channel "omega" with my session
    Then the response status is 200
    And "channel.name" is "Omega"
    And "subscription.by" is "admin"

  Scenario: A channel of the library is subscribed to, the three tabs by default
    Given YouTube has the channel "beta": the name "Beta", 20 subscribers
    When I add the channel "beta" with my session
    Then the response status is 202
    And "tabs.videos" is "true"
    And the latest run ends succeeded within 30 seconds
    And TonTuyau is subscribed to the channel "beta": "videos,shorts,streams", from tonTuyau

  Scenario: A channel TonTuyau is subscribed to already is refused: its page changes its tabs
    When I add the channel "alpha" with my session
    Then the response status is 409
    And "detail" mentions "its admin page changes its tabs"

  Scenario Outline: What isn't a channel, or isn't tabs, is refused
    When I add the channel "<target>" with the tabs "<tabs>" with my session
    Then the response status is 400

    Examples:
      | target                                   | tabs    |
      | https://www.youtube.com/watch?v=abc      | videos  |
      | https://vimeo.com/@omega                 | videos  |
      | @omega                                   | podcast |

  Scenario: A channel YouTube says is gone isn't added
    Given YouTube says of the channel "omega": "ERROR: [youtube:tab] This channel does not exist."
    When I add the channel "omega" with my session
    Then the response status is 202
    And the latest run ends failed within 30 seconds
    When I request the latest run's "" with my session
    Then "error" mentions "Not added"
    And TonTuyau isn't subscribed to the channel "omega"

  Scenario: YouTube refusing puts "Add a channel" on hold
    Given YouTube says of the channel "omega": "ERROR: [youtube:tab] Sign in to confirm you’re not a bot"
    When I add the channel "omega" with my session
    Then the latest run is onHold within 30 seconds
    And TonTuyau isn't subscribed to the channel "omega"

  Scenario: On the command line, a channel is added with the tabs named
    Given YouTube has the channel "omega": the name "Omega", 500 subscribers
    And YouTube has the channel "omega" at the handle "@omega"
    When I run "tontuyau youtube add-channel @omega shorts"
    Then the command exits with code 0
    And the output mentions ": shorts only."
    And TonTuyau is subscribed to the channel "omega": "shorts", from tonTuyau
