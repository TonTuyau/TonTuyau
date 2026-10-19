@elasticsearch @sync @api @youtube
Feature: A video's comments and chapters, for its watch page
  GET /api/videos/{id}/comments and the chapters of GET /api/videos/{id} (contract v1.13, TonTuyau 1.5): what TonTuyau
  read on YouTube when it refreshed the video. Comments come as threads (a comment on the video and its answers), in
  YouTube's order or the newest first, page by page. Never read: no comments at all (readAt null); read and none: an
  empty list. yt-dlp is a fake here.

  Background:
    Given TubeArchivist has the channel "alpha" with 2 videos
    And the backend is ready
    And a first import has run
    And I logged in as the admin
    And reading comments is on

  Scenario: Never read: no comments, and no chapters
    When I request "/api/videos/alpha-01___/comments" with my session
    Then the response status is 200
    And the answer has exactly the fields "readAt, youTubeCount, kept, items, nextCursor, total"
    And "readAt" is null
    And "items" has 0 items
    When I request "/api/videos/alpha-01___" with my session
    Then "chapters" has 0 items

  Scenario: Read: the threads in YouTube's order, each with its answers
    Given YouTube has the video "alpha-01" with 3 comments and 2 chapters
    When I refresh the video "alpha-01" with my session
    Then the latest run ends succeeded within 20 seconds
    When I request "/api/videos/alpha-01___/comments" with my session
    Then the response status is 200
    And "readAt" is not null
    And "youTubeCount" is "3"
    And "kept" is "6"
    And "total" is "3"
    And "items[0].comment.text" is "Comment 1"
    And "items[0].comment.pinned" is "true"
    And "items[0].replies[0].text" is "Reply 1"
    And "items[0].replies[0].byUploader" is "true"
    And "items[0].comment" has exactly the fields "id, text, author, published, likes, pinned, byUploader, hearted"
    When I request "/api/videos/alpha-01___" with my session
    Then "chapters" has 2 items
    And "chapters[1].title" is "Part 2"
    And "chapters[1].startSeconds" is "30"

  Scenario: The newest first, page by page
    Given YouTube has the video "alpha-01" with 3 comments and 0 chapters
    When I refresh the video "alpha-01" with my session
    Then the latest run ends succeeded within 20 seconds
    When I request "/api/videos/alpha-01___/comments?sort=newest&size=2" with my session
    Then "items" has 2 items
    And "items[0].comment.text" is "Comment 3"
    When I request the next page of "/api/videos/alpha-01___/comments?sort=newest&size=2" with my session
    Then "items" has 1 item
    And "items[0].comment.text" is "Comment 1"
    And "nextCursor" is null

  Scenario: Read, and there are none
    Given YouTube has the video "alpha-01" with 0 comments and 0 chapters
    When I refresh the video "alpha-01" with my session
    Then the latest run ends succeeded within 20 seconds
    When I request "/api/videos/alpha-01___/comments" with my session
    Then "readAt" is not null
    And "kept" is "0"
    And "items" has 0 items

  Scenario Outline: A video that isn't there, or a request that isn't right
    When I request "<path>" with my session
    Then the answer is a problem <status>

    Examples:
      | path                                       | status |
      | /api/videos/zzzzzzzzzzz/comments           | 404    |
      | /api/videos/alpha-01___/comments?sort=best | 400    |
      | /api/videos/alpha-01___/comments?size=0    | 400    |
      | /api/videos/alpha-01___/comments?cursor=x  | 400    |
