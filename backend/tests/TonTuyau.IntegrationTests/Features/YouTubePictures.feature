@elasticsearch @sync @api @youtube
Feature: YouTube's pictures, into TonTuyau's art volume
  A refresh (1.5) downloads the video's largest thumbnail, the channel's avatar and banner (uncropped), from YouTube's
  picture hosts only, and stores them in TonTuyau's art volume in the place of TubeArchivist's copies, with their resized
  versions. The format is read from the picture itself. A picture that can't be had changes nothing: the one there stays.
  YouTube's picture hosts are a fake here.

  Background:
    Given YouTube's pictures can be downloaded
    And TubeArchivist has the channel "alpha" with 2 videos
    And the backend is ready
    And a first sync has run
    And I logged in as the admin

  Scenario: A video's largest thumbnail replaces TubeArchivist's copy, resized
    Given YouTube has the video "alpha-01": the title "Le vrai titre", 1234 views
    When I refresh the video "alpha-01" with my session
    Then the latest run ends succeeded within 20 seconds
    And YouTube's pictures were asked for "/vi/alpha-01___/maxresdefault.jpg"
    When I request the latest run's "" with my session
    Then "counts.artCopied" is "1"
    When I request the video "alpha-01" of TubeArchivist
    Then "thumbnailUrl" is "/art/videos/alpha-01___"
    And "thumbnails" has 3 items
    When I request "/art/videos/alpha-01___"
    Then the response status is 200
    And the header "Content-Type" is "image/jpeg"

  Scenario: A WebP thumbnail is kept as a WebP (its format read from the picture)
    Given YouTube has the video "alpha-01": the title "En WebP", 10 views
    When I refresh the video "alpha-01" with my session
    Then the latest run ends succeeded within 20 seconds
    When I request "/art/videos/alpha-01___"
    Then the response status is 200
    And the header "Content-Type" is "image/webp"

  Scenario: A channel's avatar is stored; a banner that can't be had leaves TubeArchivist's
    Given YouTube has the channel "alpha": the name "Alpha officiel", 5000 subscribers
    When I refresh the channel "alpha" with my session
    Then the latest run ends succeeded within 20 seconds
    And YouTube's pictures were asked for "/avatar=s900, /missing-banner"
    When I request the latest run's "" with my session
    Then "counts.artCopied" is "1"
    And "counts.artFailed" is "1"
    When I request "/art/channels/UCalpha_________________/thumb"
    Then the response status is 200
    And the header "Content-Type" is "image/jpeg"
