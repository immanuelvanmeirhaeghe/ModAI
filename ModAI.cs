using AIs;
using ModManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ModAI
{
    public enum MessageType
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// ModAI is a mod for Green Hell
    /// that allows a player to custom setAI behaviour.
    /// (only in single player mode - Use ModManager for multiplayer).
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
        private static float ModScreenStartPositionX { get; set; } = 0f;
        private static float ModScreenStartPositionY { get; set; } = 0f;
        private static bool IsMinimized { get; set; } = false;
        private bool ShowUI;

        private static HUDManager LocalHUDManager;
        private static Player LocalPlayer;
        private static EnemyAISpawnManager LocalEnemyAISpawnManager;

        public static Rect ModAIScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenTotalHeight);
        public static string CountEnemies = "3";
        public static string SelectedAIName = string.Empty;
        public static int SelectedAIIndex = 0;
        public static string[] GetAINames()
        {
            var aiNames = Enum.GetNames(typeof(AI.AIID));
            return aiNames;
        }
        public static FirecampGroup PlayerFireCampGroup { get; set; }

        public bool IsHostile { get; private set; } = true;
        public bool CanSwim { get; private set; } = false;
        public bool IsHallucination { get; private set; }

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
                          (optionValue ?
                            HUDBigInfoMessage(PermissionChangedMessage($"granted"), MessageType.Info, Color.green)
                            : HUDBigInfoMessage(PermissionChangedMessage($"revoked"), MessageType.Info, Color.yellow))
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
                    SpawnEnemyWaveBox();
                    SpawnAIBox();
                }

            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 10000f));
        }
        private void ModOptionsBox()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (var optionsScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    CanSwim = GUILayout.Toggle(CanSwim, $"Can swim?", GUI.skin.toggle);
                    IsHostile = GUILayout.Toggle(IsHostile, $"Is hostile?", GUI.skin.toggle);
                    IsHallucination = GUILayout.Toggle(IsHallucination, $"Is hallucination?", GUI.skin.toggle);
                }
            }
            else
            {
                OnlyForSingleplayerOrWhenHostBox();
            }
        }

        private void SpawnEnemyWaveBox()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (var infoScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label("Spawn a wave of enemies to your camp.", GUI.skin.label);
                    using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
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
            using (var infoScope = new GUILayout.VerticalScope(GUI.skin.box))
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
                        StringBuilder info = new StringBuilder($"Wave spawned. {wave.m_Members.Count} enemies incoming!\n");
                        foreach (HumanAI humanAI in wave.m_Members)
                        {
                            info.AppendLine($"\t{humanAI.GetName().Replace("Clone", "")}\n");
                        }
                        ShowHUDBigInfo(HUDBigInfoMessage(info.ToString(), MessageType.Info, Color.green));
                    }
                }
            }
            catch (Exception exc)
            {
                string info = $"[{ModName}:{nameof(OnClickSpawnWaveButton)}] throws exception:\n{exc.Message}";
                ModAPI.Log.Write(info);
                ShowHUDBigInfo(HUDBigInfoMessage(info, MessageType.Error, Color.red));
            }
        }

        private void SpawnAIBox()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (var vertiScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label("Select AI to spawn. Then click Spawn AI", GUI.skin.label);
                    SelectedAIIndex = GUILayout.SelectionGrid(SelectedAIIndex, GetAINames(), 3, GUI.skin.button);
                    if (GUILayout.Button("Spawn AI", GUI.skin.button))
                    {
                        OnClickSpawnAIButton();
                        CloseWindow();
                    }
                }
            }
            else
            {
                using (var vertiScope2 = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label("Spawn AI", GUI.skin.label);
                    GUILayout.Label("is only for single player or when host", GUI.skin.label);
                    GUILayout.Label("Host can activate using ModManager.", GUI.skin.label);
                }
            }
        }

        private void OnClickSpawnAIButton()
        {
            try
            {
                string[] aiNames = GetAINames();
                SelectedAIName = aiNames[SelectedAIIndex];

                GameObject prefab = GreenHellGame.Instance.GetPrefab(SelectedAIName);
                if ((bool)prefab)
                {
                    Vector3 forward = Camera.main.transform.forward;
                    Vector3 position = LocalPlayer.GetHeadTransform().position + forward * 10f;

                    AI ai = Instantiate(prefab, position, Quaternion.LookRotation(-Camera.main.transform.forward, Vector3.up)).GetComponent<AI>();
                    if (ai != null)
                    {
                        StringBuilder info = new StringBuilder($"Spawned ");
                        info.Append($"{ai.GetName()} at position {position}");
                        ai.enabled = true;
                        ShowHUDBigInfo(HUDBigInfoMessage(info.ToString(), MessageType.Info, Color.green));
                    }
                }
            }
            catch (Exception exc)
            {
                string info = $"[{ModName}:{nameof(OnClickSpawnAIButton)}] throws exception:\n{exc.Message}";
                ModAPI.Log.Write(info);
                ShowHUDBigInfo(HUDBigInfoMessage(info, MessageType.Error, Color.red));
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
                if (count > 10)
                {
                    count = 10;
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
