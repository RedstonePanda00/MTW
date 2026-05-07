using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace NCL
{
    class NCL_StoryWC : WorldComponent
    {
        // 基础属性
        private int ticks = -1;
        private string lastKnownVersion = string.Empty; // 存档中保存的上次运行MOD版本
        private bool ranActionsOnceAtStartUp;

        // 故事标志字典 - 包含持久化的各种标志
        public Dictionary<string, bool> storyFlags;

        // 当前MOD版本 - 硬编码，每次更新MOD时手动修改
        private const string currentVersion = "2.0.1";

        // 是否强制显示更新信息 - 每次需要显示更新信息但不想更改版本号时设为true
        private const bool forceShowUpdateInfo = false;

        public NCL_StoryWC(World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref storyFlags, "storyFlags", LookMode.Value);
            Scribe_Values.Look(ref lastKnownVersion, "lastKnownVersion", string.Empty);
            Scribe_Values.Look(ref ticks, "ticks", -1);
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);

            // 初始化故事标志
            if (storyFlags == null)
                storyFlags = new Dictionary<string, bool>();

            // 确保存在启动窗口显示标志
            if (!storyFlags.ContainsKey("NCL_Display_Settings_Menu"))
                storyFlags["NCL_Display_Settings_Menu"] = false;

            // 检查是否有版本更新
            bool isVersionUpdated = CheckVersionUpdate();

            // 只有在版本更新的情况下才重置窗口显示标志
            if (isVersionUpdated || forceShowUpdateInfo)
            {
                storyFlags["NCL_Display_Settings_Menu"] = false;
                Log.Message("NCL_Log_VersionUpdated".Translate());
            }
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            // 执行启动时的一次性操作
            if (!ranActionsOnceAtStartUp)
            {
                RunActionsOnceAtStartUp();
                ranActionsOnceAtStartUp = true;
            }

            // 在游戏启动后一段时间检查是否显示菜单
            if (ticks == 260)
            {
                // 检查是否需要显示菜单
                bool shouldShowMenu = false;
                bool isVersionUpdated = CheckVersionUpdate();

                // 分情况确定是否显示菜单：
                // 1. 有版本更新（或强制显示）时总是显示
                // 2. 没有版本更新，且未设置禁用菜单，且此存档中尚未显示过设置菜单
                if (isVersionUpdated || forceShowUpdateInfo)
                {
                    shouldShowMenu = true;
                }
                else if (!storyFlags["NCL_Display_Settings_Menu"] && !ModSettingsAbout.GG_Disable_Settings_Window)
                {
                    shouldShowMenu = true;
                }

                if (shouldShowMenu)
                {
                    Find.WindowStack.Add(new NewStorySettings(isVersionUpdated || forceShowUpdateInfo, currentVersion));

                    if (isVersionUpdated || forceShowUpdateInfo)
                    {
                        Log.Message("更新信息菜单显示任务执行完成");
                        // 记录新版本号，这样下次启动就不会再显示此更新提示
                        lastKnownVersion = currentVersion;
                    }
                    else
                    {
                        Log.Message("常规设置菜单显示任务执行完成");
                    }

                    // 标记已显示过设置菜单 - 这个标志会持久化到存档中
                    storyFlags["NCL_Display_Settings_Menu"] = true;
                }
            }

            ticks++;
        }

        private void RunActionsOnceAtStartUp()
        {
            try
            {
                // 这里可以添加每次启动时需要执行的其他操作
                Log.Message("正在执行启动时的初始化操作");
            }
            catch (Exception ex)
            {
                Log.Error($"执行启动操作时出错: {ex.Message}");
            }
        }

        // 检查MOD版本是否有更新
        private bool CheckVersionUpdate()
        {
            // 如果强制显示更新信息，直接返回true
            if (forceShowUpdateInfo)
                return true;

            // 如果上次记录的版本不为空且与当前版本不同，表示有更新
            // 首次运行时不显示更新信息，只有在真正有版本更新时才显示
            return !string.IsNullOrEmpty(lastKnownVersion) && currentVersion != lastKnownVersion;
        }
    }
}




namespace NCL
{
    public class NewStorySettings : Page
    {
        private float width = 600f; // 窗口宽度
        private float height = 700f; // 窗口高度
        private Vector2 scrollPos = Vector2.zero; // 文本区域滚动位置

