using Coop.Core.Identity;
using TaleWorlds.ObjectSystem;

namespace Coop.GameInterface.Identity
{
    /// <summary>
    /// Converts the engine's own MBGUID-shaped identity into our wire-stable
    /// <see cref="EngineObjectId"/> — the "straightforward" half of docs/SYNCHRONIZATION_MODEL.md
    /// §4.2. No registry needed: the engine already guarantees uniqueness and persistence.
    /// </summary>
    public static class EngineObjectIdAdapter
    {
        public static EngineObjectId ToEngineObjectId(this MBObjectBase engineObject) =>
            new EngineObjectId(engineObject.Id.InternalValue);
    }
}
