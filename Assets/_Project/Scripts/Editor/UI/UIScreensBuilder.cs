using System.IO;
using TLA.UI.Components;
using TLA.UI.Dialogue;
using TLA.UI.HUD;
using TLA.UI.Menus;
using TLA.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static TLA.EditorTools.UI.UIComponentLibrary;
using static TLA.EditorTools.UI.UIKit;

namespace TLA.EditorTools.UI
{
    /// <summary>
    /// Builds the game's screens from the theme and the component prefabs:
    ///   UI_GameRoot prefab (lives in Bootstrap, persists across scenes):
    ///       HUD · Notifications · Dialogue · Pause menu + Notebook · Settings
    ///   Main menu canvas (inside the MainMenu scene): main panel + Case Files
    /// Menu: The Last Answer → UI → Rebuild UI.
    /// </summary>
    internal static class UIScreensBuilder
    {
        public const string ThemePath = "Assets/_Project/Data/Settings/UITheme.asset";
        public const string GameRootPath = "Assets/_Project/Prefabs/UI/UI_GameRoot.prefab";

        /// <summary>Creates the theme (if missing), the component prefabs and the UI_GameRoot prefab.</summary>
        public static GameObject BuildPrefabs()
        {
            Theme = LoadOrCreateTheme();
            UIComponentLibrary.BuildAll();
            return BuildGameRoot();
        }

        public static UITheme LoadOrCreateTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UITheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            if (theme.font == null)
            {
                theme.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
                EditorUtility.SetDirty(theme);
            }
            return theme;
        }

        // =====================================================================================
        // GAME ROOT (Bootstrap)
        // =====================================================================================

        private static GameObject BuildGameRoot()
        {
            Canvas canvas = ScreenCanvas("UI_GameRoot", null, 100);
            Transform root = canvas.transform;

            BuildHud(root);
            BuildDialogue(root);
            BuildPauseMenu(root);
            BuildSettings(root); // last = drawn on top

            Directory.CreateDirectory(Path.GetDirectoryName(GameRootPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, GameRootPath);
            Object.DestroyImmediate(canvas.gameObject);
            return prefab;
        }

        // ------------------------------------------------------------------ HUD

        private static void BuildHud(Transform parent)
        {
            RectTransform hud = Stretch(Rect("HUD", parent));
            hud.gameObject.AddComponent<CanvasGroup>();
            var controller = hud.gameObject.AddComponent<HudController>();

            // Top-left: case + objective.
            RectTransform caseBlock = Place(Rect("CaseBlock", hud), new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -44), new Vector2(760, 170));
            caseBlock.gameObject.AddComponent<CanvasGroup>();
            VLayout(caseBlock, 6);
            Label(caseBlock, "CURRENT CASE");
            TextMeshProUGUI caseLabel = Text(caseBlock, "CaseName", "CASE 0000 · TITLE", 21, Theme.textPrimary, TextAlignmentOptions.TopLeft, FontStyles.Normal, 3, false);
            Spacer(caseBlock, 8);
            RectTransform objHeader = Rect("ObjectiveHeader", caseBlock);
            HLayout(objHeader, 14);
            Label(objHeader, "OBJECTIVE");
            TextMeshProUGUI tag = Label(objHeader, "NEW", Theme.accent);
            tag.name = "NewTag";
            tag.alpha = 0f;
            TextMeshProUGUI objective = Text(caseBlock, "Objective", "Objective text", 20, Theme.textSecondary);
            Layout(objective, 620);

            // Top-right: evidence counter.
            RectTransform evidence = Place(Rect("EvidenceBlock", hud), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-56, -44), new Vector2(260, 80));
            evidence.gameObject.AddComponent<CanvasGroup>();
            VLayout(evidence, 2, null, TextAnchor.UpperRight);
            TextMeshProUGUI evLabel = Label(evidence, "EVIDENCE");
            evLabel.alignment = TextAlignmentOptions.TopRight;
            TextMeshProUGUI evCount = Text(evidence, "Count", "0", 34, Theme.textPrimary, TextAlignmentOptions.TopRight, FontStyles.Normal, 2, false);

