using LazerSystem.Core;

namespace LazerSystem.Timeline
{
    public static class ClipboardManager
    {
        private static LaserCueBlock _copied;

        public static bool HasContent => _copied != null;

        public static void Copy(LaserCueBlock block)
        {
            _copied = block?.DeepClone();
        }

        public static LaserCueBlock PasteClone()
        {
            return _copied?.DeepClone();
        }
    }
}
