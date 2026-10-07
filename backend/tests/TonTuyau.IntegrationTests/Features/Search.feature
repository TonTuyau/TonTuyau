@elasticsearch @api
Feature: Searching
  Search looks at titles (first), channel names, tags and descriptions, in French with accents folded and
  in English. Suggestions complete what is being typed: channels first, then video titles.

  Background:
    Given the clock shows "2026-03-03T18:00:00Z"
    And the channels:
      | key        | name              | subscribers | tags     | description                       |
      | tomcooks   | Tom Cooks         | 224000      | Cuisine  | Recettes et science de la cuisine |
      | sciences   | Science Étonnante | 1500000     | Science  | Vulgarisation                     |
      | cookchef   | The Cooking Chef  | 30000       |          | Cuisine anglaise                  |
      | patisserie | Pâtisserie Maison | 8000        | Desserts | Cuisine sucrée                    |
      | chefs      | Chefs de France   | 2000        |          | La cuisine des chefs              |
    And the videos:
      | key    | title                     | channel    | type   | published  | views  | duration | tags    | description      |
      | creme  | Crème brûlée à la vanille | tomcooks   | video  | 2025-10-10 | 11663  | 105      | dessert |                  |
      | poulet | Poulet à la crème         | tomcooks   | video  | 2026-05-22 | 164569 | 1592     |         |                  |
      | huile  | L'huile d'olive           | tomcooks   | video  | 2026-04-24 | 74198  | 1914     |         |                  |
      | trous  | Les trous noirs           | sciences   | video  | 2024-03-01 | 900000 | 1300     |         |                  |
      | eggs   | How a chef cooks eggs     | cookchef   | video  | 2023-01-01 | 50000  | 300      |         |                  |
      | crepe  | Crêpes faciles            | patisserie | short  | 2026-02-02 | 3000   | 45       | dessert |                  |
      | mousse | Mousse au chocolat        | patisserie | video  | 2026-03-03 | 8000   | 900      |         | Une crème légère |
      | live   | Live pâtisserie           | patisserie | stream | 2026-01-15 | 200    | 7200     |         |                  |

  # ---------------------------------------------------------------- matching

  Scenario Outline: Accents and case don't matter
    When I list "/api/search?kind=videos&q=<typed>"
    Then the titles are, in any order, "Crème brûlée à la vanille, Poulet à la crème, Mousse au chocolat"

    Examples:
      | typed |
      | creme |
      | crème |
      | CRÈME |

  Scenario: A match in the title ranks above a match in the description
    When I list "/api/search?kind=videos&q=creme"
    Then "Poulet à la crème" comes before "Mousse au chocolat"
    And "Crème brûlée à la vanille" comes before "Mousse au chocolat"

  Scenario: French elisions and plurals are understood (in the title only: English analysis can't do this)
    When I list "/api/search?kind=videos&q=huiles"
    Then the titles are "L'huile d'olive"

  Scenario: English words are stemmed too (cooking = cooks: the title, and Tom Cooks' channel name)
    When I list "/api/search?kind=videos&q=cooking"
    Then the titles are, in any order, "How a chef cooks eggs, Crème brûlée à la vanille, L'huile d'olive, Poulet à la crème"

  Scenario: Every word must match, each in any field
    When I list "/api/search?kind=videos&q=tom%20huile"
    Then the titles are "L'huile d'olive"

  Scenario: Tags are searched
    When I list "/api/search?kind=videos&q=dessert"
    Then the titles are, in any order, "Crème brûlée à la vanille, Crêpes faciles"

  # ---------------------------------------------------------------- the word being typed (QA10 M5-1)

  Scenario Outline: The last word may be the start of a word, accents and case ignored
    When I list "/api/search?kind=videos&q=<typed>"
    Then the titles are "Crème brûlée à la vanille"

    Examples:
      | typed            |
      | creme%20b        |
      | creme%20bru      |
      | CRÈME%20BRÛ      |
      | crème%20brûlée   |
      | creme%20brulee   |

  Scenario: A single word being typed finds the words it starts
    When I list "/api/search?kind=videos&q=chocol"
    Then the titles are "Mousse au chocolat"

  Scenario: The other words stay whole words
    When I list "/api/search?kind=videos&q=cre%20brulee"
    Then there are no results

  Scenario Outline: A trailing space means the last word is complete: no start-of-word match
    When I list "/api/search?kind=videos&q=<typed>"
    Then there are no results

    Examples:
      | typed            |
      | creme%20b%20     |
      | creme%20b%20%20  |
      | chocol%20        |

  Scenario: Every word still has to be found, the one being typed included
    When I list "/api/search?kind=videos&q=tom%20c"
    Then the titles are, in any order, "Crème brûlée à la vanille, Poulet à la crème, L'huile d'olive"

  Scenario: A whole-word match ranks above a start-of-word match
    Given the videos:
      | key        | title                 | channel  | published  | views  |
      | tarte      | Tarte aux pommes      | tomcooks | 2025-01-01 | 10     |
      | tartelette | Tartelettes au citron | tomcooks | 2026-01-01 | 900000 |
    When I list "/api/search?kind=videos&q=tarte"
    Then the titles are "Tarte aux pommes, Tartelettes au citron"

  Scenario Outline: The word being typed may start a word of a description (QA11 M5-13)
    Given the videos:
      | key | title       | channel  | published  | views | description                  |
      | riz | Riz au lait | tomcooks | 2025-05-05 | 100   | Un dessert crémeux et onctueux |
    When I list "/api/search?kind=videos&q=<typed>"
    Then the titles are "Riz au lait"

    # Starts that no stemming reaches: "onct" (onctueux), "crém" (crémeux) next to a whole word.
    Examples:
      | typed            |
      | onct             |
      | ONCT             |
      | riz%20cr%C3%A9m  |
      | lait%20CREM      |

  Scenario: "Creme" finds "crémeux" (your QA11 example)
    Given the videos:
      | key | title       | channel  | published  | views | description          |
      | riz | Riz au lait | tomcooks | 2025-05-05 | 100   | Un dessert crémeux |
    When I list "/api/search?kind=videos&q=creme"
    Then the titles are, in any order, "Crème brûlée à la vanille, Poulet à la crème, Mousse au chocolat, Riz au lait"

  Scenario: The word being typed may start a tag
    When I list "/api/search?kind=videos&q=desse"
    Then the titles are, in any order, "Crème brûlée à la vanille, Crêpes faciles"

  Scenario: A start of word in a title ranks above one in a description
    Given the videos:
      | key     | title              | channel  | published  | views  | description     |
      | goûter  | Le goûter          | tomcooks | 2026-01-01 | 900000 | Un chocolat chaud |
      | chocola | Chocolatine maison | tomcooks | 2025-01-01 | 10     |                 |
    When I list "/api/search?kind=videos&q=chocola"
    Then "Chocolatine maison" comes before "Le goûter"
    And "Mousse au chocolat" comes before "Le goûter"

  Scenario: A whole word in a description ranks above a start of word in a description
    Given the videos:
      | key     | title           | channel  | published  | views  | description      |
      | laitage | Idée du soir    | tomcooks | 2026-01-01 | 900000 | Un laitage maison |
      | lait    | Recette du jour | tomcooks | 2025-01-01 | 10     | Avec du lait frais |
    When I list "/api/search?kind=videos&q=lait"
    Then the titles are "Recette du jour, Idée du soir"

  Scenario: Paging through a search for a word being typed gives each match exactly once
    When I page through "/api/search?kind=videos&q=c&size=2"
    Then I saw, in any order, the titles "Crème brûlée à la vanille, Poulet à la crème, L'huile d'olive, How a chef cooks eggs, Crêpes faciles, Mousse au chocolat"
    And every page gave the total 6

  Scenario: A word being typed and the same word complete are two searches: their cursors don't mix
    When I list "/api/search?kind=videos&q=c&size=2"
    And I ask for the next page with "/api/search?kind=videos&q=c%20&size=2" instead
    Then the answer is a problem 400 about "cursor"

  Scenario: Channel searches take the word being typed too
    When I list "/api/search?kind=channels&q=patis"
    Then the names are "Pâtisserie Maison"
    When I list "/api/search?q=chefs%20de%20fr"
    Then the channel names above the videos are "Chefs de France"

  Scenario: Nothing searchable finds nothing
    When I list "/api/search?kind=videos&q=-"
    Then there are no results

  Scenario: Nothing found is an empty page, not an error
    When I list "/api/search?kind=videos&q=zzzz"
    Then there are no results

  # ---------------------------------------------------------------- filters and sorts

  Scenario Outline: Video filters
    When I list "/api/search?kind=videos&q=pâtisserie&sort=latest&<filter>"
    Then the titles are "<titles>"

    Examples:
      | filter                                  | titles                                              |
      | type=video                              | Mousse au chocolat                                  |
      | type=short                              | Crêpes faciles                                      |
      | type=stream                             | Live pâtisserie                                     |
      | duration=short                          | Crêpes faciles                                      |
      | duration=medium                         | Mousse au chocolat                                  |
      | duration=long                           | Live pâtisserie                                     |
      | published=today                         | Mousse au chocolat                                  |
      | published=week                          | Mousse au chocolat                                  |
      | published=month                         | Mousse au chocolat, Crêpes faciles                  |
      | published=year                          | Mousse au chocolat, Crêpes faciles, Live pâtisserie |
      | published=month&type=short              | Crêpes faciles                                      |
      | channel={channel:patisserie}&type=short | Crêpes faciles                                      |
      | channel={channel:tomcooks}              |                                                     |

  Scenario: A period's cut-off is set on the first page and kept by the next ones, even a month later
    When I list "/api/search?kind=videos&q=pâtisserie&published=month&sort=latest&size=1"
    Then the titles are "Mousse au chocolat"
    And the total is 2
    When the clock moves on 30 days
    And I ask for the next page
    Then the titles are "Crêpes faciles"
    And the total is 2
    When I list "/api/search?kind=videos&q=pâtisserie&published=month&sort=latest&size=1"
    Then there are no results

  Scenario: Search results can be sorted by date instead of relevance
    When I list "/api/search?kind=videos&q=creme&sort=latest"
    Then the titles are "Poulet à la crème, Mousse au chocolat, Crème brûlée à la vanille"

  Scenario: Popular puts the most viewed first
    When I list "/api/search?kind=videos&q=tom&sort=popular"
    Then the titles are "Poulet à la crème, L'huile d'olive, Crème brûlée à la vanille"

  # ---------------------------------------------------------------- kinds

  Scenario: Searching everything shows the best channels above a page of videos
    When I list "/api/search?q=tom"
    Then the channel names above the videos are "Tom Cooks"
    And the titles are, in any order, "Crème brûlée à la vanille, Poulet à la crème, L'huile d'olive"
    And the total is 3

  Scenario: At most three channels are shown above the videos
    When I list "/api/search?q=cuisine"
    Then "channels" has 3 items

  Scenario: The channels above the videos are on the first page only
    When I list "/api/search?q=tom&size=2"
    Then the channel names above the videos are "Tom Cooks"
    When I ask for the next page
    Then "channels" has 0 items
    And the total is 3
    And there is no next page

  Scenario: Searching channels gives a page of channel cards
    When I page through "/api/search?kind=channels&q=cuisine&size=3"
    Then I saw, in any order, the titles "Tom Cooks, The Cooking Chef, Pâtisserie Maison, Chefs de France"
    And every page gave the total 4

  Scenario: Paging through search results gives each match exactly once
    When I page through "/api/search?kind=videos&q=creme&size=1"
    Then I saw, in any order, the titles "Crème brûlée à la vanille, Poulet à la crème, Mousse au chocolat"
    And every page gave the total 3

  Scenario: A cursor of one search can't continue another
    When I list "/api/search?q=tom&size=1"
    And I ask for the next page with "/api/search?kind=videos&q=tom&size=1" instead
    Then the answer is a problem 400 about "cursor"

  Scenario Outline: Invalid searches
    When I request "/api/search?<query>"
    Then the answer is a problem 400 about "<field>"

    Examples:
      | query                            | field     |
      |                                  | q         |
      | q=%20%20                         | q         |
      | q=x&kind=playlists               | kind      |
      | q=x&sort=oldest                  | sort      |
      | q=x&duration=epic                | duration  |
      | q=x&channel=../../etc            | channel   |
      | q=x&published=yesterday          | published |
      | q=x&published=Week               | published |
      | q=x&published=2026-01-01         | published |
      | q=x&kind=channels&published=week | published |
      | q=x&kind=channels&type=video     | type      |
      | q=x&kind=channels&sort=latest    | sort      |
      | q=x&size=0                       | size      |

  Scenario: A query longer than 200 characters is refused
    When I request "/api/search?q=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
    Then the answer is a problem 400 about "q"

  # ---------------------------------------------------------------- suggestions

  Scenario: Suggestions give matching channels first, then video titles
    When I request "/api/search/suggest?q=pat"
    Then the suggestions are:
      | kind    | text              |
      | channel | Pâtisserie Maison |
      | video   | Live pâtisserie   |

  Scenario Outline: Suggestions complete the start of words, accents and case ignored
    When I request "/api/search/suggest?q=<typed>"
    Then the suggestions are, in any order:
      | kind  | text                      |
      | video | Crème brûlée à la vanille |
      | video | Crêpes faciles            |
      | video | Poulet à la crème         |

    Examples:
      | typed |
      | cre   |
      | CRÈ   |
      | crê   |

  Scenario: Every word typed must start a word of the suggestion
    When I request "/api/search/suggest?q=tom%20c"
    Then the suggestions are:
      | kind    | text      |
      | channel | Tom Cooks |

  Scenario: A title shared by several videos is suggested once
    Given the videos:
      | key    | title          | channel  | published  |
      | crepe2 | Crêpes faciles | tomcooks | 2026-02-03 |
    When I request "/api/search/suggest?q=crep"
    Then the suggestions are:
      | kind  | text           |
      | video | Crêpes faciles |

  Scenario: At most three channels open the suggestions, video titles fill the rest
    Given the channels:
      | key    | name           |
      | facile | Cuisine Facile |
    When I request "/api/search/suggest?q=c"
    Then there are 3 channel suggestions first, then 5 video suggestions

  Scenario: The number of suggestions is limited
    When I request "/api/search/suggest?q=c&size=2"
    Then "suggestions" has 2 items

  Scenario Outline: Invalid suggestion requests
    When I request "/api/search/suggest?<query>"
    Then the answer is a problem 400 about "<field>"

    Examples:
      | query       | field |
      |             | q     |
      | q=a&size=21 | size  |
      | q=a&size=0  | size  |
