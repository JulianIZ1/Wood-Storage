using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace WoodStorage
{
    /// <summary>A simple click-to-transfer storage menu for the wood pile, restricted to wood and hardwood.</summary>
    internal sealed class WoodPileMenu : IClickableMenu
    {
        /*********
        ** Fields
        *********/
        private const int Margin = 32;
        private const int SectionGap = 96;

        private readonly IList<Item> storage;
        private readonly InventoryMenu storageMenu;
        private readonly InventoryMenu playerInventoryMenu;


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="storage">The wood pile's backing item list, sized to <paramref name="capacity"/>.</param>
        /// <param name="capacity">The number of slots to display.</param>
        public WoodPileMenu(IList<Item> storage, int capacity)
            : base(0, 0, 0, 0, true)
        {
            this.storage = storage;

            int rows = capacity > 3 ? 3 : 1;

            this.storageMenu = new InventoryMenu(0, 0, false, this.storage, WoodPileMenu.IsWoodOrHardwood, capacity, rows);
            this.playerInventoryMenu = new InventoryMenu(0, 0, true, Game1.player.Items, null, -1, 3);

            int contentWidth = Math.Max(this.storageMenu.width, this.playerInventoryMenu.width);
            this.width = contentWidth + Margin * 2;
            this.height = this.storageMenu.height + SectionGap + this.playerInventoryMenu.height + Margin * 2;

            this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
            this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2;

            int storageX = this.xPositionOnScreen + (this.width - this.storageMenu.width) / 2;
            int storageY = this.yPositionOnScreen + Margin;
            this.storageMenu.SetPosition(storageX, storageY);

            int playerX = this.xPositionOnScreen + (this.width - this.playerInventoryMenu.width) / 2;
            int playerY = storageY + this.storageMenu.height + SectionGap;
            this.playerInventoryMenu.SetPosition(playerX, playerY);

            this.initializeUpperRightCloseButton();
        }

        /// <inheritdoc />
        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (this.upperRightCloseButton != null && this.upperRightCloseButton.containsPoint(x, y))
            {
                Game1.playSound("bigDeSelect");
                this.exitThisMenu(true);
                return;
            }

            int storageIndex = this.storageMenu.getInventoryPositionOfClick(x, y);
            if (storageIndex >= 0 && storageIndex < this.storage.Count)
            {
                this.TakeFromStorage(storageIndex, takeAll: true);
                return;
            }

            int playerIndex = this.playerInventoryMenu.getInventoryPositionOfClick(x, y);
            if (playerIndex >= 0 && playerIndex < Game1.player.Items.Count)
                this.DepositFromPlayer(playerIndex, depositAll: true);
        }

        /// <inheritdoc />
        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            int storageIndex = this.storageMenu.getInventoryPositionOfClick(x, y);
            if (storageIndex >= 0 && storageIndex < this.storage.Count)
            {
                this.TakeFromStorage(storageIndex, takeAll: false);
                return;
            }

            int playerIndex = this.playerInventoryMenu.getInventoryPositionOfClick(x, y);
            if (playerIndex >= 0 && playerIndex < Game1.player.Items.Count)
                this.DepositFromPlayer(playerIndex, depositAll: false);
        }

        /// <inheritdoc />
        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.5f);

            IClickableMenu.drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);

            this.storageMenu.draw(b);
            this.playerInventoryMenu.draw(b);

            this.upperRightCloseButton?.draw(b);

            this.drawMouse(b);
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Whether an item is wood or hardwood, the only item types the wood pile can store.</summary>
        private static bool IsWoodOrHardwood(Item item)
        {
            return item != null
                && (item.QualifiedItemId == ModEntry.WoodQualifiedItemId || item.QualifiedItemId == ModEntry.HardwoodQualifiedItemId);
        }

        /// <summary>Move some or all of a storage slot's contents into the player's inventory.</summary>
        private void TakeFromStorage(int index, bool takeAll)
        {
            Item item = this.storage[index];
            if (item == null)
                return;

            int amount = takeAll ? item.Stack : 1;
            Item toGive = item.getOne();
            toGive.Stack = amount;

            Item? leftover = Game1.player.addItemToInventory(toGive);
            int given = amount - (leftover?.Stack ?? 0);

            if (given <= 0)
            {
                Game1.playSound("cancel");
                return;
            }

            item.Stack -= given;
            if (item.Stack <= 0)
                this.storage[index] = null!;

            Game1.playSound("coin");
        }

        /// <summary>Move some or all of a player inventory slot's wood/hardwood into the wood pile.</summary>
        private void DepositFromPlayer(int index, bool depositAll)
        {
            Item item = Game1.player.Items[index];
            if (item == null || !WoodPileMenu.IsWoodOrHardwood(item))
                return;

            int remaining = depositAll ? item.Stack : 1;
            int toDeposit = remaining;

            for (int i = 0; i < this.storage.Count && remaining > 0; i++)
            {
                Item slot = this.storage[i];
                if (slot != null && slot.QualifiedItemId == item.QualifiedItemId)
                {
                    int space = slot.getRemainingStackSpace();
                    int toMove = Math.Min(space, remaining);
                    slot.Stack += toMove;
                    remaining -= toMove;
                }
            }

            for (int i = 0; i < this.storage.Count && remaining > 0; i++)
            {
                if (this.storage[i] == null)
                {
                    int toMove = Math.Min(item.maximumStackSize(), remaining);
                    Item newStack = item.getOne();
                    newStack.Stack = toMove;
                    this.storage[i] = newStack;
                    remaining -= toMove;
                }
            }

            int moved = toDeposit - remaining;
            if (moved <= 0)
            {
                Game1.playSound("cancel");
                return;
            }

            item.Stack -= moved;
            if (item.Stack <= 0)
                Game1.player.removeItemFromInventory(item);

            Game1.playSound("coin");
        }
    }
}
