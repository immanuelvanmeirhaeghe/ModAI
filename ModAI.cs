using AIs;
using ModAI.Data.Enums;
using ModAI.Data.Interfaces;
using ModAI.Data.Modding;
using ModAI.Managers;
using ModManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEngine;
using UnityEngine.UI;

namespace ModAI
{
    /// <summary>
    /// ModAI is a mod for Green Hell that enables a player to use cheats,
    /// customize some AI behaviour
    /// and spawn in  enemy waves and other creatures.
    /// Press Keypad8 (default) or the key configurable in ModAPI to open the main mod screen.
    /// </summary>
    public class ModAI : MonoBehaviour
    {
        private static ModAI Instance;
        private static readonly string ModName = nameof(ModAI);
        private static readonly string RuntimeConfiguration = Path.Combine(Application.dataPath.Replace("GH_Data", "Mods"), $"{nameof(RuntimeConfiguration)}.xml");

        private static float ModAIScreenTotalWidth { get; set; } = 700f;
        private static float ModAIScreenTotalHeight { get; set; } = 150f;
        private static float ModAIScreenMinWidth { get; set; } = 700f;
        private static float ModAIScreenMaxWidth { get; set; } = Screen.width;
        private static float ModAIScreenMinHeight { get; set; } = 50f;
        private static float ModAIScreenMaxHeight { get; set; } = Screen.height;
        private static float ModAIScreenStartPositionX { get; set; } = Screen.width / 8f;
        private static float ModAIScreenStartPositionY { get; set; } = 0f;
        private bool IsModAIScreenMinimized { get; set; } = false;
        private static int ModAIScreenId { get; set; }
        private static Rect ModAIScreen = new Rect(ModAIScreenStartPositionX, ModAIScreenStartPositionY, ModAIScreenTotalWidth, ModAIScreenTotalHeight);
        private bool ShowModAIScreen { get; set; } = false;
        private bool ShowModInfo { get; set; } = false;
        private bool ShowCheatOptions { get; set; } = false;

        private static CursorManager LocalCursorManager;
        private static HUDManager LocalHUDManager;
        private static Player LocalPlayer;
        private static EnemyAISpawnManager LocalEnemyAISpawnManager;
        private static FirecampGroupsManager LocalFirecampGroupsManager;
        private static AIManager LocalAIManager;
        private static StylingManager LocalStylingManager;
        
        public Vector2 ModInfoScrollViewPosition { get;  set; }
        public IConfigurableMod SelectedMod { get; set; }
        public Vector2 AISelectionScrollViewPosition { get; set; }

        public string TribalsInWaveCount { get; set; } = "3";
        public string SelectedAiCount { get; set; } = "1";
        public string SelectedAiName { get; set; } = string.Empty;
        public int SelectedAiIndex { get; set; } = 0;
        public string[] GetAINames()
        {
            var aiNames = Enum.GetNames(typeof(AI.AIID));
            return aiNames;
        }
        public FirecampGroup PlayerFireCampGroup { get; set; }

        public string WaveWest { get; set; } = string.Empty;
        public string WaveSouth { get; set; } = string.Empty;
        public bool IsHostile { get; set; } = true;
        public bool CanSwim { get; set; } = false;
        public bool IsHallucination { get; set; } = false;
        public bool IsGodModeCheatEnabled { get; set; } = false;
        public bool IsItemDecayCheatEnabled { get; set; } = false;
        public bool IsGhostModeCheatEnabled { get; set; } = false;
        public bool IsOneShotAICheatEnabled { get; set; } = false;
        public bool IsOneShotDestroyCheatEnabled { get; set; } = false;

        public bool IsModActiveForMultiplayer { get; private set; } = false;
        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

        private string OnlyForSinglePlayerOrHostMessage()
                     => "Only available for single player or when host. Host can activate using ModManager.";
        private string PermissionChangedMessage(string permission, string reason)
            => $"Permission to use mods and cheats in multiplayer was {permission} because {reason}.";
        private string HUDBigInfoMessage(string message, MessageType messageType, Color? headcolor = null)
            => $"<color=#{(headcolor != null ? ColorUtility.ToHtmlStringRGBA(headcolor.Value) : ColorUtility.ToHtmlStringRGBA(Color.red))}>{messageType}</color>\n{message}";
        private void OnlyForSingleplayerOrWhenHostBox()
        {
            using (new GUILayout.HorizontalScope(GUI.skin.box))
            {
                GUILayout.Label(OnlyForSinglePlayerOrHostMessage(), LocalStylingManager.ColoredCommentLabel(Color.yellow));
            }
        }

