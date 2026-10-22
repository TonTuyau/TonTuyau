@elasticsearch
Feature: TonTuyau's search store
  TonTuyau indexes its library (PostgreSQL since 2.0, Library.feature) in Elasticsearch. Every write to the index is
  an upsert by id, so doing the same work twice is harmless, and a rebuild never shows users a half-built index.

  Scenario: The indices are created when the backend starts
    When the backend starts
    Then the backend becomes ready
    And reads and writes of "videos" both use generation 1
    And reads and writes of "channels" both use generation 1

  Scenario: Restarting keeps the data
    Given the backend is ready
    And the videos "aaaaaaaaaaa, bbbbbbbbbbb" are stored
    When the backend restarts
    Then the backend becomes ready
    And the store holds 2 videos

  Scenario: Writing the index is idempotent and only rewrites what changed
    Given the backend is ready
    When the videos "aaaaaaaaaaa, bbbbbbbbbbb" are indexed
    Then 2 were created, 0 updated and 0 unchanged
    When the videos "aaaaaaaaaaa, bbbbbbbbbbb" are indexed
    Then 0 were created, 0 updated and 2 unchanged
    When the title of "aaaaaaaaaaa" becomes "Nouveau titre" and the videos "aaaaaaaaaaa, bbbbbbbbbbb" are indexed
    Then 0 were created, 1 updated and 1 unchanged
    And the index's video "aaaaaaaaaaa" has the title "Nouveau titre"

  Scenario: Deleting from the index removes only what exists
    Given the backend is ready
    And the videos "aaaaaaaaaaa, bbbbbbbbbbb" are indexed
    When the videos "aaaaaaaaaaa, zzzzzzzzzzz" are deleted from the index
    Then 1 video was deleted
    And reads find "bbbbbbbbbbb" but not "aaaaaaaaaaa"

  Scenario: Users keep browsing the old index while a rebuild writes the new one
    Given the backend is ready
    And the videos "aaaaaaaaaaa" are stored
    When a rebuild starts
    And the videos "bbbbbbbbbbb" are stored
    Then reads find "aaaaaaaaaaa" but not "bbbbbbbbbbb"
    When the rebuild completes
    Then reads find "bbbbbbbbbbb" but not "aaaaaaaaaaa"
    And reads and writes of "videos" both use generation 2

  Scenario: An aborted rebuild changes nothing for users
    Given the backend is ready
    And the videos "aaaaaaaaaaa" are stored
    When a rebuild starts
    And the videos "bbbbbbbbbbb" are stored
    And the rebuild is aborted
    Then reads find "aaaaaaaaaaa" but not "bbbbbbbbbbb"
    And reads and writes of "videos" both use generation 1

  Scenario: A rebuild still works after an abort that couldn't delete its new generation
    Given the backend is ready
    And a leftover generation 2 of every index, without aliases, holds the video "zzzzzzzzzzz"
    When a rebuild starts
    Then writes of "videos" go to generation 2 and reads to generation 1
    And generation 2 of "videos" doesn't hold the video "zzzzzzzzzzz"

  Scenario: Only one rebuild at a time
    Given the backend is ready
    And a rebuild has started
    When another rebuild starts
    Then it is refused because a rebuild is already in progress

  Scenario: An index created by an older definition is reported as outdated
    Given the "videos" index already exists with an outdated definition
    When the backend starts
    Then the backend becomes ready
    And the "videos" index is reported as outdated
    And the "channels" index is reported as up to date

  Scenario Outline: French analysis ignores accents, elisions and plurals
    Given the backend is ready
    Then the French analysis of "<written>" and "<searched>" gives the same terms

    Examples:
      | written              | searched             |
      | Crème brûlée         | creme brulee         |
      | Les épisodes         | episode              |
      | L'histoire de France | histoire france      |
      | Château fort         | CHATEAU FORT         |
      | Chef-d'œuvre         | chef d oeuvre        |
      | Piñata géante        | pinata geante        |

  Scenario: English analysis reduces word forms
    Given the backend is ready
    Then the English analysis of "Running the tests" and "run test" gives the same terms

  Scenario: Readiness reflects Elasticsearch
    When the backend starts
    Then the backend becomes ready
    And "/health/ready" answers 200
