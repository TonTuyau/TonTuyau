@elasticsearch @api
Feature: Browsing the library
  The front shows what the API gives it and nothing more: lists come sorted and paged by the backend,
  every count and URL is in the answer, and a cursor only continues the list it came from.

  Background:
    Given the channels:
      | key      | name          | subscribers | tags    | description         |
      | tomcooks | Tom Cooks     | 224000      | Cuisine | Recettes et science |
      | ecole    | École du Goût | 5000        |         | Cours de cuisine    |
      | arte     | ARTE          | 9000000     | Culture | Documentaires       |
      | zeste    | Zeste         | 100         |         |                     |
    And the videos:
      | key     | title                    | channel  | type  | published  | views  | duration |
      | huiles  | Huiles et cuisson        | tomcooks | video | 2026-04-24 | 74198  | 1914     |
      | poulet  | Poulet à la crème        | tomcooks | video | 2026-05-22 | 164569 | 1592     |
      | morteau | Saucisse de Morteau      | tomcooks | short | 2026-06-01 | 5000   | 55       |
      | usa     | La révolution américaine | arte     | video | 2026-10-01 | 55907  | 3211     |
      | sauce   | Les cinq sauces mères    | ecole    | video | 2026-02-10 | 900    | 600      |
      | astuce  | Astuce du jour           | ecole    | short | 2026-07-14 | 12000  | 40       |

  # ---------------------------------------------------------------- home

  Scenario: The home page shows the latest published videos of each type, in the order video, short, stream
    When I request "/api/home"
    Then the response status is 200
    And the section types are "video, short"
    And the "video" section has the titles "La révolution américaine, Poulet à la crème, Huiles et cuisson, Les cinq sauces mères"
    And the "short" section has the titles "Astuce du jour, Saucisse de Morteau"

  Scenario: A stream gets its own section on the home page, after the shorts
    Given the videos:
      | key  | title           | channel | type   | published  |
      | live | Débat en direct | arte    | stream | 2026-03-01 |
    When I request "/api/home?size=1"
    Then the section types are "video, short, stream"
    And the "video" section has the titles "La révolution américaine"
    And the "stream" section has the titles "Débat en direct"

  # ---------------------------------------------------------------- video lists

  Scenario Outline: The video list is sorted by the backend
    When I list "/api/videos?<query>"
    Then the titles are "<titles>"
    And the total is <total>

    Examples:
      | query               | titles                                                                                                                     | total |
      | size=10             | La révolution américaine, Astuce du jour, Saucisse de Morteau, Poulet à la crème, Huiles et cuisson, Les cinq sauces mères | 6     |
      | sort=popular&size=3 | Poulet à la crème, Huiles et cuisson, La révolution américaine                                                             | 6     |
      | sort=oldest&size=2  | Les cinq sauces mères, Huiles et cuisson                                                                                   | 6     |
      | type=short          | Astuce du jour, Saucisse de Morteau                                                                                        | 2     |
      | type=stream         |                                                                                                                            | 0     |

  Scenario: Paging through every video with cursors gives each one exactly once, with the exact total on every page
    When I page through "/api/videos?sort=popular&size=4"
    Then I saw the titles "Poulet à la crème, Huiles et cuisson, La révolution américaine, Astuce du jour, Saucisse de Morteau, Les cinq sauces mères"
    And every page gave the total 6

  Scenario Outline: The shorts player leaves the short being watched out of the library list and of the total
    When I list "/api/videos?<query>"
    Then the titles are "<titles>"
    And the total is <total>

    Examples:
      | query                                      | titles                                                                                           | total |
      | type=short&exclude={video:astuce}          | Saucisse de Morteau                                                                              | 1     |
      | type=short&exclude={video:huiles}          | Astuce du jour, Saucisse de Morteau                                                              | 2     |
      | exclude={video:usa}                        | Astuce du jour, Saucisse de Morteau, Poulet à la crème, Huiles et cuisson, Les cinq sauces mères | 5     |
      | sort=popular&exclude={video:poulet}&size=2 | Huiles et cuisson, La révolution américaine                                                      | 5     |
      | exclude=zzzzzzzzzzz&size=1                 | La révolution américaine                                                                         | 6     |

  Scenario: Paging the library with a video left out never shows it
    When I page through "/api/videos?sort=oldest&size=2&exclude={video:poulet}"
    Then I saw the titles "Les cinq sauces mères, Huiles et cuisson, Saucisse de Morteau, Astuce du jour, La révolution américaine"
    And every page gave the total 5

  Scenario: A library cursor made with another left-out video can't continue the list
    When I list "/api/videos?size=1&exclude={video:astuce}"
    And I ask for the next page with "/api/videos?size=1&exclude={video:morteau}" instead
    Then the answer is a problem 400 about "cursor"

  Scenario: The last page has no cursor
    When I list "/api/videos?size=4"
    Then there is a next page
    When I ask for the next page
    Then the titles are "Huiles et cuisson, Les cinq sauces mères"
    And there is no next page

  Scenario: Videos published at the same time keep a stable order across pages
    Given the videos:
      | key   | title  | channel | published  |
      | same1 | Twin A | zeste   | 2025-01-01 |
      | same2 | Twin B | zeste   | 2025-01-01 |
      | same3 | Twin C | zeste   | 2025-01-01 |
    When I page through "/api/channels/{channel:zeste}/videos?size=1"
    Then I saw the titles "Twin A, Twin B, Twin C"

  Scenario: A cursor only continues the list it came from
    When I list "/api/videos?sort=popular&size=2"
    And I ask for the next page with "/api/videos?sort=latest&size=2" instead
    Then the answer is a problem 400 about "cursor"
    And the problem says "start again from the first page"

  Scenario Outline: Invalid parameters are refused with a ProblemDetails naming them
    When I request "<path>"
    Then the answer is a problem 400 about "<field>"

    Examples:
      | path                                                   | field   |
      | /api/videos?sort=views                                 | sort    |
      | /api/videos?type=movie                                 | type    |
      | /api/videos?size=0                                     | size    |
      | /api/videos?size=101                                   | size    |
      | /api/videos?size=abc                                   | size    |
      | /api/videos?cursor=not-a-cursor                        | cursor  |
      | /api/videos?exclude=abc                                | exclude |
      | /api/videos?cursor=eyJmIjoxfQ                          | cursor  |
      | /api/home?size=500                                     | size    |
      | /api/channels?sort=latest                              | sort    |
      | /api/channels/{channel:tomcooks}/videos?sort=relevance | sort    |
      | /api/channels/{channel:tomcooks}/videos?exclude=abc    | exclude |
      | /api/channels/{channel:tomcooks}/videos?exclude=..%2Fx | exclude |

  # ---------------------------------------------------------------- watch page

  Scenario: The watch page has everything about the video and its file, URLs included
    When I request "/api/videos/{video:huiles}"
    Then the response status is 200
    And the answer has exactly the fields "id, title, type, published, durationSeconds, views, active, thumbnailUrl, thumbnails, channel, description, downloaded, likes, tags, categories, fileSize, mimeType, tracks, mediaUrl, downloadUrl, mediaAvailable"
    And "title" is "Huiles et cuisson"
    And "type" is "video"
    And "published" is "2026-04-24T00:00:00Z"
    And "durationSeconds" is "1914"
    And "thumbnailUrl" is "/art/videos/{video:huiles}"
    And "channel.name" is "Tom Cooks"
    And "channel.thumbnailUrl" is "/art/channels/{channel:tomcooks}/thumb"
    And "channel.subscribers" is "224000"
    And "mimeType" is 'video/mp4; codecs="av01.0.08M.08, opus"'
    And "tracks[0].codec" is "av1"
    And "tracks[1].kind" is "audio"
    And "tracks[1].width" is null
    And "mediaUrl" is "/media/{video:huiles}"
    And "downloadUrl" is "/media/{video:huiles}/download"
    And "mediaAvailable" is "true"

  Scenario Outline: The playable type is built from the file and its codecs
    Given the videos:
      | key  | title | channel | file        | codecs   |
      | clip | Clip  | zeste   | <extension> | <codecs> |
    When I request "/api/videos/{video:clip}"
    Then "mimeType" is '<mimeType>'

    Examples:
      | extension | codecs     | mimeType                                    |
      | mp4       | h264+aac   | video/mp4; codecs="avc1.640028, mp4a.40.2"  |
      | webm      | vp9+opus   | video/webm; codecs="vp09.00.40.08, opus"    |
      | mkv       | hevc+opus  | video/webm; codecs="hvc1.1.6.L120.90, opus" |
      | mp4       | av1        | video/mp4; codecs="av01.0.08M.08"           |
      | mp4       | h264+mp3   | video/mp4; codecs="avc1.640028, mp4a.6B"    |
      | mp4       | prores+pcm | video/mp4; codecs="prores, pcm"             |

  Scenario: A file the full sync found missing is reported as unavailable
    Given the full sync flagged the file of the video "huiles" as missing
    When I request "/api/videos/{video:huiles}"
    Then "mediaAvailable" is "false"

  Scenario: A video TonTuyau confirmed gone from YouTube is still listed, flagged
    Given the videos:
      | key  | title    | channel | youtube | published  |
      | gone | Disparue | zeste   | gone    | 2026-11-01 |
    When I list "/api/videos?size=1"
    Then the titles are "Disparue"
    And "items[0].active" is "false"
    When I request "/api/videos/{video:gone}"
    Then "active" is "false"

  Scenario Outline: Anything short of confirmed gone isn't flagged (1.5): TubeArchivist's flag alone never is
    Given the videos:
      | key   | title  | channel | active   | youtube   | published  |
      | maybe | Peut-ê | zeste   | <active> | <youtube> | 2026-11-01 |
    When I list "/api/videos?size=1"
    Then "items[0].active" is "true"
    When I request "/api/videos/{video:maybe}"
    Then "active" is "true"

    Examples:
      | active | youtube     |
      | false  |             |
      | false  | retry later |
      | true   | not checked |
      | true   | on YouTube  |

  Scenario: No URL is given for an image that was never copied
    Given the thumbnail of the video "huiles" was never copied
    And the thumbnail of the channel "tomcooks" was never copied
    When I request "/api/videos/{video:huiles}"
    Then "thumbnailUrl" is null
    And "channel.thumbnailUrl" is null
    And "thumbnails" lists the widths ""
    And "channel.thumbnails" lists the widths ""

  # ---------------------------------------------------------------- resized art (QA09, contract v1.4)

  Scenario: Cards list the versions the sync recorded, smallest first, the original last
    Given the sync recorded the thumbnail of the video "huiles" as 1280 px wide with the versions "360, 720"
    And the sync recorded the thumbnail of the channel "tomcooks" as 900 px wide with the versions "48, 96, 176, 320"
    When I request "/api/videos/{video:huiles}"
    Then "thumbnails" lists the widths "360, 720, 1280"
    And "thumbnails[0].url" is "/art/videos/{video:huiles}/360"
    And "thumbnails[1].url" is "/art/videos/{video:huiles}/720"
    And "thumbnails[2].url" is "/art/videos/{video:huiles}"
    And "channel.thumbnails" lists the widths "48, 96, 176, 320, 900"
    And "channel.thumbnails[0].url" is "/art/channels/{channel:tomcooks}/thumb/48"
    And "channel.thumbnails[4].url" is "/art/channels/{channel:tomcooks}/thumb"

  Scenario: Lists show the recorded versions too
    Given the sync recorded the thumbnail of the video "huiles" as 1280 px wide with the versions "360"
    When I list "/api/channels/{channel:tomcooks}/videos?sort=oldest"
    Then "items[0].thumbnails" lists the widths "360, 1280"
    And "items[0].thumbnails[1].url" is "/art/videos/{video:huiles}"

  Scenario: An image the sync hasn't recorded yet is described from the art store
    Given the resized versions of the video "huiles" were made
    When I request "/api/videos/{video:huiles}"
    Then "thumbnails" lists the widths "360, 720, 800"
    And "channel.thumbnails" lists the widths "400"

  Scenario: A channel's page lists its picture and banner versions
    Given the sync recorded the thumbnail of the channel "tomcooks" as 900 px wide with the versions "48, 96"
    And the sync recorded the banner of the channel "tomcooks" as 2560 px wide with the versions "1280"
    When I request "/api/channels/{channel:tomcooks}"
    Then "thumbnails" lists the widths "48, 96, 900"
    And "banners" lists the widths "1280, 2560"
    And "banners[0].url" is "/art/channels/{channel:tomcooks}/banner/1280"
    And "banners[1].url" is "/art/channels/{channel:tomcooks}/banner"

  Scenario Outline: Unknown or malformed video ids
    When I request "<path>"
    Then the answer is a problem <status>

    Examples:
      | path                    | status |
      | /api/videos/zzzzzzzzzzz | 404    |
      | /api/videos/short       | 400    |
      | /api/videos/..%2F..%2Fx | 400    |

  # ---------------------------------------------------------------- channels

  Scenario: Channels are sorted by name, accents and case ignored
    When I list "/api/channels"
    Then the names are "ARTE, École du Goût, Tom Cooks, Zeste"
    And each item has exactly the fields "id, name, subscribers, thumbnailUrl, thumbnails, videoCounts"

  Scenario: Channels sorted by subscribers, paged
    When I page through "/api/channels?sort=subscribers&size=3"
    Then I saw the titles "ARTE, Tom Cooks, École du Goût, Zeste"
    And every page gave the total 4

  Scenario Outline: The channel list filters by name as you type, accents ignored
    When I list "/api/channels?q=<typed>"
    Then the names are "<names>"

    Examples:
      | typed | names         |
      | tom   | Tom Cooks     |
      | ecol  | École du Goût |
      | ÉCO   | École du Goût |
      | du g  | École du Goût |
      | xyz   |               |

  Scenario: Each channel card carries its video counts per type, counted by the backend
    When I list "/api/channels"
    Then the "videoCounts.videos" of the channel "Tom Cooks" is "2"
    And the "videoCounts.shorts" of the channel "Tom Cooks" is "1"
    And the "videoCounts.streams" of the channel "Tom Cooks" is "0"
    And the "videoCounts.videos" of the channel "Zeste" is "0"

  Scenario: A channel's page has its art, tags and counts
    Given the banner of the channel "arte" was never copied
    When I request "/api/channels/{channel:arte}"
    Then the response status is 200
    And the answer has exactly the fields "id, name, subscribers, thumbnailUrl, thumbnails, videoCounts, description, bannerUrl, banners, tags, active"
    And "name" is "ARTE"
    And "thumbnailUrl" is "/art/channels/{channel:arte}/thumb"
    And "bannerUrl" is null
    And "banners" lists the widths ""
    And "tags" lists "Culture"
    And "videoCounts.videos" is "1"

  Scenario Outline: A channel's tabs list only its own videos of one type
    When I list "/api/channels/{channel:tomcooks}/videos<query>"
    Then the titles are "<titles>"

    Examples:
      | query         | titles                               |
      |               | Poulet à la crème, Huiles et cuisson |
      | ?sort=popular | Poulet à la crème, Huiles et cuisson |
      | ?sort=oldest  | Huiles et cuisson, Poulet à la crème |
      | ?type=short   | Saucisse de Morteau                  |
      | ?type=stream  |                                      |

  Scenario: Video cards name their channel without its subscribers (only the watch page has them)
    When I list "/api/videos?size=3"
    Then "items[0].channel" has exactly the fields "id, name, thumbnailUrl, thumbnails"

  Scenario Outline: "More from <channel>" leaves the video being watched out of the list and of the total
    When I list "/api/channels/{channel:tomcooks}/videos?<query>"
    Then the titles are "<titles>"
    And the total is <total>

    Examples:
      | query                               | titles                               | total |
      | exclude={video:huiles}              | Poulet à la crème                    | 1     |
      | exclude={video:poulet}&sort=popular | Huiles et cuisson                    | 1     |
      | type=short&exclude={video:morteau}  |                                      | 0     |
      | exclude={video:usa}                 | Poulet à la crème, Huiles et cuisson | 2     |
      | type=short&exclude={video:huiles}   | Saucisse de Morteau                  | 1     |
      | exclude=zzzzzzzzzzz                 | Poulet à la crème, Huiles et cuisson | 2     |

  Scenario: Paging with a video left out never shows it
    Given the videos:
      | key   | title  | channel  | published  |
      | more1 | Plus 1 | tomcooks | 2026-03-01 |
      | more2 | Plus 2 | tomcooks | 2026-03-02 |
    When I page through "/api/channels/{channel:tomcooks}/videos?size=1&exclude={video:poulet}"
    Then I saw the titles "Huiles et cuisson, Plus 2, Plus 1"
    And every page gave the total 3

  Scenario: A cursor made without the left-out video can't continue a list with one
    When I list "/api/channels/{channel:tomcooks}/videos?size=1"
    And I ask for the next page with "/api/channels/{channel:tomcooks}/videos?size=1&exclude={video:huiles}" instead
    Then the answer is a problem 400 about "cursor"

  Scenario Outline: Unknown or malformed channel ids
    When I request "<path>"
    Then the answer is a problem <status>

    Examples:
      | path                                          | status |
      | /api/channels/UCzzzzzzzzzzzzzzzzzzzzzz        | 404    |
      | /api/channels/UCzzzzzzzzzzzzzzzzzzzzzz/videos | 404    |
      | /api/channels/tomcooks                        | 400    |

  # ---------------------------------------------------------------- the API itself

  Scenario: Only reading is allowed
    When I send a POST request to "/api/videos"
    Then the answer is a problem 405

  Scenario: An unknown path answers a ProblemDetails too
    When I request "/api/playlists"
    Then the answer is a problem 404

  Scenario: The front's copy of the OpenAPI document is what the backend serves (its types are generated from it)
    When I request "/openapi/v1.json"
    Then the response status is 200
    And the answer is the front's copy of the OpenAPI document

  Scenario: The OpenAPI document lists what each parameter takes, as the validators check it
    When I request "/openapi/v1.json"
    Then the parameter "sort" of "/api/videos" takes "latest, popular, oldest"
    And the parameter "sort" of "/api/channels" takes "name, subscribers"
    And the parameter "sort" of "/api/search" takes "relevance, latest, popular"
    And the parameter "kind" of "/api/search" takes "all, videos, channels"
    And the parameter "published" of "/api/search" takes "today, week, month, year"
    And the parameter "duration" of "/api/search" takes "short, medium, long"
    And the parameter "type" of "/api/channels/{id}/videos" takes "video, short, stream"
    And "paths./api/search/suggest.get.parameters[1].schema.maximum" is "20"
    And "paths./api/search.get.parameters[0].schema.maxLength" is "200"
    And "paths./api/channels.get.parameters[0].schema.maxLength" is "100"
    And "components.schemas.ProblemDetails.required" lists "title, status"
    And "components.schemas.VideoType.type" is "string"
    And "components.schemas.SearchAllResponse.properties.videos.$ref" is "#/components/schemas/PageOfVideoCard"
    And "paths./api/home.get.responses.200.content" has exactly the fields "application/json"
    And "servers" is null

  Scenario: The backend publishes its OpenAPI document with the contract's shapes
    When I request "/openapi/v1.json"
    Then the response status is 200
    And "info.title" is "TonTuyau API"
    And "components.schemas.VideoCard.required" lists "id, title, type, published, durationSeconds, views, active, thumbnailUrl, thumbnails, channel"
    And "components.schemas.VideoType.enum" lists "video, short, stream"
    And "paths./api/search.get.responses.200.content.application/json.schema.oneOf[1].$ref" is "#/components/schemas/PageOfVideoCard"
