using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;

public class RTLFixer
{
    // Regex to identify left-to-right chunks (English letters, digits, plus attached punctuation/spaces).
    // These will later be reversed when mixed with RTL text.
    static readonly Regex LtrChunkRegex = new Regex(
        @"[0-9A-Za-z]+(?:[\p{P}\s]+[0-9A-Za-z]+)*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant); // סטטי-משתנה שמשותף לכולם, כמו משהו ששייך לכל הבנקים. משתנה ששייך לכל המופעים. אם אני משנה במקום אחד, זה משנה לכולם. זה שייך לכל המופעים של המחלקה הזאת (RTLFixer)

    // Mapping of punctuation pairs for mirroring in RTL contexts.
    // Example: '(' becomes ')'.
    static readonly Dictionary<char, char> PuncMap = new Dictionary<char, char>
    {
        ['('] = ')', [')'] = '(',
        ['['] = ']', [']'] = '[',
        ['{'] = '}', ['}'] = '{',
        ['<'] = '>', ['>'] = '<',
        ['"'] = '"',
        ['\''] = '\''
    };

    /// <summary>
    /// Swaps directional punctuation to match RTL flow.
    /// Example: "Hello (World)" -> "Hello )World(".
    /// </summary>
    static string SwapPunctuationOnce(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var chars = input.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (PuncMap.TryGetValue(c, out char mapped))
            {
                chars[i] = mapped; // Replace with mirrored punctuation
            }
        }
        return new string(chars);
    }

    /// <summary>
    /// Reverses English/Latin chunks inside a string while leaving RTL text untouched.
    /// This helps align mixed-direction text correctly.
    /// </summary>
    static string ReverseLtrChunks(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new StringBuilder(input.Length);
        int lastIndex = 0;

        foreach (Match m in LtrChunkRegex.Matches(input))
        {
            // Copy text between last processed index and current match unchanged
            if (m.Index > lastIndex)
                sb.Append(input, lastIndex, m.Index - lastIndex);

            int len = m.Length;
            char[] buffer = ArrayPool<char>.Shared.Rent(len); // Use pooled buffer for efficiency
            try
            {
                // Copy chunk characters into buffer
                for (int i = 0; i < len; i++)
                    buffer[i] = input[m.Index + i];

                // Reverse buffer in place
                int l = 0, r = len - 1;
                while (l < r)
                { 
                    var t = buffer[l]; 
                    buffer[l] = buffer[r]; 
                    buffer[r] = t; 
                    l++; r--;
                }

                // Append reversed chunk
                sb.Append(buffer, 0, len);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer); // Return buffer to pool
            }

            lastIndex = m.Index + m.Length; // Update cursor
        }

        // Append any remaining text after last match
        if (lastIndex < input.Length)
            sb.Append(input, lastIndex, input.Length - lastIndex);

        return sb.ToString();
    }

    /// <summary>
    /// Checks if a string contains any RTL characters (Hebrew, Arabic, etc.).
    /// Uses Unicode ranges for Hebrew, Arabic, and their presentation forms.
    /// </summary>
    static bool HasRtlChars(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;

        foreach (char ch in s)
        {
            int code = ch;
            if ((code >= 0x0590 && code <= 0x08FF) ||   // Hebrew + Arabic blocks
                (code >= 0xFB1D && code <= 0xFDFF) ||  // Hebrew/Arabic presentation forms
                (code >= 0xFE70 && code <= 0xFEFF))    // Arabic presentation forms B
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Fixes mixed-direction (LTR + RTL) text for a TextMeshPro text object.
    /// - Enables/disables RTL mode
    /// - Reverses Latin chunks inside RTL strings
    /// - Swaps punctuation for RTL readability
    /// </summary>
    public static void FixRtl<T>(T textMesh, string input) where T : TMP_Text
    {
        if (textMesh == null) return;

        if (string.IsNullOrWhiteSpace(input))
        {
            // Reset state if input is empty
            textMesh.isRightToLeftText = false;
            if (textMesh.text != string.Empty)
                textMesh.text = string.Empty;
            return;
        }

        bool hasRtl = HasRtlChars(input);
        textMesh.isRightToLeftText = hasRtl;

        // If no RTL chars are present, just set the text as is
        if (!hasRtl)
        {
            if (textMesh.text != input)
                textMesh.text = input;
            return;
        }

        // Process string: reverse LTR chunks and swap punctuation
        string processed = SwapPunctuationOnce(ReverseLtrChunks(input));

        if (textMesh.text != processed)
            textMesh.text = processed;
    }

    public static void SetTextInTMP<T>(T textMesh, string input, bool isRtl) where T : TMP_Text // Written by SilkySorters team
    {
        if (isRtl)
        {
            textMesh.isRightToLeftText = true;
            textMesh.alignment = TextAlignmentOptions.Right;
            FixRtl(textMesh, input);
        }
        else
        {
            textMesh.text = input;
            textMesh.alignment = TextAlignmentOptions.Left;
            textMesh.isRightToLeftText = false;
        }
    }
}
