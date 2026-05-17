using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.TextCore.LowLevel;
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
        public Image PieceSelectionHighlight;
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
        private enum ButtonLabelLength
        {
            Auto,
            TwoCharacters,
            FourCharacters,
            SixCharacters
        }

        private struct ButtonLabelProfile
        {
            public int MaxFontSize;
            public int MinFontSize;
            public float HorizontalPadding;
            public float VerticalPadding;
        }

        private sealed class ButtonLabelFitter : MonoBehaviour
        {
            public TMP_Text Label;
            public ButtonLabelLength Length;

            private string lastText;
            private Vector2 lastSize;
            private Vector2 lastParentSize;

            public void ApplyNow()
            {
                if (Label == null)
                {
                    return;
                }

                ApplyButtonLabelStyle(Label, Label.text, Length);
                lastText = Label.text;
                lastSize = Label.rectTransform.rect.size;
                RectTransform parentRect = Label.rectTransform.parent as RectTransform;
                lastParentSize = parentRect == null ? Vector2.zero : parentRect.rect.size;
            }

            private void LateUpdate()
            {
                if (Label == null)
                {
                    return;
                }

                Vector2 size = Label.rectTransform.rect.size;
                RectTransform parentRect = Label.rectTransform.parent as RectTransform;
                Vector2 parentSize = parentRect == null ? Vector2.zero : parentRect.rect.size;
                if (lastText != Label.text || lastSize != size || lastParentSize != parentSize)
                {
                    ApplyNow();
                }
            }
        }

        // 棋盘整体缩放后，距离棋盘容器边缘保留的比例。这里进一步缩小留白，让 iPad 端棋盘更贴近边缘。
        private const float BoardSafeMarginRatio = 0.004f;
        // 单个六边形格子的基准外接圆半径。提高后可整体放大棋盘，减少六边形之间视觉缝隙。
        private const float BoardBaseCellRadius = 66f;
        // 单个六边形格子的基准宽高，等于外接圆直径。
        private const float BoardBaseCellSize = BoardBaseCellRadius * 2f;
        // 六边形里面的小圆单线外框基准直径；随整体尺寸同步放大。
        private const float BoardBaseSlotRingSize = 74f;
        // 棋子阴影基准直径，略大于棋子本体。
        private const float BoardBasePieceShadowSize = 108f;
        // 棋子本体基准直径；进一步增大，贴近 iPad 端观感。
        private const float BoardBasePieceSize = 106f;
        // 选中棋子局部高光层基准直径。
        private const float BoardBaseSelectionSize = 92f;
        private const float BoardNameRevealDurationSeconds = 5f;
        private const float BoardNameRevealDoubleTapSeconds = 0.38f;
        private static readonly Vector2 PrimaryMenuButtonSize = new Vector2(620f, 116f);
        private static readonly Vector2 FullWidthButtonSize = new Vector2(620f, 82f);
        private static readonly Vector2 OptionButtonSize = new Vector2(620f, 78f);
        private static readonly Vector2 OptionHalfButtonSize = new Vector2(300f, 78f);
        private static readonly Vector2 OptionFileButtonSize = new Vector2(380f, 78f);
        private static readonly Vector2 RoomEditLargeButtonSize = new Vector2(430f, 72f);
        private static readonly Vector2 RoomEditCompactButtonSize = new Vector2(286f, 66f);
        private static readonly Vector2 RoomEditActionButtonSize = new Vector2(430f, 82f);
        private static readonly Vector2 GameActionButtonSize = new Vector2(360f, 82f);
        private const string CommonUiCharacterSet =
            "甜姐的跳跳棋农夫山泉有点第一比赛第二开始游戏在线玩游戏选项默认规则一子跳空跳背景主题粉色糖果薄荷花园音效开关音乐催促间隔秒从文件选择恢复返回主菜单房间列表阶段只提供默房但已经支持本地保存和入口新建编辑名称保存取消人局上方右上右下下方左下左上真人玩家人机高级普通初级房密服务器连接断开创建加入准备取消准备开始本局人机设置发现暂时没有可加入申请同意拒绝完成悔棋移动放弃退出确认继续胜利当前进度不会保留请输入你的昵称输入密钥查看重开投票发起请求解散当前MP0123456789jumpmddxztopmobileiPad";
        private const string LargeUiCharacterSet =
            "甜姐的跳跳棋在线房间房间列表游戏选项编辑房间人机设置加入申请退出本局胜利";

        // BoardCellColor：普通六边形格子的填充色和边框整体色调，保持接近冰蓝棋盘。
        private static readonly Color BoardCellColor = new Color(0.86f, 0.93f, 0.98f, 0.86f);
        // BoardTargetColor：选中棋子后，可走目标六边形的格子高亮色。
        private static readonly Color BoardTargetColor = new Color(0.97f, 1f, 1f, 0.98f);
        // BoardSlotRingColor：每个六边形内部小圆单线的默认颜色。
        private static readonly Color BoardSlotRingColor = new Color(0.56f, 0.62f, 0.66f, 0.72f);
        // BoardTargetRingColor：可走目标位置的小圆单线颜色，通常比默认小圆更亮。
        private static readonly Color BoardTargetRingColor = new Color(0.32f, 0.66f, 1f, 0.92f);
        // BoardTargetDotColor：可走目标位置中心实心提示点的颜色。
        private static readonly Color BoardTargetDotColor = new Color(1f, 0.48f, 0.05f, 0.82f);
        // BoardSelectionColor：选中棋子时叠在棋子上的局部亮斑颜色，不是整格外圈。
        private static readonly Color BoardSelectionColor = new Color(1f, 1f, 1f, 0.72f);

        private static AppController instance;
        private static Sprite cachedCircleSprite;
        private static Sprite cachedHexCellSprite;
        private static Sprite cachedHexGlowSprite;
        private static Sprite cachedRingSprite;
        private static Sprite cachedPieceSprite;
        private static Sprite cachedPieceHighlightSprite;
        private static Sprite cachedMountainSprite;
        private const string BundledChineseFontResource = "Fonts/NotoSansSC-Regular";

        private readonly Dictionary<HexCoord, BoardCellView> cellViews = new Dictionary<HexCoord, BoardCellView>();
        private readonly Dictionary<SlotId, Button> boardNameButtons = new Dictionary<SlotId, Button>();
        private readonly Dictionary<SlotId, TMP_Text> boardNameLabels = new Dictionary<SlotId, TMP_Text>();
        private readonly Dictionary<SlotId, float> boardNameRevealUntil = new Dictionary<SlotId, float>();
        private readonly Dictionary<string, AudioClip> sfxClips = new Dictionary<string, AudioClip>();
        private readonly List<Button> gameChromeButtons = new List<Button>();

        private AppSaveData saveData;
        private RoomConfig selectedRoom;
        private GameSession session;
        private Canvas rootCanvas;
        private TMP_FontAsset defaultFont;
        private AudioSource sfxSource;
        private AudioSource musicSource;
        private AudioClip musicClip;

        private RectTransform splashPanel;
        private RectTransform menuPanel;
        private RectTransform optionsPanel;
        private RectTransform roomsPanel;
        private RectTransform roomEditPanel;
        private RectTransform onlinePanel;
        private RectTransform onlineLoginSection;
        private RectTransform onlineLobbySection;
        private RectTransform gamePanel;
        private RectTransform roomCardContainer;
        private RectTransform boardContainer;
        private RectTransform bottomControlBar;
        private RectTransform onlineDiscoveryListContainer;
        private GameObject victoryModal;
        private GameObject exitConfirmModal;
        private GameObject onlineJoinRoomConfirmModal;
        private GameObject onlineJoinRequestModal;
        private GameObject onlineAiSettingsModal;
        private GameObject onlineRestartVoteModal;
        private GameObject onlineHostSettingsModal;
        private GameObject onlineLoginConflictModal;

        private TMP_Text splashTitleText;
        private TMP_Text statusText;
        private TMP_Text roomTitleText;
        private TMP_Text victoryText;
        private TMP_Text optionsSummaryText;
        private TMP_Text roomEditTitleText;
        private TMP_Text roomEditValidationText;
        private TMP_Text onlineStatusText;
        private TMP_Text onlineRoomKeyText;
        private TMP_Text onlineSlotPickerText;
        private TMP_Text onlineDiscoveryText;
        private TMP_Text onlineJoinRoomConfirmText;
        private TMP_Text onlineJoinRequestText;
        private TMP_Text onlineRestartVoteText;
        private TMP_Text onlineLoginConflictText;
        private Image currentPlayerPieceImage;

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
        private Button onlineStartButton;
        private Button onlineHostSettingsButton;
        private Button onlineHostRuleButton;
        private Button onlineDualDeviceButton;
        private Button onlineLoginButton;
        private TMP_InputField roomNameInput;
        private TMP_InputField onlineRoomKeyInput;
        private TMP_InputField onlinePlayerNameInput;
        private TMP_InputField onlineAccountInput;
        private TMP_InputField onlinePasswordInput;
        private Button musicPickButton;
        private Button musicResetButton;
        private TMP_Text musicImportStatusText;
        private readonly Dictionary<SlotId, Button> roomSlotButtons = new Dictionary<SlotId, Button>();
        private readonly Dictionary<SlotId, Button> onlineAiSlotButtons = new Dictionary<SlotId, Button>();
        private readonly Dictionary<SlotId, Button> onlineSlotButtons = new Dictionary<SlotId, Button>();
        private readonly Dictionary<SlotId, Image> onlineSlotNodeImages = new Dictionary<SlotId, Image>();
        private readonly Dictionary<SlotId, Image> onlineSlotNodeRings = new Dictionary<SlotId, Image>();
        private readonly Dictionary<SlotId, TMP_Text> onlineSlotNodeLabels = new Dictionary<SlotId, TMP_Text>();
        private readonly Dictionary<SlotId, TMP_Text> onlineSlotNodeNames = new Dictionary<SlotId, TMP_Text>();
        private readonly Dictionary<SlotId, TMP_Text> onlineSlotNodeBadges = new Dictionary<SlotId, TMP_Text>();
        private readonly Dictionary<SlotId, OnlineSeatSummary> onlineSeatsBySlot = new Dictionary<SlotId, OnlineSeatSummary>();
        private readonly List<Button> onlineDiscoveredRoomButtons = new List<Button>();
        private readonly List<string> onlineDiscoveredRoomKeys = new List<string>();
        private readonly Dictionary<string, OnlineRoomSummary> onlineDiscoveredRooms = new Dictionary<string, OnlineRoomSummary>();
        private readonly HashSet<SlotId> onlineAiSlots = new HashSet<SlotId>();
        private RoomConfig editingRoomDraft;
        private string editingRoomOriginalId;
        private string pendingDeleteRoomId;
        private OnlineClient onlineClient;
        private bool onlineMode;
        private bool onlineReady;
        private bool onlineIsHost;
        private SlotId onlineSlot;
        private List<SlotId> onlineSlots = new List<SlotId>();
        private bool onlineIsDualDevice;
        private bool onlineAuthenticated;
        private string onlineRoomKey = string.Empty;
        private string onlineClientId = string.Empty;
        private string onlineLobbySummary = string.Empty;
        private RuleVariant onlineCurrentRoomRule = RuleVariant.SpaceJump;
        private string onlineDiscoveredRoomKey = string.Empty;
        private string onlineDiscoverySummary = string.Empty;
        private string onlineJoinConfirmRoomKey = string.Empty;
        private string onlinePendingAction = string.Empty;
        private string onlinePendingActionRoomKey = string.Empty;
        private string onlinePendingJoinClientId = string.Empty;
        private string onlinePendingJoinPlayerName = string.Empty;
        private string onlinePendingRestartClientId = string.Empty;
        private string onlinePendingRestartPlayerName = string.Empty;
        private int onlineHighlightedPieceId = -1;
        private int onlineLastActionSeq;
        private string onlineLastStateSignature = string.Empty;
        private float onlineReconnectDelaySeconds;
        private float onlineAutoFinishMovedAt = -1f;
        private float onlineAutoFinishReminderAt = -1f;
        private float onlineAutoFinishSubmitAt = -1f;
        private bool onlineAutoFinishModalVisible;
        private float lastBoardNameRevealTapAt = -1f;
        private bool onlineIgnoreNextDisconnectedNotice;
        private float promptElapsedSeconds;
        private bool promptShown;
        private bool victorySoundPlayed;
        private bool refreshingUiText;
        private Vector2 lastBoardContainerSize;
        private ThemePalette activeTheme;
        private BoardCellView selectedPulseView;
        private GameObject onlineAutoFinishModal;
        private TMP_Text onlineAutoFinishModalText;

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

            defaultFont = GetDefaultFont();
            EnsureSceneScaffold();
            SetupAudio();
            saveData = SaveManager.Load();
            if (saveData.Options != null && !string.IsNullOrWhiteSpace(saveData.Options.CustomMusicPath))
            {
                StartCoroutine(LoadCustomMusic(saveData.Options.CustomMusicPath, false));
            }
            EnsureDefaultRoom();
            BuildUi();
            ApplyTheme(saveData.Options.ThemeId);
            RefreshMusicState();
            StartCoroutine(ShowSplashThenMenu());
        }

        private void OnDestroy()
        {
            DisconnectOnline();
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
        }

        private void Update()
        {
            PumpOnlineMessages();
            if (onlineClient != null)
            {
                onlineClient.UpdateRetries();
            }
            UpdateOnlineReconnect();
            UpdateOnlineAutoFinishReminder();
            UpdateBoardLayoutIfNeeded();
            UpdateBoardNameReveal();
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

        private TMP_FontAsset GetDefaultFont()
        {
            TMP_FontAsset fallbackFont = TMP_Settings.defaultFontAsset;
            TMP_FontAsset bundledChineseFont = CreateTmpFontAsset(
                Resources.Load<Font>(BundledChineseFontResource),
                "SweetJumpJumpBundledChineseTMP",
                fallbackFont);
            if (bundledChineseFont != null)
            {
                return bundledChineseFont;
            }

#if UNITY_IOS && !UNITY_EDITOR
            string[] preferredFonts =
            {
                "PingFangSC-Regular",
                "PingFang SC",
                "PingFangSC-Semibold",
                "STHeitiSC-Light",
                "STHeitiSC-Medium",
                "Heiti SC",
                "Hiragino Sans GB",
                "Arial Unicode MS",
                "Arial"
            };
#else
            string[] preferredFonts =
            {
                "PingFang SC",
                "PingFangSC-Regular",
                "Heiti SC",
                "STHeiti",
                "Hiragino Sans GB",
                "Arial Unicode MS",
                "Arial"
            };
#endif

            try
            {
                Font font = Font.CreateDynamicFontFromOSFont(preferredFonts, 32);
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }

                TMP_FontAsset fontAsset = CreateTmpFontAsset(font, "SweetJumpJumpDynamicChineseTMP", fallbackFont);
                if (fontAsset != null)
                {
                    return fontAsset;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("TMP dynamic Chinese font setup failed. Falling back to TMP default font. " + exception.Message);
            }

            if (fallbackFont != null)
            {
                return fallbackFont;
            }

            return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        private static TMP_FontAsset CreateTmpFontAsset(Font font, string assetName, TMP_FontAsset fallbackFont)
        {
            if (font == null)
            {
                return null;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            if (fontAsset == null)
            {
                return null;
            }

            fontAsset.name = assetName;
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = true;
            if (fallbackFont != null && fallbackFont != fontAsset)
            {
                fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallbackFont };
            }

            return fontAsset;
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
            onlinePanel = CreateFullscreenPanel("OnlinePanel", canvasObject.transform, new Color(1f, 0.95f, 0.98f, 0.97f));
            gamePanel = CreateFullscreenPanel("GamePanel", canvasObject.transform, new Color(1f, 0.96f, 0.98f, 0.98f));

            BuildSplashPanel();
            BuildMenuPanel();
            BuildOptionsPanel();
            BuildRoomsPanel();
            BuildRoomEditPanel();
            BuildOnlinePanel();
            BuildGamePanel();

            ShowPanel(splashPanel);
            RefreshAllUiText();
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
            onlineMode = false;
            ApplyTheme(saveData.Options.ThemeId);
            RefreshRoomCards();
            ShowPanel(roomsPanel);
        }

        private void ShowOnline()
        {
            ApplyTheme(saveData.Options.ThemeId);
            if (onlineAuthenticated && onlineClient != null && onlineClient.IsConnected)
            {
                onlineClient.Send(new OnlineMessage { type = "LIST" });
                RefreshOnlineLobby("已登录，可以创建或加入房间。");
            }
            else
            {
                string acct = saveData.Options.OnlinePlayerAccount == null ? string.Empty : saveData.Options.OnlinePlayerAccount.Trim();
                string pwd = saveData.Options.OnlinePlayerPassword ?? string.Empty;
                string token = saveData.Options.OnlineSessionToken ?? string.Empty;
                if (!string.IsNullOrEmpty(acct) && !string.IsNullOrEmpty(pwd) && !string.IsNullOrEmpty(token))
                {
                    ConnectOnlineIfNeeded();
                    RefreshOnlineLobby("正在自动登录...");
                }
                else
                {
                    RefreshOnlineLobby("请先登录，然后进入在线大厅。");
                }
            }
            ShowPanel(onlinePanel);
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
            onlineMode = false;
            selectedRoom = roomConfig;
            session = new GameSession(roomConfig);
            ResetPromptTimer();
            victorySoundPlayed = false;
            ApplyTheme(roomConfig.ThemeId);
            roomTitleText.text = roomConfig.RoomName;
            EnsureBoardCreated();
            RefreshBoard();
            victoryModal.SetActive(false);
            if (exitConfirmModal != null)
            {
                exitConfirmModal.SetActive(false);
            }
            ShowPanel(gamePanel);
            StartCoroutine(RunAiTurnIfNeeded());
        }

        private void HandleExitCurrentGame()
        {
            ShowExitConfirm();
        }

        private void ShowExitConfirm()
        {
            if (exitConfirmModal != null)
            {
                exitConfirmModal.SetActive(true);
            }
        }

        private void HideExitConfirm()
        {
            if (exitConfirmModal != null)
            {
                exitConfirmModal.SetActive(false);
            }
        }

        private void ConfirmExitCurrentGame()
        {
            if (onlineMode)
            {
                DisconnectOnline();
            }

            HideExitConfirm();
            session = null;
            selectedRoom = null;
            ShowRooms();
        }

        private IEnumerator RunAiTurnIfNeeded()
        {
            while (session != null && !session.IsGameOver && BoardLayout.IsAi(session.CurrentPlayerKind))
            {
                RefreshBoard();
                statusText.text = string.Format("{0}\nAI 思考中...", session.StatusMessage);
                yield return new WaitForSeconds(0.8f);
                MoveOption move = session.GetBestAiMove();
                if (move == null)
                {
                    session.ApplyAiMove(null);
                    ResetPromptTimer();
                    RefreshBoard();
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                for (int i = 0; i < move.Path.Count; i++)
                {
                    HexCoord stepPosition = session.ApplyAiMoveStep(move, i);
                    ResetPromptTimer();
                    RefreshBoard();
                    PlaySfx("move");
                    StartCoroutine(AnimatePiecePulse(stepPosition, 1.18f, 0.18f));
                    yield return new WaitForSeconds(0.5f);
                }

                yield return new WaitForSeconds(0.15f);
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
            BoardCellView selectedView = null;

            foreach (KeyValuePair<HexCoord, BoardCellView> entry in cellViews)
            {
                BoardCellView view = entry.Value;
                PieceState piece = session.GetPieceAt(view.Coord);
                bool isTarget = legalTargets.Contains(view.Coord);
                bool isSelected = piece != null && (piece.PieceId == session.SelectedPieceId || piece.PieceId == onlineHighlightedPieceId);
                if (isSelected)
                {
                    selectedView = view;
                }

                view.BaseImage.color = isTarget
                    ? BoardTargetColor
                    : BoardCellColor;
                if (view.SlotRingImage != null)
                {
                    view.SlotRingImage.color = isTarget
                        ? BoardTargetRingColor
                        : BoardSlotRingColor;
                }

                view.HintImage.gameObject.SetActive(isTarget);
                view.HintImage.color = BoardTargetDotColor;
                view.SelectionRing.gameObject.SetActive(isSelected);
                view.SelectionRing.color = BoardSelectionColor;
                if (view.PieceSelectionHighlight != null)
                {
                    view.PieceSelectionHighlight.gameObject.SetActive(isSelected);
                    view.PieceSelectionHighlight.color = BoardSelectionColor;
                }
                if (view.PieceShadowImage != null)
                {
                    view.PieceShadowImage.gameObject.SetActive(piece != null);
                }

                view.PieceImage.gameObject.SetActive(piece != null);

                if (piece != null)
                {
                    view.PieceImage.color = SoftenPieceColor(BoardLayout.GetPieceColor(piece.Owner));
                    if (!isSelected)
                    {
                        view.PieceImage.transform.localScale = Vector3.one;
                        if (view.PieceSelectionHighlight != null)
                        {
                            view.PieceSelectionHighlight.transform.localScale = Vector3.one;
                        }
                    }
                }
            }

            selectedPulseView = selectedView;

            RefreshCurrentPlayerPieceIndicator();
            RefreshBoardNameLabels();
            statusText.text = session.StatusMessage;
            bool showPassButton = session.CanPass && !session.CanFinishTurn;
            finishTurnButton.gameObject.SetActive(!showPassButton);
            passTurnButton.gameObject.SetActive(showPassButton);
            bool onlineMyTurn = !onlineMode || IsOnlineMyTurn();
            finishTurnButton.interactable = session.CanFinishTurn && onlineMyTurn;
            passTurnButton.interactable = session.CanPass && onlineMyTurn;
            undoButton.interactable = session.CanUndo && !onlineMode;
            if (onlineHostSettingsButton != null)
            {
                onlineHostSettingsButton.gameObject.SetActive(onlineMode && onlineIsHost);
                onlineHostSettingsButton.interactable = onlineMode && onlineIsHost && onlineClient != null && onlineClient.IsConnected && !string.IsNullOrEmpty(onlineRoomKey);
            }
            RefreshGameChromeStyle();

            if (session.IsGameOver)
            {
                ShowVictory();
            }
        }

        private void RefreshBoardNameLabels()
        {
            if (session == null || boardNameLabels.Count == 0)
            {
                return;
            }

            Dictionary<SlotId, PlayerKind> slotMap = BoardLayout.GetSlotMap(session.RoomConfig);
            foreach (KeyValuePair<SlotId, TMP_Text> entry in boardNameLabels)
            {
                PlayerKind kind;
                if (!slotMap.TryGetValue(entry.Key, out kind) || kind == PlayerKind.None)
                {
                    entry.Value.gameObject.SetActive(false);
                    continue;
                }

                float revealUntil;
                bool reveal = onlineMode && boardNameRevealUntil.TryGetValue(entry.Key, out revealUntil) && revealUntil > Time.unscaledTime;
                entry.Value.gameObject.SetActive(reveal);
                entry.Value.text = GetOnlineBoardName(entry.Key, kind);
                entry.Value.color = SoftenPieceColor(BoardLayout.GetPieceColor(entry.Key));
            }

            RefreshBoardNameButtons(slotMap);
        }

        private void RefreshBoardNameButtons(Dictionary<SlotId, PlayerKind> slotMap)
        {
            foreach (KeyValuePair<SlotId, Button> entry in boardNameButtons)
            {
                entry.Value.gameObject.SetActive(false);
                entry.Value.interactable = false;
            }
        }

        private void RevealBoardNames()
        {
            if (session == null)
            {
                return;
            }

            Dictionary<SlotId, PlayerKind> slotMap = BoardLayout.GetSlotMap(session.RoomConfig);
            float revealUntil = Time.unscaledTime + BoardNameRevealDurationSeconds;
            SlotId[] slots = BoardLayout.GetSlotsInDisplayOrder();
            for (int i = 0; i < slots.Length; i++)
            {
                PlayerKind kind;
                if (slotMap.TryGetValue(slots[i], out kind) && kind != PlayerKind.None)
                {
                    boardNameRevealUntil[slots[i]] = revealUntil;
                }
            }

            RefreshBoardNameLabels();
            PlaySfx("button");
        }

        private bool TryHandleBoardNameDoubleTap()
        {
            if (!onlineMode || session == null)
            {
                lastBoardNameRevealTapAt = -1f;
                return false;
            }

            float now = Time.unscaledTime;
            bool doubleTap = lastBoardNameRevealTapAt > 0f && now - lastBoardNameRevealTapAt <= BoardNameRevealDoubleTapSeconds;
            lastBoardNameRevealTapAt = doubleTap ? -1f : now;
            if (!doubleTap)
            {
                return false;
            }

            RevealBoardNames();
            return true;
        }

        private void UpdateBoardNameReveal()
        {
            if (!onlineMode || boardNameRevealUntil.Count == 0)
            {
                return;
            }

            bool changed = false;
            SlotId[] slots = BoardLayout.GetSlotsInDisplayOrder();
            for (int i = 0; i < slots.Length; i++)
            {
                float revealUntil;
                if (!boardNameRevealUntil.TryGetValue(slots[i], out revealUntil) || revealUntil <= 0f || revealUntil > Time.unscaledTime)
                {
                    continue;
                }

                boardNameRevealUntil[slots[i]] = 0f;
                changed = true;
            }

            if (changed)
            {
                RefreshBoardNameLabels();
            }
        }

        private string GetOnlineBoardName(SlotId slotId, PlayerKind kind)
        {
            if (BoardLayout.IsAi(kind))
            {
                return "高级人机";
            }

            string slotName = slotId.ToString();
            if (!string.IsNullOrEmpty(onlineLobbySummary))
            {
                string[] lines = onlineLobbySummary.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (!line.StartsWith(slotName + ":", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string value = line.Substring(slotName.Length + 1).Trim();
                    int readyIndex = value.IndexOf(" 已", StringComparison.Ordinal);
                    if (readyIndex >= 0)
                    {
                        value = value.Substring(0, readyIndex).Trim();
                    }

                    int offlineIndex = value.IndexOf(" 离线", StringComparison.Ordinal);
                    if (offlineIndex >= 0)
                    {
                        value = value.Substring(0, offlineIndex).Trim();
                    }

                    int hostIndex = value.IndexOf(" 房主", StringComparison.Ordinal);
                    if (hostIndex >= 0)
                    {
                        value = value.Substring(0, hostIndex).Trim();
                    }

                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }
            }

            bool isMine = (onlineSlots.Count > 0) ? onlineSlots.Contains(slotId) : slotId == onlineSlot;
            return isMine ? GetOnlinePlayerName() : BoardLayout.GetSlotLabel(slotId);
        }

        private static Color SoftenPieceColor(Color color)
        {
            Color softened = Color.Lerp(color, Color.white, 0.22f);
            softened.r = Mathf.Clamp01(softened.r);
            softened.g = Mathf.Clamp01(softened.g);
            softened.b = Mathf.Clamp01(softened.b);
            softened.a = color.a;
            return softened;
        }

        private void RefreshCurrentPlayerPieceIndicator()
        {
            if (currentPlayerPieceImage == null || session == null)
            {
                return;
            }

            currentPlayerPieceImage.gameObject.SetActive(!session.IsGameOver);
            currentPlayerPieceImage.color = SoftenPieceColor(BoardLayout.GetPieceColor(session.CurrentPlayerSlot));
        }

        private void ShowVictory()
        {
            if (victoryModal != null)
            {
                victoryModal.SetActive(false);
            }
            HideExitConfirm();

            if (statusText != null && session != null && !string.IsNullOrEmpty(session.StatusMessage))
            {
                statusText.text = session.StatusMessage;
            }

            finishTurnButton.interactable = false;
            passTurnButton.interactable = false;
            undoButton.interactable = false;
            RefreshMusicState();
            if (!victorySoundPlayed && session != null && session.IsGameOver)
            {
                victorySoundPlayed = true;
                PlaySfx("victory");
            }
        }

        private void HandleCellClicked(HexCoord coord)
        {
            if (TryHandleBoardNameDoubleTap())
            {
                return;
            }

            if (session == null || session.IsGameOver || session.CurrentPlayerKind != PlayerKind.Human)
            {
                return;
            }

            if (onlineMode && !IsOnlineMyTurn())
            {
                statusText.text = "还没轮到你。";
                PlaySfx("invalid");
                return;
            }

            string message;

            if (session.LegalTargets.Contains(coord))
            {
                if (onlineMode)
                {
                    ClearOnlineAutoFinishReminder();
                    onlineClient.Send(new OnlineMessage
                    {
                        type = "MOVE",
                        pieceId = session.SelectedPieceId,
                        q = coord.Q,
                        r = coord.R
                    });
                    return;
                }

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
            else if (onlineMode)
            {
                PieceState piece = session.GetPieceAt(coord);
                if (piece != null && (onlineSlots.Count > 0 ? onlineSlots.Contains(piece.Owner) : piece.Owner == onlineSlot))
                {
                    ClearOnlineAutoFinishReminder();
                    onlineClient.Send(new OnlineMessage
                    {
                        type = "SELECT",
                        pieceId = piece.PieceId
                    });
                    PlaySfx("select");
                }

                ClearOnlineAutoFinishReminder();
                return;
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

            if (onlineMode)
            {
                if (IsOnlineMyTurn() && session.CanFinishTurn)
                {
                    ClearOnlineAutoFinishReminder();
                    onlineClient.Send(new OnlineMessage { type = "FINISH" });
                }
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

            if (onlineMode)
            {
                if (IsOnlineMyTurn() && session.CanPass)
                {
                    ClearOnlineAutoFinishReminder();
                    onlineClient.Send(new OnlineMessage { type = "PASS" });
                }
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

            if (onlineMode)
            {
                statusText.text = "在线模式暂不支持悔棋。";
                PlaySfx("invalid");
                return;
            }

            string message;
            if (session.TryUndo(out message))
            {
                ResetPromptTimer();
                ClearOnlineAutoFinishReminder();
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

        private void CreateOnlineRoom()
        {
            if (!EnsureOnlineAuthenticatedOrQueue("CREATE", string.Empty, "正在连接并登录，稍后自动创建房间..."))
            {
                return;
            }

            SendQueuedOnlineAction("CREATE", string.Empty);
        }

        private void JoinOnlineRoom()
        {
            string key = onlineRoomKeyInput.text == null ? string.Empty : onlineRoomKeyInput.text.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(key))
            {
                RefreshOnlineLobby("请输入房间密钥。");
                return;
            }

            if (!EnsureOnlineAuthenticatedOrQueue("JOIN", key, "正在连接并登录，稍后自动加入房间 " + key + "..."))
            {
                return;
            }

            SendQueuedOnlineAction("JOIN", key);
        }

        private void JoinDiscoveredOnlineRoom()
        {
            if (string.IsNullOrEmpty(onlineDiscoveredRoomKey))
            {
                RefreshOnlineLobby("还没有发现可加入的房间。");
                return;
            }

            if (!EnsureOnlineAuthenticatedOrQueue("JOIN", onlineDiscoveredRoomKey, "正在连接并登录，稍后自动加入房间 " + onlineDiscoveredRoomKey + "..."))
            {
                return;
            }

            SendQueuedOnlineAction("JOIN", onlineDiscoveredRoomKey);
        }

        private void RequestJoinDiscoveredRoom(string roomKey)
        {
            if (string.IsNullOrEmpty(roomKey))
            {
                return;
            }

            onlineDiscoveredRoomKey = roomKey;
            JoinDiscoveredOnlineRoom();
        }

        private bool EnsureOnlineAuthenticatedOrQueue(string action, string roomKey, string waitingMessage)
        {
            ApplyOnlineAccountPassword();
            if (!onlineAuthenticated)
            {
                onlinePendingAction = action;
                onlinePendingActionRoomKey = roomKey ?? string.Empty;
                ConnectOnlineIfNeeded();
                RefreshOnlineLobby(waitingMessage);
                return false;
            }

            ConnectOnlineIfNeeded();
            if (onlineClient == null || !onlineClient.IsConnected)
            {
                return false;
            }

            if (!onlineAuthenticated)
            {
                onlinePendingAction = action;
                onlinePendingActionRoomKey = roomKey ?? string.Empty;
                RefreshOnlineLobby(waitingMessage);
                return false;
            }

            return true;
        }

        private void LoginOnline()
        {
            ApplyOnlineAccountPassword();
            ApplyOnlinePlayerName();
            string acct = saveData.Options.OnlinePlayerAccount == null ? string.Empty : saveData.Options.OnlinePlayerAccount.Trim();
            string pwd = saveData.Options.OnlinePlayerPassword ?? string.Empty;
            if (string.IsNullOrEmpty(acct) || string.IsNullOrEmpty(pwd))
            {
                RefreshOnlineLobby("请先填写账号和密码。");
                return;
            }

            string pendingAction = onlinePendingAction;
            string pendingRoomKey = onlinePendingActionRoomKey;
            DisconnectOnline();
            ClearOnlineRoomState();
            onlinePendingAction = pendingAction;
            onlinePendingActionRoomKey = pendingRoomKey;
            ConnectOnlineIfNeeded();
            RefreshOnlineLobby("正在登录...");
        }

        private void LogoutOnline()
        {
            DisconnectOnline();
            ClearOnlineRoomState();
            onlineAuthenticated = false;
            saveData.Options.OnlineSessionToken = string.Empty;
            SaveManager.Save(saveData);
            RefreshOnlineLobby("已退出登录。");
        }

        private void RunPendingOnlineAction()
        {
            if (string.IsNullOrEmpty(onlinePendingAction))
            {
                return;
            }

            string action = onlinePendingAction;
            string roomKey = onlinePendingActionRoomKey;
            onlinePendingAction = string.Empty;
            onlinePendingActionRoomKey = string.Empty;
            SendQueuedOnlineAction(action, roomKey);
        }

        private void SendQueuedOnlineAction(string action, string roomKey)
        {
            if (onlineClient == null || !onlineClient.IsConnected || !onlineAuthenticated)
            {
                return;
            }

            if (action == "CREATE")
            {
                onlineClient.Send(new OnlineMessage { type = "CREATE", ruleVariant = saveData.Options.DefaultRule.ToString() });
                RefreshOnlineLobby("正在创建房间...");
            }
            else if (action == "JOIN")
            {
                onlineClient.Send(new OnlineMessage { type = "JOIN", roomKey = roomKey });
                RefreshOnlineLobby("正在加入房间 " + roomKey + "...");
            }
        }

        private void ShowOnlineAiSettings()
        {
            if (onlineAiSettingsModal != null)
            {
                onlineAiSettingsModal.SetActive(true);
                RefreshOnlineAiButtons();
            }
        }

        private void HideOnlineAiSettings()
        {
            if (onlineAiSettingsModal != null)
            {
                onlineAiSettingsModal.SetActive(false);
            }
        }

        private void RequestOnlineRestart()
        {
            if (!onlineIsHost || onlineClient == null || !onlineClient.IsConnected || string.IsNullOrEmpty(onlineRoomKey))
            {
                RefreshOnlineLobby("请先确保房间仍保持连接。");
                return;
            }

            onlineClient.Send(new OnlineMessage
            {
                type = "RESTART_REQUEST",
                roomKey = onlineRoomKey
            });
            RefreshOnlineLobby("已发起重开投票，等待所有玩家同意。");
        }

        private void ShowOnlineHostSettings()
        {
            if (!onlineIsHost || onlineClient == null || !onlineClient.IsConnected || string.IsNullOrEmpty(onlineRoomKey))
            {
                if (statusText != null)
                {
                    statusText.text = "只有房主可以打开房间设置。";
                }
                return;
            }

            if (onlineHostSettingsModal != null)
            {
                RefreshOnlineHostRuleButtonLabel();
                onlineHostSettingsModal.SetActive(true);
            }
        }

        private void ToggleOnlineRoomRuleFromSettings()
        {
            if (!onlineIsHost || onlineClient == null || !onlineClient.IsConnected || string.IsNullOrEmpty(onlineRoomKey))
            {
                return;
            }

            if (session != null)
            {
                RefreshOnlineLobby("棋局已开始，不能切换规则。请先解散房间后重建。");
                return;
            }

            onlineCurrentRoomRule = onlineCurrentRoomRule == RuleVariant.SpaceJump
                ? RuleVariant.OnePieceJump
                : RuleVariant.SpaceJump;
            onlineClient.Send(new OnlineMessage
            {
                type = "SET_RULE",
                ruleVariant = onlineCurrentRoomRule.ToString()
            });
            RefreshOnlineHostRuleButtonLabel();
            RefreshOnlineLobby("已切换房间规则为" + BoardLayout.GetRuleLabel(onlineCurrentRoomRule) + "。该设置对本房间立即生效。");
        }

        private void RefreshOnlineHostRuleButtonLabel()
        {
            if (onlineHostRuleButton == null)
            {
                return;
            }

            SetButtonLabel(onlineHostRuleButton, "规则：" + BoardLayout.GetRuleLabel(onlineCurrentRoomRule));
            onlineHostRuleButton.interactable = session == null;
        }

        private void HideOnlineHostSettings()
        {
            if (onlineHostSettingsModal != null)
            {
                onlineHostSettingsModal.SetActive(false);
            }
        }

        private void RestartOnlineGameFromSettings()
        {
            HideOnlineHostSettings();
            if (onlineClient == null || !onlineClient.IsConnected || string.IsNullOrEmpty(onlineRoomKey))
            {
                return;
            }

            onlineClient.Send(new OnlineMessage { type = "RESTART_GAME" });
            if (statusText != null)
            {
                statusText.text = "正在重开本局...";
            }
        }

        private void DisbandOnlineRoomFromSettings()
        {
            HideOnlineHostSettings();
            if (onlineClient == null || !onlineClient.IsConnected || string.IsNullOrEmpty(onlineRoomKey))
            {
                return;
            }

            onlineClient.Send(new OnlineMessage { type = "DISBAND_ROOM" });
        }

        private void ShowOnlineRestartVote()
        {
            if (onlineRestartVoteText != null)
            {
                onlineRestartVoteText.text = onlinePendingRestartPlayerName + " 想重开一局，是否同意？";
            }

            if (onlineRestartVoteModal != null)
            {
                onlineRestartVoteModal.SetActive(true);
            }
        }

        private void HideOnlineRestartVote()
        {
            if (onlineRestartVoteModal != null)
            {
                onlineRestartVoteModal.SetActive(false);
            }
        }

        private void ApproveOnlineRestart()
        {
            if (onlineClient == null || !onlineClient.IsConnected || string.IsNullOrEmpty(onlineRoomKey))
            {
                HideOnlineRestartVote();
                return;
            }

            onlineClient.Send(new OnlineMessage
            {
                type = "RESTART_APPROVE",
                roomKey = onlineRoomKey
            });
            RefreshOnlineLobby("已同意重开，等待其他玩家回应。");
            onlinePendingRestartClientId = string.Empty;
            onlinePendingRestartPlayerName = string.Empty;
            HideOnlineRestartVote();
        }

        private void RejectOnlineRestart()
        {
            if (onlineClient == null || !onlineClient.IsConnected || string.IsNullOrEmpty(onlineRoomKey))
            {
                HideOnlineRestartVote();
                return;
            }

            onlineClient.Send(new OnlineMessage
            {
                type = "RESTART_REJECT",
                roomKey = onlineRoomKey
            });
            RefreshOnlineLobby("你拒绝了本次重开请求。");
            onlinePendingRestartClientId = string.Empty;
            onlinePendingRestartPlayerName = string.Empty;
            HideOnlineRestartVote();
        }

        private void ConfirmOnlineJoinRoom()
        {
            HideOnlineJoinRoomConfirm();
            JoinDiscoveredOnlineRoom();
        }

        private void HideOnlineJoinRoomConfirm()
        {
            if (onlineJoinRoomConfirmModal != null)
            {
                onlineJoinRoomConfirmModal.SetActive(false);
            }
        }

        private void ApprovePendingOnlineJoin()
        {
            if (onlineClient == null || !onlineClient.IsConnected || string.IsNullOrEmpty(onlineRoomKey))
            {
                HideOnlineJoinRequest();
                return;
            }

            onlineClient.Send(new OnlineMessage
            {
                type = "JOIN_APPROVE",
                roomKey = onlineRoomKey,
                clientId = onlinePendingJoinClientId
            });
            RefreshOnlineLobby("已同意加入请求。");
            onlinePendingJoinClientId = string.Empty;
            onlinePendingJoinPlayerName = string.Empty;
            HideOnlineJoinRequest();
        }

        private void RejectPendingOnlineJoin()
        {
            if (onlineClient == null || !onlineClient.IsConnected || string.IsNullOrEmpty(onlineRoomKey))
            {
                HideOnlineJoinRequest();
                return;
            }

            onlineClient.Send(new OnlineMessage
            {
                type = "JOIN_REJECT",
                roomKey = onlineRoomKey,
                clientId = onlinePendingJoinClientId
            });
            RefreshOnlineLobby("已拒绝加入请求。");
            onlinePendingJoinClientId = string.Empty;
            onlinePendingJoinPlayerName = string.Empty;
            HideOnlineJoinRequest();
        }

        private void HideOnlineJoinRequest()
        {
            if (onlineJoinRequestModal != null)
            {
                onlineJoinRequestModal.SetActive(false);
            }
        }

        private void ToggleOnlineDualDeviceMode()
        {
            saveData.Options.OnlineDualDevice = !saveData.Options.OnlineDualDevice;
            SaveManager.Save(saveData);
            RefreshOnlineSlotButtons();
            RefreshOnlineLobby(saveData.Options.OnlineDualDevice
                ? "已切换为双人共用设备：选择位置时会同时选择对家。"
                : "已切换为单人设备：选择位置时只占一个角。");
        }

        private void SelectOnlineSlot(SlotId slotId)
        {
            if (onlineClient == null || !onlineClient.IsConnected || string.IsNullOrEmpty(onlineRoomKey))
            {
                RefreshOnlineLobby("请先创建或加入房间。");
                return;
            }

            if (session != null)
            {
                RefreshOnlineLobby("棋局已开始，不能切换位置。");
                return;
            }

            List<SlotId> requestedSlots = new List<SlotId> { slotId };
            if (saveData.Options.OnlineDualDevice)
            {
                requestedSlots.Add(BoardLayout.GetOppositeSlot(slotId));
            }

            for (int i = 0; i < requestedSlots.Count; i++)
            {
                SlotId requested = requestedSlots[i];
                if (IsOnlineSeatTakenByOther(requested))
                {
                    RefreshOnlineLobby(BoardLayout.GetSlotLabel(requested) + " 已被其他玩家占用。");
                    return;
                }
            }

            onlineClient.Send(new OnlineMessage
            {
                type = "SELECT_SLOTS",
                slots = requestedSlots.Select(value => value.ToString()).ToArray()
            });
            RefreshOnlineLobby(saveData.Options.OnlineDualDevice
                ? "正在切换到 " + BoardLayout.GetSlotLabel(slotId) + " / " + BoardLayout.GetSlotLabel(requestedSlots[1]) + "..."
                : "正在切换到 " + BoardLayout.GetSlotLabel(slotId) + "...");
        }

        private void StartOnlineGame()
        {
            if (onlineClient == null || !onlineClient.IsConnected || string.IsNullOrEmpty(onlineRoomKey))
            {
                RefreshOnlineLobby("请先创建房间。");
                return;
            }

            onlineClient.Send(new OnlineMessage { type = "START" });
        }

        private void ToggleOnlineAiSlot(SlotId slotId)
        {
            // Not supported in new server protocol — no-op
        }

        private void ConnectOnlineIfNeeded()
        {
            if (onlineClient != null && onlineClient.IsConnected)
            {
                return;
            }

            string acct = saveData.Options.OnlinePlayerAccount == null ? string.Empty : saveData.Options.OnlinePlayerAccount.Trim();
            string pwd = saveData.Options.OnlinePlayerPassword ?? string.Empty;
            if (string.IsNullOrEmpty(acct) || string.IsNullOrEmpty(pwd))
            {
                RefreshOnlineLobby("请先填写账号和密码。");
                return;
            }

            try
            {
                string[] serverUrls =
                {
                    "wss://jump.mddxz.top/ws"
                };

                Exception lastException = null;
                for (int i = 0; i < serverUrls.Length; i++)
                {
                    try
                    {
                        onlineClient = new OnlineClient();
                        onlineAuthenticated = false;
                        onlineClient.Connect(serverUrls[i]);
                        onlineReconnectDelaySeconds = 0f;
                        RefreshOnlineLobby("已连接服务器：" + serverUrls[i].Replace("/ws", string.Empty));
                        return;
                    }
                    catch (Exception exception)
                    {
                        lastException = exception;
                        if (onlineClient != null)
                        {
                            onlineClient.Dispose();
                            onlineClient = null;
                        }
                    }
                }

                throw lastException ?? new InvalidOperationException("无法连接服务器。");
            }
            catch (Exception exception)
            {
                RefreshOnlineLobby("连接服务器失败：" + exception.Message);
            }
        }

        private void UpdateOnlineReconnect()
        {
            if (string.IsNullOrEmpty(onlineRoomKey) || onlineClient != null)
            {
                return;
            }

            onlineReconnectDelaySeconds -= Time.deltaTime;
            if (onlineReconnectDelaySeconds > 0f)
            {
                return;
            }

            onlineReconnectDelaySeconds = 3f;
            ConnectOnlineIfNeeded();
        }

        private void SendOnlineAuth(bool force)
        {
            if (onlineClient == null || !onlineClient.IsConnected)
            {
                RefreshOnlineLobby("服务器未连接。");
                return;
            }

            string acct = saveData.Options.OnlinePlayerAccount == null ? string.Empty : saveData.Options.OnlinePlayerAccount.Trim();
            string pwd = saveData.Options.OnlinePlayerPassword ?? string.Empty;
            onlineClient.Send(new OnlineMessage
            {
                type = "AUTH",
                account = acct,
                password = pwd,
                name = GetOnlinePlayerName(),
                dualDevice = saveData.Options.OnlineDualDevice,
                force = force
            });
        }

        private void ShowOnlineLoginConflict(string message)
        {
            if (onlineLoginConflictText != null)
            {
                onlineLoginConflictText.text = string.IsNullOrWhiteSpace(message)
                    ? "这个账号已经在其他地方登录。是否踢掉原来的登录？"
                    : message;
            }

            if (onlineLoginConflictModal != null)
            {
                onlineLoginConflictModal.SetActive(true);
            }
            else
            {
                RefreshOnlineLobby("错误：" + message);
            }
        }

        private void HideOnlineLoginConflict()
        {
            if (onlineLoginConflictModal != null)
            {
                onlineLoginConflictModal.SetActive(false);
            }
        }

        private void ConfirmOnlineLoginTakeover()
        {
            HideOnlineLoginConflict();
            SendOnlineAuth(true);
        }

        private void CancelOnlineLoginConflict()
        {
            onlinePendingAction = string.Empty;
            onlinePendingActionRoomKey = string.Empty;
            HideOnlineLoginConflict();
            RefreshOnlineLobby("已取消登录。");
        }

        private void HandleOnlineSessionReplaced(string message)
        {
            onlineIgnoreNextDisconnectedNotice = true;
            ClearOnlineAutoFinishReminder();
            ClearOnlineRoomState();
            HideOnlineLoginConflict();
            session = null;
            selectedRoom = null;
            if (victoryModal != null)
            {
                victoryModal.SetActive(false);
            }
            if (exitConfirmModal != null)
            {
                exitConfirmModal.SetActive(false);
            }
            if (onlineClient != null)
            {
                onlineClient.Dispose();
                onlineClient = null;
            }
            onlineAuthenticated = false;
            onlinePendingAction = string.Empty;
            onlinePendingActionRoomKey = string.Empty;

            ShowPanel(onlinePanel);
            RefreshOnlineLobby(string.IsNullOrWhiteSpace(message) ? "你的账号已在另一个地方登录，本端已下线。" : message);
        }

        private void PumpOnlineMessages()
        {
            if (onlineClient == null)
            {
                return;
            }

            OnlineMessage message;
            while (onlineClient.TryDequeue(out message))
            {
                HandleOnlineMessage(message);
            }
        }

        private void HandleOnlineMessage(OnlineMessage message)
        {
            if (message == null)
            {
                return;
            }

            if (message.type == "WELCOME")
            {
                onlineClientId = message.clientId;
                string acct = saveData.Options.OnlinePlayerAccount == null ? string.Empty : saveData.Options.OnlinePlayerAccount.Trim();
                string pwd = saveData.Options.OnlinePlayerPassword ?? string.Empty;
                string token = saveData.Options.OnlineSessionToken ?? string.Empty;
                if (string.IsNullOrEmpty(acct) || string.IsNullOrEmpty(pwd))
                {
                    RefreshOnlineLobby("请先填写账号和密码，再连接服务器。");
                    if (onlineClient != null)
                    {
                        onlineClient.Dispose();
                        onlineClient = null;
                    }
                    return;
                }

                if (!string.IsNullOrEmpty(token))
                {
                    onlineClient.Send(new OnlineMessage
                    {
                        type = "AUTH_TOKEN",
                        account = acct,
                        sessionToken = token,
                        name = GetOnlinePlayerName(),
                        dualDevice = saveData.Options.OnlineDualDevice,
                        force = false
                    });
                }
                else
                {
                    SendOnlineAuth(false);
                }
                return;
            }

            if (message.type == "AUTH_OK")
            {
                onlineAuthenticated = true;
                onlineClientId = string.IsNullOrEmpty(message.clientId) ? onlineClientId : message.clientId;
                if (!string.IsNullOrEmpty(message.sessionToken))
                {
                    saveData.Options.OnlineSessionToken = message.sessionToken;
                }
                if (!string.IsNullOrEmpty(message.name))
                {
                    saveData.Options.OnlinePlayerName = message.name;
                }
                SaveManager.Save(saveData);
                onlineIsDualDevice = message.dualDevice;
                RefreshOnlineLobby("已登录，可以创建或加入房间。");
                // Request room list
                onlineClient.Send(new OnlineMessage { type = "LIST" });
                RunPendingOnlineAction();
                return;
            }

            if (message.type == "ERROR")
            {
                if (message.code == "AUTH_DUPLICATE")
                {
                    ShowOnlineLoginConflict(string.IsNullOrWhiteSpace(message.message)
                        ? "这个账号已经在其他地方登录。是否踢掉原来的登录？"
                        : message.message);
                    return;
                }

                if (message.code == "SESSION_REPLACED")
                {
                    HandleOnlineSessionReplaced(message.message);
                    return;
                }

                if (message.code == "AUTH_EXPIRED" || message.code == "AUTH_DISABLED")
                {
                    onlineAuthenticated = false;
                    onlinePendingAction = string.Empty;
                    onlinePendingActionRoomKey = string.Empty;
                    saveData.Options.OnlineSessionToken = string.Empty;
                    SaveManager.Save(saveData);
                }

                if (statusText != null && gamePanel != null && gamePanel.gameObject.activeSelf)
                {
                    statusText.text = message.message;
                }
                else
                {
                    RefreshOnlineLobby("错误：" + message.message);
                }
                return;
            }

            if (message.type == "DISCONNECTED")
            {
                if (onlineIgnoreNextDisconnectedNotice)
                {
                    onlineIgnoreNextDisconnectedNotice = false;
                    return;
                }

                ClearOnlineAutoFinishReminder();
                if (onlineClient != null)
                {
                    onlineClient.Dispose();
                    onlineClient = null;
                }
                onlineAuthenticated = false;

                if (statusText != null && gamePanel != null && gamePanel.gameObject.activeSelf)
                {
                    statusText.text = message.message;
                }
                else
                {
                    RefreshOnlineLobby(message.message);
                }
                return;
            }

            if (message.type == "SESSION_REPLACED")
            {
                HandleOnlineSessionReplaced(message.message);
                return;
            }

            if (message.type == "ROOM_LIST")
            {
                UpdateOnlineDiscoveredRooms(message.rooms);
                RefreshOnlineLobby(string.Empty);
                return;
            }

            if (message.type == "ROOM")
            {
                onlineRoomKey = message.roomKey;
                if (TryParseRuleVariant(message.ruleVariant, out RuleVariant roomRuleVariant))
                {
                    onlineCurrentRoomRule = roomRuleVariant;
                }
                RefreshOnlineHostRuleButtonLabel();
                onlineClientId = string.IsNullOrEmpty(onlineClientId) ? message.clientId : onlineClientId;
                onlineIsHost = message.isHost;
                TryParseSlot(message.slot, out onlineSlot);
                onlineIsDualDevice = message.dualDevice;
                onlineSlots.Clear();
                if (message.controlledSlots != null)
                {
                    foreach (var slotName in message.controlledSlots)
                    {
                        if (TryParseSlot(slotName, out SlotId sid)) onlineSlots.Add(sid);
                    }
                }
                if (onlineSlots.Count == 0 && onlineSlot != default) onlineSlots.Add(onlineSlot);
                onlinePendingRestartClientId = string.Empty;
                onlinePendingRestartPlayerName = string.Empty;
                if (message.seats != null)
                {
                    UpdateSeatsLobby(message.seats);
                }
                else
                {
                    onlineSeatsBySlot.Clear();
                }
                RefreshOnlineLobby("房间已就绪，等待玩家加入。");
                return;
            }

            if (message.type == "LOBBY")
            {
                if (message.room != null && TryParseRuleVariant(message.room.ruleVariant, out RuleVariant lobbyRuleVariant))
                {
                    onlineCurrentRoomRule = lobbyRuleVariant;
                }
                RefreshOnlineHostRuleButtonLabel();
                onlineIsHost = message.isHost;
                onlineIsDualDevice = message.dualDevice;
                if (message.controlledSlots != null)
                {
                    onlineSlots.Clear();
                    foreach (var slotName in message.controlledSlots)
                    {
                        if (TryParseSlot(slotName, out SlotId sid)) onlineSlots.Add(sid);
                    }
                }
                if (message.seats != null)
                {
                    UpdateSeatsLobby(message.seats);
                }
                RefreshOnlineLobby(string.Empty);
                return;
            }

            if (message.type == "LOBBY_RETURN")
            {
                ClearOnlineRoomState();
                RefreshOnlineLobby("已退出房间。");
                return;
            }

            if (message.type == "KICKED")
            {
                ClearOnlineRoomState();
                RefreshOnlineLobby("你已被房主移出房间。");
                return;
            }

            if (message.type == "ROOM_DISBANDED")
            {
                ClearOnlineRoomState();
                HideOnlineHostSettings();
                session = null;
                selectedRoom = null;
                if (victoryModal != null)
                {
                    victoryModal.SetActive(false);
                }
                if (exitConfirmModal != null)
                {
                    exitConfirmModal.SetActive(false);
                }

                ShowPanel(onlinePanel);
                RefreshOnlineLobby(string.IsNullOrWhiteSpace(message.message) ? "房间已被房主解散。" : message.message);
                if (onlineClient != null && onlineClient.IsConnected)
                {
                    onlineClient.Send(new OnlineMessage { type = "LIST" });
                }
                return;
            }

            if (message.type == "STATE")
            {
                if (message.snapshot != null)
                {
                    ApplyStateSnapshot(message.snapshot, message.seats, message.roomKey, message.version);
                }
                return;
            }

            if (message.type == "RESTART_REQUEST")
            {
                onlinePendingRestartClientId = message.clientId;
                onlinePendingRestartPlayerName = string.IsNullOrWhiteSpace(message.name) ? "房主" : message.name;
                RefreshOnlineLobby(message.message);
                if (message.clientId != onlineClientId)
                {
                    ShowOnlineRestartVote();
                }
                return;
            }

            if (message.type == "RESTART_PENDING" || message.type == "RESTART_REJECTED" || message.type == "RESTART_CANCELLED")
            {
                if (message.type != "RESTART_PENDING")
                {
                    onlinePendingRestartClientId = string.Empty;
                    onlinePendingRestartPlayerName = string.Empty;
                    HideOnlineRestartVote();
                }

                RefreshOnlineLobby(message.message);
                return;
            }
        }

        private void ApplyStateSnapshot(OnlineGameSnapshot snapshot, OnlineSeatSummary[] seats, string roomKey, int version)
        {
            string signature = BuildOnlineStateSignature(snapshot, seats, roomKey, version);
            if (!string.IsNullOrEmpty(onlineLastStateSignature) && onlineLastStateSignature == signature)
            {
                return;
            }

            onlineLastStateSignature = signature;
            if (version > 0)
            {
                onlineLastActionSeq = version;
            }

            bool sessionNew = session == null;

            if (sessionNew)
            {
                // Build a minimal RoomConfig from snapshot players so GameSession can be created
                RoomConfig room = new RoomConfig
                {
                    RoomId = "online-" + (roomKey ?? onlineRoomKey),
                    RoomName = "在线房间 " + (roomKey ?? onlineRoomKey),
                    RuleVariant = onlineCurrentRoomRule,
                    SoundEnabled = saveData.Options.SoundEnabled,
                    MusicEnabled = saveData.Options.MusicEnabled,
                    PromptEnabled = false,
                    PromptIntervalSeconds = saveData.Options.PromptIntervalSeconds,
                    ThemeId = saveData.Options.ThemeId,
                    Slots = new List<SlotConfig>()
                };

                if (snapshot.players != null)
                {
                    foreach (OnlinePlayerEntry entry in snapshot.players)
                    {
                        SlotId sid;
                        PlayerKind pk;
                        if (!TryParseSlot(entry.slotId, out sid))
                        {
                            continue;
                        }

                        if (!Enum.TryParse(entry.playerKind, true, out pk))
                        {
                            pk = PlayerKind.Human;
                        }

                        room.Slots.Add(new SlotConfig { SlotId = sid, PlayerKind = pk });
                    }
                }

                if (room.Slots.Count < 2)
                {
                    RefreshOnlineLobby("等待玩家数据...");
                    return;
                }

                if (!string.IsNullOrEmpty(roomKey))
                {
                    onlineRoomKey = roomKey;
                }

                onlineMode = true;
                selectedRoom = room;
                session = new GameSession(room);
                victorySoundPlayed = false;
                ApplyTheme(room.ThemeId);
                roomTitleText.text = string.Format("{0} · 你是{1}", room.RoomName, onlineSlots.Count > 1 ? string.Join("/", onlineSlots.ConvertAll(s => BoardLayout.GetSlotLabel(s))) : BoardLayout.GetSlotLabel(onlineSlot));
                EnsureBoardCreated();
                UpdateBoardLayout(true);
                victoryModal.SetActive(false);
                if (exitConfirmModal != null)
                {
                    exitConfirmModal.SetActive(false);
                }

                HideOnlineRestartVote();
                ShowPanel(gamePanel);
            }

            // Update seat summary (for name display)
            if (seats != null)
            {
                UpdateSeatsLobby(seats);
            }

            session.ApplySnapshot(snapshot);
            ResetPromptTimer();
            RefreshBoard();

            if (session.IsGameOver && !victorySoundPlayed)
            {
                victorySoundPlayed = true;
                PlaySfx("victory");
                victoryModal.SetActive(true);
                if (victoryText != null)
                {
                    victoryText.text = session.WinnerLabel;
                }
            }

            // Online mode uses authoritative server snapshots. Avoid spawning extra AI coroutines per STATE.
        }

        private string BuildOnlineStateSignature(OnlineGameSnapshot snapshot, OnlineSeatSummary[] seats, string roomKey, int version)
        {
            if (snapshot == null)
            {
                return "";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder(1024);
            builder.Append(roomKey).Append('|').Append(version).Append('|')
                .Append(snapshot.currentPlayerSlot).Append('|')
                .Append(snapshot.currentPlayerKind).Append('|')
                .Append(snapshot.selectedPieceId).Append('|')
                .Append(snapshot.hasMovedThisTurn ? '1' : '0').Append('|')
                .Append(snapshot.isGameOver ? '1' : '0').Append('|')
                .Append(snapshot.statusMessage);

            if (snapshot.pieces != null)
            {
                for (int i = 0; i < snapshot.pieces.Length; i++)
                {
                    OnlinePieceState piece = snapshot.pieces[i];
                    if (piece == null || piece.position == null)
                    {
                        continue;
                    }

                    builder.Append('|').Append(piece.pieceId).Append(':').Append(piece.owner)
                        .Append('@').Append(piece.position.q).Append(',').Append(piece.position.r);
                }
            }

            if (seats != null)
            {
                for (int i = 0; i < seats.Length; i++)
                {
                    OnlineSeatSummary seat = seats[i];
                    if (seat == null)
                    {
                        continue;
                    }

                    builder.Append('|').Append(seat.slot).Append(':').Append(seat.name).Append(':')
                        .Append(seat.isHost ? '1' : '0').Append(':').Append(seat.clientId);
                }
            }

            return builder.ToString();
        }

        private void UpdateOnlineAutoFinishReminder()
        {
            if (!onlineMode || session == null || !IsOnlineMyTurn() || session.IsGameOver || !session.HasMovedThisTurn)
            {
                ClearOnlineAutoFinishReminder();
                return;
            }

            float now = Time.unscaledTime;
            if (onlineAutoFinishMovedAt < 0f)
            {
                onlineAutoFinishMovedAt = now;
                onlineAutoFinishReminderAt = now + 7f;
                onlineAutoFinishSubmitAt = now + 10f;
                return;
            }

            if (!onlineAutoFinishModalVisible && now >= onlineAutoFinishReminderAt)
            {
                onlineAutoFinishModalVisible = true;
                if (statusText != null)
                {
                    statusText.text = "还有 3 秒将自动完成移动；你也可以现在点“完成移动”。";
                }
                PlaySfx("prompt");
            }

            if (now >= onlineAutoFinishSubmitAt)
            {
                ClearOnlineAutoFinishReminder();
                if (onlineClient != null && onlineClient.IsConnected && session.CanFinishTurn)
                {
                    onlineClient.Send(new OnlineMessage { type = "FINISH" });
                    if (statusText != null)
                    {
                        statusText.text = "已自动完成移动。";
                    }
                }
            }
        }

        private void ClearOnlineAutoFinishReminder()
        {
            onlineAutoFinishMovedAt = -1f;
            onlineAutoFinishReminderAt = -1f;
            onlineAutoFinishSubmitAt = -1f;
            HideOnlineAutoFinishReminder();
        }

        private void ShowOnlineAutoFinishReminder()
        {
            if (onlineAutoFinishModal != null)
            {
                onlineAutoFinishModal.SetActive(true);
            }

            onlineAutoFinishModalVisible = true;
            if (onlineAutoFinishModalText != null)
            {
                onlineAutoFinishModalText.text = "你已经完成移动，请点击“完成移动”。";
            }
        }

        private void HideOnlineAutoFinishReminder()
        {
            if (onlineAutoFinishModal != null)
            {
                onlineAutoFinishModal.SetActive(false);
            }

            onlineAutoFinishModalVisible = false;
        }

        private void UpdateSeatsLobby(OnlineSeatSummary[] seats)
        {
            // Build onlineLobbySummary from seats (used by RefreshOnlineLobby for name display)
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            onlineSeatsBySlot.Clear();
            for (int i = 0; i < seats.Length; i++)
            {
                OnlineSeatSummary seat = seats[i];
                SlotId parsedSeatSlot;
                bool hasSlot = TryParseSlot(seat.slot, out parsedSeatSlot);
                if (hasSlot)
                {
                    onlineSeatsBySlot[parsedSeatSlot] = seat;
                }

                string label = BoardLayout.GetSlotLabel(hasSlot ? parsedSeatSlot : SlotId.Top);
                string dualTag = seat.isDualDevice ? "（双人端）" : string.Empty;
                sb.AppendFormat("{0}：{1}{2}{3}\n", label, seat.name ?? "?", seat.isHost ? "（房主）" : string.Empty, dualTag);
                SlotId seatSlot;
                if (TryParseSlot(seat.slot, out seatSlot) && seat.isHost)
                {
                    if ((onlineSlots.Count > 0 ? onlineSlots.Contains(seatSlot) : seatSlot == onlineSlot))
                    {
                        onlineIsHost = true;
                    }
                }
            }

            onlineLobbySummary = sb.ToString();
            RefreshOnlineSlotButtons();
        }

        private void ApplyOnlineSelect(OnlineMessage message)
        {
            // In new protocol, SELECT/MOVE are replaced by STATE snapshots.
            // This method is kept for compatibility but does nothing.
        }

        private void ApplyOnlineMove(OnlineMessage message)
        {
            // Replaced by STATE snapshots.
        }

        private void ApplyOnlineFinish()
        {
            // Replaced by STATE snapshots.
        }

        private void ApplyOnlinePass()
        {
            // Replaced by STATE snapshots.
        }

        private void StartOnlineSession(OnlineMessage message)
        {
            // Legacy — new protocol drives session via ApplyStateSnapshot from STATE messages.
        }

        private bool IsOnlineMyTurn()
        {
            if (session == null) return false;
            if (onlineSlots.Count > 0) return onlineSlots.Contains(session.CurrentPlayerSlot);
            return session.CurrentPlayerSlot == onlineSlot;
        }

        private void RefreshOnlineLobby(string message)
        {
            if (onlineLoginSection != null)
            {
                onlineLoginSection.gameObject.SetActive(!onlineAuthenticated);
            }

            if (onlineLobbySection != null)
            {
                onlineLobbySection.gameObject.SetActive(onlineAuthenticated);
            }

            if (onlineLoginButton != null)
            {
                onlineLoginButton.interactable = onlineClient == null || !onlineClient.IsConnected || !onlineAuthenticated;
            }

            if (onlineRoomKeyText != null)
            {
                onlineRoomKeyText.text = string.IsNullOrEmpty(onlineRoomKey)
                    ? "房间密钥：未创建"
                    : "房间密钥：" + onlineRoomKey + "  颜色：" + (onlineSlots.Count > 1 ? string.Join("/", onlineSlots.ConvertAll(s => BoardLayout.GetSlotLabel(s))) : BoardLayout.GetSlotLabel(onlineSlot));
            }

            if (onlineStatusText != null)
            {
                string hostLine = onlineIsHost ? "你是房主，可以开始游戏。" : "等待房主开始。";
                string ruleLine = string.IsNullOrEmpty(onlineRoomKey)
                    ? ""
                    : "规则：" + BoardLayout.GetRuleLabel(onlineCurrentRoomRule);
                if (!onlineAuthenticated)
                {
                    onlineStatusText.text = string.IsNullOrWhiteSpace(message) ? "请先登录，然后进入在线大厅。" : message;
                }
                else
                {
                    onlineStatusText.text = string.Format("{0}\n{1}\n{2}{3}", message, onlineLobbySummary, string.IsNullOrEmpty(onlineRoomKey) ? string.Empty : hostLine, string.IsNullOrEmpty(ruleLine) ? string.Empty : "\n" + ruleLine);
                }
            }

            if (onlineDiscoveryText != null)
            {
                onlineDiscoveryText.text = "发现房间";
            }

            RefreshOnlineDiscoveryList();

            if (onlineStartButton != null)
            {
                onlineStartButton.interactable = onlineIsHost && !string.IsNullOrEmpty(onlineRoomKey);
            }

            RefreshOnlineSlotButtons();
        }

        private void UpdateOnlineDiscoveredRooms(OnlineRoomSummary[] rooms)
        {
            onlineDiscoveredRoomKeys.Clear();
            onlineDiscoveredRooms.Clear();
            if (rooms == null)
            {
                return;
            }

            for (int i = 0; i < rooms.Length; i++)
            {
                OnlineRoomSummary room = rooms[i];
                if (room == null || room.started)
                {
                    continue;
                }

                string key = room.roomKey;
                if (!string.IsNullOrEmpty(key) && !onlineDiscoveredRoomKeys.Contains(key))
                {
                    onlineDiscoveredRoomKeys.Add(key);
                    onlineDiscoveredRooms[key] = room;
                }
            }
        }

        private void RefreshOnlineDiscoveryList()
        {
            if (onlineDiscoveryListContainer == null)
            {
                return;
            }

            for (int i = onlineDiscoveryListContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(onlineDiscoveryListContainer.GetChild(i).gameObject);
            }
            onlineDiscoveredRoomButtons.Clear();

            if (!string.IsNullOrEmpty(onlineRoomKey))
            {
                TMP_Text joinedText = CreateText("DiscoveryJoined", onlineDiscoveryListContainer, "已在房间中。", 25, FontStyle.Normal, new Color(0.48f, 0.32f, 0.4f), TextAnchor.MiddleCenter, size: new Vector2(900f, 46f));
                SetLayoutElement(joinedText, 46f);
                return;
            }

            if (onlineDiscoveredRoomKeys.Count == 0)
            {
                TMP_Text emptyText = CreateText("DiscoveryEmpty", onlineDiscoveryListContainer, "暂时没有可加入的房间。", 25, FontStyle.Normal, new Color(0.48f, 0.32f, 0.4f), TextAnchor.MiddleCenter, size: new Vector2(900f, 46f));
                SetLayoutElement(emptyText, 46f);
                return;
            }

            for (int i = 0; i < onlineDiscoveredRoomKeys.Count; i++)
            {
                string roomKey = onlineDiscoveredRoomKeys[i];
                string summary = "房间 " + roomKey + " · 申请加入";
                if (onlineDiscoveredRooms.TryGetValue(roomKey, out OnlineRoomSummary room) && room != null)
                {
                    int playerCount = room.players == null ? 0 : room.players.Length;
                    RuleVariant parsedRule = RuleVariant.SpaceJump;
                    if (TryParseRuleVariant(room.ruleVariant, out RuleVariant roomRule))
                    {
                        parsedRule = roomRule;
                    }

                    summary = string.Format("房间 {0} · {1} · {2}人", roomKey, BoardLayout.GetRuleLabel(parsedRule), playerCount);
                }

                Button button = CreateLayoutButton(onlineDiscoveryListContainer, summary, () => RequestJoinDiscoveredRoom(roomKey), ButtonLabelLength.SixCharacters);
                onlineDiscoveredRoomButtons.Add(button);
            }
        }

        private void RefreshOnlineSlotButtons()
        {
            if (onlineDualDeviceButton != null)
            {
                SetButtonLabel(onlineDualDeviceButton, saveData.Options.OnlineDualDevice ? "本设备：双人共用" : "本设备：单人");
                onlineDualDeviceButton.interactable = session == null;
            }

            if (onlineSlotPickerText != null)
            {
                onlineSlotPickerText.text = string.IsNullOrEmpty(onlineRoomKey)
                    ? "选择位置"
                    : (saveData.Options.OnlineDualDevice ? "选择位置（自动选择对家）" : "选择位置");
            }

            foreach (KeyValuePair<SlotId, Button> entry in onlineSlotButtons)
            {
                SlotId slotId = entry.Key;
                Button button = entry.Value;
                if (button == null)
                {
                    continue;
                }

                bool mine = onlineSlots.Contains(slotId);
                bool occupiedByOther = IsOnlineSeatTakenByOther(slotId);
                SlotId opposite = BoardLayout.GetOppositeSlot(slotId);
                bool oppositeBlocked = saveData.Options.OnlineDualDevice && IsOnlineSeatTakenByOther(opposite);
                bool canPick = !string.IsNullOrEmpty(onlineRoomKey) && session == null && !occupiedByOther && !oppositeBlocked;

                button.interactable = canPick;
                RefreshOnlineSlotDiagramNode(slotId, button, mine, occupiedByOther, oppositeBlocked, canPick);
            }
        }

        private void RefreshOnlineSlotDiagramNode(SlotId slotId, Button button, bool mine, bool occupiedByOther, bool oppositeBlocked, bool canPick)
        {
            Color pieceColor = SoftenPieceColor(BoardLayout.GetPieceColor(slotId));
            Color displayColor = pieceColor;
            float alpha = 0.82f;
            if (string.IsNullOrEmpty(onlineRoomKey))
            {
                displayColor = Color.Lerp(pieceColor, Color.white, 0.58f);
                alpha = 0.42f;
            }
            else if (mine)
            {
                displayColor = pieceColor;
                alpha = 1f;
            }
            else if (occupiedByOther)
            {
                displayColor = Color.Lerp(pieceColor, Color.black, 0.16f);
                alpha = 0.9f;
            }
            else if (oppositeBlocked || session != null)
            {
                displayColor = Color.Lerp(pieceColor, new Color(0.78f, 0.78f, 0.78f), 0.5f);
                alpha = 0.48f;
            }
            else if (canPick)
            {
                displayColor = Color.Lerp(pieceColor, Color.white, 0.08f);
                alpha = 0.95f;
            }

            displayColor.a = alpha;
            if (onlineSlotNodeImages.TryGetValue(slotId, out Image image) && image != null)
            {
                image.color = displayColor;
            }

            if (onlineSlotNodeRings.TryGetValue(slotId, out Image ring) && ring != null)
            {
                ring.color = mine
                    ? new Color(1f, 1f, 1f, 0.94f)
                    : (canPick ? new Color(1f, 1f, 1f, 0.62f) : new Color(0.45f, 0.45f, 0.45f, 0.36f));
            }

            OnlineSeatSummary seat = null;
            onlineSeatsBySlot.TryGetValue(slotId, out seat);
            string nameText = "空位";
            if (mine)
            {
                nameText = "我";
            }
            else if (seat != null && !string.IsNullOrEmpty(seat.name))
            {
                nameText = TrimOnlineSeatName(seat.name);
            }
            else if (oppositeBlocked)
            {
                nameText = "对家占用";
            }
            else if (session != null)
            {
                nameText = "本局中";
            }

            Color textColor = mine ? new Color(1f, 1f, 1f, 0.96f) : new Color(0.32f, 0.24f, 0.3f, 0.88f);
            if (onlineSlotNodeLabels.TryGetValue(slotId, out TMP_Text label) && label != null)
            {
                label.text = BoardLayout.GetSlotLabel(slotId);
                label.color = textColor;
                RequestTextCharacters(label, label.text, label.fontSize);
            }

            if (onlineSlotNodeNames.TryGetValue(slotId, out TMP_Text nameLabel) && nameLabel != null)
            {
                nameLabel.text = nameText;
                nameLabel.color = mine ? new Color(1f, 1f, 1f, 0.86f) : new Color(0.24f, 0.22f, 0.26f, 0.72f);
                RequestTextCharacters(nameLabel, nameLabel.text, nameLabel.fontSize);
            }

            if (onlineSlotNodeBadges.TryGetValue(slotId, out TMP_Text badge) && badge != null)
            {
                bool showBadge = seat != null && seat.isHost;
                badge.gameObject.SetActive(showBadge);
                if (showBadge)
                {
                    badge.text = "★";
                    RequestTextCharacters(badge, badge.text, badge.fontSize);
                }
            }

            if (button != null)
            {
                button.targetGraphic = image;
            }
        }

        private static string TrimOnlineSeatName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length > 5 ? value.Substring(0, 5) + "…" : value;
        }

        private bool IsOnlineSeatTakenByOther(SlotId slotId)
        {
            if (!onlineSeatsBySlot.TryGetValue(slotId, out OnlineSeatSummary seat) || seat == null)
            {
                return false;
            }

            return !onlineSlots.Contains(slotId);
        }

        private void UpdateOnlineAiSlots(string aiSlots)
        {
            onlineAiSlots.Clear();
            string[] values = (aiSlots ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < values.Length; i++)
            {
                SlotId slotId;
                if (TryParseSlot(values[i], out slotId))
                {
                    onlineAiSlots.Add(slotId);
                }
            }
        }

        private void RefreshOnlineAiButtons()
        {
            foreach (KeyValuePair<SlotId, Button> entry in onlineAiSlotButtons)
            {
                Button button = entry.Value;
                if (button == null)
                {
                    continue;
                }

                bool enabled = onlineAiSlots.Contains(entry.Key);
                SetButtonLabel(button, string.Format("{0}：{1}", BoardLayout.GetSlotLabel(entry.Key), enabled ? "高级AI" : "空位"));
                button.interactable = onlineIsHost && !string.IsNullOrEmpty(onlineRoomKey) && !onlineSlots.Contains(entry.Key) && entry.Key != onlineSlot;
            }
        }

        private void DisconnectOnline()
        {
            ClearOnlineAutoFinishReminder();
            if (onlineClient != null)
            {
                onlineClient.Dispose();
                onlineClient = null;
            }

            onlineAuthenticated = false;
            onlinePendingAction = string.Empty;
            onlinePendingActionRoomKey = string.Empty;
            ClearOnlineRoomState();
            onlineClientId = string.Empty;
        }

        private void ClearOnlineRoomState()
        {
            ClearOnlineAutoFinishReminder();
            onlineMode = false;
            onlineIsHost = false;
            onlineIsDualDevice = false;
            onlineSlots.Clear();
            onlineRoomKey = string.Empty;
            onlineCurrentRoomRule = saveData != null && saveData.Options != null ? saveData.Options.DefaultRule : RuleVariant.SpaceJump;
            onlineLobbySummary = string.Empty;
            onlineDiscoveredRoomKey = string.Empty;
            onlinePendingRestartClientId = string.Empty;
            onlinePendingRestartPlayerName = string.Empty;
            onlinePendingAction = string.Empty;
            onlinePendingActionRoomKey = string.Empty;
            onlineReconnectDelaySeconds = 0f;
            onlineLastActionSeq = 0;
            onlineLastStateSignature = string.Empty;
            selectedPulseView = null;
            onlineDiscoveredRoomKeys.Clear();
            onlineSeatsBySlot.Clear();
            HideOnlineRestartVote();
            HideOnlineLoginConflict();
            HideOnlineHostSettings();
            RefreshOnlineSlotButtons();
        }

        private static bool TryParseSlot(string value, out SlotId slotId)
        {
            return Enum.TryParse(value, true, out slotId);
        }

        private static bool TryParseRuleVariant(string value, out RuleVariant ruleVariant)
        {
            return Enum.TryParse(value, true, out ruleVariant);
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

            if (saveData.SaveVersion < 4)
            {
                EnsureOnlineIdentity();
                saveData.SaveVersion = 4;
            }

            if (saveData.SaveVersion < 5)
            {
                saveData.Options.DefaultRule = RuleVariant.SpaceJump;
                saveData.SaveVersion = 5;
            }

            EnsureOnlineIdentity();

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

        private void EnsureOnlineIdentity()
        {
            if (saveData == null)
            {
                saveData = new AppSaveData();
            }

            if (saveData.Options == null)
            {
                saveData.Options = BoardLayout.CreateDefaultOptions();
            }

            if (string.IsNullOrWhiteSpace(saveData.Options.OnlinePlayerToken))
            {
                saveData.Options.OnlinePlayerToken = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(saveData.Options.OnlinePlayerName))
            {
                saveData.Options.OnlinePlayerName = string.IsNullOrWhiteSpace(Environment.UserName) ? "玩家" : Environment.UserName;
            }
        }

        private string GetOnlinePlayerToken()
        {
            EnsureOnlineIdentity();
            return saveData.Options.OnlinePlayerToken;
        }

        private string GetOnlinePlayerName()
        {
            EnsureOnlineIdentity();
            return string.IsNullOrWhiteSpace(saveData.Options.OnlinePlayerName) ? "玩家" : saveData.Options.OnlinePlayerName.Trim();
        }

        private string GetOnlineAccount()
        {
            if (saveData != null && saveData.Options != null && !string.IsNullOrWhiteSpace(saveData.Options.OnlinePlayerAccount))
            {
                return saveData.Options.OnlinePlayerAccount.Trim();
            }

            // Fall back to token-based pseudo-account for backward compat
            EnsureOnlineIdentity();
            return saveData.Options.OnlinePlayerToken;
        }

        private string GetOnlinePassword()
        {
            if (saveData != null && saveData.Options != null && !string.IsNullOrWhiteSpace(saveData.Options.OnlinePlayerPassword))
            {
                return saveData.Options.OnlinePlayerPassword;
            }

            // Fall back to token as password
            EnsureOnlineIdentity();
            return saveData.Options.OnlinePlayerToken;
        }

        private void ApplyOnlineAccountPassword()
        {
            if (onlineAccountInput == null || onlinePasswordInput == null)
            {
                return;
            }

            string account = onlineAccountInput.text == null ? string.Empty : onlineAccountInput.text.Trim();
            string password = onlinePasswordInput.text ?? string.Empty;
            bool authChanged = false;
            if (!string.IsNullOrEmpty(account))
            {
                authChanged = !string.Equals(saveData.Options.OnlinePlayerAccount ?? string.Empty, account, StringComparison.Ordinal);
                saveData.Options.OnlinePlayerAccount = account;
            }

            if (!string.IsNullOrEmpty(password))
            {
                authChanged = authChanged || !string.Equals(saveData.Options.OnlinePlayerPassword ?? string.Empty, password, StringComparison.Ordinal);
                saveData.Options.OnlinePlayerPassword = password;
            }

            if (authChanged)
            {
                saveData.Options.OnlineSessionToken = string.Empty;
            }

            SaveManager.Save(saveData);
        }

        private void ApplyOnlinePlayerName()
        {
            if (onlinePlayerNameInput == null)
            {
                return;
            }

            EnsureOnlineIdentity();
            string value = onlinePlayerNameInput.text == null ? string.Empty : onlinePlayerNameInput.text.Trim();
            saveData.Options.OnlinePlayerName = string.IsNullOrEmpty(value) ? "玩家" : value;
            SaveManager.Save(saveData);
            if (onlineClient != null && onlineClient.IsConnected)
            {
                onlineClient.Send(new OnlineMessage { type = "UPDATE_NICKNAME", name = GetOnlinePlayerName() });
            }

            RefreshOnlineLobby("昵称已更新。");
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

            if (string.IsNullOrWhiteSpace(saveData.Options.CustomMusicPath))
            {
                saveData.Options.CustomMusicPath = string.Empty;
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
                GameObject card = new GameObject("RoomCard", typeof(RectTransform), typeof(Image));
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
                element.preferredHeight = 470f;

                TMP_Text roomName = CreateText("RoomName", card.transform, room.RoomName, 48, FontStyle.Bold, new Color(0.47f, 0.22f, 0.31f), TextAnchor.MiddleLeft);
                roomName.gameObject.AddComponent<LayoutElement>().preferredHeight = 62f;
                TMP_Text roomBody = CreateText(
                    "RoomBody",
                    card.transform,
                    GetRoomSummary(room),
                    34,
                    FontStyle.Normal,
                    new Color(0.53f, 0.35f, 0.42f),
                    TextAnchor.UpperLeft);
                roomBody.enableAutoSizing = false;
                roomBody.fontSize = 30;
                roomBody.lineSpacing = 0.92f;
                roomBody.gameObject.AddComponent<LayoutElement>().preferredHeight = 252f;

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
                "{0} 人对局\n规则：{1}  主题：{2}\n催促：{3}/{4}秒\n{5}",
                playerCount,
                BoardLayout.GetRuleLabel(room.RuleVariant),
                GetThemeLabel(room.ThemeId),
                room.PromptEnabled ? "开" : "关",
                room.PromptIntervalSeconds,
                FormatRoomSummaryRows(activeSlots));
        }

        private static string FormatRoomSummaryRows(List<string> activeSlots)
        {
            List<string> rows = new List<string>();
            for (int i = 0; i < activeSlots.Count; i += 2)
            {
                if (i + 1 < activeSlots.Count)
                {
                    rows.Add(activeSlots[i] + "  " + activeSlots[i + 1]);
                }
                else
                {
                    rows.Add(activeSlots[i]);
                }
            }

            return string.Join("\n", rows.ToArray());
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
                "当前配置\n默认规则：{0}\n主题：{1}\n音效：{2}\n背景音乐：{3}\n催促：{4} / {5} 秒\n音乐文件：{6}",
                BoardLayout.GetRuleLabel(saveData.Options.DefaultRule),
                GetThemeLabel(saveData.Options.ThemeId),
                saveData.Options.SoundEnabled ? "开启" : "关闭",
                saveData.Options.MusicEnabled ? "开启" : "关闭",
                saveData.Options.PromptEnabled ? "开启" : "关闭",
                saveData.Options.PromptIntervalSeconds,
                string.IsNullOrWhiteSpace(saveData.Options.CustomMusicPath) ? "默认旋律" : Path.GetFileName(saveData.Options.CustomMusicPath));

            SetButtonLabel(ruleToggleButton, string.Format("默认规则：{0}", BoardLayout.GetRuleLabel(saveData.Options.DefaultRule)));
            SetButtonLabel(themeToggleButton, string.Format("背景主题：{0}", GetThemeLabel(saveData.Options.ThemeId)));
            SetButtonLabel(soundToggleButton, saveData.Options.SoundEnabled ? "音效：开" : "音效：关");
            SetButtonLabel(musicToggleButton, saveData.Options.MusicEnabled ? "背景音乐：开" : "背景音乐：关");
            SetButtonLabel(promptToggleButton, saveData.Options.PromptEnabled ? "催促：开" : "催促：关");
            SetButtonLabel(promptIntervalButton, string.Format("催促间隔：{0} 秒", saveData.Options.PromptIntervalSeconds));
            SetButtonLabel(musicPickButton, NativeMusicPicker.IsSupported ? "从文件选择MP3" : "选择MP3文件");
            SetButtonLabel(musicResetButton, "恢复默认音乐");
            if (musicImportStatusText != null)
            {
                musicImportStatusText.text = string.IsNullOrWhiteSpace(saveData.Options.CustomMusicPath)
                    ? "当前使用内置背景音乐。"
                    : "当前音乐：" + Path.GetFileName(saveData.Options.CustomMusicPath);
            }
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

        private void ChooseMusicFile()
        {
            if (NativeMusicPicker.IsSupported)
            {
                if (musicImportStatusText != null)
                {
                    musicImportStatusText.text = "正在打开文件选择器...";
                }

                NativeMusicPicker.Open(gameObject.name);
                return;
            }

            if (musicImportStatusText != null)
            {
                musicImportStatusText.text = "请在 iPad/iPhone 上通过“文件”选择 MP3。";
            }
        }

        public void OnNativeMusicPicked(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                OnNativeMusicPickCancelled(string.Empty);
                return;
            }

            if (musicImportStatusText != null)
            {
                musicImportStatusText.text = "正在导入：" + Path.GetFileName(path);
            }

            StartCoroutine(LoadCustomMusic(path, true));
        }

        public void OnNativeMusicPickCancelled(string message)
        {
            if (musicImportStatusText != null)
            {
                musicImportStatusText.text = string.IsNullOrWhiteSpace(message) ? "未选择音乐文件。" : message;
            }
        }

        private void ResetMusicToDefault()
        {
            saveData.Options.CustomMusicPath = string.Empty;
            musicClip = CreateMusicLoop();
            musicSource.Stop();
            musicSource.clip = musicClip;
            SaveManager.Save(saveData);
            RefreshMusicState();
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
                Cell = new Color(0.78f, 0.87f, 0.93f, 0.94f),
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
            Cell = new Color(0.78f, 0.87f, 0.93f, 0.94f),
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

            RectTransform[] panels = { splashPanel, menuPanel, optionsPanel, roomsPanel, roomEditPanel, onlinePanel, gamePanel };
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
                    // 棋盘底部操作栏半透明黑色遮罩；alpha 越大，背景越暗。
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
                    // 棋盘底部按钮默认底色，接近黑色半透明。
                    image.color = new Color(0.02f, 0.025f, 0.03f, 0.88f);
                }

                ColorBlock colors = button.colors;
                // 棋盘底部按钮交互状态颜色：normal/highlighted/pressed/disabled。
                colors.normalColor = new Color(0.02f, 0.025f, 0.03f, 0.88f);
                colors.highlightedColor = new Color(0.18f, 0.2f, 0.22f, 0.92f);
                colors.pressedColor = new Color(0f, 0f, 0f, 0.96f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(0.05f, 0.05f, 0.05f, 0.36f);
                button.colors = colors;

                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    // 棋盘底部按钮文字颜色；不可点时降低透明度。
                    label.color = new Color(1f, 1f, 1f, button.interactable ? 0.96f : 0.48f);
                    ButtonLabelFitter fitter = button.GetComponent<ButtonLabelFitter>();
                    if (fitter != null)
                    {
                        fitter.ApplyNow();
                    }
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

        private bool ShouldPlayMusic()
        {
            return gamePanel != null
                && gamePanel.gameObject.activeInHierarchy
                && session != null
                && selectedRoom != null
                && !session.IsGameOver
                && IsMusicEnabled();
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

            if (ShouldPlayMusic())
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

        private IEnumerator LoadCustomMusic(string path, bool persist)
        {
            string resolvedPath = path;
            if (!Path.IsPathRooted(resolvedPath))
            {
                resolvedPath = Path.Combine(Application.persistentDataPath, resolvedPath);
            }

            if (!File.Exists(resolvedPath))
            {
                if (persist && optionsSummaryText != null)
                {
                    optionsSummaryText.text = "没有找到 MP3 文件：\n" + resolvedPath;
                }
                if (persist && musicImportStatusText != null)
                {
                    musicImportStatusText.text = "没有找到 MP3 文件。";
                }
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip("file://" + resolvedPath, AudioType.MPEG))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (persist && optionsSummaryText != null)
                    {
                        optionsSummaryText.text = "导入 MP3 失败：\n" + request.error;
                    }
                    if (persist && musicImportStatusText != null)
                    {
                        musicImportStatusText.text = "导入 MP3 失败：" + request.error;
                    }
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null)
                {
                    if (persist && optionsSummaryText != null)
                    {
                        optionsSummaryText.text = "导入 MP3 失败：音频内容为空。";
                    }
                    if (persist && musicImportStatusText != null)
                    {
                        musicImportStatusText.text = "导入 MP3 失败：音频内容为空。";
                    }
                    yield break;
                }

                musicClip = clip;
                musicSource.Stop();
                musicSource.clip = musicClip;
                if (persist)
                {
                    saveData.Options.CustomMusicPath = resolvedPath;
                    SaveManager.Save(saveData);
                }
                RefreshMusicState();
                RefreshOptionsSummary();
            }
        }

        private void UpdateSelectionPulse()
        {
            if (session == null || session.SelectedPieceId < 0 || selectedPulseView == null || selectedPulseView.PieceImage == null || !selectedPulseView.PieceImage.gameObject.activeSelf)
            {
                return;
            }

            float scale = 1.08f + Mathf.Sin(Time.time * 7f) * 0.045f;
            selectedPulseView.PieceImage.transform.localScale = Vector3.one * scale;
            if (selectedPulseView.PieceSelectionHighlight != null)
            {
                selectedPulseView.PieceSelectionHighlight.transform.localScale = Vector3.one * scale;
            }
            if (selectedPulseView.SelectionRing != null)
            {
                selectedPulseView.SelectionRing.color = BoardSelectionColor;
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
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;

                Image baseImage = cellObject.GetComponent<Image>();
                baseImage.sprite = GenerateHexCellSprite();
                // 单个六边形格子的颜色，运行中会在普通格/可走格之间切换。
                baseImage.color = BoardCellColor;

                Button button = cellObject.GetComponent<Button>();
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.pressedColor = Color.white;
                colors.selectedColor = Color.white;
                colors.disabledColor = Color.white;
                button.colors = colors;

                GameObject ringObject = CreateCircleImage("SlotRing", cellObject.transform, BoardBaseSlotRingSize, BoardSlotRingColor);
                Image ringImage = ringObject.GetComponent<Image>();
                // 六边形内部的小圆单线，直径由 BoardBaseSlotRingSize 控制。
                ringImage.sprite = GenerateRingSprite();

                GameObject hintObject = new GameObject("Hint", typeof(RectTransform), typeof(Image));
                hintObject.transform.SetParent(cellObject.transform, false);
                RectTransform hintRect = hintObject.GetComponent<RectTransform>();
                hintRect.anchorMin = new Vector2(0.5f, 0.5f);
                hintRect.anchorMax = new Vector2(0.5f, 0.5f);
                // 可走目标中心点直径 = 小圆单线直径的 72%。
                hintRect.sizeDelta = new Vector2(BoardBaseSlotRingSize * 0.72f, BoardBaseSlotRingSize * 0.72f);
                hintRect.anchoredPosition = Vector2.zero;
                Image hintImage = hintObject.GetComponent<Image>();
                hintImage.sprite = GenerateCircleSprite();
                // 可走目标中心点颜色。
                hintImage.color = BoardTargetDotColor;
                hintImage.raycastTarget = false;
                hintImage.gameObject.SetActive(false);

                // 棋子阴影颜色，偏黑蓝并带透明度。
                GameObject shadowObject = CreateCircleImage("PieceShadow", cellObject.transform, BoardBasePieceShadowSize, new Color(0.04f, 0.06f, 0.08f, 0.3f));
                Image shadowImage = shadowObject.GetComponent<Image>();
                shadowObject.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                shadowImage.gameObject.SetActive(false);

                GameObject pieceObject = CreateCircleImage("Piece", cellObject.transform, BoardBasePieceSize, Color.white);
                Image pieceImage = pieceObject.GetComponent<Image>();
                // 棋子贴图是灰度光照纹理，真正颜色来自 BoardLayout.GetPieceColor。
                pieceImage.sprite = GeneratePieceSprite();
                pieceImage.gameObject.SetActive(false);

                GameObject highlightObject = CreateCircleImage("PieceSelectionHighlight", cellObject.transform, BoardBasePieceSize, BoardSelectionColor);
                Image highlightImage = highlightObject.GetComponent<Image>();
                // 选中棋子的局部亮斑。
                highlightImage.sprite = GeneratePieceHighlightSprite();
                highlightImage.gameObject.SetActive(false);

                GameObject selectionObject = CreateCircleImage("Selection", cellObject.transform, BoardBaseSelectionSize, BoardSelectionColor);
                Image selectionImage = selectionObject.GetComponent<Image>();
                selectionImage.sprite = GeneratePieceHighlightSprite();
                selectionImage.gameObject.SetActive(false);

                BoardCellView view = cellObject.AddComponent<BoardCellView>();
                view.Coord = coord;
                view.Button = button;
                view.BaseImage = baseImage;
                view.SlotRingImage = ringImage;
                view.PieceShadowImage = shadowImage;
                view.PieceImage = pieceImage;
                view.PieceSelectionHighlight = highlightImage;
                view.HintImage = hintImage;
                view.SelectionRing = selectionImage;

                button.onClick.AddListener(() => HandleCellClicked(coord));
                cellViews[coord] = view;
            }

            EnsureBoardNameWidgets();
            UpdateBoardLayout(true);
        }

        private void EnsureBoardNameWidgets()
        {
            if (boardContainer == null)
            {
                return;
            }

            SlotId[] slots = BoardLayout.GetSlotsInDisplayOrder();
            for (int i = 0; i < slots.Length; i++)
            {
                if (boardNameLabels.ContainsKey(slots[i]))
                {
                    continue;
                }

                TMP_Text label = CreateText(
                    "BoardName_" + slots[i],
                    boardContainer,
                    string.Empty,
                    26,
                    FontStyle.Bold,
                    new Color(1f, 1f, 1f, 0.94f),
                    TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(220f, 48f));
                label.raycastTarget = false;
                label.outlineWidth = 0.18f;
                label.outlineColor = new Color(0f, 0f, 0f, 0.48f);
                label.gameObject.SetActive(false);
                boardNameLabels[slots[i]] = label;
            }
        }

        private void UpdateBoardLayoutIfNeeded()
        {
            if (boardContainer == null || cellViews.Count == 0)
            {
                return;
            }

            Vector2 size = boardContainer.rect.size;
            if (Mathf.Abs(size.x - lastBoardContainerSize.x) > 0.5f || Mathf.Abs(size.y - lastBoardContainerSize.y) > 0.5f)
            {
                UpdateBoardLayout(false);
            }
        }

        private void UpdateBoardLayout(bool force)
        {
            if (boardContainer == null || cellViews.Count == 0)
            {
                return;
            }

            Vector2 containerSize = boardContainer.rect.size;
            if (containerSize.x <= 1f || containerSize.y <= 1f)
            {
                containerSize = boardContainer.sizeDelta;
            }

            if (!force && (containerSize.x <= 1f || containerSize.y <= 1f))
            {
                return;
            }

            float viewRotation = GetBoardViewRotationDegrees();
            float radius = CalculateAdaptiveBoardRadius(containerSize, viewRotation);
            Vector2 centerOffset = CalculateBoardCenterOffset(radius, viewRotation);

            foreach (BoardCellView view in cellViews.Values)
            {
                float x = Mathf.Sqrt(3f) * radius * (view.Coord.Q + (view.Coord.R * 0.5f)) - centerOffset.x;
                float y = -1.5f * radius * view.Coord.R - centerOffset.y;
                Vector2 rotated = RotatePoint(new Vector2(x + centerOffset.x, y + centerOffset.y), viewRotation) - centerOffset;

                RectTransform cellRect = view.GetComponent<RectTransform>();
                cellRect.sizeDelta = new Vector2(radius * 2f, radius * 2f);
                cellRect.anchoredPosition = rotated;

                SetRectSize(view.HintImage, BoardBaseSlotRingSize * 0.72f * radius / BoardBaseCellRadius);
                SetRectSize(view.SlotRingImage, BoardBaseSlotRingSize * radius / BoardBaseCellRadius);
                SetRectSize(view.PieceShadowImage, BoardBasePieceShadowSize * radius / BoardBaseCellRadius);
                SetRectSize(view.PieceImage, BoardBasePieceSize * radius / BoardBaseCellRadius);
                SetRectSize(view.PieceSelectionHighlight, BoardBasePieceSize * radius / BoardBaseCellRadius);
                SetRectSize(view.SelectionRing, BoardBaseSelectionSize * radius / BoardBaseCellRadius);

                if (view.PieceShadowImage != null)
                {
                    view.PieceShadowImage.rectTransform.anchoredPosition = new Vector2(8f, -9f) * radius / BoardBaseCellRadius;
                }
            }

            UpdateBoardNameLabelLayout(radius, centerOffset, viewRotation);
            lastBoardContainerSize = containerSize;
        }

        private void UpdateBoardNameLabelLayout(float radius, Vector2 centerOffset, float viewRotation)
        {
            if (boardNameLabels.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<SlotId, TMP_Text> entry in boardNameLabels)
            {
                HexCoord coord = GetBoardNameAnchor(entry.Key);
                float x = Mathf.Sqrt(3f) * radius * (coord.Q + (coord.R * 0.5f)) - centerOffset.x;
                float y = -1.5f * radius * coord.R - centerOffset.y;
                Vector2 rotated = RotatePoint(new Vector2(x + centerOffset.x, y + centerOffset.y), viewRotation) - centerOffset;

                Vector2 fromCenter = rotated.sqrMagnitude > 0.1f ? rotated.normalized : Vector2.up;
                RectTransform rect = entry.Value.rectTransform;
                rect.sizeDelta = new Vector2(236f, 50f);
                rect.anchoredPosition = rotated + new Vector2(fromCenter.x * 158f, fromCenter.y * 108f);
            }
        }

        private static HexCoord GetBoardNameAnchor(SlotId slotId)
        {
            switch (slotId)
            {
                case SlotId.Top:
                    return new HexCoord(4, -8);
                case SlotId.TopRight:
                    return new HexCoord(8, -4);
                case SlotId.BottomRight:
                    return new HexCoord(4, 4);
                case SlotId.Bottom:
                    return new HexCoord(-4, 8);
                case SlotId.BottomLeft:
                    return new HexCoord(-8, 4);
                case SlotId.TopLeft:
                    return new HexCoord(-4, -4);
                default:
                    return new HexCoord(0, 0);
            }
        }

        private static void SetRectSize(Graphic graphic, float size)
        {
            if (graphic == null)
            {
                return;
            }

            graphic.rectTransform.sizeDelta = new Vector2(size, size);
        }

        private float CalculateAdaptiveBoardRadius(Vector2 containerSize, float rotationDegrees)
        {
            Vector4 bounds = CalculateBoardBounds(1f, rotationDegrees);
            float boardWidth = bounds.z - bounds.x;
            float boardHeight = bounds.w - bounds.y;
            float safeWidth = containerSize.x * (1f - BoardSafeMarginRatio * 2f);
            float safeHeight = containerSize.y * (1f - BoardSafeMarginRatio * 2f);
            return Mathf.Max(1f, Mathf.Min(safeWidth / boardWidth, safeHeight / boardHeight));
        }

        private Vector2 CalculateBoardCenterOffset(float radius, float rotationDegrees)
        {
            Vector4 bounds = CalculateBoardBounds(radius, rotationDegrees);
            return new Vector2((bounds.x + bounds.z) * 0.5f, (bounds.y + bounds.w) * 0.5f);
        }

        private Vector4 CalculateBoardBounds(float radius, float rotationDegrees)
        {
            IReadOnlyList<HexCoord> cells = BoardLayout.AllCells;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            for (int i = 0; i < cells.Count; i++)
            {
                HexCoord coord = cells[i];
                float x = Mathf.Sqrt(3f) * radius * (coord.Q + (coord.R * 0.5f));
                float y = -1.5f * radius * coord.R;
                Vector2 rotated = RotatePoint(new Vector2(x, y), rotationDegrees);
                minX = Mathf.Min(minX, rotated.x - radius);
                maxX = Mathf.Max(maxX, rotated.x + radius);
                minY = Mathf.Min(minY, rotated.y - radius);
                maxY = Mathf.Max(maxY, rotated.y + radius);
            }

            return new Vector4(minX, minY, maxX, maxY);
        }

        private float GetBoardViewRotationDegrees()
        {
            if (!onlineMode)
            {
                return 0f;
            }

            switch (onlineSlot)
            {
                case SlotId.Bottom:
                    return 0f;
                case SlotId.BottomLeft:
                    return 60f;
                case SlotId.TopLeft:
                    return 120f;
                case SlotId.Top:
                    return 180f;
                case SlotId.TopRight:
                    return -120f;
                case SlotId.BottomRight:
                    return -60f;
                default:
                    return 0f;
            }
        }

        private static Vector2 RotatePoint(Vector2 point, float degrees)
        {
            if (Mathf.Abs(degrees) < 0.001f)
            {
                return point;
            }

            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(point.x * cos - point.y * sin, point.x * sin + point.y * cos);
        }

        private void BuildSplashPanel()
        {
            CreateText("SplashCaption", splashPanel, "农夫山泉有点甜", 36, FontStyle.Normal, new Color(0.64f, 0.4f, 0.48f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.43f), new Vector2(0.5f, 0.43f), new Vector2(900f, 60f));
            splashTitleText = CreateText("SplashTitle", splashPanel, "甜姐的跳跳棋", 96, FontStyle.Bold, new Color(0.73f, 0.24f, 0.42f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), new Vector2(1200f, 140f));
            CreateText("SplashDecor", splashPanel, "★  ○  ✦  ○  ★", 50, FontStyle.Bold, new Color(1f, 0.74f, 0.82f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.33f), new Vector2(900f, 80f));
        }

        private void BuildMenuPanel()
        {
            CreateText("MenuTitle", menuPanel, "甜姐的跳跳棋", 88, FontStyle.Bold, new Color(0.72f, 0.24f, 0.41f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.76f), new Vector2(0.5f, 0.76f), new Vector2(1100f, 120f));
            CreateText("MenuSubtitle", menuPanel, "甜姐第一 比赛第二", 34, FontStyle.Normal, new Color(0.55f, 0.37f, 0.44f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.67f), new Vector2(0.5f, 0.67f), new Vector2(1200f, 80f));

            CreateButton(menuPanel, "开始游戏", ShowRooms, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), PrimaryMenuButtonSize, Vector2.zero);
            CreateButton(menuPanel, "在线玩", ShowOnline, new Vector2(0.5f, 0.41f), new Vector2(0.5f, 0.41f), PrimaryMenuButtonSize, Vector2.zero);
            CreateButton(menuPanel, "游戏选项", ShowOptions, new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f), PrimaryMenuButtonSize, Vector2.zero);
        }

        private void BuildOptionsPanel()
        {
            CreateText("OptionsTitle", optionsPanel, "游戏选项", 76, FontStyle.Bold, new Color(0.71f, 0.25f, 0.41f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.83f), new Vector2(0.5f, 0.83f), new Vector2(800f, 100f));
            optionsSummaryText = CreateText("OptionsSummary", optionsPanel, string.Empty, 32, FontStyle.Normal, new Color(0.54f, 0.35f, 0.44f), TextAnchor.UpperLeft, new Vector2(0.5f, 0.64f), new Vector2(0.5f, 0.64f), new Vector2(880f, 300f));

            ruleToggleButton = CreateButton(optionsPanel, "默认规则：空跳", ToggleDefaultRule, new Vector2(0.5f, 0.48f), new Vector2(0.5f, 0.48f), OptionButtonSize, Vector2.zero);
            themeToggleButton = CreateButton(optionsPanel, "背景主题：粉色糖果", ToggleTheme, new Vector2(0.5f, 0.405f), new Vector2(0.5f, 0.405f), OptionButtonSize, Vector2.zero);
            soundToggleButton = CreateButton(optionsPanel, "音效：开", ToggleSound, new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.33f), OptionButtonSize, Vector2.zero);
            musicToggleButton = CreateButton(optionsPanel, "背景音乐：开", ToggleMusic, new Vector2(0.5f, 0.255f), new Vector2(0.5f, 0.255f), OptionButtonSize, Vector2.zero);
            promptToggleButton = CreateButton(optionsPanel, "催促：关", TogglePrompt, new Vector2(0.5f, 0.195f), new Vector2(0.5f, 0.195f), OptionHalfButtonSize, new Vector2(-160f, 0f));
            promptIntervalButton = CreateButton(optionsPanel, "催促间隔：30 秒", CyclePromptInterval, new Vector2(0.5f, 0.195f), new Vector2(0.5f, 0.195f), OptionHalfButtonSize, new Vector2(160f, 0f));
            musicImportStatusText = CreateText("MusicImportStatus", optionsPanel, "当前使用内置背景音乐。", 26, FontStyle.Normal, new Color(0.54f, 0.35f, 0.44f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.15f), new Vector2(900f, 54f));
            musicPickButton = CreateButton(optionsPanel, "从文件选择MP3", ChooseMusicFile, new Vector2(0.5f, 0.095f), new Vector2(0.5f, 0.095f), OptionFileButtonSize, new Vector2(-210f, 0f), ButtonLabelLength.SixCharacters);
            musicResetButton = CreateButton(optionsPanel, "恢复默认音乐", ResetMusicToDefault, new Vector2(0.5f, 0.095f), new Vector2(0.5f, 0.095f), OptionFileButtonSize, new Vector2(210f, 0f), ButtonLabelLength.SixCharacters);
            CreateButton(optionsPanel, "返回主菜单", ShowMenu, new Vector2(0.5f, 0.035f), new Vector2(0.5f, 0.035f), FullWidthButtonSize, Vector2.zero, ButtonLabelLength.FourCharacters);
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

            CreateButton(roomsPanel, "新建房间", () => ShowRoomEditor(null), new Vector2(0.5f, 0.16f), new Vector2(0.5f, 0.16f), FullWidthButtonSize, Vector2.zero);
            CreateButton(roomsPanel, "返回主菜单", ShowMenu, new Vector2(0.5f, 0.075f), new Vector2(0.5f, 0.075f), FullWidthButtonSize, Vector2.zero);
        }

        private void BuildRoomEditPanel()
        {
            roomEditTitleText = CreateText("RoomEditTitle", roomEditPanel, "编辑房间", 70, FontStyle.Bold, new Color(0.7f, 0.25f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.9f), new Vector2(0.5f, 0.9f), new Vector2(900f, 90f));
            CreateText("RoomNameLabel", roomEditPanel, "房间名称", 30, FontStyle.Bold, new Color(0.48f, 0.28f, 0.37f), TextAnchor.MiddleLeft, new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.82f), new Vector2(900f, 50f));
            roomNameInput = CreateInputField(roomEditPanel, new Vector2(0.5f, 0.775f), new Vector2(900f, 72f));

            roomRuleToggleButton = CreateButton(roomEditPanel, "规则：空跳", ToggleRoomRule, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), RoomEditLargeButtonSize, new Vector2(-235f, 0f));
            roomThemeToggleButton = CreateButton(roomEditPanel, "主题：粉色糖果", ToggleRoomTheme, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), RoomEditLargeButtonSize, new Vector2(235f, 0f));
            roomSoundToggleButton = CreateButton(roomEditPanel, "音效：开", ToggleRoomSound, new Vector2(0.5f, 0.635f), new Vector2(0.5f, 0.635f), RoomEditCompactButtonSize, new Vector2(-310f, 0f));
            roomMusicToggleButton = CreateButton(roomEditPanel, "音乐：开", ToggleRoomMusic, new Vector2(0.5f, 0.635f), new Vector2(0.5f, 0.635f), RoomEditCompactButtonSize, Vector2.zero);
            roomPromptToggleButton = CreateButton(roomEditPanel, "催促：关", ToggleRoomPrompt, new Vector2(0.5f, 0.635f), new Vector2(0.5f, 0.635f), RoomEditCompactButtonSize, new Vector2(310f, 0f));
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
                Button slotButton = CreateButton(roomEditPanel, BoardLayout.GetSlotLabel(slotId), () => CycleRoomSlot(slotId), new Vector2(0.5f, startY - row * rowGap), new Vector2(0.5f, startY - row * rowGap), RoomEditLargeButtonSize, offset);
                roomSlotButtons[slotId] = slotButton;
            }

            roomEditValidationText = CreateText("RoomEditValidation", roomEditPanel, string.Empty, 28, FontStyle.Normal, new Color(0.7f, 0.22f, 0.35f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), new Vector2(980f, 70f));
            CreateButton(roomEditPanel, "保存房间", SaveEditedRoom, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f), RoomEditActionButtonSize, new Vector2(-235f, 0f));
            CreateButton(roomEditPanel, "取消", ShowRooms, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f), RoomEditActionButtonSize, new Vector2(235f, 0f));
        }

        private void BuildOnlinePanel()
        {
            onlineAiSlotButtons.Clear();
            onlineSlotButtons.Clear();
            onlineSlotNodeImages.Clear();
            onlineSlotNodeRings.Clear();
            onlineSlotNodeLabels.Clear();
            onlineSlotNodeNames.Clear();
            onlineSlotNodeBadges.Clear();

            CreateText("OnlineTitle", onlinePanel, "在线房间", 66, FontStyle.Bold, new Color(0.7f, 0.25f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.91f), new Vector2(0.5f, 0.91f), new Vector2(900f, 88f));

            GameObject scrollObject = new GameObject("OnlineScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollObject.transform.SetParent(onlinePanel, false);
            RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0.08f, 0.11f);
            scrollRectTransform.anchorMax = new Vector2(0.92f, 0.84f);
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = Vector2.zero;
            scrollObject.GetComponent<Image>().color = new Color(1f, 0.98f, 0.99f, 0.42f);

            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportObject.transform.SetParent(scrollObject.transform, false);
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(18f, 18f);
            viewportRect.offsetMax = new Vector2(-18f, -18f);
            viewportObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("OnlineContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewportObject.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 14, 14);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            TMP_Text serverText = CreateText("OnlineServer", content.transform, "服务器：jump.mddxz.top", 28, FontStyle.Normal, new Color(0.55f, 0.36f, 0.43f), TextAnchor.MiddleCenter, size: new Vector2(900f, 46f));
            SetLayoutElement(serverText, 46f);

            onlineStatusText = CreateText("OnlineStatus", content.transform, string.Empty, 27, FontStyle.Normal, new Color(0.45f, 0.28f, 0.36f), TextAnchor.MiddleCenter, size: new Vector2(900f, 108f));
            SetLayoutElement(onlineStatusText, 108f);

            onlineLoginSection = CreateLayoutSection(content.transform, "OnlineLoginSection");
            onlineLobbySection = CreateLayoutSection(content.transform, "OnlineLobbySection");

            onlineAccountInput = CreateInputField(onlineLoginSection, new Vector2(0.5f, 0.5f), new Vector2(900f, 62f));
            onlineAccountInput.text = saveData.Options.OnlinePlayerAccount ?? string.Empty;
            onlineAccountInput.characterLimit = 32;
            TMP_Text accountPlaceholder = onlineAccountInput.placeholder as TMP_Text;
            if (accountPlaceholder != null)
            {
                accountPlaceholder.text = "账号（首次注册自动创建）";
            }
            onlineAccountInput.onEndEdit.AddListener(_ => ApplyOnlineAccountPassword());
            SetLayoutElement(onlineAccountInput, 62f);

            onlinePasswordInput = CreateInputField(onlineLoginSection, new Vector2(0.5f, 0.5f), new Vector2(900f, 62f));
            onlinePasswordInput.text = saveData.Options.OnlinePlayerPassword ?? string.Empty;
            onlinePasswordInput.contentType = TMP_InputField.ContentType.Password;
            onlinePasswordInput.characterLimit = 64;
            TMP_Text passwordPlaceholder = onlinePasswordInput.placeholder as TMP_Text;
            if (passwordPlaceholder != null)
            {
                passwordPlaceholder.text = "密码";
            }
            onlinePasswordInput.onEndEdit.AddListener(_ => ApplyOnlineAccountPassword());
            SetLayoutElement(onlinePasswordInput, 62f);

            onlinePlayerNameInput = CreateInputField(onlineLoginSection, new Vector2(0.5f, 0.5f), new Vector2(900f, 62f));
            onlinePlayerNameInput.text = GetOnlinePlayerName();
            onlinePlayerNameInput.characterLimit = 14;
            TMP_Text namePlaceholder = onlinePlayerNameInput.placeholder as TMP_Text;
            if (namePlaceholder != null)
            {
                namePlaceholder.text = "你的昵称";
            }
            onlinePlayerNameInput.onEndEdit.AddListener(_ => ApplyOnlinePlayerName());
            SetLayoutElement(onlinePlayerNameInput, 62f);

            onlineLoginButton = CreateLayoutButton(onlineLoginSection, "登录进入大厅", LoginOnline, ButtonLabelLength.SixCharacters);

            onlineDualDeviceButton = CreateLayoutButton(onlineLobbySection, saveData.Options.OnlineDualDevice ? "本设备：双人共用" : "本设备：单人", ToggleOnlineDualDeviceMode, ButtonLabelLength.SixCharacters);

            onlineRoomKeyText = CreateText("OnlineRoomKey", onlineLobbySection, "房间密钥：未创建", 36, FontStyle.Bold, new Color(0.48f, 0.28f, 0.37f), TextAnchor.MiddleCenter, size: new Vector2(900f, 60f));
            SetLayoutElement(onlineRoomKeyText, 60f);

            onlineSlotPickerText = CreateText("OnlineSlotPicker", onlineLobbySection, "选择位置", 30, FontStyle.Bold, new Color(0.48f, 0.28f, 0.37f), TextAnchor.MiddleCenter, size: new Vector2(900f, 44f));
            SetLayoutElement(onlineSlotPickerText, 44f);

            CreateOnlineSlotDiagram(onlineLobbySection);

            onlineDiscoveryText = CreateText("OnlineDiscovery", onlineLobbySection, "发现房间", 30, FontStyle.Bold, new Color(0.48f, 0.28f, 0.37f), TextAnchor.MiddleCenter, size: new Vector2(900f, 44f));
            SetLayoutElement(onlineDiscoveryText, 44f);

            GameObject discoveryListObject = new GameObject("OnlineDiscoveryList", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
            discoveryListObject.transform.SetParent(onlineLobbySection, false);
            onlineDiscoveryListContainer = discoveryListObject.GetComponent<RectTransform>();
            VerticalLayoutGroup discoveryLayout = discoveryListObject.GetComponent<VerticalLayoutGroup>();
            discoveryLayout.spacing = 10f;
            discoveryLayout.childControlWidth = true;
            discoveryLayout.childControlHeight = true;
            discoveryLayout.childForceExpandWidth = true;
            discoveryLayout.childForceExpandHeight = false;
            discoveryListObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            LayoutElement discoveryElement = discoveryListObject.GetComponent<LayoutElement>();
            discoveryElement.preferredHeight = 146f;
            discoveryElement.minHeight = 92f;

            onlineRoomKeyInput = CreateInputField(onlineLobbySection, new Vector2(0.5f, 0.5f), new Vector2(900f, 66f));
            SetLayoutElement(onlineRoomKeyInput, 66f);
            onlineRoomKeyInput.characterLimit = 8;
            TMP_Text placeholder = onlineRoomKeyInput.placeholder as TMP_Text;
            if (placeholder != null)
            {
                placeholder.text = "输入房间密钥";
            }

            Transform rowOne = CreateLayoutRow(onlineLobbySection, "OnlineRowOne", 74f);
            CreateLayoutButton(rowOne, "创建房间", CreateOnlineRoom, ButtonLabelLength.FourCharacters);
            CreateLayoutButton(rowOne, "加入房间", JoinOnlineRoom, ButtonLabelLength.FourCharacters);

            Transform rowTwo = CreateLayoutRow(onlineLobbySection, "OnlineRowTwo", 74f);
            onlineStartButton = CreateLayoutButton(rowTwo, "开始本局", StartOnlineGame, ButtonLabelLength.FourCharacters);
            CreateLayoutButton(rowTwo, "刷新房间", () => { if (onlineClient != null && onlineClient.IsConnected) onlineClient.Send(new OnlineMessage { type = "LIST" }); }, ButtonLabelLength.FourCharacters);

            Transform rowThree = CreateLayoutRow(onlineLobbySection, "OnlineRowThree", 74f);
            CreateLayoutButton(rowThree, "退出房间", () =>
            {
                if (onlineClient != null && onlineClient.IsConnected && !string.IsNullOrEmpty(onlineRoomKey))
                    onlineClient.Send(new OnlineMessage { type = "LEAVE_ROOM" });
            }, ButtonLabelLength.FourCharacters);
            CreateLayoutButton(rowThree, "退出登录", LogoutOnline, ButtonLabelLength.FourCharacters);

            CreateButton(onlinePanel, "返回主菜单", () => { DisconnectOnline(); ShowMenu(); }, new Vector2(0.5f, 0.055f), new Vector2(0.5f, 0.055f), FullWidthButtonSize, Vector2.zero);

            onlineRestartVoteModal = CreateOnlineRestartVoteModal(onlinePanel);
            onlineJoinRoomConfirmModal = CreateOnlineJoinRoomConfirmModal(onlinePanel);
            onlineJoinRequestModal = CreateOnlineJoinRequestModal(onlinePanel);
            onlineLoginConflictModal = CreateOnlineLoginConflictModal(onlinePanel);
            RefreshOnlineLobby("请先登录，然后进入在线大厅。");
        }

        private void CreateOnlineSlotDiagram(Transform parent)
        {
            GameObject diagramObject = new GameObject("OnlineSlotDiagram", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            diagramObject.transform.SetParent(parent, false);
            RectTransform diagramRect = diagramObject.GetComponent<RectTransform>();
            diagramRect.sizeDelta = new Vector2(900f, 380f);
            Image diagramImage = diagramObject.GetComponent<Image>();
            diagramImage.color = new Color(1f, 1f, 1f, 0.36f);
            diagramImage.raycastTarget = false;
            SetLayoutElement(diagramRect, 380f);

            Dictionary<SlotId, Vector2> positions = GetOnlineSlotDiagramPositions();
            SlotId[] slots = BoardLayout.GetSlotsInDisplayOrder();
            for (int i = 0; i < slots.Length; i++)
            {
                CreateOnlineSlotDiagramLine(diagramObject.transform, positions[slots[i]], positions[slots[(i + 1) % slots.Length]], 2f, new Color(0.58f, 0.64f, 0.68f, 0.24f));
            }

            CreateOnlineSlotDiagramLine(diagramObject.transform, positions[SlotId.Top], positions[SlotId.Bottom], 2f, new Color(0.58f, 0.64f, 0.68f, 0.2f));
            CreateOnlineSlotDiagramLine(diagramObject.transform, positions[SlotId.TopRight], positions[SlotId.BottomLeft], 2f, new Color(0.58f, 0.64f, 0.68f, 0.2f));
            CreateOnlineSlotDiagramLine(diagramObject.transform, positions[SlotId.BottomRight], positions[SlotId.TopLeft], 2f, new Color(0.58f, 0.64f, 0.68f, 0.2f));

            GameObject center = new GameObject("OnlineSlotDiagramCenter", typeof(RectTransform), typeof(Image));
            center.transform.SetParent(diagramObject.transform, false);
            RectTransform centerRect = center.GetComponent<RectTransform>();
            centerRect.anchorMin = new Vector2(0.5f, 0.5f);
            centerRect.anchorMax = new Vector2(0.5f, 0.5f);
            centerRect.sizeDelta = new Vector2(142f, 142f);
            centerRect.anchoredPosition = Vector2.zero;
            Image centerImage = center.GetComponent<Image>();
            centerImage.sprite = GenerateHexCellSprite();
            centerImage.color = new Color(BoardCellColor.r, BoardCellColor.g, BoardCellColor.b, 0.42f);
            centerImage.raycastTarget = false;

            for (int i = 0; i < slots.Length; i++)
            {
                CreateOnlineSlotNode(diagramObject.transform, slots[i], positions[slots[i]]);
            }
        }

        private static Dictionary<SlotId, Vector2> GetOnlineSlotDiagramPositions()
        {
            float radius = 120f;
            Dictionary<SlotId, Vector2> positions = new Dictionary<SlotId, Vector2>();
            positions[SlotId.Top] = GetOnlineSlotDiagramPosition(-90f, radius);
            positions[SlotId.TopRight] = GetOnlineSlotDiagramPosition(-30f, radius);
            positions[SlotId.BottomRight] = GetOnlineSlotDiagramPosition(30f, radius);
            positions[SlotId.Bottom] = GetOnlineSlotDiagramPosition(90f, radius);
            positions[SlotId.BottomLeft] = GetOnlineSlotDiagramPosition(150f, radius);
            positions[SlotId.TopLeft] = GetOnlineSlotDiagramPosition(210f, radius);
            return positions;
        }

        private static Vector2 GetOnlineSlotDiagramPosition(float degrees, float radius)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians) * radius, -Mathf.Sin(radians) * radius);
        }

        private void CreateOnlineSlotDiagramLine(Transform parent, Vector2 start, Vector2 end, float thickness, Color color)
        {
            GameObject lineObject = new GameObject("OnlineSlotDiagramLine", typeof(RectTransform), typeof(Image));
            lineObject.transform.SetParent(parent, false);
            RectTransform rect = lineObject.GetComponent<RectTransform>();
            Vector2 delta = end - start;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(delta.magnitude, thickness);
            rect.anchoredPosition = start + delta * 0.5f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            Image image = lineObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private void CreateOnlineSlotNode(Transform parent, SlotId slotId, Vector2 position)
        {
            const float nodeSize = 106f;
            GameObject nodeObject = new GameObject("OnlineSlotNode_" + slotId, typeof(RectTransform), typeof(Image), typeof(Button));
            nodeObject.transform.SetParent(parent, false);
            RectTransform rect = nodeObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(nodeSize, nodeSize);
            rect.anchoredPosition = position;

            Image nodeImage = nodeObject.GetComponent<Image>();
            nodeImage.sprite = GeneratePieceSprite();
            nodeImage.color = SoftenPieceColor(BoardLayout.GetPieceColor(slotId));

            Button button = nodeObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                PlaySfx("button");
                SelectOnlineSlot(slotId);
            });
            onlineSlotButtons[slotId] = button;
            onlineSlotNodeImages[slotId] = nodeImage;

            GameObject ringObject = CreateCircleImage("Ring", nodeObject.transform, nodeSize + 14f, Color.white);
            Image ringImage = ringObject.GetComponent<Image>();
            ringImage.sprite = GenerateRingSprite();
            ringImage.raycastTarget = false;
            onlineSlotNodeRings[slotId] = ringImage;

            TMP_Text slotLabel = CreateText("SlotLabel", nodeObject.transform, BoardLayout.GetSlotLabel(slotId), 24, FontStyle.Bold, new Color(0.32f, 0.24f, 0.3f), TextAnchor.MiddleCenter, size: new Vector2(96f, 34f));
            slotLabel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            slotLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            slotLabel.rectTransform.anchoredPosition = new Vector2(0f, 16f);
            onlineSlotNodeLabels[slotId] = slotLabel;

            TMP_Text nameLabel = CreateText("SeatLabel", nodeObject.transform, "空位", 20, FontStyle.Normal, new Color(0.24f, 0.22f, 0.26f, 0.72f), TextAnchor.MiddleCenter, size: new Vector2(104f, 30f));
            nameLabel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            nameLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            nameLabel.rectTransform.anchoredPosition = new Vector2(0f, -18f);
            nameLabel.enableWordWrapping = false;
            onlineSlotNodeNames[slotId] = nameLabel;

            TMP_Text badge = CreateText("HostBadge", nodeObject.transform, "★", 22, FontStyle.Bold, new Color(0.72f, 0.2f, 0.28f, 0.94f), TextAnchor.MiddleCenter, size: new Vector2(32f, 32f));
            badge.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            badge.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            badge.rectTransform.anchoredPosition = new Vector2(0f, 54f);
            badge.gameObject.SetActive(false);
            onlineSlotNodeBadges[slotId] = badge;
        }

        private GameObject CreateOnlineRestartVoteModal(Transform parent)
        {
            GameObject modal = new GameObject("OnlineRestartVoteModal", typeof(RectTransform), typeof(Image));
            modal.transform.SetParent(parent, false);
            RectTransform rect = modal.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            modal.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.42f);

            GameObject card = new GameObject("RestartVoteCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(modal.transform, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(760f, 360f);
            cardRect.anchoredPosition = Vector2.zero;
            card.GetComponent<Image>().color = new Color(1f, 0.98f, 0.99f, 0.98f);

            CreateText("RestartVoteTitle", card.transform, "重开一局", 50, FontStyle.Bold, new Color(0.55f, 0.22f, 0.34f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.73f), new Vector2(0.5f, 0.73f), new Vector2(660f, 72f));
            onlineRestartVoteText = CreateText("RestartVoteBody", card.transform, "是否同意重开？", 31, FontStyle.Normal, new Color(0.48f, 0.32f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(660f, 60f));
            CreateButton(card.transform, "同意", ApproveOnlineRestart, new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f), new Vector2(240f, 74f), new Vector2(-140f, 0f), ButtonLabelLength.TwoCharacters);
            CreateButton(card.transform, "拒绝", RejectOnlineRestart, new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f), new Vector2(240f, 74f), new Vector2(140f, 0f), ButtonLabelLength.TwoCharacters);

            modal.SetActive(false);
            return modal;
        }

        private GameObject CreateOnlineJoinRoomConfirmModal(Transform parent)
        {
            GameObject modal = new GameObject("OnlineJoinRoomConfirmModal", typeof(RectTransform), typeof(Image));
            modal.transform.SetParent(parent, false);
            RectTransform rect = modal.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            modal.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.38f);

            GameObject card = new GameObject("JoinRoomConfirmCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(modal.transform, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(760f, 340f);
            cardRect.anchoredPosition = Vector2.zero;
            card.GetComponent<Image>().color = new Color(1f, 0.98f, 0.99f, 0.98f);

            CreateText("JoinRoomConfirmTitle", card.transform, "申请加入", 50, FontStyle.Bold, new Color(0.55f, 0.22f, 0.34f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.73f), new Vector2(0.5f, 0.73f), new Vector2(660f, 72f));
            onlineJoinRoomConfirmText = CreateText("JoinRoomConfirmBody", card.transform, "申请加入房间？", 31, FontStyle.Normal, new Color(0.48f, 0.32f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(660f, 60f));
            CreateButton(card.transform, "申请", ConfirmOnlineJoinRoom, new Vector2(0.5f, 0.27f), new Vector2(0.5f, 0.27f), new Vector2(240f, 74f), new Vector2(-140f, 0f), ButtonLabelLength.TwoCharacters);
            CreateButton(card.transform, "取消", HideOnlineJoinRoomConfirm, new Vector2(0.5f, 0.27f), new Vector2(0.5f, 0.27f), new Vector2(240f, 74f), new Vector2(140f, 0f), ButtonLabelLength.TwoCharacters);

            modal.SetActive(false);
            return modal;
        }

        private GameObject CreateOnlineJoinRequestModal(Transform parent)
        {
            GameObject modal = new GameObject("OnlineJoinRequestModal", typeof(RectTransform), typeof(Image));
            modal.transform.SetParent(parent, false);
            RectTransform rect = modal.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            modal.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.42f);

            GameObject card = new GameObject("JoinRequestCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(modal.transform, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(760f, 360f);
            cardRect.anchoredPosition = Vector2.zero;
            card.GetComponent<Image>().color = new Color(1f, 0.98f, 0.99f, 0.98f);

            CreateText("JoinRequestTitle", card.transform, "加入申请", 50, FontStyle.Bold, new Color(0.55f, 0.22f, 0.34f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.73f), new Vector2(0.5f, 0.73f), new Vector2(660f, 72f));
            onlineJoinRequestText = CreateText("JoinRequestBody", card.transform, "一位玩家想加入房间。", 31, FontStyle.Normal, new Color(0.48f, 0.32f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(660f, 60f));
            CreateButton(card.transform, "同意", ApprovePendingOnlineJoin, new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f), new Vector2(240f, 74f), new Vector2(-140f, 0f), ButtonLabelLength.TwoCharacters);
            CreateButton(card.transform, "拒绝", RejectPendingOnlineJoin, new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f), new Vector2(240f, 74f), new Vector2(140f, 0f), ButtonLabelLength.TwoCharacters);

            modal.SetActive(false);
            return modal;
        }

        private GameObject CreateOnlineLoginConflictModal(Transform parent)
        {
            GameObject modal = new GameObject("OnlineLoginConflictModal", typeof(RectTransform), typeof(Image));
            modal.transform.SetParent(parent, false);
            RectTransform rect = modal.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            modal.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.42f);

            GameObject card = new GameObject("LoginConflictCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(modal.transform, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(760f, 360f);
            cardRect.anchoredPosition = Vector2.zero;
            card.GetComponent<Image>().color = new Color(1f, 0.98f, 0.99f, 0.98f);

            CreateText("LoginConflictTitle", card.transform, "账号占用", 50, FontStyle.Bold, new Color(0.55f, 0.22f, 0.34f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.73f), new Vector2(0.5f, 0.73f), new Vector2(660f, 72f));
            onlineLoginConflictText = CreateText("LoginConflictBody", card.transform, "这个账号已经在其他地方登录。是否踢掉原来的登录？", 30, FontStyle.Normal, new Color(0.48f, 0.32f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(660f, 78f));
            CreateButton(card.transform, "踢掉原登录", ConfirmOnlineLoginTakeover, new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f), new Vector2(280f, 74f), new Vector2(-155f, 0f), ButtonLabelLength.SixCharacters);
            CreateButton(card.transform, "取消", CancelOnlineLoginConflict, new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f), new Vector2(240f, 74f), new Vector2(155f, 0f), ButtonLabelLength.TwoCharacters);

            modal.SetActive(false);
            return modal;
        }

        private GameObject CreateOnlineAiSettingsModal(Transform parent)
        {
            GameObject modal = new GameObject("OnlineAiSettingsModal", typeof(RectTransform), typeof(Image));
            modal.transform.SetParent(parent, false);
            RectTransform rect = modal.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            modal.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.38f);

            GameObject card = new GameObject("AiSettingsCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(modal.transform, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(820f, 560f);
            cardRect.anchoredPosition = Vector2.zero;
            card.GetComponent<Image>().color = new Color(1f, 0.98f, 0.99f, 0.98f);

            CreateText("AiSettingsTitle", card.transform, "人机设置", 50, FontStyle.Bold, new Color(0.55f, 0.22f, 0.34f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.86f), new Vector2(0.5f, 0.86f), new Vector2(720f, 70f));
            SlotId[] slots = BoardLayout.GetSlotsInDisplayOrder();
            for (int i = 0; i < slots.Length; i += 2)
            {
                float y = 0.66f - (i / 2) * 0.15f;
                SlotId left = slots[i];
                onlineAiSlotButtons[left] = CreateButton(card.transform, BoardLayout.GetSlotLabel(left), () => ToggleOnlineAiSlot(left), new Vector2(0.5f, y), new Vector2(0.5f, y), new Vector2(330f, 68f), new Vector2(-180f, 0f), ButtonLabelLength.SixCharacters);
                if (i + 1 < slots.Length)
                {
                    SlotId right = slots[i + 1];
                    onlineAiSlotButtons[right] = CreateButton(card.transform, BoardLayout.GetSlotLabel(right), () => ToggleOnlineAiSlot(right), new Vector2(0.5f, y), new Vector2(0.5f, y), new Vector2(330f, 68f), new Vector2(180f, 0f), ButtonLabelLength.SixCharacters);
                }
            }

            CreateButton(card.transform, "完成", HideOnlineAiSettings, new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.12f), new Vector2(420f, 76f), Vector2.zero, ButtonLabelLength.TwoCharacters);

            modal.SetActive(false);
            return modal;
        }

        private Transform CreateLayoutRow(Transform parent, string name, float height)
        {
            GameObject rowObject = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowObject.transform.SetParent(parent, false);
            HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            LayoutElement layoutElement = rowObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = height;
            layoutElement.minHeight = height;
            return rowObject.transform;
        }

        private RectTransform CreateLayoutSection(Transform parent, string name)
        {
            GameObject sectionObject = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
            sectionObject.transform.SetParent(parent, false);
            RectTransform rect = sectionObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = sectionObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            sectionObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            LayoutElement layoutElement = sectionObject.GetComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            return rect;
        }

        private Button CreateLayoutButton(Transform parent, string label, Action onClick, ButtonLabelLength labelLength)
        {
            Button button = CreateButton(parent, label, onClick, Vector2.zero, Vector2.one, new Vector2(0f, 74f), Vector2.zero, labelLength);
            LayoutElement layoutElement = button.gameObject.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.preferredHeight = 74f;
            layoutElement.minHeight = 74f;
            return button;
        }

        private static void SetLayoutElement(Component component, float height)
        {
            if (component == null)
            {
                return;
            }

            LayoutElement layoutElement = component.gameObject.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = component.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.preferredHeight = height;
            layoutElement.minHeight = height;
        }

        private void BuildGamePanel()
        {
            gameChromeButtons.Clear();

            roomTitleText = CreateText("RoomTitle", gamePanel, "默认房间", 34, FontStyle.Bold, new Color(1f, 1f, 1f, 0.72f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.968f), new Vector2(0.5f, 0.968f), new Vector2(1000f, 58f));

            GameObject boardObject = new GameObject("BoardContainer", typeof(RectTransform), typeof(Image));
            boardContainer = boardObject.GetComponent<RectTransform>();
            boardContainer.SetParent(gamePanel, false);
            // 棋盘容器占屏幕的区域：底部留给状态文字和按钮，顶部留少量房间标题空间。
            boardContainer.anchorMin = new Vector2(0f, 0.14f);
            boardContainer.anchorMax = new Vector2(1f, 0.972f);
            boardContainer.offsetMin = Vector2.zero;
            boardContainer.offsetMax = Vector2.zero;
            Image boardImage = boardObject.GetComponent<Image>();
            // 棋盘容器自身透明，真正的背景由 GamePanel 的雪山图负责。
            boardImage.color = Color.clear;
            boardImage.raycastTarget = false;

            GameObject barObject = new GameObject("BottomControlBar", typeof(RectTransform), typeof(Image));
            barObject.transform.SetParent(gamePanel, false);
            bottomControlBar = barObject.GetComponent<RectTransform>();
            bottomControlBar.anchorMin = new Vector2(0f, 0f);
            bottomControlBar.anchorMax = new Vector2(1f, 0f);
            bottomControlBar.pivot = new Vector2(0.5f, 0f);
            bottomControlBar.sizeDelta = new Vector2(0f, 300f);
            bottomControlBar.anchoredPosition = Vector2.zero;
            // 首次创建时的底部栏颜色；运行中 RefreshGameChromeStyle 会继续统一刷新。
            barObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.47f);

            // 当前回合提示不显示文字，只显示当前玩家阵营的一颗棋子。
            GameObject currentPieceObject = CreateCircleImage("CurrentPlayerPiece", bottomControlBar, 58f, Color.white);
            RectTransform currentPieceRect = currentPieceObject.GetComponent<RectTransform>();
            currentPieceRect.anchorMin = new Vector2(0.5f, 0.69f);
            currentPieceRect.anchorMax = new Vector2(0.5f, 0.69f);
            currentPlayerPieceImage = currentPieceObject.GetComponent<Image>();
            currentPlayerPieceImage.sprite = GeneratePieceSprite();

            // 底部栏状态文本颜色。
            statusText = CreateText("Status", bottomControlBar, "请选择一个棋子。", 30, FontStyle.Normal, new Color(1f, 1f, 1f, 0.92f), TextAnchor.MiddleCenter, new Vector2(0.08f, 0.46f), new Vector2(0.92f, 0.46f), new Vector2(0f, 92f));
            statusText.fontSizeMin = 16;

            undoButton = CreateButton(bottomControlBar, "悔棋", HandleUndo, new Vector2(0.34f, 0.21f), new Vector2(0.34f, 0.21f), GameActionButtonSize, Vector2.zero, ButtonLabelLength.TwoCharacters);
            finishTurnButton = CreateButton(bottomControlBar, "完成移动", HandleFinishTurn, new Vector2(0.66f, 0.21f), new Vector2(0.66f, 0.21f), GameActionButtonSize, Vector2.zero, ButtonLabelLength.FourCharacters);
            passTurnButton = CreateButton(bottomControlBar, "放弃移动", HandlePassTurn, new Vector2(0.66f, 0.21f), new Vector2(0.66f, 0.21f), GameActionButtonSize, Vector2.zero, ButtonLabelLength.FourCharacters);
            onlineHostSettingsButton = CreateButton(gamePanel, "设置", ShowOnlineHostSettings, Vector2.one, Vector2.one, new Vector2(132f, 56f), new Vector2(-232f, -58f), ButtonLabelLength.TwoCharacters);
            Button exitGameButton = CreateButton(gamePanel, "退出", HandleExitCurrentGame, Vector2.one, Vector2.one, new Vector2(132f, 56f), new Vector2(-86f, -58f), ButtonLabelLength.TwoCharacters);
            gameChromeButtons.Add(undoButton);
            gameChromeButtons.Add(finishTurnButton);
            gameChromeButtons.Add(passTurnButton);
            gameChromeButtons.Add(onlineHostSettingsButton);
            gameChromeButtons.Add(exitGameButton);

            victoryModal = CreateVictoryModal(gamePanel);
            exitConfirmModal = CreateExitConfirmModal(gamePanel);
            onlineHostSettingsModal = CreateOnlineHostSettingsModal(gamePanel);
            onlineAutoFinishModal = CreateOnlineAutoFinishModal(gamePanel);
            if (onlineHostSettingsButton != null)
            {
                onlineHostSettingsButton.gameObject.SetActive(false);
            }
            RefreshGameChromeStyle();
        }

        private GameObject CreateOnlineAutoFinishModal(Transform parent)
        {
            GameObject modal = new GameObject("OnlineAutoFinishModal", typeof(RectTransform), typeof(Image));
            modal.transform.SetParent(parent, false);
            RectTransform rect = modal.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            modal.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.38f);

            GameObject card = new GameObject("OnlineAutoFinishCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(modal.transform, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(720f, 320f);
            cardRect.anchoredPosition = Vector2.zero;
            card.GetComponent<Image>().color = new Color(1f, 0.98f, 0.99f, 0.98f);

            CreateText("OnlineAutoFinishTitle", card.transform, "操作提醒", 48, FontStyle.Bold, new Color(0.55f, 0.22f, 0.34f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), new Vector2(640f, 72f));
            onlineAutoFinishModalText = CreateText("OnlineAutoFinishText", card.transform, "你已经完成移动，请点击“完成移动”。", 30, FontStyle.Normal, new Color(0.48f, 0.32f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), new Vector2(640f, 70f));
            CreateButton(card.transform, "立即完成", HandleFinishTurn, new Vector2(0.5f, 0.24f), new Vector2(0.5f, 0.24f), new Vector2(320f, 72f), Vector2.zero, ButtonLabelLength.FourCharacters);

            modal.SetActive(false);
            return modal;
        }

        private GameObject CreateOnlineHostSettingsModal(Transform parent)
        {
            GameObject modal = new GameObject("OnlineHostSettingsModal", typeof(RectTransform), typeof(Image));
            modal.transform.SetParent(parent, false);
            RectTransform rect = modal.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            modal.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.42f);

            GameObject card = new GameObject("OnlineHostSettingsCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(modal.transform, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(720f, 440f);
            cardRect.anchoredPosition = Vector2.zero;
            card.GetComponent<Image>().color = new Color(1f, 0.98f, 0.99f, 0.98f);

            CreateText("HostSettingsTitle", card.transform, "房间设置", 48, FontStyle.Bold, new Color(0.55f, 0.22f, 0.34f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), new Vector2(640f, 72f));
            CreateText("HostSettingsBody", card.transform, "房主可修改规则（开局前）、重开当前棋局，或解散房间。", 29, FontStyle.Normal, new Color(0.48f, 0.32f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(640f, 56f));
            onlineHostRuleButton = CreateButton(card.transform, "规则：空跳", ToggleOnlineRoomRuleFromSettings, new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f), new Vector2(520f, 64f), Vector2.zero, ButtonLabelLength.FourCharacters);
            CreateButton(card.transform, "重开本局", RestartOnlineGameFromSettings, new Vector2(0.5f, 0.31f), new Vector2(0.5f, 0.31f), new Vector2(520f, 64f), Vector2.zero, ButtonLabelLength.FourCharacters);
            CreateButton(card.transform, "解散房间", DisbandOnlineRoomFromSettings, new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.18f), new Vector2(520f, 64f), Vector2.zero, ButtonLabelLength.FourCharacters);
            CreateButton(card.transform, "取消", HideOnlineHostSettings, new Vector2(0.5f, 0.06f), new Vector2(0.5f, 0.06f), new Vector2(520f, 60f), Vector2.zero, ButtonLabelLength.TwoCharacters);

            modal.SetActive(false);
            return modal;
        }

        private GameObject CreateExitConfirmModal(Transform parent)
        {
            GameObject modal = new GameObject("ExitConfirmModal", typeof(RectTransform), typeof(Image));
            modal.transform.SetParent(parent, false);
            RectTransform rect = modal.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            modal.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.42f);

            GameObject card = new GameObject("ExitConfirmCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(modal.transform, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(720f, 360f);
            cardRect.anchoredPosition = Vector2.zero;
            card.GetComponent<Image>().color = new Color(1f, 0.98f, 0.99f, 0.98f);

            CreateText("ExitConfirmTitle", card.transform, "退出本局？", 48, FontStyle.Bold, new Color(0.55f, 0.22f, 0.34f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), new Vector2(640f, 72f));
            CreateText("ExitConfirmBody", card.transform, "当前棋局进度不会保留。", 30, FontStyle.Normal, new Color(0.48f, 0.32f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(640f, 56f));
            CreateButton(card.transform, "确认退出", ConfirmExitCurrentGame, new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f), new Vector2(260f, 72f), new Vector2(-150f, 0f), ButtonLabelLength.FourCharacters);
            CreateButton(card.transform, "继续本局", HideExitConfirm, new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f), new Vector2(260f, 72f), new Vector2(150f, 0f), ButtonLabelLength.FourCharacters);

            modal.SetActive(false);
            return modal;
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
            onlinePanel.gameObject.SetActive(panel == onlinePanel);
            gamePanel.gameObject.SetActive(panel == gamePanel);
            RefreshAllUiText();
            RefreshMusicState();
        }

        private TMP_Text CreateText(
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
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            Vector2 min = anchorMin ?? new Vector2(0.5f, 0.5f);
            Vector2 max = anchorMax ?? min;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.sizeDelta = size ?? new Vector2(400f, 80f);
            rect.anchoredPosition = Vector2.zero;

            TMP_Text label = textObject.GetComponent<TextMeshProUGUI>();
            label.font = defaultFont;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = ToTmpFontStyle(style);
            label.color = color;
            label.alignment = ToTmpAlignment(anchor);
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableAutoSizing = false;
            label.fontSizeMin = Mathf.Max(14, Mathf.RoundToInt(fontSize * 0.68f));
            label.fontSizeMax = fontSize;
            label.lineSpacing = 1f;
            RequestTextCharacters(label, text, fontSize);
            return label;
        }

        private TMP_InputField CreateInputField(Transform parent, Vector2 anchor, Vector2 size)
        {
            GameObject inputObject = new GameObject("RoomNameInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inputObject.transform.SetParent(parent, false);

            RectTransform rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            inputObject.GetComponent<Image>().color = new Color(1f, 0.99f, 1f, 0.98f);

            TMP_Text text = CreateText("Text", inputObject.transform, string.Empty, 34, FontStyle.Normal, new Color(0.45f, 0.25f, 0.34f), TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(24f, 0f);
            text.rectTransform.offsetMax = new Vector2(-24f, 0f);

            TMP_Text placeholder = CreateText("Placeholder", inputObject.transform, "请输入房间名", 32, FontStyle.Italic, new Color(0.72f, 0.56f, 0.62f), TextAnchor.MiddleLeft);
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = new Vector2(24f, 0f);
            placeholder.rectTransform.offsetMax = new Vector2(-24f, 0f);

            TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = 18;
            return input;
        }

        private Button CreateButton(Transform parent, string label, Action onClick, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 offset, ButtonLabelLength labelLength = ButtonLabelLength.Auto)
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

            TMP_Text buttonText = CreateText("Label", buttonObject.transform, label, 34, FontStyle.Bold, new Color(0.47f, 0.22f, 0.31f), TextAnchor.MiddleCenter);
            buttonText.rectTransform.anchorMin = Vector2.zero;
            buttonText.rectTransform.anchorMax = Vector2.one;
            buttonText.rectTransform.offsetMin = Vector2.zero;
            buttonText.rectTransform.offsetMax = Vector2.zero;

            ButtonLabelFitter fitter = buttonObject.AddComponent<ButtonLabelFitter>();
            fitter.Label = buttonText;
            fitter.Length = labelLength;
            fitter.ApplyNow();

            return button;
        }

        private static void ApplyButtonLabelStyle(TMP_Text label, string value, ButtonLabelLength length)
        {
            ButtonLabelProfile profile = GetButtonLabelProfile(length == ButtonLabelLength.Auto ? ResolveButtonLabelLength(value) : length);
            RectTransform rect = label.rectTransform;
            rect.offsetMin = new Vector2(profile.HorizontalPadding, profile.VerticalPadding);
            rect.offsetMax = new Vector2(-profile.HorizontalPadding, -profile.VerticalPadding);

            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableAutoSizing = false;
            label.fontSizeMin = profile.MinFontSize;
            label.fontSizeMax = profile.MaxFontSize;
            label.lineSpacing = 1f;
            label.fontSize = GetFittedButtonFontSize(label, value, profile);
            RequestTextCharacters(label, value, label.fontSize);
        }

        private static ButtonLabelLength ResolveButtonLabelLength(string value)
        {
            int count = CountDisplayCharacters(value);
            if (count <= 2)
            {
                return ButtonLabelLength.TwoCharacters;
            }

            if (count <= 4)
            {
                return ButtonLabelLength.FourCharacters;
            }

            return ButtonLabelLength.SixCharacters;
        }

        private static ButtonLabelProfile GetButtonLabelProfile(ButtonLabelLength length)
        {
            switch (length)
            {
                case ButtonLabelLength.TwoCharacters:
                    return new ButtonLabelProfile { MaxFontSize = 32, MinFontSize = 18, HorizontalPadding = 8f, VerticalPadding = 4f };
                case ButtonLabelLength.FourCharacters:
                    return new ButtonLabelProfile { MaxFontSize = 32, MinFontSize = 14, HorizontalPadding = 8f, VerticalPadding = 4f };
                case ButtonLabelLength.SixCharacters:
                    return new ButtonLabelProfile { MaxFontSize = 30, MinFontSize = 11, HorizontalPadding = 6f, VerticalPadding = 4f };
                default:
                    return new ButtonLabelProfile { MaxFontSize = 30, MinFontSize = 11, HorizontalPadding = 6f, VerticalPadding = 4f };
            }
        }

        private static int CountDisplayCharacters(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSeparator(c) || char.IsSymbol(c))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private static int GetFittedButtonFontSize(TMP_Text label, string value, ButtonLabelProfile profile)
        {
            float availableWidth = GetAvailableLabelWidth(label);
            float availableHeight = GetAvailableLabelHeight(label);
            if (availableWidth <= 1f || availableHeight <= 1f || string.IsNullOrEmpty(value))
            {
                return profile.MaxFontSize;
            }

            for (int fontSize = profile.MaxFontSize; fontSize >= profile.MinFontSize; fontSize--)
            {
                if (DoesButtonTextFit(label, value, fontSize, availableWidth, availableHeight))
                {
                    return fontSize;
                }
            }

            return profile.MinFontSize;
        }

        private static float GetAvailableLabelWidth(TMP_Text label)
        {
            RectTransform rect = label.rectTransform;
            float width = rect.rect.width;
            if (width > 1f)
            {
                return width;
            }

            RectTransform parentRect = rect.parent as RectTransform;
            if (parentRect != null)
            {
                width = parentRect.rect.width - rect.offsetMin.x + rect.offsetMax.x;
                if (width > 1f)
                {
                    return width;
                }

                width = parentRect.sizeDelta.x - rect.offsetMin.x + rect.offsetMax.x;
            }

            return Mathf.Max(0f, width);
        }

        private static float GetAvailableLabelHeight(TMP_Text label)
        {
            RectTransform rect = label.rectTransform;
            float height = rect.rect.height;
            if (height > 1f)
            {
                return height;
            }

            RectTransform parentRect = rect.parent as RectTransform;
            if (parentRect != null)
            {
                height = parentRect.rect.height - rect.offsetMin.y + rect.offsetMax.y;
                if (height > 1f)
                {
                    return height;
                }

                height = parentRect.sizeDelta.y - rect.offsetMin.y + rect.offsetMax.y;
            }

            return Mathf.Max(0f, height);
        }

        private static bool DoesButtonTextFit(TMP_Text label, string value, int fontSize, float availableWidth, float availableHeight)
        {
            label.fontSize = fontSize;
            RequestTextCharacters(label, value, fontSize);
            Vector2 preferred = label.GetPreferredValues(value, 10000f, availableHeight);
            return preferred.x <= availableWidth && preferred.y <= availableHeight;
        }

        private static void RequestTextCharacters(TMP_Text label, string value, float fontSize)
        {
            if (label == null || label.font == null || string.IsNullOrEmpty(value))
            {
                return;
            }

            RequestFontCharacters(label.font, value);
        }

        private static void RequestFontCharacters(TMP_FontAsset font, string value)
        {
            if (font == null || string.IsNullOrEmpty(value))
            {
                return;
            }

            font.TryAddCharacters(value, out string _);
        }

        private static void PrewarmDefaultFont(TMP_FontAsset font)
        {
            RequestFontCharacters(font, CommonUiCharacterSet + LargeUiCharacterSet);
        }

        private void RefreshAllUiText()
        {
            if (rootCanvas == null || defaultFont == null)
            {
                return;
            }

            refreshingUiText = true;
            try
            {
                PrewarmDefaultFont(defaultFont);
                TMP_Text[] texts = rootCanvas.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text text = texts[i];
                    if (text == null)
                    {
                        continue;
                    }

                    RequestTextCharacters(text, text.text, text.fontSize);
                    text.SetAllDirty();
                }

                ButtonLabelFitter[] fitters = rootCanvas.GetComponentsInChildren<ButtonLabelFitter>(true);
                for (int i = 0; i < fitters.Length; i++)
                {
                    fitters[i].ApplyNow();
                }
            }
            finally
            {
                refreshingUiText = false;
            }

            Canvas.ForceUpdateCanvases();
        }

        private static FontStyles ToTmpFontStyle(FontStyle style)
        {
            switch (style)
            {
                case FontStyle.Bold:
                    return FontStyles.Bold;
                case FontStyle.Italic:
                    return FontStyles.Italic;
                case FontStyle.BoldAndItalic:
                    return FontStyles.Bold | FontStyles.Italic;
                default:
                    return FontStyles.Normal;
            }
        }

        private static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter:
                    return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight:
                    return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft:
                    return TextAlignmentOptions.MidlineLeft;
                case TextAnchor.MiddleCenter:
                    return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight:
                    return TextAlignmentOptions.MidlineRight;
                case TextAnchor.LowerLeft:
                    return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter:
                    return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight:
                    return TextAlignmentOptions.BottomRight;
                default:
                    return TextAlignmentOptions.Center;
            }
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
            // 通用实心圆 sprite 的纹理半径；实际 UI 尺寸由 RectTransform 控制。
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
            // 六边形外边框的纹理半径。这里是贴图坐标半径，不是最终屏幕半径。
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
                    // 格子内部轻微冰面噪声，值越大纹理越明显。
                    float grain = Mathf.PerlinNoise(x * 0.052f + 1.7f, y * 0.052f + 4.9f) * 0.035f;
                    // 斜向浅色光照，避免格子太平。
                    float diagonalLight = Mathf.Clamp01((x * 0.62f + y * 0.38f) / 128f) * 0.025f;
                    // 六边形边框线：1.4 是线的中心离外边缘距离，除数 1.4 控制线宽。
                    float line = Mathf.Clamp01(1f - Mathf.Abs(edge - 1.4f) / 1.4f);
                    // 边框线颜色偏深，格子内部偏亮。
                    float shade = Mathf.Lerp(1.03f + grain + diagonalLight, 0.58f, line);
                    // 边框线比格子内部更不透明。
                    float alpha = Mathf.Lerp(0.66f, 0.96f, line);
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
            // 可走目标六边形高光用的外轮廓半径。
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

                    float edge = DistanceToPolygonEdge(point, vertices);
                    // 高光外边线：2.2 是线中心，除数 2.2 控制线宽和柔边。
                    float line = Mathf.Clamp01(1f - Mathf.Abs(edge - 2.2f) / 2.2f);
                    // 可走目标格子的白色边缘高光透明度。
                    float alpha = Mathf.Lerp(0.1f, 0.62f, line);
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
                    // 六边形内部小圆单线的纹理半径。改 38 会改变小圆大小。
                    float ring = Mathf.Abs(distance - 38f);
                    // 小圆单线粗细和柔边。数值越大线越粗。
                    float alpha = Mathf.Clamp01(1f - ring / 2.1f);
                    if (alpha <= 0f)
                    {
                        texture.SetPixel(x, y, clear);
                        continue;
                    }

                    // 小圆线的贴图灰度；最终显示色还会乘 BoardSlotRingColor/BoardTargetRingColor。
                    texture.SetPixel(x, y, new Color(0.86f, 0.86f, 0.86f, alpha));
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

            Texture2D texture = new Texture2D(192, 192, TextureFormat.ARGB32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2(95.5f, 95.5f);
            // 棋子自带的固定亮点位置。
            Vector2 highlight = new Vector2(72f, 120f);
            Color clear = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    Vector2 delta = point - center;
                    float radius = delta.magnitude;
                    // 棋子纹理内部实际圆半径。改 86 会影响棋子边缘大小。
                    if (radius > 86f)
                    {
                        texture.SetPixel(x, y, clear);
                        continue;
                    }

                    // 86 必须和上面的棋子实际半径保持一致，用来算球面光照。
                    float normalized = radius / 86f;
                    float height = Mathf.Sqrt(Mathf.Clamp01(1f - normalized * normalized));
                    Vector3 normal = new Vector3(delta.x / 86f, delta.y / 86f, height).normalized;
                    Vector3 lightDirection = new Vector3(-0.42f, 0.58f, 0.7f).normalized;
                    float lambert = Mathf.Clamp01(Vector3.Dot(normal, lightDirection));
                    float latitude = Mathf.Sin((normalized * 18f + Mathf.PerlinNoise(x * 0.035f, y * 0.035f) * 1.8f) * Mathf.PI);
                    float carve = Mathf.Sin(normalized * 62f + Mathf.PerlinNoise((x + 19f) * 0.06f, (y + 37f) * 0.06f) * 3f);
                    float grain = Mathf.PerlinNoise((x + 13f) * 0.085f, (y + 41f) * 0.085f);
                    float highlightStrength = Mathf.Clamp01(1f - Vector2.Distance(point, highlight) / 31f);
                    float rim = Mathf.SmoothStep(0.64f, 1f, normalized);
                    float shade = 0.76f + lambert * 0.24f + height * 0.08f + latitude * 0.022f + carve * 0.02f + grain * 0.045f;
                    shade += highlightStrength * 0.14f;
                    shade -= rim * 0.16f;
                    shade = Mathf.Clamp01(shade);
                    // 棋子边缘透明柔化宽度，3.5 越大边缘越软。
                    float alpha = Mathf.Clamp01((86f - radius) / 3.5f);
                    texture.SetPixel(x, y, new Color(shade, shade, shade, alpha));
                }
            }

            texture.Apply();
            cachedPieceSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            return cachedPieceSprite;
        }

        private Sprite GeneratePieceHighlightSprite()
        {
            if (cachedPieceHighlightSprite != null)
            {
                return cachedPieceHighlightSprite;
            }

            Texture2D texture = new Texture2D(192, 192, TextureFormat.ARGB32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2(95.5f, 95.5f);
            // 选中棋子局部高光中心位置。
            Vector2 shineCenter = new Vector2(72f, 118f);
            Color clear = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    float pieceRadius = Vector2.Distance(point, center);
                    // 选中高光只允许画在棋子内部，半径略小于棋子本体。
                    if (pieceRadius > 82f)
                    {
                        texture.SetPixel(x, y, clear);
                        continue;
                    }

                    Vector2 shineDelta = point - shineCenter;
                    // 高光椭圆半径：横向 34、纵向 25。改这里可以调亮斑大小。
                    float oval = Mathf.Sqrt((shineDelta.x * shineDelta.x) / (34f * 34f) + (shineDelta.y * shineDelta.y) / (25f * 25f));
                    float alpha = Mathf.Clamp01(1f - oval);
                    // 高光透明度曲线，1.8 越大中心越集中，0.9 是最大透明度。
                    alpha = Mathf.Pow(alpha, 1.8f) * 0.9f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            cachedPieceHighlightSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            return cachedPieceHighlightSprite;
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
                    // 雪山背景基础色：淡蓝到白色的渐变。
                    Color color = Color.Lerp(new Color(0.9f, 0.96f, 1f), Color.white, Mathf.Pow(ny, 0.72f));

                    float farPeak = MountainHeight(nx, 0.71f, 0.085f, 1.8f, 3.4f);
                    if (ny < farPeak)
                    {
                        float depth = Mathf.Clamp01((farPeak - ny) / 0.36f);
                        float snow = Mathf.PerlinNoise(nx * 9f + 2.1f, ny * 13f + 1.7f);
                        // 远山岩石色和雪色，整体最浅。
                        Color rock = Color.Lerp(new Color(0.78f, 0.86f, 0.92f), new Color(0.54f, 0.62f, 0.68f), depth * 0.7f);
                        Color ice = new Color(0.95f, 0.985f, 1f);
                        color = Color.Lerp(rock, ice, snow > 0.56f ? 0.56f : 0.24f);
                        color.a = 1f;
                    }

                    float midPeak = MountainHeight(nx + 0.18f, 0.55f, 0.11f, 2.4f, 4.7f);
                    if (ny < midPeak)
                    {
                        float depth = Mathf.Clamp01((midPeak - ny) / 0.34f);
                        float snow = Mathf.PerlinNoise(nx * 12f + 7.3f, ny * 17f + 9.2f);
                        // 中景山体颜色，比远山更深。
                        Color rock = Color.Lerp(new Color(0.7f, 0.78f, 0.84f), new Color(0.42f, 0.49f, 0.55f), depth * 0.82f);
                        Color ice = new Color(0.91f, 0.97f, 1f);
                        color = Color.Lerp(rock, ice, snow > 0.52f ? 0.62f : 0.2f);
                    }

                    float frontPeak = MountainHeight(nx + 0.43f, 0.26f, 0.08f, 3.2f, 5.5f);
                    if (ny < frontPeak)
                    {
                        float depth = Mathf.Clamp01((frontPeak - ny) / 0.28f);
                        float snow = Mathf.PerlinNoise(nx * 18f + 5.8f, ny * 20f + 4.1f);
                        // 前景山体颜色，最深，用来压住底部操作栏后的背景。
                        Color rock = Color.Lerp(new Color(0.64f, 0.72f, 0.78f), new Color(0.34f, 0.41f, 0.47f), depth * 0.85f);
                        Color ice = new Color(0.88f, 0.96f, 1f);
                        color = Color.Lerp(rock, ice, snow > 0.5f ? 0.52f : 0.18f);
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

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = value;
                ButtonLabelFitter fitter = button.GetComponent<ButtonLabelFitter>();
                if (fitter != null)
                {
                    fitter.ApplyNow();
                }
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
