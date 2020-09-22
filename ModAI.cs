using AIs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ModAI
{
    /// <summary>
    /// ModAI is a mod for Green Hell
    /// that allows a player to custom setAI behaviour.
    /// (only in single player mode - Use ModManager for multiplayer).
    /// Enable the mod UI by pressing Home.
    /// </summary>
    public class ModAI : MonoBehaviour
    {
        private static ModAI s_Instance;

        private static readonly string ModName = nameof(ModAI);

        private bool ShowUI;

        public Rect ModAIScreen = new Rect(800f, 800f, 450f, 150f);

        private static HUDManager hUDManager;

        private static Player player;

        private static EnemyAISpawnManager enemyAISpawnManager;

        public bool IsModAIActive = false;

        private static string SelectedAI = string.Empty;

        private static int SelectedAIIndex = 0;

        public static string[] GetAINames() => Enum.GetNames(typeof(AI.AIID));

        public bool IsHostile { get; private set; }

        public bool CanSwimOption { get; private set; }

        public bool IsModActiveForMultiplayer => FindObjectOfType(typeof(ModManager.ModManager)) != null && ModManager.ModManager.AllowModsForMultiplayer;
        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

        private static string CountEnemies = "3";

        public bool IsHallucination { get; private set; }

        public FirecampGroup PlayerFireCampGroup { get; private set; }


        public ModAI()
        {
            IsModAIActive = true;
            useGUILayout = true;
            s_Instance = this;
        }

        public static ModAI Get()
        {
            return s_Instance;
        }

        public static void ShowHUDInfoLog(string itemID, string localizedTextKey)
        {
            Localization localization = GreenHellGame.Instance.GetLocalization();
            ((HUDMessages)hUDManager.GetHUD(typeof(HUDMessages))).AddMessage(localization.Get(localizedTextKey) + "  " + localization.Get(itemID));
        }

        public static void ShowHUDBigInfo(string text, string header, string textureName)
        {
            HUDManager hUDManager = HUDManager.Get();

            HUDBigInfo hudBigInfo = (HUDBigInfo)hUDManager.GetHUD(typeof(HUDBigInfo));
            HUDBigInfoData hudBigInfoData = new HUDBigInfoData
            {
                m_Header = header,
                m_Text = text,
                m_TextureName = textureName,
                m_ShowTime = Time.time
            };
            hudBigInfo.AddInfo(hudBigInfoData);
            hudBigInfo.Show(true);
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
                player.BlockMoves();
                player.BlockRotation();
                player.BlockInspection();
            }
            else
            {
                player.UnblockMoves();
                player.UnblockRotation();
                player.UnblockInspection();
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
            hUDManager = HUDManager.Get();
            player = Player.Get();
            enemyAISpawnManager = EnemyAISpawnManager.Get();
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
            ModAIScreen = GUILayout.Window(wid, ModAIScreen, InitModAIScreen, $"{ModName}", GUI.skin.window);
        }

        private void CloseWindow()
        {
            ShowUI = false;
            EnableCursor(false);
        }

        private void InitModAIScreen(int windowId)
        {
            using (var verticalScope = new GUILayout.VerticalScope(GUI.skin.box))
            {
                if (GUI.Button(new Rect(430f, 0f, 20f, 20f), "X", GUI.skin.button))
                {
                    CloseWindow();
                }

                AICanSwimOption();

                SpawnEnemyWaveButton();

                SpawnAIButton();

            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 10000f));
        }

        private void SpawnEnemyWaveButton()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (var verticalScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label("Spawns a wave of enemies to your camp.", GUI.skin.label);
                    GUILayout.Label("Set how many enemies to spawn.", GUI.skin.label);
                    GUILayout.Label("Choose whether as a hallucination or not.", GUI.skin.label);
                }

                using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label("How many enemies?: ", GUI.skin.label);
                    CountEnemies = GUILayout.TextField(CountEnemies, GUI.skin.textField);
                }

                using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    IsHallucination = GUILayout.Toggle(IsHallucination, $"Is hallucination?", GUI.skin.toggle);

                    if (GUILayout.Button("Spawn Wave", GUI.skin.button, GUILayout.MinWidth(100f), GUILayout.MaxWidth(200f)))
                    {
                        OnClickSpawnWaveButton();
                        CloseWindow();
                    }
                }
            }
            else
            {
                using (var verticalScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label("Spawn enemy wave", GUI.skin.label);
                    GUILayout.Label("is only for single player or when host", GUI.skin.label);
                    GUILayout.Label("Host can activate using ModManager.", GUI.skin.label);
                }
            }
        }

        private void OnClickSpawnWaveButton()
        {
            try
            {
                var wave = enemyAISpawnManager.SpawnWave(Convert.ToInt32(CountEnemies), IsHallucination, PlayerFireCampGroup);
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(OnClickSpawnWaveButton)}] throws exception: {exc.Message}");
            }
        }

        private void SpawnAIButton()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label("Select AI to spawn. Then click Spawn AI", GUI.skin.label);
                    SelectedAIIndex = GUILayout.SelectionGrid(SelectedAIIndex, GetAINames(), 3, GUI.skin.button);

                    //IsHostile = GUILayout.Toggle(IsHostile, $"Is hostile?", GUI.skin.toggle);

                    if (GUILayout.Button("Spawn AI", GUI.skin.button, GUILayout.MinWidth(100f), GUILayout.MaxWidth(200f)))
                    {
                        OnClickSpawnAIButton();
                        CloseWindow();
                    }
                }
            }
            else
            {
                using (var verticalScope = new GUILayout.VerticalScope(GUI.skin.box))
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
                SelectedAI = aiNames[SelectedAIIndex];

                GameObject prefab = GreenHellGame.Instance.GetPrefab(SelectedAI);
                if ((bool)prefab)
                {
                    Vector3 forward = Camera.main.transform.forward;
                    AI ai = Instantiate(prefab, Player.Get().GetHeadTransform().position + forward * 10f, Quaternion.LookRotation(-Camera.main.transform.forward, Vector3.up)).GetComponent<AI>();
                    ai.m_Hallucination = IsHallucination;
                    ai.enabled = true;

                }

            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(OnClickSpawnWaveButton)}] throws exception: {exc.Message}");
            }
        }

        private void AICanSwimOption()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    CanSwimOption = GUILayout.Toggle(CanSwimOption, $"AI can swim ?", GUI.skin.toggle);
                }
            }
            else
            {
                using (var verticalScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label("AI can swim option", GUI.skin.label);
                    GUILayout.Label("is only for single player or when host", GUI.skin.label);
                    GUILayout.Label("Host can activate using ModManager.", GUI.skin.label);
                }
            }
        }
    }
}
