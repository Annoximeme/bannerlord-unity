using TaleWorlds.SaveSystem;

namespace Coop.GameInterface
{
    /// <summary>
    /// Phase 1.2 exit criterion: stub only, defines nothing yet. Auto-discovered by
    /// TaleWorlds.SaveSystem.SaveManager (verified by decompiling SaveManager /
    /// DefinitionContext against the real install — TaleWorlds.SaveSystem.Definition
    /// .DefinitionContext.GetSaveableAssemblies() collects every loaded assembly that
    /// references TaleWorlds.SaveSystem.dll, then CollectTypes() reflects over each one
    /// for non-abstract SaveableTypeDefiner subclasses and Activator.CreateInstance()s
    /// them — no manual registration call exists or is needed).
    ///
    /// SAVE_BASE_ID is provisional. Real id-range allocation (avoiding collisions with
    /// other installed mods' definers) is Phase 1.9 work, not before real types are
    /// defined here.
    /// </summary>
    public sealed class CoopSaveableTypeDefiner : SaveableTypeDefiner
    {
        private const int SaveBaseId = 87_412_000;

        public CoopSaveableTypeDefiner() : base(SaveBaseId)
        {
        }

        protected override void DefineClassTypes()
        {
        }
    }
}
