using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.UserInterface;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotKeyHUD
{
    public sealed class HotKeyButton : Panel
    {
        private enum ComponentSlot
        {
            IconPanel = 1,
            ButtonKeyLabel,
            ButtonConditionBarBackground,
            ButtonConditionBar,
            ButtonStackLabel
        }

        public const float buttonWidth = 22f;
        public const float buttonHeight = 22f;
        private const float maxCondBarWidth = buttonWidth - 3f;
        private const float condBarHeight = 1f;
        private const float iconsWidth = buttonWidth * 10f;
        private const float iconsY = 177f;
        private readonly Vector2 originalPosition;
        public bool ForceUse { get; set; }
        public object Payload { get; set; }
        public byte PositionIndex { get; set; }
        public Panel Icon => (Panel)Components[(int)ComponentSlot.IconPanel];
        public TextLabel KeyLabel => (TextLabel)Components[(int)ComponentSlot.ButtonKeyLabel];
        public TextLabel StackLabel => (TextLabel)Components[(int)ComponentSlot.ButtonStackLabel];
        public Panel ConditionBarBackground => (Panel)Components[(int)ComponentSlot.ButtonConditionBarBackground];
        public Panel ConditionBar => (Panel)Components[(int)ComponentSlot.ButtonConditionBar];
        private static Vector2 KeyLabelOriginalPos = new Vector2(1f, 1f);
        private static Vector2 StackLabelOriginalPos = new Vector2(1f, 14f);
        private static Vector2 CondBarOriginalPos = new Vector2(2f, buttonHeight - 3f);

        public HotKeyButton(Texture2D backdrop, Vector2 position, int keyIndex)
        {
            // Button Backdrop
            BackgroundColor = Color.black;
            BackgroundTexture = backdrop;
            Size = new Vector2 { x = buttonWidth + 1f, // Scaling workaround (width > height)
                                 y = buttonHeight };
            Position = position;
            originalPosition = position;

            // Payload Icon
            Components.Add(new Panel
            {
                BackgroundColor = Color.clear,
                AutoSize = AutoSizeModes.ScaleToFit,
                MaxAutoScale = 1f,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Middle
            });

            // Key # Label
            Components.Add(new TextLabel
            {
                Position = KeyLabelOriginalPos,
                HorizontalAlignment = HorizontalAlignment.None,
                Text = keyIndex.ToString(),
                ShadowColor = DaggerfallUI.DaggerfallDefaultShadowColor,
                ShadowPosition = DaggerfallUI.DaggerfallDefaultShadowPos
            });

            // Item condition bar background
            Components.Add(new Panel
            {
                Position = CondBarOriginalPos,
                Size = new Vector2(maxCondBarWidth, condBarHeight),
                BackgroundColor = Color.black,
                Enabled = false
            });

            // Item condition bar
            Components.Add(new Panel
            {
                Position = CondBarOriginalPos,
                Size = new Vector2(maxCondBarWidth, condBarHeight),
                BackgroundColor = Color.green,
                Enabled = false
            });

            // Stack # Label
            Components.Add(new TextLabel
            {
                Position = StackLabelOriginalPos,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.None,
                Text = string.Empty,
                Enabled = false,
                ShadowColor = DaggerfallUI.DaggerfallDefaultShadowColor,
                ShadowPosition = DaggerfallUI.DaggerfallDefaultShadowPos
            });

            PositionIndex = (byte)(keyIndex - 1);
        }

        public override void Update()
        {
            base.Update();
            if (Payload is DaggerfallUnityItem item)
            {
                // Update stack count.
                if (StackLabel.Enabled)
                    StackLabel.Text = IsBow(item) ? GetArrowCount().ToString() : item.stackCount.ToString();
                // Update condition bar.
                ConditionBar.Size = new Vector2(item.ConditionPercentage / 100f * (maxCondBarWidth * Parent.Scale.x), condBarHeight * Parent.Scale.y);
                if (item.ConditionPercentage >= 70)
                    ConditionBar.BackgroundColor = Color.green;
                else if (item.ConditionPercentage >= 20)
                    ConditionBar.BackgroundColor = Color.yellow;
                else
                    ConditionBar.BackgroundColor = Color.red;
            }
        }

        public void SetItem(DaggerfallUnityItem item, bool forceUse = false)
        {
            // Toggle clear slot.
            if (item != null && item == Payload)
            {
                SetItem(null);
                return;
            }

            Payload = item;
            ForceUse = forceUse;
            if (item == null)
            {
                Icon.BackgroundTexture = null;
                ConditionBarBackground.Enabled = false;
                ConditionBar.Enabled = false;
                StackLabel.Enabled = false;
            }
            else
            {
                var image = DaggerfallUnity.Instance.ItemHelper.GetInventoryImage(item);
                Icon.BackgroundTexture = image.texture;
                Icon.Size = new Vector2(image.width == 0 ? image.texture.width : image.width, image.height == 0 ? image.texture.height : image.height);
                StackLabel.Enabled = item.IsStackable() || IsBow(item);
                // I'm assuming there aren't any stackables with condition worth tracking.
                ConditionBar.Enabled = !StackLabel.Enabled || IsBow(item);
                ConditionBarBackground.Enabled = ConditionBar.Enabled;
            }
        }

        public void SetSpell(in EffectBundleSettings spell)
        {
            const float spellIconScale = .8f;
            // Toggle clear slot.
            if (Payload is EffectBundleSettings settings && HotKeyUtil.CompareSpells(spell, settings))
            {
                SetItem(null);
                return;
            }

            Payload = spell;
            Icon.BackgroundTexture = DaggerfallUI.Instance.SpellIconCollection.GetSpellIcon(spell.Icon);
            Icon.Size = new Vector2(Icon.Parent.Size.x * spellIconScale, Icon.Parent.Size.y * spellIconScale);
            StackLabel.Enabled = false;
            ConditionBarBackground.Enabled = false;
            ConditionBar.Enabled = false;
        }

        public void HandleItemHotkeyPress(DaggerfallUnityItem item)
        {
            var equipTable = GameManager.Instance.PlayerEntity.ItemEquipTable;
            var player = GameManager.Instance.PlayerEntity;
            List<DaggerfallUnityItem> unequippedList = null;

            // Handle quest items
            // Note: copied from DaggerfallInventoryWindow
            // Handle quest items on use clicks
            if (item.IsQuestItem)
            {
                // Get the quest this item belongs to
                var quest = QuestMachine.Instance.GetQuest(item.QuestUID) ?? throw new Exception("DaggerfallUnityItem references a quest that could not be found.");

                // Get the Item resource from quest
                var questItem = quest.GetItem(item.QuestItemSymbol);

                // Use quest item
                if (!questItem.UseClicked && questItem.ActionWatching)
                {
                    questItem.UseClicked = true;

                    // Non-parchment and non-clothing items pop back to HUD so quest system has first shot at a custom click action in game world
                    // This is usually the case when actioning most quest items (e.g. a painting, bell, holy item, etc.)
                    // But when clicking a parchment or clothing item, this behaviour is usually incorrect (e.g. a letter to read)
                    if (!questItem.DaggerfallUnityItem.IsParchment && !questItem.DaggerfallUnityItem.IsClothing)
                    {
                        DaggerfallUI.Instance.PopToHUD();
                        return;
                    }
                }

                // Check for an on use value
                if (questItem.UsedMessageID != 0)
                {
                    // Display the message popup
                    quest.ShowMessagePopup(questItem.UsedMessageID, true);
                }
            }

            // Toggle light source.
            if (item.IsLightSource)
                player.LightSource = (player.LightSource == item ? null : item);
            // Refill lantern with oil
            // Note: Copied from DaggerfallInventoryWindow
            else if (item.ItemGroup == ItemGroups.UselessItems2 && item.TemplateIndex == (int)UselessItems2.Oil)
            {
                var lantern = player.Items.GetItem(ItemGroups.UselessItems2, (int)UselessItems2.Lantern, allowQuestItem: false);
                if (lantern != null && lantern.currentCondition <= lantern.maxCondition - item.currentCondition)
                {   // Re-fuel lantern with the oil.
                    lantern.currentCondition += item.currentCondition;
                    player.Items.RemoveItem(item.IsAStack() ? player.Items.SplitStack(item, 1) : item);
                    DaggerfallUI.Instance.PlayOneShot(SoundClips.MakePotion); // Audio feedback when using oil.
                }
                else
                    DaggerfallUI.MessageBox(TextManager.Instance.GetLocalizedText("lightFull"), false, lantern);
            }
            // Use enchanted item.
            if (item.IsEnchanted && (equipTable.GetEquipSlot(item) == EquipSlots.None || ForceUse))
            {
                var playerEffectManager = GameManager.Instance.PlayerEffectManager;
                if (playerEffectManager && !GameManager.Instance.PlayerSpellCasting.IsPlayingAnim)
                    GameManager.Instance.PlayerEffectManager.DoItemEnchantmentPayloads(EnchantmentPayloadFlags.Used, item, player.Items);
            }
            // Do drugs.
            // Note: Copied from DaggerfallInventoryWindow
            else if (item.ItemGroup == ItemGroups.Drugs)
            {
                // Drug poison IDs are 136 through 139. Template indexes are 78 through 81, so add to that.
                FormulaHelper.InflictPoison(player, player, (Poisons)item.TemplateIndex + 66, true);
                player.Items.RemoveItem(item);
            }
            // Consume potion.
            else if (item.IsPotion)
            {
                GameManager.Instance.PlayerEffectManager.DrinkPotion(item);
                player.Items.RemoveOne(item);
            }
            // Toggle item unequipped.
            else if (equipTable.IsEquipped(item))
            {
                equipTable.UnequipItem(item);
                player.UpdateEquippedArmorValues(item, false);
            }

            // Open the spellbook.
            else if (item.TemplateIndex == (int)MiscItems.Spellbook)
            {
                if (player.SpellbookCount() == 0)
                {
                    // Player has no spells
                    const int noSpellsTextId = 12;
                    var textTokens = DaggerfallUnity.Instance.TextProvider.GetRSCTokens(noSpellsTextId);
                    DaggerfallUI.MessageBox(textTokens);
                }
                else
                {
                    // Show spellbook
                    DaggerfallUI.UIManager.PostMessage(DaggerfallUIMessages.dfuiOpenSpellBookWindow);
                }
            }
            // Item is a mode of transportation.
            else if (item.ItemGroup == ItemGroups.Transportation)
            {
                if (GameManager.Instance.IsPlayerInside)
                    DaggerfallUI.AddHUDText(TextManager.Instance.GetLocalizedText("cannotChangeTransportationIndoors"));
                else if (GameManager.Instance.PlayerController.isGrounded)
                {
                    var transportManager = GameManager.Instance.TransportManager;
                    var mode = transportManager.TransportMode;
                    if (item.TemplateIndex == (int)Transportation.Small_cart && mode != TransportModes.Cart)
                        transportManager.TransportMode = TransportModes.Cart;
                    else if (item.TemplateIndex == (int)Transportation.Horse && mode != TransportModes.Horse)
                        transportManager.TransportMode = TransportModes.Horse;
                    else
                        transportManager.TransportMode = TransportModes.Foot;
                }
            }
            // Otherwise, use a non-equippable.
            else if (equipTable.GetEquipSlot(item) == EquipSlots.None)
            {
                // Try to use a delegate that may have been registered by a mod.
                if (DaggerfallUnity.Instance.ItemHelper.GetItemUseHandler(item.TemplateIndex, out ItemHelper.ItemUseHandler itemUseHandler))
                    itemUseHandler(item, player.Items);
                // Handle normal items
                else if (item.ItemGroup == ItemGroups.Books && !item.IsArtifact)
                {
                    DaggerfallUI.Instance.BookReaderWindow.OpenBook(item);
                    if (DaggerfallUI.Instance.BookReaderWindow.IsBookOpen)
                        DaggerfallUI.PostMessage(DaggerfallUIMessages.dfuiOpenBookReaderWindow);
                    else
                        DaggerfallUI.MessageBox(TextManager.Instance.GetLocalizedText("bookUnavailable"));
                }
            }
            // Toggle item equipped.
            else
                unequippedList = equipTable.EquipItem(item);

            // Handle equipped armor and list of unequipped items.
            if (unequippedList != null)
            {
                foreach (DaggerfallUnityItem unequippedItem in unequippedList)
                    player.UpdateEquippedArmorValues(unequippedItem, false);
                player.UpdateEquippedArmorValues(item, true);
            }
        }

        public void HandleSpellHotkeyPress(ref EffectBundleSettings spell)
        {
            // Note: Copied from DaggerfallSpellBookWindow with slight modification
            // Lycanthropes cast for free
            bool noSpellPointCost = spell.Tag == PlayerEntity.lycanthropySpellTag;

            // Assign to player effect manager as ready spell
            var playerEffectManager = GameManager.Instance.PlayerEffectManager;
            if (playerEffectManager && !GameManager.Instance.PlayerSpellCasting.IsPlayingAnim)
                playerEffectManager.SetReadySpell(new EntityEffectBundle(spell, GameManager.Instance.PlayerEntityBehaviour), noSpellPointCost);
        }

        public void SetScale(Vector2 scale)
        {
            var position = new Vector2((float)Math.Round((160f - iconsWidth / 2f + originalPosition.x + 0.5f) * scale.x) + .5f, (float)Math.Round(iconsY * scale.y) + .5f);
            var buttonSize = new Vector2((float)Math.Round(buttonWidth * scale.x + .5f), (float)Math.Round(buttonHeight * scale.y) + .5f);
            Position = position;
            Size = buttonSize;
            KeyLabel.Scale = scale;
            KeyLabel.Position = new Vector2((float)Math.Round(KeyLabelOriginalPos.x * scale.x + .5f), (float)Math.Round(KeyLabelOriginalPos.y * scale.y + .5f));
            KeyLabel.TextScale = scale.x;
            StackLabel.Scale = scale;
            StackLabel.Position = new Vector2((float)Math.Round(StackLabelOriginalPos.x * scale.x + .5f), (float)Math.Round(StackLabelOriginalPos.y * scale.y + .5f));
            StackLabel.TextScale = scale.x;
            ConditionBarBackground.Scale = scale;
            ConditionBarBackground.Position = new Vector2((float)Math.Round(CondBarOriginalPos.x * scale.x + .5f), (float)Math.Round(CondBarOriginalPos.y * scale.y + .5f));
            ConditionBarBackground.Size = new Vector2((float)Math.Round(maxCondBarWidth * scale.x + .5f), (float)Math.Round(condBarHeight * scale.y + .5f));
            ConditionBar.Scale = scale;
            ConditionBar.Position = new Vector2((float)Math.Round(CondBarOriginalPos.x * scale.x + .5f), (float)Math.Round(CondBarOriginalPos.y * scale.y + .5f));
            ConditionBar.Size = new Vector2((float)Math.Round(ConditionBar.Size.x * scale.x + .5f), (float)Math.Round(ConditionBar.Size.y * scale.y + .5f));
        }

        private static bool IsBow(DaggerfallUnityItem item)
        {
            return item.ItemGroup == ItemGroups.Weapons && (item.ItemTemplate.index == (int)Weapons.Long_Bow || item.ItemTemplate.index == (int)Weapons.Short_Bow);
        }

        private static int GetArrowCount()
        {
            var arrows = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.Weapons, (int)Weapons.Arrow, allowQuestItem: false, priorityToConjured: true);
            if (arrows != null)
                return arrows.stackCount;
            return 0;
        }
    }
}
