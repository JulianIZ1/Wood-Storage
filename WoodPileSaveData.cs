using System.Collections.Generic;

namespace WoodStorage
{
    /// <summary>The per-save data persisted for the wood pile's contents.</summary>
    internal sealed class WoodPileSaveData
    {
        /// <summary>The item stored in each slot, in slot order. A slot with no item is represented by a null entry.</summary>
        public List<WoodPileSlotData?> Slots { get; set; } = new();
    }

    /// <summary>A serializable snapshot of a single item stack stored in the wood pile.</summary>
    internal sealed class WoodPileSlotData
    {
        /// <summary>The qualified item ID (e.g. <c>(O)388</c> for wood).</summary>
        public string QualifiedItemId { get; set; } = "";

        /// <summary>The number of items in the stack.</summary>
        public int Stack { get; set; }
    }
}
