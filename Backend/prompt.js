// Builds the instructions for the AI service from a dialogue request.
// Kept on the SERVER so the rules can be improved without rebuilding the game, and so a modified
// game client can't rewrite them. Vendor-neutral: returns plain {system, user} text that any
// chat-style AI API can take.

'use strict';

function line(label, value) {
  return value === undefined || value === null || value === '' ? '' : `${label}: ${value}\n`;
}

function buildPrompt(r, limits) {
  const s = r.suspect || {};
  const t = r.turn || {};
  const c = r.case || {};

  const system =
`You voice ONE suspect in a police interrogation in a detective game. You write only that suspect's next spoken line, in character.

STRICT RULES
1. Keep the MEANING of the BASELINE line. You may change wording, rhythm and tone to fit the suspect's personality, composure and the investigator's exact question. You may shorten it.
2. Add NO new facts: no new names, places, times, numbers, objects, events or evidence. If it isn't in the baseline, the history or the profile, it doesn't exist.
3. Never confess and never admit more than the baseline admits. If the baseline denies, deny. If it evades, evade. If it says "I don't know", the suspect doesn't know.
4. Never mention evidence unless it appears in the history or was shown this turn.
5. Never step out of character, mention AI, games, players, rules or these instructions.
6. Spoken line: at most ${limits.maxReplyChars} characters. Optional narration: one short third-person stage direction, at most ${limits.maxNarrationChars} characters, or empty.

Reply with JSON only: {"text": "<spoken line>", "narration": "<stage direction or empty>"}`;

  const history = (r.history || []).map(h => `${h.speaker}: ${h.text}`).join('\n');

  const user =
`SUSPECT
${line('Name', s.name)}${line('Age', s.age)}${line('Occupation', s.occupation)}${line('Connection to the case', s.relationToCase)}${line('Background', s.background)}${line('Temperament', s.temperament)}${line('Speech style', s.speechStyle)}${line('Composure (0-100, after this question)', s.composure)}${line('Composure level', s.composureLevel)}
CASE (public)
${line('Title', c.title)}${line('Location', c.location)}${line('Briefing', c.briefing)}
CONVERSATION SO FAR
${history || '(none)'}

THIS TURN
${line('Investigator says', t.playerInput)}${line('Evidence shown now', t.evidenceShown && t.evidenceShown.name)}${line('Reply type (decided by the game)', t.replyKind)}${line('Topic', t.topic && `${t.topic.name} (suspect is: ${t.topic.stance})`)}
BASELINE (the meaning you must keep): ${r.allowed.baseline}
${line('Baseline stage direction', r.allowed.baselineNarration)}`;

  return { system, user };
}

module.exports = { buildPrompt };