        // 轮播图相关变量中添加按钮贴图
        private List<Texture2D> carouselImages = new List<Texture2D>(); // 轮播图片列表
        private int currentImageIndex = 0; // 当前显示的图片索引
        private float lastImageChangeTime; // 上次图片切换时间
        private const float IMAGE_CHANGE_INTERVAL = 3f; // 图片切换间隔（秒）
        private const float CAROUSEL_HEIGHT = 160f; // 轮播图高度
        private Texture2D leftArrowTexture; // 左箭头按钮贴图
        private Texture2D rightArrowTexture; // 右箭头按钮贴图

        // 背景图片
        private Texture2D backgroundImage;

        // 文本区域高度 - 主页面和子页面使用不同的高度
        private const float MAIN_TEXT_AREA_HEIGHT = 280f;
        private const float SUB_TEXT_AREA_HEIGHT = 460f; // 子页面文本区域高度更大，延伸到顶部

        // 窗口ID，确保唯一
        private readonly int windowId = 11459;

        // 页面相关变量
        private enum PageType
        {
            Main,   // 主页面
            Page1,  // 页面1
            Page2,  // 页面2
            Page3   // 页面3
        }

        private PageType currentPage = PageType.Main; // 当前显示的页面
        private Dictionary<PageType, string> pageContents; // 各页面的内容

        // 在NewStorySettings类中添加/修改
        private bool isVersionUpdate = false;
        private string currentVersion = ""; // 当前版本号
        public NewStorySettings(bool isVersionUpdate = false, string version = "")
        {
            doCloseButton = true; // 显示关闭按钮
            closeOnCancel = true; // 点击取消时关闭窗口

            // 记录是否为版本更新模式和当前版本
            this.isVersionUpdate = isVersionUpdate;
            this.currentVersion = version;

            // 初始化上次图片切换时间
            lastImageChangeTime = Time.realtimeSinceStartup;

            // 初始化图片资源
            InitializeImages();

            // 初始化页面内容
            InitializePageContents();
        }

        // 修改InitializePageContents方法
        private void InitializePageContents()
        {
            string updatePrefix = isVersionUpdate ?
                "NCL_Content_UpdatePrefix".Translate(currentVersion) : "";

            pageContents = new Dictionary<PageType, string>
        {
            {
                PageType.Main,
                updatePrefix + "NCL_Content_Main".Translate(currentVersion)
            },
            {
                PageType.Page1,
                updatePrefix + "NCL_Content_Page1".Translate(currentVersion)
            },
            {
                PageType.Page2,
                updatePrefix + "NCL_Content_Page2".Translate(currentVersion)
            },
            {
                PageType.Page3,
                updatePrefix + "NCL_Content_Page3".Translate(currentVersion)
            }
        };
        }

