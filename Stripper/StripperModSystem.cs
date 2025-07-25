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
        private StripperConfig config;
        private ICoreClientAPI capi;
        private ClientMain main;

        private ItemSlotCharacter headSlot;
        private ItemSlotCharacter bodySlot;
        private ItemSlotCharacter legSlot;
        private ItemSlot handSlot;

        private StripState state = StripState.Unknown;
        private bool hookedOnHurt = false;
        private int lastHurtCounter = 0;

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(capi);
            capi = api;
            main = (capi.World as ClientMain);

            config = new StripperConfig(capi);

            capi.Event.IsPlayerReady += Init;
        }

        public ItemSlot FindEmptyItemSlot(ItemSlot compatibleItemSlot = null)
        {
            var player = capi.World.Player;
            var inventories = player.InventoryManager.Inventories;
            var backpackInv = inventories["backpack-" + player.PlayerUID];
            var hotbarInv = inventories["hotbar-" + player.PlayerUID];
            IInventory[] searchInvs = { backpackInv, hotbarInv };

            foreach (var searchInv in searchInvs)
            {
                foreach (ItemSlot slot in searchInv)
                {
                    if (slot.Empty && (compatibleItemSlot == null || slot.CanHold(compatibleItemSlot)))
                    {
                        return slot;
                    }
                }
            }

            return null;
        }

        private bool Init(ref EnumHandling handling)
        {
            capi.Input.RegisterHotKey(
                "stripper:swapout",
                Lang.Get("hotkey-stripper:swapout"),
                GlKeys.X,
                HotkeyType.CharacterControls,
                false,
                true,
                false
            );
            capi.Input.SetHotKeyHandler("stripper:swapout", HandleSwapOutHotkey);

            capi.Input.RegisterHotKey(
                "stripper:nightvision",
                Lang.Get("hotkey-stripper:nightvision"),
                GlKeys.V,
                HotkeyType.CharacterControls,
                false,
                false,
                false
            );
            capi.Input.SetHotKeyHandler("stripper:nightvision", HandleToggleNightVisionHotkey);

            capi.Input.RegisterHotKey(
                "stripper:helmtoggle",
                Lang.Get("hotkey-stripper:helmtoggle"),
                GlKeys.Unknown,
                HotkeyType.CharacterControls
            );
            capi.Input.SetHotKeyHandler("stripper:helmtoggle", HandleToggleHelmHotkey);

            InitSlots();
            return true;
        }

        public bool HandleToggleHelmHotkey(KeyCombination _)
        {
            try
            {
                if (headSlot.Empty)
                {
                    FindAndEquipArmourType(EnumCharacterDressType.ArmorHead, this.headSlot);
                }
                else
                {
                    ClearSlot(this.headSlot, capi.World.Player.InventoryManager.ActiveHotbarSlot);
                }
            } catch (Exception e)
            {
                capi.ShowChatMessage("[Stripper Error]: " + e.Message);
            }
            return true;
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

        bool HandleToggleNightVisionHotkey(KeyCombination _)
        {
            try { 

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
            }
            catch (Exception e)
            {
                capi.ShowChatMessage("[Stripper Error]: " + e.Message);
            }
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
                lastHurtCounter = capi.World.Player.Entity.WatchedAttributes.GetInt("onHurtCounter");
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
            int onHurtCounter = capi.World.Player.Entity.WatchedAttributes.GetInt("onHurtCounter");
            if (onHurtCounter == lastHurtCounter)
            {
                return;
            }

            lastHurtCounter = onHurtCounter;

            if (!config.data.EquipArmourOnTakeDamage) return;

            // Ideally, we only care about damage that can be mitigated by armour, as per this server-side code:
            // https://github.com/anegostudios/vssurvivalmod/blob/master/Systems/WearableStats.cs#L126
            // However, the damage type and source doesn't seem to be shared with the client - only the raw damage number & knockback direction:
            // https://github.com/anegostudios/vsapi/blob/master/Common/Entity/Entity.cs#L941

            // So, instead we'll just check if the damage is more than 1hp before suiting up.

            float damage = capi.World.Player.Entity.WatchedAttributes.GetFloat("onHurt");
            if (damage > config.data.EquipArmourOnDamageThreshold)
            {
                capi.ShowChatMessage(Lang.Get("stripper:panic", damage));
                FindAndEquipArmour();
            }
        }

        bool HandleSwapOutHotkey(KeyCombination _)
        {
            try { 
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
            }
            catch (Exception e)
            {
                capi.ShowChatMessage("[Stripper Error]: " + e.Message);
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

        private bool ClearSlot(ItemSlot slot, ItemSlot preferredSlot = null)
        {
            if (slot.Empty) return false;

            var emptySlot = (preferredSlot != null && preferredSlot.Empty) ? preferredSlot : FindEmptyItemSlot(slot);
            if (emptySlot == null) return false;
            
            var op = new ItemStackMoveOperation(
                capi.World,
                EnumMouseButton.Left,
                0,
                EnumMergePriority.AutoMerge,
                slot.StackSize
            );
            var packet = capi.World.Player.InventoryManager.TryTransferTo(slot, emptySlot, ref op);
            if (packet == null) return false;
            capi.Network.SendPacketClient(packet);
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