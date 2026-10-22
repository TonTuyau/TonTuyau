@elasticsearch @sync
Feature: Importing TubeArchivist's library
  TonTuyau 2.0 imports TubeArchivist's library: a run that walks every channel and its videos, and adds what TonTuyau
  doesn't have. What TonTuyau has already is left as it is (its own data, refreshed or not), and nothing is ever
  deleted because TubeArchivist no longer has it: once imported, TubeArchivist can be shut down. The import runs at
  startup (when TubeArchivist's settings are there), from the Tasks page and from the command line.
  TubeArchivist here is a fake with the real one's quirks, at a small scale (pages of 10).

  Background:
    Given TubeArchivist has the channel "alpha" with 25 videos
    And TubeArchivist has the channel "beta" with 3 videos
    And the backend is ready

  # ---------------------------------------------------------------- what an import adds

  Scenario: The first import brings everything, art included
    When an import runs
    Then the run succeeded
    And the run created 28 videos
    And TonTuyau has 28 videos and 2 channels
    And the art of every video and channel is copied

  Scenario: A later import adds the new videos, in every channel
    Given a first import has run
    And TubeArchivist downloads 3 new videos in "alpha"
    And TubeArchivist downloads 2 new videos in "beta"
    When an import runs
    Then the run succeeded
    And the run created 5 videos
    And the run updated 0 videos and left 28 unchanged
    And TonTuyau has 33 videos

  Scenario: An import records its new videos by type (the dashboard's import history)
    Given a first import has run
    And TubeArchivist downloads 2 new videos in "alpha"
    And TubeArchivist downloads 1 new short and 2 new streams in "beta"
    When an import runs
    Then the run created 5 videos: 2 videos, 1 short and 2 streams

  Scenario: A new channel comes with its videos
    Given a first import has run
    And TubeArchivist has the channel "gamma" with 2 videos
    When an import runs
    Then TonTuyau has 30 videos and 3 channels
    And the art of channel "gamma" is copied

  Scenario: What TonTuyau has already is left as it is, even when TubeArchivist changed it
    Given a first import has run
    And the video "alpha-05" gets the title "Nouveau titre"
    And the video "alpha-06" becomes a stream
    When an import runs
    Then the run updated 0 videos and left 28 unchanged
    And TonTuyau shows the video "alpha-05" with the title "Video alpha-05"
    And the video "alpha-06" isn't a stream

  Scenario: Nothing is deleted because TubeArchivist no longer has it
    Given a first import has run
    And TubeArchivist deletes the video "alpha-04"
    And TubeArchivist deletes the channel "beta" with its videos
    When an import runs
    Then the run succeeded
    And TonTuyau has 28 videos and 2 channels
    And TonTuyau has the video "alpha-04"

  Scenario: The backend imports by itself when it starts, TubeArchivist's settings there
    Given the backend imports from TubeArchivist when it starts
    When the backend restarts and is ready
    Then an import started by the backend succeeds, with 28 new videos
    And TonTuyau has 28 videos and 2 channels

  Scenario: Without TubeArchivist's settings, there's nothing to import
    Given TubeArchivist isn't set up
    When an import runs
    Then it is refused because "TubeArchivist isn't set up"

  Scenario: An import its caller cancels ends canceled, not failed, and frees the lock
    Given the caller cancels the import right before TubeArchivist answers page 2
    When an import runs
    Then the run was canceled, not failed
    And the stored run is canceled too
    And the run lock is free

  # ---------------------------------------------------------------- failures

  Scenario: An import that dies halfway is completed by the next one
    Given TubeArchivist fails with 500 on "page=2" 10 times
    When an import runs
    Then the run failed
    When TubeArchivist works again
    And an import runs
    Then the run succeeded
    And TonTuyau has 28 videos

  Scenario: A database error ends the import failed, not still running (alpha.4)
    Given PostgreSQL refuses the videos written after TubeArchivist answers "/api/video/"
    When an import runs
    Then the run failed because "refused by the test"
    And the stored run is failed too
    And the run lock is free

  Scenario: A TubeArchivist hiccup is retried within the run
    Given TubeArchivist fails with 502 on "page=2" 1 times
    When an import runs
    Then the run succeeded
    And TonTuyau has 28 videos

  Scenario: TubeArchivist down: the import fails, TonTuyau keeps what it has
    Given a first import has run
    And TubeArchivist answers 503 to everything
    When an import runs
    Then the run failed
    And TonTuyau has 28 videos

  Scenario: Art that can't be copied is retried by the next import
    Given TubeArchivist has no thumbnail for "alpha-03"
    And a first import has run
    Then the art of "alpha-03" is pending
    When TubeArchivist gets the thumbnail of "alpha-03" back
    And an import runs
    Then the art of "alpha-03" is copied
    And no art is pending

  # ---------------------------------------------------------------- resized art (QA09, contract v1.4)

  Scenario: The import makes the resized versions of every image and records them in the store
    When an import runs
    Then the run succeeded
    And every thumbnail has its "360, 720" px versions
    And every channel picture has its "48, 96, 176, 320" px versions and every banner its "1280" px version
    And the run made 66 resized versions
    And the run failed to make 0 resized versions
    And the store records the thumbnail of "alpha-01" as 800 px wide with the versions "360, 720"
    And the store records the picture and banner versions of every channel

  Scenario: No version is as wide as the original or wider
    Given the thumbnail of "alpha-03" is 480 px wide in TubeArchivist
    And the thumbnail of "alpha-04" is 360 px wide in TubeArchivist
    And the thumbnail of "alpha-05" is 120 px wide in TubeArchivist
    When an import runs
    Then the thumbnail of "alpha-03" has the versions "360"
    And the thumbnail of "alpha-04" has the versions ""
    And the thumbnail of "alpha-05" has the versions ""
    And the run made 61 resized versions
    And the store records the thumbnail of "alpha-03" as 480 px wide with the versions "360"
    And the store records the thumbnail of "alpha-04" as 360 px wide with the versions ""

  Scenario: An image that can't be resized is logged and counted, and the run goes on
    Given the thumbnail of "alpha-06" isn't an image in TubeArchivist
    When an import runs
    Then the run succeeded
    And the run failed to make 2 resized versions
    And the run made 64 resized versions
    And the thumbnail of "alpha-06" has the versions ""
    And the store records no versions for the thumbnail of "alpha-06"
    And the store records the thumbnail of "alpha-07" as 800 px wide with the versions "360, 720"

  Scenario: Art copied by a retry gets its versions too
    Given TubeArchivist has no thumbnail for "alpha-03"
    And a first import has run
    When TubeArchivist gets the thumbnail of "alpha-03" back
    And an import runs
    Then the thumbnail of "alpha-03" has the versions "360, 720"
    And the run made 2 resized versions
    And the store records the thumbnail of "alpha-03" as 800 px wide with the versions "360, 720"

  # ---------------------------------------------------------------- what TubeArchivist sends

  Scenario: A broken document is rejected and reported, the rest is imported
    Given the video "alpha-07" is broken in TubeArchivist
    When an import runs
    Then the run succeeded
    And the run rejected 1 document
    And the run reports a rejected "TubeArchivist video alpha-07___"
    And TonTuyau has 27 videos

  Scenario: Mostly broken documents are rejected all the same, the rest is imported
    Given every video of "alpha" is broken in TubeArchivist
    When an import runs
    Then the run succeeded
    And the run rejected 25 documents
    And TonTuyau has 3 videos

  Scenario: A media path pointing to another channel's folder is corrected
    Given the video "beta-02" has its file path in the folder of "alpha"
    When an import runs
    Then the run corrected 1 media path
    And the media path of "beta-02" is in the folder of "beta"

  Scenario: A channel with more than the window of one type: imported as far as possible
    Given TubeArchivist's result window is 20
    And TubeArchivist has the channel "huge" with 24 videos
    When an import runs
    Then the run succeeded
    And the run notes mention "only the reachable ones were imported"
    And TonTuyau has at least 20 videos of "huge"

  # ---------------------------------------------------------------- media share

  Scenario: Imported videos whose file is missing on the share are flagged
    Given the media share has every video's file except "alpha-10"
    When an import runs
    Then the run counted 1 missing media file
    And the video "alpha-10" is flagged as missing its file
    And the video "alpha-09" isn't flagged as missing its file

  Scenario: An unmounted share is never read as "every file is gone"
    When an import runs
    Then the run notes mention "Media check skipped"
    And the run counted 0 missing media files

  # ---------------------------------------------------------------- lock and rebuild

  Scenario: An import can't start while another run holds the lock
    Given another process holds the run lock
    When an import runs
    Then it is refused because "already going on"

  Scenario: An index rebuild switches users to a fresh generation at the end
    Given a first import has run
    When an index rebuild runs
    Then the run succeeded
    And TonTuyau has 28 videos and 2 channels
    And reads and writes of "videos" both use generation 2

  Scenario: An index rebuild needs nothing from TubeArchivist: it writes the library from PostgreSQL
    Given a first import has run
    And TubeArchivist answers 503 to everything
    When an index rebuild runs
    Then the run succeeded
    And TonTuyau has 28 videos and 2 channels
    And reads and writes of "videos" both use generation 2