        private Dictionary<PageType, Texture2D> backgroundImages = new Dictionary<PageType, Texture2D>();
        // 在InitializeImages方法中添加按钮贴图的加载
        private void InitializeImages()
        {
            try
            {
                // 加载背景图片
                // 加载主页面背景图片
                backgroundImages[PageType.Main] = ContentFinder<Texture2D>.Get("ModIcon/BackGround", false);
                if (backgroundImages[PageType.Main] == null)
                    backgroundImages[PageType.Main] = CreateDefaultTexture(600, 700, new Color(0.25f, 0.25f, 0.25f));

                // 加载其他页面的背景图片 - 使用不同的背景图片
                backgroundImages[PageType.Page1] = ContentFinder<Texture2D>.Get("ModIcon/BackGround", false);
                if (backgroundImages[PageType.Page1] == null)
                    backgroundImages[PageType.Page1] = CreateDefaultTexture(600, 700, new Color(0.3f, 0.2f, 0.2f));

                backgroundImages[PageType.Page2] = ContentFinder<Texture2D>.Get("ModIcon/BackGround", false);
                if (backgroundImages[PageType.Page2] == null)
                    backgroundImages[PageType.Page2] = CreateDefaultTexture(600, 700, new Color(0.2f, 0.3f, 0.2f));

                backgroundImages[PageType.Page3] = ContentFinder<Texture2D>.Get("ModIcon/BackGround", false);
                if (backgroundImages[PageType.Page3] == null)
                    backgroundImages[PageType.Page3] = CreateDefaultTexture(600, 700, new Color(0.2f, 0.2f, 0.3f));

                // 加载轮播图片
                var image1 = ContentFinder<Texture2D>.Get("ModIcon/ModAbout", false);
                var image2 = ContentFinder<Texture2D>.Get("ModIcon/ModAbout", false);
                var image3 = ContentFinder<Texture2D>.Get("ModIcon/ModUpdate", false);

                // 如果图片加载成功，则添加到列表中，否则添加默认白色图片
                carouselImages.Add(image1 ?? CreateDefaultTexture(400, 160, Color.white));
                carouselImages.Add(image2 ?? CreateDefaultTexture(400, 160, Color.white));
                carouselImages.Add(image3 ?? CreateDefaultTexture(400, 160, Color.white));

                // 加载箭头按钮贴图
                leftArrowTexture = ContentFinder<Texture2D>.Get("ModIcon/AboutLeft", false) ?? CreateDefaultTexture(30, 30, Color.white);
                rightArrowTexture = ContentFinder<Texture2D>.Get("ModIcon/AboutRight", false) ?? CreateDefaultTexture(30, 30, Color.white);

            }
            catch (Exception ex)
            {
                Log.Error($"加载图片资源时出错: {ex.Message}");

                // 创建默认背景图片
                foreach (PageType pageType in Enum.GetValues(typeof(PageType)))
                {
                    Color bgColor;
                    switch (pageType)
                    {
                        case PageType.Main:
                            bgColor = new Color(0.25f, 0.25f, 0.25f);
                            break;
                        case PageType.Page1:
                            bgColor = new Color(0.3f, 0.2f, 0.2f);
                            break;
                        case PageType.Page2:
                            bgColor = new Color(0.2f, 0.3f, 0.2f);
                            break;
                        case PageType.Page3:
                            bgColor = new Color(0.2f, 0.2f, 0.3f);
                            break;
                        default:
                            bgColor = new Color(0.25f, 0.25f, 0.25f);
                            break;
                    }
                    backgroundImages[pageType] = CreateDefaultTexture(600, 700, bgColor);
                }

                if (carouselImages.Count == 0)
                {
                    carouselImages.Add(CreateDefaultTexture(400, 160, Color.white));
                    carouselImages.Add(CreateDefaultTexture(400, 160, Color.white));
                    carouselImages.Add(CreateDefaultTexture(400, 160, Color.white));
                }

                // 创建默认箭头按钮贴图
                leftArrowTexture = CreateDefaultTexture(30, 30, Color.white);
                rightArrowTexture = CreateDefaultTexture(30, 30, Color.white);
            }
        }

        // 创建默认纯色纹理的通用方法
        private Texture2D CreateDefaultTexture(int width, int height, Color color)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        protected override void SetInitialSizeAndPosition()
        {
            // 设置窗口的初始位置和大小，使其居中显示
            windowRect = new Rect((UI.screenWidth - width) / 2f, (UI.screenHeight - height) / 2f, width, height);
            windowRect = windowRect.Rounded(); // 将窗口位置四舍五入
        }

        // 覆盖PreOpen方法以确保窗口正确初始化
        public override void PreOpen()
        {
            base.PreOpen();
            // 确保正确设置窗口位置和大小
            SetInitialSizeAndPosition();
        }

        // 使用RimWorld的标准窗口绘制方法，确保交互正常工作

        // 修改WindowOnGUI方法，使用当前页面对应的背景图
        public override void WindowOnGUI()
        {
            // 只在主页面更新轮播图片
            if (currentPage == PageType.Main)
            {
                UpdateCarousel();
            }

            // 使用标准窗口框架，但设置为完全透明
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0f); // 完全透明的窗口框架

            // 使用标准窗口函数，但不绘制边框和背景
            Rect inRect = windowRect.AtZero();

            // 使用 GUI.Window 但设置为完全透明样式
            GUI.Window(windowId, windowRect, DoWindowContentsWrapper, "", GUI.skin.window);

            // 恢复颜色设置
            GUI.color = oldColor;

            // 根据当前页面绘制对应的背景图
            if (Event.current.type == EventType.Repaint && backgroundImages.ContainsKey(currentPage))
            {
                GUI.DrawTexture(windowRect, backgroundImages[currentPage]);
            }
        }