        private void ModManager_onPermissionValueChanged(bool optionValue)
        {
            string reason = optionValue ? "the game host allowed usage" : "the game host did not allow usage";
            IsModActiveForMultiplayer = optionValue;

            ShowHUDBigInfo(
                          (optionValue ?
                            HUDBigInfoMessage(PermissionChangedMessage($"granted", $"{reason}"), MessageType.Info, Color.green)
                            : HUDBigInfoMessage(PermissionChangedMessage($"revoked", $"{reason}"), MessageType.Info, Color.yellow))
                            );
        }

        public void ShowHUDBigInfo(string text, float duration = 3f)
        {
            string header = $"{ModName} Info";
            string textureName = HUDInfoLogTextureType.Count.ToString();
            HUDBigInfo obj = (HUDBigInfo)LocalHUDManager.GetHUD(typeof(HUDBigInfo));
            HUDBigInfoData.s_Duration = duration;
            HUDBigInfoData data = new HUDBigInfoData
            {
                m_Header = header,
                m_Text = text,
                m_TextureName = textureName,
                m_ShowTime = Time.time
            };
            obj.AddInfo(data);
            obj.Show(show: true);
        }

        public void ShowHUDInfoLog(string itemID, string localizedTextKey)
        {
            Localization localization = GreenHellGame.Instance.GetLocalization();
            var messages = ((HUDMessages)LocalHUDManager.GetHUD(typeof(HUDMessages)));
            messages.AddMessage($"{localization.Get(localizedTextKey)}  {localization.Get(itemID)}");
        }

        protected virtual void Start()
        {
            ModManager.ModManager.onPermissionValueChanged += ModManager_onPermissionValueChanged;
            ShortcutKey = GetShortcutKey(nameof(ShortcutKey));
        }

        public ModAI()
        {
            useGUILayout = true;
            Instance = this;
        }

        public static ModAI Get()
        {
            return Instance;
        }

        private void InitSkinUI()
        {
            GUI.skin = ModAPI.Interface.Skin;
        }

        private void EnableCursor(bool blockPlayer = false)
        {
            LocalCursorManager.ShowCursor(blockPlayer, false);

            if (blockPlayer)
            {
                LocalPlayer.BlockMoves();
                LocalPlayer.BlockRotation();
                LocalPlayer.BlockInspection();
            }
            else
            {
                LocalPlayer.UnblockMoves();
                LocalPlayer.UnblockRotation();
                LocalPlayer.UnblockInspection();
            }
        }

        public KeyCode ShortcutKey { get; set; } = KeyCode.Keypad8;
       
        public KeyCode GetShortcutKey(string buttonID)
        {
            var ConfigurableModList = GetModList();
            if (ConfigurableModList != null && ConfigurableModList.Count > 0)
            {
                SelectedMod = ConfigurableModList.Find(cfgMod => cfgMod.ID == ModName);
                return SelectedMod.ConfigurableModButtons.Find(cfgButton => cfgButton.ID == buttonID).ShortcutKey;
            }
            else
            {
                return KeyCode.Keypad8;
            }
        }

        private List<IConfigurableMod> GetModList()
        {
            List<IConfigurableMod> modList = new List<IConfigurableMod>();
            try
            {
                if (File.Exists(RuntimeConfiguration))
                {
                    using (XmlReader configFileReader = XmlReader.Create(new StreamReader(RuntimeConfiguration)))
                    {
                        while (configFileReader.Read())
                        {
                            configFileReader.ReadToFollowing("Mod");
                            do
                            {
                                string gameID = GameID.GreenHell.ToString();
                                string modID = configFileReader.GetAttribute(nameof(IConfigurableMod.ID));
                                string uniqueID = configFileReader.GetAttribute(nameof(IConfigurableMod.UniqueID));
                                string version = configFileReader.GetAttribute(nameof(IConfigurableMod.Version));

                                var configurableMod = new ConfigurableMod(gameID, modID, uniqueID, version);

                                configFileReader.ReadToDescendant("Button");
                                do
                                {
                                    string buttonID = configFileReader.GetAttribute(nameof(IConfigurableModButton.ID));
                                    string buttonKeyBinding = configFileReader.ReadElementContentAsString();

                                    configurableMod.AddConfigurableModButton(buttonID, buttonKeyBinding);

                                } while (configFileReader.ReadToNextSibling("Button"));

                                if (!modList.Contains(configurableMod))
                                {
                                    modList.Add(configurableMod);
                                }

                            } while (configFileReader.ReadToNextSibling("Mod"));
                        }
                    }
                }
                return modList;
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(GetModList));
                modList = new List<IConfigurableMod>();
                return modList;
            }
        }

