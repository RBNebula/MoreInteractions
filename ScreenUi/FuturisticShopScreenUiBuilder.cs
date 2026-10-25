using TMPro;
using UnityEngine.UI;

namespace MoreInteractions.ScreenUi;

internal static class FuturisticShopScreenUiBuilder
{
    internal static readonly Vector2 ReferenceCanvasSize = new(1200f, 800f);

    private static Sprite? s_defaultSprite;
    private static TMP_FontAsset? s_font;

    private static readonly Color Background = new(0.04f, 0.07f, 0.16f, 0.96f);
    private static readonly Color BackgroundSecondary = new(0.03f, 0.05f, 0.11f, 0.92f);
    private static readonly Color Panel = new(0.10f, 0.15f, 0.24f, 0.78f);
    private static readonly Color PanelSoft = new(0.12f, 0.18f, 0.29f, 0.52f);
    private static readonly Color Outline = new(0.52f, 0.82f, 1.00f, 0.34f);
    private static readonly Color OutlineStrong = new(0.48f, 0.89f, 1.00f, 0.78f);
    private static readonly Color Accent = new(0.18f, 0.86f, 0.95f, 1f);
    private static readonly Color AccentSoft = new(0.18f, 0.86f, 0.95f, 0.16f);
    private static readonly Color AccentGreen = new(0.26f, 0.86f, 0.58f, 1f);
    private static readonly Color WhiteStrong = new(0.96f, 0.98f, 1f, 1f);
    private static readonly Color WhiteSoft = new(0.86f, 0.91f, 1f, 0.74f);
    private static readonly Color WhiteDim = new(0.84f, 0.89f, 0.98f, 0.45f);

    private static readonly string[] Categories =
    {
        "CONVEYORS",
        "CHUTES",
        "MINING",
        "MACHINES",
        "BUILDING",
        "TOOLS",
        "LIGHTING",
        "UTILITY"
    };

    private static readonly ShopCardData[] ShopCards =
    {
        new("STARTER CONVEYOR MK.I", "Compact entry-tier belt for short ore runs and first-stage sorting.", "$120", "01"),
        new("LONGLINE CONVEYOR MK.II", "Extended run segment with reduced friction rails for stable throughput.", "$280", "02"),
        new("TURBO CONVEYOR MK.III", "Boosted belt speed with reinforced bearings for fast ore transfer.", "$540", "03"),
        new("CORNER CONVEYOR L-45", "Precision corner unit for routing around tight base geometry.", "$360", "04"),
        new("VERTICAL LIFT CONVEYOR", "Stack-safe vertical section for moving ore between floor levels.", "$710", "05"),
        new("DUAL-LANE CONVEYOR", "Two-channel splitter for parallel ore and utility routing.", "$940", "06")
    };

    private static readonly BasketItemData[] BasketItems =
    {
        new("Starter Conveyor Mk.I", "x12"),
        new("Turbo Conveyor Mk.III", "x03"),
        new("Vertical Lift Conveyor", "x01")
    };

    internal static void Build(RectTransform root)
    {
        EnsureResources();
        ClearChildren(root);
        root.sizeDelta = ReferenceCanvasSize;
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);