            SetField(controller, "caseBlock", caseBlock.GetComponent<CanvasGroup>());
            SetField(controller, "caseLabel", caseLabel);
            SetField(controller, "objectiveLabel", objective);
            SetField(controller, "objectiveTag", tag);
            SetField(controller, "evidenceBlock", evidence.GetComponent<CanvasGroup>());
            SetField(controller, "evidenceCount", evCount);

            // Centre: crosshair dot + interaction prompt.
            RectTransform interaction = Stretch(Rect("Interaction", hud));
            var prompt = interaction.gameObject.AddComponent<InteractionPromptUI>();
            RectTransform dot = Place(Rect("Crosshair", interaction), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5, 5));
            Image dotImage = Image(dot, new Color(1, 1, 1, 0.35f));
            dotImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            RectTransform promptBox = Place(Rect("Prompt", interaction), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -42), new Vector2(10, 10));
            promptBox.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            Image(promptBox, new Color(Theme.panel.r, Theme.panel.g, Theme.panel.b, 0.85f));
            var promptLayout = HLayout(promptBox, 0, new RectOffset(18, 18, 9, 9), TextAnchor.MiddleCenter);
            promptBox.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            promptBox.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TextMeshProUGUI promptLabel = Text(promptBox, "PromptLabel", "[E] Interact", 18, Theme.textPrimary, TextAlignmentOptions.Center, FontStyles.Normal, 3, false);
            SetField(prompt, "promptGroup", promptBox.GetComponent<CanvasGroup>());
            SetField(prompt, "promptLabel", promptLabel);
            SetField(prompt, "crosshair", dotImage);

            // Bottom-left: key hints.
            RectTransform hints = Place(Rect("KeyHints", hud), new Vector2(0, 0), new Vector2(0, 0), new Vector2(56, 40), new Vector2(600, 24));
            Stretch(Text(hints, "Text", "[J]  NOTES      [ESC]  PAUSE", Theme.labelSize, Theme.textMuted, TextAlignmentOptions.BottomLeft, FontStyles.Normal, Theme.labelSpacing, false).rectTransform);

            // Top-right, under the counter: notifications.
            RectTransform feedRt = Place(Rect("Notifications", hud), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-56, -150), new Vector2(420, 400));
            VLayout(feedRt, 10, null, TextAnchor.UpperRight, true, false);
            GameObject toast = Spawn(UIComponentLibrary.Toast, feedRt, "ToastTemplate");
            var feed = feedRt.gameObject.AddComponent<NotificationFeed>();
            SetField(feed, "toastTemplate", toast.GetComponent<NotificationToast>());
            var notifications = feedRt.gameObject.AddComponent<GameNotifications>();
            SetField(notifications, "feed", feed);
        }

        // ------------------------------------------------------------------ DIALOGUE

        private static void BuildDialogue(Transform parent)
        {
            RectTransform screen = Stretch(Rect("Dialogue", parent));
            screen.gameObject.AddComponent<CanvasGroup>();
            var panel = screen.gameObject.AddComponent<DialoguePanel>();

            // Right-hand column; the left 62% shows the cinematic camera.
            RectTransform column = Region(Rect("Column", screen), 0.62f, 0f, 1f, 1f);
            Image(column, new Color(Theme.panel.r, Theme.panel.g, Theme.panel.b, 0.95f), true);
            RectTransform edge = Rect("Edge", column);
            edge.anchorMin = new Vector2(0, 0); edge.anchorMax = new Vector2(0, 1); edge.pivot = new Vector2(1, 0.5f); edge.sizeDelta = new Vector2(1, 0);
            Image(edge, Theme.divider);
            VLayout(column, 16, new RectOffset(40, 40, 36, 30));

            // Header row: label + End interview.
            RectTransform header = Rect("Header", column);
            HLayout(header, 10);
            TextMeshProUGUI headerLabel = Label(header, "INTERVIEW  ·  ROOM 1");
            Layout(headerLabel, -1, -1, 1);
            Button end = SpawnButton(ActionButton, header, "END INTERVIEW");
            Layout(end, 190, 40);

            // Profile: initials "portrait" + name, meta, status.
            RectTransform profile = Rect("Profile", column);
            HLayout(profile, 20, null, TextAnchor.UpperLeft);
            RectTransform portrait = Rect("Portrait", profile);
            Layout(portrait, 96, 96);
            Image(portrait, Theme.panelRaised);
            TextMeshProUGUI initials = Text(portrait, "Initials", "EM", 38, Theme.textSecondary, TextAlignmentOptions.Center, FontStyles.Normal, 2, false);
            Stretch(initials.rectTransform);
            RectTransform portraitLine = Rect("Underline", portrait);
            portraitLine.anchorMin = new Vector2(0, 0); portraitLine.anchorMax = new Vector2(1, 0); portraitLine.pivot = new Vector2(0.5f, 0); portraitLine.sizeDelta = new Vector2(0, 2);
            Image(portraitLine, Theme.accent);

            RectTransform info = Rect("Info", profile);
            VLayout(info, 4);
            Layout(info, -1, -1, 1);
            TextMeshProUGUI name = Text(info, "Name", "SUSPECT NAME", 30, Theme.textPrimary, TextAlignmentOptions.TopLeft, FontStyles.Normal, 3, false);
            TextMeshProUGUI meta = Text(info, "Meta", "51 · Architect", 18, Theme.textSecondary, TextAlignmentOptions.TopLeft, FontStyles.Normal, 0, false);
            TextMeshProUGUI status = Text(info, "Status", "PERSON OF INTEREST", Theme.labelSize, Theme.accent, TextAlignmentOptions.TopLeft, FontStyles.Normal, Theme.labelSpacing, false);

            TextMeshProUGUI summary = Text(column, "Summary", "Summary.", 17, Theme.textSecondary);
            summary.lineSpacing = 4f;
            Divider(column);

            // Conversation history (takes all remaining height).
            Label(column, "CONVERSATION");
            ScrollRect history = ScrollList(column, "History", 0, out RectTransform historyContent);
            Layout(history, -1, -1, 1, 1, 160);
            GameObject entry = Spawn(UIComponentLibrary.DialogueEntry, historyContent, "EntryTemplate");

            RectTransform thinking = Rect("Thinking", column);
            thinking.gameObject.AddComponent<CanvasGroup>();
            Layout(thinking, -1, 20);
            TextMeshProUGUI thinkingLabel = Text(thinking, "Label", "SUSPECT IS THINKING", Theme.labelSize, Theme.textMuted, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, Theme.labelSpacing, false);
            Stretch(thinkingLabel.rectTransform);

            // Suggested questions.
            Label(column, "SUGGESTED QUESTIONS");
            RectTransform suggestions = Rect("Suggestions", column);
            VLayout(suggestions, 6);
            GameObject suggestion = Spawn(ListButton, suggestions, "SuggestionTemplate");

            // Free-text input + Ask.
            RectTransform inputRow = Rect("InputRow", column);
            HLayout(inputRow, 10);
            TMP_InputField input = InputField(inputRow, "Type your own question…");
            Layout(input, -1, 52, 1);
            Button ask = SpawnButton(ActionButton, inputRow, "ASK");
            Layout(ask, 120, 52);

            // Error / validation line (fades in for a few seconds).
            TextMeshProUGUI error = Text(column, "Error", "", 15, Theme.accent, TextAlignmentOptions.TopLeft, FontStyles.Normal, 2, false);
            Layout(error, -1, 20);

            // Present evidence: a button that opens a short list of discovered evidence.
            Button present = SpawnButton(ActionButton, column, "PRESENT EVIDENCE");
            Layout(present, -1, 46);
            RectTransform picker = Rect("EvidencePicker", column);
            VLayout(picker, 6, new RectOffset(0, 0, 0, 4));
            Label(picker, "SHOW THE SUSPECT…");
            TextMeshProUGUI evidenceEmpty = Text(picker, "Empty", "No evidence discovered yet. Search the Evidence Room.", 16, Theme.textMuted, TextAlignmentOptions.TopLeft, FontStyles.Italic);
            RectTransform evidenceList = Rect("Items", picker);
            VLayout(evidenceList, 6);
            GameObject evidenceTemplate = Spawn(ListButton, evidenceList, "EvidenceTemplate");

            Text(column, "Hint", "[TAB]  CHANGE CAMERA      [ENTER]  ASK      [ESC]  PAUSE", 13, Theme.textMuted, TextAlignmentOptions.TopLeft, FontStyles.Normal, 4, false);

            SetField(panel, "portraitInitials", initials);
            SetField(panel, "suspectName", name);
            SetField(panel, "suspectMeta", meta);
            SetField(panel, "suspectStatus", status);
            SetField(panel, "suspectSummary", summary);
            SetField(panel, "historyScroll", history);
            SetField(panel, "entryTemplate", entry.GetComponent<DialogueEntryView>());
            SetField(panel, "historyContainer", historyContent);
            SetField(panel, "thinkingIndicator", thinking.GetComponent<CanvasGroup>());
            SetField(panel, "thinkingLabel", thinkingLabel);
            SetField(panel, "suggestionTemplate", suggestion.GetComponent<Button>());
            SetField(panel, "suggestionContainer", suggestions);
            SetField(panel, "questionInput", input);
            SetField(panel, "askButton", ask);
            SetField(panel, "endInterviewButton", end);
            SetField(panel, "errorLabel", error);
            SetField(panel, "presentEvidenceButton", present);
            SetField(panel, "evidencePicker", picker.gameObject);
            SetField(panel, "evidenceTemplate", evidenceTemplate.GetComponent<Button>());
            SetField(panel, "evidenceContainer", evidenceList);
            SetField(panel, "evidenceEmpty", evidenceEmpty);
        }

        private static TMP_InputField InputField(Transform parent, string placeholderText)
        {
            RectTransform root = Rect("QuestionInput", parent);
            Image bg = Image(root, Theme.panelRaised, true);

            RectTransform area = Stretch(Rect("TextArea", root), 18, 18, 6, 6);
            area.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI placeholder = Text(area, "Placeholder", placeholderText, 19, Theme.textMuted, TextAlignmentOptions.MidlineLeft, FontStyles.Italic, 0, false);
            Stretch(placeholder.rectTransform);
            TextMeshProUGUI text = Text(area, "Text", "", 19, Theme.textPrimary, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, 0, false);
            Stretch(text.rectTransform);

            var input = root.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = bg;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = Theme.font;
            input.pointSize = 19;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 200;
            input.caretColor = Theme.accent;
            input.customCaretColor = true;
            input.selectionColor = new Color(Theme.accent.r, Theme.accent.g, Theme.accent.b, 0.35f);
            input.restoreOriginalTextOnEscape = false;
            return input;
        }

        // ------------------------------------------------------------------ PAUSE + NOTEBOOK

        private static void BuildPauseMenu(Transform parent)
        {
            RectTransform screen = Stretch(Rect("PauseMenu", parent));
            screen.gameObject.AddComponent<CanvasGroup>();
            var pause = screen.gameObject.AddComponent<PauseMenuController>();
            Scrim(screen, new Color(0, 0, 0, 0.78f));

            // Left column with actions.
            RectTransform left = Region(Rect("Actions", screen), 0f, 0f, 0.3f, 1f);
            Image(left, Theme.panel);
            VLayout(left, 4, new RectOffset(64, 32, 90, 60));
            Label(left, "PAUSED", Theme.accent);
            Text(left, "Title", "INVESTIGATION", 34, Theme.textPrimary, TextAlignmentOptions.TopLeft, FontStyles.Normal, 6, false);
            Spacer(left, 36);
            Button resume = SpawnButton(MenuButton, left, "RESUME");
            Button settings = SpawnButton(MenuButton, left, "SETTINGS");
            Button save = SpawnButton(MenuButton, left, "SAVE INVESTIGATION");
            Button mainMenu = SpawnButton(MenuButton, left, "MAIN MENU");
            Button exit = SpawnButton(MenuButton, left, "EXIT GAME");
            foreach (Button b in new[] { resume, settings, save, mainMenu, exit }) Layout(b, -1, 54);

            // Right: notebook.
            RectTransform right = Region(Rect("Notebook", screen), 0.34f, 0.08f, 0.95f, 0.92f);
            VLayout(right, 10);
            var notebook = right.gameObject.AddComponent<NotebookView>();
            Label(right, "INVESTIGATION NOTES", Theme.accent);
            TextMeshProUGUI caseTitle = Text(right, "CaseTitle", "CASE", 30, Theme.textPrimary, TextAlignmentOptions.TopLeft, FontStyles.Normal, 3, false);
            Spacer(right, 6);
            Label(right, "CURRENT OBJECTIVE");
            TextMeshProUGUI objective = Text(right, "Objective", "Objective", 20, Theme.textSecondary);
            Spacer(right, 6);
            Divider(right);

            RectTransform columns = Rect("Columns", right);
            HorizontalLayoutGroup cols = HLayout(columns, 40, null, TextAnchor.UpperLeft);
            cols.childForceExpandWidth = true;
            Layout(columns, -1, -1, 1, 1);

            RectTransform evCol = Rect("EvidenceColumn", columns);
            VLayout(evCol, 8);
            Layout(evCol, -1, -1, 1, 1);
            TextMeshProUGUI evHeader = Label(evCol, "EVIDENCE");
            ScrollRect evScroll = ScrollList(evCol, "EvidenceList", 6, out RectTransform evContent);
            Layout(evScroll, -1, -1, 1, 1);
            GameObject evTemplate = Spawn(UIComponentLibrary.ListEntry, evContent, "EvidenceTemplate");

            RectTransform noteCol = Rect("NotesColumn", columns);
            VLayout(noteCol, 8);
            Layout(noteCol, -1, -1, 1, 1);
            Label(noteCol, "NOTES");
            TextMeshProUGUI empty = Text(noteCol, "Empty", "Nothing recorded yet. Examine the scene and question suspects.", 18, Theme.textMuted, TextAlignmentOptions.TopLeft, FontStyles.Italic);
            ScrollRect noteScroll = ScrollList(noteCol, "NoteList", 6, out RectTransform noteContent);
            Layout(noteScroll, -1, -1, 1, 1);
            GameObject noteTemplate = Spawn(UIComponentLibrary.ListEntry, noteContent, "NoteTemplate");

            SetField(notebook, "caseTitle", caseTitle);
            SetField(notebook, "objective", objective);
            SetField(notebook, "evidenceHeader", evHeader);
            SetField(notebook, "evidenceTemplate", evTemplate.GetComponent<UIListEntry>());
            SetField(notebook, "evidenceContainer", evContent);
            SetField(notebook, "noteTemplate", noteTemplate.GetComponent<UIListEntry>());
            SetField(notebook, "noteContainer", noteContent);
            SetField(notebook, "emptyNotes", empty);

            SetField(pause, "notebook", notebook);
            SetField(pause, "resumeButton", resume);
            SetField(pause, "settingsButton", settings);
            SetField(pause, "saveButton", save);
            SetField(pause, "mainMenuButton", mainMenu);
            SetField(pause, "exitButton", exit);
            SetField(pause, "firstSelected", resume.gameObject);
        }

        // ------------------------------------------------------------------ SETTINGS

        private static void BuildSettings(Transform parent)
        {
            RectTransform screen = Stretch(Rect("Settings", parent));
            screen.gameObject.AddComponent<CanvasGroup>();
            var settings = screen.gameObject.AddComponent<SettingsScreen>();
            Scrim(screen, new Color(0, 0, 0, 0.8f));

            RectTransform card = Place(Rect("Card", screen), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 880));
            Image(card, Theme.panel, true);
            VLayout(card, 2, new RectOffset(64, 64, 52, 44));

            Label(card, "OPTIONS", Theme.accent);
            Text(card, "Title", "SETTINGS", 34, Theme.textPrimary, TextAlignmentOptions.TopLeft, FontStyles.Normal, 6, false);
            Spacer(card, 14);

            Section(card, "CONTROLS");
            UISliderRow sensitivity = Row<UISliderRow>(SliderRow, card, "MouseSensitivity");
            UIOptionStepper invert = Row<UIOptionStepper>(OptionStepper, card, "InvertLook");
            Section(card, "AUDIO");
            UISliderRow volume = Row<UISliderRow>(SliderRow, card, "MasterVolume");
            Section(card, "DISPLAY");
            UIOptionStepper mode = Row<UIOptionStepper>(OptionStepper, card, "DisplayMode");
            UIOptionStepper resolution = Row<UIOptionStepper>(OptionStepper, card, "Resolution");
            UIOptionStepper quality = Row<UIOptionStepper>(OptionStepper, card, "Quality");
            UIOptionStepper vsync = Row<UIOptionStepper>(OptionStepper, card, "VSync");
            UISliderRow fov = Row<UISliderRow>(SliderRow, card, "FieldOfView");
            UISliderRow brightness = Row<UISliderRow>(SliderRow, card, "Brightness");

            Spacer(card, 22);
            RectTransform buttons = Rect("Buttons", card);
            HLayout(buttons, 12);
            Button reset = SpawnButton(ActionButton, buttons, "RESET TO DEFAULTS");
            Layout(reset, 260, 52);
            RectTransform gap = Rect("Gap", buttons);
            Layout(gap, -1, 10, 1);
            Button back = SpawnButton(ActionButton, buttons, "BACK");
            Layout(back, 180, 52);

            SetField(settings, "mouseSensitivity", sensitivity);
            SetField(settings, "invertY", invert);
            SetField(settings, "masterVolume", volume);
            SetField(settings, "displayMode", mode);
            SetField(settings, "resolution", resolution);
            SetField(settings, "quality", quality);
            SetField(settings, "vSync", vsync);
            SetField(settings, "fieldOfView", fov);
            SetField(settings, "brightness", brightness);
            SetField(settings, "resetButton", reset);
            SetField(settings, "backButton", back);
            SetField(settings, "firstSelected", back.gameObject);
        }

        private static void Section(Transform parent, string title)
        {
            Spacer(parent, 12);
            Label(parent, title, Theme.textMuted);
            Spacer(parent, 2);
            Divider(parent);
        }

        private static T Row<T>(GameObject prefab, Transform parent, string name) where T : Component
        {
            return Spawn(prefab, parent, name).GetComponent<T>();
        }

        // =====================================================================================
        // MAIN MENU (inside the MainMenu scene)
        // =====================================================================================

        public static void BuildMainMenu()
        {
            Theme = LoadOrCreateTheme();
            if (UIComponentLibrary.MenuButton == null) UIComponentLibrary.BuildAll();

            Canvas canvas = ScreenCanvas("MainMenuUI", null, 0);
            Transform root = canvas.transform;

            // Dark panel down the left for text contrast over the 3D backdrop.
            RectTransform shade = Region(Rect("LeftShade", root), 0f, 0f, 0.46f, 1f);
            Image(shade, new Color(0, 0, 0, 0.62f));

            // --- Main panel
            RectTransform main = Stretch(Rect("MainPanel", root));
            main.gameObject.AddComponent<CanvasGroup>();
            var mainScreen = main.gameObject.AddComponent<UIScreen>();

            RectTransform titleBlock = Place(Rect("TitleBlock", main), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(128, 150), new Vector2(1000, 230));
            VLayout(titleBlock, 12, null, TextAnchor.LowerLeft, true, false); // no width expansion, so the red rule keeps its 120px width
            Label(titleBlock, "A CASE FILE INTERROGATION THRILLER", Theme.textMuted);
            Text(titleBlock, "Title", "THE LAST ANSWER", Theme.titleSize, Theme.textPrimary, TextAlignmentOptions.BottomLeft, FontStyles.Bold, 10, false);
            RectTransform rule = Rect("Rule", titleBlock);
            Layout(rule, 120, 3);
            Image(rule, Theme.accent);
            Text(titleBlock, "Tagline", "Every case ends with one question.", 22, Theme.textSecondary, TextAlignmentOptions.BottomLeft, FontStyles.Italic, 1, false);

            RectTransform buttons = Place(Rect("Buttons", main), new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(106, 110), new Vector2(520, 420));
            VLayout(buttons, 2);
            Button newInv = SpawnButton(MenuButton, buttons, "NEW INVESTIGATION");
            Button cont = SpawnButton(MenuButton, buttons, "CONTINUE INVESTIGATION");
            TextMeshProUGUI contDetail = Text(buttons, "ContinueDetail", "No saved investigation", 14, Theme.textMuted, TextAlignmentOptions.TopLeft, FontStyles.Normal, 2, false);
            contDetail.margin = new Vector4(25, -10, 0, 6);
            Button cases = SpawnButton(MenuButton, buttons, "CASE FILES");
            Button settings = SpawnButton(MenuButton, buttons, "SETTINGS");
            Button exit = SpawnButton(MenuButton, buttons, "EXIT GAME");

            TextMeshProUGUI version = Text(main, "Version", "v0.1", 13, Theme.textMuted, TextAlignmentOptions.BottomLeft, FontStyles.Normal, 3, false);
            Place(version.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(128, 40), new Vector2(600, 24));

            SetField(mainScreen, "firstSelected", newInv.gameObject);
            SetBool(mainScreen, "startHidden", false);

            // --- Case files
            CaseFilesScreen caseFiles = BuildCaseFiles(root);

            var controller = canvas.gameObject.AddComponent<MainMenuController>();
            SetField(controller, "mainPanel", mainScreen);
            SetField(controller, "caseFiles", caseFiles);
            SetField(controller, "newInvestigationButton", newInv);
            SetField(controller, "continueButton", cont);
            SetField(controller, "caseFilesButton", cases);
            SetField(controller, "settingsButton", settings);
            SetField(controller, "exitButton", exit);
            SetField(controller, "continueDetail", contDetail);
            SetField(controller, "versionLabel", version);
        }

        private static CaseFilesScreen BuildCaseFiles(Transform parent)
        {
            RectTransform screen = Stretch(Rect("CaseFiles", parent));
            screen.gameObject.AddComponent<CanvasGroup>();
            var caseFiles = screen.gameObject.AddComponent<CaseFilesScreen>();
            Scrim(screen, new Color(Theme.background.r, Theme.background.g, Theme.background.b, 0.9f));

            // Left: list.
            RectTransform left = Region(Rect("List", screen), 0f, 0f, 0.34f, 1f);
            Image(left, Theme.panel);
            VLayout(left, 8, new RectOffset(64, 40, 90, 60));
            Label(left, "ARCHIVE", Theme.accent);
            Text(left, "Title", "CASE FILES", 34, Theme.textPrimary, TextAlignmentOptions.TopLeft, FontStyles.Normal, 6, false);
            Spacer(left, 24);
            RectTransform list = Rect("Cases", left);
            VLayout(list, 6);
            GameObject template = Spawn(ListButton, list, "CaseTemplate");

            // Right: the selected file.
            RectTransform right = Region(Rect("Detail", screen), 0.38f, 0.1f, 0.92f, 0.9f);
            VLayout(right, 10);
            TextMeshProUGUI number = Label(right, "CASE FILE · 0000", Theme.accent);
            TextMeshProUGUI title = Text(right, "CaseTitle", "Case Title", 46, Theme.textPrimary, TextAlignmentOptions.TopLeft, FontStyles.Normal, 1);
            TextMeshProUGUI meta = Label(right, "LOCATION     STATUS");
            Spacer(right, 6);
            Divider(right);
            Spacer(right, 6);
            TextMeshProUGUI briefing = Text(right, "Briefing", "Briefing.", 21, Theme.textSecondary);
            briefing.lineSpacing = 8f;
            Spacer(right, 10);
            TextMeshProUGUI suspects = Text(right, "Suspects", "", 18, Theme.textSecondary);
            suspects.lineSpacing = 10f;
            RectTransform filler = Rect("Filler", right);
            Layout(filler, -1, -1, 1, 1);
            RectTransform buttonRow = Rect("Buttons", right);
            HLayout(buttonRow, 12);
            Button open = SpawnButton(ActionButton, buttonRow, "OPEN INVESTIGATION");
            Layout(open, 300, 54);
            Button back = SpawnButton(ActionButton, buttonRow, "BACK");
            Layout(back, 160, 54);

            SetField(caseFiles, "caseButtonTemplate", template.GetComponent<Button>());
            SetField(caseFiles, "caseListContainer", list);
            SetField(caseFiles, "caseNumber", number);
            SetField(caseFiles, "caseTitle", title);
            SetField(caseFiles, "caseMeta", meta);
            SetField(caseFiles, "caseBriefing", briefing);
            SetField(caseFiles, "caseSuspects", suspects);
            SetField(caseFiles, "openButton", open);
            SetField(caseFiles, "backButton", back);
            SetField(caseFiles, "firstSelected", back.gameObject);
            return caseFiles;
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