        private void HandleException(Exception exc, string methodName)
        {
            string info = $"[{ModName}:{methodName}] throws exception -  {exc.TargetSite?.Name}:\n{exc.Message}\n{exc.InnerException}\n{exc.Source}\n{exc.StackTrace}";
            ModAPI.Log.Write(info);
            Debug.Log(info);
        }

        protected virtual void Update()
        {
            if (Input.GetKeyDown(ShortcutKey))
            {
                if (!ShowModAIScreen)
                {
                    InitData();
                    EnableCursor(true);
                }
                ToggleShowUI(0);
                if (!ShowModAIScreen)
                {
                    EnableCursor(false);
                }
            }
        }

        protected virtual void InitData()
        {
            LocalCursorManager = CursorManager.Get();
            LocalHUDManager = HUDManager.Get();
            LocalPlayer = Player.Get();
            LocalEnemyAISpawnManager = EnemyAISpawnManager.Get();
            LocalFirecampGroupsManager = FirecampGroupsManager.Get();
            LocalAIManager = AIManager.Get();
            LocalStylingManager = StylingManager.Get();
        }

        private void ToggleShowUI(int controlId)
        {
            switch (controlId)
            {
                case 0:
                    ShowModAIScreen = !ShowModAIScreen;
                    return;
                case 3:
                    ShowModInfo = !ShowModInfo;
                    return;
                case 4:
                    ShowCheatOptions = !ShowCheatOptions;
                    return;
                default:
                    ShowModAIScreen = !ShowModAIScreen;
                    ShowModInfo = !ShowModInfo;
                    ShowCheatOptions = !ShowCheatOptions;
                    return;
            }
        }

        private void OnGUI()
        {
            if (ShowModAIScreen)
            {
                InitData();
                InitSkinUI();
                ShowModAIWindow();
            }
        }

        private void ShowModAIWindow()
        {
            if (ModAIScreenId <= 0)
            {
                ModAIScreenId = GetHashCode();
            }
            string modAIScreenTitle = $"{ModName} created by [Dragon Legion] Immaanuel#4300";
            ModAIScreen = GUILayout.Window(ModAIScreenId, ModAIScreen, InitModAIScreen, modAIScreenTitle,
                                           GUI.skin.window,
                                           GUILayout.ExpandWidth(true),
                                           GUILayout.MinWidth(ModAIScreenMinWidth),
                                           GUILayout.MaxWidth(ModAIScreenMaxWidth),
                                           GUILayout.ExpandHeight(true),
                                           GUILayout.MinHeight(ModAIScreenMinHeight),
                                           GUILayout.MaxHeight(ModAIScreenMaxHeight));
        }

        private void ScreenMenuBox()
        {
            string CollapseButtonText = IsModAIScreenMinimized ? "O" : "-";
            if (GUI.Button(new Rect(ModAIScreen.width - 40f, 0f, 20f, 20f), CollapseButtonText, GUI.skin.button))
            {
                CollapseWindow();
            }

            if (GUI.Button(new Rect(ModAIScreen.width - 20f, 0f, 20f, 20f), "X", GUI.skin.button))
            {
                CloseWindow();
            }
        }

        private void CollapseWindow()
        {
            if (!IsModAIScreenMinimized)
            {
                ModAIScreen = new Rect(ModAIScreenStartPositionX, ModAIScreenStartPositionY, ModAIScreenTotalWidth, ModAIScreenMinHeight);
                IsModAIScreenMinimized = true;
            }
            else
            {
                ModAIScreen = new Rect(ModAIScreenStartPositionX, ModAIScreenStartPositionY, ModAIScreenTotalWidth, ModAIScreenTotalHeight);
                IsModAIScreenMinimized = false;
            }
            ShowModAIWindow();
        }