        // 窗口内容包装器 - 用于标准GUI.Window调用
        private void DoWindowContentsWrapper(int id)
        {
            // 内容区域，缩小以留出边距
            Rect inRect = new Rect(0f, 0f, windowRect.width, windowRect.height).ContractedBy(18f);

            // 处理实际内容
            DoWindowContents(inRect);

            // 允许窗口拖动 - 只在顶部区域生效
            GUI.DragWindow(new Rect(0, 0, windowRect.width, 30f));
        }

        public override void DoWindowContents(Rect wrect)
        {

            // 确保GUI是启用状态
            GUI.enabled = true;

            Listing_Standard options = new Listing_Standard();
            options.Begin(wrect);

            // 只在主页面显示轮播图
            if (currentPage == PageType.Main)
            {
                Rect carouselRect = options.GetRect(CAROUSEL_HEIGHT);
                DrawCarousel(carouselRect);
                options.Gap(10);
            }

            Rect subtitleRect = options.GetRect(40);
            DrawStyledText(subtitleRect, "NCL_Subtitle".Translate());

            options.Gap(15);

            string titleText = "";
            if (isVersionUpdate)
            {
                switch (currentPage)
                {
                    case PageType.Main:
                        titleText = "NCL_Title_UpdateWithPage".Translate(currentVersion, "NCL_Title_Main".Translate());
                        break;
                    case PageType.Page1:
                        titleText = "NCL_Title_UpdateWithPage".Translate(currentVersion, "NCL_Title_Page1".Translate());
                        break;
                    case PageType.Page2:
                        titleText = "NCL_Title_UpdateWithPage".Translate(currentVersion, "NCL_Title_Page2".Translate());
                        break;
                    case PageType.Page3:
                        titleText = "NCL_Title_UpdateWithPage".Translate(currentVersion, "NCL_Title_Page3".Translate());
                        break;
                    default:
                        titleText = "NCL_Title_UpdateWithPage".Translate(currentVersion, "NCL_Title_Main".Translate());
                        break;
                }
            }
            else
            {
                switch (currentPage)
                {
                    case PageType.Main: titleText = "NCL_Title_Main".Translate(); break;
                    case PageType.Page1: titleText = "NCL_Title_Page1".Translate(); break;
                    case PageType.Page2: titleText = "NCL_Title_Page2".Translate(); break;
                    case PageType.Page3: titleText = "NCL_Title_Page3".Translate(); break;
                }
            }

            Rect titleRect = options.GetRect(40);
            DrawStyledText(titleRect, titleText, GameFont.Small, TextAnchor.MiddleCenter,
                          isVersionUpdate ? Color.yellow : Color.white); // 版本更新时使用黄色

            // 创建一个可滚动的文本区域
            float textAreaHeight = currentPage == PageType.Main ? MAIN_TEXT_AREA_HEIGHT : SUB_TEXT_AREA_HEIGHT;
            Rect outRect = options.GetRect(textAreaHeight);
            Rect viewRect = new Rect(0, 0, outRect.width - 20f, 600f);

            // 文本区域背景
            Widgets.DrawBoxSolid(outRect, new Color(0f, 0f, 0f, 0.2f));

            // 使用RimWorld原生滚动视图
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);

            Listing_Standard textContent = new Listing_Standard();
            textContent.Begin(viewRect);
            textContent.Label(pageContents[currentPage]);
            textContent.End();

            Widgets.EndScrollView();

            options.Gap(15);
            // 在滚动文本区域下面添加返回按钮
            if (currentPage != PageType.Main)
            {
                Rect returnButtonRect = options.GetRect(30);
                returnButtonRect.width = 120;
                returnButtonRect.x = (wrect.width - returnButtonRect.width) / 2; // 居中按钮

                // 使用之前定义的DrawCustomButton方法实现自定义贴图的按钮
                Texture2D returnButtonTexture = ContentFinder<Texture2D>.Get("ModIcon/Taptaptap", false);
                if (DrawCustomButton(returnButtonRect,
                    "NCL_Button_Return".Translate(), returnButtonTexture))
                {
                    currentPage = PageType.Main;
                    scrollPos = Vector2.zero; // 重置滚动位置
                }

                options.Gap(10);
            }


