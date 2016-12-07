using System;
using System.Collections.Generic;
using System.Linq;

namespace NuClear.VStore.Objects.Validate
{
    public static class TextValidationUtils
    {
        public static int GetTextLength(string text)
        {
            return text.Count(ch => ch != '\n');
        }

        public static IReadOnlyCollection<string> GetTooLongWords(string text, int maxSymbolsPerWord, char[] separators)
        {
            return
                text.Split(separators, StringSplitOptions.RemoveEmptyEntries)
                    .Where(x => x.Length > maxSymbolsPerWord)
                    .ToArray();
        }

        public static IEnumerable<Exception> CheckRestrictedSymbols(string textToCheck)
        {
            const char NonBreakingSpaceSymbol = (char)160;

            var errors = new List<Exception>();
            if (textToCheck.Contains(NonBreakingSpaceSymbol))
            {
                errors.Add(new NonBreakingSpaceSymbolException());
            }

            if (textToCheck.Any(c => char.IsControl(c) && c != '\t' && c != '\n'))
            {
                errors.Add(new ControlСharactersInTextException());
            }

            return errors;
        }
    }
}
