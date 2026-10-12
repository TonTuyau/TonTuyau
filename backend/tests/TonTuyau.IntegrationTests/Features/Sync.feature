@elasticsearch @sync
Feature: Sync from TubeArchivist
  TonTuyau mirrors TubeArchivist's library (docs/SYNC_DESIGN.md, v5). Incremental runs read the newest
  downloads until they're back in known territory; full runs walk every channel, refresh everything,
  and delete only what TubeArchivist confirms it no longer has.
  TubeArchivist here is a fake with the real one's quirks, at a small scale (pages of 10).

  Background:
    Given TubeArchivist has the channel "alpha" with 25 videos
    And TubeArchivist has the channel "beta" with 3 videos
    And the backend is ready

  # ---------------------------------------------------------------- first run, incremental

  Scenario: The first sync imports everything, as a full run
    When an incremental sync runs
    Then the run succeeded
    And the run was escalated to a full run because "no watermark yet"
    And TonTuyau has 28 videos and 2 channels
    And the watermark is the download date of the newest video
    And the art of every video and channel is copied

  Scenario: Nothing new costs one list page
    Given a first sync has run
    When an incremental sync runs
    Then the run succeeded as an incremental run
    And the run created 0 videos
    And TubeArchivist answered 1 video list request during the run

  Scenario: New videos in two channels are picked up
    Given a first sync has run
    And TubeArchivist downloads 3 new videos in "alpha"
    And TubeArchivist downloads 2 new videos in "beta"
    When an incremental sync runs
    Then the run succeeded as an incremental run
    And the run created 5 videos
    And TonTuyau has 33 videos
    And the watermark is the download date of the newest video

  Scenario: A run records its new videos by type (the dashboard's import history)
    Given a first sync has run
    And TubeArchivist downloads 2 new videos in "alpha"
    And TubeArchivist downloads 1 new short and 2 new streams in "beta"
    When an incremental sync runs
    Then the run created 5 videos: 2 videos, 1 short and 2 streams

  Scenario: Too many missing videos for an incremental run: straight to a full run
    Given TubeArchivist's result window is 30
    And a first sync has run
    And TubeArchivist has the channel "gamma" with 16 videos
    And TubeArchivist has the channel "delta" with 16 videos
    When an incremental sync runs
    Then the run succeeded
    And the run was escalated to a full run because "videos missing"
    And TonTuyau has 60 videos

  Scenario: A gap hidden by deletions hits the window net, then a full run catches up
    Given TubeArchivist's result window is 30
    And a first sync has run
    And TubeArchivist deletes 10 videos of "alpha"
    And TubeArchivist downloads 35 new videos in "beta"
    When an incremental sync runs
    Then the run succeeded as an incremental run
    And the run notes mention "full run requested"
    And the watermark didn't move
    When an incremental sync runs
    Then the run was escalated to a full run because "requested"
    And TonTuyau has 53 videos

  Scenario: A brand-new channel appears with its videos
    Given a first sync has run
    And TubeArchivist has the channel "gamma" with 2 videos
    When an incremental sync runs
    Then TonTuyau has 30 videos and 3 channels
    And the art of channel "gamma" is copied

  Scenario: A video imported with an old download date waits for the full run
    Given a first sync has run
    And TubeArchivist imports the video "old-1" in "beta", downloaded 30 days before the first one
    When an incremental sync runs
    Then TonTuyau doesn't have the video "old-1"
    When a full sync runs
    Then TonTuyau has the video "old-1"

  Scenario: Several videos downloaded in the same second, across a page boundary
    Given a first sync has run
    And TubeArchivist downloads 12 new videos in "alpha", all in the same second
    When an incremental sync runs
    Then TonTuyau has 40 videos

  Scenario: A video downloaded while the sync is paging is not lost
    Given a first sync has run
    And TubeArchivist downloads 15 new videos in "alpha"
    And TubeArchivist downloads the video "late-1" in "beta" right before answering page 2
    When an incremental sync runs
    Then the run succeeded
    And TonTuyau has 43 videos
    When an incremental sync runs
    Then TonTuyau has the video "late-1"

  # ---------------------------------------------------------------- failures

  Scenario: A run that dies halfway leaves no gap
    Given a first sync has run
    And TubeArchivist downloads 15 new videos in "alpha"
    And TubeArchivist fails with 500 on "page=2" 10 times
    When an incremental sync runs
    Then the run failed
    And the watermark didn't move
    When TubeArchivist works again
    And an incremental sync runs
    Then the run succeeded
    And TonTuyau has 43 videos

  Scenario: A TubeArchivist hiccup is retried within the run
    Given a first sync has run
    And TubeArchivist downloads 15 new videos in "alpha"
    And TubeArchivist fails with 502 on "page=2" 1 times
    When an incremental sync runs
    Then the run succeeded
    And TonTuyau has 43 videos

  Scenario: TubeArchivist down: the run fails, TonTuyau keeps what it has
    Given a first sync has run
    And TubeArchivist answers 503 to everything
    When an incremental sync runs
    Then the run failed
    And TonTuyau has 28 videos
    And the watermark didn't move

  Scenario: Art that can't be copied is retried by the next run
    Given TubeArchivist has no thumbnail for "alpha-03"
    And a first sync has run
    Then the art of "alpha-03" is pending
    When TubeArchivist gets the thumbnail of "alpha-03" back
    And an incremental sync runs
    Then the art of "alpha-03" is copied
    And no art is pending

  # ---------------------------------------------------------------- resized art (QA09, contract v1.4)

  Scenario: The sync makes the resized versions of every image and records them in the store
    When an incremental sync runs
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
    When an incremental sync runs
    Then the thumbnail of "alpha-03" has the versions "360"
    And the thumbnail of "alpha-04" has the versions ""
    And the thumbnail of "alpha-05" has the versions ""
    And the run made 61 resized versions
    And the store records the thumbnail of "alpha-03" as 480 px wide with the versions "360"
    And the store records the thumbnail of "alpha-04" as 360 px wide with the versions ""

  Scenario: Art already copied gets its missing versions at the next full run, once
    Given a first sync has run
    And the resized versions are deleted from the art volume
    When a full sync runs
    Then the run made 66 resized versions
    And every thumbnail has its "360, 720" px versions
    When a full sync runs
    Then the run made 0 resized versions
    And the store records the thumbnail of "beta-02" as 800 px wide with the versions "360, 720"

  Scenario: An image that can't be resized is logged and counted, and the run goes on
    Given the thumbnail of "alpha-06" isn't an image in TubeArchivist
    When an incremental sync runs
    Then the run succeeded
    And the run failed to make 2 resized versions
    And the run made 64 resized versions
    And the thumbnail of "alpha-06" has the versions ""
    And the store records no versions for the thumbnail of "alpha-06"
    And the store records the thumbnail of "alpha-07" as 800 px wide with the versions "360, 720"

  Scenario: Art copied by a retry gets its versions too
    Given TubeArchivist has no thumbnail for "alpha-03"
    And a first sync has run
    When TubeArchivist gets the thumbnail of "alpha-03" back
    And an incremental sync runs
    Then the thumbnail of "alpha-03" has the versions "360, 720"
    And the run made 2 resized versions
    And the store records the thumbnail of "alpha-03" as 800 px wide with the versions "360, 720"

  Scenario: A broken document is rejected and reported, the rest is imported
    Given the video "alpha-07" is broken in TubeArchivist
    When an incremental sync runs
    Then the run succeeded
    And the run rejected 1 document
    And the run reports a rejected "TubeArchivist video alpha-07___"
    And TonTuyau has 27 videos

  Scenario: Mostly broken documents: TubeArchivist's format probably changed, the run stops
    Given every video of "alpha" is broken in TubeArchivist
    When an incremental sync runs
    Then the run failed because "format may have changed"

  Scenario: A higher format-guard threshold lets a mostly broken run finish
    # Running share of rejected documents peaks at 25 of 27 (92.6 %) while walking "alpha".
    Given the format guard stops above 95 percent rejected, after 5 documents
    And every video of "alpha" is broken in TubeArchivist
    When an incremental sync runs
    Then the run succeeded
    And the run rejected 25 documents
    And TonTuyau has 3 videos

  Scenario: With no minimum, the guard judges from the first page
    # "aaa" is walked first: 3 channels + its 4 broken videos = 4 rejected out of 7 read (57 %).
    # With the default minimum of 20 documents, this would not stop the run.
    Given the format guard stops above 50 percent rejected, after 0 documents
    And TubeArchivist has the channel "aaa" with 4 videos
    And every video of "aaa" is broken in TubeArchivist
    When a full sync runs
    Then the run failed because "format may have changed"

  Scenario: With the default minimum, a few broken documents at the start don't stop the run
    Given TubeArchivist has the channel "aaa" with 4 videos
    And every video of "aaa" is broken in TubeArchivist
    When a full sync runs
    Then the run succeeded
    And the run rejected 4 documents

  Scenario: A media path pointing to another channel's folder is corrected
    Given the video "beta-02" has its file path in the folder of "alpha"
    When an incremental sync runs
    Then the run corrected 1 media path
    And the media path of "beta-02" is in the folder of "beta"

  # ---------------------------------------------------------------- full runs and deletion

  Scenario: A video deleted in TubeArchivist disappears at the next full run
    Given a first sync has run
    And TubeArchivist deletes the video "alpha-04"
    When a full sync runs
    Then the run deleted 1 item
    And TonTuyau doesn't have the video "alpha-04"
    And its thumbnail is removed from the art volume
    And the deletion list of the run has 1 entry

  Scenario: A channel deleted in TubeArchivist disappears with its videos
    Given a first sync has run
    And TubeArchivist deletes the channel "beta" with its videos
    When a full sync runs
    Then the run deleted 4 items
    And TonTuyau has 25 videos and 1 channel

  Scenario: Changed metadata is refreshed; unchanged documents aren't rewritten
    Given a first sync has run
    And the video "alpha-05" gets the title "Nouveau titre"
    And the video "alpha-06" becomes a stream
    When a full sync runs
    Then the run updated 2 videos and left 26 unchanged
    And TonTuyau shows the video "alpha-05" with the title "Nouveau titre"
    And the video "alpha-06" is a stream

  Scenario: A full run that dies halfway deletes nothing
    Given a first sync has run
    And TubeArchivist deletes the video "alpha-04"
    And TubeArchivist fails with 500 on "channel=UCbeta" 10 times
    When a full sync runs
    Then the run failed
    And TonTuyau has the video "alpha-04"

  Scenario: A channel whose list returns fewer videos than it counts isn't cleaned
    Given a first sync has run
    And TubeArchivist's list of "alpha" counts "alpha-08" but doesn't return it
    And TubeArchivist deletes the video "beta-01"
    When a full sync runs
    Then the run notes mention "nothing is deleted in this channel"
    And TonTuyau has the video "alpha-08"
    And TonTuyau doesn't have the video "beta-01"

  Scenario: A video the walk missed but TubeArchivist still has is kept
    Given a first sync has run
    And TubeArchivist's lists don't show "alpha-09" any more, but TubeArchivist still has it
    When a full sync runs
    Then the run kept 1 item still in TubeArchivist
    And TonTuyau has the video "alpha-09"

  Scenario: A channel with more than the window of one type: imported as far as possible, never cleaned
    Given TubeArchivist's result window is 20
    And TubeArchivist has the channel "huge" with 24 videos
    And a first sync has run
    And TubeArchivist deletes the video "beta-01"
    When a full sync runs
    Then the run succeeded
    And the run notes mention "only the reachable ones were imported"
    And TonTuyau has at least 20 videos of "huge"
    And TonTuyau doesn't have the video "beta-01"

  Scenario: Too many deletions: nothing deleted until the list is applied by hand
    Given the deletion limit is 3
    And a first sync has run
    And TubeArchivist deletes 5 videos of "alpha"
    When a full sync runs
    Then the run deleted 0 items
    And the run's deletions are blocked by the limit
    And the deletion list of the run has 5 entries
    And the run notes mention "5 deletions planned > limit 3: nothing deleted."
    And a run note has a line starting with "Check "
    And the run notes mention ", then run: tontuyau sync apply-deletions run-"
    When the deletions of that run are applied
    Then 5 items were deleted by the apply
    And TonTuyau has 23 videos

  Scenario: Applying an outdated deletion list is refused
    Given the deletion limit is 3
    And a first sync has run
    And TubeArchivist deletes 5 videos of "alpha"
    And a full sync has run
    And another full sync has run
    When the deletions of the first of those runs are applied
    Then it is refused because "isn't the latest full run"

  Scenario: Applying keeps a video that came back in TubeArchivist
    Given the deletion limit is 1
    And a first sync has run
    And TubeArchivist deletes the video "alpha-01"
    And TubeArchivist deletes the video "alpha-02"
    And a full sync has run
    And TubeArchivist gets the video "alpha-02" back
    When the deletions of that run are applied
    Then 1 item was deleted by the apply
    And TonTuyau has the video "alpha-02"

  # ---------------------------------------------------------------- media share

  Scenario: Videos whose file is missing on the share are flagged
    Given the media share has every video's file except "alpha-10"
    When an incremental sync runs
    Then the run counted 1 missing media file
    And the video "alpha-10" is flagged as missing its file
    When the file of "alpha-10" appears on the share
    And a full sync runs
    Then the video "alpha-10" isn't flagged as missing its file

  Scenario: An unmounted share is never read as "every file is gone"
    When an incremental sync runs
    Then the run notes mention "Media check skipped"
    And the run counted 0 missing media files

  # ---------------------------------------------------------------- lock and rebuild

  Scenario: A sync can't start while another one holds the lock
    Given another process holds the sync lock
    When an incremental sync runs
    Then it is refused because "already running"

  Scenario: An index rebuild switches users to a fresh generation at the end
    Given a first sync has run
    When an index rebuild runs
    Then the run succeeded
    And TonTuyau has 28 videos and 2 channels
    And reads and writes of "videos" both use generation 2

  Scenario: A failing rebuild leaves users on the previous generation
    Given a first sync has run
    And TubeArchivist fails with 500 on "channel=UCbeta" 10 times
    When an index rebuild runs
    Then the run failed
    And TonTuyau has 28 videos and 2 channels
    And reads and writes of "videos" both use generation 1
