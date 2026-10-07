// Free-text limits of docs/API_CONTRACT.md, so the boxes never let the user type a value the API refuses.
// They're the contract's numbers, not a filter: longer text simply can't be typed (or, for suggestions, isn't
// asked about). The backend counts the trimmed text, and the front trims before sending too.

/** `/api/search` `q`: 1–200 characters. */
export const SEARCH_MAX_LENGTH = 200

/** `/api/search/suggest` `q`: 1–100 characters. */
export const SUGGEST_MAX_LENGTH = 100

/** `/api/channels` `q`: at most 100 characters (contract, QA10 M5-3). */
export const CHANNEL_FILTER_MAX_LENGTH = 100
