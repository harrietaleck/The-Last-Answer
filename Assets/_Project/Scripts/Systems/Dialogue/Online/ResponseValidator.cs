using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TLA.Systems.Dialogue.Online
{
    /// <summary>Outcome of checking a backend reply.</summary>
    public readonly struct ValidationResult
    {
        public readonly bool IsValid;
        public readonly string Text;
        public readonly string Narration;
        public readonly string Reason;

        private ValidationResult(bool ok, string text, string narration, string reason)
        {
            IsValid = ok;
            Text = text;
            Narration = narration;
            Reason = reason;
        }

        public static ValidationResult Ok(string text, string narration) => new ValidationResult(true, text, narration, null);
        public static ValidationResult Invalid(string reason) => new ValidationResult(false, null, null, reason);
    }

    /// <summary>
    /// Checks a backend reply before anything reaches the screen. A reply that fails is treated
    /// like a network failure: the offline mock answers instead. Checks, in order:
    ///  1. JSON shape: an object with the right schemaVersion, our requestId and a non-empty "text".
    ///  2. Clean-up: strips rich-text tags (no &lt;size=500&gt; tricks), control characters, wrapping quotes,
    ///     a leading "Name:" prefix, and extra whitespace.
    ///  3. Out-of-character text: AI disclaimers, refusals, prompt talk.
    ///  4. Length: over the limit → cut at the last sentence end that fits, else rejected.
    ///  5. Invented details: digits or names (capitalised words mid-sentence) that appear nowhere in
    ///     what the request contained. Catches new times, amounts, addresses and people. It's a
    ///     heuristic (spelled-out numbers slip through); the backend prompt is the first line of defence.
    /// Narration goes through the same checks but is simply dropped if it fails.
    /// </summary>
    public static class ResponseValidator
    {
        private static readonly string[] OutOfCharacter =
        {
            "as an ai", "language model", "i'm an ai", "i am an ai", "ai assistant", "i cannot assist", "i can't assist",
            "i can't help with", "i cannot help with", "as a suspect in this game", "system prompt", "my instructions",
            "role-play", "roleplay", "in this scenario", "the player", "json"
        };

        // Capitalised words that are fine anywhere (not names of new people or places).
        private static readonly HashSet<string> CommonCapitalised = new HashSet<string>(StringComparer.Ordinal)
        {
            "I", "I'm", "I've", "I'd", "I'll", "OK", "Okay", "God", "Christ", "Jesus", "Detective", "Inspector", "Officer",
            "Sergeant", "Sir", "Ma'am", "Mr", "Mrs", "Ms", "Dr", "Mister", "Miss", "No", "Yes", "Look", "Listen", "Fine",
            "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday", "TV", "CCTV", "Police"
        };

        private static readonly Regex RichTextTag = new Regex(@"<[^>]{0,64}>", RegexOptions.Compiled);
        private static readonly Regex NamePrefix = new Regex(@"^\s*[A-Z][\w .'-]{0,30}:\s+", RegexOptions.Compiled);
        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex Digits = new Regex(@"\d+", RegexOptions.Compiled);
        private static readonly Regex Word = new Regex(@"[A-Za-z][A-Za-z'’-]*", RegexOptions.Compiled);

        /// <param name="json">Raw response body.</param>
        /// <param name="request">The request it answers (for requestId and the allowed content).</param>
        public static ValidationResult Validate(string json, DialogueRequestDto request, int maxReplyChars, int maxNarrationChars)
        {
            if (string.IsNullOrWhiteSpace(json)) return ValidationResult.Invalid("empty body");

            DialogueResponseDto dto;
            try
            {
                JToken token = JToken.Parse(json);
                if (token.Type != JTokenType.Object) return ValidationResult.Invalid("not a JSON object");
                dto = token.ToObject<DialogueResponseDto>();
            }
            catch (JsonException e)
            {
                return ValidationResult.Invalid("unreadable JSON: " + e.Message);
            }

            if (dto == null) return ValidationResult.Invalid("no content");
            if (dto.schemaVersion != DialogueRequestBuilder.SchemaVersion) return ValidationResult.Invalid($"schemaVersion {dto.schemaVersion}, expected {DialogueRequestBuilder.SchemaVersion}");
            if (request != null && dto.requestId != request.requestId) return ValidationResult.Invalid("requestId doesn't match");

            string corpus = BuildCorpus(request);

            string text = Clean(dto.text);
            if (text.Length == 0) return ValidationResult.Invalid("empty text");
            string problem = Check(text, corpus);
            if (problem != null) return ValidationResult.Invalid(problem);
            text = FitLength(text, maxReplyChars);
            if (text == null) return ValidationResult.Invalid($"text longer than {maxReplyChars} characters");

            string narration = Clean(dto.narration);
            if (narration.Length > 0 && (Check(narration, corpus) != null || narration.Length > maxNarrationChars)) narration = "";

            return ValidationResult.Ok(text, narration);
        }

        /// <summary>Removes markup, control characters, wrapping quotes, a "Name:" prefix and extra spaces.</summary>
        public static string Clean(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            s = RichTextTag.Replace(s, "");
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s) sb.Append(char.IsControl(ch) ? ' ' : ch);
            s = Whitespace.Replace(sb.ToString(), " ").Trim();
            s = NamePrefix.Replace(s, "");
            if (s.Length >= 2 && (s[0] == '"' || s[0] == '“') && (s[s.Length - 1] == '"' || s[s.Length - 1] == '”'))
                s = s.Substring(1, s.Length - 2).Trim();
            return s;
        }

        /// <summary>Null if fine, otherwise the reason it's rejected.</summary>
        private static string Check(string text, string corpus)
        {
            string lower = text.ToLowerInvariant();
            foreach (string phrase in OutOfCharacter)
                if (lower.Contains(phrase)) return $"out of character (\"{phrase}\")";

            foreach (Match m in Digits.Matches(text))
                if (corpus.IndexOf(m.Value, StringComparison.Ordinal) < 0) return $"new detail: number \"{m.Value}\"";

            // Capitalised words that don't start a sentence are probably names: they must already be known.
            bool sentenceStart = true;
            int last = 0;
            foreach (Match m in Word.Matches(text))
            {
                string between = text.Substring(last, m.Index - last);
                if (between.IndexOfAny(new[] { '.', '!', '?', '…', '—', '"', '“', '(' }) >= 0) sentenceStart = true;
                last = m.Index + m.Length;

                string word = m.Value.TrimEnd('\'', '’', '-');
                bool capitalised = char.IsUpper(word[0]);
                if (capitalised && !sentenceStart && !CommonCapitalised.Contains(word) && !CommonCapitalised.Contains(StripPossessive(word))
                    && corpus.IndexOf(StripPossessive(word), StringComparison.Ordinal) < 0)
                    return $"new detail: name \"{word}\"";
                sentenceStart = false;
            }
            return null;
        }

        private static string StripPossessive(string word)
        {
            if (word.EndsWith("'s", StringComparison.Ordinal) || word.EndsWith("’s", StringComparison.Ordinal)) return word.Substring(0, word.Length - 2);
            return word;
        }

        /// <summary>Returns the text if it fits, or its longest whole-sentence prefix that fits, or null.</summary>
        public static string FitLength(string text, int max)
        {
            if (max <= 0 || text.Length <= max) return text;
            for (int i = max - 1; i > 0; i--)
            {
                char ch = text[i];
                if (ch == '.' || ch == '!' || ch == '?' || ch == '…')
                {
                    string cut = text.Substring(0, i + 1).Trim();
                    return cut.Length >= 2 ? cut : null;
                }
            }
            return null;
        }

        /// <summary>All text the request contained: the only source of names and numbers a reply may use.</summary>
        private static string BuildCorpus(DialogueRequestDto r)
        {
            if (r == null) return string.Empty;
            var sb = new StringBuilder();
            void Add(string s)
            {
                if (!string.IsNullOrEmpty(s)) sb.Append(s).Append('\n');
            }

            Add(r.allowed?.baseline);
            Add(r.allowed?.baselineNarration);
            Add(r.suspect?.name);
            Add(r.suspect?.occupation);
            Add(r.suspect?.relationToCase);
            Add(r.suspect?.background);
            Add(r.@case?.title);
            Add(r.@case?.location);
            Add(r.@case?.briefing);
            Add(r.turn?.playerInput);
            Add(r.turn?.topic?.name);
            Add(r.turn?.evidenceShown?.name);
            if (r.history != null)
                foreach (HistoryLineDto h in r.history) Add(h.text);
            return sb.ToString();
        }
    }
}
