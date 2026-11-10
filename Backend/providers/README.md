# Provider adapters

Each file here connects the backend to one AI service. Choose one with the environment variable `TLA_PROVIDER` (the file name without `.js`). The default is `stub`, which uses no AI.

An adapter exports one function:

```js
async function generate({ request, prompt, limits }) {
  // prompt.system and prompt.user: vendor-neutral instructions built by ../prompt.js
  // limits.maxReplyChars and limits.maxNarrationChars: the length limits to enforce
  // Read the API key and model name from process.env, never from this file.
  // Call the service, parse its JSON reply, and return:
  return { text: '...', narration: '...' };
}
module.exports = { generate };
```

- Throwing an error makes the server answer 502. The game then uses offline dialogue for that turn.
- The server cleans and length-limits whatever you return, and the game validates it again.

Only `stub.js` exists so far. No AI service has been chosen yet; see `ONLINE_AI_GUIDE.md` in the repository root for the options.
