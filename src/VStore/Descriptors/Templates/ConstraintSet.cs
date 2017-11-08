using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using NuClear.VStore.Templates;

namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class ConstraintSet : IReadOnlyDictionary<Language, IElementConstraints>, IEnumerable<ConstraintSetItem>
    {
        private readonly Dictionary<Language, IElementConstraints> _constraints = new Dictionary<Language, IElementConstraints>();

        public ConstraintSet(IEnumerable<ConstraintSetItem> constraintSetItems)
        {
            foreach (var item in constraintSetItems)
            {
                if (_constraints.ContainsKey(item.Language))
                {
                    throw new ArgumentException($"Constraints for language {item.Language} appears more than once.", nameof(constraintSetItems));
                }

                _constraints.Add(item.Language, item.ElementConstraints);
            }
        }

        int IReadOnlyCollection<KeyValuePair<Language, IElementConstraints>>.Count => _constraints.Count;

        IEnumerable<Language> IReadOnlyDictionary<Language, IElementConstraints>.Keys => _constraints.Keys;

        IEnumerable<IElementConstraints> IReadOnlyDictionary<Language, IElementConstraints>.Values => _constraints.Values;

        IElementConstraints IReadOnlyDictionary<Language, IElementConstraints>.this[Language key] => _constraints[key];

        public IElementConstraints For(Language language)
        {
            if (_constraints.TryGetValue(language, out var constraints))
            {
                return constraints;
            }

            if (_constraints.TryGetValue(Language.Unspecified, out constraints))
            {
                return constraints;
            }

            throw new ConstraintsNotFoundException(language);
        }

        public IEnumerator<ConstraintSetItem> GetEnumerator()
        {
            return _constraints.Select(item => new ConstraintSetItem(item.Key, item.Value)).GetEnumerator();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ConstraintSet other))
            {
                return false;
            }

            return ReferenceEquals(obj, this) || Equals(other);
        }

        public override int GetHashCode()
        {
            return _constraints?.GetHashCode() ?? 0;
        }

        private bool Equals(ConstraintSet other)
        {
            return _constraints.Count == other._constraints.Count && !_constraints.Except(other._constraints).Any();
        }

        IEnumerator<KeyValuePair<Language, IElementConstraints>> IEnumerable<KeyValuePair<Language, IElementConstraints>>.GetEnumerator() => _constraints.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _constraints.GetEnumerator();

        bool IReadOnlyDictionary<Language, IElementConstraints>.ContainsKey(Language key) => _constraints.ContainsKey(key);

        bool IReadOnlyDictionary<Language, IElementConstraints>.TryGetValue(Language key, out IElementConstraints value) => _constraints.TryGetValue(key, out value);
    }
}