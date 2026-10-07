@elasticsearch @api
Feature: Media and art files
  Videos are served as they are from the media share, with range requests so players can seek, and can be
  downloaded under their real title. Thumbnails and channel art come from TonTuyau's art store. A file is
  always found from the id (never from a path in the URL), and only if it's really there.

  Background:
    Given the channels:
      | key      | name      |
      | tomcooks | Tom Cooks |
    And the videos:
      | key    | title                                                  | channel  |
      | huiles | HUILES : pourquoi certaines deviennent "dangereuses" ? | tomcooks |
      | creme  | Crème brûlée / version 2                               | tomcooks |
    And the file of the video "huiles" is on the media share with 1000 bytes

  # ---------------------------------------------------------------- playing

  Scenario: The whole file, ready for seeking
    When I request "/media/{video:huiles}"
    Then the response status is 200
    And the header "Content-Type" is "video/mp4"
    And the header "Content-Length" is "1000"
    And the header "Accept-Ranges" is "bytes"
    And the header "ETag" is there
    And the header "Last-Modified" is there
    And the header "Content-Disposition" is missing
    And the body is the whole file of the video "huiles"

  Scenario Outline: A range of the file
    When I request "/media/{video:huiles}" with the header "Range: <range>"
    Then the response status is 206
    And the header "Content-Range" is "bytes <first>-<last>/1000"
    And the header "Content-Length" is "<length>"
    And the body is bytes <first> to <last> of the file

    Examples:
      | range          | first | last | length |
      | bytes=0-99     | 0     | 99   | 100    |
      | bytes=100-100  | 100   | 100  | 1      |
      | bytes=990-     | 990   | 999  | 10     |
      | bytes=-10      | 990   | 999  | 10     |
      | bytes=-5000    | 0     | 999  | 1000   |
      | bytes=995-2000 | 995   | 999  | 5      |
      | bytes=0-       | 0     | 999  | 1000   |

  Scenario Outline: A range outside the file is refused with 416
    When I request "/media/{video:huiles}" with the header "Range: <range>"
    Then the answer is a problem 416
    And the header "Content-Range" is "bytes */1000"

    Examples:
      | range                       |
      | bytes=1000-                 |
      | bytes=5000-6000             |
      | bytes=-0                    |
      | bytes=99999999999999999999- |

  Scenario Outline: A range the server may ignore gets the whole file
    When I request "/media/{video:huiles}" with the header "Range: <range>"
    Then the response status is 200
    And the body is the whole file of the video "huiles"

    Examples:
      | range           |
      | bytes=0-9,20-29 |
      | bytes=50-10     |
      | items=0-9       |
      | bytes=abc       |

  Scenario: A range for another version of the file gets the whole (current) file
    When I request "/media/{video:huiles}" with an If-Range of another version
    Then the response status is 200
    And the body is the whole file of the video "huiles"

  Scenario: A range for the current version is honoured
    When I request "/media/{video:huiles}"
    And I request "/media/{video:huiles}" with the range "bytes=10-19" and its ETag as If-Range
    Then the response status is 206
    And the body is bytes 10 to 19 of the file

  Scenario: HEAD gives the headers without the file
    When I send a HEAD request to "/media/{video:huiles}"
    Then the response status is 200
    And the header "Content-Length" is "1000"
    And the header "Accept-Ranges" is "bytes"
    And the body is empty

  Scenario Outline: The Content-Type is what the file is
    Given the videos:
      | key  | title | channel  | file        |
      | clip | Clip  | tomcooks | <extension> |
    And the file of the video "clip" is on the media share with 10 bytes
    When I request "/media/{video:clip}"
    Then the header "Content-Type" is "<type>"

    Examples:
      | extension | type             |
      | mp4       | video/mp4        |
      | webm      | video/webm       |
      | mkv       | video/x-matroska |
      | mov       | video/quicktime  |

  # ---------------------------------------------------------------- downloading

  Scenario: The download is named after the title, with the characters files can't have replaced
    When I request "/media/{video:huiles}/download"
    Then the response status is 200
    And the header "Content-Disposition" is:
      """
      attachment; filename="HUILES _ pourquoi certaines deviennent _dangereuses_ _.mp4"; filename*=UTF-8''HUILES%20_%20pourquoi%20certaines%20deviennent%20_dangereuses_%20_.mp4
      """
    And the header "Accept-Ranges" is "bytes"
    And the body is the whole file of the video "huiles"

  Scenario: Accents stay in the UTF-8 name and are dropped from the plain one
    Given the file of the video "creme" is on the media share with 10 bytes
    When I send a HEAD request to "/media/{video:creme}/download"
    Then the header "Content-Disposition" is:
      """
      attachment; filename="Creme brulee _ version 2.mp4"; filename*=UTF-8''Cr%C3%A8me%20br%C3%BBl%C3%A9e%20_%20version%202.mp4
      """

  Scenario: A download can be resumed
    When I request "/media/{video:huiles}/download" with the header "Range: bytes=500-"
    Then the response status is 206
    And the header "Content-Disposition" is there
    And the body is bytes 500 to 999 of the file

  # ---------------------------------------------------------------- not there

  Scenario Outline: No file, no answer
    When I request "<path>"
    Then the answer is a problem 404
    And the problem says "<says>"

    Examples:
      | path                          | says                         |
      | /media/zzzzzzzzzzz            | There's no video with the id |
      | /media/{video:creme}          | isn't on the media share     |
      | /media/{video:creme}/download | isn't on the media share     |

  Scenario: A file the full sync flagged as missing isn't served, even if it came back
    Given the full sync flagged the file of the video "huiles" as missing
    When I request "/media/{video:huiles}"
    Then the answer is a problem 404

  Scenario Outline: Ids that try to reach other files are refused before anything is looked up
    Given a file "secret.txt" sits next to the media share
    When I request "<path>"
    Then the response status is <status>
    And the body doesn't contain "secret"

    Examples:
      | path                                           | status |
      | /media/..%2Fsecret.txt                         | 400    |
      | /media/..%2F..%2Fsecret.txt/download           | 400    |
      | /media/%2E%2E%2Fsecret.tx                      | 400    |
      | /media/{channel:tomcooks}%2F{video:huiles}.mp4 | 400    |
      | /media/../secret.txt                           | 404    |
      | /art/videos/..%2F..%2Fsecret.txt               | 400    |
      | /art/channels/..%2F..%2Fsecret.txt/thumb       | 400    |
      | /art/channels/{channel:tomcooks}/..%2Fsecret   | 404    |

  # ---------------------------------------------------------------- art

  Scenario: A thumbnail, with cache headers
    When I request "/art/videos/{video:huiles}"
    Then the response status is 200
    And the header "Content-Type" is "image/jpeg"
    And the header "Cache-Control" is "public, max-age=86400"
    And the header "ETag" is there
    And the body is the thumbnail of the video "huiles"

  Scenario: A thumbnail the browser already has is answered 304
    When I request "/art/videos/{video:huiles}"
    And I request "/art/videos/{video:huiles}" again with its ETag
    Then the response status is 304
    And the body is empty

  Scenario Outline: Channel art
    When I request "/art/channels/{channel:tomcooks}/<kind>"
    Then the response status is 200
    And the header "Content-Type" is "image/jpeg"
    And the header "Cache-Control" is "public, max-age=86400"

    Examples:
      | kind   |
      | thumb  |
      | banner |

  Scenario Outline: Missing art is a 404
    Given the thumbnail of the video "creme" was never copied
    And the banner of the channel "tomcooks" was never copied
    When I request "<path>"
    Then the answer is a problem 404

    Examples:
      | path                                         |
      | /art/videos/{video:creme}                    |
      | /art/videos/zzzzzzzzzzz                      |
      | /art/channels/{channel:tomcooks}/banner      |
      | /art/channels/UCzzzzzzzzzzzzzzzzzzzzzz/thumb |

  Scenario Outline: Other kinds of channel art don't exist, TV art included (the sync still copies it)
    When I request "/art/channels/{channel:tomcooks}/<kind>"
    Then the answer is a problem 404

    Examples:
      | kind   |
      | tvart  |
      | poster |
      | THUMB  |

  # ---------------------------------------------------------------- resized art (QA09, contract v1.4)

  Scenario: A resized thumbnail, as WebP, with the same caching
    Given the resized versions of the video "huiles" were made
    When I request "/art/videos/{video:huiles}/360"
    Then the response status is 200
    And the header "Content-Type" is "image/webp"
    And the header "Cache-Control" is "public, max-age=86400"
    And the header "ETag" is there
    And the body is a WebP image 360 px wide

  Scenario: A resized version the browser already has is answered 304
    Given the resized versions of the video "huiles" were made
    When I request "/art/videos/{video:huiles}/720"
    And I request "/art/videos/{video:huiles}/720" again with its ETag
    Then the response status is 304
    And the body is empty

  Scenario Outline: Resized channel pictures and banners
    Given the resized versions of the channel "tomcooks" were made
    When I request "/art/channels/{channel:tomcooks}/<kind>/<width>"
    Then the response status is 200
    And the header "Content-Type" is "image/webp"
    And the header "Cache-Control" is "public, max-age=86400"
    And the body is a WebP image <width> px wide

    Examples:
      | kind   | width |
      | thumb  | 48    |
      | thumb  | 96    |
      | thumb  | 176   |
      | thumb  | 320   |
      | banner | 1280  |

  Scenario Outline: Only the widths the sync makes exist
    Given the resized versions of the video "huiles" were made
    And the resized versions of the channel "tomcooks" were made
    When I request "<path>"
    Then the answer is a problem 404

    Examples:
      | path                                          |
      | /art/videos/{video:huiles}/361                |
      | /art/videos/{video:huiles}/800                |
      | /art/videos/{video:huiles}/0360               |
      | /art/videos/{video:huiles}/abc                |
      | /art/channels/{channel:tomcooks}/thumb/1280   |
      | /art/channels/{channel:tomcooks}/banner/48    |
      | /art/channels/{channel:tomcooks}/tvart/48     |
      | /art/channels/UCzzzzzzzzzzzzzzzzzzzzzz/thumb/48 |
      | /art/videos/zzzzzzzzzzz/360                   |

  Scenario: A version the sync hasn't made (yet) is a 404
    When I request "/art/videos/{video:creme}/360"
    Then the answer is a problem 404

  Scenario: A thumbnail narrower than a size has no version of it
    Given the thumbnail of the video "creme" is 480 px wide
    And the resized versions of the video "creme" were made
    When I request "/art/videos/{video:creme}/720"
    Then the answer is a problem 404
    When I request "/art/videos/{video:creme}/360"
    Then the response status is 200

  Scenario Outline: A malformed id in a resized art URL is refused before anything else
    When I request "<path>"
    Then the answer is a problem 400

    Examples:
      | path                                    |
      | /art/videos/..%2F..%2Fsecret.txt/360    |
      | /art/channels/..%2Fsecret/thumb/48      |