        CreateBackground(root);
        RectTransform shell = CreateRect(root, "ScreenShell", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(26f, 20f), new Vector2(-26f, -18f));
        CreateTopTabs(shell);
        RectTransform mainPanel = CreateMainPanel(shell);
        CreateCategoryStrip(mainPanel);
        CreateSectionHeader(mainPanel);
        CreateFilters(mainPanel);
        CreateShopGrid(mainPanel);
        CreateBasket(mainPanel);
    }

    private static void CreateBackground(RectTransform root)
    {
        Image baseLayer = CreateImage(root, "BaseLayer", Stretch(), Background);
        baseLayer.raycastTarget = false;

        CreateImage(root, "NebulaTopGlow", new Rect(90f, 555f, 1020f, 180f), new Color(0.12f, 0.36f, 0.55f, 0.26f));
        CreateImage(root, "NebulaBottomGlow", new Rect(180f, 40f, 840f, 200f), new Color(0.07f, 0.20f, 0.31f, 0.30f));
        CreateImage(root, "CenterGlow", new Rect(355f, 178f, 490f, 450f), new Color(0.08f, 0.22f, 0.35f, 0.28f));

        RectTransform stars = CreateRect(root, "DecorativeGrid", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(60f, 54f), new Vector2(-60f, -50f));
        for (int i = 0; i < 12; i++)
        {
            float x = 40f + (i * 86f);
            float y = 70f + ((i % 4) * 148f);
            CreateImage(stars, $"Star_{i + 1:00}", new Rect(x, y, 3f, 3f), new Color(0.78f, 0.93f, 1f, 0.45f));
        }
    }

    private static void CreateTopTabs(RectTransform shell)
    {
        RectTransform tabs = CreateRect(shell, "TopTabs", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), new Vector2(0f, 0f));
        CreateTab(tabs, "BuyTab", new Rect(0f, 12f, 128f, 48f), "BUY", true);
        CreateTab(tabs, "SellTab", new Rect(130f, 12f, 128f, 48f), "SELL", false);

        RectTransform wallet = CreatePanel(tabs, "WalletPanel", new Rect(910f, 10f, 260f, 50f), PanelSoft, Outline);
        CreateLabel(wallet, "WalletCaption", new Rect(18f, 12f, 70f, 24f), "WALLET", 18f, WhiteDim, FontStyles.Normal, TextAlignmentOptions.Left);
        CreateLabel(wallet, "WalletAmount", new Rect(90f, 8f, 150f, 28f), "$2,614,091", 24f, WhiteStrong, FontStyles.Bold, TextAlignmentOptions.Left);
        CreateEdgeGlow(wallet);
    }

    private static RectTransform CreateMainPanel(RectTransform shell)
    {
        RectTransform mainPanel = CreatePanel(shell, "MainPanel", new Rect(0f, 54f, 1172f, 706f), Panel, Outline);
        CreateEdgeGlow(mainPanel);
        CreateImage(mainPanel, "MainPanelInnerFill", new Rect(8f, 8f, 1156f, 690f), BackgroundSecondary);
        CreatePanelFrame(mainPanel, 14f, OutlineStrong);
        return mainPanel;
    }

    private static void CreateCategoryStrip(RectTransform mainPanel)
    {
        RectTransform categoryStrip = CreateRect(mainPanel, "CategoryStrip", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -74f), new Vector2(-290f, -36f));
        float x = 0f;
        for (int i = 0; i < Categories.Length; i++)
        {
            bool isActive = i == 0;
            float width = i < 5 ? 110f : 96f;
            RectTransform chip = CreatePanel(categoryStrip, $"Category_{Categories[i]}", new Rect(x, 0f, width, 34f), isActive ? AccentSoft : new Color(1f, 1f, 1f, 0.02f), isActive ? OutlineStrong : Outline);
            CreateLabel(chip, "Label", new Rect(6f, 5f, width - 12f, 24f), Categories[i], 16f, isActive ? Accent : WhiteSoft, FontStyles.Bold, TextAlignmentOptions.Center);
            x += width + 10f;
        }
    }

    private static void CreateSectionHeader(RectTransform mainPanel)
    {
        RectTransform titleArea = CreateRect(mainPanel, "TitleArea", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -132f), new Vector2(-26f, -82f));
        CreateImage(titleArea, "Separator", new Rect(0f, 44f, 1120f, 2f), new Color(0.45f, 0.78f, 0.95f, 0.24f));
        CreateLabel(titleArea, "PanelTitle", new Rect(0f, 0f, 360f, 42f), "CONVEYORS", 34f, WhiteStrong, FontStyles.Bold, TextAlignmentOptions.Left);
    }

    private static void CreateFilters(RectTransform mainPanel)
    {
        RectTransform filters = CreateRect(mainPanel, "Filters", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -204f), new Vector2(-306f, -140f));

        RectTransform subcategory = CreatePanel(filters, "SubcategoryPanel", new Rect(0f, 0f, 386f, 64f), PanelSoft, Outline);
        CreateLabel(subcategory, "SubcategoryLabel", new Rect(14f, 8f, 170f, 16f), "CHOOSE SUBCATEGORY", 14f, WhiteDim, FontStyles.Bold, TextAlignmentOptions.Left);
        CreateLabel(subcategory, "SubcategoryValue", new Rect(14f, 26f, 220f, 24f), "ALL ITEMS", 20f, WhiteStrong, FontStyles.Bold, TextAlignmentOptions.Left);
        CreateLabel(subcategory, "SubcategoryArrow", new Rect(332f, 22f, 34f, 24f), "v", 22f, Accent, FontStyles.Bold, TextAlignmentOptions.Center);

        RectTransform search = CreatePanel(filters, "SearchPanel", new Rect(406f, 0f, 386f, 64f), PanelSoft, Outline);
        CreateLabel(search, "SearchLabel", new Rect(14f, 8f, 120f, 16f), "SEARCH", 14f, WhiteDim, FontStyles.Bold, TextAlignmentOptions.Left);
        CreateLabel(search, "SearchIcon", new Rect(14f, 26f, 24f, 24f), "Q", 18f, Accent, FontStyles.Bold, TextAlignmentOptions.Center);
        CreateLabel(search, "SearchValue", new Rect(42f, 26f, 220f, 24f), "ITEM NAME", 18f, WhiteSoft, FontStyles.Normal, TextAlignmentOptions.Left);
    }

    private static void CreateShopGrid(RectTransform mainPanel)
    {
        RectTransform contentArea = CreateRect(mainPanel, "ContentArea", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(26f, 24f), new Vector2(-306f, -226f));
        float cardWidth = 412f;
        float cardHeight = 130f;
        float gapX = 16f;
        float gapY = 16f;

        for (int i = 0; i < ShopCards.Length; i++)
        {
            int row = i / 2;
            int column = i % 2;
            float x = column * (cardWidth + gapX);
            float y = contentArea.rect.height - cardHeight - (row * (cardHeight + gapY));
            CreateShopCard(contentArea, ShopCards[i], i + 1, new Rect(x, y, cardWidth, cardHeight));
        }
    }

    private static void CreateBasket(RectTransform mainPanel)
    {
        RectTransform basket = CreatePanel(mainPanel, "BasketPanel", new Rect(880f, 208f, 264f, 458f), new Color(0.05f, 0.09f, 0.15f, 0.78f), Outline);
        CreateEdgeGlow(basket);
        CreateLabel(basket, "BasketIcon", new Rect(16f, 14f, 26f, 26f), "[]", 18f, Accent, FontStyles.Bold, TextAlignmentOptions.Center);
        CreateLabel(basket, "BasketTitle", new Rect(48f, 12f, 120f, 28f), "BASKET", 24f, WhiteStrong, FontStyles.Bold, TextAlignmentOptions.Left);
        CreateImage(basket, "BasketDivider", new Rect(16f, 48f, 232f, 2f), new Color(0.45f, 0.78f, 0.95f, 0.24f));

        for (int i = 0; i < BasketItems.Length; i++)
        {
            float y = 344f - (i * 74f);
            CreateBasketItem(basket, BasketItems[i], i + 1, new Rect(16f, y, 232f, 62f));
        }

        RectTransform emptyHint = CreateRect(basket, "BasketHint", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(18f, 124f), new Vector2(-18f, 166f));
        CreateLabel(emptyHint, "HintLine1", new Rect(0f, 18f, 220f, 18f), "SELECTED ITEMS READY", 16f, WhiteDim, FontStyles.Bold, TextAlignmentOptions.Center);
        CreateLabel(emptyHint, "HintLine2", new Rect(0f, 0f, 220f, 18f), "FOR RAPID CHECKOUT", 14f, WhiteDim, FontStyles.Normal, TextAlignmentOptions.Center);

        RectTransform total = CreateRect(basket, "BasketTotal", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 68f), new Vector2(-16f, 112f));
        CreateLabel(total, "TotalLabel", new Rect(0f, 10f, 70f, 20f), "TOTAL", 18f, WhiteDim, FontStyles.Bold, TextAlignmentOptions.Left);
        CreateLabel(total, "TotalValue", new Rect(112f, 4f, 120f, 28f), "$3,790", 28f, Accent, FontStyles.Bold, TextAlignmentOptions.Right);

        RectTransform buyButton = CreatePanel(basket, "BuyButton", new Rect(16f, 18f, 232f, 40f), new Color(0.10f, 0.44f, 0.62f, 0.90f), new Color(0.44f, 0.95f, 1f, 0.80f));
        CreateLabel(buyButton, "BuyButtonText", new Rect(0f, 8f, 232f, 24f), "BUY NOW", 20f, WhiteStrong, FontStyles.Bold, TextAlignmentOptions.Center);
        CreateImage(buyButton, "BuyButtonGlow", new Rect(8f, 6f, 216f, 28f), new Color(0.20f, 0.88f, 0.98f, 0.18f));
    }

    private static void CreateShopCard(RectTransform parent, ShopCardData data, int index, Rect rect)
    {
        RectTransform card = CreatePanel(parent, $"ShopCard_{index:00}", rect, new Color(0.08f, 0.12f, 0.19f, 0.82f), Outline);
        CreatePanelFrame(card, 8f, new Color(0.55f, 0.88f, 1f, 0.16f));

        RectTransform preview = CreatePanel(card, "Preview", new Rect(10f, 10f, 102f, 110f), new Color(0.05f, 0.08f, 0.12f, 0.94f), new Color(0.52f, 0.82f, 1f, 0.30f));
        CreateImage(preview, "PreviewGlow", new Rect(12f, 14f, 78f, 50f), new Color(0.15f, 0.80f, 0.95f, 0.24f));
        CreateImage(preview, "PreviewBarTop", new Rect(14f, 22f, 64f, 6f), Accent);
        CreateImage(preview, "PreviewBarBottom", new Rect(18f, 40f, 50f, 6f), new Color(0.23f, 0.61f, 1f, 0.80f));
        CreateImage(preview, "PreviewPlate", new Rect(20f, 62f, 58f, 18f), new Color(0.26f, 0.86f, 0.58f, 0.75f));
        CreateLabel(preview, "PreviewTag", new Rect(10f, 86f, 82f, 18f), $"UNIT {data.IndexLabel}", 12f, WhiteDim, FontStyles.Bold, TextAlignmentOptions.Center);

        RectTransform quickBuy = CreatePanel(card, "QuickBuyButton", new Rect(10f, 96f, 102f, 24f), new Color(0.10f, 0.42f, 0.24f, 0.92f), new Color(0.42f, 1.00f, 0.68f, 0.65f));
        CreateLabel(quickBuy, "QuickBuyLabel", new Rect(0f, 4f, 102f, 16f), "QUICK BUY", 12f, WhiteStrong, FontStyles.Bold, TextAlignmentOptions.Center);

        CreateLabel(card, "Title", new Rect(126f, 14f, 236f, 24f), data.Title, 18f, WhiteStrong, FontStyles.Bold, TextAlignmentOptions.Left);
        CreateLabel(card, "Description", new Rect(126f, 42f, 240f, 34f), data.Description, 13f, WhiteSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        CreateLabel(card, "Price", new Rect(126f, 78f, 120f, 28f), data.Price, 24f, Accent, FontStyles.Bold, TextAlignmentOptions.Left);

        RectTransform controls = CreateRect(card, "QuantityControls", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-118f, 14f), new Vector2(-14f, 42f));
        CreateControlButton(controls, "Minus", new Rect(0f, 0f, 26f, 28f), "-", false);
        RectTransform amount = CreatePanel(controls, "AmountPanel", new Rect(32f, 0f, 42f, 28f), new Color(0.03f, 0.05f, 0.09f, 0.94f), new Color(0.52f, 0.82f, 1f, 0.24f));
        CreateLabel(amount, "AmountText", new Rect(0f, 4f, 42f, 20f), "12", 16f, WhiteStrong, FontStyles.Bold, TextAlignmentOptions.Center);
        CreateControlButton(controls, "Plus", new Rect(80f, 0f, 26f, 28f), "+", true);
    }

    private static void CreateBasketItem(RectTransform parent, BasketItemData data, int index, Rect rect)
    {
        RectTransform item = CreatePanel(parent, $"BasketItem_{index:00}", rect, new Color(1f, 1f, 1f, 0.05f), Outline);
        RectTransform thumb = CreatePanel(item, "Thumb", new Rect(8f, 8f, 46f, 46f), new Color(0.05f, 0.08f, 0.12f, 0.94f), new Color(0.52f, 0.82f, 1f, 0.24f));
        CreateImage(thumb, "ThumbGlow", new Rect(8f, 10f, 30f, 18f), new Color(0.18f, 0.86f, 0.95f, 0.22f));
        CreateLabel(item, "Name", new Rect(64f, 12f, 120f, 18f), data.Title.ToUpperInvariant(), 14f, WhiteStrong, FontStyles.Bold, TextAlignmentOptions.Left);
        CreateLabel(item, "Qty", new Rect(184f, 18f, 38f, 18f), data.Quantity, 16f, Accent, FontStyles.Bold, TextAlignmentOptions.Right);
    }

    private static void CreateControlButton(RectTransform parent, string name, Rect rect, string text, bool highlighted)
    {
        RectTransform button = CreatePanel(parent, name, rect, new Color(1f, 1f, 1f, highlighted ? 0.12f : 0.08f), highlighted ? OutlineStrong : Outline);
        CreateLabel(button, "Text", new Rect(0f, 2f, rect.width, rect.height - 4f), text, 18f, WhiteStrong, FontStyles.Bold, TextAlignmentOptions.Center);
    }

    private static void CreateTab(RectTransform parent, string name, Rect rect, string label, bool active)
    {
        RectTransform tab = CreatePanel(parent, name, rect, active ? new Color(1f, 1f, 1f, 0.12f) : new Color(1f, 1f, 1f, 0.04f), active ? OutlineStrong : Outline);
        CreateLabel(tab, "Label", new Rect(0f, 10f, rect.width, 20f), label, 18f, active ? WhiteStrong : WhiteSoft, FontStyles.Bold, TextAlignmentOptions.Center);
        if (active)
        {
            CreateImage(tab, "ActiveGlow", new Rect(6f, 8f, rect.width - 12f, rect.height - 14f), new Color(0.18f, 0.86f, 0.95f, 0.16f));
        }
    }

    private static RectTransform CreatePanel(RectTransform parent, string name, Rect rect, Color fill, Color border)
    {
        RectTransform panel = CreateRect(parent, name, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(rect.x, rect.y), new Vector2(rect.x + rect.width, rect.y + rect.height));
        CreateImage(panel, "Fill", new Rect(0f, 0f, rect.width, rect.height), fill);
        CreateOutline(panel, border);
        return panel;
    }

    private static void CreateOutline(RectTransform parent, Color color)
    {
        CreateImage(parent, "BorderTop", new Rect(0f, parent.rect.height - 1f, parent.rect.width, 1f), color);
        CreateImage(parent, "BorderBottom", new Rect(0f, 0f, parent.rect.width, 1f), color);
        CreateImage(parent, "BorderLeft", new Rect(0f, 0f, 1f, parent.rect.height), color);
        CreateImage(parent, "BorderRight", new Rect(parent.rect.width - 1f, 0f, 1f, parent.rect.height), color);
    }

    private static void CreatePanelFrame(RectTransform parent, float inset, Color color)
    {
        RectTransform frame = CreateRect(parent, "InnerFrame", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(inset, inset), new Vector2(-inset, -inset));
        CreateOutline(frame, color);
    }

    private static void CreateEdgeGlow(RectTransform parent)
    {
        CreateImage(parent, "GlowTop", new Rect(10f, parent.rect.height - 6f, parent.rect.width - 20f, 4f), new Color(0.18f, 0.86f, 0.95f, 0.24f));
        CreateImage(parent, "GlowBottom", new Rect(10f, 2f, parent.rect.width - 20f, 4f), new Color(0.18f, 0.86f, 0.95f, 0.16f));
    }

    private static Image CreateImage(RectTransform parent, string name, Rect rect, Color color)
    {
        RectTransform imageRect = CreateRect(parent, name, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(rect.x, rect.y), new Vector2(rect.x + rect.width, rect.y + rect.height));
        Image image = imageRect.gameObject.AddComponent<Image>();
        image.sprite = s_defaultSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateLabel(RectTransform parent, string name, Rect rect, string text, float size, Color color, FontStyles style, TextAlignmentOptions alignment)
    {
        RectTransform labelRect = CreateRect(parent, name, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(rect.x, rect.y), new Vector2(rect.x + rect.width, rect.y + rect.height));
        TextMeshProUGUI label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = s_font;
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.alignment = alignment;
        label.text = text;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject child = new(name, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static Rect Stretch() => new(0f, 0f, ReferenceCanvasSize.x, ReferenceCanvasSize.y);

    private static void EnsureResources()
    {
        s_defaultSprite ??= Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        s_font ??= TMP_Settings.defaultFontAsset ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    private static void ClearChildren(RectTransform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.Destroy(root.GetChild(i).gameObject);
        }
    }

    private readonly struct ShopCardData
    {
        internal ShopCardData(string title, string description, string price, string indexLabel)
        {
            Title = title;
            Description = description;
            Price = price;
            IndexLabel = indexLabel;
        }

        internal string Title { get; }

        internal string Description { get; }

        internal string Price { get; }

        internal string IndexLabel { get; }
    }

    private readonly struct BasketItemData
    {
        internal BasketItemData(string title, string quantity)
        {
            Title = title;
            Quantity = quantity;
        }

        internal string Title { get; }

        internal string Quantity { get; }
    }
}
