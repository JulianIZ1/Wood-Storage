using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;

namespace WoodStorage
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        /*********
        ** Fields
        *********/
        private const string SaveDataKey = "wood-pile-storage";

        internal const string WoodQualifiedItemId = "(O)388";
        internal const string HardwoodQualifiedItemId = "(O)709";

        /// <summary>The wood/hardwood currently stored in the pile, indexed by slot. A slot holding no item is represented by a null entry.</summary>
        private IList<Item> storage = new List<Item>();

        /// <summary>The offsets (from the farmhouse door tile) of the tiles the wood pile's graphic occupies at house upgrade level 0, where it's drawn as a large 3x2 pile.</summary>
        private static readonly Point[] StarterPileTileOffsets =
        {
            new(-4, -1), new(-3, -1), new(-2, -1),
            new(-4, 0), new(-3, 0), new(-2, 0)
        };

        /// <summary>The offsets (from the farmhouse door tile) of the tiles the wood pile's graphic occupies at house upgrade level 1, where it's drawn as a 2x2 pile.</summary>
        private static readonly Point[] Level1PileTileOffsets =
        {
            new(-4, -1), new(-3, -1),
            new(-4, 0), new(-3, 0)
        };

        /// <summary>The offsets (from the farmhouse door tile) of the tiles the wood pile's graphic occupies at house upgrade level 2 and above, where it's drawn even smaller.</summary>
        private static readonly Point[] Level2PileTileOffsets =
        {
            new(-3, -1),
            new(-3, 0)
        };


        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.Saving += this.OnSaving;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Get how many storage slots the wood pile should have for a given farmhouse upgrade level.</summary>
        /// <param name="houseUpgradeLevel">The farmhouse's current upgrade level (<see cref="Farmer.HouseUpgradeLevel"/>).</param>
        private static int GetCapacityForHouseLevel(int houseUpgradeLevel)
        {
            if (houseUpgradeLevel >= 2)
                return 9;
            if (houseUpgradeLevel >= 1)
                return 3;
            return 1;
        }

        /// <summary>Raised after the player loads a save.</summary>
        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            WoodPileSaveData data = this.Helper.Data.ReadSaveData<WoodPileSaveData>(SaveDataKey) ?? new WoodPileSaveData();

            this.storage = data.Slots
                .Select(slot => slot == null ? null! : ItemRegistry.Create(slot.QualifiedItemId, slot.Stack))
                .ToList();
        }

        /// <summary>Raised before the game writes data to the save file.</summary>
        private void OnSaving(object? sender, SavingEventArgs e)
        {
            var data = new WoodPileSaveData
            {
                Slots = this.storage
                    .Select(item => item == null ? null : new WoodPileSlotData { QualifiedItemId = item.QualifiedItemId, Stack = item.Stack })
                    .ToList()
            };

            this.Helper.Data.WriteSaveData(SaveDataKey, data);
        }

        /// <summary>Raised after the player returns to the title screen.</summary>
        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            this.storage = new List<Item>();
        }

        /// <summary>Raised after the player presses a button on the keyboard, controller, or mouse.</summary>
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.activeClickableMenu != null)
                return;

            // Left-click ("use tool") opens chests in vanilla, so accept it here too alongside the
            // dedicated action button (right-click / X), which is what most players will try first.
            bool isAction = e.Button.IsActionButton();
            bool isUseTool = e.Button.IsUseToolButton();
            if (!isAction && !isUseTool)
                return;

            if (Game1.currentLocation is not Farm farm)
                return;

            // Recomputed fresh on every attempt rather than cached: on a brand-new save, the
            // farmhouse building's position isn't finalized yet at the point this mod's SaveLoaded
            // handler would run, so caching that early result can lock in the wrong door tile for
            // the rest of the session.
            List<Vector2> pileTiles = ModEntry.GetWoodPileTiles(farm).ToList();

            // Cursor.GrabTile clamps to whatever tile the game considers reachable near the player,
            // which excludes the far side of a multi-tile object like this pile (e.g. the row behind
            // the near row, from the player's approach direction). Cursor.Tile isn't reach-limited, so
            // it's used for hit-testing instead; the distance check below still requires the player
            // be standing next to the pile.
            if (!pileTiles.Contains(e.Cursor.Tile))
                return;
            if (!pileTiles.Any(tile => Vector2.Distance(Game1.player.Tile, tile) <= 2f))
                return;

            this.Helper.Input.Suppress(e.Button);
            this.OpenWoodPileMenu();
        }

        /// <summary>Open the wood pile's storage menu, growing its slot count to match the current house upgrade level if needed.</summary>
        private void OpenWoodPileMenu()
        {
            int capacity = ModEntry.GetCapacityForHouseLevel(Game1.player.HouseUpgradeLevel);
            while (this.storage.Count < capacity)
                this.storage.Add(null!);

            Game1.playSound("openChest");
            Game1.activeClickableMenu = new WoodPileMenu(this.storage, capacity);
        }

        /// <summary>Get the tile the wood pile's offsets are anchored to, one tile up and one tile left of the farmhouse door itself.</summary>
        /// <param name="farm">The farm to find the farmhouse on.</param>
        private static Point? GetPileAnchorTile(Farm farm)
        {
            // Note: this deliberately doesn't use Farm.GetMainFarmHouseEntry(). That method caches
            // its result the first time it's called, and on a brand-new save it can be called
            // (by this mod or the game itself) before the farmhouse building's position is finalized,
            // permanently caching a wrong tile for the rest of the session.
            Building? farmhouse = farm.GetMainFarmHouse();
            if (farmhouse == null)
                return null;

            return new Point(farmhouse.tileX.Value + farmhouse.humanDoor.X - 1, farmhouse.tileY.Value + farmhouse.humanDoor.Y);
        }

        /// <summary>Get the tiles the wood pile's graphic occupies, near the farmhouse door.</summary>
        /// <param name="farm">The farm to find the wood pile on.</param>
        private static IEnumerable<Vector2> GetWoodPileTiles(Farm farm)
        {
            Point? doorTile = ModEntry.GetPileAnchorTile(farm);
            if (doorTile == null)
                yield break;

            Point[] offsets = Game1.player.HouseUpgradeLevel switch
            {
                0 => ModEntry.StarterPileTileOffsets,
                1 => ModEntry.Level1PileTileOffsets,
                _ => ModEntry.Level2PileTileOffsets
            };

            foreach (Point offset in offsets)
                yield return new Vector2(doorTile.Value.X + offset.X, doorTile.Value.Y + offset.Y);
        }
    }
}
