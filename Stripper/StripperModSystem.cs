using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

namespace Stripper
{

    public enum StripState {
        Unknown = 0,
        Stripped = 1,
        Equipped = 2
    }

    public class StripperModSystem : ModSystem
    {


        private ICoreClientAPI capi;
        private ClientMain main;

        private ItemSlotCharacter headSlot;
        private ItemSlotCharacter bodySlot;
        private ItemSlotCharacter legSlot;
        private StripState state = StripState.Unknown;
        private bool hookedOnHurt = false;

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(capi);
            capi = api;
            main = (capi.World as ClientMain);

            capi.Input.RegisterHotKey(
                "stripper:swapout",
                Lang.Get("hotkey-stripper:swapout"),
                GlKeys.X,
                HotkeyType.CharacterControls,
                false,
                true,
                false
            );
            capi.Input.SetHotKeyHandler("stripper:swapout", SwapOut);
        }

        void InitSlots()
        {
            if (!hookedOnHurt)
            {
                capi.World.Player?.Entity?.WatchedAttributes.RegisterModifiedListener("onHurt", OnHurt);
                hookedOnHurt = true;
            }

            if (this.headSlot != null && this.bodySlot != null && this.legSlot != null)
            {
                return;
            }

            var player = capi.World.Player;
            var inventories = player.InventoryManager.Inventories;
            var charInv = inventories["character-" + player.PlayerUID];

            foreach (ItemSlotCharacter slot in charInv)
            {
                if (ItemSlotCharacter.IsDressType(slot.Itemstack, EnumCharacterDressType.ArmorHead) || slot.BackgroundIcon == "armorhead")
                {
                    this.headSlot = slot;
                }

                if (ItemSlotCharacter.IsDressType(slot.Itemstack, EnumCharacterDressType.ArmorBody) || slot.BackgroundIcon == "armorbody")
                {
                    this.bodySlot = slot;
                }

                if (ItemSlotCharacter.IsDressType(slot.Itemstack, EnumCharacterDressType.ArmorLegs) || slot.BackgroundIcon == "armorlegs")
                {
                    this.legSlot = slot;
                }
            }
        }

        int CountEquippedArmourPieces()
        {
            InitSlots();
            var count = 0;
            if (this.headSlot?.Itemstack != null) count++;
            if (this.bodySlot?.Itemstack != null) count++;
            if (this.legSlot?.Itemstack != null) count++;
            return count;
        }

        void OnHurt()
        {
            FindAndEquipArmour();
        }

        bool SwapOut(KeyCombination _)
        {
            var armourCount = CountEquippedArmourPieces();

            if (armourCount == 3)
            {
                StripArmour(); 
            } else if (armourCount == 0)
            {
                FindAndEquipArmour();
            } else if (state == StripState.Equipped)
            {
                StripArmour();
            } else if (state == StripState.Stripped)
            {
                FindAndEquipArmour();
            } else
            {
                // safe default
                FindAndEquipArmour();
            }

            return true;
        }

        void StripArmour()
        {
            if (this.headSlot != null)
            {
                ClearSlot(this.headSlot);
            }

            if (this.bodySlot != null)
            {
                ClearSlot(this.bodySlot);
            }

            if (this.legSlot != null)
            {
                ClearSlot(this.legSlot);
            }

            state = StripState.Stripped;

            capi.ShowChatMessage(Lang.Get("stripper:stripped"));
        }

        private bool ClearSlot(ItemSlot slot)
        {
            if (slot.Empty) return false;
            var op = new ItemStackMoveOperation(
                capi.World,
                EnumMouseButton.Left,
                EnumModifierKey.SHIFT,
                EnumMergePriority.AutoMerge
            );
            var packets = capi.World.Player.InventoryManager.TryTransferAway(slot, ref op, true, true);
            if (packets == null) return false;
            foreach (object packet in packets)
            {
                capi.Network.SendPacketClient(packet);
            }
            return true;
        }

        int FindAndEquipArmour()
        {
            InitSlots();

            int equipped = 0;

            if (this.headSlot?.Itemstack == null)
            {
                if (FindAndEquipArmourType(EnumCharacterDressType.ArmorHead, this.headSlot))
                    equipped++;
            }

            if (this.bodySlot?.Itemstack == null)
            {
                if (FindAndEquipArmourType(EnumCharacterDressType.ArmorBody, this.bodySlot))
                    equipped++;
            }

            if (this.legSlot?.Itemstack == null)
            {
                if (FindAndEquipArmourType(EnumCharacterDressType.ArmorLegs, this.legSlot))
                    equipped++;
            }

            if (equipped > 0)
            {
                state = StripState.Equipped;
                capi.ShowChatMessage(Lang.Get("stripper:equipped"));
            }

            return equipped;
        }

        bool FindAndEquipArmourType(EnumCharacterDressType type, ItemSlotCharacter chrSlot) 
        {
            var player = capi.World.Player;
            var inventories = player.InventoryManager.Inventories;

            var hotbarInv = inventories["hotbar-" + player.PlayerUID];
            var backpackInv = inventories["backpack-" + player.PlayerUID];

            List<ItemSlot> seekSlots = new List<ItemSlot>();
            foreach (ItemSlot invSlot in hotbarInv)
            {
                if (ItemSlotCharacter.IsDressType(invSlot.Itemstack, type))
                {
                    return EquipIntoSlot(invSlot, chrSlot);
                }
            }
            foreach (ItemSlot invSlot in backpackInv)
            {
                if (ItemSlotCharacter.IsDressType(invSlot.Itemstack, type))
                {
                    return EquipIntoSlot(invSlot, chrSlot);
                }
            }

            capi.ShowChatMessage(string.Format(Lang.Get("stripper:missingpiece"), type));
            return false;
        }

        bool EquipIntoSlot(ItemSlot srcSlot, ItemSlotCharacter destSlot)
        {
            if (srcSlot.Empty) return false;
            var op = new ItemStackMoveOperation(
                capi.World,
                EnumMouseButton.Left,
                EnumModifierKey.SHIFT,
                EnumMergePriority.AutoMerge,
                1
            );
            
            var packet = capi.World.Player.InventoryManager.TryTransferTo(srcSlot, destSlot, ref op);
            if (packet == null) return false;
            capi.Network.SendPacketClient(packet);
            return true;
        }


    }
}