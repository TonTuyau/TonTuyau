@elasticsearch @sync @api
Feature: What TonTuyau knows of YouTube, over what TubeArchivist says
  TonTuyau keeps its own facts about videos and channels in PostgreSQL (1.5): their state on YouTube (not checked, on
  YouTube, retry later, gone) and, once it refreshed them, their metadata. The sync lays them over TubeArchivist's data
  in every write, an index rebuild too. TubeArchivist's "inactive" flag alone means retry later, never gone: visitors
  are told a video is gone ("active": false) only when TonTuyau confirmed it. Once TonTuyau refreshed a video, the
  TubeArchivist sync no longer changes its metadata.

  Background:
    Given TubeArchivist has the channel "alpha" with 3 videos
    And the backend is ready

  Scenario: TubeArchivist's "inactive" alone is retry later, not gone: no badge
    Given the video "alpha-01" is unavailable in TubeArchivist
    And a first sync has run
    Then the stored YouTube state of the video "alpha-01" is "retry later"
    And the stored YouTube state of the video "alpha-02" is "not checked"
    When I request the video "alpha-01" of TubeArchivist
    Then "active" is "true"

  Scenario: A video TonTuyau found gone is flagged, whatever TubeArchivist says
    Given TonTuyau found the video "alpha-02" gone from YouTube
    And a first sync has run
    Then the stored YouTube state of the video "alpha-02" is "gone"
    When I request the video "alpha-02" of TubeArchivist
    Then "active" is "false"

  Scenario: A channel TonTuyau found gone is flagged
    Given TonTuyau found the channel "alpha" gone from YouTube
    And a first sync has run
    When I request the channel "alpha" of TubeArchivist
    Then "active" is "false"

  Scenario: Once TonTuyau refreshed a video, the TubeArchivist sync no longer changes its metadata
    Given TonTuyau refreshed the video "alpha-01": the title "Le vrai titre", 999 views
    And a first sync has run
    And the video "alpha-01" gets the title "Autre titre"
    And the video "alpha-02" gets the title "Nouveau titre"
    When a full sync runs
    And I request the video "alpha-01" of TubeArchivist
    Then "title" is "Le vrai titre"
    And "views" is "999"
    When I request the video "alpha-02" of TubeArchivist
    Then "title" is "Nouveau titre"

  Scenario: TonTuyau's facts survive an index rebuild
    Given TonTuyau found the video "alpha-02" gone from YouTube
    And a first sync has run
    When I run "tontuyau index rebuild"
    Then the command exits with code 0
    And the stored YouTube state of the video "alpha-02" is "gone"
