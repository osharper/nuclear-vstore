using System;
using System.Collections.Generic;
using System.Linq;

using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Errors;

namespace NuClear.VStore.Objects.ContentValidation
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

        public static IEnumerable<ObjectElementValidationError> CheckRestrictedSymbols(string textToCheck, TextElementConstraints constraints)
        {
            const char NonBreakingSpaceSymbol = (char)160;

            var errors = new List<ObjectElementValidationError>();
            if (constraints.WithoutNonBreakingSpace && textToCheck.Contains(NonBreakingSpaceSymbol))
            {
                errors.Add(new NonBreakingSpaceSymbolError());
            }

            if (constraints.WithoutControlСhars && textToCheck.Any(c => char.IsControl(c) && c != '\t' && c != '\n'))
            {
                errors.Add(new ControlСharactersInTextError());
            }

            return errors;
        }
    }
}
