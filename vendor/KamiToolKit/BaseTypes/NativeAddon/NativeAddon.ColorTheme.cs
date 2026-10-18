namespace KamiToolKit.BaseTypes;

public unsafe partial class NativeAddon
{
    /// <summary>
    /// 已打开界面的节点完成主题刷新后触发。
    /// </summary>
    public event Action? ColorThemeChanged;

    internal void NotifyColorThemeChanged()
    {
        if (disposeState != AddonDisposeState.Alive || InternalAddon == null)
            return;

        ColorThemeChanged?.Invoke();
    }
}
