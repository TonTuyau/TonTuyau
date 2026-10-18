Feature: The library in PostgreSQL
  TonTuyau 2.0 keeps its library in PostgreSQL, the source of truth Elasticsearch indexes: a row per video and per
  channel, one column each. What TubeArchivist brings only adds what TonTuyau doesn't have, "not checked" on
  YouTube: a video or channel TonTuyau has is left as it is, so what a refresh read is never overwritten.

  Scenario: A video is read back as it was added, with its channel's name, not checked on YouTube
    When the videos "aaaaaaaaaaa" are added to the library
    Then the library's video "aaaaaaaaaaa" is the one added, tracks, tags and duration included

  Scenario: A video's channel is in the library first
    When the videos "aaaaaaaaaaa" of a channel the library doesn't have are added to the library
    Then the library refuses them, and holds 0 videos

  Scenario: Adding only adds what the library doesn't have
    When the videos "aaaaaaaaaaa, bbbbbbbbbbb" are added to the library
    Then the library counts 2 added and 0 already there
    When the library's video "aaaaaaaaaaa" gets the title "Nouveau titre" and the videos "aaaaaaaaaaa, bbbbbbbbbbb, ccccccccccc" are added to the library
    Then the library counts 1 added and 2 already there
    And the library's video "aaaaaaaaaaa" has the title "Vidéo aaaaaaaaaaa"
    And the library holds 3 videos

  Scenario: Videos of one category share its row
    When the videos "aaaaaaaaaaa, bbbbbbbbbbb" in the category "Science" are added to the library
    And the videos "ccccccccccc" in the category "Education" are added to the library
    And the videos "ddddddddddd" in the category "Science" are added to the library
    Then the library holds the categories "Education, Science"
    And the library's video "aaaaaaaaaaa" is in the categories "Science"
    And the library's video "ccccccccccc" is in the categories "Education"

  Scenario: A video without a category
    When the videos "aaaaaaaaaaa" in the category "" are added to the library
    Then the library's video "aaaaaaaaaaa" is in the categories ""
    And the library holds the categories ""

  Scenario: TubeArchivist's own "inactive" flag isn't kept: the video is not checked
    When the video "aaaaaaaaaaa", inactive in TubeArchivist, is added to the library
    Then the library's video "aaaaaaaaaaa" is not checked on YouTube, and active

  Scenario: What a refresh wrote stays when TubeArchivist brings the video again
    Given the videos "aaaaaaaaaaa" are added to the library
    And TonTuyau refreshed the library's video "aaaaaaaaaaa" on YouTube with the title "Le vrai titre"
    When the library's video "aaaaaaaaaaa" gets the title "Titre de TubeArchivist" and the videos "aaaaaaaaaaa" are added to the library
    Then the library counts 0 added and 1 already there
    And the library's video "aaaaaaaaaaa" has the title "Le vrai titre"
    And the library's video "aaaaaaaaaaa" is on YouTube, refreshed

  Scenario: The file flag and the thumbnail's versions are recorded
    Given the videos "aaaaaaaaaaa, bbbbbbbbbbb" are added to the library
    When the library flags the files of "aaaaaaaaaaa" as missing
    And the library records the thumbnail of "aaaaaaaaaaa" as 1280 px wide with the versions "360, 720"
    Then the library's video "aaaaaaaaaaa" has its file flagged as missing
    And the library's video "bbbbbbbbbbb" has its file
    And the library's video "aaaaaaaaaaa" has a thumbnail 1280 px wide with the versions "360, 720"
    When the library clears the file flag of "aaaaaaaaaaa"
    Then the library's video "aaaaaaaaaaa" has its file

  Scenario: A channel is read back as it was added, with its pictures' versions
    When the channels "UCaaaaaaaaaaaaaaaaaaaaaa" are added to the library
    And the library records the thumb of the channel "UCaaaaaaaaaaaaaaaaaaaaaa" as 900 px wide with the versions "88, 176"
    And the library records the banner of the channel "UCaaaaaaaaaaaaaaaaaaaaaa" as 2560 px wide with the versions "1280"
    Then the library's channel "UCaaaaaaaaaaaaaaaaaaaaaa" is the one added, tabs and tags included
    And the library's channel "UCaaaaaaaaaaaaaaaaaaaaaa" has a thumb 900 px wide with the versions "88, 176"
    And the library's channel "UCaaaaaaaaaaaaaaaaaaaaaa" has a banner 2560 px wide with the versions "1280"

  Scenario: Adding channels only adds what the library doesn't have
    Given the channels "UCaaaaaaaaaaaaaaaaaaaaaa" are added to the library
    When the channel "UCaaaaaaaaaaaaaaaaaaaaaa" gets the name "Autre nom" and the channels "UCaaaaaaaaaaaaaaaaaaaaaa, UCbbbbbbbbbbbbbbbbbbbbbb" are added to the library
    Then the library counts 1 added and 1 already there
    And the library's channel "UCaaaaaaaaaaaaaaaaaaaaaa" has the name "Chaîne UCaaaaaaaaaaaaaaaaaaaaaa"
    And the library holds 2 channels

  Scenario: Each video and each channel gets its state on YouTube, in a table of its own
    When the videos "aaaaaaaaaaa, bbbbbbbbbbb" are added to the library
    And the channels "UCaaaaaaaaaaaaaaaaaaaaaa" are added to the library
    Then the library keeps 2 video states and 1 channel state, all not checked
