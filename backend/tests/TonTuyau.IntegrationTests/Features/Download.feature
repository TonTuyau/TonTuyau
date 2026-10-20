@elasticsearch @sync @api @youtube
Feature: Downloading a video from YouTube
  TonTuyau downloads a video itself (2.0): yt-dlp, with the YouTube page's format (the best video and the best audio by
  default), into one .mkv, merged by ffmpeg, with its chapters and metadata inside. It goes to a folder of its own in
  the media folder first, then to its place, <channel id>/<video id>.mkv, as TubeArchivist lays its files out. The
  video is added to the library with what YouTube said of it, as a refresh records it; its channel first when the
  library doesn't have it, then read on YouTube for its name and pictures. A video of the library isn't downloaded
  again, unless its file is missing (B3): then its file is, and the flag is cleared. It's a run like the refreshes:
  YouTube refusing puts it on hold. It starts from the command line or the Tasks page ("Add a video", B3: an id or a
  YouTube link), through the run queue. yt-dlp is a fake here.

  Background:
    Given TubeArchivist has the channel "alpha" with 3 videos
    And the backend is ready
    And a first import has run
    And I logged in as the admin

  Scenario: A video of a channel the library doesn't have: both are added, the file is in its place
    Given YouTube has the video "omega-01": the title "Une vidéo neuve", 4321 views
    And YouTube has the channel "omega": the name "Omega", 500 subscribers
    When I run "tontuyau youtube download omega-01___"
    Then the command exits with code 0
    And the media folder has the file of the video "omega-01" with 4096 bytes
    And no download is left in the media folder
    And yt-dlp was asked for the format "bestvideo+bestaudio" for the video "omega-01"
    And the video "omega-01" and its channel "omega" are TonTuyau's own
    And the stored YouTube state of the video "omega-01" is "on YouTube"
    When I request the video "omega-01" of TubeArchivist
    Then the response status is 200
    And "title" is "Une vidéo neuve"
    And "views" is "4321"
    And "channel.name" is "Omega"
    And "active" is "true"
    When I request the channel "omega" of TubeArchivist
    Then the response status is 200
    And "name" is "Omega"
    And "subscribers" is "500"

  Scenario: A video of a channel the library has: the channel stays as it is
    Given YouTube has the video "alpha-09": the title "La neuvième", 10 views
    When I run "tontuyau youtube download alpha-09___"
    Then the command exits with code 0
    And the media folder has the file of the video "alpha-09" with 4096 bytes
    When I request the video "alpha-09" of TubeArchivist
    Then the response status is 200
    And "title" is "La neuvième"
    And "channel.name" is "alpha"

  Scenario: On the command line, a video pasted as a link is downloaded
    Given YouTube has the video "omega-05": the title "En ligne de commande", 3 views
    When I run "tontuyau youtube download https://youtu.be/omega-05___?si=x"
    Then the command exits with code 0
    And the media folder has the file of the video "omega-05" with 4096 bytes

  Scenario: A video of the library isn't downloaded again
    When I run "tontuyau youtube download alpha-01___"
    Then the command exits with code 4
    And the output mentions "is in the library already"

  Scenario: From the Tasks page, a video pasted as a link is downloaded through the queue
    Given YouTube has the video "omega-04": the title "Par le lien", 7 views
    When I download "https://www.youtube.com/watch?v=omega-04___&t=42s" with my session
    Then the response status is 202
    And "task" is "downloadVideo"
    And "target" is "omega-04___"
    And "trigger" is "admin"
    And the latest run ends succeeded within 30 seconds
    And the media folder has the file of the video "omega-04" with 4096 bytes
    And the video "omega-04" and its channel "omega" are TonTuyau's own

  Scenario: From the Tasks page, a video of the library with its file is refused
    When I download "alpha-01___" with my session
    Then the response status is 409
    And "detail" mentions "is in the library already"

  Scenario Outline: From the Tasks page, what isn't a video is refused
    When I download "<target>" with my session
    Then the response status is 400

    Examples:
      | target                                   |
      | not a video                              |
      | https://www.youtube.com/@alpha           |
      | https://vimeo.com/watch?v=alpha-01___    |

  Scenario: A video of the library whose file is missing gets its file, and the flag is cleared
    Given YouTube has the video "alpha-02": the title "Revenue", 12 views
    And the file of the video "alpha-02" was found missing
    When I download "https://youtu.be/alpha-02___" with my session
    Then the response status is 202
    And the latest run ends succeeded within 30 seconds
    And the media folder has the file of the video "alpha-02" with 4096 bytes
    And the video "alpha-02" isn't flagged as missing its file
    And the video "alpha-02" has the file TonTuyau downloaded, and is still TubeArchivist's
    And the latest run's notes mention "had no file: it's downloaded now"
    When I request the video "alpha-02" of TubeArchivist
    Then the response status is 200
    And "title" is "Revenue"

  Scenario: A video YouTube says is removed isn't added, and nothing is left behind
    Given YouTube says of the video "omega-02": "ERROR: [youtube] omega-02___: Video unavailable. This video has been removed by the uploader"
    When I run "tontuyau youtube download omega-02___"
    Then the command exits with code 1
    And the output mentions "Not downloaded"
    And TonTuyau doesn't have the video "omega-02"
    And no download is left in the media folder

  Scenario: YouTube refusing puts the download on hold
    Given YouTube says of the video "omega-03": "ERROR: [youtube] omega-03___: Sign in to confirm you’re not a bot"
    When I run "tontuyau youtube download omega-03___"
    Then the command exits with code 5
    And TonTuyau doesn't have the video "omega-03"
    And no download is left in the media folder
