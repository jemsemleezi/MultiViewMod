// MultiViewKeyBindings.cs
using RimWorld;
using Verse;

namespace MultiViewMod
{
    /// <summary>
    /// 快捷键定义类
    /// </summary>
    [DefOf]
    public static class MultiViewKeyBindings
    {
        public static KeyBindingDef MultiView_OpenWindow;
        public static KeyBindingDef MultiView_CloseWindow;
        public static KeyBindingDef MultiView_ToggleFollow;
        public static KeyBindingDef MultiView_ResetZoom;

        static MultiViewKeyBindings()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MultiViewKeyBindings));
        }
    }
}