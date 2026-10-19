@elasticsearch @sync @api
Feature: What TonTuyau knows of YouTube, over what TubeArchivist says
  TonTuyau keeps what it knows of videos and channels in PostgreSQL (1.5): their state on YouTube (not checked, on
  YouTube, retry later, gone) and, once it refreshed them, their metadata. Since 2.0 they're the library's rows: an
  import only adds what TonTuyau doesn't have, "not checked" (TubeArchivist's own "inactive" flag isn't kept), and
  never changes a video TonTuyau has already. Visitors are told a video is gone ("active": false) only when TonTuyau
  confirmed it.

  Background:
    Given TubeArchivist has the channel "alpha" with 3 videos
    And the backend is ready

  Scenario: TubeArchivist's "inactive" isn't kept: the video is not checked, no badge
    Given the video "alpha-01" is unavailable in TubeArchivist
    And a first import has run
    Then the stored YouTube state of the video "alpha-01" is "not checked"
    And the stored YouTube state of the video "alpha-02" is "not checked"
    When I request the video "alpha-01" of TubeArchivist
    Then "active" is "true"

  Scenario: A video TonTuyau found gone is flagged, whatever TubeArchivist says
    Given a first import has run
    And TonTuyau found the video "alpha-02" gone from YouTube
    When an import runs
    Then the stored YouTube state of the video "alpha-02" is "gone"
    When I request the video "alpha-02" of TubeArchivist
    Then "active" is "false"

  Scenario: A channel TonTuyau found gone is flagged
    Given a first import has run
    And TonTuyau found the channel "alpha" gone from YouTube
    When an import runs
    And I request the channel "alpha" of TubeArchivist
    Then "active" is "false"

  Scenario: What TonTuyau refreshed is shown, and a later import changes nothing of a video TonTuyau has
    Given a first import has run
    And TonTuyau refreshed the video "alpha-01": the title "Le vrai titre", 999 views
    And the video "alpha-01" gets the title "Autre titre"
    And the video "alpha-02" gets the title "Nouveau titre"
    When an import runs
    And I request the video "alpha-01" of TubeArchivist
    Then "title" is "Le vrai titre"
    And "views" is "999"
    When I request the video "alpha-02" of TubeArchivist
    Then "title" is "Video alpha-02"

  Scenario: TonTuyau's facts survive an index rebuild
    Given a first import has run
    And TonTuyau found the video "alpha-02" gone from YouTube
    When I run "tontuyau index rebuild"
    Then the command exits with code 0
    And the stored YouTube state of the video "alpha-02" is "gone"
