using AIs;
using ModAI.Enums;
using ModManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ModAI
{
    /// <summary>
    /// ModAI is a mod for Green Hell
    /// that enables the player to customize some AI behaviour
    /// and spawn in enemy waves and other creatures.
    /// Enable the mod UI by pressing Home.
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
        private static float ModScreenStartPositionX { get; set; } = Screen.width - ModScreenTotalWidth;
        private static float ModScreenStartPositionY { get; set; } = 0f;
        private static bool IsMinimized { get; set; } = false;
        private bool ShowUI;

        private static HUDManager LocalHUDManager;
        private static Player LocalPlayer;
        private static EnemyAISpawnManager LocalEnemyAISpawnManager;

        public static Rect ModAIScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenTotalHeight);
        public static Vector2 AISelectionScrollViewPosition;
        public static string CountEnemies { get; set; } = "3";
        public static string CountSpawnAI { get; set; } = "1";
        public static string SelectedAIName { get; set; } = string.Empty;
        public static int SelectedAIIndex { get; set; } = 0;
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

        public bool IsModActiveForMultiplayer { get; private set; }
        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

        public static string OnlyForSinglePlayerOrHostMessage() => $"Only available for single player or when host. Host can activate using ModManager.";
        public static string PermissionChangedMessage(string permission) => $"Permission to use mods and cheats in multiplayer was {permission}";
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
        }

        private void ModManager_onPermissionValueChanged(bool optionValue)
        {
            IsModActiveForMultiplayer = optionValue;
            ShowHUDBigInfo(
                          optionValue ?
                            HUDBigInfoMessage(PermissionChangedMessage($"granted"), MessageType.Info, Color.green)
                            : HUDBigInfoMessage(PermissionChangedMessage($"revoked"), MessageType.Info, Color.yellow)
                            );
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
            CursorManager.Get().ShowCursor(blockPlayer, false);

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
            if (Input.GetKeyDown(KeyCode.Home))
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

        private void InitData()
        {
            LocalHUDManager = HUDManager.Get();
            LocalPlayer = Player.Get();
            LocalEnemyAISpawnManager = EnemyAISpawnManager.Get();
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
                    GUILayout.Label($"Options for mod behaviour", GUI.skin.label);
                    using (var modScope = new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        StatusForMultiplayer();
                    }
                    GUILayout.Label($"Options for AI behaviour", GUI.skin.label);
                    using (var AIBehaviourScope = new GUILayout.VerticalScope(GUI.skin.box))
                    {
                        CanSwim = GUILayout.Toggle(CanSwim, $"Can swim?", GUI.skin.toggle);
                        IsHostile = GUILayout.Toggle(IsHostile, $"Is hostile?", GUI.skin.toggle);
                        IsHallucination = GUILayout.Toggle(IsHallucination, $"Is hallucination?", GUI.skin.toggle);
                    }
                    GUILayout.Label($"Options for player behaviour", GUI.skin.label);
                    using (var playerBehaviourScope = new GUILayout.VerticalScope(GUI.skin.box))
                    {
                        Cheats.m_GodMode = GUILayout.Toggle(IsGodModeCheatEnabled, $"Player God cheat mode enabled?", GUI.skin.toggle);
                        Cheats.m_ImmortalItems = GUILayout.Toggle(IsItemDecayCheatEnabled, $"Item decay cheat mode enabled?", GUI.skin.toggle);
                    }
                }
            }
            else
            {
                OnlyForSingleplayerOrWhenHostBox();
            }
        }

        private void StatusForMultiplayer()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                GUI.color = Color.green;
                GUILayout.Label(PermissionChangedMessage($"granted"), GUI.skin.label);
            }
            else
            {
                GUI.color = Color.yellow;
                GUILayout.Label(PermissionChangedMessage($"revoked"), GUI.skin.label);
            }
            GUI.color = Color.white;
        }

        private void SpawnWaveBox()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (var spawnWaveScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label("Spawn in a wave of enemies to your camp.", GUI.skin.label);
                    using (var actionScope = new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label("How many enemies?: ", GUI.skin.label);
                        CountEnemies = GUILayout.TextField(CountEnemies, GUI.skin.textField, GUILayout.MaxWidth(50f));
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
                GUI.color = Color.white;
            }
        }

        private void OnClickSpawnWaveButton()
        {
            try
            {
                int countEnemies = ValidMinMax(CountEnemies);
                if (countEnemies > 0)
                {
                    PlayerFireCampGroup = FirecampGroupsManager.Get().GetGroupToAttack();
                    HumanAIWave wave = LocalEnemyAISpawnManager.SpawnWave(countEnemies, IsHallucination, PlayerFireCampGroup);
                    if (wave != null && wave.m_Members != null && wave.m_Members.Count > 0)
                    {
                        StringBuilder info = new StringBuilder($"Wave of {wave.m_Members.Count} incoming!\n");
                        foreach (HumanAI humanAI in wave.m_Members)
                        {
                            humanAI.enabled = true;
                            humanAI.m_HostileStateModule.m_State = IsHostile ? HostileStateModule.State.Aggressive : HostileStateModule.State.Calm;
                            humanAI.m_Params.m_CanSwim = CanSwim;
                            info.AppendLine($"\t{humanAI.GetName().Replace("Clone", "")}\n");
                            info.AppendLine($"\t{(CanSwim ? "can swim" : "cannot swim")}\n");
                            info.AppendLine($"\t{(IsHostile ? "and is hostile." : "and is not hostile.")}\n");
                        }
                        ShowHUDBigInfo(HUDBigInfoMessage(info.ToString(), MessageType.Info, Color.green));
                    }
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
                using (var vertiScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    AISelectionScrollView();
                    using (var actionScope = new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label("Spawn how many?: ", GUI.skin.label);
                        CountSpawnAI = GUILayout.TextField(CountSpawnAI, GUI.skin.textField, GUILayout.MaxWidth(50f));
                        if (GUILayout.Button("Spawn selected", GUI.skin.button, GUILayout.MaxWidth(200f)))
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

        private static void AISelectionScrollView()
        {
            string[] aiNames = GetAINames();
            if (aiNames != null)
            {
                GUI.color = Color.cyan;
                GUILayout.Label($"Selected: {aiNames[SelectedAIIndex]}", GUI.skin.label);
                GUI.color = Color.white;
                GUILayout.Label("Select AI to spawn: ", GUI.skin.label);

                AISelectionScrollViewPosition = GUILayout.BeginScrollView(AISelectionScrollViewPosition, GUI.skin.scrollView, GUILayout.MinHeight(300f));
                SelectedAIIndex = GUILayout.SelectionGrid(SelectedAIIndex, aiNames, 3, GUI.skin.button);
                GUILayout.EndScrollView();
            }
        }

        private void OnClickSpawnAIButton()
        {
            try
            {
                int countSpawnAI = ValidMinMax(CountSpawnAI);
                if (countSpawnAI > 0)
                {
                    string[] aiNames = GetAINames();
                    SelectedAIName = aiNames[SelectedAIIndex];
                    if (!string.IsNullOrEmpty(SelectedAIName))
                    {
                        for (int i = 0; i < countSpawnAI; i++)
                        {
                            SpawnAI(SelectedAIName);
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(OnClickSpawnAIButton));
            }
        }

        private void SpawnAI(string aiName)
        {
            GameObject prefab = GreenHellGame.Instance.GetPrefab(aiName);
            if ((bool)prefab)
            {
                Vector3 forward = Camera.main.transform.forward;
                Vector3 position = LocalPlayer.GetHeadTransform().position + forward * 10f;

                AI ai = Instantiate(prefab, position, Quaternion.LookRotation(-forward, Vector3.up)).GetComponent<AI>();
                if (ai != null)
                {
                    ai.enabled = true;
                    ai.m_HostileStateModule.m_State = IsHostile ? HostileStateModule.State.Aggressive : HostileStateModule.State.Calm;
                    ai.m_Params.m_CanSwim = CanSwim;
                    StringBuilder info = new StringBuilder($"Spawned in ");
                    info.Append($"{ai.GetName()} at position {position} that\n");
                    info.AppendLine($"\t{(CanSwim ? "can swim" : "cannot swim")}\n");
                    info.AppendLine($"\t{(IsHostile ? "and is hostile." : "and is not hostile.")}\n");
                    ShowHUDBigInfo(HUDBigInfoMessage(info.ToString(), MessageType.Info, Color.green));
                }
            }
        }

        private int ValidMinMax(string countToValidate)
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
                ShowHUDBigInfo(HUDBigInfoMessage($"Invalid input {countToValidate}: please input numbers only - min. 1 and max. 5", MessageType.Error, Color.red));
                return -1;
            }
        }
    }
}
