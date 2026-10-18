using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using FFXIVClientStructs.FFXIV.Component.GUI;

AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XIVLauncherCN", "addon", "Hooks", "dev", name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
Run();

[MethodImpl(MethodImplOptions.NoInlining)]
static unsafe void Run()
{
    // Only owned test memory is accessed: no game node allocation or invocation.
    var labelMemory = (AtkTextNode*)NativeMemory.AllocZeroed((nuint)sizeof(AtkTextNode));
    try
    {
        var label = (TextNode)RuntimeHelpers.GetUninitializedObject(typeof(TextNode));
        typeof(NodeBase<AtkTextNode>).GetProperty("Node")!.SetValue(label, Pointer.Box(labelMemory, typeof(AtkTextNode*)));
        var checkbox = (CheckboxNode)RuntimeHelpers.GetUninitializedObject(typeof(CheckboxNode));
        typeof(CheckboxNode).GetField("<Label>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(checkbox, label);
        labelMemory->TextFlags = TextFlags.Ellipsis;
        Check(checkbox.DisableAutoResize, "Fixed-width checkbox labels use the full assigned width rather than their initial short width");
        labelMemory->TextFlags = TextFlags.AutoAdjustNodeSize;
        Check(!checkbox.DisableAutoResize, "Auto-sized checkbox labels still report automatic sizing");
    }
    finally { NativeMemory.Free(labelMemory); }
    Check(typeof(NativeAddon).Assembly.GetName().Name == "UndercutKing.KamiToolKit", "Native UI dependency is isolated from other plugins");
    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
    var addon = (NativeAddon)RuntimeHelpers.GetUninitializedObject(typeof(NativeAddon));
    // Reproduce the closed window: the wrapper survives, but its children are gone.
    var staleWindow = (WindowNode)RuntimeHelpers.GetUninitializedObject(typeof(WindowNode));
    typeof(NativeAddon).GetField("<WindowNode>k__BackingField", flags)!.SetValue(addon, staleWindow);
    addon.Title = "处理品质冲突";
    addon.Subtitle = "重复打开";
    Check(addon.Title.ToString() == "处理品质冲突" && addon.Subtitle.Value.ToString() == "重复打开",
        "Closed addon caches titles without touching released nodes");

    var tracking = (TrackingWindow)RuntimeHelpers.GetUninitializedObject(typeof(TrackingWindow));
    typeof(NativeAddon).GetField("<WindowNode>k__BackingField", flags)!.SetValue(addon, tracking);
    typeof(NativeAddon).GetProperty("InternalAddon", flags)!.SetValue(addon, Pointer.Box((AtkUnitBase*)1, typeof(AtkUnitBase*)));
    addon.Title = "新标题";
    addon.Subtitle = "新副标题";
    Check(tracking.Calls == 2, "Active addon still updates title and subtitle immediately");

    typeof(NativeAddon).GetField("isFinalized", flags)!.SetValue(addon, true);
    addon.Title = "下次打开";
    addon.Subtitle = "已关闭";
    Check(tracking.Calls == 2, "Finalized addon ignores stale nodes before destructor completes");
    Check(addon.Title.ToString() == "下次打开" && addon.Subtitle.Value.ToString() == "已关闭",
        "Next allocation retains titles assigned during closing");
}
static void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
    Console.WriteLine("PASS: " + message);
}
sealed class TrackingWindow : WindowNode
{
    internal int Calls;
    public override void SetTitle(string title, string subtitle = null) => Calls++;
}
