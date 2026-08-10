using System;

namespace NoModBar.Core
{
    public sealed class ModBarEntry
    {
        public string Id;
        public string Name;
        public string Tooltip;
        public Func<bool> IsVisible;
        public Action Toggle;
    }
}
