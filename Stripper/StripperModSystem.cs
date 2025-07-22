using Cairo;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
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
        private ItemSlot handSlot;

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

            capi.Input.RegisterHotKey(
                "stripper:nightvision",
                Lang.Get("hotkey-stripper:nightvision"),
                GlKeys.V,
                HotkeyType.CharacterControls,
                false,
                false,
                false
            );
            capi.Input.SetHotKeyHandler("stripper:nightvision", ToggleNightVision);
        }

        ItemSlot FindNightVisionDevice(bool checkHead = true)
        {
            if (checkHead && headSlot?.Itemstack?.Item?.Code.Path == "nightvisiondevice")
            {
                return headSlot;
            }

            var nvHelm = FindItemSlot((invSlot) =>
            {
                return invSlot?.Itemstack?.Item?.Code.Path == "nightvisiondevice";
            });

            return nvHelm;
        }

        bool ToggleNightVision(KeyCombination _)
        {
            if (!InitSlots())
            {
                return false;
            }

            var nvHelm = FindNightVisionDevice();
            if (nvHelm != null)
            {
                if (this.headSlot != nvHelm)
                {
                    // equip nightvision
                    EquipIntoSlot(nvHelm, this.headSlot);
                    return true;
                } else
                {
                    // unequip night vision
                    ClearSlot(this.headSlot);
                    if (this.state == StripState.Equipped)
                    {
                        FindAndEquipArmourType(EnumCharacterDressType.ArmorHead, this.headSlot);
                    }
                    return true;
                }
            }

            string[] lightClasses = { "BlockLantern", "BlockTorch" };
            foreach (string lightClass in lightClasses)
            {
                var handLight = FindHandlight(lightClass);
                if (handLight != null)
                {
                    if (this.handSlot != handLight)
                    {
                        // equip lantern
                        EquipIntoSlot(handLight, this.handSlot);
                        return true;
                    }
                    else
                    {
                        // unequip lantern
                        ClearSlot(this.handSlot);
                        return true;
                    }
                }
            }

            capi.ShowChatMessage(Lang.Get("stripper:nolight"));

            return true;
        }

        ItemSlot FindHandlight(string lightClass, bool checkHand = true)
        {
            var handBlockClass = handSlot?.Itemstack?.Block?.Class;
            if (handBlockClass != null)
            {
                if (checkHand && lightClass == handBlockClass)
                {
                    return handSlot;
                }
            }

            var handLight = FindItemSlot((invSlot) =>
            {
                var blockClass = invSlot?.Itemstack?.Block?.Class;
                if (blockClass == null) return false;
                return lightClass == blockClass;
            });

            return handLight;
        }

        bool InitSlots()
        {
            if (!hookedOnHurt)
            {
                capi.World.Player?.Entity?.WatchedAttributes.RegisterModifiedListener("onHurt", OnHurt);
                hookedOnHurt = true;
            }

            if (this.headSlot != null && this.bodySlot != null && this.legSlot != null && this.handSlot != null)
            {
                return true;
            }

            var player = capi.World.Player;
            var inventories = player.InventoryManager.Inventories;
            var charInv = inventories["character-" + player.PlayerUID];
            var hotbarInv = inventories["hotbar-" + player.PlayerUID];

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

            foreach (ItemSlot slot in hotbarInv)
            {
                if (slot is ItemSlotOffhand)
                {
                    this.handSlot = slot;
                }
            }

            return (this.headSlot != null && this.bodySlot != null && this.legSlot != null && this.handSlot != null);
        }

        int CountEquippedArmourPieces()
        {
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
            if (!InitSlots())
            {
                return false;
            }

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

        ItemSlot FindItemSlot(System.Func<ItemSlot, bool> filter)
        {
            var player = capi.World.Player;
            var inventories = player.InventoryManager.Inventories;

            var hotbarInv = inventories["hotbar-" + player.PlayerUID];
            var backpackInv = inventories["backpack-" + player.PlayerUID];

            List<ItemSlot> seekSlots = new List<ItemSlot>();
            foreach (ItemSlot invSlot in hotbarInv)
            {
                if (filter(invSlot))
                {
                    return invSlot;
                }
            }
            foreach (ItemSlot invSlot in backpackInv)
            {
                if (filter(invSlot))
                {
                    return invSlot;
                }
            }

            return null;
        }

        bool FindAndEquipArmourType(EnumCharacterDressType type, ItemSlotCharacter chrSlot) 
        {
            var invSlot = FindItemSlot(invSlot =>
            {
                return ItemSlotCharacter.IsDressType(invSlot.Itemstack, type) && invSlot.Itemstack?.Item?.Code.Path != "nightvisiondevice";
            });

            if (invSlot != null)
            {
                return EquipIntoSlot(invSlot, chrSlot);
            }

            return false;
        }

        bool EquipIntoSlot(ItemSlot srcSlot, ItemSlot destSlot)
        {
            if (srcSlot.Empty) return false;

            if (!destSlot.Empty)
            {
                ClearSlot(destSlot);
            }
            
            var op = new ItemStackMoveOperation(
                capi.World,
                EnumMouseButton.Left,
                EnumModifierKey.SHIFT,
                EnumMergePriority.AutoMerge,
                srcSlot.StackSize
            );
            
            var packet = capi.World.Player.InventoryManager.TryTransferTo(srcSlot, destSlot, ref op);
            if (packet == null) return false;
            capi.Network.SendPacketClient(packet);
            return true;
        }


    }
}