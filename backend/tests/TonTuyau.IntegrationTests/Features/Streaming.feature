@elasticsearch @api
Feature: A video played in parts
  The player fetches a few seconds ahead at a time (2.0, C3, question 9): an HLS playlist of the video's parts, keyframe
  to keyframe, each copied out of the file by ffmpeg when it's asked for, nothing written to disk. The keyframe times
  are read from the file's own index at its first play. A file that can't be played in parts is a 409: the player
  plays it whole. The test video lasts 10 s with a keyframe every 2 s: two parts, 0 to 6 s and 6 to 10 s.

  Background:
    Given the channels:
      | key      | name      |
      | tomcooks | Tom Cooks |
    And the videos:
      | key    | title       | channel  | file | duration |
      | crepes | Les crêpes  | tomcooks | mkv  | 10       |
      | tatin  | La tatin    | tomcooks | mp4  | 10       |
      | broken | Pas un film | tomcooks | mkv  | 10       |
    And the file of the video "crepes" is the test video "vp9.mkv"
    And the file of the video "tatin" is the test video "h264-end.mp4"
    And the file of the video "broken" is on the media share with 1000 bytes

  Scenario Outline: The playlist lists the parts after the init segment, the viewer's own
    When I request "/media/{video:<video>}/hls/viewing-0123456789abcdef/index.m3u8"
    Then the response status is 200
    And the header "Content-Type" is "application/vnd.apple.mpegurl"
    And the header "Cache-Control" is "no-cache, private"
    And the body contains '#EXT-X-MAP:URI="init.mp4"'
    And the body contains "#EXTINF:6.000,"
    And the body contains "0.m4s"
    And the body contains "#EXTINF:4.000,"
    And the body contains "1.m4s"
    And the body doesn't contain "2.m4s"

    Examples:
      | video  |
      | crepes |
      | tatin  |

  Scenario: The init segment, then each part, copied out of the file
    When I request "/media/{video:crepes}/hls/viewing-0123456789abcdef/init.mp4"
    Then the response status is 200
    And the header "Content-Type" is "video/mp4"
    And the body is MP4 starting with the box "ftyp"
    When I request "/media/{video:crepes}/hls/viewing-0123456789abcdef/1.m4s"
    Then the response status is 200
    And the body is MP4 starting with the box "moof"
    When I request "/media/{video:crepes}/hls/viewing-0123456789abcdef/0.m4s"
    Then the response status is 200
    And the body is MP4 starting with the box "moof"
    When I request "/media/{video:crepes}/hls/viewing-0123456789abcdef/2.m4s"
    Then the answer is a problem 404

  Scenario: A file without an index to play in parts is a 409: the player plays it whole
    When I request "/media/{video:broken}/hls/viewing-0123456789abcdef/index.m3u8"
    Then the answer is a problem 409

  Scenario: A session id that isn't one is refused
    When I request "/media/{video:crepes}/hls/x/index.m3u8"
    Then the answer is a problem 400 about "session"

  Scenario: The viewer leaving stops its viewing
    When I send DELETE "/media/{video:crepes}/hls/viewing-0123456789abcdef"
    Then the response status is 204