            // 复选框 - 使用RimWorld原生控件
            if (currentPage == PageType.Main)
            {
                Rect checkboxBgRect = options.GetRect(Text.LineHeight + 10);
                checkboxBgRect.width = 310;
                Widgets.DrawBoxSolid(checkboxBgRect, new Color(0f, 0f, 0f, 0.2f));

                Rect checkboxRect = checkboxBgRect.ContractedBy(5);
                bool checkboxValue = ModSettingsAbout.GG_Disable_Settings_Window;

                // 修改复选框实现，使用更基础的方法确保点击响应
                Rect boxRect = new Rect(checkboxRect.x, checkboxRect.y, 24f, 24f);
                Rect labelRect = new Rect(checkboxRect.x + 28f, checkboxRect.y, checkboxRect.width - 28f, checkboxRect.height);

                // 分别处理点击区域
                if (Widgets.ButtonInvisible(boxRect) || Widgets.ButtonInvisible(labelRect))
                {
                    checkboxValue = !checkboxValue;
                    ModSettingsAbout.GG_Disable_Settings_Window = checkboxValue;
                    Log.Message($"更改禁用窗口设置为: {checkboxValue}");
                }

                // 绘制复选框和标签
                Widgets.CheckboxDraw(boxRect.x, boxRect.y, checkboxValue, true);

                string labelText = isVersionUpdate ?
                    "NCL_Checkbox_DisableWindowWithUpdate".Translate() :
                    "NCL_Checkbox_DisableWindow".Translate();
                Widgets.Label(labelRect, labelText);
            }

            options.Gap(15);

            // 页面切换按钮区域 - 无论是否为版本更新模式都显示
            if (currentPage == PageType.Main)
            {
                Rect buttonsAreaRect = options.GetRect(24);
                float buttonWidth = (buttonsAreaRect.width - 40 - 50) / 3; // 减去关闭按钮宽度和间距

                // 使用自定义图片的按钮
                Rect button1Rect = new Rect(buttonsAreaRect.x, buttonsAreaRect.y, buttonWidth, 40);
                if (DrawCustomButton(button1Rect,
                    "NCL_Button_Page1".Translate(), ContentFinder<Texture2D>.Get("ModIcon/Taptaptap", false)))
                {
                    currentPage = PageType.Page1;
                    scrollPos = Vector2.zero;
                }

                Rect button2Rect = new Rect(buttonsAreaRect.x + buttonWidth + 20, buttonsAreaRect.y, buttonWidth, 40);
                if (DrawCustomButton(button2Rect,
                    "NCL_Button_Page2".Translate(), ContentFinder<Texture2D>.Get("ModIcon/Taptaptap", false)))
                {
                    currentPage = PageType.Page2;
                    scrollPos = Vector2.zero;
                }

                Rect button3Rect = new Rect(buttonsAreaRect.x + (buttonWidth + 20) * 2, buttonsAreaRect.y, buttonWidth, 40);
                if (DrawCustomButton(button3Rect,
                    "NCL_Button_Page3".Translate(), ContentFinder<Texture2D>.Get("ModIcon/Taptaptap", false)))
                {
                    currentPage = PageType.Page3;
                    scrollPos = Vector2.zero;
                }

                // 添加关闭按钮在最右侧
                if (doCloseButton)
                {
                    float closeButtonWidth = 40;
                    float closeButtonHeight = 40;
                    Rect closeRect = new Rect(buttonsAreaRect.x + buttonsAreaRect.width - closeButtonWidth, buttonsAreaRect.y, closeButtonWidth, closeButtonHeight);

                    // 使用自定义图片的关闭按钮
                    Texture2D closeButtonTexture = ContentFinder<Texture2D>.Get("ModIcon/NoNoNo", false);
                    if (DrawCustomButton(closeRect, "", closeButtonTexture))
                    {
                        Close();
                    }
                }
            }

