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

  Scenario: The format is the YouTube page's, and saving the other settings keeps it
    Given YouTube has the video "omega-06": the title "En 720p", 3 views
    When I send the YouTube settings '{"comments": "off", "downloadFormat": "  bestvideo[height<=720]+bestaudio/best  "}' with my session
    Then the response status is 200
    And "downloadFormat" is "bestvideo[height<=720]+bestaudio/best"
    When I send the YouTube settings '{"comments": "top", "commentsMax": 100}' with my session
    Then "downloadFormat" is "bestvideo[height<=720]+bestaudio/best"
    When I run "tontuyau youtube download omega-06___"
    Then the command exits with code 0
    And yt-dlp was asked for the format "bestvideo[height<=720]+bestaudio/best" for the video "omega-06"

  Scenario Outline: A format that isn't one line of yt-dlp's syntax is refused
    When I send the YouTube settings '{"comments": "off", "downloadFormat": <format>}' with my session
    Then the response status is 400

    Examples:
      | format                     |
      | ""                         |
      | "   "                      |
      | "bestvideo\nbestaudio"     |
      | "<201 characters>"         |

  Scenario: Signed in, the format matching none: downloaded again at once without the cookie
    Given YouTube has the video "omega-10": the title "Sans cookie", 8 views
    And YouTube, signed in, says of the video "omega-10": "ERROR: [youtube] omega-10___: Requested format is not available. Use --list-formats for a list of available formats"
    And I sent the YouTube cookie "SIGNED_IN" with my session
    When I download "omega-10___" with my session
    Then the response status is 202
    And the latest run ends succeeded within 30 seconds
    And the media folder has the file of the video "omega-10" with 4096 bytes
    And the last run's log has, in order, lines with:
      | level       | text                                                   |
      | information | Requested format is not available                      |
      | information | matched none of omega-10___'s: tried again without it  |

  Scenario: Signed in, the format matching none, the retry without the cookie turned off: the download fails
    Given YouTube has the video "omega-11": the title "Avec cookie seulement", 8 views
    And YouTube, signed in, says of the video "omega-11": "ERROR: [youtube] omega-11___: Requested format is not available. Use --list-formats for a list of available formats"
    And I sent the YouTube cookie "SIGNED_IN" with my session
    When I send the YouTube settings '{"comments": "off", "retryWithoutCookie": false}' with my session
    Then the response status is 200
    And "retryWithoutCookie" is "false"
    When I download "omega-11___" with my session
    Then the response status is 202
    And the latest run ends failed within 30 seconds
    And TonTuyau doesn't have the video "omega-11"

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

  Scenario: Try again with a format: this download takes it, the YouTube page's stays as it was
    Given YouTube has the video "omega-07": the title "Au format choisi", 5 views
    When I download "omega-07___" with the format "  398+251  " with my session
    Then the response status is 202
    And the latest run ends succeeded within 30 seconds
    And yt-dlp was asked for the format "398+251" for the video "omega-07"
    Given YouTube has the video "omega-08": the title "Au format de la page", 5 views
    When I download "omega-08___" with my session
    Then the response status is 202
    And the latest run ends succeeded within 30 seconds
    And yt-dlp was asked for the format "bestvideo+bestaudio" for the video "omega-08"

  Scenario: A format given this once that is blank counts as none: the YouTube page's
    Given YouTube has the video "omega-09": the title "Au format vide", 5 views
    When I download "omega-09___" with the format "   " with my session
    Then the response status is 202
    And the latest run ends succeeded within 30 seconds
    And yt-dlp was asked for the format "bestvideo+bestaudio" for the video "omega-09"

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
