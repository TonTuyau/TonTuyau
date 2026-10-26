@elasticsearch @sync @api @youtube
Feature: Run now, and the queue beside a run on hold
  2.3.0: a run on hold no longer always holds the queue. Held by hand (or for disk space), it lets the queue go on;
  its Resume now, while another run goes on, puts it back in the queue, first: it goes on once that one ends. Run now
  on a queued run puts it first; the refresh going on is held to let it go, and goes on right after it. Held because
  YouTube refused, the queue still waits (the next runs would be refused too), and the Tasks page says which run it
  waits for; Run now still starts a queued run then. The clock is frozen here: a pause between videos waits until
  the clock moves on.

  Background:
    Given the clock shows "2026-10-26T08:00:00Z"
    And TubeArchivist has the channel "alpha" with 3 videos
    And TubeArchivist has the channel "beta" with 3 videos
    And TubeArchivist has the channel "gamma" with 1 videos
    And the backend is ready
    And a first import has run
    And I logged in as the admin
    And YouTube has the channel "alpha": the name "Alpha officiel", 5000 subscribers
    And YouTube has the channel "beta": the name "Beta officiel", 300 subscribers
    And YouTube has the video "alpha-01": the title "Un", 1 views
    And YouTube has the video "alpha-02": the title "Deux", 2 views
    And YouTube has the video "alpha-03": the title "Trois", 3 views
    And YouTube has the video "beta-01": the title "Bêta un", 1 views
    And YouTube has the video "beta-02": the title "Bêta deux", 2 views
    And YouTube has the video "beta-03": the title "Bêta trois", 3 views
    # The frozen clock never ends the pause after a channel's first video (an hour, give or take half): its run waits
    # there.
    And the pause between videos is 3600 seconds
    And the clock moves on 1 minutes

  Scenario: Held by hand, a run lets the queue go on
    When I refresh the channel "alpha" and its videos with my session
    Then the latest run's "counts.videosRead" is "1" within 20 seconds
    When I refresh the video "beta-01" with my session
    Then "state" is "queued"
    When I put the run of the channel "alpha" on hold with my session
    Then the response status is 202
    And the run of the video "beta-01" is succeeded within 20 seconds
    And the run of the channel "alpha" is onHold within 2 seconds
    When I request "/api/admin/tasks" with my session
    Then "onHold" has 1 items
    And "onHold[0].heldFor" is "hand"
    And "queueWaitsFor" is null
    And "queue" has 0 items
    When I request the run of the channel "alpha" with my session
    Then "notes[1]" mentions "the queue goes on meanwhile."
    Given there's no pause between videos
    When I resume the run of the channel "alpha" with my session
    Then the response status is 202
    And the run of the channel "alpha" is succeeded within 20 seconds
    And the run of the channel "alpha" has read 3 videos

  Scenario: Resume now while another run goes on: first in the queue, it goes on once that one ends
    When I refresh the channel "alpha" and its videos with my session
    Then the latest run's "counts.videosRead" is "1" within 20 seconds
    When I put the run of the channel "alpha" on hold with my session
    Then the run of the channel "alpha" is onHold within 20 seconds
    Given TubeArchivist downloads 15 new videos in "beta"
    And TubeArchivist holds its answer to page 2 until told
    When I send POST "/api/admin/tasks/import/runs" with my session
    Then "state" is "running"
    When I refresh the video "alpha-01" with my session
    Then "state" is "queued"
    When I resume the run of the channel "alpha" with my session
    Then the response status is 202
    And "state" is "queued"
    And "position" is "1"
    And "firstAt" is not null
    And "heldFor" is null
    And "left" is "2"
    And "notes[2]" mentions "Resume now by admin while another run went on: first in the queue"
    When I request "/api/admin/tasks" with my session
    Then "onHold" has 0 items
    And "queue" has 2 items
    And "queue[0].target" is "UCalpha_________________"
    And "queue[1].target" is "alpha-01___"
    Given there's no pause between videos
    When TubeArchivist answers
    Then the run of the channel "alpha" is succeeded within 40 seconds
    And the run of the video "alpha-01" is succeeded within 20 seconds
    And the run of the channel "alpha" has read 3 videos
    And the run of the channel "alpha"'s notes mention "Resumed by itself, its turn come in the queue."

  Scenario: Run now holds the refresh going on, runs the one asked, then the refresh goes on right after it
    When I refresh the channel "alpha" and its videos with my session
    Then the latest run's "counts.videosRead" is "1" within 20 seconds
    When I refresh the video "beta-01" with my session
    # Asked for a minute later: the queue's order is when each was asked for.
    And the clock moves on 1 minutes
    And I refresh the video "beta-02" with my session
    Then "position" is "2"
    When I run now the run of the video "beta-02" with my session
    Then the response status is 202
    And "firstAt" is not null
    And "notes[0]" is "Run now by admin: first in the queue."
    And the run of the video "beta-02" is succeeded within 20 seconds
    # Back from the queue, it stops again at the pause after its next video: the queue waits behind it.
    And the run of the channel "alpha" is running within 20 seconds
    And the run of the video "beta-01" is queued within 1 seconds
    And the run of the channel "alpha"'s notes mention "to run another task first: it goes on right after it."
    When I request "/api/admin/tasks" with my session
    Then "queue" has 1 items
    And "queue[0].target" is "beta-01____"
    Given there's no pause between videos
    When the clock moves on 100 minutes
    Then the run of the channel "alpha" is succeeded within 20 seconds
    And the run of the video "beta-01" is succeeded within 20 seconds
    And the run of the channel "alpha" has read 3 videos
    When I run now the run of the video "beta-01" with my session
    Then the response status is 409
    And "detail" mentions "doesn't wait in the queue"

  Scenario: Held because YouTube refused, the queue waits for it and says so; Run now still starts a run
    Given there's no pause between videos
    And YouTube says of the video "gamma-01": "ERROR: [youtube] gamma-01___: Sign in to confirm you’re not a bot"
    When I refresh the video "gamma-01" with my session
    Then the run of the video "gamma-01" is onHold within 20 seconds
    When I refresh the video "beta-01" with my session
    Then "state" is "queued"
    And the run of the video "beta-01" stays queued for 3 seconds
    When I request "/api/admin/tasks" with my session
    Then "onHold[0].heldFor" is "youTube"
    And "queueWaitsFor" is the id of the run of the video "gamma-01"
    When I run now the run of the video "beta-01" with my session
    Then the response status is 202
    And the run of the video "beta-01" is succeeded within 20 seconds
    And the run of the video "gamma-01" is onHold within 1 seconds