            options.End();
        }


        // 自定义按钮绘制，整合了之前多个按钮绘制方法
        private bool DrawCustomButton(Rect rect, string label, Texture2D customTexture)
        {
            // 绘制背景
            if (customTexture != null)
            {
                GUI.DrawTexture(rect, customTexture);
            }
            else
            {
                // 使用白色作为默认背景
                Widgets.DrawBoxSolid(rect, new Color(1f, 1f, 1f, 0.8f));
            }

            // 检测鼠标悬停并高亮
            bool mouseOver = Mouse.IsOver(rect);
            if (mouseOver)
            {
                Widgets.DrawHighlight(rect);
            }

            // 只有当标签不为空时才绘制文本
            if (!string.IsNullOrEmpty(label))
            {
                // 保存原始字体设置
                GameFont originalFont = Text.Font;
                TextAnchor originalAnchor = Text.Anchor;
                Color originalColor = GUI.color;

                // 设置加粗的字体
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white; // 设置为白色文字

                // 创建临时GUIStyle来设置加粗效果（用于GUI.Label）
                GUIStyle boldStyle = new GUIStyle(Text.CurFontStyle)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

                // 绘制文字阴影效果增强可读性
                Rect shadowRect = rect;
                shadowRect.x += 1;
                shadowRect.y += 1;

                // 使用GUI.Label绘制阴影（支持GUIStyle参数）
                GUI.color = new Color(0f, 0f, 0f, 0.8f);
                GUI.Label(shadowRect, label, boldStyle);

                // 绘制白色文字
                GUI.color = Color.white;
                GUI.Label(rect, label, boldStyle);

                // 恢复原始设置
                Text.Font = originalFont;
                Text.Anchor = originalAnchor;
                GUI.color = originalColor;
            }

            // 检测点击
            return Widgets.ButtonInvisible(rect);
        }

        private void DrawStyledText(Rect rect, string text, GameFont font = GameFont.Small,
                           TextAnchor anchor = TextAnchor.MiddleCenter,
                           Color? textColor = null)
        {
            // 默认使用白色
            Color color = textColor ?? Color.white;

            // 保存原始字体设置
            GameFont originalFont = Text.Font;
            TextAnchor originalAnchor = Text.Anchor;
            Color originalColor = GUI.color;

            // 设置字体
            Text.Font = font;
            Text.Anchor = anchor;

            // 创建临时GUIStyle来设置加粗效果
            GUIStyle boldStyle = new GUIStyle(Text.CurFontStyle)
            {
                fontStyle = FontStyle.Bold,
                alignment = anchor
            };

            // 绘制文字阴影效果增强可读性
            Rect shadowRect = rect;
            shadowRect.x += 1;
            shadowRect.y += 1;
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(shadowRect, text, boldStyle);

            // 绘制主文字
            GUI.color = color;
            GUI.Label(rect, text, boldStyle);

            // 恢复原始设置
            Text.Font = originalFont;
            Text.Anchor = originalAnchor;
            GUI.color = originalColor;
        }

        // 修改轮播图绘制方法，调整预览图尺寸为固定大小
        private void DrawCarousel(Rect rect)
        {
            if (carouselImages.Count == 0)
                return;

            Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.3f));

            // 绘制当前图片
            Rect imageRect = rect.ContractedBy(4f);
            GUI.DrawTexture(imageRect, carouselImages[currentImageIndex], ScaleMode.ScaleToFit);

            // 计算下一张图片的索引
            int nextImageIndex = (currentImageIndex + 1) % carouselImages.Count;
            int prevImageIndex = (currentImageIndex - 1 + carouselImages.Count) % carouselImages.Count;

            // 使用固定尺寸的预览图，不再计算比例
            float previewWidth = 100f; // 宽度设为100
            float previewHeight = 60f;  // 高度设为60 (100:60 = 10:6)
            float buttonWidth = 30f;
            float buttonHeight = 30f;
            float buttonPadding = 5f;

            // 左侧预览图和按钮（靠近轮播图的位置）
            Rect leftPreviewRect = new Rect(
                rect.x + buttonWidth + buttonPadding * 2,
                rect.y + (rect.height - previewHeight) / 2,
                previewWidth,
                previewHeight);

            Rect leftArrowRect = new Rect(
                leftPreviewRect.x - buttonWidth - buttonPadding,
                rect.y + (rect.height - buttonHeight) / 2,
                buttonWidth,
                buttonHeight);

            // 右侧预览图和按钮（靠近轮播图的位置）
            Rect rightPreviewRect = new Rect(
                rect.x + rect.width - previewWidth - buttonWidth - buttonPadding * 2,
                rect.y + (rect.height - previewHeight) / 2,
                previewWidth,
                previewHeight);

            Rect rightArrowRect = new Rect(
                rightPreviewRect.x + previewWidth + buttonPadding,
                rect.y + (rect.height - buttonHeight) / 2,
                buttonWidth,
                buttonHeight);

            // 绘制预览图内容和边框
            Color oldColor = GUI.color;

            // 左边预览图 - 先绘制边框，再绘制图片确保边框可见
            Widgets.DrawBox(leftPreviewRect, 2); // 加粗边框线条
            GUI.color = new Color(0.8f, 0.8f, 0.8f, 0.7f); // 稍微暗一点，半透明
            GUI.DrawTexture(leftPreviewRect, carouselImages[prevImageIndex], ScaleMode.ScaleToFit);

            // 右边预览图
            Widgets.DrawBox(rightPreviewRect, 2); // 加粗边框线条
            GUI.DrawTexture(rightPreviewRect, carouselImages[nextImageIndex], ScaleMode.ScaleToFit);

            // 恢复颜色
            GUI.color = oldColor;

            // 绘制按钮 - 确保不重复绘制
            if (leftArrowTexture != null)
            {
                // 确保按钮绘制时没有透明度
                GUI.color = Color.white;
                GUI.DrawTexture(leftArrowRect, leftArrowTexture, ScaleMode.ScaleToFit);

                // 点击检测
                if (Widgets.ButtonInvisible(leftArrowRect))
                {
                    currentImageIndex = prevImageIndex;
                    lastImageChangeTime = Time.realtimeSinceStartup;
                }
            }
            else if (Widgets.ButtonText(leftArrowRect, "◀")) // 如果没有贴图则使用文本按钮
            {
                currentImageIndex = prevImageIndex;
                lastImageChangeTime = Time.realtimeSinceStartup;
            }

            if (rightArrowTexture != null)
            {
                // 确保按钮绘制时没有透明度
                GUI.color = Color.white;
                GUI.DrawTexture(rightArrowRect, rightArrowTexture, ScaleMode.ScaleToFit);

                // 点击检测
                if (Widgets.ButtonInvisible(rightArrowRect))
                {
                    currentImageIndex = nextImageIndex;
                    lastImageChangeTime = Time.realtimeSinceStartup;
                }
            }
            else if (Widgets.ButtonText(rightArrowRect, "▶")) // 如果没有贴图则使用文本按钮
            {
                currentImageIndex = nextImageIndex;
                lastImageChangeTime = Time.realtimeSinceStartup;
            }

            // 恢复颜色设置
            GUI.color = oldColor;

            // 导航点
            float dotSize = 8f;
            float spacing = 4f;
            float totalWidth = (carouselImages.Count * dotSize) + ((carouselImages.Count - 1) * spacing);
            float startX = rect.x + (rect.width - totalWidth) / 2;
            float y = rect.y + rect.height - dotSize - 8f;

            for (int i = 0; i < carouselImages.Count; i++)
            {
                Rect dotRect = new Rect(startX + (i * (dotSize + spacing)), y, dotSize, dotSize);
                Color dotColor = i == currentImageIndex ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.7f);
                Widgets.DrawBoxSolid(dotRect, dotColor);

                if (Widgets.ButtonInvisible(dotRect))
                {
                    currentImageIndex = i;
                    lastImageChangeTime = Time.realtimeSinceStartup;
                }
            }
        }
        // 更新轮播图片
        private void UpdateCarousel()
        {
            float currentTime = Time.realtimeSinceStartup;

            // 检查是否应该切换到下一张图片
            if (currentTime - lastImageChangeTime >= IMAGE_CHANGE_INTERVAL && carouselImages.Count > 0)
            {
                currentImageIndex = (currentImageIndex + 1) % carouselImages.Count;
                lastImageChangeTime = currentTime;
            }
        }

        private IEnumerable<Widgets.DropdownMenuElement<StoryMode>> GenerateStoryModeDropDownContent(StoryMode target)
        {
            // 生成下拉菜单内容
            foreach (var difficulty in Enum.GetValues(typeof(StoryMode)).Cast<StoryMode>())
                yield return new Widgets.DropdownMenuElement<StoryMode>() { option = new FloatMenuOption(difficulty.ToString(), () => ModSettingsAbout.storyMode = difficulty), payload = difficulty };
        }
    }
}


namespace NCL
{
    public enum StoryMode
    {
        Normal,
        Performance,
    }

    class ModSettingsAbout : Verse.ModSettings
    {
        public static bool GG_Disable_Settings_Window;
        public static StoryMode storyMode = StoryMode.Normal;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref GG_Disable_Settings_Window, "NCL_Disable_Settings_Window", false);
        }
    }
}
