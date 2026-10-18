Feature: The library in PostgreSQL
  TonTuyau 2.0 keeps its library in PostgreSQL, the source of truth Elasticsearch indexes: a row per video and per
  channel. What TubeArchivist brings and what TonTuyau read on YouTube live in columns of their own, so a sync never
  overwrites a refresh, and TonTuyau's values win when the video is read. Every write is an upsert by id.

  Scenario: A video is read back as it was written
    When the videos "aaaaaaaaaaa" are written to the library
    Then the library's video "aaaaaaaaaaa" is the one written, tracks, tags and duration included

  Scenario: Writing is idempotent and only rewrites what changed
    When the videos "aaaaaaaaaaa, bbbbbbbbbbb" are written to the library
    Then the library counts 2 created, 0 updated and 0 unchanged
    When the videos "aaaaaaaaaaa, bbbbbbbbbbb" are written to the library
    Then the library counts 0 created, 0 updated and 2 unchanged
    When the library's video "aaaaaaaaaaa" gets the title "Nouveau titre" and the videos "aaaaaaaaaaa, bbbbbbbbbbb" are written to the library
    Then the library counts 0 created, 1 updated and 1 unchanged
    And the library's video "aaaaaaaaaaa" has the title "Nouveau titre"
    And the library holds 2 videos

  Scenario: A run marks every video it saw, changed or not, and a plain write keeps that mark
    Given the videos "aaaaaaaaaaa, bbbbbbbbbbb, ccccccccccc" are written to the library
    When the videos "aaaaaaaaaaa, bbbbbbbbbbb" are written to the library by run "run-2"
    And the library's video "aaaaaaaaaaa" gets the title "Changé" and the videos "aaaaaaaaaaa" are written to the library
    Then the library's videos not seen by run "run-2" are "ccccccccccc"
    When the library marks the videos "ccccccccccc" as seen by run "run-2"
    Then the library's videos not seen by run "run-2" are ""

  Scenario: A content rewrite keeps the file flag and the thumbnail's versions
    Given the videos "aaaaaaaaaaa, bbbbbbbbbbb" are written to the library
    When the library flags the files of "aaaaaaaaaaa" as missing
    And the library records the thumbnail of "aaaaaaaaaaa" as 1280 px wide with the versions "360, 720"
    And the library's video "aaaaaaaaaaa" gets the title "Changé" and the videos "aaaaaaaaaaa" are written to the library
    Then the library's files flagged as missing are "aaaaaaaaaaa"
    And the library's video "aaaaaaaaaaa" has a thumbnail 1280 px wide with the versions "360, 720"
    When the library clears the file flag of "aaaaaaaaaaa"
    Then the library's files flagged as missing are ""

  Scenario: What TonTuyau read on YouTube wins, and a sync's write leaves it
    Given the videos "aaaaaaaaaaa" are written to the library
    And TonTuyau refreshed the library's video "aaaaaaaaaaa" on YouTube with the title "Le vrai titre"
    When the library's video "aaaaaaaaaaa" gets the title "Titre de TubeArchivist" and the videos "aaaaaaaaaaa" are written to the library
    Then the library counts 0 created, 1 updated and 0 unchanged
    And the library's video "aaaaaaaaaaa" has the title "Le vrai titre"
    And the library's video "aaaaaaaaaaa" is on YouTube, refreshed

  Scenario: Deleting removes only what exists
    Given the videos "aaaaaaaaaaa, bbbbbbbbbbb" are written to the library
    When the videos "aaaaaaaaaaa, zzzzzzzzzzz" are deleted from the library
    Then 1 video was deleted from the library
    And the library holds 1 video

  Scenario: The videos of some channels, for the deletion planning
    Given the videos "aaaaaaaaaaa, bbbbbbbbbbb" of the channel "UCaaaaaaaaaaaaaaaaaaaaaa" are written to the library
    And the videos "ccccccccccc" of the channel "UCbbbbbbbbbbbbbbbbbbbbbb" are written to the library
    Then the library's videos of the channel "UCaaaaaaaaaaaaaaaaaaaaaa" are "aaaaaaaaaaa, bbbbbbbbbbb"

  Scenario: A channel is read back as it was written, with its pictures' versions
    When the channels "UCaaaaaaaaaaaaaaaaaaaaaa" are written to the library
    And the library records the thumb of the channel "UCaaaaaaaaaaaaaaaaaaaaaa" as 900 px wide with the versions "88, 176"
    And the library records the banner of the channel "UCaaaaaaaaaaaaaaaaaaaaaa" as 2560 px wide with the versions "1280"
    Then the library's channel "UCaaaaaaaaaaaaaaaaaaaaaa" is the one written, tabs and tags included
    And the library's channel "UCaaaaaaaaaaaaaaaaaaaaaa" has a thumb 900 px wide with the versions "88, 176"
    And the library's channel "UCaaaaaaaaaaaaaaaaaaaaaa" has a banner 2560 px wide with the versions "1280"

  Scenario: Channels a run didn't see
    Given the channels "UCaaaaaaaaaaaaaaaaaaaaaa, UCbbbbbbbbbbbbbbbbbbbbbb" are written to the library
    When the channels "UCaaaaaaaaaaaaaaaaaaaaaa" are written to the library by run "run-2"
    Then the library's channels not seen by run "run-2" are "UCbbbbbbbbbbbbbbbbbbbbbb"
    And the library holds 2 channels
