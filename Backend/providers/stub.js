// "stub" provider: no AI service, no account, no cost.
// Returns the game's authored baseline line unchanged, so the whole online path (network,
// validation, fallback) can be tested before any AI service is chosen.

'use strict';

async function generate({ request }) {
  return {
    text: request.allowed.baseline,
    narration: request.allowed.baselineNarration || ''
  };
}

module.exports = { generate };
