using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SweetJumpJump
{
    public sealed class BoardCellView : MonoBehaviour
    {
        public HexCoord Coord;
        public Button Button;
        public Image BaseImage;
        public Image SlotRingImage;
        public Image PieceShadowImage;
        public Image PieceImage;
        public Image HintImage;
        public Image SelectionRing;
    }

    internal struct ThemePalette
    {
        public Color Backdrop;
        public Color Panel;
        public Color Card;
        public Color Button;
        public Color ButtonHot;
        public Color Text;
        public Color MutedText;
        public Color Board;
        public Color Cell;
        public Color Target;
        public Color Selection;
    }

    public sealed class AppController : MonoBehaviour
    {
        private static AppController instance;
        private static Sprite cachedCircleSprite;
        private static Sprite cachedHexCellSprite;
        private static Sprite cachedHexGlowSprite;
        private static Sprite cachedRingSprite;
        private static Sprite cachedPieceSprite;
        private static Sprite cachedMountainSprite;

        private readonly Dictionary<HexCoord, BoardCellView> cellViews = new Dictionary<HexCoord, BoardCellView>();
        private readonly Dictionary<string, AudioClip> sfxClips = new Dictionary<string, AudioClip>();
        private readonly List<Button> gameChromeButtons = new List<Button>();

        private AppSaveData saveData;
        private RoomConfig selectedRoom;
        private GameSession session;
        private Canvas rootCanvas;
        private Font defaultFont;
        private AudioSource sfxSource;
        private AudioSource musicSource;
        private AudioClip musicClip;

        private RectTransform splashPanel;
        private RectTransform menuPanel;
        private RectTransform optionsPanel;
        private RectTransform roomsPanel;
        private RectTransform roomEditPanel;
        private RectTransform gamePanel;
        private RectTransform roomCardContainer;
        private RectTransform boardContainer;
        private RectTransform bottomControlBar;
        private GameObject victoryModal;

        private Text splashTitleText;
        private Text currentPlayerText;
        private Text statusText;
        private Text roomTitleText;
        private Text victoryText;
        private Text optionsSummaryText;
        private Text roomEditTitleText;
        private Text roomEditValidationText;

        private Button finishTurnButton;
        private Button passTurnButton;
        private Button undoButton;
        private Button startDefaultRoomButton;
        private Button ruleToggleButton;
        private Button themeToggleButton;
        private Button soundToggleButton;
        private Button musicToggleButton;
        private Button promptToggleButton;
        private Button promptIntervalButton;
        private Button roomRuleToggleButton;
        private Button roomThemeToggleButton;
        private Button roomSoundToggleButton;
        private Button roomMusicToggleButton;
        private Button roomPromptToggleButton;
        private Button roomPromptIntervalButton;
        private InputField roomNameInput;
        private readonly Dictionary<SlotId, Button> roomSlotButtons = new Dictionary<SlotId, Button>();
        private RoomConfig editingRoomDraft;
        private string editingRoomOriginalId;
        private string pendingDeleteRoomId;
        private float promptElapsedSeconds;
        private bool promptShown;
        private bool victorySoundPlayed;
        private ThemePalette activeTheme;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
            {
                return;
            }

            GameObject appObject = new GameObject("SweetJumpJumpApp");
            instance = appObject.AddComponent<AppController>();
            DontDestroyOnLoad(appObject);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            EnsureSceneScaffold();
            SetupAudio();
            saveData = SaveManager.Load();
            EnsureDefaultRoom();
            BuildUi();
            ApplyTheme(saveData.Options.ThemeId);
            RefreshMusicState();
            StartCoroutine(ShowSplashThenMenu());
        }

        private void Update()
        {
            UpdateSelectionPulse();
            RefreshMusicState();

            if (session == null || session.IsGameOver || selectedRoom == null)
            {
                return;
            }

            if (!selectedRoom.PromptEnabled || session.CurrentPlayerKind != PlayerKind.Human)
            {
                return;
            }

            promptElapsedSeconds += Time.deltaTime;
            if (!promptShown && promptElapsedSeconds >= selectedRoom.PromptIntervalSeconds)
            {
                promptShown = true;
                statusText.text = string.Format("{0}\n甜姐提醒：轮到你啦，请尽快下棋。", session.StatusMessage);
                PlaySfx("prompt");
            }
        }

        private void EnsureSceneScaffold()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(1f, 0.96f, 0.97f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;

            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }
        }

        private void SetupAudio()
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.volume = 0.45f;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.volume = 0.16f;

            sfxClips["button"] = CreateToneClip("button", 720f, 0.06f, 0.28f);
            sfxClips["select"] = CreateToneClip("select", 920f, 0.08f, 0.32f);
            sfxClips["move"] = CreateToneClip("move", 560f, 0.09f, 0.36f);
            sfxClips["invalid"] = CreateToneClip("invalid", 180f, 0.12f, 0.28f);
            sfxClips["prompt"] = CreateToneClip("prompt", 1040f, 0.18f, 0.25f);
            sfxClips["victory"] = CreateArpeggioClip("victory", new[] { 660f, 880f, 990f, 1320f }, 0.42f, 0.28f);
            musicClip = CreateMusicLoop();
            musicSource.clip = musicClip;
        }

        private void BuildUi()
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            rootCanvas = canvasObject.GetComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1536f, 2048f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.65f;

            DontDestroyOnLoad(canvasObject);

            CreateBackgroundDecor(canvasObject.transform);
            splashPanel = CreateFullscreenPanel("SplashPanel", canvasObject.transform, new Color(1f, 0.87f, 0.92f, 0.92f));
            menuPanel = CreateFullscreenPanel("MenuPanel", canvasObject.transform, new Color(1f, 0.94f, 0.96f, 0.95f));
            optionsPanel = CreateFullscreenPanel("OptionsPanel", canvasObject.transform, new Color(1f, 0.94f, 0.96f, 0.95f));
            roomsPanel = CreateFullscreenPanel("RoomsPanel", canvasObject.transform, new Color(1f, 0.95f, 0.98f, 0.96f));
            roomEditPanel = CreateFullscreenPanel("RoomEditPanel", canvasObject.transform, new Color(1f, 0.95f, 0.98f, 0.97f));
            gamePanel = CreateFullscreenPanel("GamePanel", canvasObject.transform, new Color(1f, 0.96f, 0.98f, 0.98f));

            BuildSplashPanel();
            BuildMenuPanel();
            BuildOptionsPanel();
            BuildRoomsPanel();
            BuildRoomEditPanel();
            BuildGamePanel();

            ShowPanel(splashPanel);
        }

        private IEnumerator ShowSplashThenMenu()
        {
            splashTitleText.text = "甜姐的跳跳棋";
            yield return new WaitForSeconds(3f);
            ShowMenu();
        }

        private void ShowMenu()
        {
            ApplyTheme(saveData.Options.ThemeId);
            ShowPanel(menuPanel);
        }

        private void ShowOptions()
        {
            ApplyTheme(saveData.Options.ThemeId);
            RefreshOptionsSummary();
            ShowPanel(optionsPanel);
        }

        private void ShowRooms()
        {
            ApplyTheme(saveData.Options.ThemeId);
            RefreshRoomCards();
            ShowPanel(roomsPanel);
        }

        private void ShowRoomEditor(RoomConfig roomConfig)
        {
            editingRoomOriginalId = roomConfig == null ? string.Empty : roomConfig.RoomId;
            editingRoomDraft = roomConfig == null
                ? BoardLayout.CreateNewRoom(saveData.Options, saveData.Rooms.Count + 1)
                : BoardLayout.CloneRoom(roomConfig);
            ApplyTheme(editingRoomDraft.ThemeId);
            RefreshRoomEditor();
            ShowPanel(roomEditPanel);
        }

        private void StartRoom(RoomConfig roomConfig)
        {
            selectedRoom = roomConfig;
            session = new GameSession(roomConfig);
            ResetPromptTimer();
            victorySoundPlayed = false;
            ApplyTheme(roomConfig.ThemeId);
            roomTitleText.text = roomConfig.RoomName;
            EnsureBoardCreated();
            RefreshBoard();
            victoryModal.SetActive(false);
            ShowPanel(gamePanel);
            StartCoroutine(RunAiTurnIfNeeded());
        }

        private IEnumerator RunAiTurnIfNeeded()
        {
            while (session != null && !session.IsGameOver && BoardLayout.IsAi(session.CurrentPlayerKind))
            {
                RefreshBoard();
                statusText.text = string.Format("{0}\nAI 思考中...", session.StatusMessage);
                yield return new WaitForSeconds(0.8f);
                MoveOption move = session.GetBestAiMove();
                session.ApplyAiMove(move);
                ResetPromptTimer();
                RefreshBoard();
                if (move != null)
                {
                    PlaySfx("move");
                    StartCoroutine(AnimatePiecePulse(move.FinalPosition, 1.18f, 0.18f));
                }
                yield return new WaitForSeconds(0.2f);
            }

            if (session != null && session.IsGameOver)
            {
                ShowVictory();
            }
        }

        private void RefreshBoard()
        {
            if (session == null)
            {
                return;
            }

            HashSet<HexCoord> legalTargets = new HashSet<HexCoord>(session.LegalTargets);

            foreach (KeyValuePair<HexCoord, BoardCellView> entry in cellViews)
            {
                BoardCellView view = entry.Value;
                PieceState piece = session.GetPieceAt(view.Coord);
                bool isTarget = legalTargets.Contains(view.Coord);
                bool isSelected = piece != null && piece.PieceId == session.SelectedPieceId;

                view.BaseImage.color = isTarget
                    ? activeTheme.Target
                    : activeTheme.Cell;
                if (view.SlotRingImage != null)
                {
                    view.SlotRingImage.color = isTarget
                        ? new Color(0.95f, 1f, 1f, 0.9f)
                        : new Color(0.33f, 0.38f, 0.42f, 0.4f);
                }

                view.HintImage.gameObject.SetActive(isTarget);
                view.SelectionRing.gameObject.SetActive(isSelected);
                view.SelectionRing.color = activeTheme.Selection;
                if (view.PieceShadowImage != null)
                {
                    view.PieceShadowImage.gameObject.SetActive(piece != null);
                }

                view.PieceImage.gameObject.SetActive(piece != null);

                if (piece != null)
                {
                    view.PieceImage.color = BoardLayout.GetPieceColor(piece.Owner);
                    if (!isSelected)
                    {
                        view.PieceImage.transform.localScale = Vector3.one;
                    }
                }
            }

            currentPlayerText.text = string.Format("当前玩家：{0}", session.CurrentPlayerLabel);
            statusText.text = session.StatusMessage;
            bool showPassButton = session.CanPass && !session.CanFinishTurn;
            finishTurnButton.gameObject.SetActive(!showPassButton);
            passTurnButton.gameObject.SetActive(showPassButton);
            finishTurnButton.interactable = session.CanFinishTurn;
            passTurnButton.interactable = session.CanPass;
            undoButton.interactable = session.CanUndo;
            RefreshGameChromeStyle();

            if (session.IsGameOver)
            {
                ShowVictory();
            }
        }

        private void ShowVictory()
        {
            victoryText.text = session == null ? string.Empty : session.WinnerLabel;
            victoryModal.SetActive(session != null && session.IsGameOver);
            finishTurnButton.interactable = false;
            passTurnButton.interactable = false;
            undoButton.interactable = false;
            if (!victorySoundPlayed && session != null && session.IsGameOver)
            {
                victorySoundPlayed = true;
                PlaySfx("victory");
                StartCoroutine(AnimateVictoryModal());
            }
        }

        private void HandleCellClicked(HexCoord coord)
        {
            if (session == null || session.IsGameOver || session.CurrentPlayerKind != PlayerKind.Human)
            {
                return;
            }

            string message;

            if (session.LegalTargets.Contains(coord))
            {
                if (session.TryMoveSelectedPiece(coord, out message))
                {
                    ResetPromptTimer();
                    PlaySfx("move");
                    RefreshBoard();
                    StartCoroutine(AnimatePiecePulse(coord, 1.18f, 0.18f));
                    StartCoroutine(RunAiTurnIfNeeded());
                    return;
                }
            }
            else if (session.TrySelectPiece(coord, out message))
            {
                ResetPromptTimer();
                PlaySfx("select");
                RefreshBoard();
                return;
            }

            PlaySfx("invalid");
            statusText.text = message;
        }

        private void HandleFinishTurn()
        {
            if (session == null)
            {
                return;
            }

            string message;
            if (session.TryFinishTurn(out message))
            {
                ResetPromptTimer();
                PlaySfx("button");
                RefreshBoard();
                StartCoroutine(RunAiTurnIfNeeded());
            }
            else
            {
                PlaySfx("invalid");
                statusText.text = message;
            }
        }

        private void HandlePassTurn()
        {
            if (session == null)
            {
                return;
            }

            string message;
            if (session.TryPassTurn(out message))
            {
                ResetPromptTimer();
                PlaySfx("button");
                RefreshBoard();
                StartCoroutine(RunAiTurnIfNeeded());
            }
            else
            {
                PlaySfx("invalid");
                statusText.text = message;
            }
        }

        private void HandleUndo()
        {
            if (session == null)
            {
                return;
            }

            string message;
            if (session.TryUndo(out message))
            {
                ResetPromptTimer();
                PlaySfx("button");
                RefreshBoard();
            }
            else
            {
                PlaySfx("invalid");
                statusText.text = message;
            }
        }

        private void ResetPromptTimer()
        {
            promptElapsedSeconds = 0f;
            promptShown = false;
        }

        private void EnsureDefaultRoom()
        {
            if (saveData == null)
            {
                saveData = new AppSaveData();
            }

            if (saveData.Options == null)
            {
                saveData.Options = BoardLayout.CreateDefaultOptions();
            }

            if (saveData.Rooms == null)
            {
                saveData.Rooms = new List<RoomConfig>();
            }

            if (saveData.SaveVersion < 2)
            {
                MigrateLegacySaveData();
                saveData.SaveVersion = 2;
            }

            if (saveData.SaveVersion < 3)
            {
                MigrateThemeDefaults();
                saveData.SaveVersion = 3;
            }

            RoomConfig defaultRoom = saveData.Rooms.FirstOrDefault(room => room.RoomId == "default-room");
            if (defaultRoom == null)
            {
                saveData.Rooms.Add(BoardLayout.CreateDefaultRoom(saveData.Options));
                saveData.Initialized = true;
            }
            else
            {
                defaultRoom.RoomName = string.IsNullOrWhiteSpace(defaultRoom.RoomName) ? "默认房间" : defaultRoom.RoomName;
                if (defaultRoom.Slots == null || defaultRoom.Slots.Count == 0)
                {
                    defaultRoom.Slots = new List<SlotConfig>
                    {
                        new SlotConfig { SlotId = SlotId.Bottom, PlayerKind = PlayerKind.Human },
                        new SlotConfig { SlotId = SlotId.Top, PlayerKind = PlayerKind.AiNormal }
                    };
                }
            }

            SaveManager.Save(saveData);
        }

        private void MigrateLegacySaveData()
        {
            for (int roomIndex = 0; roomIndex < saveData.Rooms.Count; roomIndex++)
            {
                RoomConfig room = saveData.Rooms[roomIndex];
                if (room == null || room.Slots == null)
                {
                    continue;
                }

                bool looksLikeStageOneDefault = room.RoomId == "default-room"
                    && room.Slots.Count == 2
                    && room.Slots.Any(slot => slot.SlotId == SlotId.Bottom && slot.PlayerKind == PlayerKind.Human)
                    && room.Slots.Any(slot => slot.SlotId == SlotId.Top && slot.PlayerKind == PlayerKind.AiBeginner);

                if (looksLikeStageOneDefault)
                {
                    SlotConfig topSlot = room.Slots.First(slot => slot.SlotId == SlotId.Top);
                    topSlot.PlayerKind = PlayerKind.AiNormal;
                }
            }
        }

        private void MigrateThemeDefaults()
        {
            if (string.IsNullOrWhiteSpace(saveData.Options.ThemeId))
            {
                saveData.Options.ThemeId = "pink";
            }

            for (int i = 0; i < saveData.Rooms.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(saveData.Rooms[i].ThemeId))
                {
                    saveData.Rooms[i].ThemeId = saveData.Options.ThemeId;
                }
            }
        }

        private void RefreshRoomCards()
        {
            for (int i = roomCardContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(roomCardContainer.GetChild(i).gameObject);
            }

            for (int i = 0; i < saveData.Rooms.Count; i++)
            {
                RoomConfig room = saveData.Rooms[i];
                GameObject card = new GameObject("RoomCard", typeof(Image));
                card.transform.SetParent(roomCardContainer, false);

                Image cardImage = card.GetComponent<Image>();
                cardImage.color = new Color(1f, 0.99f, 1f, 0.98f);

                VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(28, 28, 24, 24);
                layout.spacing = 14f;
                layout.childControlHeight = true;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;

                LayoutElement element = card.AddComponent<LayoutElement>();
                element.preferredHeight = 280f;

                CreateText("RoomName", card.transform, room.RoomName, 48, FontStyle.Bold, new Color(0.47f, 0.22f, 0.31f), TextAnchor.MiddleLeft);
                CreateText(
                    "RoomBody",
                    card.transform,
                    GetRoomSummary(room),
                    34,
                    FontStyle.Normal,
                    new Color(0.53f, 0.35f, 0.42f),
                    TextAnchor.UpperLeft);

                GameObject buttonRow = new GameObject("RoomButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                buttonRow.transform.SetParent(card.transform, false);
                HorizontalLayoutGroup rowLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 14f;
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = true;
                rowLayout.childForceExpandHeight = false;
                LayoutElement rowElement = buttonRow.AddComponent<LayoutElement>();
                rowElement.preferredHeight = 82f;

                Button startButton = CreateButton(buttonRow.transform, "开始", () => StartRoom(room), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 82f), Vector2.zero);
                LayoutElement buttonLayout = startButton.gameObject.AddComponent<LayoutElement>();
                buttonLayout.preferredHeight = 82f;

                Button editButton = CreateButton(buttonRow.transform, "编辑", () => ShowRoomEditor(room), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 82f), Vector2.zero);
                editButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 82f;

                string deleteLabel = pendingDeleteRoomId == room.RoomId ? "确认删除" : "删除";
                Button deleteButton = CreateButton(buttonRow.transform, deleteLabel, () => DeleteRoom(room), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 82f), Vector2.zero);
                deleteButton.interactable = saveData.Rooms.Count > 1;
                deleteButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 82f;

                if (i == 0)
                {
                    startDefaultRoomButton = startButton;
                }
            }
        }

        private string GetRoomSummary(RoomConfig room)
        {
            Dictionary<SlotId, PlayerKind> slotMap = BoardLayout.GetSlotMap(room);
            int playerCount = slotMap.Values.Count(kind => kind != PlayerKind.None);
            List<string> activeSlots = new List<string>();

            foreach (SlotId slotId in BoardLayout.GetSlotsInDisplayOrder())
            {
                if (slotMap[slotId] == PlayerKind.None)
                {
                    continue;
                }

                activeSlots.Add(string.Format("{0}:{1}", BoardLayout.GetSlotLabel(slotId), BoardLayout.GetPlayerKindLabel(slotMap[slotId])));
            }

            return string.Format(
                "{0} 人对局\n规则：{1}  主题：{2}  催促：{3}/{4}秒\n{5}",
                playerCount,
                BoardLayout.GetRuleLabel(room.RuleVariant),
                GetThemeLabel(room.ThemeId),
                room.PromptEnabled ? "开" : "关",
                room.PromptIntervalSeconds,
                string.Join("  ", activeSlots.ToArray()));
        }

        private void DeleteRoom(RoomConfig room)
        {
            if (room == null || saveData.Rooms.Count <= 1)
            {
                return;
            }

            if (pendingDeleteRoomId != room.RoomId)
            {
                pendingDeleteRoomId = room.RoomId;
                RefreshRoomCards();
                return;
            }

            saveData.Rooms.RemoveAll(value => value.RoomId == room.RoomId);
            pendingDeleteRoomId = string.Empty;
            SaveManager.Save(saveData);
            RefreshRoomCards();
        }

        private void RefreshRoomEditor()
        {
            if (editingRoomDraft == null)
            {
                return;
            }

            roomEditTitleText.text = string.IsNullOrEmpty(editingRoomOriginalId) ? "新建房间" : "编辑房间";
            roomNameInput.text = editingRoomDraft.RoomName;
            SetButtonLabel(roomRuleToggleButton, string.Format("规则：{0}", BoardLayout.GetRuleLabel(editingRoomDraft.RuleVariant)));
            SetButtonLabel(roomThemeToggleButton, string.Format("主题：{0}", GetThemeLabel(editingRoomDraft.ThemeId)));
            SetButtonLabel(roomSoundToggleButton, editingRoomDraft.SoundEnabled ? "音效：开" : "音效：关");
            SetButtonLabel(roomMusicToggleButton, editingRoomDraft.MusicEnabled ? "音乐：开" : "音乐：关");
            SetButtonLabel(roomPromptToggleButton, editingRoomDraft.PromptEnabled ? "催促：开" : "催促：关");
            SetButtonLabel(roomPromptIntervalButton, string.Format("催促间隔：{0} 秒", editingRoomDraft.PromptIntervalSeconds));

            Dictionary<SlotId, PlayerKind> slotMap = BoardLayout.GetSlotMap(editingRoomDraft);
            foreach (SlotId slotId in BoardLayout.GetSlotsInDisplayOrder())
            {
                SetButtonLabel(
                    roomSlotButtons[slotId],
                    string.Format("{0}：{1}", BoardLayout.GetSlotLabel(slotId), BoardLayout.GetPlayerKindLabel(slotMap[slotId])));
            }

            roomEditValidationText.text = string.Empty;
        }

        private void ToggleRoomRule()
        {
            editingRoomDraft.RuleVariant = editingRoomDraft.RuleVariant == RuleVariant.OnePieceJump
                ? RuleVariant.SpaceJump
                : RuleVariant.OnePieceJump;
            RefreshRoomEditor();
        }

        private void ToggleRoomTheme()
        {
            editingRoomDraft.ThemeId = GetNextThemeId(editingRoomDraft.ThemeId);
            ApplyTheme(editingRoomDraft.ThemeId);
            RefreshRoomEditor();
        }

        private void ToggleRoomSound()
        {
            editingRoomDraft.SoundEnabled = !editingRoomDraft.SoundEnabled;
            RefreshRoomEditor();
        }

        private void ToggleRoomMusic()
        {
            editingRoomDraft.MusicEnabled = !editingRoomDraft.MusicEnabled;
            RefreshRoomEditor();
        }

        private void ToggleRoomPrompt()
        {
            editingRoomDraft.PromptEnabled = !editingRoomDraft.PromptEnabled;
            RefreshRoomEditor();
        }

        private void CycleRoomPromptInterval()
        {
            editingRoomDraft.PromptIntervalSeconds = GetNextPromptInterval(editingRoomDraft.PromptIntervalSeconds);
            RefreshRoomEditor();
        }

        private void CycleRoomSlot(SlotId slotId)
        {
            Dictionary<SlotId, PlayerKind> slotMap = BoardLayout.GetSlotMap(editingRoomDraft);
            slotMap[slotId] = BoardLayout.GetNextPlayerKind(slotMap[slotId]);
            editingRoomDraft.Slots = slotMap
                .Where(pair => pair.Value != PlayerKind.None)
                .Select(pair => new SlotConfig { SlotId = pair.Key, PlayerKind = pair.Value })
                .ToList();
            RefreshRoomEditor();
        }

        private void SaveEditedRoom()
        {
            editingRoomDraft.RoomName = roomNameInput.text == null ? string.Empty : roomNameInput.text.Trim();
            BoardLayout.NormalizeRoomSlots(editingRoomDraft);

            string validationMessage;
            if (!BoardLayout.ValidateRoom(editingRoomDraft, out validationMessage))
            {
                roomEditValidationText.text = validationMessage;
                return;
            }

            int existingIndex = saveData.Rooms.FindIndex(room => room.RoomId == editingRoomOriginalId);
            if (existingIndex >= 0)
            {
                saveData.Rooms[existingIndex] = BoardLayout.CloneRoom(editingRoomDraft);
            }
            else
            {
                saveData.Rooms.Add(BoardLayout.CloneRoom(editingRoomDraft));
            }

            SaveManager.Save(saveData);
            ShowRooms();
        }

        private static int GetNextPromptInterval(int current)
        {
            int[] options = { 15, 30, 60, 90 };
            for (int i = 0; i < options.Length; i++)
            {
                if (current < options[i])
                {
                    return options[i];
                }
            }

            return options[0];
        }

        private void RefreshOptionsSummary()
        {
            if (optionsSummaryText == null)
            {
                return;
            }

            optionsSummaryText.text = string.Format(
                "当前配置\n默认规则：{0}\n主题：{1}\n音效：{2}\n背景音乐：{3}\n催促：{4} / {5} 秒",
                BoardLayout.GetRuleLabel(saveData.Options.DefaultRule),
                GetThemeLabel(saveData.Options.ThemeId),
                saveData.Options.SoundEnabled ? "开启" : "关闭",
                saveData.Options.MusicEnabled ? "开启" : "关闭",
                saveData.Options.PromptEnabled ? "开启" : "关闭",
                saveData.Options.PromptIntervalSeconds);

            SetButtonLabel(ruleToggleButton, string.Format("默认规则：{0}", BoardLayout.GetRuleLabel(saveData.Options.DefaultRule)));
            SetButtonLabel(themeToggleButton, string.Format("背景主题：{0}", GetThemeLabel(saveData.Options.ThemeId)));
            SetButtonLabel(soundToggleButton, saveData.Options.SoundEnabled ? "音效：开" : "音效：关");
            SetButtonLabel(musicToggleButton, saveData.Options.MusicEnabled ? "背景音乐：开" : "背景音乐：关");
            SetButtonLabel(promptToggleButton, saveData.Options.PromptEnabled ? "催促：开" : "催促：关");
            SetButtonLabel(promptIntervalButton, string.Format("催促间隔：{0} 秒", saveData.Options.PromptIntervalSeconds));
        }

        private void ToggleDefaultRule()
        {
            saveData.Options.DefaultRule = saveData.Options.DefaultRule == RuleVariant.OnePieceJump
                ? RuleVariant.SpaceJump
                : RuleVariant.OnePieceJump;
            SyncDefaultRoomWithOptions();
            SaveManager.Save(saveData);
            RefreshOptionsSummary();
        }

        private void ToggleTheme()
        {
            saveData.Options.ThemeId = GetNextThemeId(saveData.Options.ThemeId);
            SyncDefaultRoomWithOptions();
            SaveManager.Save(saveData);
            ApplyTheme(saveData.Options.ThemeId);
            RefreshOptionsSummary();
        }

        private void ToggleSound()
        {
            saveData.Options.SoundEnabled = !saveData.Options.SoundEnabled;
            SyncDefaultRoomWithOptions();
            SaveManager.Save(saveData);
            PlaySfx("button");
            RefreshOptionsSummary();
        }

        private void ToggleMusic()
        {
            saveData.Options.MusicEnabled = !saveData.Options.MusicEnabled;
            SyncDefaultRoomWithOptions();
            SaveManager.Save(saveData);
            RefreshMusicState();
            RefreshOptionsSummary();
        }

        private void TogglePrompt()
        {
            saveData.Options.PromptEnabled = !saveData.Options.PromptEnabled;
            SyncDefaultRoomWithOptions();
            SaveManager.Save(saveData);
            RefreshOptionsSummary();
        }

        private void CyclePromptInterval()
        {
            saveData.Options.PromptIntervalSeconds = GetNextPromptInterval(saveData.Options.PromptIntervalSeconds);
            SyncDefaultRoomWithOptions();
            SaveManager.Save(saveData);
            RefreshOptionsSummary();
        }

        private void SyncDefaultRoomWithOptions()
        {
            RoomConfig defaultRoom = saveData.Rooms.FirstOrDefault(room => room.RoomId == "default-room");
            if (defaultRoom == null)
            {
                return;
            }

            defaultRoom.SoundEnabled = saveData.Options.SoundEnabled;
            defaultRoom.MusicEnabled = saveData.Options.MusicEnabled;
            defaultRoom.PromptEnabled = saveData.Options.PromptEnabled;
            defaultRoom.PromptIntervalSeconds = saveData.Options.PromptIntervalSeconds;
            defaultRoom.ThemeId = saveData.Options.ThemeId;
            defaultRoom.RuleVariant = saveData.Options.DefaultRule;
        }

        private static string GetNextThemeId(string themeId)
        {
            return themeId == "mint" ? "pink" : "mint";
        }

        private static string GetThemeLabel(string themeId)
        {
            return themeId == "mint" ? "薄荷花园" : "粉色糖果";
        }

        private ThemePalette GetTheme(string themeId)
        {
            if (themeId == "mint")
            {
            return new ThemePalette
            {
                Backdrop = new Color(0.86f, 0.94f, 0.98f),
                Panel = new Color(0.94f, 1f, 0.98f, 0.96f),
                Card = new Color(0.99f, 1f, 0.98f, 0.98f),
                Button = new Color(0.58f, 0.88f, 0.78f),
                ButtonHot = new Color(0.68f, 0.94f, 0.85f),
                Text = new Color(0.2f, 0.42f, 0.38f),
                MutedText = new Color(0.36f, 0.55f, 0.5f),
                Board = new Color(0f, 0f, 0f, 0f),
                Cell = new Color(0.78f, 0.9f, 0.98f, 0.9f),
                Target = new Color(0.98f, 1f, 1f, 1f),
                Selection = new Color(1f, 1f, 1f, 0.78f)
            };
            }

            return new ThemePalette
            {
            Backdrop = new Color(0.87f, 0.93f, 0.98f),
            Panel = new Color(1f, 0.94f, 0.96f, 0.96f),
            Card = new Color(1f, 0.99f, 1f, 0.98f),
            Button = new Color(0.98f, 0.71f, 0.81f),
            ButtonHot = new Color(1f, 0.77f, 0.85f),
            Text = new Color(0.47f, 0.22f, 0.31f),
            MutedText = new Color(0.55f, 0.37f, 0.44f),
            Board = new Color(0f, 0f, 0f, 0f),
            Cell = new Color(0.78f, 0.9f, 0.99f, 0.9f),
            Target = new Color(0.98f, 1f, 1f, 1f),
            Selection = new Color(1f, 1f, 1f, 0.78f)
        };
        }

        private void ApplyTheme(string themeId)
        {
            activeTheme = GetTheme(themeId);
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.backgroundColor = activeTheme.Backdrop;
            }

            RectTransform[] panels = { splashPanel, menuPanel, optionsPanel, roomsPanel, roomEditPanel, gamePanel };
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null)
                {
                    continue;
                }

                Image image = panels[i].GetComponent<Image>();
                if (image != null)
                {
                    if (panels[i] == gamePanel)
                    {
                        image.sprite = GenerateMountainBackdropSprite();
                        image.color = Color.white;
                    }
                    else
                    {
                        image.sprite = null;
                        image.color = activeTheme.Panel;
                    }
                }
            }

            if (boardContainer != null)
            {
                Image boardImage = boardContainer.GetComponent<Image>();
                if (boardImage != null)
                {
                    boardImage.color = activeTheme.Board;
                }
            }

            RefreshButtonTheme(rootCanvas == null ? null : rootCanvas.transform);
            RefreshGameChromeStyle();
            if (session != null)
            {
                RefreshBoard();
            }
        }

        private void RefreshButtonTheme(Transform root)
        {
            if (root == null)
            {
                return;
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Image image = buttons[i].GetComponent<Image>();
                if (image != null)
                {
                    image.color = activeTheme.Button;
                }

                ColorBlock colors = buttons[i].colors;
                colors.normalColor = activeTheme.Button;
                colors.highlightedColor = activeTheme.ButtonHot;
                colors.pressedColor = Color.Lerp(activeTheme.Button, activeTheme.Text, 0.12f);
                colors.selectedColor = activeTheme.ButtonHot;
                colors.disabledColor = new Color(0.86f, 0.84f, 0.86f);
                buttons[i].colors = colors;
            }
        }

        private void RefreshGameChromeStyle()
        {
            if (bottomControlBar != null)
            {
                Image barImage = bottomControlBar.GetComponent<Image>();
                if (barImage != null)
                {
                    barImage.color = new Color(0f, 0f, 0f, 0.48f);
                }
            }

            for (int i = 0; i < gameChromeButtons.Count; i++)
            {
                Button button = gameChromeButtons[i];
                if (button == null)
                {
                    continue;
                }

                Image image = button.GetComponent<Image>();
                if (image != null)
                {
                    image.color = new Color(0.02f, 0.025f, 0.03f, 0.88f);
                }

                ColorBlock colors = button.colors;
                colors.normalColor = new Color(0.02f, 0.025f, 0.03f, 0.88f);
                colors.highlightedColor = new Color(0.18f, 0.2f, 0.22f, 0.92f);
                colors.pressedColor = new Color(0f, 0f, 0f, 0.96f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(0.05f, 0.05f, 0.05f, 0.36f);
                button.colors = colors;

                Text label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.color = new Color(1f, 1f, 1f, button.interactable ? 0.96f : 0.48f);
                    label.fontSize = button == passTurnButton || button == finishTurnButton ? 34 : 32;
                }
            }
        }

        private bool IsSoundEnabled()
        {
            return selectedRoom == null ? saveData.Options.SoundEnabled : selectedRoom.SoundEnabled;
        }

        private bool IsMusicEnabled()
        {
            return selectedRoom == null ? saveData.Options.MusicEnabled : selectedRoom.MusicEnabled;
        }

        private void PlaySfx(string clipId)
        {
            if (sfxSource == null || !IsSoundEnabled())
            {
                return;
            }

            AudioClip clip;
            if (sfxClips.TryGetValue(clipId, out clip))
            {
                sfxSource.PlayOneShot(clip);
            }
        }

        private void RefreshMusicState()
        {
            if (musicSource == null || musicClip == null)
            {
                return;
            }

            if (IsMusicEnabled())
            {
                if (!musicSource.isPlaying)
                {
                    musicSource.Play();
                }
            }
            else if (musicSource.isPlaying)
            {
                musicSource.Stop();
            }
        }

        private AudioClip CreateToneClip(string clipName, float frequency, float duration, float volume)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = 1f - (i / (float)sampleCount);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume * envelope;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateArpeggioClip(string clipName, float[] frequencies, float duration, float volume)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            int segment = Mathf.Max(1, sampleCount / frequencies.Length);

            for (int i = 0; i < sampleCount; i++)
            {
                int noteIndex = Mathf.Clamp(i / segment, 0, frequencies.Length - 1);
                float t = i / (float)sampleRate;
                float envelope = 1f - Mathf.Pow(i / (float)sampleCount, 1.5f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequencies[noteIndex] * t) * volume * envelope;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateMusicLoop()
        {
            const int sampleRate = 44100;
            const float duration = 8f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float[] notes = { 261.63f, 329.63f, 392f, 523.25f, 440f, 392f, 329.63f, 293.66f };
            int noteLength = sampleCount / notes.Length;

            for (int i = 0; i < sampleCount; i++)
            {
                int noteIndex = Mathf.Clamp(i / noteLength, 0, notes.Length - 1);
                float t = i / (float)sampleRate;
                float local = (i % noteLength) / (float)noteLength;
                float envelope = Mathf.Sin(Mathf.PI * local) * 0.42f;
                float main = Mathf.Sin(2f * Mathf.PI * notes[noteIndex] * t);
                float harmony = Mathf.Sin(2f * Mathf.PI * notes[noteIndex] * 1.5f * t) * 0.35f;
                samples[i] = (main + harmony) * envelope * 0.12f;
            }

            AudioClip clip = AudioClip.Create("sweet_loop", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void UpdateSelectionPulse()
        {
            if (session == null || session.SelectedPieceId < 0)
            {
                return;
            }

            float scale = 1.08f + Mathf.Sin(Time.time * 7f) * 0.045f;
            foreach (BoardCellView view in cellViews.Values)
            {
                PieceState piece = session.GetPieceAt(view.Coord);
                if (piece != null && piece.PieceId == session.SelectedPieceId)
                {
                    view.PieceImage.transform.localScale = Vector3.one * scale;
                    if (view.SelectionRing != null)
                    {
                        view.SelectionRing.color = activeTheme.Selection;
                    }
                    return;
                }
            }
        }

        private IEnumerator AnimatePiecePulse(HexCoord coord, float maxScale, float duration)
        {
            BoardCellView view;
            if (!cellViews.TryGetValue(coord, out view) || view.PieceImage == null)
            {
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(maxScale, 1f, progress);
                view.PieceImage.transform.localScale = Vector3.one * scale;
                yield return null;
            }

            view.PieceImage.transform.localScale = Vector3.one;
        }

        private IEnumerator AnimateVictoryModal()
        {
            if (victoryModal == null)
            {
                yield break;
            }

            Transform card = victoryModal.transform.childCount > 0 ? victoryModal.transform.GetChild(0) : victoryModal.transform;
            float elapsed = 0f;
            const float duration = 0.36f;
            card.localScale = Vector3.one * 0.72f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.Sin(progress * Mathf.PI * 0.5f);
                card.localScale = Vector3.one * Mathf.Lerp(0.72f, 1f, eased);
                yield return null;
            }

            card.localScale = Vector3.one;
        }

        private void EnsureBoardCreated()
        {
            if (cellViews.Count > 0)
            {
                return;
            }

            IReadOnlyList<HexCoord> cells = BoardLayout.AllCells;

            for (int i = 0; i < cells.Count; i++)
            {
                HexCoord coord = cells[i];
                GameObject cellObject = new GameObject(string.Format("Cell_{0}_{1}", coord.Q, coord.R), typeof(RectTransform), typeof(Image), typeof(Button));
                cellObject.transform.SetParent(boardContainer, false);

                RectTransform rect = cellObject.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(104f, 104f);

                const float hexRadius = 52f;
                float x = Mathf.Sqrt(3f) * hexRadius * (coord.Q + (coord.R * 0.5f));
                float y = -1.5f * hexRadius * coord.R;
                rect.anchoredPosition = new Vector2(x, y);

                Image baseImage = cellObject.GetComponent<Image>();
                baseImage.sprite = GenerateHexCellSprite();
                baseImage.color = activeTheme.Cell;

                Button button = cellObject.GetComponent<Button>();
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.pressedColor = Color.white;
                colors.selectedColor = Color.white;
                colors.disabledColor = Color.white;
                button.colors = colors;

                GameObject ringObject = CreateCircleImage("SlotRing", cellObject.transform, 48f, new Color(0.33f, 0.38f, 0.42f, 0.4f));
                Image ringImage = ringObject.GetComponent<Image>();
                ringImage.sprite = GenerateRingSprite();

                GameObject hintObject = new GameObject("Hint", typeof(RectTransform), typeof(Image));
                hintObject.transform.SetParent(cellObject.transform, false);
                RectTransform hintRect = hintObject.GetComponent<RectTransform>();
                hintRect.anchorMin = new Vector2(0.5f, 0.5f);
                hintRect.anchorMax = new Vector2(0.5f, 0.5f);
                hintRect.sizeDelta = new Vector2(104f, 104f);
                hintRect.anchoredPosition = Vector2.zero;
                Image hintImage = hintObject.GetComponent<Image>();
                hintImage.sprite = GenerateHexGlowSprite();
                hintImage.color = new Color(1f, 1f, 1f, 0.64f);
                hintImage.raycastTarget = false;
                hintImage.gameObject.SetActive(false);

                GameObject shadowObject = CreateCircleImage("PieceShadow", cellObject.transform, 78f, new Color(0.05f, 0.08f, 0.11f, 0.28f));
                Image shadowImage = shadowObject.GetComponent<Image>();
                shadowObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(7f, -8f);
                shadowImage.gameObject.SetActive(false);

                GameObject pieceObject = CreateCircleImage("Piece", cellObject.transform, 76f, Color.white);
                Image pieceImage = pieceObject.GetComponent<Image>();
                pieceImage.sprite = GeneratePieceSprite();
                pieceImage.gameObject.SetActive(false);

                GameObject selectionObject = CreateCircleImage("Selection", cellObject.transform, 96f, new Color(1f, 1f, 1f, 0.78f));
                Image selectionImage = selectionObject.GetComponent<Image>();
                selectionImage.sprite = GenerateRingSprite();
                selectionImage.gameObject.SetActive(false);

                BoardCellView view = cellObject.AddComponent<BoardCellView>();
                view.Coord = coord;
                view.Button = button;
                view.BaseImage = baseImage;
                view.SlotRingImage = ringImage;
                view.PieceShadowImage = shadowImage;
                view.PieceImage = pieceImage;
                view.HintImage = hintImage;
                view.SelectionRing = selectionImage;

                button.onClick.AddListener(() => HandleCellClicked(coord));
                cellViews[coord] = view;
            }
        }

        private void BuildSplashPanel()
        {
            CreateText("SplashCaption", splashPanel, "温暖 · 可爱 · 本地对战", 36, FontStyle.Normal, new Color(0.64f, 0.4f, 0.48f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.43f), new Vector2(0.5f, 0.43f), new Vector2(900f, 60f));
            splashTitleText = CreateText("SplashTitle", splashPanel, "甜姐的跳跳棋", 96, FontStyle.Bold, new Color(0.73f, 0.24f, 0.42f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), new Vector2(1200f, 140f));
            CreateText("SplashDecor", splashPanel, "★  ○  ✦  ○  ★", 50, FontStyle.Bold, new Color(1f, 0.74f, 0.82f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.33f), new Vector2(900f, 80f));
        }

        private void BuildMenuPanel()
        {
            CreateText("MenuTitle", menuPanel, "甜姐的跳跳棋", 88, FontStyle.Bold, new Color(0.72f, 0.24f, 0.41f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.76f), new Vector2(0.5f, 0.76f), new Vector2(1100f, 120f));
            CreateText("MenuSubtitle", menuPanel, "第一阶段 MVP：Splash、默认房间、121 格棋盘、真人 vs AI", 34, FontStyle.Normal, new Color(0.55f, 0.37f, 0.44f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.67f), new Vector2(0.5f, 0.67f), new Vector2(1200f, 80f));

            CreateButton(menuPanel, "开始游戏", ShowRooms, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(620f, 116f), Vector2.zero);
            CreateButton(menuPanel, "游戏选项", ShowOptions, new Vector2(0.5f, 0.41f), new Vector2(0.5f, 0.41f), new Vector2(620f, 116f), Vector2.zero);
        }

        private void BuildOptionsPanel()
        {
            CreateText("OptionsTitle", optionsPanel, "游戏选项", 76, FontStyle.Bold, new Color(0.71f, 0.25f, 0.41f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.83f), new Vector2(0.5f, 0.83f), new Vector2(800f, 100f));
            optionsSummaryText = CreateText("OptionsSummary", optionsPanel, string.Empty, 36, FontStyle.Normal, new Color(0.54f, 0.35f, 0.44f), TextAnchor.UpperLeft, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(840f, 260f));

            ruleToggleButton = CreateButton(optionsPanel, "默认规则：一子跳", ToggleDefaultRule, new Vector2(0.5f, 0.48f), new Vector2(0.5f, 0.48f), new Vector2(620f, 86f), Vector2.zero);
            themeToggleButton = CreateButton(optionsPanel, "背景主题：粉色糖果", ToggleTheme, new Vector2(0.5f, 0.405f), new Vector2(0.5f, 0.405f), new Vector2(620f, 78f), Vector2.zero);
            soundToggleButton = CreateButton(optionsPanel, "音效：开", ToggleSound, new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.33f), new Vector2(620f, 78f), Vector2.zero);
            musicToggleButton = CreateButton(optionsPanel, "背景音乐：开", ToggleMusic, new Vector2(0.5f, 0.255f), new Vector2(0.5f, 0.255f), new Vector2(620f, 78f), Vector2.zero);
            promptToggleButton = CreateButton(optionsPanel, "催促：关", TogglePrompt, new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.18f), new Vector2(620f, 78f), Vector2.zero);
            promptIntervalButton = CreateButton(optionsPanel, "催促间隔：30 秒", CyclePromptInterval, new Vector2(0.5f, 0.105f), new Vector2(0.5f, 0.105f), new Vector2(620f, 78f), Vector2.zero);
            CreateButton(optionsPanel, "返回主菜单", ShowMenu, new Vector2(0.5f, 0.035f), new Vector2(0.5f, 0.035f), new Vector2(620f, 70f), Vector2.zero);
        }

        private void BuildRoomsPanel()
        {
            CreateText("RoomsTitle", roomsPanel, "房间列表", 76, FontStyle.Bold, new Color(0.7f, 0.25f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.86f), new Vector2(0.5f, 0.86f), new Vector2(900f, 100f));
            CreateText("RoomsHint", roomsPanel, "第一阶段只提供默认房间，但已经支持本地保存和列表入口。", 32, FontStyle.Normal, new Color(0.55f, 0.36f, 0.43f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), new Vector2(1100f, 60f));

            GameObject scrollObject = new GameObject("RoomScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollObject.transform.SetParent(roomsPanel, false);
            RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0.5f, 0.47f);
            scrollRectTransform.anchorMax = new Vector2(0.5f, 0.47f);
            scrollRectTransform.sizeDelta = new Vector2(1040f, 660f);
            scrollObject.GetComponent<Image>().color = new Color(1f, 0.98f, 0.99f, 0.55f);

            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportObject.transform.SetParent(scrollObject.transform, false);
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(18f, 18f);
            viewportRect.offsetMax = new Vector2(-18f, -18f);
            viewportObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            GameObject container = new GameObject("RoomCardContainer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            roomCardContainer = container.GetComponent<RectTransform>();
            roomCardContainer.SetParent(viewportObject.transform, false);
            roomCardContainer.anchorMin = new Vector2(0f, 1f);
            roomCardContainer.anchorMax = new Vector2(1f, 1f);
            roomCardContainer.pivot = new Vector2(0.5f, 1f);
            roomCardContainer.offsetMin = Vector2.zero;
            roomCardContainer.offsetMax = Vector2.zero;

            ContentSizeFitter fitter = container.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            VerticalLayoutGroup layout = container.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 24f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = roomCardContainer;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            CreateButton(roomsPanel, "新建房间", () => ShowRoomEditor(null), new Vector2(0.5f, 0.16f), new Vector2(0.5f, 0.16f), new Vector2(620f, 82f), Vector2.zero);
            CreateButton(roomsPanel, "返回主菜单", ShowMenu, new Vector2(0.5f, 0.075f), new Vector2(0.5f, 0.075f), new Vector2(620f, 82f), Vector2.zero);
        }

        private void BuildRoomEditPanel()
        {
            roomEditTitleText = CreateText("RoomEditTitle", roomEditPanel, "编辑房间", 70, FontStyle.Bold, new Color(0.7f, 0.25f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.9f), new Vector2(0.5f, 0.9f), new Vector2(900f, 90f));
            CreateText("RoomNameLabel", roomEditPanel, "房间名称", 30, FontStyle.Bold, new Color(0.48f, 0.28f, 0.37f), TextAnchor.MiddleLeft, new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.82f), new Vector2(900f, 50f));
            roomNameInput = CreateInputField(roomEditPanel, new Vector2(0.5f, 0.775f), new Vector2(900f, 72f));

            roomRuleToggleButton = CreateButton(roomEditPanel, "规则：一子跳", ToggleRoomRule, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), new Vector2(430f, 72f), new Vector2(-235f, 0f));
            roomThemeToggleButton = CreateButton(roomEditPanel, "主题：粉色糖果", ToggleRoomTheme, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), new Vector2(430f, 72f), new Vector2(235f, 0f));
            roomSoundToggleButton = CreateButton(roomEditPanel, "音效：开", ToggleRoomSound, new Vector2(0.5f, 0.635f), new Vector2(0.5f, 0.635f), new Vector2(286f, 66f), new Vector2(-310f, 0f));
            roomMusicToggleButton = CreateButton(roomEditPanel, "音乐：开", ToggleRoomMusic, new Vector2(0.5f, 0.635f), new Vector2(0.5f, 0.635f), new Vector2(286f, 66f), Vector2.zero);
            roomPromptToggleButton = CreateButton(roomEditPanel, "催促：关", ToggleRoomPrompt, new Vector2(0.5f, 0.635f), new Vector2(0.5f, 0.635f), new Vector2(286f, 66f), new Vector2(310f, 0f));
            roomPromptIntervalButton = CreateButton(roomEditPanel, "催促间隔：30 秒", CycleRoomPromptInterval, new Vector2(0.5f, 0.575f), new Vector2(0.5f, 0.575f), new Vector2(900f, 66f), Vector2.zero);

            float startY = 0.505f;
            float rowGap = 0.06f;
            SlotId[] slots = BoardLayout.GetSlotsInDisplayOrder();
            for (int i = 0; i < slots.Length; i++)
            {
                SlotId slotId = slots[i];
                int row = i / 2;
                int col = i % 2;
                Vector2 offset = new Vector2(col == 0 ? -235f : 235f, 0f);
                Button slotButton = CreateButton(roomEditPanel, BoardLayout.GetSlotLabel(slotId), () => CycleRoomSlot(slotId), new Vector2(0.5f, startY - row * rowGap), new Vector2(0.5f, startY - row * rowGap), new Vector2(430f, 66f), offset);
                roomSlotButtons[slotId] = slotButton;
            }

            roomEditValidationText = CreateText("RoomEditValidation", roomEditPanel, string.Empty, 28, FontStyle.Normal, new Color(0.7f, 0.22f, 0.35f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), new Vector2(980f, 70f));
            CreateButton(roomEditPanel, "保存房间", SaveEditedRoom, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f), new Vector2(430f, 82f), new Vector2(-235f, 0f));
            CreateButton(roomEditPanel, "取消", ShowRooms, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f), new Vector2(430f, 82f), new Vector2(235f, 0f));
        }

        private void BuildGamePanel()
        {
            gameChromeButtons.Clear();

            roomTitleText = CreateText("RoomTitle", gamePanel, "默认房间", 34, FontStyle.Bold, new Color(1f, 1f, 1f, 0.72f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.968f), new Vector2(0.5f, 0.968f), new Vector2(1000f, 58f));

            GameObject boardObject = new GameObject("BoardContainer", typeof(RectTransform), typeof(Image));
            boardContainer = boardObject.GetComponent<RectTransform>();
            boardContainer.SetParent(gamePanel, false);
            boardContainer.anchorMin = new Vector2(0.5f, 0.54f);
            boardContainer.anchorMax = new Vector2(0.5f, 0.54f);
            boardContainer.sizeDelta = new Vector2(1400f, 1480f);
            Image boardImage = boardObject.GetComponent<Image>();
            boardImage.color = Color.clear;
            boardImage.raycastTarget = false;

            GameObject barObject = new GameObject("BottomControlBar", typeof(RectTransform), typeof(Image));
            barObject.transform.SetParent(gamePanel, false);
            bottomControlBar = barObject.GetComponent<RectTransform>();
            bottomControlBar.anchorMin = new Vector2(0f, 0f);
            bottomControlBar.anchorMax = new Vector2(1f, 0f);
            bottomControlBar.pivot = new Vector2(0.5f, 0f);
            bottomControlBar.sizeDelta = new Vector2(0f, 220f);
            bottomControlBar.anchoredPosition = Vector2.zero;
            barObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.47f);

            currentPlayerText = CreateText("CurrentPlayer", bottomControlBar, "当前玩家：", 26, FontStyle.Bold, new Color(1f, 1f, 1f, 0.72f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.68f), new Vector2(1120f, 42f));
            statusText = CreateText("Status", bottomControlBar, "请选择一个棋子。", 34, FontStyle.Normal, new Color(1f, 1f, 1f, 0.92f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), new Vector2(1300f, 58f));

            undoButton = CreateButton(bottomControlBar, "悔棋", HandleUndo, new Vector2(0.34f, 0.2f), new Vector2(0.34f, 0.2f), new Vector2(240f, 72f), Vector2.zero);
            finishTurnButton = CreateButton(bottomControlBar, "完成移动", HandleFinishTurn, new Vector2(0.66f, 0.2f), new Vector2(0.66f, 0.2f), new Vector2(280f, 72f), Vector2.zero);
            passTurnButton = CreateButton(bottomControlBar, "放弃移动", HandlePassTurn, new Vector2(0.66f, 0.2f), new Vector2(0.66f, 0.2f), new Vector2(280f, 72f), Vector2.zero);
            Button roomsButton = CreateButton(bottomControlBar, "设置", ShowRooms, new Vector2(0.94f, 0.24f), new Vector2(0.94f, 0.24f), new Vector2(130f, 72f), Vector2.zero);
            gameChromeButtons.Add(undoButton);
            gameChromeButtons.Add(finishTurnButton);
            gameChromeButtons.Add(passTurnButton);
            gameChromeButtons.Add(roomsButton);

            victoryModal = CreateVictoryModal(gamePanel);
            RefreshGameChromeStyle();
        }

        private GameObject CreateVictoryModal(Transform parent)
        {
            GameObject modal = new GameObject("VictoryModal", typeof(RectTransform), typeof(Image));
            modal.transform.SetParent(parent, false);
            RectTransform rect = modal.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            modal.GetComponent<Image>().color = new Color(0.95f, 0.58f, 0.73f, 0.3f);

            GameObject card = new GameObject("VictoryCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(modal.transform, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(840f, 520f);
            cardRect.anchoredPosition = Vector2.zero;
            card.GetComponent<Image>().color = new Color(1f, 0.98f, 0.99f, 0.98f);

            victoryText = CreateText("VictoryText", card.transform, "胜利", 72, FontStyle.Bold, new Color(0.75f, 0.24f, 0.42f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), new Vector2(760f, 120f));
            CreateText("VictoryBody", card.transform, "恭喜完成一局本地单机 MVP 对战。", 32, FontStyle.Normal, new Color(0.54f, 0.36f, 0.43f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), new Vector2(760f, 70f));
            CreateButton(card.transform, "返回房间列表", ShowRooms, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), new Vector2(520f, 88f), Vector2.zero);
            CreateButton(card.transform, "回到主菜单", ShowMenu, new Vector2(0.5f, 0.17f), new Vector2(0.5f, 0.17f), new Vector2(520f, 88f), Vector2.zero);

            modal.SetActive(false);
            return modal;
        }

        private void CreateBackgroundDecor(Transform parent)
        {
            GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(parent, false);
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(1f, 0.97f, 0.98f, 1f);

            CreateDecorCircle(parent, new Vector2(160f, -180f), 340f, new Color(1f, 0.84f, 0.91f, 0.4f));
            CreateDecorCircle(parent, new Vector2(-190f, 240f), 270f, new Color(1f, 0.91f, 0.75f, 0.35f));
            CreateDecorCircle(parent, new Vector2(0f, -620f), 220f, new Color(0.92f, 0.93f, 1f, 0.35f));
        }

        private void CreateDecorCircle(Transform parent, Vector2 anchoredPosition, float size, Color color)
        {
            GameObject circle = CreateCircleImage("DecorCircle", parent, size, color);
            RectTransform rect = circle.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
        }

        private RectTransform CreateFullscreenPanel(string name, Transform parent, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = color;
            return rect;
        }

        private void ShowPanel(RectTransform panel)
        {
            splashPanel.gameObject.SetActive(panel == splashPanel);
            menuPanel.gameObject.SetActive(panel == menuPanel);
            optionsPanel.gameObject.SetActive(panel == optionsPanel);
            roomsPanel.gameObject.SetActive(panel == roomsPanel);
            roomEditPanel.gameObject.SetActive(panel == roomEditPanel);
            gamePanel.gameObject.SetActive(panel == gamePanel);
        }

        private Text CreateText(
            string name,
            Transform parent,
            string text,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor anchor,
            Vector2? anchorMin = null,
            Vector2? anchorMax = null,
            Vector2? size = null)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            Vector2 min = anchorMin ?? new Vector2(0.5f, 0.5f);
            Vector2 max = anchorMax ?? min;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.sizeDelta = size ?? new Vector2(400f, 80f);
            rect.anchoredPosition = Vector2.zero;

            Text label = textObject.GetComponent<Text>();
            label.font = defaultFont;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private InputField CreateInputField(Transform parent, Vector2 anchor, Vector2 size)
        {
            GameObject inputObject = new GameObject("RoomNameInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            inputObject.transform.SetParent(parent, false);

            RectTransform rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            inputObject.GetComponent<Image>().color = new Color(1f, 0.99f, 1f, 0.98f);

            Text text = CreateText("Text", inputObject.transform, string.Empty, 34, FontStyle.Normal, new Color(0.45f, 0.25f, 0.34f), TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(24f, 0f);
            text.rectTransform.offsetMax = new Vector2(-24f, 0f);

            Text placeholder = CreateText("Placeholder", inputObject.transform, "请输入房间名", 32, FontStyle.Italic, new Color(0.72f, 0.56f, 0.62f), TextAnchor.MiddleLeft);
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = new Vector2(24f, 0f);
            placeholder.rectTransform.offsetMax = new Vector2(-24f, 0f);

            InputField input = inputObject.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = 18;
            return input;
        }

        private Button CreateButton(Transform parent, string label, Action onClick, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 offset)
        {
            GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.98f, 0.71f, 0.81f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.98f, 0.71f, 0.81f);
            colors.highlightedColor = new Color(1f, 0.77f, 0.85f);
            colors.pressedColor = new Color(0.95f, 0.63f, 0.76f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.87f, 0.84f, 0.86f);
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                PlaySfx("button");
                onClick();
            });

            Text buttonText = CreateText("Label", buttonObject.transform, label, 34, FontStyle.Bold, new Color(0.47f, 0.22f, 0.31f), TextAnchor.MiddleCenter);
            buttonText.rectTransform.anchorMin = Vector2.zero;
            buttonText.rectTransform.anchorMax = Vector2.one;
            buttonText.rectTransform.offsetMin = Vector2.zero;
            buttonText.rectTransform.offsetMax = Vector2.zero;

            return button;
        }

        private GameObject CreateCircleImage(string name, Transform parent, float size, Color color)
        {
            GameObject circle = new GameObject(name, typeof(RectTransform), typeof(Image));
            circle.transform.SetParent(parent, false);
            RectTransform rect = circle.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;

            Image image = circle.GetComponent<Image>();
            image.color = color;
            image.sprite = GenerateCircleSprite();
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            return circle;
        }

        private Sprite GenerateCircleSprite()
        {
            if (cachedCircleSprite != null)
            {
                return cachedCircleSprite;
            }

            Texture2D texture = new Texture2D(128, 128, TextureFormat.ARGB32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Color clear = new Color(0f, 0f, 0f, 0f);
            Vector2 center = new Vector2(63.5f, 63.5f);
            float radius = 60f;

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    texture.SetPixel(x, y, distance <= radius ? Color.white : clear);
                }
            }

            texture.Apply();
            cachedCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            return cachedCircleSprite;
        }

        private Sprite GenerateHexCellSprite()
        {
            if (cachedHexCellSprite != null)
            {
                return cachedHexCellSprite;
            }

            Texture2D texture = new Texture2D(128, 128, TextureFormat.ARGB32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Vector2[] vertices = CreatePointyHexVertices(new Vector2(63.5f, 63.5f), 60f);
            Color clear = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    if (!IsPointInPolygon(point, vertices))
                    {
                        texture.SetPixel(x, y, clear);
                        continue;
                    }

                    float edge = DistanceToPolygonEdge(point, vertices);
                    float grain = Mathf.PerlinNoise(x * 0.045f, y * 0.045f) * 0.08f;
                    float bevel = Mathf.Clamp01(edge / 12f);
                    float shade = Mathf.Lerp(0.68f, 0.96f, bevel) + grain;
                    if (edge < 2.2f)
                    {
                        shade = 0.55f;
                    }
                    else if (edge < 6f)
                    {
                        shade = Mathf.Lerp(0.64f, shade, edge / 6f);
                    }

                    float alpha = edge < 1.4f ? 0.78f : 0.94f;
                    texture.SetPixel(x, y, new Color(shade, shade, shade, alpha));
                }
            }

            texture.Apply();
            cachedHexCellSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            return cachedHexCellSprite;
        }

        private Sprite GenerateHexGlowSprite()
        {
            if (cachedHexGlowSprite != null)
            {
                return cachedHexGlowSprite;
            }

            Texture2D texture = new Texture2D(128, 128, TextureFormat.ARGB32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2(63.5f, 63.5f);
            Vector2[] vertices = CreatePointyHexVertices(center, 60f);
            Color clear = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    if (!IsPointInPolygon(point, vertices))
                    {
                        texture.SetPixel(x, y, clear);
                        continue;
                    }

                    float distance = Vector2.Distance(point, center);
                    float edge = DistanceToPolygonEdge(point, vertices);
                    float alpha = Mathf.Clamp01((1f - distance / 68f) * 0.55f + Mathf.Clamp01(edge / 10f) * 0.22f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            cachedHexGlowSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            return cachedHexGlowSprite;
        }

        private Sprite GenerateRingSprite()
        {
            if (cachedRingSprite != null)
            {
                return cachedRingSprite;
            }

            Texture2D texture = new Texture2D(128, 128, TextureFormat.ARGB32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2(63.5f, 63.5f);
            Color clear = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    Vector2 delta = new Vector2(x, y) - center;
                    float distance = delta.magnitude;
                    float ring = Mathf.Abs(distance - 42f);
                    float alpha = Mathf.Clamp01(1f - ring / 4.2f);
                    if (alpha <= 0f)
                    {
                        texture.SetPixel(x, y, clear);
                        continue;
                    }

                    float angle = Mathf.Atan2(delta.y, delta.x);
                    float nick = Mathf.Sin(angle * 3f + distance * 0.08f) > 0.86f ? 0.38f : 1f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * nick));
                }
            }

            texture.Apply();
            cachedRingSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            return cachedRingSprite;
        }

        private Sprite GeneratePieceSprite()
        {
            if (cachedPieceSprite != null)
            {
                return cachedPieceSprite;
            }

            Texture2D texture = new Texture2D(160, 160, TextureFormat.ARGB32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2(79.5f, 79.5f);
            Vector2 highlight = new Vector2(57f, 104f);
            Color clear = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    float radius = Vector2.Distance(point, center);
                    if (radius > 72f)
                    {
                        texture.SetPixel(x, y, clear);
                        continue;
                    }

                    float normalized = radius / 72f;
                    float light = 1f - normalized * 0.62f;
                    float rings = Mathf.Sin(radius * 0.34f + Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 6f) * 0.055f;
                    float wood = Mathf.PerlinNoise((x + 13f) * 0.075f, (y + 41f) * 0.075f) * 0.075f;
                    float highlightStrength = Mathf.Clamp01(1f - Vector2.Distance(point, highlight) / 38f) * 0.38f;
                    float rim = Mathf.SmoothStep(0f, 1f, normalized) * 0.26f;
                    float shade = Mathf.Clamp01(0.82f + light * 0.28f + rings + wood + highlightStrength - rim);
                    float alpha = Mathf.Clamp01((72f - radius) / 3.5f);
                    texture.SetPixel(x, y, new Color(shade, shade, shade, alpha));
                }
            }

            texture.Apply();
            cachedPieceSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            return cachedPieceSprite;
        }

        private Sprite GenerateMountainBackdropSprite()
        {
            if (cachedMountainSprite != null)
            {
                return cachedMountainSprite;
            }

            const int width = 512;
            const int height = 768;
            Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    Color color = Color.Lerp(new Color(0.9f, 0.96f, 1f), Color.white, Mathf.Pow(ny, 0.72f));

                    float farPeak = MountainHeight(nx, 0.71f, 0.085f, 1.8f, 3.4f);
                    if (ny < farPeak)
                    {
                        float depth = Mathf.Clamp01((farPeak - ny) / 0.36f);
                        float snow = Mathf.PerlinNoise(nx * 9f + 2.1f, ny * 13f + 1.7f);
                        Color rock = Color.Lerp(new Color(0.67f, 0.75f, 0.82f), new Color(0.36f, 0.43f, 0.5f), depth * 0.86f);
                        Color ice = new Color(0.9f, 0.96f, 1f);
                        color = Color.Lerp(rock, ice, snow > 0.56f ? 0.48f : 0.16f);
                        color.a = 1f;
                    }

                    float midPeak = MountainHeight(nx + 0.18f, 0.55f, 0.11f, 2.4f, 4.7f);
                    if (ny < midPeak)
                    {
                        float depth = Mathf.Clamp01((midPeak - ny) / 0.34f);
                        float snow = Mathf.PerlinNoise(nx * 12f + 7.3f, ny * 17f + 9.2f);
                        Color rock = Color.Lerp(new Color(0.57f, 0.65f, 0.72f), new Color(0.22f, 0.27f, 0.32f), depth);
                        Color ice = new Color(0.84f, 0.93f, 1f);
                        color = Color.Lerp(rock, ice, snow > 0.52f ? 0.55f : 0.1f);
                    }

                    float frontPeak = MountainHeight(nx + 0.43f, 0.26f, 0.08f, 3.2f, 5.5f);
                    if (ny < frontPeak)
                    {
                        float depth = Mathf.Clamp01((frontPeak - ny) / 0.28f);
                        float snow = Mathf.PerlinNoise(nx * 18f + 5.8f, ny * 20f + 4.1f);
                        Color rock = Color.Lerp(new Color(0.49f, 0.57f, 0.63f), new Color(0.13f, 0.17f, 0.21f), depth);
                        Color ice = new Color(0.78f, 0.9f, 0.98f);
                        color = Color.Lerp(rock, ice, snow > 0.5f ? 0.44f : 0.08f);
                    }

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            cachedMountainSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            return cachedMountainSprite;
        }

        private static float MountainHeight(float x, float baseHeight, float amplitude, float frequencyA, float frequencyB)
        {
            float ridge = Mathf.Sin(x * Mathf.PI * frequencyA) * 0.52f + Mathf.Sin((x + 0.17f) * Mathf.PI * frequencyB) * 0.28f;
            float noise = Mathf.PerlinNoise(x * 4.2f, 0.37f) * 0.42f;
            return baseHeight + amplitude * (ridge + noise);
        }

        private static Vector2[] CreatePointyHexVertices(Vector2 center, float radius)
        {
            Vector2[] vertices = new Vector2[6];
            for (int i = 0; i < vertices.Length; i++)
            {
                float angle = Mathf.Deg2Rad * (90f + i * 60f);
                vertices[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            return vertices;
        }

        private static bool IsPointInPolygon(Vector2 point, Vector2[] vertices)
        {
            bool inside = false;
            for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
            {
                bool intersects = ((vertices[i].y > point.y) != (vertices[j].y > point.y))
                    && (point.x < (vertices[j].x - vertices[i].x) * (point.y - vertices[i].y) / (vertices[j].y - vertices[i].y) + vertices[i].x);
                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static float DistanceToPolygonEdge(Vector2 point, Vector2[] vertices)
        {
            float distance = float.MaxValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 start = vertices[i];
                Vector2 end = vertices[(i + 1) % vertices.Length];
                distance = Mathf.Min(distance, DistanceToSegment(point, start, end));
            }

            return distance;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = Vector2.Dot(segment, segment);
            if (lengthSquared <= 0.0001f)
            {
                return Vector2.Distance(point, start);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        private void SetButtonLabel(Button button, string value)
        {
            if (button == null)
            {
                return;
            }

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = value;
            }
        }
    }

    public static class SaveManager
    {
        private const string FileName = "sweet_jump_jump_save.json";

        private static string SavePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        public static AppSaveData Load()
        {
            try
            {
                if (!File.Exists(SavePath))
                {
                    return new AppSaveData
                    {
                        Initialized = false,
                        Options = BoardLayout.CreateDefaultOptions(),
                        Rooms = new List<RoomConfig>()
                    };
                }

                string json = File.ReadAllText(SavePath);
                AppSaveData loaded = JsonUtility.FromJson<AppSaveData>(json);
                if (loaded == null)
                {
                    return new AppSaveData
                    {
                        Initialized = false,
                        Options = BoardLayout.CreateDefaultOptions(),
                        Rooms = new List<RoomConfig>()
                    };
                }

                if (loaded.Options == null)
                {
                    loaded.Options = BoardLayout.CreateDefaultOptions();
                }

                if (loaded.Rooms == null)
                {
                    loaded.Rooms = new List<RoomConfig>();
                }

                return loaded;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(string.Format("读取存档失败，已回退到默认配置：{0}", exception.Message));
                return new AppSaveData
                {
                    Initialized = false,
                    Options = BoardLayout.CreateDefaultOptions(),
                    Rooms = new List<RoomConfig>()
                };
            }
        }

        public static void Save(AppSaveData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(string.Format("写入存档失败：{0}", exception.Message));
            }
        }
    }
}