        private void CloseWindow()
        {
            ShowModAIScreen = false;
            EnableCursor(false);
        }

        private void InitModAIScreen(int windowId)
        {
            ModAIScreenStartPositionX = ModAIScreen.x;
            ModAIScreenStartPositionY = ModAIScreen.y;

            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
                ScreenMenuBox();

                if (!IsModAIScreenMinimized)
                {
                    ModAIManagerBox();
                    CheatsManagerBox();
                    AIManagerBox();
                }
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 10000f));
        }

        private void AIManagerBox()
        {
            try
            {
                if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
                {
                    using (new GUILayout.VerticalScope(GUI.skin.box))
                    {
                        GUILayout.Label($"AI Manager", LocalStylingManager.ColoredHeaderLabel(Color.yellow));
                        GUILayout.Label($"AI Options", LocalStylingManager.ColoredSubHeaderLabel(Color.yellow));
                        AiOptionsBox();

                        SpawnWaveBox();
                        AISelectionScrollViewBox();
                        SpawnAIBox();
                        KillAiBox();
                    }
                }
                else
                {
                    OnlyForSingleplayerOrWhenHostBox();
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(AIManagerBox));
            }
        }

        private void ModAIManagerBox()
        {
            try
            {
                if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
                {
                    using (new GUILayout.VerticalScope(GUI.skin.box))
                    {
                        GUILayout.Label($"{ModName} Manager", LocalStylingManager.ColoredHeaderLabel(Color.yellow));
                        GUILayout.Label($"{ModName} Options", LocalStylingManager.ColoredSubHeaderLabel(Color.yellow));

                        if (GUILayout.Button($"Mod Info", GUI.skin.button))
                        {
                            ToggleShowUI(3);
                        }
                        if (ShowModInfo)
                        {
                            ModInfoBox();
                        }

                        MultiplayerOptionBox();
                    }
                }
                else
                {
                    OnlyForSingleplayerOrWhenHostBox();
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(ModAIManagerBox));
            }
        }

        private void ModInfoBox()
        {
            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
                ModInfoScrollViewPosition = GUILayout.BeginScrollView(ModInfoScrollViewPosition, GUI.skin.scrollView, GUILayout.MinHeight(150f));

                GUILayout.Label("Mod Info", LocalStylingManager.ColoredSubHeaderLabel(Color.cyan));

                using (new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{nameof(IConfigurableMod.GameID)}:", LocalStylingManager.FormFieldNameLabel);
                    GUILayout.Label($"{SelectedMod.GameID}", LocalStylingManager.FormFieldValueLabel);
                }
                using (new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{nameof(IConfigurableMod.ID)}:", LocalStylingManager.FormFieldNameLabel);
                    GUILayout.Label($"{SelectedMod.ID}", LocalStylingManager.FormFieldValueLabel);
                }
                using (var uidScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{nameof(IConfigurableMod.UniqueID)}:", LocalStylingManager.FormFieldNameLabel);
                    GUILayout.Label($"{SelectedMod.UniqueID}", LocalStylingManager.FormFieldValueLabel);
                }
                using (var versionScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{nameof(IConfigurableMod.Version)}:", LocalStylingManager.FormFieldNameLabel);
                    GUILayout.Label($"{SelectedMod.Version}", LocalStylingManager.FormFieldValueLabel);
                }

                GUILayout.Label("Buttons Info", LocalStylingManager.ColoredSubHeaderLabel(Color.cyan));

                foreach (var configurableModButton in SelectedMod.ConfigurableModButtons)
                {
                    using (var btnidScope = new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label($"{nameof(IConfigurableModButton.ID)}:", LocalStylingManager.FormFieldNameLabel);
                        GUILayout.Label($"{configurableModButton.ID}", LocalStylingManager.FormFieldValueLabel);
                    }
                    using (var btnbindScope = new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label($"{nameof(IConfigurableModButton.KeyBinding)}:", LocalStylingManager.FormFieldNameLabel);
                        GUILayout.Label($"{configurableModButton.KeyBinding}", LocalStylingManager.FormFieldValueLabel);
                    }
                }

                GUILayout.EndScrollView();
            }
        }

        private void MultiplayerOptionBox()
        {
            try
            {
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label("Multiplayer Options", LocalStylingManager.ColoredSubHeaderLabel(Color.yellow));

                    string multiplayerOptionMessage = string.Empty;
                    if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
                    {
                        if (IsModActiveForSingleplayer)
                        {
                            multiplayerOptionMessage = $"you are the game host";
                        }
                        if (IsModActiveForMultiplayer)
                        {
                            multiplayerOptionMessage = $"the game host allowed usage";
                        }
                        GUILayout.Label(PermissionChangedMessage($"granted", multiplayerOptionMessage), LocalStylingManager.ColoredFieldValueLabel(Color.green));
                    }
                    else
                    {
                        if (!IsModActiveForSingleplayer)
                        {
                            multiplayerOptionMessage = $"you are not the game host";
                        }
                        if (!IsModActiveForMultiplayer)
                        {
                            multiplayerOptionMessage = $"the game host did not allow usage";
                        }
                        GUILayout.Label(PermissionChangedMessage($"revoked", $"{multiplayerOptionMessage}"), LocalStylingManager.ColoredFieldValueLabel(Color.yellow));
                    }
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(MultiplayerOptionBox));
            }
        }

        private void CheatsManagerBox()
        {
            try
            {
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label($"Cheat manager", LocalStylingManager.ColoredHeaderLabel(Color.yellow));
                    GUILayout.Label($"Cheat Options", LocalStylingManager.ColoredSubHeaderLabel(Color.yellow));

                    if (GUILayout.Button($"Cheats", GUI.skin.button))
                    {
                        ToggleShowUI(4);
                    }
                    if (ShowCheatOptions)
                    {
                        CheatOptionsBox();
                    }                   
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(CheatsManagerBox));
            }
        }

        private void CheatOptionsBox()
        {
            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("These are all available cheats. You can switch any cheat on / off.", LocalStylingManager.TextLabel);

                Cheats.m_OneShotAI = GUILayout.Toggle(Cheats.m_OneShotAI, "One shot AI cheat on / off", GUI.skin.toggle);
                Cheats.m_OneShotConstructions = GUILayout.Toggle(Cheats.m_OneShotConstructions, "One shot constructions cheat on / off", GUI.skin.toggle);
                Cheats.m_GhostMode = GUILayout.Toggle(Cheats.m_GhostMode, "Ghost mode cheat on / off", GUI.skin.toggle);
                Cheats.m_GodMode = GUILayout.Toggle(Cheats.m_GodMode, "God mode cheat on / off", GUI.skin.toggle);
                Cheats.m_ImmortalItems = GUILayout.Toggle(Cheats.m_ImmortalItems, "No item decay cheat on / off", GUI.skin.toggle);
                Cheats.m_InstantBuild = GUILayout.Toggle(Cheats.m_InstantBuild, "Instant build cheat on / off", GUI.skin.toggle);
            }
        }

        private void AiOptionsBox()
        {
            try
            {
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label($"Switch to set for AI to be able to swim or not.", LocalStylingManager.FormFieldNameLabel);
                    CanSwimOption();

                    GUILayout.Label($"Switch to set for AI to become hostile or not", LocalStylingManager.FormFieldNameLabel);
                    IsHostileOption();

                    GUILayout.Label($"Switch to set for AI to be a hallucination or not", LocalStylingManager.FormFieldNameLabel);
                    IsHallucinationOption();
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(AiOptionsBox));
            }
        }

        private void IsHallucinationOption()
        {
            try
            {
                using (new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    bool _isHallucinationValue = IsHallucination;
                    IsHallucination = GUILayout.Toggle(IsHallucination, $"AI is a hallucination?", LocalStylingManager.FormFieldNameLabel);
                    if (_isHallucinationValue != IsHallucination)
                    {
                        if (LocalAIManager.m_ActiveAIs != null)
                        {
                            foreach (var activeAi in LocalAIManager.m_ActiveAIs)
                            {
                                activeAi.m_Hallucination = IsHallucination;
                                if (IsHallucination)
                                {
                                    activeAi.Disappear(true);
                                }
                                activeAi.InitializeModules();
                            }
                        }
                        string _optionText = $"AI is hallucination has been { (IsHallucination ? "enabled" : "disabled") }";
                        ShowHUDBigInfo(HUDBigInfoMessage(_optionText, MessageType.Info, Color.green));
                    }
                    if (IsHallucination)
                    {
                        GUILayout.Label($"enabled", LocalStylingManager.ColoredToggleFieldValueLabel(IsHallucination, Color.green, LocalStylingManager.DefaultColor));
                    }
                    else
                    {
                        GUILayout.Label($"disabled", LocalStylingManager.ColoredToggleFieldValueLabel(IsHallucination, Color.green, LocalStylingManager.DefaultColor));
                    }
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(IsHallucinationOption));
            }
        }

        private void IsHostileOption()
        {
            try
            {
                using (new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    bool _isHostileValue = IsHostile;
                    IsHostile = GUILayout.Toggle(IsHostile, $"AI is hostile?", LocalStylingManager.FormFieldNameLabel);
                    if (_isHostileValue != IsHostile)
                    {
                        if (LocalAIManager.m_ActiveAIs != null)
                        {
                            foreach (var activeAi in LocalAIManager.m_ActiveAIs)
                            {
                                if (activeAi.m_HostileStateModule != null)
                                {
                                    activeAi.m_HostileStateModule.m_State = IsHostile ? HostileStateModule.State.Aggressive : HostileStateModule.State.Calm;
                                    activeAi.InitializeModules();
                                }
                            }
                        }
                        string _optionText = $"AI is hostile has been { (IsHostile ? "enabled" : "disabled") }";
                        ShowHUDBigInfo(HUDBigInfoMessage(_optionText, MessageType.Info, Color.green));
                    }
                    if (IsHostile)
                    {
                        GUILayout.Label($"enabled", LocalStylingManager.ColoredToggleFieldValueLabel(IsHostile, Color.green, LocalStylingManager.DefaultColor));
                    }
                    else
                    { 
                        GUILayout.Label($"disabled", LocalStylingManager.ColoredToggleFieldValueLabel(IsHostile, Color.green, LocalStylingManager.DefaultColor));
                    }
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(IsHostileOption));
            }
        }

        private void CanSwimOption()
        {
            try
            {
                using (new GUILayout.HorizontalScope(GUI.skin.box))
                {                
                    bool _canSwimValue = CanSwim;
                    CanSwim = GUILayout.Toggle(CanSwim, $"AI can swim?", LocalStylingManager.FormFieldNameLabel);
                    if (_canSwimValue != CanSwim)
                    {
                        if (LocalAIManager?.m_ActiveAIs != null)
                        {
                            foreach (var activeAi in LocalAIManager?.m_ActiveAIs)
                            {
                                if (activeAi.m_Params != null)
                                {
                                    activeAi.m_Params.m_CanSwim = CanSwim;
                                    activeAi.InitializeModules();
                                }
                            }
                        }
                        string _optionText = $"AI can swim has been { (CanSwim ? "enabled" : "disabled") }";
                        ShowHUDBigInfo(HUDBigInfoMessage(_optionText, MessageType.Info, Color.green));
                    }
                    if (CanSwim)
                    {
                        GUILayout.Label($"enabled", LocalStylingManager.ColoredToggleFieldValueLabel(CanSwim, Color.green, LocalStylingManager.DefaultColor));
                    }
                    else
                    {
                        GUILayout.Label($"disabled", LocalStylingManager.ColoredToggleFieldValueLabel(CanSwim, Color.green, LocalStylingManager.DefaultColor));
                    }
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(CanSwimOption));
            }
        }

        private void SpawnWaveBox()
        {
            if (!IsGodModeCheatEnabled)
            {
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label("Here you can spawn in waves of enemies. Set how many enemies you would like in the wave. Click [Spawn wave] to start the attack.", LocalStylingManager.TextLabel);

                    using (var actionScope = new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label("Enemy count in wave", LocalStylingManager.TextLabel);
                        TribalsInWaveCount = GUILayout.TextField(TribalsInWaveCount, LocalStylingManager.FormInputTextField);
                        if (GUILayout.Button("Spawn wave", GUI.skin.button, GUILayout.Width(150f)))
                        {
                            OnClickSpawnWaveButton();
                        }
                    }
                }
            }
            else
            {
                using (var infoScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"To enable, please switch player cheat god mode off", LocalStylingManager.ColoredCommentLabel(Color.yellow));
                }
            }
        }

        private void OnClickSpawnWaveButton()
        {
            try
            {
                int validatedTribalCount = ValidMinMax(TribalsInWaveCount);
                if (validatedTribalCount > 0)
                {
                    HumanAIWave humanAiWave = LocalEnemyAISpawnManager?.SpawnWave(validatedTribalCount, IsHallucination, PlayerFireCampGroup);
                    if (humanAiWave != null && humanAiWave.m_Members != null && humanAiWave.m_Members.Count > 0)
                    {
                        Vector3 spawnPosition = humanAiWave.transform.position;
                        LocalPlayer.GetGPSCoordinates(spawnPosition, out int gps_lat, out int gps_long);
                        WaveWest = gps_lat.ToString();
                        WaveSouth = gps_long.ToString();

                        LocalEnemyAISpawnManager?.BlockSpawn();

                        StringBuilder info = new StringBuilder($"Spawned a wave of {humanAiWave.m_Members.Count} enemies");
                        info.Append($" at GPS coordinates W {WaveWest} S {WaveSouth}");
                        info.AppendLine($"");
                        info.Append($" Each enemy {(CanSwim ? "can swim" : "cannot swim")}, ");
                        info.Append($" {(IsHostile ? "is hostile" : "is not hostile")} ");
                        info.Append($" and {(IsHallucination ? "is a hallucination." : "is real")}.");
                        info.AppendLine($"");
                        foreach (HumanAI humanAI in humanAiWave.m_Members)
                        {
                            info.Append($"{humanAI.GetName().Replace("Clone", "").Replace("(", "").Replace(")", "")} incoming!");
                            if (humanAI.m_HostileStateModule != null)
                            {
                                humanAI.m_HostileStateModule.m_State = IsHostile ? HostileStateModule.State.Aggressive : HostileStateModule.State.Calm;
                            }
                            if (humanAI.m_Params != null)
                            {
                                humanAI.m_Params.m_CanSwim = CanSwim;
                            }
                            humanAI.InitializeModules();
                        }
                        humanAiWave.Initialize();
                        LocalEnemyAISpawnManager?.UnblockSpawn();
                        ShowHUDBigInfo(HUDBigInfoMessage(info.ToString(), MessageType.Info, Color.green));
                    }
                    else
                    {
                        ShowHUDBigInfo(HUDBigInfoMessage($"Something went wrong. Cannot spawn a wave of {validatedTribalCount}!", MessageType.Warning, Color.yellow));
                    }
                }
                else
                {
                    ShowHUDBigInfo(HUDBigInfoMessage($"Invalid input {validatedTribalCount}: please input numbers only - min. 1 and max. 5", MessageType.Warning, Color.yellow));
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(OnClickSpawnWaveButton));
            }
        }

        private void SpawnAIBox()
        {
            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label($"Here you can spawn in any selected being. Set how many AI beings you would like to spawn in, then click [Spawn {SelectedAiName}].", LocalStylingManager.TextLabel);

                GUILayout.Label($"Only a count between min. 1 and max. 5 beings is allowed!", LocalStylingManager.ColoredCommentLabel(Color.yellow) );

                using (var actionScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"AI count", LocalStylingManager.TextLabel);
                    SelectedAiCount = GUILayout.TextField(SelectedAiCount, LocalStylingManager.FormInputTextField);
                    if (GUILayout.Button($"Spawn {SelectedAiName}", GUI.skin.button, GUILayout.Width(150f)))
                    {
                        OnClickSpawnAIButton();
                    }
                }
            }
        }

        private void KillAiBox()
        {
            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label($"Here you can kill all occurrences in-game of a selected AI being type.", LocalStylingManager.TextLabel);

                using (new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"To kill all {SelectedAiName}", LocalStylingManager.TextLabel);
                    if (GUILayout.Button($"Execute {SelectedAiName}", GUI.skin.button, GUILayout.Width(150f)))
                    {
                        OnClickKillButton();
                    }
                }
            }           
        }

        private void OnClickKillButton()
        {
            try
            {
                if (!string.IsNullOrEmpty(SelectedAiName))
                {
                    if (LocalAIManager.m_ActiveAIs != null)
                    {
                        var toKill = LocalAIManager.m_ActiveAIs?.Where(an => an.GetName().Contains(SelectedAiName));
                        if (toKill != null)
                        {
                            foreach (AI ai in toKill)
                            {
                                DamageInfo damageInfo = new DamageInfo
                                {
                                    m_Damage = float.MaxValue,
                                    m_Damager = LocalPlayer.gameObject,
                                    m_Normal = Vector3.up,
                                    m_Position = ai.transform.position
                                };
                                ai.TakeDamage(damageInfo);
                            }
                        }
                    }

                    StringBuilder info = new StringBuilder($"Killed all {SelectedAiName}!");
                    ShowHUDBigInfo(HUDBigInfoMessage(info.ToString(), MessageType.Info, Color.green));
                }
                else
                {
                    ShowHUDBigInfo(HUDBigInfoMessage($"Please select AI to kill!", MessageType.Warning, Color.yellow));
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(OnClickKillButton));
            }
        }

        private void AISelectionScrollViewBox()
        {
            try
            {
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label("AI selection grid", LocalStylingManager.TextLabel);

                    AiSelectionScrollView();
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(AISelectionScrollViewBox));
            }
        }

        private void AiSelectionScrollView()
        {
            try
            {
                AISelectionScrollViewPosition = GUILayout.BeginScrollView(AISelectionScrollViewPosition, GUI.skin.scrollView, GUILayout.MinHeight(300f));

                string[] aiNames = GetAINames();
                if (aiNames != null)
                {
                    int _selectedAiIndex = SelectedAiIndex;
                    SelectedAiIndex = GUILayout.SelectionGrid(SelectedAiIndex, aiNames, 3, LocalStylingManager.ColoredSelectedGridButton(_selectedAiIndex!=SelectedAiIndex));
                    SelectedAiName = aiNames[SelectedAiIndex];
                }

                GUILayout.EndScrollView();
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(AiSelectionScrollView));
            }
        }

        private void OnClickSpawnAIButton()
        {
            try
            {
                int validatedSelectedAiCount = ValidMinMax(SelectedAiCount);
                if (validatedSelectedAiCount > 0)
                {
                    string[] aiNames = GetAINames();
                    SelectedAiName = aiNames[SelectedAiIndex];
                    if (!string.IsNullOrEmpty(SelectedAiName))
                    {
                        for (int i = 0; i < validatedSelectedAiCount; i++)
                        {
                            SpawnAI(SelectedAiName);
                        }
                        StringBuilder info = new StringBuilder($"Spawned in {SelectedAiCount} x  {SelectedAiName}");
                        ShowHUDBigInfo(HUDBigInfoMessage(info.ToString(), MessageType.Info, Color.green));
                    }
                }
                else
                {
                    ShowHUDBigInfo(HUDBigInfoMessage($"Invalid input {validatedSelectedAiCount}: please input numbers only - min. 1 and max. 5", MessageType.Warning, Color.yellow));
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(OnClickSpawnAIButton));
            }
        }

        private void SpawnAI(string aiName)
        {
            try
            {
                AI ai = default;
                GameObject prefab = GreenHellGame.Instance.GetPrefab(aiName);
                if (prefab != null)
                {
                    Vector3 forward = Camera.main.transform.forward;
                    Vector3 position = LocalPlayer.GetHeadTransform().position + forward * 10f;
                    ai = Instantiate(prefab, position, Quaternion.LookRotation(-forward, Vector3.up)).GetComponent<AI>();
                    if (ai == null)
                    {
                        ShowHUDBigInfo(HUDBigInfoMessage($"Something went wrong. Could not instantiate game object {aiName}!", MessageType.Warning, Color.yellow));
                    }
                }
                else
                {
                    ShowHUDBigInfo(HUDBigInfoMessage($"Something went wrong. Could not get prefab for {aiName}!", MessageType.Warning, Color.yellow));
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(SpawnAI));
            }
        }

        private int ValidMinMax(string countToValidate)
        {
            try
            {
                if (int.TryParse(countToValidate, out int count))
                {
                    if (count <= 0)
                    {
                        count = 1;
                    }
                    if (count > 5)
                    {
                        count = 5;
                    }
                    return count;
                }
                else
                {
                    return -1;
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(ValidMinMax));
                return 1;
            }
        }
    }
}
