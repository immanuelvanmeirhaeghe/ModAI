using AIs;
using ModAI.Enums;
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
    /// customize some AI behaviour and spawn in enemy waves and other creatures.
    /// Press Keypad8 (default) or the key configurable in ModAPI to open the main mod screen.
    /// </summary>
    public class ModAI : MonoBehaviour
    {
        private static ModAI Instance;

        private static readonly string ModName = nameof(ModAI);
        private static readonly float ModScreenTotalWidth = 850f;
        private static readonly float ModScreenTotalHeight = 500f;
        private static readonly float ModScreenMinWidth = 800f;
        private static readonly float ModScreenMaxWidth = 850f;
        private static readonly float ModScreenMinHeight = 50f;
        private static readonly float ModScreenMaxHeight = 550f;
        private static float ModScreenStartPositionX { get; set; } = Screen.width / 8f;
        private static float ModScreenStartPositionY { get; set; } = 0f;
        private static bool IsMinimized { get; set; } = false;
        private Color DefaultGuiColor = GUI.color;
        private bool ShowUI = false;

        private static CursorManager LocalCursorManager;
        private static HUDManager LocalHUDManager;
        private static Player LocalPlayer;
        private static EnemyAISpawnManager LocalEnemyAISpawnManager;
        private static FirecampGroupsManager LocalFirecampGroupsManager;
        private static AIManager LocalAIManager;

        public static Rect ModAIScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenTotalHeight);
        public static Vector2 AISelectionScrollViewPosition;
        public static string TribalsInWaveCount { get; set; } = "3";
        public static string SelectedAiCount { get; set; } = "1";
        public static string SelectedAiName { get; set; } = string.Empty;
        public static int SelectedAiIndex { get; set; } = 0;
        public static string[] GetAINames()
        {
            var aiNames = Enum.GetNames(typeof(AI.AIID));
            return aiNames;
        }
        public static FirecampGroup PlayerFireCampGroup { get; set; }

        public bool IsHostile { get; private set; } = true;
        public bool CanSwim { get; private set; } = false;
        public bool IsHallucination { get; private set; } = false;
        public bool IsGodModeCheatEnabled { get; private set; } = false;
        public bool IsItemDecayCheatEnabled { get; private set; } = false;

        public bool IsModActiveForMultiplayer { get; private set; } = false;
        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

        public static string OnlyForSinglePlayerOrHostMessage()
            => $"Only available for single player or when host. Host can activate using ModManager.";
        public static string PermissionChangedMessage(string permission, string reason)
            => $"Permission to use mods and cheats in multiplayer was {permission} because {reason}.";
        public static string HUDBigInfoMessage(string message, MessageType messageType, Color? headcolor = null)
            => $"<color=#{ (headcolor != null ? ColorUtility.ToHtmlStringRGBA(headcolor.Value) : ColorUtility.ToHtmlStringRGBA(Color.red))  }>{messageType}</color>\n{message}";

        public void ShowHUDBigInfo(string text)
        {
            string header = $"{ModName} Info";
            string textureName = HUDInfoLogTextureType.Count.ToString();

            HUDBigInfo bigInfo = (HUDBigInfo)LocalHUDManager.GetHUD(typeof(HUDBigInfo));
            HUDBigInfoData.s_Duration = 2f;
            HUDBigInfoData bigInfoData = new HUDBigInfoData
            {
                m_Header = header,
                m_Text = text,
                m_TextureName = textureName,
                m_ShowTime = Time.time
            };
            bigInfo.AddInfo(bigInfoData);
            bigInfo.Show(true);
        }

        public ModAI()
        {
            useGUILayout = true;
            Instance = this;
        }

        public void Start()
        {
            ModManager.ModManager.onPermissionValueChanged += ModManager_onPermissionValueChanged;
            ModKeybindingId = GetConfigurableKey(nameof(ModKeybindingId));
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

        private void Update()
        {
            if (Input.GetKeyDown(ModKeybindingId))
            {
                if (!ShowUI)
                {
                    InitData();
                    EnableCursor(true);
                }
                ToggleShowUI();
                if (!ShowUI)
                {
                    EnableCursor(false);
                }
            }
        }

        private static readonly string RuntimeConfigurationFile = Path.Combine(Application.dataPath.Replace("GH_Data", "Mods"), "RuntimeConfiguration.xml");
        private static KeyCode ModKeybindingId { get; set; } = KeyCode.Keypad8;
        private KeyCode GetConfigurableKey(string buttonId)
        {
            KeyCode configuredKeyCode = default;
            string configuredKeybinding = string.Empty;

            try
            {
                if (File.Exists(RuntimeConfigurationFile))
                {
                    using (var xmlReader = XmlReader.Create(new StreamReader(RuntimeConfigurationFile)))
                    {
                        while (xmlReader.Read())
                        {
                            if (xmlReader["ID"] == ModName)
                            {
                                if (xmlReader.ReadToFollowing(nameof(Button)) && xmlReader["ID"] == buttonId)
                                {
                                    configuredKeybinding = xmlReader.ReadElementContentAsString();
                                }
                            }
                        }
                    }
                }

                configuredKeybinding = configuredKeybinding?.Replace("NumPad", "Keypad").Replace("Oem", "");

                configuredKeyCode = (KeyCode)(!string.IsNullOrEmpty(configuredKeybinding)
                                                            ? Enum.Parse(typeof(KeyCode), configuredKeybinding)
                                                            : GetType().GetProperty(buttonId)?.GetValue(this));
                return configuredKeyCode;
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(GetConfigurableKey));
                configuredKeyCode = (KeyCode)(GetType().GetProperty(buttonId)?.GetValue(this));
                return configuredKeyCode;
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

        private void InitData()
        {
            LocalCursorManager = CursorManager.Get();
            LocalHUDManager = HUDManager.Get();
            LocalPlayer = Player.Get();
            LocalEnemyAISpawnManager = EnemyAISpawnManager.Get();
            LocalFirecampGroupsManager = FirecampGroupsManager.Get();
            LocalAIManager = AIManager.Get();
        }

        private void ToggleShowUI()
        {
            ShowUI = !ShowUI;
        }

        private void OnGUI()
        {
            if (ShowUI)
            {
                InitData();
                InitSkinUI();
                InitWindow();
            }
        }

        private void InitWindow()
        {
            int wid = GetHashCode();
            ModAIScreen = GUILayout.Window(wid, ModAIScreen, InitModAIScreen, ModName,
                                           GUI.skin.window,
                                           GUILayout.ExpandWidth(true),
                                           GUILayout.MinWidth(ModScreenMinWidth),
                                           GUILayout.MaxWidth(ModScreenMaxWidth),
                                           GUILayout.ExpandHeight(true),
                                           GUILayout.MinHeight(ModScreenMinHeight),
                                           GUILayout.MaxHeight(ModScreenMaxHeight));
        }

        private void ScreenMenuBox()
        {
            if (GUI.Button(new Rect(ModAIScreen.width - 40f, 0f, 20f, 20f), "-", GUI.skin.button))
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
            if (!IsMinimized)
            {
                ModAIScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenMinHeight);
                IsMinimized = true;
            }
            else
            {
                ModAIScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenTotalHeight);
                IsMinimized = false;
            }
            InitWindow();
        }

        private void CloseWindow()
        {
            ShowUI = false;
            EnableCursor(false);
        }

        private void InitModAIScreen(int windowId)
        {
            ModScreenStartPositionX = ModAIScreen.x;
            ModScreenStartPositionY = ModAIScreen.y;

            using (var modContentScope = new GUILayout.VerticalScope(GUI.skin.box))
            {
                ScreenMenuBox();
                if (!IsMinimized)
                {
                    ModOptionsBox();
                    SpawnWaveBox();
                    SpawnAIBox();
                }
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 10000f));
        }

        private void ModOptionsBox()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (var optionsScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label($"To toggle the main mod UI, press [{ModKeybindingId}]", GUI.skin.label);
                    MultiplayerOptionBox();
                    PlayerCheatOptionsBox();
                    AiOptionsBox();
                }
            }
            else
            {
                OnlyForSingleplayerOrWhenHostBox();
            }
        }

        private void PlayerCheatOptionsBox()
        {
            try
            {
                using (var playerBehaviourScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    CurrentlySetPlayerCheatOptionsInfoBox();

                    GUI.color = DefaultGuiColor;
                    GUILayout.Label($"Player cheat options: ", GUI.skin.label);
                    GodModeCheatOption();
                    ItemDecayCheatOption();
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(PlayerCheatOptionsBox));
            }
        }

        private void ItemDecayCheatOption()
        {
            try
            {
                GUI.color = DefaultGuiColor;
                bool _decayCheatEnabled = IsItemDecayCheatEnabled;
                IsItemDecayCheatEnabled = GUILayout.Toggle(IsItemDecayCheatEnabled, $"Switch to enable or disable item decay cheat mode", GUI.skin.toggle);
                if (_decayCheatEnabled != IsItemDecayCheatEnabled)
                {
                    Cheats.m_ImmortalItems = IsItemDecayCheatEnabled;
                    string _decayText = $"Item decay cheat mode has been { (IsItemDecayCheatEnabled ? "enabled" : "disabled") }";
                    ShowHUDBigInfo(HUDBigInfoMessage(_decayText, MessageType.Info, Color.green));
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(ItemDecayCheatOption));
            }
        }

        private void GodModeCheatOption()
        {
            try
            {
                GUI.color = DefaultGuiColor;
                bool _godModeCheatEnabled = IsGodModeCheatEnabled;
                IsGodModeCheatEnabled = GUILayout.Toggle(IsGodModeCheatEnabled, $"Switch to enable or disable player God cheat mode", GUI.skin.toggle);
                if (_godModeCheatEnabled != IsGodModeCheatEnabled)
                {
                    Cheats.m_GodMode = IsGodModeCheatEnabled;
                    string _godText = $"Player God cheat mode has been { (IsGodModeCheatEnabled ? "enabled" : "disabled") }";
                    ShowHUDBigInfo(HUDBigInfoMessage(_godText, MessageType.Info, Color.green));
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(GodModeCheatOption));
            }
        }

        private void AiOptionsBox()
        {
            try
            {
                using (var AIBehaviourScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    CurrentlySetAiOptionsInfoBox();

                    GUI.color = DefaultGuiColor;
                    GUILayout.Label($"AI options: ", GUI.skin.label);
                    CanSwimOption();
                    IsHostileOption();
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
                bool _isHallucinationValue = IsHallucination;
                IsHallucination = GUILayout.Toggle(IsHallucination, $"Switch to set for AI to be a hallucination or not", GUI.skin.toggle);
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
                bool _isHostileValue = IsHostile;
                IsHostile = GUILayout.Toggle(IsHostile, $"Switch to set for AI to become hostile or not", GUI.skin.toggle);
                if (_isHostileValue != IsHostile)
                {
                    if (LocalAIManager.m_EnemyAIs != null)
                    {
                        foreach (var enemyAi in LocalAIManager.m_EnemyAIs)
                        {
                            if (enemyAi.m_HostileStateModule != null)
                            {
                                enemyAi.m_HostileStateModule.m_State = IsHostile ? HostileStateModule.State.Aggressive : HostileStateModule.State.Calm;
                                enemyAi.InitializeModules();
                            }
                        }
                    }
                    string _optionText = $"AI is hostile has been { (IsHostile ? "enabled" : "disabled") }";
                    ShowHUDBigInfo(HUDBigInfoMessage(_optionText, MessageType.Info, Color.green));
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
                bool _canSwimValue = CanSwim;
                CanSwim = GUILayout.Toggle(CanSwim, $"Switch to set for AI to be able to swim or not", GUI.skin.toggle);
                if (_canSwimValue != CanSwim)
                {
                    if (LocalAIManager.m_ActiveAIs != null)
                    {
                        foreach (var activeAi in LocalAIManager.m_ActiveAIs)
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
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(CanSwimOption));
            }
        }

        private void MultiplayerOptionBox()
        {
            try
            {
                using (var multiplayeroptionsScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUI.color = DefaultGuiColor;
                    GUILayout.Label("Multiplayer options: ", GUI.skin.label);
                    string multiplayerOptionMessage = string.Empty;
                    if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
                    {
                        GUI.color = Color.green;
                        if (IsModActiveForSingleplayer)
                        {
                            multiplayerOptionMessage = $"you are the game host";
                        }
                        if (IsModActiveForMultiplayer)
                        {
                            multiplayerOptionMessage = $"the game host allowed usage";
                        }
                        _ = GUILayout.Toggle(true, PermissionChangedMessage($"granted", multiplayerOptionMessage), GUI.skin.toggle);
                    }
                    else
                    {
                        GUI.color = Color.yellow;
                        if (!IsModActiveForSingleplayer)
                        {
                            multiplayerOptionMessage = $"you are not the game host";
                        }
                        if (!IsModActiveForMultiplayer)
                        {
                            multiplayerOptionMessage = $"the game host did not allow usage";
                        }
                        _ = GUILayout.Toggle(false, PermissionChangedMessage($"revoked", $"{multiplayerOptionMessage}"), GUI.skin.toggle);
                    }
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(MultiplayerOptionBox));
            }
        }

        private void SpawnWaveBox()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (var spawnWaveScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUI.color = DefaultGuiColor;
                    GUILayout.Label("Set the above AI options. Set how many tribals you would like in a wave, then click [Spawn wave]", GUI.skin.label);
                    using (var actionScope = new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label("How many?: ", GUI.skin.label);
                        TribalsInWaveCount = GUILayout.TextField(TribalsInWaveCount, GUI.skin.textField, GUILayout.MaxWidth(50f));
                        if (GUILayout.Button("Spawn wave", GUI.skin.button, GUILayout.MaxWidth(200f)))
                        {
                            OnClickSpawnWaveButton();
                        }
                    }
                }
            }
            else
            {
                OnlyForSingleplayerOrWhenHostBox();
            }
        }

        private void OnlyForSingleplayerOrWhenHostBox()
        {
            using (var infoScope = new GUILayout.HorizontalScope(GUI.skin.box))
            {
                GUI.color = Color.yellow;
                GUILayout.Label(OnlyForSinglePlayerOrHostMessage(), GUI.skin.label);
            }
        }

        private void OnClickSpawnWaveButton()
        {
            try
            {
                int validatedTribalCount = ValidMinMax(TribalsInWaveCount);
                if (validatedTribalCount > 0)
                {
                    PlayerFireCampGroup = LocalFirecampGroupsManager.GetGroupToAttack();
                    HumanAIWave humanAiWave = LocalEnemyAISpawnManager.SpawnWave(validatedTribalCount, IsHallucination, PlayerFireCampGroup);
                    if (humanAiWave != null && humanAiWave.m_Members != null && humanAiWave.m_Members.Count > 0)
                    {
                        StringBuilder info = new StringBuilder($"Wave of {humanAiWave.m_Members.Count} tribals incoming!");
                        foreach (HumanAI humanAI in humanAiWave.m_Members)
                        {
                            humanAI.enabled = true;
                            if (humanAI.m_HostileStateModule != null)
                            {
                                humanAI.m_HostileStateModule.m_State = IsHostile ? HostileStateModule.State.Aggressive : HostileStateModule.State.Calm;
                            }
                            if (humanAI.m_Params != null)
                            {
                                humanAI.m_Params.m_CanSwim = CanSwim;
                            }

                            info.AppendLine($"{humanAI.GetName().Replace("Clone", "")} incoming");
                            info.AppendLine($"{(CanSwim ? "can swim" : "cannot swim")}");
                            info.AppendLine($"{(IsHostile ? "is hostile" : "is not hostile")}");
                            info.AppendLine($"and {(IsHallucination ? "as hallucination." : "as real ")}");
                        }
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

        private void HandleException(Exception exc, string methodName)
        {
            string info = $"[{ModName}:{methodName}] throws exception:\n{exc.Message}";
            ModAPI.Log.Write(info);
            ShowHUDBigInfo(HUDBigInfoMessage(info, MessageType.Error, Color.red));
        }

        private void SpawnAIBox()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (var spawnaiboxScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUI.color = DefaultGuiColor;
                    GUILayout.Label("Set the above AI options. Select an AI from the grid below. Set how many of the selected AI you would like, then click [Spawn AI].", GUI.skin.label);
                    AISelectionScrollViewBox();
                    using (var actionScope = new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label("How many?: ", GUI.skin.label);
                        SelectedAiCount = GUILayout.TextField(SelectedAiCount, GUI.skin.textField, GUILayout.MaxWidth(50f));
                        if (GUILayout.Button("Spawn AI", GUI.skin.button, GUILayout.MaxWidth(200f)))
                        {
                            OnClickSpawnAIButton();
                        }
                    }
                }
            }
            else
            {
                OnlyForSingleplayerOrWhenHostBox();
            }
        }

        private void CurrentlySetAiOptionsInfoBox()
        {
            GUI.color = Color.cyan;
            GUILayout.Label($"Currently set AI options: ", GUI.skin.label);
            GUILayout.Label($"Can swim { (CanSwim ? "enabled" : "disabled") }", GUI.skin.label);
            GUILayout.Label($"Is hostile { (IsHostile ? "enabled" : "disabled") }", GUI.skin.label);
            GUILayout.Label($"Is hallucination { (IsHallucination ? "enabled" : "disabled") }", GUI.skin.label);
            GUILayout.Label($"Currently selected AI: {SelectedAiName}", GUI.skin.label);
        }

        private void CurrentlySetPlayerCheatOptionsInfoBox()
        {
            GUI.color = Color.cyan;
            GUILayout.Label($"Currently set player cheat options: ", GUI.skin.label);
            GUILayout.Label($"Item decay cheat mode { (IsItemDecayCheatEnabled ? "enabled" : "disabled") }", GUI.skin.label);
            GUILayout.Label($"Player God cheat mode { (IsGodModeCheatEnabled ? "enabled" : "disabled") }", GUI.skin.label);
        }

        private void AISelectionScrollViewBox()
        {
            try
            {
                string[] aiNames = GetAINames();
                if (aiNames != null)
                {
                    GUILayout.Label("AI selection grid: ", GUI.skin.label);
                    AISelectionScrollViewPosition = GUILayout.BeginScrollView(AISelectionScrollViewPosition, GUI.skin.scrollView, GUILayout.MinHeight(300f));
                    SelectedAiIndex = GUILayout.SelectionGrid(SelectedAiIndex, aiNames, 3, GUI.skin.button);
                    SelectedAiName = aiNames[SelectedAiIndex];
                    GUILayout.EndScrollView();
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(AISelectionScrollViewBox));
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
                if ((bool)prefab)
                {
                    Vector3 forward = Camera.main.transform.forward;
                    Vector3 position = LocalPlayer.GetHeadTransform().position + forward * 10f;
                    ai = Instantiate(prefab, position, Quaternion.LookRotation(-forward, Vector3.up)).GetComponent<AI>();
                    if (ai != null)
                    {
                        ai.m_Hallucination = IsHallucination;
                        StringBuilder info = new StringBuilder($"Spawned in {ai.GetName()}");
                        info.AppendLine($"at position {position}");
                        info.AppendLine($"that {(CanSwim ? "can swim" : "cannot swim")}");
                        info.AppendLine($"{(IsHostile ? "is hostile" : "is not hostile")}");
                        info.AppendLine($"and {(IsHallucination ? "as hallucination." : "as real")}");
                        ShowHUDBigInfo(HUDBigInfoMessage(info.ToString(), MessageType.Info, Color.green));
                    }
                    else
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
