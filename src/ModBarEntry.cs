using System;

namespace NoModBar
{
    internal sealed class ModBarEntry
    {
        public string Id;
        public string Name;
        public string Tooltip;
        public Func<bool> IsVisible;
        public Action Toggle;
    }
}
