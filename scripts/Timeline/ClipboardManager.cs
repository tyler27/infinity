using System.Collections.Generic;
using LazerSystem.Core;

namespace LazerSystem.Timeline
{
    public static class ClipboardManager
    {
        private static LaserCueBlock _copied;
        private static List<LaserCueBlock> _copiedMulti;

        public static bool HasContent => _copied != null || (_copiedMulti != null && _copiedMulti.Count > 0);
        public static bool HasMultiple => _copiedMulti != null && _copiedMulti.Count > 1;

        public static void Copy(LaserCueBlock block)
        {
            _copied = block?.DeepClone();
            _copiedMulti = null;
        }

        public static void CopyMultiple(IEnumerable<LaserCueBlock> blocks)
        {
            _copiedMulti = new List<LaserCueBlock>();
            foreach (var block in blocks)
            {
                if (block != null)
                    _copiedMulti.Add(block.DeepClone());
            }
            _copied = _copiedMulti.Count > 0 ? _copiedMulti[0] : null;
        }

        public static LaserCueBlock PasteClone()
        {
            return _copied?.DeepClone();
        }

        public static List<LaserCueBlock> PasteMultipleClones()
        {
            if (_copiedMulti == null || _copiedMulti.Count == 0)
            {
                var single = PasteClone();
                return single != null ? new List<LaserCueBlock> { single } : new List<LaserCueBlock>();
            }

            var result = new List<LaserCueBlock>();
            foreach (var block in _copiedMulti)
                result.Add(block.DeepClone());
            return result;
        }
    }
}
