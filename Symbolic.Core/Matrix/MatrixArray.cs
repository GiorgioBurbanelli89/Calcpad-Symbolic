using System;
using System.Text;

namespace Calcpad.Core
{
    /// <summary>
    /// Matlab-style cell array of matrices.
    /// Allows storing N matrices in a single container and accessing each by index.
    /// Created with cells(n), accessed with cells.(i).
    /// </summary>
    internal class MatrixArray : IValue, IEquatable<MatrixArray>
    {
        private readonly Matrix[] _items;

        internal int Length => _items.Length;
        internal Matrix[] Items => _items;

        internal MatrixArray(int n)
        {
            if (n < 0)
                throw new ArgumentException("Cell array size must be non-negative.");
            _items = new Matrix[n];
            // Initialize empty (null) slots. User must assign matrices via .(i) = M
        }

        /// <summary>
        /// 1-based indexing (Calcpad convention).
        /// </summary>
        internal Matrix this[int i]
        {
            get
            {
                if (i < 1 || i > _items.Length)
                    throw new IndexOutOfRangeException(
                        $"Cell index {i} out of range [1..{_items.Length}]");
                return _items[i - 1];
            }
            set
            {
                if (i < 1 || i > _items.Length)
                    throw new IndexOutOfRangeException(
                        $"Cell index {i} out of range [1..{_items.Length}]");
                _items[i - 1] = value;
            }
        }

        public bool Equals(MatrixArray other)
        {
            if (other is null) return false;
            if (Length != other.Length) return false;
            for (int i = 0; i < Length; i++)
            {
                var a = _items[i];
                var b = other._items[i];
                if (a is null && b is null) continue;
                if (a is null || b is null) return false;
                if (!a.Equals(b)) return false;
            }
            return true;
        }

        public override bool Equals(object obj) => obj is MatrixArray other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < Length; i++)
                    hash = hash * 31 + (_items[i]?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < _items.Length; i++)
            {
                if (i > 0) sb.Append("  ");
                sb.Append("M_").Append(i + 1);
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}
